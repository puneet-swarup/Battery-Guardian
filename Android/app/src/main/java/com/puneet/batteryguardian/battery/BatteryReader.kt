package com.puneet.batteryguardian.battery

import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager
import com.puneet.batteryguardian.data.DiagnosticLog

/**
 * Reads the current battery state from the sticky ACTION_BATTERY_CHANGED intent
 * and, on a best-effort basis, battery health from the kernel sysfs nodes.
 *
 * The Android analogue of BatteryHealthService.cs + the GetSystemPowerStatus
 * P/Invoke used by the Windows app.
 */
class BatteryReader(private val context: Context) {

    private val log = DiagnosticLog.get(context)

    /**
     * Returns the latest battery snapshot, or [BatterySnapshot.UNKNOWN] if the
     * system does not report battery information (e.g. some emulators).
     */
    fun read(): BatterySnapshot {
        val intent = try {
            context.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        } catch (t: Throwable) {
            log.writeException("BatteryReader.read", t)
            null
        } ?: return BatterySnapshot.UNKNOWN

        return parseIntent(intent, readHealth())
    }

    /**
     * Converts an ACTION_BATTERY_CHANGED intent into a [BatterySnapshot]. Kept
     * separate from [read] (and internal) so it can be unit tested by feeding a
     * constructed intent, without a live battery broadcast.
     */
    internal fun parseIntent(intent: Intent, health: BatteryHealthReading?): BatterySnapshot {
        val level = intent.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
        val scale = intent.getIntExtra(BatteryManager.EXTRA_SCALE, -1)
        val percent = if (level >= 0 && scale > 0) (level * 100) / scale else -1

        val statusInt = intent.getIntExtra(BatteryManager.EXTRA_STATUS, -1)
        val status = mapStatus(statusInt)

        val plugged = intent.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0)
        val isOnAc = plugged != 0
        val isCharging = statusInt == BatteryManager.BATTERY_STATUS_CHARGING
        val isFull = statusInt == BatteryManager.BATTERY_STATUS_FULL

        return BatterySnapshot(
            percent = percent,
            isOnAcPower = isOnAc,
            isCharging = isCharging,
            isFull = isFull,
            status = status,
            health = health
        )
    }

    private fun mapStatus(status: Int): BatteryStatus = when (status) {
        BatteryManager.BATTERY_STATUS_CHARGING -> BatteryStatus.CHARGING
        BatteryManager.BATTERY_STATUS_DISCHARGING -> BatteryStatus.DISCHARGING
        BatteryManager.BATTERY_STATUS_FULL -> BatteryStatus.FULL
        BatteryManager.BATTERY_STATUS_NOT_CHARGING -> BatteryStatus.NOT_CHARGING
        else -> BatteryStatus.UNKNOWN
    }

    /**
     * Best-effort read of design and full-charge capacity from sysfs. Many
     * devices restrict these nodes; when unavailable we return nulls and the UI
     * hides the health section, exactly like the Windows app on desktops.
     */
    private fun readHealth(): BatteryHealthReading? {
        val design = readSysfsLong(DESIGN_PATHS)
        val full = readSysfsLong(FULL_PATHS)
        if (design == null && full == null) return null
        return BatteryHealthReading(designCapacity = design, fullChargeCapacity = full)
    }

    private fun readSysfsLong(paths: List<String>): Long? {
        for (path in paths) {
            try {
                val file = java.io.File(path)
                if (!file.canRead()) continue
                val raw = file.readText().trim()
                val value = raw.toLongOrNull() ?: continue
                if (value > 0) return value
            } catch (t: Throwable) {
                log.write("sysfs read failed for $path: ${t.message}")
            }
        }
        return null
    }

    companion object {
        // Common sysfs locations for battery capacity values. Units are usually
        // microamp-hours (uAh) but the ratio design vs full is what matters.
        private val DESIGN_PATHS = listOf(
            "/sys/class/power_supply/battery/charge_full_design",
            "/sys/class/power_supply/BAT0/charge_full_design",
            "/sys/class/power_supply/bms/charge_full_design"
        )
        private val FULL_PATHS = listOf(
            "/sys/class/power_supply/battery/charge_full",
            "/sys/class/power_supply/BAT0/charge_full",
            "/sys/class/power_supply/bms/charge_full"
        )
    }
}
