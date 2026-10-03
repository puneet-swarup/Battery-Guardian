using System.Collections.Generic;

namespace BatteryGuardian.History
{
    /// <summary>
    /// Abstraction for persisting and retrieving battery history.
    /// Depending on this interface (not a concrete file class) keeps callers
    /// testable and honours the Dependency Inversion Principle.
    /// </summary>
    public interface IBatteryHistoryStore
    {
        /// <summary>Append a single reading.</summary>
        void Append(BatteryHistoryEntry entry);

        /// <summary>Return all stored readings, oldest first.</summary>
        IReadOnlyList<BatteryHistoryEntry> GetAll();

        /// <summary>Remove all stored readings.</summary>
        void Clear();
    }
}
