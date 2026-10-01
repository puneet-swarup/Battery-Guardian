package com.puneet.batteryguardian.update

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.notify.NotificationChannels
import com.puneet.batteryguardian.ui.MainActivity
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import java.time.Instant

/**
 * Runs a background update check when triggered (e.g. by WorkManager or an
 * alarm) and posts a notification if a newer release is available.
 */
class UpdateCheckReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        val repo = SettingsRepository(context)
        val settings = repo.load()
        if (!settings.checkForUpdatesAutomatically) return

        val appContext = context.applicationContext
        val pending = goAsync()

        CoroutineScope(Dispatchers.IO).launch {
            try {
                val info = UpdateChecker(appContext).checkForUpdate()
                repo.setLastUpdateCheck(Instant.now())
                if (info != null) {
                    notifyUpdate(appContext, info)
                }
            } catch (t: Throwable) {
                DiagnosticLog.get(appContext).writeException("UpdateCheckReceiver", t)
            } finally {
                pending.finish()
            }
        }
    }

    private fun notifyUpdate(context: Context, info: UpdateInfo) {
        NotificationChannels.ensureCreated(context)

        val intent = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val contentIntent = PendingIntent.getActivity(
            context,
            REQ_UPDATE,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, NotificationChannels.ALERTS)
            .setSmallIcon(R.drawable.ic_update)
            .setContentTitle(context.getString(R.string.update_available_title))
            .setContentText(context.getString(R.string.update_available_fmt, info.latestVersion))
            .setStyle(NotificationCompat.BigTextStyle().bigText(info.releaseNotes.ifBlank {
                context.getString(R.string.update_available_fmt, info.latestVersion)
            }))
            .setAutoCancel(true)
            .setContentIntent(contentIntent)
            .build()

        context.getSystemService(NotificationManager::class.java)
            ?.notify(NOTIF_ID_UPDATE, notification)
    }

    companion object {
        const val ACTION_CHECK_UPDATE = "com.puneet.batteryguardian.action.CHECK_UPDATE"
        private const val NOTIF_ID_UPDATE = 1002
        private const val REQ_UPDATE = 2004
    }
}
