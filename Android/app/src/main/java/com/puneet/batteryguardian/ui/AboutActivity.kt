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

        binding.githubButton.setOnClickListener {
            try {
                startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(GITHUB_URL)))
            } catch (_: Throwable) {
            }
        }
    }

    override fun onSupportNavigateUp(): Boolean {
        finish()
        return true
    }

    companion object {
        private const val GITHUB_URL = "https://github.com/puneet-swarup/Battery-Guardian"
    }
}
