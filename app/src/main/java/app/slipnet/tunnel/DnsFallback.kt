package app.slipnet.tunnel

/** At most one configured fallback, always through the same tunnel. NXDOMAIN is a valid answer. */
internal object DnsFallback {
    fun query(
        request: ByteArray,
        primary: String,
        fallback: String?,
        exchange: (String, ByteArray) -> ByteArray?
    ): ByteArray? {
        if (request.size < 12) return null
        var lastFailure: ByteArray? = null
        for (host in listOfNotNull(primary, fallback).map { it.trim() }.filter { it.isNotEmpty() }.distinct().take(2)) {
            val response = exchange(host, request) ?: continue
            if (response.size < 12 || response[0] != request[0] || response[1] != request[1] ||
                (response[2].toInt() and 0x80) == 0) continue
            when (response[3].toInt() and 0x0f) {
                2, 5 -> lastFailure = response // SERVFAIL / REFUSED may be resolver-specific.
                else -> return response // In particular, never turn NXDOMAIN into a network failure.
            }
        }
        return lastFailure
    }

    fun cacheable(response: ByteArray): Boolean = response.size >= 12 &&
        (response[3].toInt() and 0x0f) in setOf(0, 3)
}
