using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace BatteryGuardian
{
    public partial class AboutWindow : Window
    {
        private const string GitHubUrl = "https://github.com/puneet-swarup/Battery-Guardian";
        private const string ReleasesUrl = "https://github.com/puneet-swarup/Battery-Guardian/releases/latest";

        public AboutWindow()
        {
            InitializeComponent();

            VersionText.Text = $"Version {GetVersionString()}";
            AuthorText.Text = "Made by Puneet Swarup";
            CopyrightText.Text = $"© {DateTime.Now.Year} Puneet Swarup";
            LicenseText.Text = "Licensed under the MIT License";
        }

        private static string GetVersionString()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                // MinVer sets InformationalVersion to the full SemVer string.
                var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(info))
                {
                    // Strip the "+<commit-hash>" suffix that MinVer adds for local builds.
                    int plus = info.IndexOf('+');
                    return plus >= 0 ? info.Substring(0, plus) : info;
                }

                return assembly.GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
            => OpenUrl(GitHubUrl);

        private void ReleasesButton_Click(object sender, RoutedEventArgs e)
            => OpenUrl(ReleasesUrl);

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}