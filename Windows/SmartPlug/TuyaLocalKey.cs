using System;
using System.Text;

namespace BatteryGuardian.SmartPlug
{
    /// <summary>
    /// Turns the local key the user pastes in Settings into the 16 bytes the
    /// protocol needs. Accepts the 16-character key from the Smart Life app, or
    /// the same key written as 32 hex digits.
    /// </summary>
    /// <remarks>
    /// A 32-character ASCII string is deliberately not accepted: an AES-128 key
    /// is 16 bytes, and the old code quietly produced a 32-byte key that could
    /// never work.
    /// </remarks>
    public static class TuyaLocalKey
    {
        public static byte[] Parse(string localKey)
        {
            if (string.IsNullOrWhiteSpace(localKey))
                throw new ArgumentException("Local key must not be empty.", nameof(localKey));

            string key = localKey.Trim();

            if (key.Length == 32 && IsHex(key))
                return Convert.FromHexString(key);

            if (key.Length == 16)
                return Encoding.ASCII.GetBytes(key);

            throw new ArgumentException(
                "Invalid local key: expected 16 characters or 32 hex digits, got " + key.Length + ".",
                nameof(localKey));
        }

        private static bool IsHex(string value)
        {
            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }
    }
}
