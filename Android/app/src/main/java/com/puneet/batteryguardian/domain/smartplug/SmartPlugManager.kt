package com.puneet.batteryguardian.domain.smartplug

import android.content.Context
import com.puneet.batteryguardian.data.Settings
import com.puneet.batteryguardian.data.tuya.TuyaCredentialStore
import com.puneet.batteryguardian.data.tuya.TuyaLocalKey
import com.puneet.batteryguardian.data.tuya.TuyaSmartPlug
import com.tuyalan.protocol.TuyaProtocolVersion

// Builds and owns a SmartPlugController from persisted settings + the
// Keystore-backed local key. Returns null when the feature is disabled or
// not fully configured, so callers can skip network work entirely.
class SmartPlugManager(private val context: Context) {
 private var controller: SmartPlugController? = null
 private var signature: String = ""

 fun controllerFor(settings: Settings): SmartPlugController? {
 if (!settings.smartPlugEnabled) { close(); return null }
 val key = TuyaCredentialStore(context).load() ?: return null
 val sig = buildSignature(settings, key)
 if (controller != null && sig == signature) return controller
 close()
 val config = SmartPlugConfig(
 enabled = settings.smartPlugEnabled,
 highThreshold = settings.smartPlugHighThreshold,
 lowThreshold = settings.smartPlugLowThreshold,
 deviceId = settings.smartPlugDeviceId,
 host = settings.smartPlugHost,
 port = settings.smartPlugPort,
 relayDp = settings.smartPlugRelayDp,
 protocolVersion = settings.smartPlugProtocolVersion
 )
 if (config.validationError() != null) return null
 val localKey = TuyaLocalKey.parse(key)
 val plug = TuyaSmartPlug(
 deviceId = settings.smartPlugDeviceId,
 host = settings.smartPlugHost,
 port = settings.smartPlugPort,
 localKey = localKey,
 relayDp = settings.smartPlugRelayDp,
 version = protocolVersion(settings.smartPlugProtocolVersion),
 )
 controller = SmartPlugController(plug, config)
 signature = sig
 return controller
 }

 /** The settings store keeps the version as text; the SDK wants the enum. */
 private fun protocolVersion(value: String): TuyaProtocolVersion = when (value.trim()) {
 "3.3" -> TuyaProtocolVersion.V33
 "3.4" -> TuyaProtocolVersion.V34
 else -> TuyaProtocolVersion.V35
 }

 private fun buildSignature(s: Settings, key: String): String =
 listOf(s.smartPlugDeviceId, s.smartPlugHost, s.smartPlugPort.toString(), s.smartPlugRelayDp.toString(), key, s.smartPlugHighThreshold.toString(), s.smartPlugLowThreshold.toString()).joinToString("|")

 fun close() {
 controller?.close()
 controller = null
 signature = ""
 }
}
