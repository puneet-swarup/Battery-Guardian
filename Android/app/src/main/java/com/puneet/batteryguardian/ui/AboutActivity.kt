package com.puneet.batteryguardian.ui

import android.content.Intent
import android.net.Uri
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import com.puneet.batteryguardian.BuildConfig
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.databinding.ActivityAboutBinding

/**
 * Simple about screen, mirroring AboutWindow.xaml in the Windows application.
 */
class AboutActivity : AppCompatActivity() {

    private lateinit var binding: ActivityAboutBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityAboutBinding.inflate(layoutInflater)
        setContentView(binding.root)

        setSupportActionBar(binding.toolbar)
        supportActionBar?.setDisplayHomeAsUpEnabled(true)

        binding.aboutVersion.text = getString(
            R.string.about_version_fmt,
            BuildConfig.VERSION_NAME,
            BuildConfig.VERSION_CODE
        )

        binding.tuyaButton.setOnClickListener { open(TUYA_SEARCH_URL) }

        binding.githubButton.setOnClickListener { open(GITHUB_URL) }
    }

    override fun onSupportNavigateUp(): Boolean {
        finish()
        return true
    }

    private fun open(url: String) {
        try {
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        } catch (_: Throwable) {
        }
    }

    companion object {
        private const val GITHUB_URL = "https://github.com/puneet-swarup/Battery-Guardian"

        // A plain search link on purpose: it explains the hardware in the
        // reader's own words and language, and cannot go stale.
        private const val TUYA_SEARCH_URL = "https://www.google.com/search?q=tuya+smart+plug+what+is"
    }
}
