package app.slipnet.util
// JNI-dependent per-profile locking is intentionally NOT tested by this JVM harness.
object LockPasswordUtil {
 fun isValidHash(value: String) = app.slipnet.core.LockHash.isValid(value)
 fun hashPassword(password: String): String = error("JNI locking unavailable in JVM tests")
 fun encryptConfig(data: String): ByteArray = error("JNI locking unavailable in JVM tests")
 fun decryptConfig(data: ByteArray): String = error("JNI locking unavailable in JVM tests")
}
