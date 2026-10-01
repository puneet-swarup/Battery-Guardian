package com.puneet.batteryguardian.ui

import android.content.Intent
import com.puneet.batteryguardian.BuildConfig
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config

/**
 * Robolectric tests for [AboutActivity]: version label rendering, the back
 * navigation contract and the GitHub button launching a browser intent.
 */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class AboutActivityTest {

    @Test
    fun `activity launches and shows the version`() {
        val controller = Robolectric.buildActivity(AboutActivity::class.java).setup()
        val activity = controller.get()

        val versionText = activity.findViewById<android.widget.TextView>(com.puneet.batteryguardian.R.id.aboutVersion)
        assertNotNull(versionText)
        val expected = activity.getString(
            com.puneet.batteryguardian.R.string.about_version_fmt,
            BuildConfig.VERSION_NAME,
            BuildConfig.VERSION_CODE
        )
        assertEquals(expected, versionText.text.toString())
    }

    @Test
    fun `github button opens a browser intent`() {
        val activity = Robolectric.buildActivity(AboutActivity::class.java).setup().get()
        activity.findViewById<android.widget.Button>(com.puneet.batteryguardian.R.id.githubButton)
            .performClick()

        val next = shadowOf(activity).nextStartedActivity
        assertNotNull(next)
        assertEquals(Intent.ACTION_VIEW, next.action)
    }

    @Test
    fun `navigating up finishes the activity`() {
        val controller = Robolectric.buildActivity(AboutActivity::class.java).setup()
        val activity = controller.get()
        assertFalse(activity.isFinishing)
        assertTrue(activity.onSupportNavigateUp())
        assertTrue(activity.isFinishing)
    }
}
