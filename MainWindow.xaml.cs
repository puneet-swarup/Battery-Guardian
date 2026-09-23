using System;
using System.Speech.Synthesis;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace BatteryGuardian
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        private readonly SpeechSynthesizer _speechSynthesizer;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        private const byte AC_LINE_ONLINE = 1;
        private const byte BATTERY_FLAG_CHARGING = 8;
        private const byte BATTERY_FLAG_UNKNOWN = 255;
        private const byte BATTERY_PERCENT_UNKNOWN = 255;

        private Settings _settings = null!;
        private Bitmap? _trayIconBitmap;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _alarmTimer;

        private WinForms.NotifyIcon _notifyIcon = null!;

        private bool _highAlertActive;
        private bool _lowAlertActive;
        private bool _isExiting;
        private string _currentAlertMessage = ""; // <-- NEW

        public MainWindow()
        {
            InitializeComponent();

            _speechSynthesizer = new SpeechSynthesizer();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            _alarmTimer = new DispatcherTimer();
            _alarmTimer.Tick += AlarmTimer_Tick;

            LoadSettings();
            InitializeTrayIcon();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
        }

        private void InitializeTrayIcon()
        {
            var contextMenu = new WinForms.ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => RestoreWindow());
            contextMenu.Items.Add("Settings", null, (s, e) => OpenSettingsWindow());
            contextMenu.Items.Add("Exit", null, ExitMenuItem_Click);

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Battery Guardian",
                Visible = true,
                ContextMenuStrip = contextMenu
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitMenuItem_Click(object? sender, EventArgs e)
        {
            _isExiting = true;
            Close();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized) Hide();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_isExiting) { e.Cancel = true; Hide(); }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureStartup();
            RefreshBatteryStatus();
            _refreshTimer.Start();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();
            _alarmTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIconBitmap?.Dispose();
            _speechSynthesizer.Dispose();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) => RefreshBatteryStatus();

        // <-- NEW: Repeats voice + beep, not just beep
        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            if (_highAlertActive || _lowAlertActive)
            {
                SystemSounds.Beep.Play();
                if (!string.IsNullOrEmpty(_currentAlertMessage))
                    _speechSynthesizer.SpeakAsync(_currentAlertMessage);
            }
            else
            {
                _alarmTimer.Stop();
                _currentAlertMessage = "";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshBatteryStatus();

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        // ------- Settings & Startup Logic -------

        private string GetSettingsPath()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryGuardian");
            if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
            return Path.Combine(appDataFolder, "Settings.json");
        }

        private void LoadSettings()
        {
            string settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            else
            {
                _settings = new Settings();
                SaveSettings(_settings);
            }
        }

        private void SaveSettings(Settings settings)
        {
            string settingsPath = GetSettingsPath();
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }

        private void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(_settings);
            if (settingsWindow.ShowDialog() == true)
            {
                _settings = settingsWindow.Settings;
                SaveSettings(_settings);
                _highAlertActive = false;
                _lowAlertActive = false;
                _currentAlertMessage = "";
                _alarmTimer.Stop();
                RefreshBatteryStatus();
            }
        }

        private void EnsureStartup()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    if (key != null)
                    {
                        key.SetValue("BatteryGuardian", exePath);
                        key.Close();
                    }
                }
            }
            catch { }
        }

        // ------- Battery Logic & Tray Icon -------

        private void UpdateTrayIcon(int percentage)
        {
            if (percentage < 0 || percentage > 100) return;

            _trayIconBitmap?.Dispose();

            _trayIconBitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(_trayIconBitmap))
            {
                g.Clear(Color.Transparent);
                g.DrawRectangle(Pens.Black, 0, 2, 12, 12);
                g.FillRectangle(Brushes.Black, 13, 5, 2, 6);

                Brush fillBrush;
                if (percentage >= 60) fillBrush = Brushes.Green;
                else if (percentage >= 30) fillBrush = Brushes.Orange;
                else fillBrush = Brushes.Red;

                int fillWidth = (int)(10 * percentage / 100.0);
                if (fillWidth > 0) g.FillRectangle(fillBrush, 1, 3, fillWidth, 10);
            }

            IntPtr hIcon = _trayIconBitmap.GetHicon();
            using (var tmpIcon = System.Drawing.Icon.FromHandle(hIcon))
            {
                _notifyIcon.Icon = (System.Drawing.Icon)tmpIcon.Clone();
            }
        }

        private void RefreshBatteryStatus()
        {
            if (!GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            {
                BatteryPercentageText.Text = "Battery: N/A";
                ChargingStatusText.Text = "Status: Unable to read power status";
                EstimatedTimeText.Text = "";
                LastUpdatedText.Text = $"Last updated: {DateTime.Now:T}";
                return;
            }

            bool isOnAcPower = status.ACLineStatus == AC_LINE_ONLINE;
            bool isCharging = isOnAcPower && (status.BatteryFlag & BATTERY_FLAG_CHARGING) == BATTERY_FLAG_CHARGING;

            string percentageText = status.BatteryLifePercent == BATTERY_PERCENT_UNKNOWN
                ? "Battery: N/A"
                : $"Battery: {status.BatteryLifePercent}%";

            string statusText;
            if (status.BatteryFlag == BATTERY_FLAG_UNKNOWN) statusText = "Status: Unknown";
            else if (isCharging) statusText = "Status: Charging";
            else if (isOnAcPower) statusText = "Status: Plugged In (Not Charging)";
            else statusText = "Status: Not Charging";

            BatteryPercentageText.Text = percentageText;
            ChargingStatusText.Text = statusText;
            LastUpdatedText.Text = $"Last updated: {DateTime.Now:T}";

            // NEW: Estimated time display
            EstimatedTimeText.Text = BuildEstimatedTimeText(status, isOnAcPower, isCharging);

            if (status.BatteryLifePercent != BATTERY_PERCENT_UNKNOWN)
            {
                UpdateTrayIcon(status.BatteryLifePercent);

                // NEW: Enhanced tooltip
                string chargeWord = isCharging ? "Charging" : (isOnAcPower ? "Plugged In" : "Not Charging");
                string timeHint = "";
                if (!isOnAcPower)
                {
                    string remaining = FormatTimeSpan(status.BatteryLifeTime);
                    if (!string.IsNullOrEmpty(remaining)) timeHint = $" ~{remaining} left";
                }
                else if (isCharging)
                {
                    string toFull = FormatTimeSpan(status.BatteryFullLifeTime);
                    if (!string.IsNullOrEmpty(toFull)) timeHint = $" ~{toFull} to full";
                }

                // NotifyIcon.Text has a 63-character limit on Windows, so keep it short.
                string tooltip = $"Battery Guardian - {status.BatteryLifePercent}% ({chargeWord}){timeHint}";
                if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 60) + "...";
                _notifyIcon.Text = tooltip;

                EvaluateAlerts(status.BatteryLifePercent, isCharging, isOnAcPower);
            }
            else
            {
                EstimatedTimeText.Text = "";
            }
        }

        /// <summary>
        /// Formats a duration (in seconds) as a human-readable string like "2h 15m" or "45m".
        /// Returns an empty string if the value is unknown (0 or uint.MaxValue) or unreasonable.
        /// </summary>
        private string FormatTimeSpan(uint seconds)
        {
            // Windows returns 0 or uint.MaxValue when the value is unknown or on AC power.
            if (seconds == 0 || seconds == uint.MaxValue) return "";

            // Sanity check: > 100 hours is almost certainly bogus.
            if (seconds > 360_000) return "";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        /// <summary>
        /// Builds the text shown in the EstimatedTimeText TextBlock based on the current power state.
        /// </summary>
        private string BuildEstimatedTimeText(SYSTEM_POWER_STATUS status, bool isOnAcPower, bool isCharging)
        {
            if (isCharging)
            {
                string toFull = FormatTimeSpan(status.BatteryFullLifeTime);
                return string.IsNullOrEmpty(toFull) ? "Time to full: Calculating..." : $"Time to full: {toFull}";
            }

            if (isOnAcPower)
            {
                // Plugged in but not actively charging - OS rarely reports useful time here.
                return "";
            }

            string remaining = FormatTimeSpan(status.BatteryLifeTime);
            return string.IsNullOrEmpty(remaining) ? "Time remaining: Calculating..." : $"Time remaining: {remaining}";
        }

        // <-- CHANGED: Now takes isOnAcPower as third parameter
        private void EvaluateAlerts(int batteryPercent, bool isCharging, bool isOnAcPower)
        {
            int highThreshold = _settings.HighBatteryThreshold;
            int lowThreshold = _settings.LowBatteryThreshold;

            bool highCondition = isOnAcPower && batteryPercent >= highThreshold;
            bool lowCondition = !isOnAcPower && batteryPercent <= lowThreshold;

            // Handle High Alert
            if (highCondition && !_highAlertActive)
            {
                _highAlertActive = true;
                string message = $"Battery is at {batteryPercent}%. Consider unplugging the charger.";
                _currentAlertMessage = message;
                ShowToastNotification("Battery Guardian", message);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(message);
            }
            else if (!highCondition && _highAlertActive)
            {
                _highAlertActive = false;
                // Only clear the message if Low Alert is NOT active
                if (!_lowAlertActive)
                    _currentAlertMessage = "";
            }

            // Handle Low Alert
            if (lowCondition && !_lowAlertActive)
            {
                _lowAlertActive = true;
                string message = $"Battery is low at {batteryPercent}%. Please connect the charger.";
                _currentAlertMessage = message;
                ShowToastNotification("Battery Guardian", message);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(message);
            }
            else if (!lowCondition && _lowAlertActive)
            {
                _lowAlertActive = false;
                // Only clear the message if High Alert is NOT active
                if (!_highAlertActive)
                    _currentAlertMessage = "";
            }

            // Manage the timer
            if (_highAlertActive || _lowAlertActive)
            {
                // Keep the timer running (it will restart if it was stopped)
                StartAlarmTimerIfNeeded();
            }
            else
            {
                _alarmTimer.Stop();
                _currentAlertMessage = "";
            }
        }

        private void StartAlarmTimerIfNeeded()
        {
            _alarmTimer.Interval = TimeSpan.FromSeconds(_settings.AlertRepeatIntervalSeconds);
            if (!_alarmTimer.IsEnabled)
            {
                _alarmTimer.Start();
            }
        }

        private void ShowToastNotification(string title, string message)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = WinForms.ToolTipIcon.Warning;
            _notifyIcon.ShowBalloonTip(5000);
        }
    }
}