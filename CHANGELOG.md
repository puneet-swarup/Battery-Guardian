# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.8.0] - 2026-09-23

### Added
- **Installer scripts** (`install.bat` / `install.ps1` / `uninstall.bat` / `uninstall.ps1`) for a proper per-user installation experience. Users extract the release ZIP and double-click `install.bat`. Files install to `%LocalAppData%\Programs\BatteryGuardian\`, a Start Menu shortcut is created, and the app appears in **Settings → Apps** for clean uninstall.

### Changed
- **Published build is now a folder, not a single file.** Removed `<PublishSingleFile>true</PublishSingleFile>` from the project file. This fixes the `System.DllNotFoundException` crash (`0xC000041D`) that occurred when running the single-file EXE from a user-profile install location (a known .NET/WPF issue where native WPF DLLs cannot be extracted from the bundle in certain paths).

### Removed
- The abandoned WiX Toolset MSI approach — validation errors (ICE38/43/57/64) and the resulting `setup.bat` detour were not worth the complexity for a small utility.

### Notes
- The install/uninstall scripts write only to `HKCU` and `%LocalAppData%` — no admin rights required.
- The release ZIP now contains ~200 files (folder publish) instead of a single EXE. This is the same approach used by VS Code, Discord, and most modern Windows apps.

## [1.7.1] - 2026-09-23

### Removed
- Reverted the optional "blinking tray icon" feature. The Windows 11 shell hides new tray icons inside the overflow menu by default, and the blink effect was not reliably visible there. The native Windows toast notification (added in v1.5.0) already provides strong visual alerting, so the blink was not essential.

### Notes
- The Hardcodet.NotifyIcon.Wpf migration from v1.7.0 remains in place.
- If you'd like the tray icon always visible on the taskbar, drag it out of the overflow menu once, or enable it in **Settings → Personalization → Taskbar → Other system tray icons**.

## [1.7.0] - 2026-09-23

### Changed
- **Tray icon library replaced**: migrated from the raw `WinForms.NotifyIcon` to [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) — a mature, WPF-native library that correctly manages Windows icon handles.
- The tray icon now uses the battery-themed `Assets/app.ico` file instead of the generic Windows application icon.

### Removed
- Deleted the manual GDI bitmap-drawing code that was used to render a colored battery icon dynamically (`UpdateTrayIcon` is now a no-op).

### Notes
- This is Stage A of the tray icon modernization. Stage B (optional blinking during alerts) is planned as a follow-up release.
- Hardcodet correctly handles icon lifetime, so the `0xc000041d` GDI crash we hit in earlier sprints cannot recur.

## [1.6.0] - 2026-09-23

### Added
- **Custom application icon** — a battery-themed icon now appears in the window title bar, the Windows taskbar, and the `.exe` file itself in Windows Explorer. Replaces the generic Windows application icon.

### Notes
- An attempt to migrate to a Fluent Windows 11 theme via WPF-UI was reverted in this session. The library's custom window chrome proved unreliable (missing title bar buttons, backdrop conflicts). The app keeps its plain, proven WPF styling. UI modernization will be revisited in a future sprint with a more careful, one-change-at-a-time approach.

## [1.5.1] - 2026-09-23

### Fixed
- **Reminder alerts now fire the full alert**: previously, the repeat timer only played the beep and spoke the message, but never showed a toast notification. Now the toast + beep + voice fire together on every reminder.
- **Changed repeat interval now takes effect immediately** when a new alert starts, instead of only applying the next time the app restarts.

### Added
- **Test menu items** in the tray right-click menu ("Test High Alert" and "Test Low Alert") for verifying alerts without waiting for a real battery event.

## [1.5.0] - 2026-09-23

### Changed
- **Native Windows toast notifications** — replaced the custom WPF popup and balloon tip with real Windows 10/11 Action Center toasts (the same style used by Outlook, Teams, and WhatsApp).
- Toasts now persist in the Action Center, so you can see them later.
- Clicking a toast brings Battery Guardian's main window to the front (no duplicate instance).

### Added
- `ToastService` — a thin wrapper around `Microsoft.Toolkit.Uwp.Notifications` for sending native toasts.
- Toast activation handling in `App.xaml.cs`.

### Technical
- Target framework bumped from `net10.0-windows` to `net10.0-windows10.0.17763.0` (Windows 10 1809+) to enable the Windows SDK notification APIs.
- Added NuGet package `Microsoft.Toolkit.Uwp.Notifications` v7.1.3.

## [1.4.0] - 2026-09-23

### Added
- **Battery Health display** in the main window: shows the current full-charge capacity vs. original design capacity as a percentage, with a qualitative label (Good / Fair / Poor). Data is sourced from WMI (`BatteryStaticData` and `BatteryFullChargedCapacity`).
- **`BatteryHealthService`** and **`BatteryHealthInfo`** — new classes for querying and representing battery wear data.
- **10 new unit tests** (total: 25) covering battery health calculation and label thresholds.
- GitHub issue templates and pull request template (from `v1.2.0`).

### Notes
- Battery health is queried once per app session (it changes very slowly). Some laptops do not report this data; in that case the UI shows "Battery health: Unavailable".

## [1.3.0] - 2026-09-23

### Added
- **Unit tests** (`BatteryGuardian.Tests` project) using xUnit, covering 15 scenarios for the alert-evaluation logic.
- **`BatteryAlertEvaluator`** — a new pure class that decides when High/Low alerts should be active, fully decoupled from WPF and testable in isolation.

### Changed
- `MainWindow.EvaluateAlerts` now delegates all decision-making to `BatteryAlertEvaluator`. Existing behavior is unchanged, but the code is now maintainable and regression-safe.
- GitHub Actions CI now runs the test suite on every push to `main` and every pull request.

### Developer Notes
- The alert logic is now unit tested. If you're adding a new rule (e.g., "alert only if charger connected for >2 minutes"), write a test first, then implement the change.

## [1.2.1] - 2026-09-23

### Fixed
- Estimated time remaining now uses a hybrid approach: first the native Windows API, then falls back to WMI (`Win32_Battery.EstimatedRunTime`) if the native API returns an unknown value.
- This resolves the "Calculating..." message on laptops whose drivers do not report `BatteryLifeTime` via the standard API.

### Changed
- Tray icon tooltip also benefits from the WMI fallback.
...

## [1.2.0] - 2026-09-23

### Added
- Estimated time remaining (when discharging) and time to full (when charging), shown in the main window.
- Tray icon tooltip now includes battery percentage and charging status at a glance.
- GitHub Issue templates (bug report, feature request) and Pull Request template.
- This `CHANGELOG.md` file.

### Changed
- README updated to document the new time-estimate and tooltip features.

## [1.1.0] - 2026-08-26

### Added
- Configurable repeat alert interval (default: 5 minutes).
- Voice message and beep now repeat until the user acts on the alert.

### Fixed
- Alert now stops immediately when the charger is plugged in or unplugged (uses AC line status instead of the charging flag).
- Prevented the reminder timer from being inadvertently stopped by the other alert's cleared condition.

## [1.0.0] - 2026-08-07

### Added
- Initial public release.
- Real-time battery percentage and charging status display.
- Automatic refresh every 30 seconds and manual refresh button.
- High (95%) and Low (15%) threshold alerts with voice, beep, and toast notification.
- Dynamic system tray icon (color-coded by battery level).
- Settings window for configuring thresholds.
- Auto-start registration with Windows.