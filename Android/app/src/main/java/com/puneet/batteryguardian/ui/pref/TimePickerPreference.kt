package com.puneet.batteryguardian.ui.pref

import android.app.TimePickerDialog
import android.content.Context
import android.content.res.TypedArray
import android.text.format.DateFormat
import android.util.AttributeSet
import androidx.preference.Preference
import com.puneet.batteryguardian.R
import java.util.Locale

/**
 * A preference that lets the user pick a time of day with the platform's native
 * time-picker dialog. The value is stored internally as minutes since local
 * midnight (the form the rest of the app uses), but the user only ever sees and
 * interacts with a familiar "HH:mm" clock — never a raw minute count.
 *
 * Persistence is owned by the hosting fragment (isPersistent = false), matching
 * [SliderInputPreference] and the rest of the settings screen.
 */
class TimePickerPreference @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = androidx.preference.R.attr.preferenceStyle,
    defStyleRes: Int = 0
) : Preference(context, attrs, defStyleAttr, defStyleRes) {

    /** Minutes since local midnight, 0..1439. */
    private var currentMinutes: Int = 0

    init {
        val a: TypedArray = context.obtainStyledAttributes(
            attrs, R.styleable.TimePickerPreference, defStyleAttr, defStyleRes
        )
        try {
            currentMinutes = normalize(a.getInt(R.styleable.TimePickerPreference_timeDefault, 0))
        } finally {
            a.recycle()
        }
        isPersistent = false
        updateSummary()
    }

    override fun onSetInitialValue(defaultValue: Any?) {
        currentMinutes = normalize((defaultValue as? Int) ?: 0)
        updateSummary()
    }

    override fun onGetDefaultValue(a: TypedArray, index: Int): Any =
        a.getInt(index, 0)

    /** Current value as minutes since midnight. */
    fun getValue(): Int = currentMinutes

    /** Sets the value (minutes since midnight) and refreshes the summary. */
    fun setValue(minutesSinceMidnight: Int) {
        currentMinutes = normalize(minutesSinceMidnight)
        updateSummary()
    }

    override fun onClick() {
        val hours = currentMinutes / 60
        val minutes = currentMinutes % 60

        val dialog = TimePickerDialog(
            context,
            { _, pickedHour, pickedMinute ->
                val newMinutes = pickedHour * 60 + pickedMinute
                // Let the fragment validate cross-field rules (start != end).
                if (callChangeListener(newMinutes)) {
                    setValue(newMinutes)
                }
            },
            hours,
            minutes,
            DateFormat.is24HourFormat(context)
        )
        dialog.show()
    }

    private fun updateSummary() {
        summary = formatTime(currentMinutes)
    }

    private fun normalize(minutes: Int): Int {
        val minutesPerDay = 24 * 60
        var m = minutes % minutesPerDay
        if (m < 0) m += minutesPerDay
        return m
    }

    companion object {
        /** Formats minutes-since-midnight as a zero-padded "HH:mm". */
        fun formatTime(minutesSinceMidnight: Int): String {
            val minutesPerDay = 24 * 60
            var m = minutesSinceMidnight % minutesPerDay
            if (m < 0) m += minutesPerDay
            return String.format(Locale.US, "%02d:%02d", m / 60, m % 60)
        }
    }
}
