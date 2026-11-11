package app.slipnet.util

import java.security.MessageDigest
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

object LockPasswordUtil {

    init {
        System.loadLibrary("slipstream")
    }

    private external fun nativeGetConfigKey(): ByteArray

    private const val FORMAT_VERSION: Byte = 0x01
    private const val IV_LENGTH = 12
    private const val GCM_TAG_BITS = 128

    private val aesKey: SecretKeySpec by lazy {
        val keyBytes = nativeGetConfigKey()
        SecretKeySpec(keyBytes, "AES")
    }

    fun hashPassword(password: String): String = app.slipnet.core.LockHash.create(password)
    fun isValidHash(storedHash: String): Boolean = app.slipnet.core.LockHash.isValid(storedHash)
    fun verifyPassword(password: String, storedHash: String): Boolean = app.slipnet.core.LockHash.verify(password, storedHash)

    fun encryptConfig(plaintext: String): ByteArray {
        val iv = ByteArray(IV_LENGTH).also { SecureRandom().nextBytes(it) }

        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, aesKey, GCMParameterSpec(GCM_TAG_BITS, iv))
        val ciphertext = cipher.doFinal(plaintext.toByteArray(Charsets.UTF_8))

        // [version(1)] [iv(12)] [ciphertext+tag(variable)]
        val result = ByteArray(1 + IV_LENGTH + ciphertext.size)
        result[0] = FORMAT_VERSION
        System.arraycopy(iv, 0, result, 1, IV_LENGTH)
        System.arraycopy(ciphertext, 0, result, 1 + IV_LENGTH, ciphertext.size)
        return result
    }

    fun decryptConfig(data: ByteArray): String {
        if (data.isEmpty() || data[0] != FORMAT_VERSION) {
            throw IllegalArgumentException("Unsupported encrypted format version")
        }

        val minLength = 1 + IV_LENGTH + GCM_TAG_BITS / 8
        if (data.size < minLength) {
            throw IllegalArgumentException("Encrypted data too short")
        }

        val iv = data.copyOfRange(1, 1 + IV_LENGTH)
        val ciphertext = data.copyOfRange(1 + IV_LENGTH, data.size)

        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, aesKey, GCMParameterSpec(GCM_TAG_BITS, iv))
        val plainBytes = cipher.doFinal(ciphertext)
        return String(plainBytes, Charsets.UTF_8)
    }
}
