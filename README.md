# StayAwake

A tiny Windows tray app that keeps your PC from going to sleep for a set number of hours — then lets it sleep normally again.

[فارسی](#فارسی)

## Features

- Keep the PC awake for a chosen duration (hours + minutes) or indefinitely
- Optionally keep the display on too
- Countdown shown in the window and in the tray tooltip
- Automatically turns off when time is up (with a notification)
- Closing the window (X) sends it to the system tray; **Exit** from the tray menu quits completely
- Tray menu: show window, start/stop, quick start (1/2/4/8 hours, indefinitely), language, exit
- Bilingual UI: English / فارسی (with right-to-left layout), switchable at runtime; follows the Windows display language on first run
- Single instance — launching it again brings the existing window forward
- Single ~35 KB `.exe`, no installation, no admin rights

## Download

Grab `StayAwake.exe` from the [latest release](../../releases/latest) and run it.
Requires Windows 10/11 (uses the built-in .NET Framework 4.8).

Because the executable is not code-signed, Windows SmartScreen may warn you: click **More info → Run anyway**.

## How it works

StayAwake uses the official Windows API [`SetThreadExecutionState`](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate):

- Start: `ES_CONTINUOUS | ES_SYSTEM_REQUIRED` (+ `ES_DISPLAY_REQUIRED` if "keep display on" is checked)
- Stop / time up / exit: `ES_CONTINUOUS` — clears the request so your normal power settings apply again

It does not simulate keyboard/mouse input and does not change your power plan.
You can verify it while active with `powercfg /requests` (run in Command Prompt).

Note: like any app using this API, it does not prevent sleep caused by closing a laptop lid, pressing the power button, or choosing Sleep manually.

## Build from source

```
dotnet build -c Release
```

Output: `bin/Release/net48/StayAwake.exe`. Builds on Windows, Linux or macOS with the .NET SDK (8+).

## License

MIT

---

<div dir="rtl">

## فارسی

یک برنامهٔ کوچک ویندوزی که کنار ساعت می‌نشیند و برای مدت مشخصی جلوی Sleep شدن کامپیوتر را می‌گیرد، و بعد از آن سیستم دوباره طبق تنظیمات عادی رفتار می‌کند.

### امکانات

- جلوگیری از Sleep برای مدت دلخواه (ساعت و دقیقه) یا به‌صورت نامحدود
- امکان روشن نگه داشتن صفحه‌نمایش
- نمایش زمان باقی‌مانده در پنجره و روی آیکون کنار ساعت
- غیرفعال شدن خودکار بعد از اتمام زمان، همراه با اعلان
- با زدن X برنامه به کنار ساعت (System Tray) می‌رود؛ با «خروج» از منوی راست‌کلیک کاملاً بسته می‌شود
- منوی آیکون: نمایش پنجره، شروع/توقف، شروع سریع (۱، ۲، ۴، ۸ ساعت یا نامحدود)، انتخاب زبان، خروج
- دو زبانه: فارسی (راست‌به‌چپ) و انگلیسی، قابل تغییر در لحظه
- فقط یک نسخه از برنامه اجرا می‌شود
- یک فایل exe حدود ۳۵ کیلوبایت، بدون نصب و بدون نیاز به دسترسی ادمین

### دانلود

فایل `StayAwake.exe` را از [آخرین Release](../../releases/latest) دانلود و اجرا کنید (ویندوز ۱۰ و ۱۱).
اگر SmartScreen هشدار داد: **More info ← Run anyway**.

### نحوهٔ کار

برنامه از API رسمی ویندوز یعنی `SetThreadExecutionState` استفاده می‌کند؛ ورودی کیبورد/ماوس شبیه‌سازی نمی‌کند و تنظیمات Power Plan را تغییر نمی‌دهد. در زمان فعال بودن، با دستور `powercfg /requests` می‌توانید آن را ببینید.

</div>
