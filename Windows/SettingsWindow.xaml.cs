using System;
using System.Windows;
using BatteryGuardian.QuietHours;
using BatteryGuardian.SmartPlug;

namespace BatteryGuardian
{
    public partial class SettingsWindow : Window
    {
        public Settings Settings { get; private set; }

        /// <summary>Values read back from the smart-plug fields, already validated.</summary>
        private sealed class PlugInput
        {
            public string DeviceId = string.Empty;
            public string Host = string.Empty;
            public int Port = 6668;
            public int RelayDp = 1;
            public int High = 95;
            public int Low = 15;
            public byte[]? Key;
        }

        public SettingsWindow(Settings currentSettings)
        {
            InitializeComponent();

            // One clone rather than a hand-written list of fields: the list
            // went stale the moment the smart plug added eight of them.
            Settings = currentSettings.Clone();

            HighThresholdBox.Text = Settings.HighBatteryThreshold.ToString();
            LowThresholdBox.Text = Settings.LowBatteryThreshold.ToString();
            RepeatIntervalBox.Text = Settings.AlertRepeatIntervalSeconds.ToString();
            CheckForUpdatesCheckBox.IsChecked = Settings.CheckForUpdatesAutomatically;
            DiagnosticLoggingCheckBox.IsChecked = Settings.DiagnosticLoggingEnabled;

            HistoryEnabledCheckBox.IsChecked = Settings.HistoryEnabled;
            IdealZoneLowBox.Text = Settings.IdealZoneLowPercent.ToString();
            IdealZoneHighBox.Text = Settings.IdealZoneHighPercent.ToString();

            QuietHoursEnabledCheckBox.IsChecked = Settings.QuietHoursEnabled;
            QuietStartBox.Text = TimeOfDayText.Format(Settings.QuietHoursStartMinutes);
            QuietEndBox.Text = TimeOfDayText.Format(Settings.QuietHoursEndMinutes);
            RespectFocusAssistCheckBox.IsChecked = Settings.RespectFocusAssist;

            PlugEnabledCheckBox.IsChecked = Settings.SmartPlugEnabled;
            PlugDeviceIdBox.Text = Settings.SmartPlugDeviceId;
            PlugHostBox.Text = Settings.SmartPlugHost;
            PlugPortBox.Text = Settings.SmartPlugPort.ToString();
            PlugRelayDpBox.Text = Settings.SmartPlugRelayDp.ToString();
            PlugHighBox.Text = Settings.SmartPlugHighThreshold.ToString();
            PlugLowBox.Text = Settings.SmartPlugLowThreshold.ToString();

            try
            {
                var storedKey = new TuyaCredentialStore().Load();
                if (storedKey != null) PlugKeyBox.Password = storedKey;
            }
            catch
            {
                // A key that cannot be read is reported when the user saves.
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(HighThresholdBox.Text, out int high) ||
                !int.TryParse(LowThresholdBox.Text, out int low) ||
                !int.TryParse(RepeatIntervalBox.Text, out int repeatSec) || repeatSec <= 0)
            {
                Warn("Please enter valid numeric values for the alert settings.");
                return;
            }

            if (low >= high)
            {
                Warn("Low threshold must be strictly lower than High threshold.");
                return;
            }

            if (!int.TryParse(IdealZoneLowBox.Text, out int idealLow) ||
                !int.TryParse(IdealZoneHighBox.Text, out int idealHigh) ||
                idealLow < 0 || idealHigh > 100 || idealLow >= idealHigh)
            {
                Warn("Ideal zone must be two numbers between 0 and 100, with the low value below the high value.");
                return;
            }

            bool quietEnabled = QuietHoursEnabledCheckBox.IsChecked == true;
            if (!TimeOfDayText.TryParse(QuietStartBox.Text, out int quietStart))
            {
                Warn("Quiet-hours start must be a time in HH:mm format (e.g. 22:00).");
                return;
            }

            if (!TimeOfDayText.TryParse(QuietEndBox.Text, out int quietEnd))
            {
                Warn("Quiet-hours end must be a time in HH:mm format (e.g. 07:00).");
                return;
            }

            if (quietEnabled && quietStart == quietEnd)
            {
                Warn("Quiet-hours start and end must not be identical.");
                return;
            }

            Settings.HighBatteryThreshold = high;
            Settings.LowBatteryThreshold = low;
            Settings.AlertRepeatIntervalSeconds = repeatSec;
            Settings.CheckForUpdatesAutomatically = CheckForUpdatesCheckBox.IsChecked == true;
            Settings.DiagnosticLoggingEnabled = DiagnosticLoggingCheckBox.IsChecked == true;

            Settings.HistoryEnabled = HistoryEnabledCheckBox.IsChecked == true;
            Settings.IdealZoneLowPercent = idealLow;
            Settings.IdealZoneHighPercent = idealHigh;

            Settings.QuietHoursEnabled = quietEnabled;
            Settings.QuietHoursStartMinutes = quietStart;
            Settings.QuietHoursEndMinutes = quietEnd;
            Settings.RespectFocusAssist = RespectFocusAssistCheckBox.IsChecked == true;

            bool plugEnabled = PlugEnabledCheckBox.IsChecked == true;
            if (!TryReadPlugInputs(plugEnabled, out PlugInput plug, out string plugError))
            {
                Warn(plugError);
                return;
            }

            Settings.SmartPlugEnabled = plugEnabled;
            Settings.SmartPlugDeviceId = plug.DeviceId;
            Settings.SmartPlugHost = plug.Host;
            Settings.SmartPlugPort = plug.Port;
            Settings.SmartPlugRelayDp = plug.RelayDp;
            Settings.SmartPlugHighThreshold = plug.High;
            Settings.SmartPlugLowThreshold = plug.Low;

            if (plug.Key != null)
            {
                try
                {
                    new TuyaCredentialStore().Save(PlugKeyBox.Password.Trim());
                }
                catch (Exception ex)
                {
                    Warn("The local key could not be stored: " + ex.Message);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Reads and validates the smart-plug fields. With <paramref name="requireDetails"/>
        /// the connection details must be present, which is what the Test button needs.
        /// </summary>
        private bool TryReadPlugInputs(bool requireDetails, out PlugInput input, out string error)
        {
            input = new PlugInput();
            error = string.Empty;

            input.DeviceId = PlugDeviceIdBox.Text.Trim();
            input.Host = PlugHostBox.Text.Trim();

            if (!int.TryParse(PlugPortBox.Text, out int port) || port < 1 || port > 65535)
            {
                error = "Smart plug port must be a number between 1 and 65535.";
                return false;
            }
            input.Port = port;

            if (!int.TryParse(PlugRelayDpBox.Text, out int relayDp) || relayDp < 1 || relayDp > 255)
            {
                error = "Relay data point must be a number between 1 and 255.";
                return false;
            }
            input.RelayDp = relayDp;

            if (!int.TryParse(PlugHighBox.Text, out int plugHigh) || plugHigh < 1 || plugHigh > 100)
            {
                error = "The plug switch-off level must be between 1 and 100 percent.";
                return false;
            }
            input.High = plugHigh;

            if (!int.TryParse(PlugLowBox.Text, out int plugLow) || plugLow < 0 || plugLow > 99)
            {
                error = "The plug switch-on level must be between 0 and 99 percent.";
                return false;
            }
            input.Low = plugLow;

            if (plugLow >= plugHigh)
            {
                error = "The plug switch-on level must be below the switch-off level.";
                return false;
            }

            if (plugHigh - plugLow < SmartPlugConfig.MinimumGapPercent)
            {
                error = "Keep the two plug levels at least " + SmartPlugConfig.MinimumGapPercent +
                        " points apart, otherwise the relay clicks on and off as the battery moves one percent at a time.";
                return false;
            }

            var store = new TuyaCredentialStore();
            string keyText = PlugKeyBox.Password.Trim();

            if (keyText.Length > 0)
            {
                try
                {
                    input.Key = TuyaLocalKey.Parse(keyText);
                }
                catch (ArgumentException ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
            else if (requireDetails && !store.HasKey())
            {
                error = "Enter the local key shown in the Smart Life app under the plug's device information.";
                return false;
            }

            if (requireDetails)
            {
                if (input.DeviceId.Length == 0)
                {
                    error = "Enter the smart plug device ID.";
                    return false;
                }

                if (input.Host.Length == 0)
                {
                    error = "Enter the smart plug IP address.";
                    return false;
                }

                if (input.Key == null)
                {
                    string? stored = store.Load();
                    if (stored == null)
                    {
                        error = "Enter the smart plug local key.";
                        return false;
                    }

                    try
                    {
                        input.Key = TuyaLocalKey.Parse(stored);
                    }
                    catch (ArgumentException ex)
                    {
                        error = "The stored local key is not valid: " + ex.Message;
                        return false;
                    }
                }
            }

            return true;
        }

        private async void TestPlugButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadPlugInputs(requireDetails: true, out PlugInput plug, out string error))
            {
                PlugStatusText.Text = error;
                return;
            }

            TestPlugButton.IsEnabled = false;
            PlugStatusText.Text = "Connecting to " + plug.Host + "...";

            try
            {
                using var client = new TuyaSmartPlug(plug.DeviceId, plug.Host, plug.Port, plug.Key!, plug.RelayDp);
                bool answered = await client.TestConnectionAsync().ConfigureAwait(true);
                PlugStatusText.Text = answered
                    ? "Connected. The plug answered and reports its state."
                    : "No answer from the plug. Check the IP, the key and that the phone app is closed.";
            }
            catch (Exception ex)
            {
                PlugStatusText.Text = "Failed: " + ex.Message;
            }
            finally
            {
                TestPlugButton.IsEnabled = true;
            }
        }

        private static void Warn(string message)
        {
            System.Windows.MessageBox.Show(message, "Invalid Input",
                System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
