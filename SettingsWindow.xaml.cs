using System;
using System.Windows;

namespace BatteryGuardian
{
    public partial class SettingsWindow : Window
    {
        public Settings Settings { get; private set; }

        public SettingsWindow(Settings currentSettings)
        {
            InitializeComponent();
            Settings = new Settings
            {
                HighBatteryThreshold = currentSettings.HighBatteryThreshold,
                LowBatteryThreshold = currentSettings.LowBatteryThreshold,
                AlertRepeatIntervalSeconds = currentSettings.AlertRepeatIntervalSeconds,
                CheckForUpdatesAutomatically = currentSettings.CheckForUpdatesAutomatically,
                LastUpdateCheckUtc = currentSettings.LastUpdateCheckUtc
            };

            HighThresholdBox.Text = Settings.HighBatteryThreshold.ToString();
            LowThresholdBox.Text = Settings.LowBatteryThreshold.ToString();
            RepeatIntervalBox.Text = Settings.AlertRepeatIntervalSeconds.ToString();
            CheckForUpdatesCheckBox.IsChecked = Settings.CheckForUpdatesAutomatically;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HighThresholdBox.Text, out int high) &&
                int.TryParse(LowThresholdBox.Text, out int low) &&
                int.TryParse(RepeatIntervalBox.Text, out int repeatSec) && repeatSec > 0)
            {
                if (low >= high)
                {
                    System.Windows.MessageBox.Show("Low threshold must be strictly lower than High threshold.",
                        "Invalid Input", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Settings.HighBatteryThreshold = high;
                Settings.LowBatteryThreshold = low;
                Settings.AlertRepeatIntervalSeconds = repeatSec;
                Settings.CheckForUpdatesAutomatically = CheckForUpdatesCheckBox.IsChecked == true;

                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Please enter valid numeric values.",
                    "Invalid Input", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}