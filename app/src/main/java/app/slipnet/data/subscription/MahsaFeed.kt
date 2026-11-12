package app.slipnet.data.subscription

import app.slipnet.data.export.WireBase64
import java.nio.ByteBuffer
import java.nio.charset.CodingErrorAction
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import org.json.JSONObject

/** Codec for Mahsa's public app/sub.txt. Never follows JSON advertisements or other URLs. */
internal object MahsaFeed {
    const val MAX_BYTES = 2 * 1024 * 1024
    suspend fun decode(body: String): String {
        currentCoroutineContext().ensureActive()
        require(body.length <= MAX_BYTES && body.toByteArray(Charsets.UTF_8).size <= MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        val clear = try {
            val cipher = Cipher.getInstance("AES/CBC/PKCS5Padding") // AES block padding is PKCS7.
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec("ca815ecfdb1f153a".toByteArray(Charsets.US_ASCII), "AES"),
                IvParameterSpec("lvcas56410c97lpb".toByteArray(Charsets.US_ASCII)))
            cipher.doFinal(WireBase64.decode(body.trim()))
        } catch (_: Exception) { error("INVALID_FEED") }
        currentCoroutineContext().ensureActive()
        val text = try {
            Charsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(clear)).toString()
        } catch (_: Exception) { error("INVALID_FEED") }
        // Bound depth before JSONObject allocates a recursive object graph.
        var depth = 0; var quoted = false; var escaped = false
        for (c in text) {
            if (quoted) { if (escaped) escaped = false else if (c == '\\') escaped = true else if (c == '"') quoted = false }
            else when (c) { '"' -> quoted = true; '{', '[' -> { depth++; require(depth <= 16) { "INVALID_FEED" } }; '}', ']' -> { depth--; require(depth >= 0) { "INVALID_FEED" } } }
        }
        require(depth == 0 && !quoted) { "INVALID_FEED" }
        val root = try { JSONObject(text) } catch (_: Exception) { error("INVALID_FEED") }
        val links = linkedSetOf<String>()
        var count = 0
        for (network in listOf("mci", "mtn")) {
            val entries = root.optJSONArray(network) ?: error("INVALID_FEED")
            require(entries.length() <= PublicFeed.MAX_ENTRIES - count) { "PUBLIC_FEED_LIMIT" }
            count += entries.length()
            for (i in 0 until entries.length()) {
                currentCoroutineContext().ensureActive()
                val value = entries.optJSONObject(i)?.opt("config") as? String ?: error("INVALID_FEED")
                val link = value.trim()
                require(link.isNotEmpty() && link.length <= 65_536 && !link.contains('\r') && !link.contains('\n')) { "INVALID_FEED" }
                links += link
            }
        }
        require(links.isNotEmpty()) { "PUBLIC_FEED_EMPTY" }
        return links.joinToString("\n")
    }
}
