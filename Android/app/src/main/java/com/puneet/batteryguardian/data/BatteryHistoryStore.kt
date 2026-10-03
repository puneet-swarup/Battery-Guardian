package com.puneet.batteryguardian.data

import android.content.Context
import com.puneet.batteryguardian.core.BatteryHistoryEntry
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.time.Instant

/**
 * Persists battery history to a JSON file in app-private storage, keeping a
 * rolling window of the most recent readings so the file never grows unbounded.
 *
 * All operations are best-effort: I/O failures never propagate to callers,
 * matching the app's overall fail-soft philosophy.
 *
 * Port of JsonFileBatteryHistoryStore.cs from the Windows application.
 */
class BatteryHistoryStore(context: Context) {

    private val appContext = context.applicationContext
    private val lock = Any()
    private val cache = mutableListOf<BatteryHistoryEntry>()

    private val file: File
        get() = File(appContext.filesDir, "history.json")

    init {
        load()
    }

    fun append(entry: BatteryHistoryEntry) {
        synchronized(lock) {
            cache.add(entry)
            if (cache.size > MAX_ENTRIES) {
                cache.subList(0, cache.size - MAX_ENTRIES).clear()
            }
            saveUnsafe()
        }
    }

    fun getAll(): List<BatteryHistoryEntry> = synchronized(lock) { cache.toList() }

    fun clear() {
        synchronized(lock) {
            cache.clear()
            saveUnsafe()
        }
    }

    private fun load() {
        try {
            val f = file
            if (!f.exists()) return
            val text = f.readText()
            if (text.isBlank()) return

            val array = JSONArray(text)
            for (i in 0 until array.length()) {
                val obj = array.getJSONObject(i)
                cache.add(
                    BatteryHistoryEntry(
                        utc = Instant.ofEpochMilli(obj.getLong(KEY_UTC)),
                        percent = obj.getInt(KEY_PERCENT),
                        isOnAcPower = obj.getBoolean(KEY_AC)
                    )
                )
            }
        } catch (t: Throwable) {
            // Corrupt or unreadable file - start fresh rather than crash.
            cache.clear()
        }
    }

    private fun saveUnsafe() {
        try {
            val array = JSONArray()
            for (entry in cache) {
                val obj = JSONObject()
                obj.put(KEY_UTC, entry.utc.toEpochMilli())
                obj.put(KEY_PERCENT, entry.percent)
                obj.put(KEY_AC, entry.isOnAcPower)
                array.put(obj)
            }
            file.writeText(array.toString())
        } catch (t: Throwable) {
            // Best-effort persistence; failure must not break monitoring.
        }
    }

    companion object {
        const val MAX_ENTRIES = 5000
        private const val KEY_UTC = "utc"
        private const val KEY_PERCENT = "percent"
        private const val KEY_AC = "ac"
    }
}
