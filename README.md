# Battery Guardian
[![Build and Publish EXE](https://github.com/puneet-swarup/Battery-Guardian/actions/workflows/build.yml/badge.svg)](https://github.com/puneet-swarup/Battery-Guardian/actions/workflows/build.yml)

A lightweight, open-source WPF application for Windows that helps extend your laptop battery lifespan by notifying you when it's time to unplug or plug in your charger.

Lithium-ion batteries degrade fastest when kept at 100% charge for extended periods, or when drained below 15%. Battery Guardian sits quietly in your system tray, alerts you via voice, custom sound, and uses an eye-catching popup and blinking tray icon to make sure you never miss a warning.

---

## ✨ Features

- **Real-time Monitoring**: Displays current battery percentage and charging status.
- **Automatic Refresh**: Updates the status every 30 seconds.
- **Manual Refresh**: Click the "Refresh Now" button for instant updates.
- **Smart Alerts**:
  - **High Charge Alert**: When charging and reaching 95% (configurable), it triggers an alert.
  - **Low Charge Alert**: When discharging and dropping to 15% (configurable), it triggers an alert.
- **Voice Notifications**: Uses Windows built-in Text-to-Speech to clearly speak *"Please disconnect the charger"* or *"Please connect the charger"*.
- **Custom Alert Sounds**: Pick your own `.wav` (or MP3) file for High and Low alerts via the Settings window. Defaults to the system beep.
- **Custom Visual Popup**: A modern, colored toast notification slides up from the bottom-right corner of your screen.
- **Blinking Tray Icon**: When an alert fires, the system tray battery icon blinks for 10 seconds to grab your attention.
- **Dynamic System Tray Icon**: The tray icon dynamically updates to show your battery level (Green for high, Orange for medium, Red for low).
- **Fully Configurable**: Customize the High and Low threshold percentages via the Settings window.
- **Auto-Start**: Automatically registers itself to launch when you log into Windows (on first run).

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

*High Alert Sound* (Select a custom .wav/.mp3 file, or leave it as <Default> for the classic system beep).

*Low Alert Sound* (Select a custom .wav/.mp3 file, or leave it as <Default>).

---

## 🚀 Auto-Start (Automatic Registration)
The first time you run the application after building it, it will automatically register itself in your Windows Registry (*HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run*).

This means it will launch silently in your system tray every single time you log into Windows, without you needing to manually drag shortcuts anywhere.

```quote
If you want to disable auto-start later: You can simply open Windows Task Manager, go to the Startup tab, find BatteryGuardian, and disable it. Alternatively, you can delete the BatteryGuardian entry from the registry key mentioned above.
```

---

## ⚙️ Configuration
All settings are saved locally to:
*%LocalAppData%\BatteryGuardian\Settings.json*
You can safely edit this file with a text editor if you prefer, but it is simpler to use the built-in Settings window.

---

## 🛡️ Disclaimer
This application uses the standard Windows APIs (GetSystemPowerStatus) to read battery data. It does not have the ability to physically stop your laptop from charging (that is a hardware-level limitation). It is designed solely as an alert assistant to remind you to manually unplug your charger when the battery is full.

---