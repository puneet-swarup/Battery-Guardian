package com.puneet.batteryguardian.data

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import java.io.File

/**
 * Robolectric tests for [DiagnosticLog]. These exercise the real file-backed
 * logging behaviour, including the enabled/disabled gate and clear().
 *
 * Robolectric gives each test method its own application context and filesDir,
 * but DiagnosticLog is a process-wide singleton. We therefore reset the cached
 * instance before every test so it binds to the current test's context.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class DiagnosticLogTest {

    private lateinit var context: Context
    private lateinit var log: DiagnosticLog

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        resetSingleton()
        log = DiagnosticLog.get(context)
        log.enabled = false
        log.clear()
    }

    /** Clears the private static `instance` field so get() rebuilds for this test. */
    private fun resetSingleton() {
        val field = DiagnosticLog::class.java.getDeclaredField("instance")
        field.isAccessible = true
        field.set(null, null)
    }

    private fun logFile(): File = File(context.filesDir, "diagnostic.log")

    @Test
    fun `disabled logger writes nothing`() {
        log.enabled = false
        log.write("should not appear")
        assertFalse(logFile().exists())
    }

    @Test
    fun `enabled logger appends the message`() {
        log.enabled = true
        log.write("hello diagnostics")
        assertTrue(logFile().exists())
        assertTrue(logFile().readText().contains("hello diagnostics"))
    }

    @Test
    fun `multiple writes accumulate`() {
        log.enabled = true
        log.write("first")
        log.write("second")
        val text = logFile().readText()
        assertTrue(text.contains("first"))
        assertTrue(text.contains("second"))
    }

    @Test
    fun `writeException records class and message`() {
        log.enabled = true
        log.writeException("unitTest", IllegalStateException("boom"))
        val text = logFile().readText()
        assertTrue(text.contains("EXCEPTION in unitTest"))
        assertTrue(text.contains("IllegalStateException"))
        assertTrue(text.contains("boom"))
    }

    @Test
    fun `clear deletes the log file`() {
        log.enabled = true
        log.write("to be cleared")
        assertTrue(logFile().exists())
        log.clear()
        assertFalse(logFile().exists())
    }

    @Test
    fun `clear on a missing file does not throw`() {
        logFile().delete()
        log.clear()
        assertFalse(logFile().exists())
    }

    @Test
    fun `logPath points at the diagnostic file`() {
        assertEquals(logFile().absolutePath, log.logPath())
    }

    @Test
    fun `get returns a singleton per process`() {
        val a = DiagnosticLog.get(context)
        val b = DiagnosticLog.get(context)
        assertTrue(a === b)
    }
}
