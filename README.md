# Battery Guardian
[![Build and Publish EXE](https://github.com/puneet-swarup/Battery-Guardian/actions/workflows/build.yml/badge.svg)](https://github.com/puneet-swarup/Battery-Guardian/actions/workflows/build.yml)

A lightweight, open-source WPF application for Windows that helps extend your laptop battery lifespan by notifying you when it's time to unplug or plug in your charger.

Lithium-ion batteries degrade fastest when kept at 100% charge for extended periods, or when drained below 15%. Battery Guardian sits quietly in your system tray and alerts you via voice, beep, and toast notifications so you never forget to manage your battery health.

---

## ✨ Features

- **Real-time Monitoring**: Displays current battery percentage and charging status.
- **Estimated Time**: Shows time remaining while discharging, and time to full while charging (when the OS reports it).
  > Estimated Time: Shows time remaining (hybrid native + WMI fallback) while discharging, and time to full while charging (when the OS reports it). Some laptops may still show "Calculating..." if neither method provides a value.
- **Automatic Refresh**: Updates the status every 30 seconds.
- **Manual Refresh**: Click the "Refresh Now" button for instant updates.
- **Smart Alerts**:
  - **High Charge Alert**: When the charger is **plugged in** and battery reaches the High threshold (default 95%), it plays a beep, shows a notification, and speaks *"Battery is at X%. Consider unplugging the charger."*
  - **Low Charge Alert**: When the charger is **not plugged in** and battery drops to the Low threshold (default 15%), it plays a beep, shows a notification, and speaks *"Battery is low at X%. Please connect the charger."*
- **Repeat Reminders**: If you ignore an alert, the voice message and beep will **repeat automatically** after a configurable interval (default: 5 minutes) until you act.
- **Auto-Stop**: The alert stops **immediately** when you plug in/unplug the charger, even if the battery percentage hasn't crossed the threshold yet.
- **Voice Notifications**: Uses Windows built-in Text-to-Speech for clear audio alerts.
- **Dynamic System Tray Icon**: The tray icon visually updates to show your battery level (Green for high, Orange for medium, Red for low), and the tooltip shows percentage and charging status at a glance.
- **Fully Configurable**: Customize the High threshold, Low threshold, and Repeat interval via the Settings window.
- **Auto-Start**: Registers itself to launch automatically when you sign into Windows (on first run).
- **Battery Health**: Displays the current full-charge capacity vs. original design capacity (wear level), when the laptop reports it.
---

## 🛠️ Prerequisites

- **Windows 10 or 11** (64-bit).
- **Visual Studio 2022** (with the `.NET Desktop Development` workload installed).
- **.NET 10 SDK** (included with VS 2022 17.10+).

---

## 🚀 Installation (Build from Source)

Since this repository does not include an installer, you need to compile the application yourself. It's quick and easy:

1. **Clone the repository** to your local machine using Git or download the ZIP:
   ```cmd
   git clone https://github.com/puneet-swarup/Battery-Guardian.git
   ```
2. **Open the solution**: Navigate to the cloned folder and double-click BatteryGuardian.sln to open it in Visual Studio 2022.

3. **Restore NuGet Packages**: Visual Studio will automatically restore the System.Speech dependency. If it doesn't, right-click the solution in the Solution Explorer and select Restore NuGet Packages.

4. **Build and Run**: Press F5 to compile and run the app.

--- 
## 📖 How to Use
**First Launch**: When you run the app, it will immediately minimize to the System Tray (bottom-right corner of your taskbar). You will see a battery icon.

**Open the UI**: Double-click the battery icon in the system tray to open the main window. You can also right-click it and choose "Open".

**Change Settings**: Right-click the system tray icon and select Settings. Here you can adjust:

  - High Threshold (default: 95%)

  - Low Threshold (default: 15%)

  - Repeat Alert Every (seconds) (default: 300)

**Auto-Start with Windows**: The first time you run the app, it will automatically add a registry entry to launch itself every time you log into Windows. To remove it from auto-start, simply open Task Manager > Startup tab, disable "BatteryGuardian", or delete the entry from *HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run*.

---

## ⚙️ Configuration
All settings are saved locally to:

*%LocalAppData%\BatteryGuardian\Settings.json*

You can safely edit this file with a text editor if you prefer, but it is simpler to use the built-in Settings window.

---

## 🗺️ Roadmap
- Native Windows toast notifications with action buttons (Snooze / Dismiss).

- Battery health (wear level) reporting.

- WiX-based MSI installer with a proper uninstaller.

- Auto-update from GitHub Releases.

- Unit tests for the alert-evaluation logic.

See *CHANGELOG.md* for a full history of changes.

---

## 🧪 Running Tests

The project includes unit tests for the alert-evaluation logic. To run them:

```cmd
dotnet test
```

Or from Visual Studio: Test → Run All Tests (Ctrl + R, A).

All tests must pass before submitting a pull request.

---

## 🤝 Contributing
Contributions are welcome! Please open an issue first to discuss what you'd like to change, and check the Pull Request template when submitting code.

---

## 🛡️ Disclaimer
This application uses the standard Windows APIs (GetSystemPowerStatus) to read battery data. It does not have the ability to physically stop your laptop from charging (that is a hardware-level limitation). It is designed solely as an alert assistant to remind you to manually unplug your charger when the battery is full.