using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
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
        private readonly SpeechSynthesizer _speechSynthesizer;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _alarmTimer;
        private readonly DispatcherTimer _blinkTimer;

        private WinForms.NotifyIcon _notifyIcon = null!;

        private bool _highAlertActive;
        private bool _lowAlertActive;
        private bool _isExiting;
        private bool _iconVisible = true;

        public MainWindow()
        {
            InitializeComponent();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += RefreshTimer_Tick;

            _alarmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _alarmTimer.Tick += AlarmTimer_Tick;

            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _blinkTimer.Tick += (s, e) =>
            {
                _iconVisible = !_iconVisible;
                _notifyIcon.Visible = _iconVisible;
            };

            _speechSynthesizer = new SpeechSynthesizer();

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
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
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
            _blinkTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIconBitmap?.Dispose();
            _speechSynthesizer.Dispose();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) => RefreshBatteryStatus();

        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            if (_highAlertActive || _lowAlertActive)
                SystemSounds.Beep.Play();
            else
                _alarmTimer.Stop();
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
            string path = GetSettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
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
            string path = GetSettingsPath();
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private void OpenSettingsWindow()
        {
            var sw = new SettingsWindow(_settings);
            if (sw.ShowDialog() == true)
            {
                _settings = sw.Settings;
                SaveSettings(_settings);
                _highAlertActive = false;
                _lowAlertActive = false;
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

        // ------- Blink & Toast UI -------

        private void ShowToastNotification(string title, string message)
        {
            var popup = new AlertPopup(message);
            popup.Show();

            _iconVisible = true;
            _notifyIcon.Visible = true;
            _blinkTimer.Start();

            var stopBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            stopBlinkTimer.Tick += (s, e) =>
            {
                stopBlinkTimer.Stop();
                _blinkTimer.Stop();
                _notifyIcon.Visible = true;
            };
            stopBlinkTimer.Start();
        }

        // ------- Battery Logic & Tray Icon -------

        private void UpdateTrayIcon(int percentage)
        {
            if (percentage < 0 || percentage > 100) return;

            _trayIconBitmap?.Dispose();

            _trayIconBitmap = new System.Drawing.Bitmap(16, 16);
            using (var g = System.Drawing.Graphics.FromImage(_trayIconBitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawRectangle(System.Drawing.Pens.Black, 0, 2, 12, 12);
                g.FillRectangle(System.Drawing.Brushes.Black, 13, 5, 2, 6);

                System.Drawing.Brush fillBrush = percentage >= 60 ? System.Drawing.Brushes.Green : percentage >= 30 ? System.Drawing.Brushes.Orange : System.Drawing.Brushes.Red;
                int fillWidth = (int)(10 * percentage / 100.0);
                if (fillWidth > 0) g.FillRectangle(fillBrush, 1, 3, fillWidth, 10);
            }

            // ERROR-FREE CONVERSION:
            System.IntPtr hIcon = _trayIconBitmap.GetHicon();
            using (var tmpIcon = System.Drawing.Icon.FromHandle(hIcon))
            {
                _notifyIcon.Icon = (System.Drawing.Icon)tmpIcon.Clone();
            }
        }

        private void PlayAlertSound(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var player = new System.Windows.Media.MediaPlayer();
                    player.Open(new Uri(path));
                    player.Play();
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    timer.Tick += (s, e) => { timer.Stop(); player.Close(); };
                    timer.Start();
                }
                catch { SystemSounds.Beep.Play(); }
            }
            else
            {
                SystemSounds.Beep.Play();
            }
        }

        private void RefreshBatteryStatus()
        {
            if (!GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            {
                BatteryPercentageText.Text = "Battery: N/A";
                ChargingStatusText.Text = "Status: Unable to read power status";
                LastUpdatedText.Text = $"Last updated: {DateTime.Now:T}";
                return;
            }

            bool isOnAcPower = status.ACLineStatus == AC_LINE_ONLINE;
            bool isCharging = isOnAcPower && (status.BatteryFlag & BATTERY_FLAG_CHARGING) == BATTERY_FLAG_CHARGING;

            string percentageText = status.BatteryLifePercent == BATTERY_PERCENT_UNKNOWN ? "Battery: N/A" : $"Battery: {status.BatteryLifePercent}%";
            string statusText = status.BatteryFlag == BATTERY_FLAG_UNKNOWN ? "Status: Unknown" :
                                isCharging ? "Status: Charging" :
                                isOnAcPower ? "Status: Plugged In (Not Charging)" : "Status: Not Charging";

            BatteryPercentageText.Text = percentageText;
            ChargingStatusText.Text = statusText;
            LastUpdatedText.Text = $"Last updated: {DateTime.Now:T}";

            if (status.BatteryLifePercent != BATTERY_PERCENT_UNKNOWN)
            {
                UpdateTrayIcon(status.BatteryLifePercent);
                _notifyIcon.Text = $"Battery Guardian - {status.BatteryLifePercent}% ({(isCharging ? "Charging" : "Not Charging")})";
                EvaluateAlerts(status.BatteryLifePercent, isCharging);
            }
        }

        private void EvaluateAlerts(int batteryPercent, bool isCharging)
        {
            int highThreshold = _settings.HighBatteryThreshold;
            int lowThreshold = _settings.LowBatteryThreshold;

            bool highCondition = isCharging && batteryPercent >= highThreshold;
            bool lowCondition = !isCharging && batteryPercent <= lowThreshold;

            if (highCondition && !_highAlertActive)
            {
                _highAlertActive = true;
                string message = $"Battery is at {batteryPercent}%. Consider unplugging the charger.";
                ShowToastNotification("Battery Guardian", message);
                PlayAlertSound(_settings.HighAlertSoundPath);
                _speechSynthesizer.SpeakAsync(message);
                StartAlarmTimerIfNeeded();
            }
            else if (!highCondition) _highAlertActive = false;

            if (lowCondition && !_lowAlertActive)
            {
                _lowAlertActive = true;
                string message = $"Battery is low at {batteryPercent}%. Please connect the charger.";
                ShowToastNotification("Battery Guardian", message);
                PlayAlertSound(_settings.LowAlertSoundPath);
                _speechSynthesizer.SpeakAsync(message);
                StartAlarmTimerIfNeeded();
            }
            else if (!lowCondition) _lowAlertActive = false;

            if (!_highAlertActive && !_lowAlertActive) _alarmTimer.Stop();
        }

        private void StartAlarmTimerIfNeeded()
        {
            if (!_alarmTimer.IsEnabled) _alarmTimer.Start();
        }
    }
}