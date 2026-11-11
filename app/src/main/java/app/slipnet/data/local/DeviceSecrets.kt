package app.slipnet.data.local

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Local installation only. Export formats are encoded separately after an explicit user action. */
object DeviceSecrets {
    private const val ALIAS = "rovunet.device.secrets.v1"
    @Volatile private var cached: SecretKey? = null

    @Synchronized private fun key(create: Boolean): SecretKey {
        cached?.let { return it }
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        val stored = store.getKey(ALIAS, null) as? SecretKey
        val result = stored ?: run {
            check(create) { "Saved credential key unavailable" }
            KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").run {
                init(KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                    .setKeySize(256).setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
                generateKey()
            }
        }
        cached = result
        return result
    }

    private inline fun <T> withKey(create: Boolean, operation: (SecretKey) -> T): T {
        val handle = key(create)
        return try {
            operation(handle)
        } catch (error: Exception) {
            // A provider can report invalidation at init or doFinal. Retry with a fresh handle next time.
            synchronized(this) { if (cached === handle) cached = null }
            throw error
        }
    }

    fun seal(value: String, purpose: String): String {
        if (value.isEmpty()) return ""
        return withKey(create = true) { handle ->
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, handle)
            cipher.updateAAD(purpose.toByteArray(Charsets.UTF_8))
            check(cipher.iv.size == 12)
            "1." + Base64.encodeToString(cipher.iv + cipher.doFinal(value.toByteArray(Charsets.UTF_8)), Base64.NO_WRAP)
        }
    }

    fun open(value: String, purpose: String): String {
        if (value.isEmpty()) return ""
        require(value.startsWith("1.")) { "Unknown saved credential format" }
        val bytes = Base64.decode(value.substring(2), Base64.NO_WRAP)
        require(bytes.size >= 28) { "Invalid saved credential" }
        return withKey(create = false) { handle ->
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.DECRYPT_MODE, handle, GCMParameterSpec(128, bytes, 0, 12))
            cipher.updateAAD(purpose.toByteArray(Charsets.UTF_8))
            String(cipher.doFinal(bytes, 12, bytes.size - 12), Charsets.UTF_8)
        }
    }
}
