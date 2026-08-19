using System.Windows;
using System.Windows.Threading;

namespace BatteryGuardian
{
    public partial class AlertPopup : Window
    {
        private DispatcherTimer _closeTimer;
        public AlertPopup(string message)
        {
            InitializeComponent();
            MessageText.Text = message;

            // Position in bottom-right corner
            var desktopWorkingArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
            this.Left = desktopWorkingArea.Right - this.Width - 10;
            this.Top = desktopWorkingArea.Bottom - this.Height - 10;

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _closeTimer.Tick += (s, e) => { _closeTimer.Stop(); this.Close(); };
            _closeTimer.Start();
        }
    }
}