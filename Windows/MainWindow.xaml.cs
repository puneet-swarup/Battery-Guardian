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
using BatteryGuardian.Signals;
using BatteryGuardian.SmartPlug;

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
 private SmartPlugController? _smartPlugController;
 private readonly TuyaCredentialStore _smartPlugCredStore = new TuyaCredentialStore();

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
 private const int RefreshIntervalSeconds = 30;

 // The band the levels were in last time. One pair, so one band.
 private BatteryBand _band = BatteryBand.Unknown;
 private bool _plugFailureNotified;
 private int _plugRestoreAttempts;
 private const int MaxPlugRestoreAttempts = 6;
        private System.Windows.Controls.MenuItem? _snooze30Item;
        private System.Windows.Controls.MenuItem? _snooze1hItem;
        private System.Windows.Controls.MenuItem? _resumeAlertsItem;

        public MainWindow()
        {
            InitializeComponent();
            _speechSynthesizer = new SpeechSynthesizer();

            // The plug evaluation is capped below this interval on purpose; see
            // SmartPlugController.EvaluationTimeoutSeconds.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds) };
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

            // Sleep, hibernate and shutdown. Lid close is one of these: Windows
            // turns it into a suspend or a shutdown, whichever the power plan
            // says. A lid set to "do nothing" raises nothing, and the threshold
            // rules keep working as usual.
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
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

            var plugStatusItem = new System.Windows.Controls.MenuItem { Header = "Read Smart Plug Now" };
            plugStatusItem.ToolTip = "Asks the plug for its relay state and shows what it answered.";
            plugStatusItem.Click += async (s, e) => await ShowPlugStatusAsync();
            contextMenu.Items.Add(plugStatusItem);

            var testSleepItem = new System.Windows.Controls.MenuItem { Header = "Test Sleep Handling" };
            testSleepItem.ToolTip = "Cuts the plug, waits three seconds, puts it back, and logs what happened.";
            testSleepItem.Click += async (s, e) => await TestSleepHandlingAsync();
            contextMenu.Items.Add(testSleepItem);

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

            _snooze30Item = new System.Windows.Controls.MenuItem
            {
                Header = "Snooze 30 minutes",
                ToolTip = "Silences alerts. Smart plug control keeps running."
            };
            _snooze30Item.Click += (s, e) => Snooze(TimeSpan.FromMinutes(30));
            contextMenu.Items.Add(_snooze30Item);

            _snooze1hItem = new System.Windows.Controls.MenuItem
            {
                Header = "Snooze 1 hour",
                ToolTip = "Silences alerts. Smart plug control keeps running."
            };
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
                if (!manualCheck && !UpdateService.IsCheckDue(
                        _settings.CheckForUpdatesAutomatically,
                        _settings.LastUpdateCheckUtc,
                        DateTime.UtcNow))
                {
                    return;
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

            // If the machine was switched off rather than slept, this is where
            // the plug gets put back: the app starts fresh and finds the state
            // recorded before it went away.
            _ = RestoreAfterSleepAsync();
            UpdateSnoozeMenuState();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            DiagnosticLog.Write("=== App CLOSED ===");

            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;

            _refreshTimer.Stop();
            _alarmTimer.Stop();
            _historyStore.Flush();
            _notifyIcon?.Dispose();
            _speechSynthesizer.Dispose();
            // Do NOT dispose _warningIcon — it's a shared system icon.
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // Cheap when there is nothing to restore: it returns on the first
            // check. A pending restore is retried here rather than abandoned.
            _ = RestoreAfterSleepAsync();

            RefreshBatteryStatus();

            // The check throttles itself to once a day, so this is a no-op on
            // almost every tick. Without it a tray app that runs for weeks only
            // ever checked at startup.
            _ = CheckForUpdatesAsync(manualCheck: false);
        }

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

                // The controller caches its configuration; without this a change
                // to the plug settings only took effect after a restart.
                _smartPlugController?.Dispose();
                _smartPlugController = null;
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

                // One reading, classified once per pair of levels. The alerts
                // and the plug no longer compare percentages themselves.
                // One pair of levels for everything. The alerts and the plug used
                // to hold their own, which is how a reading could alert at 95 and
                // leave the charger running.
                ThresholdSignal signal = ThresholdMonitor.Evaluate(
                    status.BatteryLifePercent, true,
                    new ThresholdPair(_settings.HighBatteryThreshold, _settings.LowBatteryThreshold),
                    _band);
                _band = signal.Band;

                EvaluateAlerts(status.BatteryLifePercent, signal.Band, isOnAcPower);
                _ = EvaluateSmartPlugAsync(signal.Band);
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

        private void EvaluateAlerts(int batteryPercent, BatteryBand band, bool isOnAcPower)
        {
            DiagnosticLog.Write($"EvaluateAlerts: batteryPercent={batteryPercent}, " +
                    $"band={band}, isOnAcPower={isOnAcPower}, " +
                    $"highActive={_highAlertActive}, lowActive={_lowAlertActive}");

            if (IsSnoozed())
            {
                // While snoozed, suppress all alerts. The tooltip still updates via RefreshBatteryStatus.
                UpdateSnoozeMenuState();
                return;
            }

            AlertState newState = _evaluator.Evaluate(batteryPercent, band, isOnAcPower);

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

        /// <summary>
        /// Runs the smart-plug decision for one battery reading.
        /// </summary>
        /// <remarks>
        /// Snooze is deliberately not consulted here. A snooze means "stop
        /// telling me", not "stop protecting me": the reminders go quiet and the
        /// plug carries on switching the charger. Adding an IsSnoozed() check
        /// would quietly turn snooze into a way to disable the protection.
        /// </remarks>
        private async Task EvaluateSmartPlugAsync(BatteryBand band)
 {
  try
  {
   if (!_settings.SmartPlugEnabled) return;
   var controller = EnsureSmartPlugController();
   if (controller == null) return;

   SmartPlugStatus status = await controller.EvaluateAsync(band);

   // A snooze silences the reminders, not the protection. A command that did
   // not reach the plug is therefore reported once, even mid-snooze.
   if (SmartPlugFailurePolicy.ShouldNotify(status, IsSnoozed(), _plugFailureNotified))
   {
    _plugFailureNotified = true;
    ShowToastNotification("Battery Guardian",
     "The smart plug did not respond, so the charger was not switched.");
   }
   else if (status.LastError == SmartPlugError.None)
   {
    _plugFailureNotified = false;
   }
  }
  catch (Exception ex) { DiagnosticLog.WriteException("EvaluateSmartPlugAsync", ex); }
 }

 // ------- Sleep, shutdown and return -------

 private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
 {
  DiagnosticLog.WriteAlways($"SLEEP: PowerModeChanged {e.Mode}");
  if (e.Mode == PowerModes.Suspend) HandleGoingAway("suspend");
  else if (e.Mode == PowerModes.Resume) _ = RestoreAfterSleepAsync();
 }

 private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
 {
  DiagnosticLog.WriteAlways("SLEEP: SessionEnding");
  HandleGoingAway("session end");
 }

 /// <summary>
 /// Records the relay state and cuts the plug. A shutdown gives the app a few
 /// seconds at most, so this is one attempt with a short timeout rather than the
 /// controller's retrying evaluation.
 /// </summary>
 /// <remarks>
 /// The state is recorded even when the command fails: on the way back the app
 /// still knows what the plug was meant to be, and the user is told if the
 /// switch did not happen.
 /// </remarks>
 /// <summary>
 /// Runs the sleep path on demand so it can be checked without closing the lid:
 /// cut the plug, wait, put it back, and report what happened.
 /// </summary>
 /// <summary>
 /// Asks the plug what it is doing, right now, and puts the answer on screen so
 /// it can be compared with the physical socket. Nothing is switched.
 /// </summary>
 private async Task ShowPlugStatusAsync()
 {
  try
  {
   SmartPlugController? controller = EnsureSmartPlugController();
   if (controller == null)
   {
    System.Windows.MessageBox.Show(
     "The smart plug is not configured.\n\nTurn it on in Settings and enter the device ID, IP address and local key.",
     "Smart Plug", MessageBoxButton.OK, MessageBoxImage.Information);
    return;
   }

   SmartPlugStatus status = await controller.ReadStatusAsync(TimeSpan.FromSeconds(5));
   DiagnosticLog.WriteAlways($"PLUG READ: relay={status.Relay} error={status.LastError} message='{status.Message}'");

   string text =
    $"Relay state: {status.Relay}\n" +
    $"Answer: {(status.Connected ? "yes" : "no")}\n" +
    $"Last error: {status.LastError}\n" +
    $"Message: {status.Message}\n\n" +
    $"Does that match the socket? A plug that is switched off should say Off.";

   System.Windows.MessageBox.Show(text, "Smart Plug",
    MessageBoxButton.OK,
    status.Connected ? MessageBoxImage.Information : MessageBoxImage.Warning);
  }
  catch (Exception ex)
  {
   DiagnosticLog.WriteAlways($"PLUG READ failed: {ex.GetType().Name}: {ex.Message}");
   System.Windows.MessageBox.Show("Could not read the plug: " + ex.Message,
    "Smart Plug", MessageBoxButton.OK, MessageBoxImage.Warning);
  }
 }

 private async Task TestSleepHandlingAsync()
 {
  try
  {
   DiagnosticLog.WriteAlways("SLEEP TEST: starting.");
   HandleGoingAway("test");
   await Task.Delay(TimeSpan.FromSeconds(3));
   await RestoreAfterSleepAsync();

   ShowToastNotification("Battery Guardian",
    $"Sleep handling tested. Log: {DiagnosticLog.GetLogPath()}");
   DiagnosticLog.WriteAlways("SLEEP TEST: finished.");
  }
  catch (Exception ex)
  {
   DiagnosticLog.WriteAlways($"SLEEP TEST: failed - {ex.GetType().Name}: {ex.Message}");
  }
 }

 private void HandleGoingAway(string reason)
 {
  // Nothing in here may wait. Holding the UI thread while a socket opens is what
  // keeps the machine awake on the way to sleep, and an app that fights suspend
  // is an app people uninstall. The switch still goes down - the command is
  // handed to the thread pool and the handler returns at once.
  try
  {
   SmartPlugController? controller = _smartPlugController;

   DiagnosticLog.WriteAlways(
    $"SLEEP: going away ({reason}). plugEnabled={_settings.SmartPlugEnabled} " +
    $"switchOffOnSleep={_settings.SmartPlugOffWhenSleeping} " +
    $"controller={(controller == null ? "none" : "present")} " +
    $"lastSeen={(controller == null ? "?" : controller.Status.Relay.ToString())} " +
    $"intent={(controller == null ? "?" : controller.IntendedRelay.ToString())}");

   if (!SmartPlugSleepPolicy.ShouldSwitchOff(_settings.SmartPlugEnabled, _settings.SmartPlugOffWhenSleeping))
   {
    DiagnosticLog.WriteAlways("SLEEP: not cutting - feature or plug control is off.");
    return;
   }

   if (controller == null)
   {
    DiagnosticLog.WriteAlways("SLEEP: not cutting - no controller (plug not configured).");
    return;
   }

   // What we knew a moment ago: no network read here. The last thing the plug
   // said, or failing that the last thing we asked it to do.
   RelayState known = controller.Status.Relay != RelayState.Unknown
    ? controller.Status.Relay
    : controller.IntendedRelay;

   // Record it even when it is unknown: the switch still goes down, and the low
   // threshold brings it back if nothing else does.
   _settings.SmartPlugStateBeforeSleep = SmartPlugSleepPolicy.Remember(known);
   _plugRestoreAttempts = 0;      // a new sleep earns a new retry budget
   SaveSettings(_settings);

   DiagnosticLog.WriteAlways($"SLEEP: cutting from '{known}', remembered '{_settings.SmartPlugStateBeforeSleep}'.");

   // Fire and forget. If the machine suspends before this completes, the plug
   // stays on - accepted, and logged on the way back in.
   _ = Task.Run(async () =>
   {
    try
    {
     SmartPlugStatus status = await controller.SetRelayAsync(
      on: false,
      timeout: TimeSpan.FromSeconds(2),
      verify: false,
      waitForGate: false,
      retryBackoffMs: Array.Empty<int>());

     DiagnosticLog.WriteAlways($"SLEEP: cut finished, error={status.LastError} message='{status.Message}'.");
    }
    catch (Exception ex)
    {
     DiagnosticLog.WriteAlways($"SLEEP: cut failed - {ex.GetType().Name}: {ex.Message}");
    }
   });
  }
  catch (Exception ex) { DiagnosticLog.WriteException("HandleGoingAway", ex); }
 }

 /// <summary>
 /// Puts the plug back the way it was before the machine went away, then hands
 /// control back to the threshold rules.
 /// </summary>
 private async Task RestoreAfterSleepAsync()
 {
  try
  {
   bool? restore = SmartPlugSleepPolicy.StateToRestore(_settings.SmartPlugStateBeforeSleep);
   if (restore == null) return;

   SmartPlugController? controller = EnsureSmartPlugController();
   if (controller == null) return;

   SmartPlugStatus status = await controller.SetRelayAsync(
    on: restore.Value,
    timeout: TimeSpan.FromSeconds(5),
    verify: true);

   DiagnosticLog.WriteAlways(
    $"RESTORE: wanted {(restore.Value ? "on" : "off")}, error={status.LastError}, message='{status.Message}', plug now reports {status.Relay}.");

   if (status.LastError == SmartPlugError.None)
   {
    _settings.SmartPlugStateBeforeSleep = string.Empty;
    _plugRestoreAttempts = 0;
    SaveSettings(_settings);
    return;
   }

   // Keep the record and try again on the next reading. Between the thresholds
   // the rules do nothing, so a failed restore would otherwise leave the
   // charger cut until the battery fell to the low level.
   _plugRestoreAttempts++;
   if (_plugRestoreAttempts < MaxPlugRestoreAttempts) return;

   _settings.SmartPlugStateBeforeSleep = string.Empty;
   _plugRestoreAttempts = 0;
   SaveSettings(_settings);
   ShowToastNotification("Battery Guardian",
    "The smart plug did not answer, so its state may not have been restored.");
  }
  catch (Exception ex) { DiagnosticLog.WriteException("RestoreAfterSleepAsync", ex); }
 }

 private SmartPlugController? EnsureSmartPlugController()
 {
 if (!_settings.SmartPlugEnabled) { _smartPlugController?.Dispose(); _smartPlugController = null; return null; }
 var localKey = _smartPlugCredStore.Load();
 if (localKey == null) return null;
 // The levels are not part of the plug's configuration any more: the band
 // arrives already classified, from the same pair the alerts use.
 var config = new SmartPlugConfig(true, _settings.HighBatteryThreshold, _settings.LowBatteryThreshold, _settings.SmartPlugDeviceId, _settings.SmartPlugHost, _settings.SmartPlugPort, _settings.SmartPlugRelayDp, 3);
 if (config.ValidationError() != null) return null;
 if (_smartPlugController == null)
 {
 var keyBytes = TuyaLocalKey.Parse(localKey);
 var plug = new TuyaSmartPlug(_settings.SmartPlugDeviceId, _settings.SmartPlugHost, _settings.SmartPlugPort, keyBytes, _settings.SmartPlugRelayDp);
 _smartPlugController = new SmartPlugController(plug, config);
 }
 return _smartPlugController;
 }

 private void ShowToastNotification(string title, string message)
        {
            _toastService.Show(title, message);
        }
    }
}
