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
using BatteryGuardian.History;
using BatteryGuardian.QuietHours;
using BatteryGuardian.ChargeLimit;

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
        private readonly BatteryHistoryAnalyzer _historyAnalyzer = new();
        private readonly IBatteryHistoryStore _historyStore = JsonFileBatteryHistoryStore.CreateDefault();
        private readonly QuietHoursEvaluator _quietHoursEvaluator;
        private DateTime _lastHistoryWriteUtc = DateTime.MinValue;
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
            _quietHoursEvaluator = new QuietHoursEvaluator(
                new SettingsQuietHoursProvider(() => _settings),
                new WindowsDoNotDisturbDetector(),
                new SystemClock());

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

            var historyItem = new System.Windows.Controls.MenuItem { Header = "Battery History & Trends" };
            historyItem.Click += (s, e) => ShowHistorySummary();
            contextMenu.Items.Add(historyItem);

            var chargeLimitItem = new System.Windows.Controls.MenuItem { Header = "Set Charge Limit..." };
            chargeLimitItem.Click += (s, e) => ShowChargeLimitDialog();
            contextMenu.Items.Add(chargeLimitItem);

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

            var aboutItem = new System.Windows.Controls.MenuItem { Header = "About Battery Guardian" };
            aboutItem.Click += (s, e) => OpenAboutWindow();
            contextMenu.Items.Add(aboutItem);
            
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

        /// <summary>Shows the aggregated battery history summary.</summary>
        private void ShowHistorySummary()
        {
            try
            {
                System.Windows.MessageBox.Show(
                    BuildHistorySummary(),
                    "Battery History & Trends",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("ShowHistorySummary", ex);
            }
        }

        /// <summary>
        /// Detects a vendor charge-limit interface and, if found, asks the user
        /// for a target percentage and applies it. If none is available, explains
        /// why rather than failing silently.
        /// </summary>
        private void ShowChargeLimitDialog()
        {
            try
            {
                var factory = new ChargeLimitControllerFactory();
                var capability = factory.Describe();

                if (!capability.IsSupported)
                {
                    System.Windows.MessageBox.Show(
                        capability.UnsupportedReason +
                        "\n\nCharge limits are a firmware feature offered by some laptop vendors " +
                        "(Lenovo, Dell, ASUS). If your laptop supports one, install the vendor's " +
                        "power-management driver and reopen this dialog.",
                        "Charge Limit Unavailable",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var controller = factory.GetAvailableController();
                if (controller == null)
                {
                    System.Windows.MessageBox.Show("No charge-limit controller is currently available.",
                        "Charge Limit", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var input = PromptForChargeLimit(capability);
                if (input == null) return;

                var result = controller.SetLimitPercent(input.Value);
                System.Windows.MessageBox.Show(
                    result.Message,
                    result.Success ? "Charge Limit Applied" : "Charge Limit Failed",
                    System.Windows.MessageBoxButton.OK,
                    result.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);

                if (result.Success)
                {
                    _settings.LastAppliedChargeLimitPercent = input.Value;
                    SaveSettings(_settings);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("ShowChargeLimitDialog", ex);
            }
        }

        /// <summary>
        /// Prompts for a charge limit percentage. Returns null if the user cancels
        /// or enters an invalid value.
        /// </summary>
        private int? PromptForChargeLimit(ChargeLimitCapability capability)
        {
            int current = capability.CurrentLimitPercent ?? _settings.LastAppliedChargeLimitPercent;
            if (current < ChargeLimitControllerBase.MinLimitPercent)
                current = ChargeLimitControllerBase.MaxLimitPercent;

            var window = new Window
            {
                Title = $"Set Charge Limit ({capability.VendorName})",
                Width = 380,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.WhiteSmoke
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"Set the battery charge limit to a value between " +
                       $"{ChargeLimitControllerBase.MinLimitPercent}% and " +
                       $"{ChargeLimitControllerBase.MaxLimitPercent}%.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var box = new System.Windows.Controls.TextBox
            {
                Text = current.ToString(),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 0, 16)
            };
            panel.Children.Add(box);

            int? result = null;

            var okButton = new System.Windows.Controls.Button
            {
                Content = "Apply",
                Width = 100,
                Height = 30,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            okButton.Click += (s, e) =>
            {
                if (int.TryParse(box.Text, out int value) &&
                    value >= ChargeLimitControllerBase.MinLimitPercent &&
                    value <= ChargeLimitControllerBase.MaxLimitPercent)
                {
                    result = value;
                    window.DialogResult = true;
                    window.Close();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Please enter a whole number between " +
                        $"{ChargeLimitControllerBase.MinLimitPercent} and " +
                        $"{ChargeLimitControllerBase.MaxLimitPercent}.",
                        "Invalid Input", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            };
            panel.Children.Add(okButton);

            window.Content = panel;
            window.ShowDialog();

            return result;
        }

        private void OpenAboutWindow()
        {
            var about = new AboutWindow { Owner = this };
            about.ShowDialog();
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

                Version? currentVersion = GetCurrentVersion();
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

        private static Version? GetCurrentVersion()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(asm.Location).FileVersion;
                if (!string.IsNullOrEmpty(fileVersion) && Version.TryParse(fileVersion, out var v))
                    return v;
            }
            catch { }

            // Fallback if FileVersionInfo fails
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
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

                    PlayAudibleAlert(_currentAlertMessage);
                    DiagnosticLog.Write("AlarmTimer_Tick: audible alert dispatched");
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

                RecordHistory(status.BatteryLifePercent, isOnAcPower);
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
                PlayAudibleAlert(newState.HighAlertMessage);
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
                PlayAudibleAlert(newState.LowAlertMessage);
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

        /// <summary>
        /// Plays the audible portion of an alert (beep + speech) unless quiet
        /// hours or Windows Focus Assist currently suppress sound. Visual
        /// notification is always handled separately by the caller.
        /// </summary>
        /// <summary>
        /// Records a battery reading into the history store at most once per
        /// five minutes, keeping the file small while still capturing trends.
        /// Honors the user's HistoryEnabled setting.
        /// </summary>
        private void RecordHistory(int percent, bool isOnAcPower)
        {
            if (!_settings.HistoryEnabled) return;

            var now = DateTime.UtcNow;
            if ((now - _lastHistoryWriteUtc).TotalMinutes < 5) return;
            _lastHistoryWriteUtc = now;

            try
            {
                _historyStore.Append(new BatteryHistoryEntry(now, percent, isOnAcPower));
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("RecordHistory", ex);
            }
        }

        /// <summary>
        /// Builds a short, human-readable summary of the recorded battery history
        /// for display in a message box.
        /// </summary>
        private string BuildHistorySummary()
        {
            var entries = _historyStore.GetAll();
            var stats = _historyAnalyzer.Analyze(
                entries,
                _settings.IdealZoneHighPercent,
                _settings.IdealZoneLowPercent);

            if (stats.TotalEntries == 0)
            {
                return "No battery history has been recorded yet. " +
                       "Leave the app running and readings will accumulate over time.";
            }

            return
                $"Recorded readings: {stats.TotalEntries}\n" +
                $"Days covered: {stats.DaysCovered}\n" +
                $"Average charge: {stats.AveragePercent}%\n" +
                $"Range: {stats.MinPercent}% - {stats.MaxPercent}%\n\n" +
                $"Ideal zone ({_settings.IdealZoneLowPercent}-{_settings.IdealZoneHighPercent}%): " +
                $"{FormatHours(stats.TimeInIdealZone)}\n" +
                $"Above {_settings.IdealZoneHighPercent}%: {FormatHours(stats.TimeAboveHighZone)}\n" +
                $"Below {_settings.IdealZoneLowPercent}%: {FormatHours(stats.TimeBelowLowZone)}\n\n" +
                $"Estimated wear from current habits: ~{stats.EstimatedAnnualWearPercent}% per year";
        }

        private static string FormatHours(TimeSpan span)
        {
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m";
        }

        private void PlayAudibleAlert(string message)
        {
            if (IsAudibleSuppressed())
            {
                DiagnosticLog.Write("PlayAudibleAlert: suppressed by quiet hours / Focus Assist");
                return;
            }

            try { SystemSounds.Beep.Play(); }
            catch (Exception ex) { DiagnosticLog.WriteException("PlayAudibleAlert.Beep", ex); }

            try { _speechSynthesizer.SpeakAsync(message); }
            catch (Exception ex) { DiagnosticLog.WriteException("PlayAudibleAlert.Speak", ex); }
        }

        /// <summary>
        /// True when audible alerting should be silenced. Delegates to the pure,
        /// unit-tested <see cref="QuietHoursEvaluator"/>. When the user has opted
        /// out of Focus Assist awareness, only the manual quiet-hours window applies.
        /// </summary>
        private bool IsAudibleSuppressed()
        {
            try
            {
                if (!_settings.RespectFocusAssist)
                    return _quietHoursEvaluator.IsWithinQuietHours();

                return _quietHoursEvaluator.IsAudibleAlertSuppressed();
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("IsAudibleSuppressed", ex);
                return false;
            }
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
