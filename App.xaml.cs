using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BatteryGuardian
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // When the user clicks a toast, bring the main window to the front.
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // The activation comes in on a background thread — marshal to UI thread.
                Current.Dispatcher.Invoke(() =>
                {
                    if (Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                });
            };
        }
    }
}