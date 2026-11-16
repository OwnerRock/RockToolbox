<div align="center">

<img src="../images/logo.png" width="112" alt="شعار Rock Toolbox">

# ROCK TOOLBOX

### صندوق أدوات ويندوز واحد لـ **تثبيت وتحسين وتنظيف وإصلاح وتحديث** كل حاجة.

برنامج C# أصلي: مش محتاج PowerShell علشان يشتغل. ٤٣٥ برنامج، ١٢ لغة، ونافذة واحدة نظيفة.

<br>

[![Version](https://img.shields.io/badge/version-4.1.0-4FB7A8?style=for-the-badge)](https://github.com/OwnerRock/RockToolbox/releases)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white)](#المتطلبات)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](#المتطلبات)
[![Languages](https://img.shields.io/badge/languages-12-FF375F?style=for-the-badge)](#اللغات)
[![License](https://img.shields.io/badge/license-MIT-2EA043?style=for-the-badge)](#الترخيص)
[![Downloads](https://img.shields.io/github/downloads/OwnerRock/RockToolbox/total?style=for-the-badge&label=downloads&color=4FB7A8)](https://github.com/OwnerRock/RockToolbox/releases)

**[⬇ تحميل](https://github.com/OwnerRock/RockToolbox/releases/latest)** ·
**[✨ المميزات](#المميزات)** ·
**[🛠 التثبيت](#التثبيت)** ·
**[📜 سجل التغييرات](../../CHANGELOG.md)** ·
**[❓ الأسئلة الشائعة](#الأسئلة-الشائعة)**

🌍 [English](../../README.md) · **العربية** · [Français](README.fr.md) · [Español](README.es.md) · [Deutsch](README.de.md) · [Türkçe](README.tr.md) · [Русский](README.ru.md) · [中文](README.zh.md) · [日本語](README.ja.md) · [Português](README.pt.md) · [Italiano](README.it.md) · [हिन्दी](README.hi.md)

<br>

<img src="../images/install-modern.png" width="860" alt="صفحة Install بالشكل Modern">

</div>

---

<div dir="rtl">

## ليه Rock Toolbox؟

تجهيز جهاز ويندوز أو تنظيفه معناه عادة عشر أدوات مختلفة وكومة سكربتات وكتير من التخمين. Rock Toolbox بيجمع كل ده في **نافذة واحدة سريعة وأصلية**، وبيشرح كل خيار قبل ما تطبّقه، وبيعمل **نقطة استعادة** قبل أي حاجة فيها خطورة.

| | |
|---|---|
| ⚡ **أصلي وخفيف** | مكتوب بـ C# (WPF). من غير نافذة كونسول ومن غير .NET SDK، و PowerShell مش من متطلبات البرنامج. |
| 🧰 **كل حاجة في مكان واحد** | تثبيت وتحديث وحذف البرامج، تحسين وتنظيف وإصلاح ويندوز، وإدارة التعريفات والبدء مع النظام. |
| 🛡 **آمن افتراضياً** | نقطة استعادة تلقائية قبل الإجراءات الخطرة، تراجع عن التحسينات، ونتائج تنظيف صادقة. |
| 🎨 **شكلين** | **Modern** (ملوّن ومستدير) أو **Classic** (أزرق نظيف)، فاتح أو داكن، وأي لون مميّز. التغييرات بتتطبّق **فوراً** من غير إعادة تشغيل. |
| 🌍 **١٢ لغة** | عربي بدعم كامل لاتجاه RTL، وإنجليزي، وفرنسي، وإسباني، وألماني، وتركي، وروسي، وصيني، وياباني، وبرتغالي، وإيطالي، وهندي. |
| 🧲 **بيشتغل من التراي** | اقفل النافذة وهيكمل شغل في الخلفية. |
| 📦 **مثبّت واحد** | ملف Setup واحد بيثبّت Rock Toolbox، وبشكل اختياري **R-Downloader** و winget و Chocolatey. |

## المحتويات

- [المميزات](#المميزات)
- [كل الصفحات](#كل-الصفحات)
- [التعريفات Drivers](#التعريفات-drivers)
- [تحسين الألعاب](#تحسين-الألعاب)
- [التنظيف Cleanup](#التنظيف-cleanup)
- [R-Downloader](#r-downloader)
- [اللغات](#اللغات)
- [التثبيت](#التثبيت)
- [المتطلبات](#المتطلبات)
- [طريقة الاستخدام](#طريقة-الاستخدام)
- [التراي وسطر الأوامر](#التراي-وسطر-الأوامر)
- [الإعدادات](#الإعدادات)
- [مكان حفظ البيانات](#مكان-حفظ-البيانات)
- [الأسئلة الشائعة](#الأسئلة-الشائعة)
- [المساهمة](#المساهمة)
- [الشكر والتقدير](#الشكر-والتقدير)
- [ملاحظات أمنية](#ملاحظات-أمنية)
- [الترخيص](#الترخيص)

## المميزات

- **٤٣٥ برنامج** في ١٢ تصنيف، بيتثبّتوا عن طريق **winget** أو **Chocolatey** (بزرار واحد)، مع الأيقونات وحجم التحميل وعرض *Package ID* و *Show Installed*.
- **Winget Upgrade** و **Uninstall Apps** في نفس البرنامج.
- **Tweaks و Customize** مع اختيارات موصى بها، ووصف لكل تحسين، وإمكانية التراجع.
- **Debloat**: حذف تطبيقات ويندوز المدمجة، مع أدوات إزالة كاملة لـ **OneDrive و Edge و Internet Explorer** (وإمكانية الاسترجاع).
- **Config**: ميزات ويندوز، إصلاحات، لوحات قديمة، OpenSSH، حزمة مؤشرات داكنة، فحص التفعيل، اختصار سطح المكتب، وقسم **الألعاب**.
- التحكم في **Windows Update**، و **Drivers**، و **Startup**، و **Repair**، و **Cleanup**.
- **System Info** مع عرض لحظي.
- **Wallpapers**: بحث على الإنترنت، خلفية Bing اليومية، و **My pictures** من فولدراتك.
- لوحة **God Mode** وتنقل **للخلف / للأمام** (`Alt+Left` و `Alt+Right` وأزرار الماوس الجانبية).
- **تحديث ذاتي**: بيفحص آخر إصدار على GitHub وبيثبّته لك.

## كل الصفحات

| المجموعة | الصفحة | وظيفتها |
|---|---|---|
| **Apps** | **Install** | كتالوج برامج بالأيقونات والأحجام، winget / Chocolatey، التثبيت بـ Package ID، ولوحة **Your list**، و Show Installed. |
| | **Updates** | ترقية البرامج عن طريق winget. |
| | **Uninstall Apps** | حذف البرامج المثبّتة. |
| **System** | **Tweaks** | تحسينات ويندوز، التخصيص، خطة الطاقة، DNS. |
| | **Debloat** | حذف التطبيقات المدمجة، وأدوات إزالة OneDrive / Edge / Internet Explorer. |
| | **Config** | الميزات، الإصلاحات، OpenSSH، المؤشرات، التفعيل، الاختصار، الألعاب. |
| | **Windows Update** | إدارة تحديثات ويندوز. |
| | **Drivers** | فحص الجهاز والمقارنة مع Microsoft Update Catalog، تثبيت أو حذف. |
| | **Startup** | إدارة برامج بدء التشغيل. |
| **Maintenance** | **Cleanup** | تفريغ مساحة وعرض اللي اتمسح فعلاً. |
| | **Repair** | أدوات إصلاح النظام. |
| | **System Info** | تفاصيل الجهاز والنظام بعرض لحظي. |
| **More** | **Wallpapers** | بحث، خلفية Bing يومية، وصورك الخاصة. |
| | **God Mode** | كل إعدادات ويندوز المخفية في قائمة واحدة. |

## التعريفات Drivers

بيفحص Rock Toolbox أجهزتك ويقارن إصدارات التعريفات المثبّتة مع **Microsoft Update Catalog**، ويعرض كل تحديث بصيغة **"من X إلى Y"**.

1. بيقرأ الإصدار والتاريخ المثبّت من WMI (`Win32_PnPSignedDriver`).
2. بيدوّر على Hardware ID في الـ Update Catalog.
3. بينزّل ملف `.cab` ويفكّه بـ `expand.exe` ويثبّته بـ `pnputil`، **بعد إنشاء نقطة استعادة**.
4. بيحفظ النتيجة مؤقتاً ٦ ساعات في `driver-cache.json`.

البرنامج **مش** بيستخدم Windows Update API عن قصد، فممكن تختلف النتايج عن صفحة Windows Update. وتقدر كمان **تحذف** حزم تعريفات الأطراف الثالثة من نفس الصفحة.

## تحسين الألعاب

موجود في **Config > Gaming**. كل قيمة بتتغيّر بتتحفظ الأول (`gameboost.bak`)، فزرار **Restore** بيرجّع كل حاجة زي ما كانت بالظبط.

- **الكمبيوتر المكتبي**: تشغيل Game Mode، إيقاف تسجيل الألعاب، تشغيل GPU scheduling، إيقاف تسارع الماوس، أولوية CPU/GPU للألعاب، وخطة طاقة High performance.
- **اللابتوب**: نفس الشيء لكن من غير تغيير خطة الطاقة؛ السرعة الكاملة والتبريد النشط بيشتغلوا فقط أثناء الشحن، فالبطارية متتأثرش.
- **Game Loop / Tencent Gaming Buddy**: كارت شاشة عالي الأداء، أولوية CPU عالية، إيقاف fullscreen optimizations، مع أفضل إعدادات داخل اللعبة لجهازك في السجل.

## التنظيف Cleanup

بينظّف الملفات المؤقتة والمخلّفات بالتوازي، ويعرض **النتيجة الحقيقية** لكل درايف: الحجم قبل وبعد، المساحة اللي اتفضّت، وإيه اللي كان قيد الاستخدام، وإيه اللي رفض ويندوز يمسحه.

## R-Downloader

من الإصدار 4.1.0 مدير التنزيلات بقى برنامج مستقل: **R-Downloader**.

<div align="center">
<img src="../images/download-window.png" width="780" alt="نافذة R-Downloader الرئيسية">
</div>

- تنزيل متعدد الاتصالات (لحد ٣٢)، استكمال، طوابير، حد للسرعة، جدولة، وإعادة محاولة تلقائية.
- تورنت وروابط magnet (عن طريق aria2).
- قوائم **Rlist**: ملف نصي بالروابط تعدّله في Notepad وتفتحه بدبل كليك.
- إضافة للمتصفح لـ **Edge و Chrome و Brave و Opera و Firefox**.
- Grabber، مراقبة الحافظة، وإجراء *بعد انتهاء كل التنزيلات* (إيقاف، سكون، إسبات، خروج).
- شريط قوائم، نافذة خيارات بـ ١١ تبويب، أصوات خاصة، وأيقونة في التراي.

علّم على **Install R-Downloader** في صفحة *Select Additional Tasks* أثناء تثبيت Rock Toolbox. أول ما يشتغل بينسخ الشكل واللغة وقائمة التنزيلات من Rock Toolbox. التفاصيل في [`RDownloader/README.md`](../../RDownloader/README.md).

## اللغات

غيّر اللغة في أي وقت من قائمة الترس أو من جولة الترحيب أول تشغيل. التغيير بيتطبّق **فوراً**، والعربية بتستخدم تخطيط من اليمين لليسار كامل.

| | اللغة | الكود | | | اللغة | الكود |
|---|---|---|---|---|---|---|
| 🇬🇧 | English | `en` | | 🇷🇺 | Русский | `ru` |
| 🇪🇬 | العربية | `ar` | | 🇨🇳 | 中文 | `zh` |
| 🇫🇷 | Français | `fr` | | 🇯🇵 | 日本語 | `ja` |
| 🇪🇸 | Español | `es` | | 🇧🇷 | Português | `pt` |
| 🇩🇪 | Deutsch | `de` | | 🇮🇹 | Italiano | `it` |
| 🇹🇷 | Türkçe | `tr` | | 🇮🇳 | हिन्दी | `hi` |

### إضافة ترجمة أو تحسينها

اللغة عبارة عن ملف JSON صغير في مجلد [`lang/`](../../lang) (نص إنجليزي ← نص مترجم، ٦١١ نص).

1. انسخ `lang/ar.json` إلى `lang/<code>.json` وترجم **القيم** فقط (سيب المفاتيح زي ما هي بالظبط).
2. أضف اللغة في القائمة داخل `src/Core/Lang.cs`.
3. افتح Pull Request. رسائل السجل بتفضل بالإنجليزي عن قصد.

## التثبيت

### الطريقة ١: المثبّت (موصى بها)

1. افتح صفحة [Releases](https://github.com/OwnerRock/RockToolbox/releases/latest).
2. نزّل `RockToolbox-Setup-4.1.0.exe` وشغّله.
3. اختار الإنجليزي أو العربي، علّم على اللي تحتاجه (أيقونة سطح المكتب، التثبيت في شريط المهام، التشغيل مع ويندوز، **R-Downloader**) وكمّل.

بيتثبّت winget (App Installer) و Chocolatey بشكل هادئ لو مش موجودين.

### الطريقة ٢: البناء من المصدر

1. حط الـ ٣ ملفات المذكورة في [`redist/README.txt`](../../redist/README.txt) داخل مجلد `redist` (اختياري، لتضمين winget و Chocolatey).
2. ثبّت **Inno Setup 6.6 أو أحدث**.
3. افتح `installer/RockToolbox.iss` واضغط **F9**.
4. هتلاقي الـ Setup في `out/RockToolbox-Setup-4.1.0.exe`.

السكربت بيبني البرنامج بمترجم C# المدمج في ويندوز (`csc.exe` من .NET Framework 4) من مجلدي `src/` و `daily/`. مش محتاج Visual Studio ولا .NET SDK. لو فشل، اقرأ `%TEMP%\RockToolboxBuild\build.log`.

## المتطلبات

- ويندوز 10 أو 11، **64-bit**
- .NET Framework 4.x (موجود مع ويندوز)
- صلاحيات Administrator (البرنامج بيطلبها من الـ manifest)
- إنترنت للتثبيت والتحديث والتعريفات والخلفيات
- winget لصفحتي Install / Updates (بيتثبّت تلقائياً لو ناقص)، و Chocolatey اختياري

## طريقة الاستخدام

**تثبيت برامج** ← افتح **Install**، علّم على البرامج، واضغط تثبيت. استخدم **Show Installed** لمعرفة اللي عندك.
**تحديث البرامج** ← **Updates** ثم شغّل winget upgrade.
**حذف البرامج** ← **Uninstall Apps**، اختار، احذف.
**تحديث التعريفات** ← **Drivers**، افحص، راجع قائمة "من X إلى Y"، حدّث.
**تفريغ مساحة** ← **Cleanup**، شغّله، واقرأ نتيجة المساحة المتفضّية.
**تحسين وتنظيف وإصلاح** ← **Tweaks** و **Debloat** و **Repair**: كل صفحة بتشرح خياراتها قبل التطبيق.

## التراي وسطر الأوامر

- قفل النافذة بيخلّي Rock Toolbox شغال في التراي. قائمة التراي: **Open** و **Run at startup** و **Exit**.
- فتح البرنامج تاني بيجيب النافذة الشغالة قدّام.
- الأوامر: `--background` و `--quit` و `--enable-startup` و `--disable-startup` و `--pin-taskbar` و `--unpin-taskbar`.

## الإعدادات

افتح **Settings** أو قائمة الترس. كل حاجة بتتطبّق فوراً وبتتحفظ تلقائياً.

- اللغة، فاتح / داكن، اللون المميّز، شكل **Classic / Modern**
- حجم الخط ومقياس الواجهة
- لون أو صورة الخلفية مع الشفافية والـ blur
- حركة التمرير، الانتقال بين الصفحات، حركة الكروت
- فوق كل النوافذ، البدء مكبّراً، فتح آخر تبويب
- الاستمرار في التراي، التشغيل مع ويندوز
- نقطة استعادة تلقائية، تثبيت winget / Chocolatey تلقائياً، فحص التحديثات عند التشغيل
- اسم وصورة الملف الشخصي، تصدير / استيراد اختياراتك

## مكان حفظ البيانات

`%LOCALAPPDATA%\RockToolbox`

| الملف | المحتوى |
|---|---|
| `settings.json` | إعداداتك |
| `native.log` | سجل النشاط والأخطاء (افتحه من **Show logs**) |
| `error.log` | تفاصيل أخطاء التشغيل، مفيد لتقارير المشاكل |
| `icons/` | أيقونات البرامج المخزّنة مؤقتاً |
| `driver-cache.json` | ذاكرة فحص التعريفات المؤقتة |
| `gameboost.bak` | القيم السابقة لزرار **Restore** في قسم الألعاب |

برنامج الإزالة بيعرض عليك حذف المجلد ده.

## الأسئلة الشائعة

<details>
<summary><b>"The remote name could not be resolved" (خطأ DNS)</b></summary>

الـ DNS عندك مش قادر يحل العنوان. استخدم زرار **Fix DNS** (بيظهر في نافذة التحديث وفي Wallpapers) أو اضبط DNS الخاص بـ Cloudflare يدوياً على `1.1.1.1`، وجرّب تاني.
</details>

<details>
<summary><b>مكافح الفيروسات بيمنع البرنامج أو بيحذّر منه</b></summary>

Rock Toolbox بيغيّر إعدادات النظام وبيحذف تطبيقات، فبعض برامج الحماية بتتحفّظ. ابنِ البرنامج من المصدر للتأكد من الكود، أو أضف استثناء لمجلد التثبيت.
</details>

<details>
<summary><b>حاجة مش شغّالة</b></summary>

شغّل Rock Toolbox **كـ Administrator**: تحسينات كتير وعمليات تثبيت محتاجاه. بعدها افتح **Show logs** (Errors only) وأرفق الأسطر مع بلاغك.
</details>

<details>
<summary><b>winget مش موجود</b></summary>

Rock Toolbox بيثبّته لك لو خيار *Install winget / Chocolatey automatically if missing* شغّال. غير كده ثبّت أو حدّث **App Installer** من Microsoft Store.
</details>

<details>
<summary><b>قائمة التعريفات مختلفة عن Windows Update</b></summary>

ده مقصود: صفحة Drivers بتستخدم Microsoft Update Catalog عن طريق Hardware ID، مش Windows Update API.
</details>

<details>
<summary><b>فين صفحة Downloads؟</b></summary>

اتنقلت إلى **R-Downloader** في الإصدار 4.1.0. شوف قسم [R-Downloader](#r-downloader).
</details>

<details>
<summary><b>فين ملف السجل؟</b></summary>

`%LOCALAPPDATA%\RockToolbox\native.log` (و `error.log` لأخطاء بدء التشغيل).
</details>

## المساهمة

البلاغات وطلبات الدمج مرحّب بيها في [github.com/OwnerRock/RockToolbox](https://github.com/OwnerRock/RockToolbox).

1. اعمل Fork وأنشئ Branch.
2. ابنِ البرنامج بـ `installer/RockToolbox.iss` وجرّبه على ويندوز 10 و 11.
3. اشرح اللي اتغيّر. وفي بلاغات الأخطاء أرفق أسطر **Show logs**.

الترجمات الجديدة وتصحيحات الترجمة مرحّب بيها جداً (شوف [اللغات](#اللغات)).

## الشكر والتقدير

- **OwnerRock** (محمد، مصر)، مطوّر Rock Toolbox. تيليجرام: [@OwnerRock](https://t.me/OwnerRock)
- **SpotX** سكربت طرف ثالث من [SpotX-Official](https://github.com/SpotX-Official). زرار **Config > SPOTX** بيشغّل أمر المطوّرين الرسمي في نافذة PowerShell ظاهرة.
- **aria2** هو اللي بيشغّل التورنت في R-Downloader.

## ملاحظات أمنية

- البرنامج بيشتغل كـ **Administrator**: شغّل فقط إصدارات تثق فيها (ابنِه من المصدر لو عندك شك).
- زرار **SPOTX** بينزّل ويشغّل سكربت SpotX الرسمي على طول (من غير تأكيد إضافي)، فاضغطه بس لو عايز ده.
- بعض الأدوات المساعدة لسه بتستدعي Windows PowerShell: SpotX، وتجهيز Chocolatey، وخطوة واحدة في Cleanup، والبديل الاحتياطي في Debloat.
- الإجراءات الخطرة بتعمل نقطة استعادة الأول (تقدر توقفها من Settings).

## الترخيص

MIT. أضف ملف `LICENSE` بالنص الكامل قبل أول إصدار عام.

</div>

---

<div align="center">

**لو Rock Toolbox وفّر عليك وقت، حط له ⭐ على GitHub.**

صُنع بعناية بواسطة [OwnerRock](https://github.com/OwnerRock)

</div>
