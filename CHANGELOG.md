# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.4.5] - 2026-11-21

### Changed
- **Snooze now says what it does.** Snoozing has always silenced the alerts while leaving smart-plug control running - "stop telling me, keep protecting me" - but nothing said so, and the opposite reading is just as reasonable. The snooze menu items carry that sentence as a tooltip, the smart-plug guide has a section on it, and the code comments at both call sites record the decision so it is not quietly reversed later.

## [2.4.4] - 2026-11-21

### Fixed (smart plug)
- **Switching the charger back on never happened.** The decision refused to act unless it had first read the relay state successfully, and treated an unknown state as "do nothing". Cutting the charger at the high threshold worked because the plug was on and answering; hours later, with the plug idle, the state read came back unknown and the app sat there - so the plug had to be turned back on by hand. The battery level now decides the action and the relay state only decides whether the command is redundant: turning an already-on plug on is harmless, and so is the reverse. The unused `Query` action is gone.

## [2.4.3] - 2026-11-21

### Changed
- **The smart plug is now visible where users actually look.** It shipped in 2.4.1 and appeared in neither About screen nor the top-level README, so the only way to discover that the app can switch a charger was to read the changelog. Both About screens now list what the app does and offer a plain search link explaining what a Tuya smart plug is, and the README has a short section pointing at the setup guide.

## [2.4.2] - 2026-11-21

### Fixed (Windows)
- **Settings could lose fields without a word.** The dialog rebuilt the settings object property by property, so any field added later was dropped on every save - which is why the smart-plug configuration kept resetting. It now clones the object, and tests fail if a writable property is missing from the clone or from the JSON round-trip.
- **The update check only ran at startup.** An app left running in the tray for a week never looked again, despite promising a daily check. The 24-hour rule is now a tested policy, checked on the refresh tick as well.

### Fixed (Android)
- **The automatic update check never ran.** `UpdateCheckReceiver` is declared in the manifest and nothing ever sent it the broadcast - no alarm, no worker, no caller. The service now triggers it from the battery broadcast, gated by a tested 24-hour policy.

### Fixed (smart plug)
- **The plug only switched when the settings were saved.** One evaluation could hold the controller for up to two minutes of retries, every 30-second reading added another waiter, and the queue never drained - so the app appeared to ignore the battery entirely until a save rebuilt the controller. Evaluations are now skipped when one is already running, and a single evaluation is capped at 20 seconds so it always finishes inside a refresh. Android had the same shape: a thread per reading, queued on one lock.

## [2.4.1] - 2026-11-21

### Fixed (Windows)
- **The smart plug had no settings screen.** The configuration could only be edited by hand in the JSON file, and the settings dialog rebuilt the object field by field without copying the eight plug fields, so opening and saving Settings silently reset them. The window now has a Smart Plug section - enable, device ID, IP, port, relay data point, both thresholds, the local key, and a Test connection button that reports what the plug answered.
- A change to the smart-plug settings now takes effect immediately; the cached controller was reused with the old configuration until the app restarted.



### Fixed (smart plug)
- The two plug levels now have to be at least 5 points apart. Nothing stopped a 96 / 95 setting, which made the relay click on and off once per percent, wearing the contacts and cycling the charger. Enforced in the controller configuration on both platforms and in the settings screen before you save.

## [2.4.0] - 2026-11-21

### Changed (Windows)
- **Smart plug control now uses the Tuya LAN SDK.** `TuyaSmartPlug` is an adapter over `TuyaLan.TuyaLocalDevice` (NuGet `TuyaLan` 2.0.0) instead of a private copy of the protocol. The copy was built from a description that does not match real 3.5 firmware - a frame layout without the reserved bytes, IV, tag or footer; the 3.4 session-key derivation on a 3.5 device; control on `0x07` with a bare `dps` body; and the status query on `0x08`, which is a push. It could not have switched a physical plug. Removed: `TuyaCrypto`, `TuyaMessageCodec`, `TuyaMessageCipher`, `TuyaSession`, `TuyaSession_Messaging` and their two test files.

### Changed (Android)
- **Smart plug control now uses the Tuya LAN SDK**, the same move as Windows. `TuyaSmartPlug.kt` is an adapter over `com.tuyalan.devices.TuyaLocalDevice`, vendored as `app/libs/tuya-lan-2.0.0.aar` because the SDK is not on Maven yet. Removed: `TuyaCrypto`, `TuyaMessageCodec`, `TuyaMessageCipher`, `TuyaSession`. The plug never worked from the phone for the same reason it never worked on Windows.
- The adapter honours the protocol version already stored in Settings (3.3 / 3.4 / 3.5) instead of assuming one, and passes the device id the SDK needs.
- `proguard-rules.pro` gains `-dontwarn` entries for the SDK's unused cloud client (OkHttp), so release builds stay clean without shipping an HTTP stack.

### Added (Android)
- `TuyaLocalKey` (Kotlin), matching the C# one: 16 characters or 32 hex digits, and a 32-character ASCII key is now rejected instead of silently producing an unusable 32-byte key.
- `SmartPlug` and `RelayState` moved to their own file, so the interface no longer lives inside the Tuya implementation.

### Added (Windows)
- `TuyaLocalKey` parses the local key (16 characters or 32 hex digits) and rejects anything that is not a 16-byte AES key. A 32-character ASCII key used to be accepted silently and could never have worked.
- `TuyaSmartPlugTests` covers the relay mapping and key parsing, replacing the tests that asserted the old wire format.
- `SmartPlugController` classifies a plug timeout as `SmartPlugError.Timeout` rather than `Unreachable`.

### Fixed (Windows)
- **Battery history rewrote the whole file on every reading.** `Append` serialised the entire list - up to 5000 entries - once per entry, which is O(n^2) and made two unit tests take over four minutes. Writes are now coalesced: an append inside a 2-second window is deferred, and `Flush()` (called on window close) forces the pending write. In normal use a reading arrives every few minutes, so every append still writes immediately and nothing changes for users.

### Changed (repository)
- **Windows sources moved into `Windows/`**, mirroring `Android/`. The root now holds only the shared documents, `docs/`, `.github/` and the two platform folders, so another stack can be added without a second round of moving files. Solution, test project, installer scripts and CI paths moved with it; `dotnet` commands are now run against `Windows/BatteryGuardian.slnx`.
- New `Windows/README.md`, matching the shape of `Android/README.md`: layout, build and test commands, output locations, versioning, and where the smart-plug protocol actually lives.

### Docs
- `docs/smart-plug.md` protocol section rewritten: it documented the incorrect wire format as if it were fact.

## [2.3.0] - 2026-11-16

### Added
- **Smart-plug control** on Windows and Android. The app switches a Tuya plug off at the high threshold and on at the low threshold, holding state between them so the relay does not chatter. Settings take the device id, IP, local key, port and relay data point; the key is stored with DPAPI on Windows and the Android Keystore on the phone.
- `docs/smart-plug.md`, the reference for the feature on both platforms.

### Fixed
- Windows build errors in the smart-plug code.

> The protocol in this release was a private implementation that does not match 3.4/3.5 firmware, so plug control did not work against real hardware. It is replaced in the next release; see Unreleased.

## [2.2.0] - 2026-11-06

### Summary
- Fixes the Android Snooze/Stop notification actions, adds a notification-health banner and a monitoring watchdog (Phase 2), and expands the Android test suite to 280 tests.


### Fixed (Android)
- Notification Snooze/Stop buttons now respond when tapped from the shade. They previously started the monitor service with `PendingIntent.getService()`, which cannot legally launch a foreground service from a background context and threw an uncaught `IllegalStateException`, so the taps appeared dead (reported on a Lenovo Tab M10, Android 10). They now use `PendingIntent.getForegroundService()`, and the service posts its foreground notification before acting so it satisfies the foreground-start contract. Snooze also carries an explicit duration so its behaviour is deterministic. No change to phone behaviour.

### Added (Android)
- Notification health diagnostics. New `NotificationHealthChecker` reports when app notifications are disabled, either channel is blocked (importance NONE), or the app is not exempt from battery optimisation - the silent conditions that made alerts vanish on tablets. Additive, read-only, no behaviour change.
- Service action plan. The `onStartCommand` dispatch is factored into a pure `ServiceActionPlan` so START/STOP/SNOOZE resolution and snooze-duration defaults are unit-testable without constructing the Android service.
- Notification health banner (Phase 2). The main screen now shows a dismissible advisory banner for the highest-priority silent-failure condition, with a fix-it action that deep-links to app notification settings, the specific channel settings, or the battery-optimisation exemption dialog (falling back gracefully when a deep link is unavailable). Pure presentation logic lives in HealthBannerPresenter.
- Monitoring watchdog (Phase 2). ServiceWatchdog compares the desired monitoring state with the service live running flag on every resume and restarts a service that OEM battery management killed, or stops one the user turned off.

### Tests (Android)
- Added `OngoingNotificationActionsTest` (both CTAs target the monitor service via foreground-service pending intents; snooze carries a positive duration), `ServiceActionPlanTest` (action resolution and snooze defaults), `NotificationHealthCheckerTest` (disabled notifications, blocked channels, exemption detection), `HealthBannerPresenterTest` (banner priority order, channel targeting, fall-through) and `ServiceWatchdogTest` (restart/stop/no-op decisions). Suite is now 280 tests across 33 classes, all passing.

### Docs (Android)
- README gains a tablet-troubleshooting section (notifications disabled, channel blocked, battery optimisation, missing TTS data), a note on the Snooze/Stop fix, and a new section documenting the Phase 2 notification-health banner and service watchdog.

## [2.1.2] - 2026-10-03

### Changed (Android)
- **Simpler quiet-hours time picker.** The previous version used the Material clock-face dialog, which was visually busy (circular dial, dots, white surface) and clashed with the app's dark theme. It is replaced with a plain, minimal dialog: two number pickers (hours and minutes) separated by a colon, on the app's normal dark surface. Same `HH:mm` value, same stored minutes-since-midnight format — no behaviour change.

## [2.1.1] - 2026-10-03

### Fixed (Android)
- **Quiet-hours window now uses a native time picker.** The settings previously asked users to type a number of minutes past midnight (e.g. `1320` for 22:00), which required mental arithmetic. The start and end fields now open the platform time-picker dialog and display a familiar `HH:mm` value. Internally the value is still stored as minutes, so no other code changed. Selecting a start identical to the end is rejected. Android suite: 249 tests.

## [2.1.0] - 2026-10-03

### Added
- **Battery history & trends** (Windows + Android). Periodic readings are recorded locally (at most one every 5 minutes, capped at 5000 entries) and summarised as average/range, time spent in the ideal charge zone, time above/below it, and a heuristic estimated annual wear. A "Battery History & Trends" view is available from the Windows tray menu and the Android main screen.
- **Quiet hours / Do Not Disturb awareness** (Windows + Android). A configurable daily window (default 22:00-07:00, midnight-spanning supported) plus OS Do Not Disturb detection (Windows Focus Assist; Android interruption filter) silence audible alerts while still showing the notification.
- **Battery charge-limit integration** (Windows). Tray menu action that detects a vendor WMI interface (Lenovo, Dell presets, ASUS) and applies a charge limit (40-100%).
- **Battery charge-limit guidance** (Android). Detects the device manufacturer (Samsung, Pixel, OnePlus/Oppo/Realme, Xiaomi) and deep-links to the OEM battery settings, since Android provides no API for third-party apps to set a charge limit.
- **Home-screen widget** (Android). A resizable widget showing live battery level, charging status and (where available) health; tapping it opens the app.

### Changed
- **Windows Settings dialog** now persists all settings fields, fixing a latent bug where saving could drop unrelated values (e.g. snooze state).
- Settings on both platforms gained sections for battery history and quiet hours.

### Fixed
- Windows Settings window now scrolls, so the added options remain reachable on small screens.

### Tests
- **Windows:** suite grew from 41 to **141 tests** (history analytics + store, quiet hours, time-of-day parsing, charge-limit controllers/factory/preset mapping).
- **Android:** suite grew from 178 to **242 tests across 27 classes** (history analytics + formatter + store, quiet hours schedule + evaluator, charge-limit resolver, widget presentation, audible-suppression in the alert coordinator).

## [2.0.1] - 2026-10-01

### Fixed (Android)
- **Voice alerts now work on Android 11+.** Added the `android.intent.action.TTS_SERVICE` entry to a `<queries>` block in the manifest. Since Android 11 (API 30), apps must declare package visibility to bind the system Text-To-Speech engine; without it, `TextToSpeech` initialization failed silently and every spoken alert was skipped while notification/beep/vibration kept working.
- **First alert on a cold start is no longer dropped.** Text-To-Speech initializes asynchronously, so an alert arriving before the engine was ready used to be discarded. `AlertNotifier` now queues the pending utterance and speaks it once the engine reports ready, and falls back to US English when the device language is unsupported by the engine.
- **Battery receiver action corrected.** The manifest declared the invalid action `android.intent.action.ACTION_BATTERY_CHANGED`; corrected to `android.intent.action.BATTERY_CHANGED`.
- **Snooze duration is now honoured.** The UI offers 30 minutes / 1 hour / 2 hours, but the service always snoozed for 30 minutes and ignored the `minutes` extra. The chosen duration is now applied.

### Changed (Android)
- `fragment-testing` is declared with `debugImplementation` (not `testImplementation`), per Android lint guidance, so its `EmptyActivity` manifest merges into the debug variant for `FragmentScenario`.

### Added (Android)
- **`AlertNotifierTest`** — Robolectric tests covering the notification/alert dispatch logic and the TTS init/queue paths. Android suite is now **178 tests across 20 classes**.

## [2.0.0] - 2026-09-30

### Added
- **Android app** (Kotlin) — a native port of Battery Guardian for Android 9 (Pie) and newer. Foreground service monitoring, high/low alerts with notification + beep + text-to-speech + vibration, snooze, battery-health display, battery-optimisation exemption, in-app update check, and diagnostic logging.
- **Android CI workflow** (`android.yml`) — builds and tests the APK on every push, and publishes a signed release APK on `v*` tags.
- **Root README** now documents both platforms with build badges, a Platforms comparison, and an Android section.

## [1.12.0] - 2026-09-25

### Added
- **About dialog** accessible from the tray menu. Shows the current version, author, license, and quick links to the GitHub repository and latest release.
- **Architecture Decision Records** (`docs/adr/`) documenting six significant technical decisions.
- **Screenshots** in the README.
- **SECURITY.md** — vulnerability disclosure policy.
- **CODE_OF_CONDUCT.md** — Contributor Covenant v2.1.
- **.editorconfig** — consistent code style across editors.
- **Dependabot configuration** — automated dependency update PRs.

## [1.11.0] - 2026-09-25

### Added
- **Snooze alerts**: right-click the tray icon and choose "Snooze 30 minutes" or "Snooze 1 hour". While snoozed, all alerts (toast, beep, speech, repeat timer) are silent. Alerts resume automatically when the snooze expires, or manually via "Resume Alerts".
- The tray tooltip now shows remaining snooze time, e.g. `Battery Guardian - 32% (Not Charging) [Snoozed 24m]`.
- The "Resume Alerts" menu item shows a live countdown.
- **Tray tooltip** shows remaining snooze time, e.g. `Battery Guardian - 32% (Not Charging) [Snoozed 24m]`.
- **Diagnostic logging** — optional toggle in Settings. When enabled, writes a detailed log to `%LocalAppData%\BatteryGuardian\diagnostic.log`, useful for troubleshooting.
- **"Open Diagnostic Log"** tray menu item to open the log file directly.
- Installer now stops any running instance before copying files, and relaunches the app after install completes.


### Fixed
- Reminder intervals longer than 30 seconds previously failed to fire (the timer's countdown was being reset by the periodic refresh cycle).
- `install.ps1` now stops any running instance before copying files (fixes the "file is being used by another process" error during updates).

### Notes
- Snooze is currently tray-menu only. A snooze button inside the Windows toast is planned for a future release.

## [1.10.0] - 2026-09-24

### Added
- **Two release variants**:
  - **Self-Contained** (~200 MB) — bundles the .NET 10 runtime; works on any Windows 10/11 machine with zero setup.
  - **Framework-Dependent** (~25 MB) — for users who already have the .NET 10 Desktop Runtime installed. 87% smaller download.
- Both variants include the same installer scripts and are attached to every GitHub Release.

### Changed
- Removed `<SelfContained>true</SelfContained>` from `BatteryGuardian.csproj`. The publish step now controls this via the `--self-contained` flag, allowing both variants to be built from the same source.
- CI workflow now builds, uploads, and releases both variants.

### Fixed
- `install.ps1` now correctly copies `uninstall.bat` and `uninstall.ps1` into the install folder so users can remove the app from the install directory itself.

## [1.9.0] - 2026-09-24

### Added
- **Automatic update check**: On startup (once every 24 hours), the app queries GitHub Releases for a newer version. If one is available, a dialog offers to open the download page in the default browser.
- **Manual "Check for Updates"** in the tray right-click menu, for on-demand checks.
- **Automatic versioning via MinVer**: The app's version number is now derived from Git tags — no more manual editing in the `.csproj`.
- **11 new unit tests** for version parsing and comparison (total: 41).

### Changed
- Settings window now has a "Check for updates automatically" checkbox (default: enabled).

### Notes
- The update check is fully silent if the network is down or GitHub is unreachable — it never interferes with normal use.
- Rate-limited by GitHub to 60 requests/hour per IP; we throttle to 1 automatic check per 24 hours.

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
