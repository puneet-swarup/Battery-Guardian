package com.puneet.batteryguardian.notify

import android.Manifest
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.media.AudioManager
import android.media.ToneGenerator
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.speech.tts.TextToSpeech
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.Settings
import com.puneet.batteryguardian.ui.MainActivity
import java.util.Locale

/**
 * Delivers an alert through every channel the user has enabled: notification,
 * sound, speech and vibration. Mirrors the combined ToastService + beep + speech
 * behaviour of the Windows application.
 */
class AlertNotifier(private val context: Context) : AlertSink {

    private val log = DiagnosticLog.get(context)

    @Volatile
    private var tts: TextToSpeech? = null

    @Volatile
    private var ttsReady = false

    init {
        try {
            tts = TextToSpeech(context.applicationContext) { status ->
                ttsReady = status == TextToSpeech.SUCCESS
                if (ttsReady) {
                    tts?.language = Locale.US
                }
            }
        } catch (t: Throwable) {
            log.writeException("AlertNotifier.init", t)
        }
    }

    /**
     * Raises an alert using the supplied settings. Never throws - alerting
     * failures must not take down the monitoring service.
     */
    override fun notify(message: String, settings: Settings) {
        if (message.isBlank()) return

        if (settings.showNotification) postNotification(message)
        if (settings.playSound) playBeep()
        if (settings.vibrate) vibrate()
        if (settings.speakAlert) speak(message)
    }

    override fun release() {
        try {
            tts?.stop()
            tts?.shutdown()
        } catch (_: Throwable) {
        } finally {
            tts = null
            ttsReady = false
        }
    }

    private fun postNotification(message: String) {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                log.write("Notification permission not granted; skipping notification")
                return
            }

            val tapIntent = Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            }
            val contentIntent = PendingIntent.getActivity(
                context,
                REQ_CONTENT,
                tapIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )

            val notification = NotificationCompat.Builder(context, NotificationChannels.ALERTS)
                .setSmallIcon(R.drawable.ic_battery_alert)
                .setContentTitle(context.getString(R.string.notification_title))
                .setContentText(message)
                .setStyle(NotificationCompat.BigTextStyle().bigText(message))
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setCategory(NotificationCompat.CATEGORY_ALARM)
                .setAutoCancel(true)
                .setContentIntent(contentIntent)
                .build()

            val manager = context.getSystemService(NotificationManager::class.java)
            manager?.notify(NOTIF_ID_ALERT, notification)
        } catch (t: Throwable) {
            log.writeException("AlertNotifier.postNotification", t)
        }
    }

    private fun playBeep() {
        var tone: ToneGenerator? = null
        try {
            tone = ToneGenerator(AudioManager.STREAM_NOTIFICATION, VOLUME)
            tone.startTone(ToneGenerator.TONE_PROP_BEEP, BEEP_MS)
        } catch (t: Throwable) {
            log.write("Beep failed: ${t.message}")
        } finally {
            // ToneGenerator releases asynchronously; schedule a delayed release.
            tone?.let { generator ->
                android.os.Handler(android.os.Looper.getMainLooper()).postDelayed({
                    try {
                        generator.release()
                    } catch (_: Throwable) {
                    }
                }, BEEP_MS + 200L)
            }
        }
    }

    private fun vibrate() {
        try {
            val vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                val manager = context.getSystemService(VibratorManager::class.java)
                manager?.defaultVibrator
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Vibrator::class.java)
            } ?: return

            val pattern = longArrayOf(0, 250, 150, 250)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createWaveform(pattern, -1))
            } else {
                @Suppress("DEPRECATION")
                vibrator.vibrate(pattern, -1)
            }
        } catch (t: Throwable) {
            log.write("Vibrate failed: ${t.message}")
        }
    }

    private fun speak(message: String) {
        try {
            if (!ttsReady) {
                log.write("TTS not ready; skipping speech")
                return
            }
            tts?.speak(message, TextToSpeech.QUEUE_FLUSH, null, UTTERANCE_ID)
        } catch (t: Throwable) {
            log.write("Speak failed: ${t.message}")
        }
    }

    companion object {
        private const val NOTIF_ID_ALERT = 1001
        private const val REQ_CONTENT = 2001
        private const val VOLUME = 90
        private const val BEEP_MS = 400
        private const val UTTERANCE_ID = "bg-alert"
    }
}
