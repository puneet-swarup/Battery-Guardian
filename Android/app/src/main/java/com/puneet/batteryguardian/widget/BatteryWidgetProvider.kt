package com.puneet.batteryguardian.widget

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.view.View
import android.widget.RemoteViews
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.battery.BatteryReader
import com.puneet.batteryguardian.ui.MainActivity

/**
 * Home-screen widget showing live battery level, status and (where available)
 * health. Refreshes when the battery changes and when the periodic update fires.
 *
 * The presentation logic lives in [WidgetState] so it can be unit-tested; this
 * class is a thin Android binding.
 */
class BatteryWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        for (id in appWidgetIds) {
            updateWidget(context, appWidgetManager, id)
        }
    }

    companion object {
        /**
         * Refreshes every instance of the widget. Safe to call from anywhere
         * (e.g. the battery receiver); never throws.
         */
        fun refreshAll(context: Context) {
            try {
                val manager = AppWidgetManager.getInstance(context) ?: return
                val component = ComponentName(context, BatteryWidgetProvider::class.java)
                val ids = manager.getAppWidgetIds(component)
                for (id in ids) {
                    updateWidget(context, manager, id)
                }
            } catch (t: Throwable) {
                // Widget refresh must never crash the monitoring path.
            }
        }

        private fun updateWidget(
            context: Context,
            manager: AppWidgetManager,
            widgetId: Int
        ) {
            try {
                val snapshot = BatteryReader(context).read()
                val state = WidgetState.from(snapshot)

                val views = RemoteViews(context.packageName, R.layout.widget_battery)
                views.setTextViewText(R.id.widget_level, state.levelText)
                views.setTextViewText(R.id.widget_status, state.statusText)

                if (state.healthText != null) {
                    views.setTextViewText(R.id.widget_health, state.healthText)
                    views.setViewVisibility(R.id.widget_health, View.VISIBLE)
                } else {
                    views.setViewVisibility(R.id.widget_health, View.GONE)
                }

                // Tapping the widget opens the main screen.
                val tapIntent = Intent(context, MainActivity::class.java).apply {
                    flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
                }
                val pending = PendingIntent.getActivity(
                    context,
                    0,
                    tapIntent,
                    PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
                )
                views.setOnClickPendingIntent(R.id.widget_root, pending)

                manager.updateAppWidget(widgetId, views)
            } catch (t: Throwable) {
                // Ignore: a failed widget update is not worth surfacing.
            }
        }
    }
}
