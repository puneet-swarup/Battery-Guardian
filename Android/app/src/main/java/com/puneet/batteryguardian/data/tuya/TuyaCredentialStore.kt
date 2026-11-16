package com.puneet.batteryguardian.data.tuya

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class TuyaCredentialStore(context: Context) {
 private val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

 companion object {
 private const val PREFS = "tuya_credentials"
 private const val KEY_CIPHERTEXT = "local_key_ct"
 private const val KEY_IV = "local_key_iv"
 private const val KEYSTORE = "AndroidKeyStore"
 private const val ALIAS = "battery_guardian_tuya"
 }

 fun hasKey(): Boolean = prefs.contains(KEY_CIPHERTEXT)

 fun save(localKey: String) {
 require(localKey.isNotBlank()) { "Local key must not be empty." }
 val cipher = Cipher.getInstance("AES/GCM/NoPadding")
 cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
 val ct = cipher.doFinal(localKey.trim().toByteArray(Charsets.UTF_8))
 prefs.edit().putString(KEY_CIPHERTEXT, Base64.encodeToString(ct, Base64.NO_WRAP)).putString(KEY_IV, Base64.encodeToString(cipher.iv, Base64.NO_WRAP)).apply()
 }

 fun load(): String? {
 val ctB64 = prefs.getString(KEY_CIPHERTEXT, null) ?: return null
 val ivB64 = prefs.getString(KEY_IV, null) ?: return null
 return try {
 val ct = Base64.decode(ctB64, Base64.NO_WRAP)
 val iv = Base64.decode(ivB64, Base64.NO_WRAP)
 val cipher = Cipher.getInstance("AES/GCM/NoPadding")
 cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(128, iv))
 String(cipher.doFinal(ct), Charsets.UTF_8)
 } catch (t: Throwable) {
 null
 }
 }

 fun clear() {
 prefs.edit().remove(KEY_CIPHERTEXT).remove(KEY_IV).apply()
 }

 private fun getOrCreateKey(): SecretKey {
 val ks = KeyStore.getInstance(KEYSTORE)
 ks.load(null)
 (ks.getEntry(ALIAS, null) as? KeyStore.SecretKeyEntry)?.let { return it.secretKey }
 val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
 generator.init(
 KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
 .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
 .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
 .build()
 )
 return generator.generateKey()
 }
}
