# Battery Guardian — Android

A sideloaded Android port of the Windows Battery Guardian app. It watches the
battery and warns you (notification + sound + speech + vibration) when you
should unplug at high charge or plug in at low charge.

- Package: com.puneet.batteryguardian
- minSdk: 28 (Android 9 Pie)
- targetSdk / compileSdk: 36
- Language: Kotlin
- Build system: Gradle 9.4.1 + Android Gradle Plugin 9.2.0 (built-in Kotlin)

## Toolchain requirements

| Tool | Version |
|---|---|
| JDK | 17 or newer |
| Gradle | 9.4.1 (installed locally) |
| Android Gradle Plugin | 9.2.0 |
| Android SDK Platform | android-36 |
| Android SDK Build-Tools | 36.0.0 |

The local.properties file points Gradle at the Android SDK via the sdk.dir
property (for example, the SDK folder under your AppData Local Android Sdk path).

## Build and test

A build.bat helper wraps the common commands. Run it from this folder:

    build.bat test       run JVM unit tests
    build.bat coverage   run unit tests + generate a JaCoCo coverage report
    build.bat debug      build the debug APK
    build.bat release    build the release APK (needs signing env vars)
    build.bat install    build + install debug APK on a connected device
    build.bat lint       run Android lint
    build.bat clean      remove build outputs

Or call Gradle directly:

    gradle testDebugUnitTest    unit tests
    gradle createDebugUnitTestCoverageReport   coverage report
    gradle assembleDebug        debug APK
    gradle assembleRelease      release APK
    gradle installDebug         deploy to a connected device

### Output locations

- Debug APK: app/build/outputs/apk/debug/app-debug.apk
- Release APK: app/build/outputs/apk/release/app-release.apk
- Unit test report: app/build/reports/tests/testDebugUnitTest/index.html
- Coverage report: app/build/reports/coverage/test/debug/index.html
- Lint report: app/build/reports/lint-results-debug.html

## Signing a release APK

The release build reads its keystore from environment variables so no secrets
are committed. Create a keystore once (keep it safe — required for every future
update):

    keytool -genkeypair -v -keystore bg-release.jks -alias batteryguardian -keyalg RSA -keysize 2048 -validity 10000

Then set these environment variables before running build.bat release:

    BG_STORE_FILE       path to bg-release.jks
    BG_STORE_PASSWORD   keystore password
    BG_KEY_ALIAS        batteryguardian
    BG_KEY_PASSWORD     key password

If BG_STORE_FILE is not set, the release build still runs but produces an
unsigned APK.

## Installing on the device (Android 9)

1. Enable developer options and USB debugging on the phone.
2. Connect it and run build.bat install (or gradle installDebug).

To install a built APK manually, copy it to the phone and tap it. Android 8+
has no global unknown-sources toggle; you allow installs per source app under
Settings, Apps and notifications, Special app access, Install unknown apps.

## Testing overview

Unit tests live in app/src/test and run entirely on the JVM — no device or
emulator required, which makes them ideal for CI. The suite has **172 tests**
across 19 classes, split into two flavours:

**Plain JUnit tests** (fast, no Android framework):

| Test class | What it covers |
|---|---|
| AlertEvaluatorTest | High/low threshold decisions and edge cases |
| AlertStateTest | Active-message precedence and anyActive |
| BatteryHealthTest | Health percentage, clamping and label boundaries |
| SettingsTest | Snooze helpers, defaults and equality |
| AlertCoordinatorTest | Repeat interval, snooze suppression, state transitions |
| UpdateCheckerTest | Version tag parsing, comparison, UpdateInfo |
| BatterySnapshotTest | Snapshot sentinels and value equality |

**Robolectric tests** (real Android framework classes on the JVM):

| Test class | What it covers |
|---|---|
| BatteryReaderTest | Battery intent parsing and status mapping |
| BatteryStateReceiverTest | Callback and manifest receiver paths |
| BootReceiverTest | Boot / package-replace restart guard |
| SettingsRepositoryTest | SharedPreferences round-trip |
| DiagnosticLogTest | File-backed logging and the enabled gate |
| OngoingNotificationTest | Foreground notification + channel creation |
| ApkInstallerTest | Install-permission gate and failure path |
| UpdateCheckReceiverTest | Auto-update-disabled early return |
| AboutActivityTest | Version label, GitHub button, up-navigation |
| MainActivityTest | Control wiring and monitoring labels |
| SettingsActivityTest | Fragment hosting and up-navigation |
| SettingsFragmentTest | Threshold + repeat-interval validation rules |

Run the full suite (this is what CI executes):

    gradle testDebugUnitTest

Generate a coverage report:

    gradle testDebugUnitTest createDebugUnitTestCoverageReport

### Coverage note

The JaCoCo report (`app/build/reports/coverage/test/debug/index.html`) measures
plain-JUnit execution precisely. Robolectric runs bytecode inside its own
sandbox classloader, which JaCoCo's on-the-fly agent does not instrument, so
Robolectric-covered classes (activities, services, receivers) may show as
uncovered in the HTML report even though the tests exercise them and pass. Treat
the report as a floor for the pure logic, not the whole picture.

Instrumented (on-device) tests live in app/src/androidTest and run with:

    gradle connectedDebugAndroidTest

## Architecture

    core/       Pure logic (no Android deps) - AlertEvaluator, BatteryHealth
    battery/    BatteryManager + sysfs reads - BatteryReader, BatterySnapshot
    data/       Settings, SettingsRepository (SharedPreferences), DiagnosticLog
    notify/     Notification channels + AlertNotifier (toast/sound/speech/vibrate)
    service/    Foreground service, battery receiver, boot receiver, coordinator
    update/     GitHub release check + APK download/install
    ui/         MainActivity, SettingsActivity, AboutActivity

The pure core/ and data/ layers have no Android dependencies and are the direct
Kotlin ports of the Windows app's BatteryAlertEvaluator.cs and
BatteryHealthInfo.cs, which is what makes them unit-testable.

## Notes on Android 9 behaviour

- Monitoring runs in a foreground service with an ongoing notification
  (Android's substitute for the Windows tray icon).
- Battery health percent is best-effort: Android does not publicly expose design
  capacity. When it cannot be read, the health card hides itself.
- The app should be exempted from battery optimisation for reliable background
  monitoring (button on the main screen).
