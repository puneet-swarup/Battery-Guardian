package com.puneet.batteryguardian.ui

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import com.puneet.batteryguardian.R

/**
 * Hosts the [SettingsFragment]. Mirrors SettingsWindow.xaml in the Windows app.
 */
class SettingsActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)
        setSupportActionBar(findViewById(R.id.toolbar))
        supportActionBar?.setDisplayHomeAsUpEnabled(true)

        if (savedInstanceState == null) {
            supportFragmentManager.beginTransaction()
                .replace(R.id.settingsContainer, SettingsFragment())
                .commit()
        }
    }

    override fun onSupportNavigateUp(): Boolean {
        finish()
        return true
    }
}
