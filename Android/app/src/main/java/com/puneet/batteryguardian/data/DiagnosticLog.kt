package com.puneet.batteryguardian.data

import android.content.Context
import android.util.Log
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Lightweight optional diagnostic logger, mirroring DiagnosticLog.cs.
 * Writes to a file in app-private storage when enabled; otherwise no-ops.
 * Never throws - logging must not crash the app.
 */
class DiagnosticLog private constructor(private val context: Context) {

    @Volatile
    var enabled: Boolean = false

    private val lock = Any()
    private val timeFormat = SimpleDateFormat("HH:mm:ss.SSS", Locale.US)

    private val logFile: File
        get() = File(context.filesDir, "diagnostic.log")

    fun write(message: String) {
        if (!enabled) return
        try {
            synchronized(lock) {
                logFile.appendText("[${timeFormat.format(Date())}] $message\n")
            }
        } catch (t: Throwable) {
            Log.w(TAG, "Diagnostic write failed", t)
        }
    }

    fun writeException(contextTag: String, throwable: Throwable) {
        write("EXCEPTION in $contextTag: ${throwable.javaClass.simpleName}: ${throwable.message}")
    }

    fun clear() {
        try {
            synchronized(lock) {
                if (logFile.exists()) logFile.delete()
            }
        } catch (t: Throwable) {
            Log.w(TAG, "Diagnostic clear failed", t)
        }
    }

    fun logPath(): String = logFile.absolutePath

    companion object {
        private const val TAG = "BatteryGuardian"

        @Volatile
        private var instance: DiagnosticLog? = null

        fun get(context: Context): DiagnosticLog =
            instance ?: synchronized(this) {
                instance ?: DiagnosticLog(context.applicationContext).also { instance = it }
            }
    }
}
