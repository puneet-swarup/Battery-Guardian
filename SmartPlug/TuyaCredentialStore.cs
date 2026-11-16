using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BatteryGuardian.SmartPlug
{
 /// <summary>
 /// Persists the Tuya local key encrypted with Windows DPAPI
 /// (CurrentUser scope). Stored separately from Settings.json and never
 /// written in plaintext.
 /// </summary>
 public sealed class TuyaCredentialStore
 {
 private readonly string _path;

 public TuyaCredentialStore(string? path = null)
 {
 _path = path ?? DefaultPath();
 }

 public static string DefaultPath()
 {
 string folder = Path.Combine(
 Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
 "BatteryGuardian");
 Directory.CreateDirectory(folder);
 return Path.Combine(folder, "smartplug.key.bin");
 }

 public bool HasKey() => File.Exists(_path) && new FileInfo(_path).Length > 0;

 public void Save(string localKey)
 {
 if (string.IsNullOrWhiteSpace(localKey))
 throw new ArgumentException("Local key must not be empty.");
 byte[] plain = Encoding.UTF8.GetBytes(localKey.Trim());
 byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
 File.WriteAllBytes(_path, encrypted);
 Array.Clear(plain);
 }

 public string? Load()
 {
 if (!HasKey()) return null;
 try
 {
 byte[] encrypted = File.ReadAllBytes(_path);
 byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
 string key = Encoding.UTF8.GetString(plain);
 Array.Clear(plain);
 return key;
 }
 catch
 {
 return null;
 }
 }

 public void Clear()
 {
 if (File.Exists(_path)) File.Delete(_path);
 }
 }
}
