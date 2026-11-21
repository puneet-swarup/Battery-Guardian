package com.puneet.batteryguardian.data.tuya

import com.tuyalan.devices.TuyaDeviceOptions
import com.tuyalan.devices.TuyaLocalDevice
import com.tuyalan.protocol.TuyaProtocolVersion
import kotlinx.coroutines.runBlocking

/**
 * Talks to a Tuya smart plug over the local network.
 *
 * The protocol itself is `com.tuyalan.devices.TuyaLocalDevice` from the
 * [Tuya LAN SDK](https://github.com/puneet-swarup/tuya-lan-sdk). This class is
 * only the adapter: it keeps the app's blocking [SmartPlug] shape, holds one
 * connection open between polls, and lets the SDK's exceptions reach
 * [com.puneet.batteryguardian.domain.smartplug.SmartPlugController], which
 * records them for the settings screen.
 *
 * It used to carry its own copy of the protocol. That copy was built from a
 * description that does not match real 3.5 firmware - wrong frame layout, the
 * 3.4 session-key derivation, control on 0x07 with a bare dps body - so it could
 * never have switched a physical plug.
 */
class TuyaSmartPlug(
    deviceId: String,
    host: String,
    port: Int,
    localKey: ByteArray,
    relayDp: Int,
    version: TuyaProtocolVersion = TuyaProtocolVersion.V35,
    private val timeoutMs: Int = 5000,
) : SmartPlug {

    private val relayDpId = relayDp.toString()

    private val options = TuyaDeviceOptions(
        host = host,
        deviceId = deviceId,
        // The SDK takes either form; bytes win when both are supplied.
        localKey = "",
        localKeyBytes = localKey,
        version = version,
        port = port,
        connectTimeoutMillis = timeoutMs,
        commandTimeoutMillis = timeoutMs.toLong(),
        // The plug is polled on a timer; its heartbeat is redundant here.
        enableHeartbeat = false,
    )

    private var device: TuyaLocalDevice? = null

    private suspend fun connected(): TuyaLocalDevice {
        val current = device
        if (current != null && current.isConnected) return current

        closeDevice()

        val fresh = TuyaLocalDevice(options)
        fresh.connect()
        device = fresh
        return fresh
    }

    @Synchronized
    override fun getStatus(): RelayState = runBlocking {
        mapRelay(connected().getStatus().getBool(relayDpId))
    }

    @Synchronized
    override fun turnOn() = setRelay(true)

    @Synchronized
    override fun turnOff() = setRelay(false)

    private fun setRelay(on: Boolean) {
        runBlocking { connected().setDp(relayDpId, on) }
    }

    override fun testConnection(): Boolean = try {
        getStatus()
        true
    } catch (t: Throwable) {
        false
    }

    @Synchronized
    override fun close() = closeDevice()

    private fun closeDevice() {
        val current = device ?: return
        device = null
        try {
            current.close()
        } catch (_: Throwable) {
            // A dead socket is not worth reporting on a teardown path.
        }
    }

    companion object {
        /** Maps the SDK's nullable boolean onto the controller's tri-state. */
        fun mapRelay(value: Boolean?): RelayState = when (value) {
            true -> RelayState.ON
            false -> RelayState.OFF
            null -> RelayState.UNKNOWN
        }
    }
}
