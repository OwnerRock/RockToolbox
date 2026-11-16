// R-Downloader - background (service worker / event page)
const api = globalThis.browser || globalThis.chrome;

const BASE_PORT = 53741;
const MAX_PORT = 53750;
const PING_TIMEOUT_MS = 800;
const DEFAULTS = { enabled: true, minSizeMB: 0, extensions: "", port: BASE_PORT };

const handledIds = new Set();
const recentUrls = new Map(); // url -> timestamp, loop guard

// ---------- storage ----------
async function getSettings() {
  try {
    const s = await api.storage.local.get(DEFAULTS);
    return Object.assign({}, DEFAULTS, s);
  } catch (e) {
    return Object.assign({}, DEFAULTS);
  }
}

// ---------- app connection ----------
function fetchWithTimeout(url, opts, ms) {
  const ctl = new AbortController();
  const t = setTimeout(() => ctl.abort(), ms);
  return fetch(url, Object.assign({}, opts, { signal: ctl.signal, cache: "no-store" }))
    .finally(() => clearTimeout(t));
}

async function pingPort(port) {
  const r = await fetchWithTimeout(`http://127.0.0.1:${port}/ping`, { method: "GET" }, PING_TIMEOUT_MS);
  if (!r.ok) throw new Error("bad status");
  const j = await r.json();
  if (!j || j.app !== "RDownloader") throw new Error("not R-Downloader");
  return { port, version: j.version || "" };
}

// Returns {port, version} or null. Remembered port is tried together with the rest.
async function findApp() {
  const { port: saved } = await getSettings();
  const ports = [];
  if (saved >= BASE_PORT && saved <= MAX_PORT) ports.push(saved);
  for (let p = BASE_PORT; p <= MAX_PORT; p++) if (!ports.includes(p)) ports.push(p);

  const found = await new Promise((resolve) => {
    let pending = ports.length;
    ports.forEach((p) => {
      pingPort(p).then(resolve, () => { if (--pending === 0) resolve(null); });
    });
  });
  if (found && found.port !== saved) {
    try { await api.storage.local.set({ port: found.port }); } catch (e) {}
  }
  return found;
}

async function postAdd(port, payload) {
  const r = await fetchWithTimeout(`http://127.0.0.1:${port}/add`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Rock-Ext": "1" },
    body: JSON.stringify(payload)
  }, 5000);
  if (!r.ok) return false;
  const j = await r.json().catch(() => null);
  return !!(j && j.ok);
}

// Ping + add. Returns true only when the app accepted the download.
async function sendToApp(payload) {
  try {
    const app = await findApp();
    if (!app) return false;
    return await postAdd(app.port, payload);
  } catch (e) {
    return false;
  }
}

// ---------- helpers ----------
function isHttp(url) {
  return typeof url === "string" && /^https?:\/\//i.test(url);
}

function isHttpOrMagnet(url) {
  return typeof url === "string" && /^(https?:\/\/|magnet:\?)\S+$/i.test(url);
}

function parseExtList(str) {
  return String(str || "")
    .split(",")
    .map((s) => s.trim().toLowerCase().replace(/^\*?\./, ""))
    .filter(Boolean);
}

function extOf(item) {
  const cands = [];
  if (item.filename) cands.push(String(item.filename).split(/[\\/]/).pop());
  try {
    cands.push(decodeURIComponent(new URL(item.finalUrl || item.url).pathname.split("/").pop()));
  } catch (e) {}
  for (const c of cands) {
    const m = /\.([A-Za-z0-9]{1,10})$/.exec(c || "");
    if (m) return m[1].toLowerCase();
  }
  return "";
}

function baseName(path) {
  return path ? String(path).split(/[\\/]/).pop() : "";
}

function passesFilters(item, s) {
  const size = typeof item.fileSize === "number" && item.fileSize > 0 ? item.fileSize
    : (typeof item.totalBytes === "number" && item.totalBytes > 0 ? item.totalBytes : -1);
  const minBytes = (parseFloat(s.minSizeMB) || 0) * 1024 * 1024;
  if (minBytes > 0 && size >= 0 && size < minBytes) return false;
  const list = parseExtList(s.extensions);
  if (list.length && !list.includes(extOf(item))) return false;
  return true;
}

// ---------- badge / notification ----------
function actionApi() { return api.action || api.browserAction; }

async function refreshBadge() {
  const s = await getSettings();
  const a = actionApi();
  if (!a) return;
  try {
    await a.setBadgeText({ text: s.enabled ? "ON" : "OFF" });
    await a.setBadgeBackgroundColor({ color: s.enabled ? "#2f9e8f" : "#6b7785" });
  } catch (e) {}
}

let flashTimer = null;
async function flashBadge() {
  const a = actionApi();
  if (!a) return;
  try {
    await a.setBadgeText({ text: "OK" });
    await a.setBadgeBackgroundColor({ color: "#4da3c4" });
  } catch (e) {}
  clearTimeout(flashTimer);
  flashTimer = setTimeout(refreshBadge, 2000);
}

function notify(message) {
  try {
    const p = api.notifications.create({
      type: "basic",
      iconUrl: "icons/icon128.png",
      title: "R-Downloader",
      message
    });
    if (p && p.then) p.then((id) => setTimeout(() => { try { api.notifications.clear(id); } catch (e) {} }, 3000), () => {});
  } catch (e) {}
}

function sentFeedback() {
  flashBadge();
  notify("Sent to R-Downloader");
}

// ---------- downloads ----------
api.downloads.onCreated.addListener(async (item) => {
  try {
    if (handledIds.has(item.id)) return;
    // only downloads that were started just now: never old / restored / finished ones from the browser's history
    if (item.state && item.state !== "in_progress") return;
    if (item.startTime) { const age = Date.now() - Date.parse(item.startTime); if (age > 20000) return; }
    if (item.exists === true && item.state === "complete") return;
    const s = await getSettings();
    if (!s.enabled) return;

    const url = item.finalUrl && isHttp(item.finalUrl) ? item.finalUrl : item.url;
    if (!isHttp(url)) return; // blob:, data:, filesystem:, chrome-extension:, ...
    if (!passesFilters(item, s)) return;

    const now = Date.now();
    for (const [u, t] of recentUrls) if (now - t > 10000) recentUrls.delete(u);
    if (recentUrls.has(url)) return;

    handledIds.add(item.id);
    const size = typeof item.fileSize === "number" && item.fileSize > 0 ? item.fileSize
      : (typeof item.totalBytes === "number" && item.totalBytes > 0 ? item.totalBytes : -1);

    const ok = await sendToApp({
      url,
      filename: baseName(item.filename),
      referrer: item.referrer || "",
      mime: item.mime || "",
      size,
      userAgent: navigator.userAgent
    });

    if (!ok) { handledIds.delete(item.id); return; } // app not running: let the browser download

    recentUrls.set(url, now);
    try { await api.downloads.cancel(item.id); } catch (e) {}
    try { await api.downloads.erase({ id: item.id }); } catch (e) {}
    sentFeedback();
  } catch (e) {
    // never break the browser download on error
  }
});

// ---------- context menu ----------
const MENU_ID = "rocktoolbox-download-link";

async function setupMenu() {
  try { await api.contextMenus.removeAll(); } catch (e) {}
  try {
    api.contextMenus.create({
      id: MENU_ID,
      title: "Download with R-Downloader",
      contexts: ["link"]
    }, () => { void api.runtime.lastError; });
  } catch (e) {}
}

api.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== MENU_ID || !isHttp(info.linkUrl)) return;
  const ok = await sendToApp({
    url: info.linkUrl,
    filename: "",
    referrer: info.pageUrl || (tab && tab.url) || "",
    mime: "",
    size: -1,
    userAgent: navigator.userAgent
  });
  if (ok) sentFeedback();
  else notify("R-Downloader is not running");
});

// ---------- messages from popup ----------
api.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg && msg.type === "status") {
    findApp().then(
      (app) => sendResponse({ connected: !!app, version: app ? app.version : "", port: app ? app.port : null }),
      () => sendResponse({ connected: false })
    );
    return true; // async response
  }
  if (msg && msg.type === "add" && isHttpOrMagnet(msg.url)) {
    sendToApp({ url: msg.url, filename: "", referrer: "", mime: "", size: -1, userAgent: navigator.userAgent })
      .then((ok) => sendResponse({ ok: !!ok }), () => sendResponse({ ok: false }));
    return true; // async response
  }
  return false;
});

// ---------- lifecycle ----------
api.runtime.onInstalled.addListener(() => { setupMenu(); refreshBadge(); });
if (api.runtime.onStartup) api.runtime.onStartup.addListener(() => { refreshBadge(); });
api.storage.onChanged.addListener((changes, area) => {
  if (area === "local" && changes.enabled) refreshBadge();
});
refreshBadge();
