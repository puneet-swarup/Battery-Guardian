# Contributing to Battery Guardian

Thanks for taking the time. This repository ships two apps that share one set of
alert rules, so most changes have to be made twice or made in the shared logic.

## Repository layout

```
Windows/    WPF app (.NET 10), xUnit tests, installer scripts
Android/    Kotlin app, JVM + Robolectric tests, Gradle build
docs/       ADRs, screenshots, the smart-plug reference
```

The Tuya protocol is not in this repository. Both apps use the
[Tuya LAN SDK](https://github.com/puneet-swarup/tuya-lan-sdk); each has a thin
adapter (`Windows/SmartPlug/TuyaSmartPlug.cs`,
`Android/.../data/tuya/TuyaSmartPlug.kt`) and nothing else that knows about
framing or crypto. Keep it that way — protocol bugs belong upstream, in the SDK,
where they can be tested against hardware.

## Running the tests

**Windows** (needs the .NET 10 SDK):

```cmd
dotnet build Windows/BatteryGuardian.slnx
dotnet test  Windows/BatteryGuardian.slnx
```

**Android** (needs **JDK 17** — Robolectric 4.14.1 fails on newer JDKs, see
`Android/README.md`):

```cmd
cd Android
gradlew.bat :app:testDebugUnitTest
```

Both suites must be green before you open a pull request. CI runs the same
commands, on JDK 17 and .NET 10.

## What a good change looks like

- **Behaviour changes need a test.** The alert thresholds, quiet hours and
  charge-limit rules are pure functions; test them without a device.
- **Keep the platform layer thin.** Battery reads, notifications and the tray
  belong in the platform folder; decisions belong in the shared rules.
- **A new feature updates the user-visible text in the same commit.** The About
  screen on both platforms, the README feature list and the settings labels are
  how anyone discovers what the app does. A feature nobody can find is a feature
  nobody has.
- **A functional change updates `CHANGELOG.md`.** Add a bullet under
  `## [Unreleased]` in the same commit. The release body comes from that
  section, and the release job fails if the tag has none.
- **Say what you did not verify.** Hardware behaviour, OEM battery managers and
  Android notification quirks vary; claiming more than you tested costs the next
  person hours.
- **Match the surrounding style.** The code uses single-space indentation in
  Kotlin and no MVVM in the WPF app — simple and lightweight on purpose.

## Submitting

1. Fork the repository.
2. Branch from `main` (`git checkout -b feature/amazing-feature`).
3. Make your change, with tests.
4. Commit (`git commit -m 'Add some amazing feature'`).
5. Push and open a pull request against `main`.

The pull request template asks which suites you ran — fill it in. CI must pass
before merging.

## Security

Do not open a public issue for a vulnerability, and never commit a local key,
device id or API token. See [SECURITY.md](SECURITY.md).
