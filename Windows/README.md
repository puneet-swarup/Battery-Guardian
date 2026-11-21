# Battery Guardian — Windows

The Windows desktop app: a tray application that watches the battery and tells
you when to unplug or plug in, plus optional control of a Tuya smart plug.

WPF, .NET 10, Windows 10 1809 or later. No installer framework — a per-user
script installer.

## Layout

```
Windows/
├── App.xaml, MainWindow.xaml       shell and main window
├── SettingsWindow.xaml, AboutWindow.xaml
├── ChargeLimit/                    charge-limit awareness
├── History/                        battery history capture and analysis
├── QuietHours/                     do-not-disturb window
├── SmartPlug/                      threshold logic + Tuya adapter
├── Assets/                         icon and images
├── BatteryGuardian.Tests/          xUnit suite
├── install.*, uninstall.*          per-user installer scripts
└── BatteryGuardian.slnx            solution
```

## Build and test

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```cmd
dotnet build Windows/BatteryGuardian.slnx
dotnet test  Windows/BatteryGuardian.slnx
```

From Visual Studio, open `Windows/BatteryGuardian.slnx` and run the tests with
Ctrl + R, A.

### Output locations

| command | output |
|---|---|
| `dotnet build Windows/BatteryGuardian.slnx -c Release` | `Windows/bin/Release/net10.0-windows10.0.17763.0/` |
| `dotnet publish Windows/BatteryGuardian.csproj -c Release -r win-x64 --self-contained true -o PublishOutput` | `PublishOutput/` |
| `dotnet publish Windows/BatteryGuardian.csproj -c Release -o PublishOutput-FDD` | `PublishOutput-FDD/` (needs the .NET 10 runtime installed) |

The release workflow builds both variants, copies the installer scripts into
each, and attaches both zips to the GitHub release.

## Versioning

[MinVer](https://github.com/adamralph/minver) derives the version from the git
tag (`v` prefix). There is no version number in the project file; tag the
release and it follows.

## Smart plug

The plug is optional. Settings → Smart Plug takes the device id, IP, local key,
port, relay data point and the two thresholds. The local key is stored with
DPAPI under the current user.

The protocol lives in the [Tuya LAN SDK](https://github.com/puneet-swarup/tuya-lan-sdk),
not in this repository — `SmartPlug/TuyaSmartPlug.cs` is a thin adapter over it.
See [`docs/smart-plug.md`](../docs/smart-plug.md) for the behaviour rules and for
what the old hand-rolled implementation got wrong.

## Architecture decisions

The ADRs live at [`docs/adr/`](../docs/adr/):

- 0001 — WPF over WinForms
- 0002 — Hardcodet.NotifyIcon for the tray icon
- 0003 — native Windows toasts
- 0004 — script installer over MSI
- 0005 — two release variants (self-contained and framework-dependent)
- 0006 — MinVer for versioning

## Installing a release

Extract the release zip and double-click `install.bat`. It installs per-user to
`%LocalAppData%\Programs\BatteryGuardian\`, adds a Start Menu shortcut, and
registers in Settings → Apps for a clean uninstall.
