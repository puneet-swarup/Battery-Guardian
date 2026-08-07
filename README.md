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