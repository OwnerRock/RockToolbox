const api = globalThis.browser || globalThis.chrome;
const $ = (id) => document.getElementById(id);
const DEFAULTS = { enabled: true, minSizeMB: 0, extensions: "" };

async function load() {
  let s = DEFAULTS;
  try { s = Object.assign({}, DEFAULTS, await api.storage.local.get(DEFAULTS)); } catch (e) {}
  $("enabled").checked = !!s.enabled;
  $("minSize").value = s.minSizeMB > 0 ? s.minSizeMB : "";
  $("exts").value = s.extensions || "";
  renderPower();
}

function renderPower() {
  const on = $("enabled").checked;
  $("powerText").textContent = on ? "Enabled" : "Disabled";
  document.querySelector(".power").classList.toggle("on", on);
}

let savedTimer = null;
function flashSaved() {
  const el = $("saved");
  el.classList.add("show");
  clearTimeout(savedTimer);
  savedTimer = setTimeout(() => el.classList.remove("show"), 1200);
}

async function save() {
  const min = parseFloat($("minSize").value);
  const data = {
    enabled: $("enabled").checked,
    minSizeMB: isFinite(min) && min > 0 ? min : 0,
    extensions: $("exts").value.trim()
  };
  try { await api.storage.local.set(data); flashSaved(); } catch (e) {}
}

let debounce = null;
function saveSoon() { clearTimeout(debounce); debounce = setTimeout(save, 350); }

function askStatus() {
  return new Promise((resolve) => {
    let done = false;
    const finish = (v) => { if (!done) { done = true; resolve(v); } };
    setTimeout(() => finish({ connected: false }), 4000);
    try {
      const p = api.runtime.sendMessage({ type: "status" }, (r) => {
        void (api.runtime.lastError);
        finish(r);
      });
      if (p && p.then) p.then(finish, () => finish({ connected: false }));
    } catch (e) { finish({ connected: false }); }
  });
}

async function refreshStatus() {
  const r = await askStatus();
  const box = $("status");
  const ok = !!(r && r.connected);
  box.classList.toggle("ok", ok);
  box.classList.toggle("bad", !ok);
  $("statusText").textContent = ok
    ? "Connected" + (r.version ? " (v" + r.version + ")" : "")
    : "R-Downloader is not running";
}

$("enabled").addEventListener("change", () => { renderPower(); save(); });
$("minSize").addEventListener("input", saveSoon);
$("exts").addEventListener("input", saveSoon);

// ---- download a link by hand (for sites the browser does not hand over) ----
function linkMsg(text, ok) {
  const el = $("linkMsg");
  el.textContent = text;
  el.classList.remove("ok", "bad");
  el.classList.add(ok ? "ok" : "bad", "show");
  clearTimeout(linkMsg.t);
  linkMsg.t = setTimeout(() => el.classList.remove("show"), 3500);
}

async function pasteLink() {
  let t = "";
  try { t = await navigator.clipboard.readText(); } catch (e) {}
  t = (t || "").trim();
  if (!t) { $("linkUrl").focus(); linkMsg("Clipboard is empty - press Ctrl+V in the box.", false); return; }
  $("linkUrl").value = t;
  $("linkUrl").focus();
}

function sendLink() {
  const url = $("linkUrl").value.trim();
  if (!/^(https?:\/\/|magnet:\?)\S+$/i.test(url)) { linkMsg("Enter a link that starts with http://, https:// or magnet:", false); return; }
  const btn = $("btnSend");
  btn.disabled = true;
  let done = false;
  const finish = (r) => {
    if (done) return; done = true;
    btn.disabled = false;
    if (r && r.ok) { linkMsg("Sent to R-Downloader", true); $("linkUrl").value = ""; }
    else linkMsg("R-Downloader is not running. Start it and try again.", false);
  };
  setTimeout(() => finish(null), 6000);
  try {
    const p = api.runtime.sendMessage({ type: "add", url }, (r) => { void (api.runtime.lastError); finish(r); });
    if (p && p.then) p.then(finish, () => finish(null));
  } catch (e) { finish(null); }
}

$("btnPaste").addEventListener("click", pasteLink);
$("btnSend").addEventListener("click", sendLink);
$("linkUrl").addEventListener("keydown", (e) => { if (e.key === "Enter") sendLink(); });

load();
refreshStatus();
setInterval(refreshStatus, 3000);
