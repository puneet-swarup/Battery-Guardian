using Microsoft.Win32;

namespace BatteryGuardian.QuietHours
{
    /// <summary>
    /// Reads the Windows Focus Assist (Do Not Disturb) state from the registry.
    ///
    /// Windows exposes Focus Assist state under
    /// HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings.
    /// The <c>NOC_GLOBAL_SETTING_TOASTS_ENABLED</c> value is 1 when toast
    /// notifications are allowed and 0 when Focus Assist is suppressing them.
    ///
    /// All reads are best-effort: any failure returns false so alerts are never
    /// silently swallowed by a registry quirk.
    /// </summary>
    public sealed class WindowsDoNotDisturbDetector : IDoNotDisturbDetector
    {
        private const string SettingsKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings";

        private const string ToastsEnabledValue = "NOC_GLOBAL_SETTING_TOASTS_ENABLED";

        public bool IsDoNotDisturbActive()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
                if (key == null) return false;

                var value = key.GetValue(ToastsEnabledValue);
                if (value is int i)
                {
                    // 0 means toasts are disabled -> Focus Assist is on.
                    return i == 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
