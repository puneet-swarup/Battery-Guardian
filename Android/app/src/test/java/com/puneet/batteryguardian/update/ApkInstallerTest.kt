package com.puneet.batteryguardian.update

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [ApkInstaller]. Covers the install-permission gate and
 * the failure path when a download cannot be completed.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class ApkInstallerTest {

    private lateinit var context: Context
    private lateinit var installer: ApkInstaller

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        installer = ApkInstaller(context)
    }

    @Test
    fun `canRequestInstalls reflects the package manager flag`() {
        // Default: not allowed to request installs.
        shadowOf(context.packageManager).setCanRequestPackageInstalls(false)
        assertFalse(ApkInstaller.canRequestInstalls(context))

        shadowOf(context.packageManager).setCanRequestPackageInstalls(true)
        assertTrue(ApkInstaller.canRequestInstalls(context))
    }

    @Test
    fun `downloadAndInstall returns false for an unreachable url`() {
        // A malformed URL cannot be opened; the installer must swallow the error
        // and report failure rather than throw.
        val result = installer.downloadAndInstall("not-a-valid-url")
        assertFalse(result)
    }
}
