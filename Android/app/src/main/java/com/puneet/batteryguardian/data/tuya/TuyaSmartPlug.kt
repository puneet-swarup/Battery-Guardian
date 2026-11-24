package com.puneet.batteryguardian.data.tuya

import com.puneet.batteryguardian.data.DiagnosticLog
import com.tuyalan.TuyaCryptoException
import com.tuyalan.TuyaHandshakeException
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
    private val log: DiagnosticLog? = null,
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
        // The heartbeat is what notices a connection that dies between polls.
        // Without it a socket that goes quiet is only discovered when a command
        // fails - which is exactly the failure this was.
        enableHeartbeat = true,
    )

    private var device: TuyaLocalDevice? = null

    /** The version that last connected; tried first from then on. */
    @Volatile
    private var workingVersion: TuyaProtocolVersion = version

    private suspend fun connected(): TuyaLocalDevice {
        // ensureConnected() reconnects over a socket that died quietly. Before
        // 2.1.1 isConnected stayed true after the peer closed, so a cached device
        // was reused for ever and every command timed out.
        val current = device
        if (current != null) {
            current.ensureConnectedAsync()
            return current
        }

        closeDevice()
        device = connectLaddered()
        return device!!
    }

    /**
     * Tries the last working version, then downwards: 3.5, 3.4, 3.3.
     *
     * Laddering is only correct when the device *answered* and rejected the
     * handshake - that is what a version mismatch looks like. A timeout is a
     * network problem and is reported at once instead of tripling the wait.
     */
    private suspend fun connectLaddered(): TuyaLocalDevice {
        val candidates = when (workingVersion) {
            TuyaProtocolVersion.V33 -> listOf(TuyaProtocolVersion.V33)
            TuyaProtocolVersion.V34 -> listOf(TuyaProtocolVersion.V34, TuyaProtocolVersion.V33)
            else -> listOf(TuyaProtocolVersion.V35, TuyaProtocolVersion.V34, TuyaProtocolVersion.V33)
        }

        var last: Throwable? = null
        for (candidate in candidates) {
            val attempt = TuyaLocalDevice(options.copy(version = candidate))
            try {
                attempt.connectAsync()
                workingVersion = candidate
                log?.writeAlways("PLUG CONNECT: $candidate to ${options.host}:${options.port} succeeded")
                return attempt
            } catch (t: Throwable) {
                last = t
                val answeredButRejected = t is TuyaHandshakeException || t is TuyaCryptoException
                log?.writeAlways(
                    "PLUG CONNECT: $candidate to ${options.host}:${options.port} failed - " +
                        "${t.javaClass.simpleName}: ${t.message}" +
                        if (answeredButRejected) " (device answered and rejected the handshake)" else " (network)"
                )
                runCatching { attempt.close() }
                if (!answeredButRejected) throw t
            }
        }
        throw last ?: IllegalStateException("The plug could not be reached.")
    }

    @Synchronized
    override fun getStatus(): RelayState = runBlocking {
        mapRelay(connected().getStatusAsync().getBool(relayDpId))
    }

    @Synchronized
    override fun turnOn() = setRelay(true)

    @Synchronized
    override fun turnOff() = setRelay(false)

    private fun setRelay(on: Boolean) {
        runBlocking { connected().setDpAsync(relayDpId, on) }
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
