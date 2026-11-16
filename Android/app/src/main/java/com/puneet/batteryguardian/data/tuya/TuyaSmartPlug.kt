package com.puneet.batteryguardian.data.tuya

import org.json.JSONObject

enum class RelayState { UNKNOWN, ON, OFF }

interface SmartPlug {
 fun getStatus(): RelayState
 fun turnOn()
 fun turnOff()
 fun testConnection(): Boolean
 fun close()
}

class TuyaSmartPlug(
 private val host: String,
 private val port: Int,
 private val localKey: ByteArray,
 private val relayDp: Int,
 private val timeoutMs: Int = 5000
) : SmartPlug {
 private var session: TuyaSession? = null

 @Synchronized
 private fun ensureConnected() {
 val s = session
 if (s != null && s.isConnected) return
 s?.close()
 val ns = TuyaSession(host, port, localKey, timeoutMs)
 ns.connect()
 session = ns
 }

 @Synchronized
 override fun getStatus(): RelayState {
 ensureConnected()
 val resp = session!!.sendAndReceive(TuyaSession.CMD_STATUS, ByteArray(0))
 return parseRelay(resp)
 }

 @Synchronized
 override fun turnOn() = setRelay(true)

 @Synchronized
 override fun turnOff() = setRelay(false)

 private fun setRelay(on: Boolean) {
 ensureConnected()
 val json = buildControlJson(relayDp, on)
 session!!.sendAndReceive(TuyaSession.CMD_CONTROL, json.toByteArray(Charsets.UTF_8))
 }

 override fun testConnection(): Boolean {
 return try {
 getStatus()
 true
 } catch (t: Throwable) {
 false
 }
 }

 private fun parseRelay(resp: ByteArray): RelayState {
 if (resp.isEmpty()) return RelayState.UNKNOWN
 return try {
 val obj = JSONObject(String(resp, Charsets.UTF_8))
 val dps = obj.optJSONObject("dps") ?: return RelayState.UNKNOWN
 if (!dps.has(relayDp.toString())) return RelayState.UNKNOWN
 when (val v = dps.get(relayDp.toString())) {
 is Boolean -> if (v) RelayState.ON else RelayState.OFF
 is String -> if (v.equals("true", ignoreCase = true)) RelayState.ON else RelayState.OFF
 else -> RelayState.UNKNOWN
 }
 } catch (t: Throwable) {
 RelayState.UNKNOWN
 }
 }

 @Synchronized
 override fun close() {
 session?.close()
 session = null
 }

 companion object {
 fun buildControlJson(dp: Int, on: Boolean): String {
 val q = (34).toChar()
 val s = q.toString()
 val value = if (on) "true" else "false"
 return s + "dps" + s + ":" + s + "{" + s + dp + s + ":" + value + s + "}" + s
 }
 }
}
