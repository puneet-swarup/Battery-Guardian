using System;
using Microsoft.Toolkit.Uwp.Notifications;

namespace BatteryGuardian
{
    /// <summary>
    /// Sends native Windows 10/11 toast notifications via the Action Center.
    /// Uses Microsoft.Toolkit.Uwp.Notifications because it handles AppUserModelID
    /// registration automatically for unpackaged desktop apps.
    /// </summary>
    public class ToastService
    {
        /// <summary>
        /// Shows a toast notification with the given title and body.
        /// Falls back silently if the OS rejects the toast (e.g., notifications disabled).
        /// </summary>
        public void Show(string title, string message)
        {
            try
            {
                new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message)
                    .Show();
            }
            catch
            {
                // Toast infrastructure is OS-level; if it fails, we don't crash the app.
                // The beep and speech will still alert the user.
            }
        }
    }
}