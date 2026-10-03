using System;
using System.IO;
using System.Linq;
using BatteryGuardian.History;
using Xunit;

namespace BatteryGuardian.Tests
{
    /// <summary>
    /// Tests for the JSON-file history store. Each test uses its own temp file so
    /// runs are isolated and no test touches the real user profile.
    /// </summary>
    public class JsonFileBatteryHistoryStoreTests : IDisposable
    {
        private readonly string _path;

        public JsonFileBatteryHistoryStoreTests()
        {
            _path = Path.Combine(Path.GetTempPath(), $"bg_history_{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }

        private static BatteryHistoryEntry Entry(int percent) =>
            new(DateTime.UtcNow, percent, false);

        [Fact]
        public void Constructor_NullOrWhitespacePath_Throws()
        {
            Assert.Throws<ArgumentException>(() => new JsonFileBatteryHistoryStore(""));
            Assert.Throws<ArgumentException>(() => new JsonFileBatteryHistoryStore("   "));
        }

        [Fact]
        public void GetAll_OnFreshStore_IsEmpty()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void Append_ThenGetAll_ReturnsTheEntry()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            store.Append(Entry(42));

            var all = store.GetAll();

            Assert.Single(all);
            Assert.Equal(42, all[0].Percent);
        }

        [Fact]
        public void Append_Null_IsIgnored()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            store.Append(null!);
            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void Entries_PersistAcrossInstances()
        {
            var first = new JsonFileBatteryHistoryStore(_path);
            first.Append(Entry(11));
            first.Append(Entry(22));

            var second = new JsonFileBatteryHistoryStore(_path);
            var all = second.GetAll();

            Assert.Equal(2, all.Count);
            Assert.Equal(new[] { 11, 22 }, all.Select(e => e.Percent).ToArray());
        }

        [Fact]
        public void Append_RespectsRollingWindowCap()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            for (int i = 0; i < JsonFileBatteryHistoryStore.MaxEntries + 50; i++)
            {
                store.Append(Entry(i % 100));
            }

            Assert.Equal(JsonFileBatteryHistoryStore.MaxEntries, store.GetAll().Count);
        }

        [Fact]
        public void RollingWindow_KeepsMostRecentEntries()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            for (int i = 0; i < JsonFileBatteryHistoryStore.MaxEntries + 1; i++)
            {
                store.Append(Entry(i % 100));
            }

            var all = store.GetAll();
            // The oldest entry (the very first) should have been dropped.
            Assert.Equal(JsonFileBatteryHistoryStore.MaxEntries, all.Count);
        }

        [Fact]
        public void Clear_EmptiesTheStore()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            store.Append(Entry(50));
            store.Clear();

            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void Clear_PersistsAcrossInstances()
        {
            var first = new JsonFileBatteryHistoryStore(_path);
            first.Append(Entry(50));
            first.Clear();

            var second = new JsonFileBatteryHistoryStore(_path);
            Assert.Empty(second.GetAll());
        }

        [Fact]
        public void Load_CorruptFile_StartsWithEmptyStore()
        {
            File.WriteAllText(_path, "this is not json");

            var store = new JsonFileBatteryHistoryStore(_path);

            Assert.Empty(store.GetAll());
        }

        [Fact]
        public void GetAll_ReturnsCopy_NotInternalList()
        {
            var store = new JsonFileBatteryHistoryStore(_path);
            store.Append(Entry(10));

            var snapshot = store.GetAll();
            store.Append(Entry(20));

            Assert.Single(snapshot);
        }
    }
}
