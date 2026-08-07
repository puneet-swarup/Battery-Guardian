# Battery Guardian

A lightweight, open-source WPF application for Windows that helps extend your laptop battery lifespan by notifying you when it's time to unplug or plug in your charger.

Lithium-ion batteries degrade fastest when kept at 100% charge for extended periods, or when drained below 15%. Battery Guardian sits quietly in your system tray and alerts you via voice, beep, and toast notifications so you never forget to manage your battery health.

---

## ✨ Features

- **Real-time Monitoring**: Displays current battery percentage and charging status.
- **Automatic Refresh**: Updates the status every 30 seconds.
- **Manual Refresh**: Click the "Refresh Now" button for instant updates.
- **Smart Alerts**:
  - **High Charge Alert**: When charging and reaching 95% (configurable), it plays a beep, shows a notification, and speaks *"Please disconnect the charger."*
  - **Low Charge Alert**: When discharging and dropping to 15% (configurable), it plays a beep, shows a notification, and speaks *"Please connect the charger."*
- **Voice Notifications**: Uses Windows built-in Text-to-Speech for clear audio alerts.
- **Dynamic System Tray Icon**: The tray icon visually updates to show your battery level (Green for high, Orange for medium, Red for low).
- **Fully Configurable**: Customize the High and Low threshold percentages via the Settings window.
- **Auto-Start**: Registers itself to launch automatically when you sign into Windows (on first run).

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
2. **Open the solution** : Navigate to the cloned folder and double-click BatteryGuardian.sln to open it in Visual Studio 2022.

3. **Restore NuGet Packages**: Visual Studio will automatically restore the System.Speech dependency. If it doesn't, right-click the solution in the Solution Explorer and select **Restore NuGet Packages**.

4. **Build and Run**: Press F5 to compile and run the app.

---

## 📖 How to Use
**First Launch**: When you run the app, it will immediately minimize to the System Tray (bottom-right corner of your taskbar). You will see a battery icon.

**Open the UI**: Double-click the battery icon in the system tray to open the main window. You can also right-click it and choose "Open".

**Change Settings**: Right-click the system tray icon and select Settings. Here you can adjust:

*High Threshold* (default: 95%)

*Low Threshold* (default: 15%)

**Auto-Start with Windows**: The first time you run the app, it will automatically add a registry entry to launch itself every time you log into Windows. To remove it from auto-start, simply open Task Manager > Startup tab, disable "BatteryGuardian", or delete the entry from *HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run*.

---

## ⚙️ Configuration
All settings are saved locally to:
*%LocalAppData%\BatteryGuardian\Settings.json*
You can safely edit this file with a text editor if you prefer, but it is simpler to use the built-in Settings window.

---

## 🛡️ Disclaimer
This application uses the standard Windows APIs (GetSystemPowerStatus) to read battery data. It does not have the ability to physically stop your laptop from charging (that is a hardware-level limitation). It is designed solely as an alert assistant to remind you to manually unplug your charger when the battery is full.

---