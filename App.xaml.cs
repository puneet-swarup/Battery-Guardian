using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BatteryGuardian
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch every unhandled exception and log it to a file.
            AppDomain.CurrentDomain.UnhandledException += (s, args) => LogCrash("AppDomain", args.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, args) => { LogCrash("Dispatcher", args.Exception); args.Handled = true; };

            try
            {
                base.OnStartup(e);

                ToastNotificationManagerCompat.OnActivated += toastArgs =>
                {
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
            catch (Exception ex)
            {
                LogCrash("OnStartup", ex);
                throw;
            }
        }

        private static void LogCrash(string source, Exception? ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BatteryGuardian",
                    "crash.log");

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}" +
                    $"{ex?.ToString() ?? "(null exception)"}{Environment.NewLine}" +
                    new string('-', 80) + Environment.NewLine);
            }
            catch { /* nothing we can do */ }
        }
    }
}