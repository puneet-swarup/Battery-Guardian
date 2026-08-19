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
                LowBatteryThreshold = currentSettings.LowBatteryThreshold
            };
            HighThresholdBox.Text = Settings.HighBatteryThreshold.ToString();
            LowThresholdBox.Text = Settings.LowBatteryThreshold.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HighThresholdBox.Text, out int high) &&
                int.TryParse(LowThresholdBox.Text, out int low))
            {
                if (low >= high)
                {
                    System.Windows.MessageBox.Show("Low threshold must be strictly lower than High threshold.",
                        "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Settings.HighBatteryThreshold = high;
                Settings.LowBatteryThreshold = low;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Please enter valid numeric values.",
                    "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}