package com.puneet.batteryguardian.update

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.core.content.FileProvider
import com.puneet.batteryguardian.data.DiagnosticLog
import java.io.File
import java.net.HttpURLConnection
import java.net.URL

/**
 * Downloads an update APK and hands it to the system package installer via a
 * FileProvider. Android always requires the user to confirm installation, so
 * this only launches the installer prompt.
 */
class ApkInstaller(private val context: Context) {

    private val log = DiagnosticLog.get(context)

    /**
     * Downloads [url] into the app cache and launches the installer. Blocking -
     * call from a background thread. Returns true if the installer was launched.
     */
    fun downloadAndInstall(url: String, fileName: String = "update.apk"): Boolean {
        return try {
            val apkFile = download(url, fileName) ?: return false
            launchInstaller(apkFile)
        } catch (t: Throwable) {
            log.writeException("ApkInstaller", t)
            false
        }
    }

    private fun download(url: String, fileName: String): File? {
        val dir = File(context.cacheDir, "updates").apply { mkdirs() }
        val target = File(dir, fileName)
        if (target.exists()) target.delete()

        val connection = (URL(url).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = TIMEOUT_MS
            readTimeout = TIMEOUT_MS
            instanceFollowRedirects = true
            setRequestProperty("User-Agent", "BatteryGuardian-Android-Updater/1.0")
        }

        val code = connection.responseCode
        if (code != HttpURLConnection.HTTP_OK) {
            log.write("APK download HTTP $code")
            connection.disconnect()
            return null
        }

        connection.inputStream.use { input ->
            target.outputStream().use { output ->
                input.copyTo(output)
            }
        }
        connection.disconnect()
        log.write("APK downloaded to ${target.absolutePath}")
        return target
    }

    private fun launchInstaller(apkFile: File): Boolean {
        val authority = "${context.packageName}.fileprovider"
        val uri: Uri = FileProvider.getUriForFile(context, authority, apkFile)

        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }

        return try {
            context.startActivity(intent)
            true
        } catch (t: Throwable) {
            log.writeException("launchInstaller", t)
            false
        }
    }

    companion object {
        private const val TIMEOUT_MS = 30000

        /**
         * Whether the app is allowed to request package installs. On Android 8+
         * this requires the REQUEST_INSTALL_PACKAGES permission and, on some
         * OEMs, an explicit user grant in settings.
         */
        fun canRequestInstalls(context: Context): Boolean {
            return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.packageManager.canRequestPackageInstalls()
            } else true
        }
    }
}
