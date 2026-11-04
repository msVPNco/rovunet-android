package app.slipnet.util
// JNI-dependent per-profile locking is intentionally NOT tested by this JVM harness.
object LockPasswordUtil {
 fun isValidHash(value: String) = value.matches(Regex("[0-9a-fA-F]{32}:[0-9a-fA-F]{64}"))
 fun hashPassword(password: String): String = error("JNI locking unavailable in JVM tests")
 fun encryptConfig(data: String): ByteArray = error("JNI locking unavailable in JVM tests")
 fun decryptConfig(data: ByteArray): String = error("JNI locking unavailable in JVM tests")
}
