package app.slipnet.data.export

import java.io.ByteArrayOutputStream
import java.util.zip.GZIPInputStream
import java.util.zip.GZIPOutputStream

/** Self-contained QR payload. No upload, URL shortener or external service. */
object CollectionLink {
    const val PREFIX = "rovunet-collection://"
    fun encode(text: String): String {
        require(text.toByteArray().size <= 2 * 1024 * 1024)
        val output = ByteArrayOutputStream()
        GZIPOutputStream(output).use { it.write(text.toByteArray(Charsets.UTF_8)) }
        return PREFIX + "1." + WireBase64.encode(output.toByteArray())
    }
    fun decode(text: String): String {
        require(text.startsWith(PREFIX + "1.")) { "Unsupported collection version" }
        val encoded = text.removePrefix(PREFIX + "1.")
        require(encoded.length <= 2 * 1024 * 1024)
        return GZIPInputStream(WireBase64.decode(encoded).inputStream()).use { input ->
            val out = ByteArrayOutputStream()
            val buffer = ByteArray(8192)
            while (true) {
                val n = input.read(buffer)
                if (n < 0) break
                require(out.size() + n <= 2 * 1024 * 1024) { "Collection exceeds 2 MiB" }
                out.write(buffer, 0, n)
            }
            out.toString("UTF-8")
        }
    }
}
