package app.slipnet.core

import java.security.MessageDigest
import java.security.SecureRandom

/** Legacy per-profile lock format. Encrypted bundles use BundleCrypto's password KDF. */
object LockHash {
    fun isValid(value: String) = value.matches(Regex("[0-9a-fA-F]{32}:[0-9a-fA-F]{64}"))
    fun create(password: String): String {
        val salt = ByteArray(16).also { SecureRandom().nextBytes(it) }
        val digest = MessageDigest.getInstance("SHA-256").apply { update(salt) }.digest(password.toByteArray(Charsets.UTF_8))
        return salt.joinToString("") { "%02x".format(it) } + ":" + digest.joinToString("") { "%02x".format(it) }
    }
    fun verify(password: String, value: String): Boolean {
        if (!isValid(value)) return false
        val parts = value.split(":")
        val salt = parts[0].chunked(2).map { it.toInt(16).toByte() }.toByteArray()
        val expected = parts[1].chunked(2).map { it.toInt(16).toByte() }.toByteArray()
        val actual = MessageDigest.getInstance("SHA-256").apply { update(salt) }.digest(password.toByteArray(Charsets.UTF_8))
        return MessageDigest.isEqual(actual, expected)
    }
}
