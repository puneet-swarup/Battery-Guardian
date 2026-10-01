package com.puneet.batteryguardian.ui.pref

import android.content.Context
import android.content.res.TypedArray
import android.text.Editable
import android.text.InputType
import android.text.TextWatcher
import android.util.AttributeSet
import android.view.View
import android.widget.EditText
import android.widget.SeekBar
import androidx.appcompat.app.AlertDialog
import androidx.preference.Preference
import com.puneet.batteryguardian.R

/**
 * A preference that lets the user pick an integer either by dragging a slider
 * or by typing the exact value into a text field. Both controls stay in sync.
 *
 * The value is not stored by the preference itself (isPersistent = false); the
 * hosting fragment owns persistence through SettingsRepository, exactly like
 * the rest of the settings screen.
 *
 * Used for the high/low battery thresholds, where precise entry matters.
 */
class SliderInputPreference @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null,
    defStyleAttr: Int = androidx.preference.R.attr.preferenceStyle,
    defStyleRes: Int = 0
) : Preference(context, attrs, defStyleAttr, defStyleRes) {

    private var currentValue: Int = 0
    private var minValue: Int = 0
    private var maxValue: Int = 100
    private var suffix: String = ""

    init {
        val a: TypedArray = context.obtainStyledAttributes(
            attrs, R.styleable.SliderInputPreference, defStyleAttr, defStyleRes
        )
        try {
            minValue = a.getInt(R.styleable.SliderInputPreference_sliderMin, 0)
            maxValue = a.getInt(R.styleable.SliderInputPreference_sliderMax, 100)
            suffix = a.getString(R.styleable.SliderInputPreference_sliderSuffix) ?: ""
        } finally {
            a.recycle()
        }
        if (maxValue <= minValue) maxValue = minValue + 1
        currentValue = minValue
        // This preference is managed by the fragment, not by SharedPreferences.
        isPersistent = false
        updateSummary(currentValue)
    }

    override fun onSetInitialValue(defaultValue: Any?) {
        val def = (defaultValue as? Int) ?: minValue
        currentValue = def.coerceIn(minValue, maxValue)
        updateSummary(currentValue)
    }

    override fun onGetDefaultValue(a: TypedArray, index: Int): Any {
        return a.getInt(index, minValue)
    }

    /** Current value held by the preference. */
    fun getValue(): Int = currentValue

    /** Sets the value and refreshes the summary. Does not persist by itself. */
    fun setValue(value: Int) {
        val clamped = value.coerceIn(minValue, maxValue)
        currentValue = clamped
        updateSummary(clamped)
    }

    fun setMinValue(min: Int) {
        minValue = min
        if (maxValue <= minValue) maxValue = minValue + 1
        currentValue = currentValue.coerceIn(minValue, maxValue)
        updateSummary(currentValue)
    }

    fun setMaxValue(max: Int) {
        maxValue = max
        if (maxValue <= minValue) maxValue = minValue + 1
        currentValue = currentValue.coerceIn(minValue, maxValue)
        updateSummary(currentValue)
    }

    private fun updateSummary(value: Int) {
        summary = "$value$suffix"
    }

    override fun onClick() {
        showDialog()
    }

    private fun showDialog() {
        val dialogView = View.inflate(context, R.layout.dialog_slider_input, null)
        val seekBar = dialogView.findViewById<SeekBar>(R.id.sliderSeekBar)
        val valueInput = dialogView.findViewById<EditText>(R.id.sliderValueInput)

        // SeekBar works on a 0-based range; map it to [minValue, maxValue].
        seekBar.max = maxValue - minValue
        seekBar.progress = currentValue - minValue

        valueInput.inputType = InputType.TYPE_CLASS_NUMBER
        valueInput.setText(currentValue.toString())
        valueInput.setSelection(valueInput.text.length)

        seekBar.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(sb: SeekBar?, progress: Int, fromUser: Boolean) {
                if (!fromUser) return
                val v = minValue + progress
                valueInput.setText(v.toString())
                valueInput.setSelection(valueInput.text.length)
            }

            override fun onStartTrackingTouch(sb: SeekBar?) {}
            override fun onStopTrackingTouch(sb: SeekBar?) {}
        })

        // Typing a valid number moves the slider to match.
        valueInput.addTextChangedListener(object : TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
            override fun afterTextChanged(s: Editable?) {
                val v = s?.toString()?.trim()?.toIntOrNull() ?: return
                if (v in minValue..maxValue) {
                    seekBar.progress = v - minValue
                }
            }
        })

        val dialog = AlertDialog.Builder(context)
            .setTitle(title)
            .setView(dialogView)
            .setPositiveButton(android.R.string.ok, null)
            .setNegativeButton(android.R.string.cancel, null)
            .create()

        dialog.setOnShowListener {
            dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
                val entered = valueInput.text.toString().trim().toIntOrNull()
                if (entered == null || entered < minValue || entered > maxValue) {
                    valueInput.error = context.getString(
                        R.string.settings_threshold_range_error, minValue, maxValue
                    )
                    return@setOnClickListener
                }
                // Let the fragment validate cross-field rules (high vs low).
                if (callChangeListener(entered)) {
                    setValue(entered)
                    dialog.dismiss()
                } else {
                    valueInput.error = context.getString(R.string.settings_threshold_conflict_error)
                }
            }
        }

        dialog.show()
    }
}
