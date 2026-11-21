using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BatteryGuardian.History
{
    /// <summary>
    /// Persists battery history to a JSON file on disk. Keeps the last
    /// <see cref="MaxEntries"/> readings (rolling window) so the file never grows
    /// without bound. All operations are best-effort: I/O failures never throw
    /// into the caller, matching the app's overall fail-soft philosophy.
    /// </summary>
    public sealed class JsonFileBatteryHistoryStore : IBatteryHistoryStore
    {
        public const int MaxEntries = 5000;

        /// <summary>
        /// Writes are coalesced: an append only touches the disk if the previous
        /// write is older than this. Readings arrive every few minutes in the app,
        /// so every append still writes in normal use. The window exists so a burst
        /// does not serialise the whole list once per entry, which is O(n^2) and
        /// turned a 5000-entry cap into minutes of I/O.
        /// </summary>
        private const int WriteCoalesceMs = 2000;

        private readonly string _path;
        private readonly object _lock = new();
        private readonly List<BatteryHistoryEntry> _cache = new();

        private DateTime _lastWriteUtc = DateTime.MinValue;
        private bool _dirty;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false
        };

        public JsonFileBatteryHistoryStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be empty.", nameof(path));

            _path = path;
            Load();
        }

        /// <summary>Convenience factory using the app's standard data folder.</summary>
        public static JsonFileBatteryHistoryStore CreateDefault()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BatteryGuardian");
            Directory.CreateDirectory(dir);
            return new JsonFileBatteryHistoryStore(Path.Combine(dir, "history.json"));
        }

        public void Append(BatteryHistoryEntry entry)
        {
            if (entry == null) return;

            lock (_lock)
            {
                _cache.Add(entry);

                if (_cache.Count > MaxEntries)
                {
                    _cache.RemoveRange(0, _cache.Count - MaxEntries);
                }

                _dirty = true;
                SaveUnsafe(force: false);
            }
        }

        /// <inheritdoc />
        public void Flush()
        {
            lock (_lock)
            {
                SaveUnsafe(force: true);
            }
        }

        public IReadOnlyList<BatteryHistoryEntry> GetAll()
        {
            lock (_lock)
            {
                return new List<BatteryHistoryEntry>(_cache);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _dirty = true;
                SaveUnsafe(force: true);
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var loaded = JsonSerializer.Deserialize<List<BatteryHistoryEntry>>(json, JsonOptions);
                if (loaded != null) _cache.AddRange(loaded);
            }
            catch
            {
                // Corrupt or unreadable file — start fresh rather than crash.
                _cache.Clear();
            }
        }

        private void SaveUnsafe(bool force)
        {
            if (!_dirty) return;

            if (!force && (DateTime.UtcNow - _lastWriteUtc).TotalMilliseconds < WriteCoalesceMs)
            {
                return; // inside the coalescing window; the next append or Flush writes
            }

            try
            {
                var json = JsonSerializer.Serialize(_cache, JsonOptions);
                File.WriteAllText(_path, json);
                _dirty = false;
                _lastWriteUtc = DateTime.UtcNow;
            }
            catch
            {
                // Best-effort persistence; failure to write must not break monitoring.
            }
        }
    }
}
