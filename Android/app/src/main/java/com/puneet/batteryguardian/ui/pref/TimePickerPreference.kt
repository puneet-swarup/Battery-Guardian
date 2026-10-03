package com.puneet.batteryguardian.ui.pref

import android.content.Context
import android.content.res.TypedArray
import android.util.AttributeSet
import android.view.View
import android.widget.NumberPicker
import android.widget.TextView
import androidx.appcompat.app.AlertDialog
import androidx.preference.Preference
import com.puneet.batteryguardian.R
import java.util.Locale

/**
 * A preference that lets the user pick a time of day with a deliberately simple
 * dialog: two number pickers (hours and minutes) separated by a colon.
 *
 * This intentionally avoids the Material clock-face picker, which is visually
 * busy and clashes with the app's dark theme. The user only ever sees a familiar
 * "HH:mm" value; internally the choice is stored as minutes since local midnight
 * (the form the rest of the app uses).
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

    override fun onGetDefaultValue(a: TypedArray, index: Int): Int =
        a.getInt(index, 0)

    /** Current value as minutes since midnight. */
    fun getValue(): Int = currentMinutes

    /** Sets the value (minutes since midnight) and refreshes the summary. */
    fun setValue(minutesSinceMidnight: Int) {
        currentMinutes = normalize(minutesSinceMidnight)
        updateSummary()
    }

    override fun onClick() {
        val dialogView = View.inflate(context, R.layout.dialog_time_picker, null)
        val hourPicker = dialogView.findViewById<NumberPicker>(R.id.hourPicker)
        val minutePicker = dialogView.findViewById<NumberPicker>(R.id.minutePicker)

        configurePicker(hourPicker, 0, 23, currentMinutes / 60)
        configurePicker(minutePicker, 0, 59, currentMinutes % 60)

        AlertDialog.Builder(context)
            .setTitle(title)
            .setView(dialogView)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                val newMinutes = hourPicker.value * 60 + minutePicker.value
                // Let the fragment validate cross-field rules (start != end).
                if (callChangeListener(newMinutes)) {
                    setValue(newMinutes)
                }
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    /**
     * Sets up a NumberPicker with a range and initial value, and forces the
     * wheel text to the app's light foreground colour so it stays readable on
     * the dark dialog surface. The dividers are tinted subtly.
     */
    private fun configurePicker(picker: NumberPicker, min: Int, max: Int, value: Int) {
        picker.minValue = min
        picker.maxValue = max
        picker.value = value
        picker.wrapSelectorWheel = true
        picker.descendantFocusability = NumberPicker.FOCUS_BLOCK_DESCENDANTS

        val light = context.getResources().getColor(R.color.text_primary, context.theme)
        tintPickerChildren(picker, light)
    }

    /**
     * NumberPicker does not expose its inner EditText, so we walk the child views
     * and recolour the text (and soften the divider) for the dark theme.
     */
    private fun tintPickerChildren(picker: NumberPicker, color: Int) {
        for (i in 0 until picker.childCount) {
            val child = picker.getChildAt(i)
            if (child is TextView) {
                child.setTextColor(color)
            }
        }
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
