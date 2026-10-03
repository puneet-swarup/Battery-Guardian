package com.puneet.batteryguardian.core

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Tests for the pure charge-limit support resolver. Verifies manufacturer
 * mapping and the guidance/action each vendor receives.
 */
class ChargeLimitResolverTest {

    @Test
    fun `samsung is supported`() {
        val support = ChargeLimitResolver.resolve("samsung")
        assertTrue(support.isSupported)
        assertTrue(support.guidance.contains("Protect battery"))
    }

    @Test
    fun `samsung matching is case insensitive`() {
        assertTrue(ChargeLimitResolver.resolve("SAMSUNG").isSupported)
        assertTrue(ChargeLimitResolver.resolve("Samsung").isSupported)
    }

    @Test
    fun `google pixel is supported`() {
        val support = ChargeLimitResolver.resolve("Google")
        assertTrue(support.isSupported)
        assertTrue(support.guidance.contains("80%"))
    }

    @Test
    fun `oneplus oppo realme are supported`() {
        assertTrue(ChargeLimitResolver.resolve("OnePlus").isSupported)
        assertTrue(ChargeLimitResolver.resolve("OPPO").isSupported)
        assertTrue(ChargeLimitResolver.resolve("realme").isSupported)
    }

    @Test
    fun `xiaomi redmi poco are supported`() {
        assertTrue(ChargeLimitResolver.resolve("Xiaomi").isSupported)
        assertTrue(ChargeLimitResolver.resolve("Redmi").isSupported)
        assertTrue(ChargeLimitResolver.resolve("POCO").isSupported)
    }

    @Test
    fun `unknown manufacturer is unsupported but offers generic guidance`() {
        val support = ChargeLimitResolver.resolve("SomeBrand")
        assertFalse(support.isSupported)
        assertTrue(support.guidance.isNotBlank())
    }

    @Test
    fun `null manufacturer is unsupported`() {
        val support = ChargeLimitResolver.resolve(null)
        assertFalse(support.isSupported)
    }

    @Test
    fun `blank manufacturer is unsupported`() {
        assertFalse(ChargeLimitResolver.resolve("   ").isSupported)
    }

    @Test
    fun `supported vendors expose a settings action`() {
        assertTrue(ChargeLimitResolver.resolve("samsung").settingsAction != null)
        assertTrue(ChargeLimitResolver.resolve("google").settingsAction != null)
    }
}
