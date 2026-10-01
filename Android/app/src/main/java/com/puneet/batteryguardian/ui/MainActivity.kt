package com.puneet.batteryguardian.ui

import android.Manifest
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.puneet.batteryguardian.BuildConfig
import com.puneet.batteryguardian.R
import com.puneet.batteryguardian.battery.BatteryReader
import com.puneet.batteryguardian.battery.BatterySnapshot
import com.puneet.batteryguardian.battery.BatteryStatus
import com.puneet.batteryguardian.core.BatteryHealth
import com.puneet.batteryguardian.core.HealthLabel
import com.puneet.batteryguardian.data.DiagnosticLog
import com.puneet.batteryguardian.data.SettingsRepository
import com.puneet.batteryguardian.databinding.ActivityMainBinding
import com.puneet.batteryguardian.service.BatteryMonitorService
import com.puneet.batteryguardian.update.ApkInstaller
import com.puneet.batteryguardian.update.UpdateChecker
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.util.Locale

/**
 * Main screen: shows live battery state and health, and exposes the monitoring
 * controls. Analogous to MainWindow.xaml.cs in the Windows application.
 */
class MainActivity : AppCompatActivity() {

    private lateinit var binding: ActivityMainBinding
    private lateinit var settingsRepository: SettingsRepository
    private lateinit var batteryReader: BatteryReader
    private lateinit var log: DiagnosticLog

    private var batteryReceiver: BroadcastReceiver? = null

    private val notificationPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { /* no-op */ }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        settingsRepository = SettingsRepository(this)
        batteryReader = BatteryReader(this)
        log = DiagnosticLog.get(this)

        wireButtons()
        ensureNotificationPermission()
    }

    override fun onResume() {
        super.onResume()
        registerBatteryReceiver()
        refreshUi(batteryReader.read())
    }

    override fun onPause() {
        unregisterBatteryReceiver()
        super.onPause()
    }

    private fun wireButtons() {
        binding.monitorToggleButton.setOnClickListener {
            val settings = settingsRepository.load()
            if (settings.monitoringEnabled) {
                BatteryMonitorService.stop(this)
                settingsRepository.setMonitoringEnabled(false)
            } else {
                BatteryMonitorService.start(this)
                settingsRepository.setMonitoringEnabled(true)
            }
            // Give the service a moment to reflect the change, then refresh.
            binding.root.postDelayed({ refreshUi(batteryReader.read()) }, 300)
        }

        binding.snoozeButton.setOnClickListener { showSnoozeDialog() }
        binding.exemptBatteryButton.setOnClickListener { requestBatteryExemption() }
        binding.settingsButton.setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }
        binding.aboutButton.setOnClickListener {
            startActivity(Intent(this, AboutActivity::class.java))
        }
        binding.checkUpdateButton.setOnClickListener { checkForUpdateManually() }
    }

    private fun registerBatteryReceiver() {
        if (batteryReceiver != null) return
        val receiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context, intent: Intent) {
                if (intent.action == Intent.ACTION_BATTERY_CHANGED) {
                    refreshUi(batteryReader.read())
                }
            }
        }
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                registerReceiver(receiver, IntentFilter(Intent.ACTION_BATTERY_CHANGED), Context.RECEIVER_NOT_EXPORTED)
            } else {
                registerReceiver(receiver, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
            }
            batteryReceiver = receiver
        } catch (t: Throwable) {
            log.writeException("registerBatteryReceiver", t)
        }
    }

    private fun unregisterBatteryReceiver() {
        batteryReceiver?.let {
            try {
                unregisterReceiver(it)
            } catch (_: Throwable) {
            }
        }
        batteryReceiver = null
    }

    private fun refreshUi(snapshot: BatterySnapshot) {
        val settings = settingsRepository.load()

        // Monitoring status.
        binding.monitoringStatus.setText(
            if (settings.monitoringEnabled) R.string.monitoring_on else R.string.monitoring_off
        )
        binding.monitorToggleButton.setText(
            if (settings.monitoringEnabled) R.string.stop_monitoring else R.string.start_monitoring
        )

        // Battery level.
        if (snapshot.percent >= 0) {
            binding.batteryPercent.text = String.format(Locale.US, "%d%%", snapshot.percent)
            binding.batteryProgress.progress = snapshot.percent
        } else {
            binding.batteryPercent.text = "--%"
            binding.batteryProgress.progress = 0
        }

        binding.batteryStatus.text = getString(statusStringRes(snapshot.status))

        // Health.
        val health = snapshot.health?.let {
            BatteryHealth.from(
                designCapacity = it.designCapacity ?: 0L,
                fullChargeCapacity = it.fullChargeCapacity ?: 0L
            )
        }
        updateHealthUi(health)

        // Snooze visibility.
        binding.snoozeButton.isEnabled = settings.monitoringEnabled
    }

    private fun updateHealthUi(health: BatteryHealth?) {
        if (health == null) {
            binding.healthValue.setText(R.string.battery_health_unavailable)
            binding.healthDetail.visibility = View.GONE
            return
        }

        val pct = health.healthPercent
        if (pct < 0) {
            binding.healthValue.setText(R.string.battery_health_unavailable)
            binding.healthDetail.visibility = View.GONE
            return
        }

        val labelRes = when (health.healthLabel) {
            HealthLabel.GOOD -> R.string.health_good
            HealthLabel.FAIR -> R.string.health_fair
            HealthLabel.POOR -> R.string.health_poor
            HealthLabel.UNAVAILABLE -> R.string.health_unavailable
        }
        binding.healthValue.text = String.format(Locale.US, "%d%% · %s", pct, getString(labelRes))
        binding.healthDetail.visibility = View.VISIBLE
        binding.healthDetail.text = String.format(
            Locale.US,
            "%s: %d / %s: %d",
            getString(R.string.design_capacity_label), health.designCapacity,
            getString(R.string.full_charge_capacity_label), health.fullChargeCapacity
        )
    }

    private fun statusStringRes(status: BatteryStatus): Int = when (status) {
        BatteryStatus.CHARGING -> R.string.status_charging
        BatteryStatus.DISCHARGING -> R.string.status_discharging
        BatteryStatus.FULL -> R.string.status_full
        BatteryStatus.NOT_CHARGING -> R.string.status_not_charging
        BatteryStatus.UNKNOWN -> R.string.status_unknown
    }

    private fun showSnoozeDialog() {
        val options = arrayOf("30 minutes", "1 hour", "2 hours")
        val minutes = intArrayOf(30, 60, 120)
        AlertDialog.Builder(this)
            .setTitle(R.string.snooze_button)
            .setItems(options) { _, which ->
                BatteryMonitorService.snooze(this, minutes[which])
                Toast.makeText(
                    this,
                    getString(R.string.alert_snoozed_fmt, minutes[which]),
                    Toast.LENGTH_SHORT
                ).show()
            }
            .setNegativeButton(R.string.cancel, null)
            .show()
    }

    private fun requestBatteryExemption() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            Toast.makeText(this, R.string.error_generic, Toast.LENGTH_SHORT).show()
            return
        }
        val pm = getSystemService(POWER_SERVICE) as android.os.PowerManager
        if (pm.isIgnoringBatteryOptimizations(packageName)) {
            Toast.makeText(this, R.string.monitoring_on, Toast.LENGTH_SHORT).show()
            return
        }
        try {
            @Suppress("BATTERY_LIFE")
            val intent = Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                data = Uri.parse("package:$packageName")
            }
            startActivity(intent)
        } catch (t: Throwable) {
            log.writeException("requestBatteryExemption", t)
            // Fall back to the general battery optimisation settings screen.
            try {
                startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
            } catch (_: Throwable) {
                Toast.makeText(this, R.string.error_generic, Toast.LENGTH_SHORT).show()
            }
        }
    }

    private fun ensureNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
    }

    private fun checkForUpdateManually() {
        Toast.makeText(this, R.string.check_update_button, Toast.LENGTH_SHORT).show()
        lifecycleScope.launch {
            val info = withContext(Dispatchers.IO) { UpdateChecker(this@MainActivity).checkForUpdate() }
            if (info == null) {
                Toast.makeText(this@MainActivity, R.string.update_up_to_date, Toast.LENGTH_LONG).show()
                return@launch
            }

            val message = buildString {
                append(getString(R.string.update_available_fmt, info.latestVersion))
                if (info.releaseNotes.isNotBlank()) {
                    append("\n\n")
                    append(info.releaseNotes.take(500))
                }
            }

            AlertDialog.Builder(this@MainActivity)
                .setTitle(R.string.update_available_title)
                .setMessage(message)
                .setPositiveButton(R.string.update_open_release_button) { _, _ ->
                    openUrl(info.releaseUrl)
                }
                .apply {
                    if (info.downloadUrl != null) {
                        setNeutralButton(R.string.update_download_button) { _, _ ->
                            downloadUpdate(info.downloadUrl)
                        }
                    }
                }
                .setNegativeButton(R.string.cancel, null)
                .show()
        }
    }

    private fun downloadUpdate(url: String) {
        if (!ApkInstaller.canRequestInstalls(this)) {
            Toast.makeText(this, R.string.error_generic, Toast.LENGTH_LONG).show()
        }
        Toast.makeText(this, R.string.update_download_button, Toast.LENGTH_SHORT).show()
        lifecycleScope.launch {
            withContext(Dispatchers.IO) {
                ApkInstaller(this@MainActivity).downloadAndInstall(url)
            }
        }
    }

    private fun openUrl(url: String) {
        try {
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
        } catch (t: Throwable) {
            log.writeException("openUrl", t)
        }
    }
}
