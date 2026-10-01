package com.puneet.batteryguardian.update

import android.content.Context
import com.puneet.batteryguardian.BuildConfig
import com.puneet.batteryguardian.data.DiagnosticLog
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

/**
 * Queries the GitHub Releases API to determine whether a newer version of
 * Battery Guardian is available. Mirrors UpdateService.cs.
 *
 * All failures are silent: if the network is down or GitHub rate-limits us, the
 * caller simply sees "no update".
 */
class UpdateChecker(private val context: Context) {

    private val log = DiagnosticLog.get(context)

    /**
     * Returns update info when a newer release exists, otherwise null. Blocking -
     * call from a background thread or coroutine.
     */
    fun checkForUpdate(): UpdateInfo? {
        return try {
            val connection = (URL(LATEST_RELEASE_API_URL).openConnection() as HttpURLConnection).apply {
                requestMethod = "GET"
                connectTimeout = TIMEOUT_MS
                readTimeout = TIMEOUT_MS
                setRequestProperty("User-Agent", "BatteryGuardian-Android-UpdateChecker/1.0")
                setRequestProperty("Accept", "application/vnd.github+json")
            }

            val code = connection.responseCode
            if (code != HttpURLConnection.HTTP_OK) {
                log.write("Update check HTTP $code")
                connection.disconnect()
                return null
            }

            val body = connection.inputStream.bufferedReader().use { it.readText() }
            connection.disconnect()

            parseRelease(body)
        } catch (t: Throwable) {
            log.write("Update check failed: ${t.message}")
            null
        }
    }

    private fun parseRelease(json: String): UpdateInfo? {
        val root = JSONObject(json)
        val tag = root.optString("tag_name", "")
        val url = root.optString("html_url", "")
        val notes = root.optString("body", "")

        val remote = parseTagToVersion(tag) ?: return null
        val current = parseTagToVersion(BuildConfig.VERSION_NAME) ?: return null

        if (!isNewer(remote, current)) return null

        return UpdateInfo(
            latestVersion = tag,
            releaseUrl = url,
            releaseNotes = notes,
            downloadUrl = findApkAsset(root)
        )
    }

    private fun findApkAsset(root: JSONObject): String? {
        val assets = root.optJSONArray("assets") ?: return null
        for (i in 0 until assets.length()) {
            val asset = assets.optJSONObject(i) ?: continue
            val name = asset.optString("name", "")
            if (name.endsWith(".apk", ignoreCase = true)) {
                return asset.optString("browser_download_url", "").ifBlank { null }
            }
        }
        return null
    }

    companion object {
        private const val LATEST_RELEASE_API_URL =
            "https://api.github.com/repos/puneet-swarup/Battery-Guardian/releases/latest"
        private const val TIMEOUT_MS = 15000

        /**
         * Parses a tag like "v1.9.0", "1.9" or "v2" into a comparable list of
         * integers. Returns null if the tag cannot be parsed. Public and static
         * so it can be unit tested, exactly like UpdateService.ParseTagToVersion.
         */
        fun parseTagToVersion(tag: String): List<Int>? {
            val cleaned = tag.trim().removePrefix("v").removePrefix("V").trim()
            if (cleaned.isEmpty()) return null

            val parts = cleaned.split('.')
            val result = ArrayList<Int>(3)
            for (part in parts) {
                // Strip any suffix such as "-beta" or "+build" from the numeric part.
                val numeric = part.takeWhile { it.isDigit() }
                if (numeric.isEmpty()) break
                result.add(numeric.toInt())
                if (result.size == 3) break
            }
            if (result.isEmpty()) return null
            while (result.size < 3) result.add(0)
            return result
        }

        /** Compares two parsed versions; returns true when [a] is newer than [b]. */
        fun isNewer(a: List<Int>, b: List<Int>): Boolean {
            for (i in 0 until maxOf(a.size, b.size)) {
                val av = a.getOrElse(i) { 0 }
                val bv = b.getOrElse(i) { 0 }
                if (av != bv) return av > bv
            }
            return false
        }
    }
}
