package com.puneet.batteryguardian.update

/**
 * Information about an available update, mirroring UpdateInfo from the Windows
 * UpdateService.cs. [downloadUrl] is the APK asset URL when one is published.
 */
data class UpdateInfo(
    val latestVersion: String,
    val releaseUrl: String,
    val releaseNotes: String,
    val downloadUrl: String?
)
