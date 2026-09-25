using System;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Media;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System.Reflection;

namespace BatteryGuardian
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        private readonly SpeechSynthesizer _speechSynthesizer;
        private readonly BatteryAlertEvaluator _evaluator = new();
        private readonly BatteryHealthService _healthService = new();
        private readonly ToastService _toastService = new();

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
        private BatteryHealthInfo? _batteryHealth;

        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _alarmTimer;

        // CHANGED: TaskbarIcon instead of WinForms.NotifyIcon
        private TaskbarIcon _notifyIcon = null!;
        private bool _highAlertActive;
        private bool _lowAlertActive;
        private bool _isExiting;
        private string _currentAlertMessage = "";
        private readonly UpdateService _updateService = new();
        private bool _updateCheckInProgress;
        private System.Windows.Controls.MenuItem? _snooze30Item;
        private System.Windows.Controls.MenuItem? _snooze1hItem;
        private System.Windows.Controls.MenuItem? _resumeAlertsItem;

        public MainWindow()
        {
            InitializeComponent();
            _speechSynthesizer = new SpeechSynthesizer();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += RefreshTimer_Tick;

            _alarmTimer = new DispatcherTimer();
            _alarmTimer.Tick += AlarmTimer_Tick;
            DiagnosticLog.Write($"CTOR: _alarmTimer created. Initial interval={_alarmTimer.Interval}");

            LoadSettings();
            InitializeTrayIcon();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new TaskbarIcon
            {
                ToolTipText = "Battery Guardian"
            };

            // Try three strategies to load the battery icon, in order of reliability.
            System.Drawing.Icon? loadedIcon = null;

            // Strategy 1: extract the icon embedded in the exe (ApplicationIcon).
            // Works in debug and self-contained single-file publishes.
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch { /* fall through */ }

            // Strategy 2: load from the embedded WPF pack resource.
            if (loadedIcon == null)
            {
                try
                {
                    var iconUri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
                    var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                    if (streamInfo != null)
                    {
                        using (var stream = streamInfo.Stream)
                        using (var tempIcon = new System.Drawing.Icon(stream))
                        {
                            loadedIcon = (System.Drawing.Icon)tempIcon.Clone();
                        }
                    }
                }
                catch { /* fall through */ }
            }

            // Strategy 3: fall back to the generic Windows application icon.
            _notifyIcon.Icon = loadedIcon ?? System.Drawing.SystemIcons.Application;

            var contextMenu = new System.Windows.Controls.ContextMenu();

            var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
            openItem.Click += (s, e) => RestoreWindow();
            contextMenu.Items.Add(openItem);

            var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
            settingsItem.Click += (s, e) => OpenSettingsWindow();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var testHighItem = new System.Windows.Controls.MenuItem { Header = "Test High Alert" };
            testHighItem.Click += (s, e) => TriggerTestAlert(high: true);
            contextMenu.Items.Add(testHighItem);

            var testLowItem = new System.Windows.Controls.MenuItem { Header = "Test Low Alert" };
            testLowItem.Click += (s, e) => TriggerTestAlert(high: false);
            contextMenu.Items.Add(testLowItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var diagItem = new System.Windows.Controls.MenuItem { Header = "Open Diagnostic Log" };
            diagItem.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = DiagnosticLog.GetLogPath(),
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            contextMenu.Items.Add(diagItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            _snooze30Item = new System.Windows.Controls.MenuItem { Header = "Snooze 30 minutes" };
            _snooze30Item.Click += (s, e) => Snooze(TimeSpan.FromMinutes(30));
            contextMenu.Items.Add(_snooze30Item);

            _snooze1hItem = new System.Windows.Controls.MenuItem { Header = "Snooze 1 hour" };
            _snooze1hItem.Click += (s, e) => Snooze(TimeSpan.FromHours(1));
            contextMenu.Items.Add(_snooze1hItem);

            _resumeAlertsItem = new System.Windows.Controls.MenuItem { Header = "Resume Alerts" };
            _resumeAlertsItem.Click += (s, e) => CancelSnooze();
            contextMenu.Items.Add(_resumeAlertsItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var updateItem = new System.Windows.Controls.MenuItem { Header = "Check for Updates" };
            updateItem.Click += async (s, e) => await CheckForUpdatesAsync(manualCheck: true);
            contextMenu.Items.Add(updateItem);

            contextMenu.Items.Add(new System.Windows.Controls.Separator());

            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => { _isExiting = true; Close(); };
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.TrayMouseDoubleClick += (s, e) => RestoreWindow();
        }

        /// <summary>
        /// Returns true if the user has snoozed alerts and the snooze window is still active.
        /// If the snooze has just expired, clears it and resets alert state so new alerts can fire.
        /// </summary>
        private bool IsSnoozed()
        {
            if (!_settings.SnoozedUntilUtc.HasValue) return false;

            if (DateTime.UtcNow >= _settings.SnoozedUntilUtc.Value)
            {
                // Snooze expired — clear it, reset alert state, allow new alerts.
                _settings.SnoozedUntilUtc = null;
                SaveSettings(_settings);

                _highAlertActive = false;
                _lowAlertActive = false;
                _currentAlertMessage = "";
                _alarmTimer.Stop();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Suppresses all alerts for the given duration and immediately silences any active alert.
        /// </summary>
        private void Snooze(TimeSpan duration)
        {
            _settings.SnoozedUntilUtc = DateTime.UtcNow.Add(duration);
            SaveSettings(_settings);

            // Immediately silence any active alert
            _highAlertActive = false;
            _lowAlertActive = false;
            _currentAlertMessage = "";
            _alarmTimer.Stop();

            UpdateSnoozeMenuState();
            RefreshBatteryStatus(); // refresh tooltip immediately
        }

        /// <summary>
        /// Clears the snooze and allows alerts to fire on the next evaluation.
        /// </summary>
        private void CancelSnooze()
        {
            _settings.SnoozedUntilUtc = null;
            SaveSettings(_settings);

            _highAlertActive = false;
            _lowAlertActive = false;

            UpdateSnoozeMenuState();
            RefreshBatteryStatus();
        }

        /// <summary>
        /// Enables/disables the snooze/resume menu items based on the current state.
        /// </summary>
        private void UpdateSnoozeMenuState()
        {
            bool snoozed = _settings.SnoozedUntilUtc.HasValue &&
                           DateTime.UtcNow < _settings.SnoozedUntilUtc.Value;

            if (_snooze30Item != null) _snooze30Item.IsEnabled = !snoozed;
            if (_snooze1hItem != null) _snooze1hItem.IsEnabled = !snoozed;

            if (_resumeAlertsItem != null)
            {
                _resumeAlertsItem.IsEnabled = snoozed;
                if (snoozed && _settings.SnoozedUntilUtc.HasValue)
                {
                    var remaining = _settings.SnoozedUntilUtc.Value - DateTime.UtcNow;
                    int minutesLeft = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                    _resumeAlertsItem.Header = $"Resume Alerts ({minutesLeft} min left)";
                }
                else
                {
                    _resumeAlertsItem.Header = "Resume Alerts";
                }
            }
        }

        private async Task CheckForUpdatesAsync(bool manualCheck)
        {
            if (_updateCheckInProgress) return;
            _updateCheckInProgress = true;

            try
            {
                if (!manualCheck)
                {
                    if (!_settings.CheckForUpdatesAutomatically) return;

                    if (_settings.LastUpdateCheckUtc.HasValue &&
                        (DateTime.UtcNow - _settings.LastUpdateCheckUtc.Value).TotalHours < 24)
                    {
                        return;
                    }
                }

                Version? currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null) return;

                UpdateInfo? update = await _updateService.CheckForUpdateAsync(currentVersion);

                _settings.LastUpdateCheckUtc = DateTime.UtcNow;
                SaveSettings(_settings);

                if (update != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"A new version of Battery Guardian is available.\n\n" +
                        $"Current: {currentVersion}\n" +
                        $"Latest:  {update.LatestVersion}\n\n" +
                        $"Open the download page?",
                        "Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes && !string.IsNullOrEmpty(update.ReleaseUrl))

                        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(update.ReleaseUrl))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = update.ReleaseUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else if (manualCheck)
                {
                    System.Windows.MessageBox.Show("You are already running the latest version.",
                        "No Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch
            {
                if (manualCheck)
                {
                    System.Windows.MessageBox.Show("Could not check for updates right now. Please try again later.",
                        "Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                _updateCheckInProgress = false;
            }
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
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
            DiagnosticLog.Enabled = _settings.DiagnosticLoggingEnabled;
            if (DiagnosticLog.Enabled)
            {
                DiagnosticLog.Clear();
                DiagnosticLog.Write("=== App STARTED ===");
            }

            EnsureStartup();
            LoadBatteryHealth();
            RefreshBatteryStatus();
            _refreshTimer.Start();
            _ = CheckForUpdatesAsync(manualCheck: false);
            UpdateSnoozeMenuState();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            DiagnosticLog.Write("=== App CLOSED ===");

            _refreshTimer.Stop();
            _alarmTimer.Stop();
            _notifyIcon?.Dispose();
            _speechSynthesizer.Dispose();
            // Do NOT dispose _warningIcon — it's a shared system icon.
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) => RefreshBatteryStatus();

        private void AlarmTimer_Tick(object? sender, EventArgs e)
        {
            DiagnosticLog.Write($"AlarmTimer_Tick FIRED. " +
                                $"highActive={_highAlertActive}, lowActive={_lowAlertActive}, " +
                                $"msg='{_currentAlertMessage}'");

            if (_highAlertActive || _lowAlertActive)
            {
                if (!string.IsNullOrEmpty(_currentAlertMessage))
                {
                    try
                    {
                        ShowToastNotification("Battery Guardian (Reminder)", _currentAlertMessage);
                        DiagnosticLog.Write("AlarmTimer_Tick: toast shown");
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.WriteException("AlarmTimer_Tick.ShowToastNotification", ex);
                    }

                    try
                    {
                        SystemSounds.Beep.Play();
                        DiagnosticLog.Write("AlarmTimer_Tick: beep played");
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.WriteException("AlarmTimer_Tick.Beep", ex);
                    }

                    try
                    {
                        _speechSynthesizer.SpeakAsync(_currentAlertMessage);
                        DiagnosticLog.Write("AlarmTimer_Tick: speech queued");
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.WriteException("AlarmTimer_Tick.Speak", ex);
                    }
                }
                else
                {
                    DiagnosticLog.Write("AlarmTimer_Tick: alert active but message is EMPTY");
                }
            }
            else
            {
                DiagnosticLog.Write("AlarmTimer_Tick: no alert active, stopping timer");
                _alarmTimer.Stop();
                _currentAlertMessage = "";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshBatteryStatus();

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

        private void TriggerTestAlert(bool high)
        {
            if (high)
            {
                _highAlertActive = false;
                string message = $"TEST: Battery is at {_settings.HighBatteryThreshold}%. Consider unplugging the charger.";
                _currentAlertMessage = message;
                ShowToastNotification("Battery Guardian (Test)", message);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(message);
                _highAlertActive = true;
                StartAlarmTimerIfNeeded(forceRestart: true);
            }
            else
            {
                _lowAlertActive = false;
                string message = $"TEST: Battery is low at {_settings.LowBatteryThreshold}%. Please connect the charger.";
                _currentAlertMessage = message;
                ShowToastNotification("Battery Guardian (Test)", message);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(message);
                _lowAlertActive = true;
                StartAlarmTimerIfNeeded(forceRestart: true);
            }
        }

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
            var settingsWindow = new SettingsWindow(_settings) { Owner = this };
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

        // ------- Battery Health -------

        private void LoadBatteryHealth()
        {
            _batteryHealth = _healthService.GetBatteryHealth();

            if (_batteryHealth == null)
            {
                BatteryHealthText.Text = "Battery health: Unavailable";
                return;
            }

            BatteryHealthText.Text =
                $"Battery health: {_batteryHealth.HealthPercent}% ({_batteryHealth.HealthLabel}) — " +
                $"{_batteryHealth.FullChargeCapacityMwh:N0} / {_batteryHealth.DesignCapacityMwh:N0} mWh";
        }

        // ------- Battery Logic -------

        // CHANGED: no-op — we no longer draw the tray icon manually
        private void UpdateTrayIcon(int percentage)
        {
            // Stage A: static icon. Dynamic icons will be added in a later stage if desired.
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
            EstimatedTimeText.Text = BuildEstimatedTimeText(status, isOnAcPower, isCharging);

            if (status.BatteryLifePercent != BATTERY_PERCENT_UNKNOWN)
            {
                // CHANGED: ToolTipText instead of Text
                string chargeWord = isCharging ? "Charging" : (isOnAcPower ? "Plugged In" : "Not Charging");
                string timeHint = "";
                if (!isOnAcPower)
                {
                    string remaining = GetBestTimeRemaining(status);
                    if (!string.IsNullOrEmpty(remaining)) timeHint = $" ~{remaining} left";
                }
                else if (isCharging)
                {
                    string toFull = FormatTimeSpan(status.BatteryFullLifeTime);
                    if (!string.IsNullOrEmpty(toFull)) timeHint = $" ~{toFull} to full";
                }

                string snoozeSuffix = "";
                if (_settings.SnoozedUntilUtc.HasValue && DateTime.UtcNow < _settings.SnoozedUntilUtc.Value)
                {
                    var remaining = _settings.SnoozedUntilUtc.Value - DateTime.UtcNow;
                    int minutesLeft = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                    snoozeSuffix = $" [Snoozed {minutesLeft}m]";
                }

                string tooltip = $"Battery Guardian - {status.BatteryLifePercent}% ({chargeWord}){timeHint}{snoozeSuffix}";
                if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 60) + "...";
                _notifyIcon.ToolTipText = tooltip;

                EvaluateAlerts(status.BatteryLifePercent, isCharging, isOnAcPower);
            }
            else
            {
                EstimatedTimeText.Text = "";
            }
        }

        private string FormatTimeSpan(uint seconds)
        {
            if (seconds == 0 || seconds == uint.MaxValue) return "";
            if (seconds > 360_000) return "";
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        private uint? GetEstimatedRunTimeFromWmi()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT EstimatedRunTime FROM Win32_Battery"))
                {
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        var runTime = (uint)queryObj["EstimatedRunTime"];
                        if (runTime > 0 && runTime < 6000)
                        {
                            DiagnosticLog.Write($"WMI: returned {runTime} min in {sw.ElapsedMilliseconds}ms");
                            return runTime;
                        }
                    }
                }
                DiagnosticLog.Write($"WMI: no valid value in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("GetEstimatedRunTimeFromWmi", ex);
            }
            return null;
        }

        private string GetBestTimeRemaining(SYSTEM_POWER_STATUS status)
        {
            string native = FormatTimeSpan(status.BatteryLifeTime);
            if (!string.IsNullOrEmpty(native)) return native;

            uint? wmiMinutes = GetEstimatedRunTimeFromWmi();
            if (wmiMinutes.HasValue) return FormatTimeSpan(wmiMinutes.Value * 60);

            return "";
        }

        private string BuildEstimatedTimeText(SYSTEM_POWER_STATUS status, bool isOnAcPower, bool isCharging)
        {
            if (isCharging)
            {
                string toFull = FormatTimeSpan(status.BatteryFullLifeTime);
                return string.IsNullOrEmpty(toFull) ? "Time to full: Calculating..." : $"Time to full: {toFull}";
            }

            if (isOnAcPower) return "";

            string remaining = GetBestTimeRemaining(status);
            return string.IsNullOrEmpty(remaining) ? "Time remaining: Calculating..." : $"Time remaining: {remaining}";
        }

        private void EvaluateAlerts(int batteryPercent, bool isCharging, bool isOnAcPower)
        {
            DiagnosticLog.Write($"EvaluateAlerts: batteryPercent={batteryPercent}, " +
                    $"isCharging={isCharging}, isOnAcPower={isOnAcPower}, " +
                    $"highActive={_highAlertActive}, lowActive={_lowAlertActive}");

            if (IsSnoozed())
            {
                // While snoozed, suppress all alerts. The tooltip still updates via RefreshBatteryStatus.
                UpdateSnoozeMenuState();
                return;
            }

            AlertState newState = _evaluator.Evaluate(
                batteryPercent,
                isOnAcPower,
                _settings.HighBatteryThreshold,
                _settings.LowBatteryThreshold);

            if (newState.HighAlertShouldBeActive && !_highAlertActive)
            {
                _highAlertActive = true;
                _currentAlertMessage = newState.HighAlertMessage;
                ShowToastNotification("Battery Guardian", newState.HighAlertMessage);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(newState.HighAlertMessage);
                StartAlarmTimerIfNeeded(forceRestart: true);
            }
            else if (!newState.HighAlertShouldBeActive && _highAlertActive)
            {
                _highAlertActive = false;
                if (!_lowAlertActive) _currentAlertMessage = "";
            }

            if (newState.LowAlertShouldBeActive && !_lowAlertActive)
            {
                _lowAlertActive = true;
                _currentAlertMessage = newState.LowAlertMessage;
                ShowToastNotification("Battery Guardian", newState.LowAlertMessage);
                SystemSounds.Beep.Play();
                _speechSynthesizer.SpeakAsync(newState.LowAlertMessage);
                StartAlarmTimerIfNeeded(forceRestart: true);
            }
            else if (!newState.LowAlertShouldBeActive && _lowAlertActive)
            {
                _lowAlertActive = false;
                if (!_highAlertActive) _currentAlertMessage = "";
            }

            if (_highAlertActive || _lowAlertActive)
            {
                StartAlarmTimerIfNeeded();
            }
            else
            {
                _alarmTimer.Stop();
                _currentAlertMessage = "";
            }

            UpdateSnoozeMenuState();

            DiagnosticLog.Write($"EvaluateAlerts END: highActive={_highAlertActive}, " +
                    $"lowActive={_lowAlertActive}, timerEnabled={_alarmTimer.IsEnabled}");
        }

        private void StartAlarmTimerIfNeeded(bool forceRestart = false)
        {
            var desiredInterval = TimeSpan.FromSeconds(_settings.AlertRepeatIntervalSeconds);

            // If the timer is already running and we're not forcing a restart,
            // do NOTHING. Setting .Interval on a running DispatcherTimer resets
            // its countdown, which prevents long intervals from ever firing.
            if (_alarmTimer.IsEnabled && !forceRestart)
            {
                return;
            }

            _alarmTimer.Stop();
            _alarmTimer.Interval = desiredInterval;
            _alarmTimer.Start();
        }

        private void ShowToastNotification(string title, string message)
        {
            _toastService.Show(title, message);
        }
    }
}