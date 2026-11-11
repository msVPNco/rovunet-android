package app.slipnet.data.export

/** Standard, unwrapped Base64 used by the portable profile format, including on API 24/25. */
internal object WireBase64 {
    fun encode(data: ByteArray): String = org.bouncycastle.util.encoders.Base64.toBase64String(data)

    fun decode(encoded: String): ByteArray {
        // Android's wire format permits omitted padding and ASCII whitespace.
        val compact = encoded.filterNot { it == ' ' || it == '\t' || it == '\r' || it == '\n' }
        val padded = if ('=' in compact) {
            require(compact.length % 4 == 0) { "Invalid Base64 padding" }
            compact
        } else {
            when (compact.length % 4) {
                0 -> compact
                2 -> "$compact=="
                3 -> "$compact="
                else -> throw IllegalArgumentException("Invalid Base64 length")
            }
        }
        return try {
            org.bouncycastle.util.encoders.Base64.decode(padded)
        } catch (e: org.bouncycastle.util.encoders.DecoderException) {
            throw IllegalArgumentException("Invalid Base64 data", e)
        }
    }
}
