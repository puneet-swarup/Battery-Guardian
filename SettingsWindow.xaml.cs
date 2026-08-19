using System;
using System.Windows;
using System.Windows.Forms;

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
                HighAlertSoundPath = currentSettings.HighAlertSoundPath ?? "",
                LowAlertSoundPath = currentSettings.LowAlertSoundPath ?? ""
            };

            HighThresholdBox.Text = Settings.HighBatteryThreshold.ToString();
            LowThresholdBox.Text = Settings.LowBatteryThreshold.ToString();
            HighSoundBox.Text = string.IsNullOrEmpty(Settings.HighAlertSoundPath) ? "<Default>" : Settings.HighAlertSoundPath;
            LowSoundBox.Text = string.IsNullOrEmpty(Settings.LowAlertSoundPath) ? "<Default>" : Settings.LowAlertSoundPath;
        }

        private void HighSoundBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Sound Files (*.wav)|*.wav|All Files (*.*)|*.*";
            dialog.Title = "Select High Alert Sound";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                HighSoundBox.Text = dialog.FileName;
        }

        private void LowSoundBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Sound Files (*.wav)|*.wav|All Files (*.*)|*.*";
            dialog.Title = "Select Low Alert Sound";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LowSoundBox.Text = dialog.FileName;
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
                Settings.HighAlertSoundPath = (HighSoundBox.Text == "<Default>") ? "" : HighSoundBox.Text;
                Settings.LowAlertSoundPath = (LowSoundBox.Text == "<Default>") ? "" : LowSoundBox.Text;

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