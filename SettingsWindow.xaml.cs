using System;
using System.Windows;
using BatteryGuardian.QuietHours;

namespace BatteryGuardian
{
    public partial class SettingsWindow : Window
    {
        public Settings Settings { get; private set; }

        public SettingsWindow(Settings currentSettings)
        {
            InitializeComponent();

            // Preserve every persisted field, including the ones this window does
            // not edit, so saving here never clobbers unrelated settings.
            Settings = new Settings
            {
                HighBatteryThreshold = currentSettings.HighBatteryThreshold,
                LowBatteryThreshold = currentSettings.LowBatteryThreshold,
                AlertRepeatIntervalSeconds = currentSettings.AlertRepeatIntervalSeconds,
                CheckForUpdatesAutomatically = currentSettings.CheckForUpdatesAutomatically,
                LastUpdateCheckUtc = currentSettings.LastUpdateCheckUtc,
                DiagnosticLoggingEnabled = currentSettings.DiagnosticLoggingEnabled,
                SnoozedUntilUtc = currentSettings.SnoozedUntilUtc,
                HistoryEnabled = currentSettings.HistoryEnabled,
                IdealZoneLowPercent = currentSettings.IdealZoneLowPercent,
                IdealZoneHighPercent = currentSettings.IdealZoneHighPercent,
                QuietHoursEnabled = currentSettings.QuietHoursEnabled,
                QuietHoursStartMinutes = currentSettings.QuietHoursStartMinutes,
                QuietHoursEndMinutes = currentSettings.QuietHoursEndMinutes,
                RespectFocusAssist = currentSettings.RespectFocusAssist,
                LastAppliedChargeLimitPercent = currentSettings.LastAppliedChargeLimitPercent
            };

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

            DialogResult = true;
            Close();
        }

        private static void Warn(string message)
        {
            System.Windows.MessageBox.Show(message, "Invalid Input",
                System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
