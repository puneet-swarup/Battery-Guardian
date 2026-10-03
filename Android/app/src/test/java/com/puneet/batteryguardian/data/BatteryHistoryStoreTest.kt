package com.puneet.batteryguardian.data

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.puneet.batteryguardian.core.BatteryHistoryEntry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.File
import java.time.Instant

/**
 * Robolectric tests for [BatteryHistoryStore]. These exercise the real file
 * round-trip in app-private storage on the JVM, so no device is needed.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class BatteryHistoryStoreTest {

    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        val file = File(context.filesDir, "history.json")
        if (file.exists()) file.delete()
    }

    private fun entry(percent: Int, offsetSeconds: Long = 0) =
        BatteryHistoryEntry(Instant.parse("2026-01-01T12:00:00Z").plusSeconds(offsetSeconds), percent, false)

    @Test
    fun `fresh store is empty`() {
        assertTrue(BatteryHistoryStore(context).getAll().isEmpty())
    }

    @Test
    fun `append then getAll returns the entry`() {
        val store = BatteryHistoryStore(context)
        store.append(entry(42))
        val all = store.getAll()
        assertEquals(1, all.size)
        assertEquals(42, all[0].percent)
    }

    @Test
    fun `entries persist across instances`() {
        BatteryHistoryStore(context).apply {
            append(entry(11))
            append(entry(22, 60))
        }
        val all = BatteryHistoryStore(context).getAll()
        assertEquals(2, all.size)
        assertEquals(11, all[0].percent)
        assertEquals(22, all[1].percent)
    }

    @Test
    fun `clear empties the store and persists`() {
        val store = BatteryHistoryStore(context)
        store.append(entry(50))
        store.clear()
        assertTrue(store.getAll().isEmpty())
        assertTrue(BatteryHistoryStore(context).getAll().isEmpty())
    }

    @Test
    fun `getAll returns a copy not the internal list`() {
        val store = BatteryHistoryStore(context)
        store.append(entry(10))
        val snapshot = store.getAll()
        store.append(entry(20, 60))
        assertEquals(1, snapshot.size)
    }

    @Test
    fun `corrupt file starts with an empty store`() {
        File(context.filesDir, "history.json").writeText("not valid json")
        assertTrue(BatteryHistoryStore(context).getAll().isEmpty())
    }

    @Test
    fun `rolling window caps the number of stored entries`() {
        val store = BatteryHistoryStore(context)
        repeat(BatteryHistoryStore.MAX_ENTRIES + 50) { i ->
            store.append(entry(i % 100, offsetSeconds = i.toLong()))
        }
        assertEquals(BatteryHistoryStore.MAX_ENTRIES, store.getAll().size)
    }

    @Test
    fun `round trip preserves on-ac flag and timestamp`() {
        val store = BatteryHistoryStore(context)
        val original = BatteryHistoryEntry(Instant.parse("2026-03-03T03:03:03Z"), 77, true)
        store.append(original)
        assertEquals(original, BatteryHistoryStore(context).getAll().single())
    }
}
