package app.slipnet.tunnel

import java.net.InetAddress

/** Failed LAN logins only. Successful connections and the local TUN are not limited to five flows/minute. */
class ProxyAuthLimiter(
    private val onCapacityLimit: () -> Unit = {},
    private val clockNanos: () -> Long = System::nanoTime
) {
    private data class Window(val start: Long, var failures: Int = 0, var pending: Int = 0)
    // Do not evict live peers: otherwise cycling addresses could reset their failure budgets.
    private val windows = HashMap<String, Window>()
    private var lastCapacityWarningNanos: Long? = null

    fun check(peer: InetAddress?, blocked: () -> Unit = {}, authenticate: () -> Boolean): Boolean {
        if (peer == null) return authenticate()
        val normalized = normalize(peer)
        if (normalized.isLoopbackAddress) return authenticate()
        val address = normalized.hostAddress ?: return false
        var warnCapacity = false
        val window = synchronized(windows) {
            val now = clockNanos()
            windows.entries.removeAll { now - it.value.start >= WINDOW_NANOS && it.value.pending == 0 }
            val current = windows[address] ?: if (windows.size < MAX_PEERS) {
                Window(now).also { windows[address] = it }
            } else {
                val previous = lastCapacityWarningNanos
                if (previous == null || now - previous >= WINDOW_NANOS) {
                    lastCapacityWarningNanos = now
                    warnCapacity = true
                }
                null
            }
            current?.takeIf { it.failures + it.pending < MAX_FAILURES }?.also { it.pending++ }
        }
        // Logging is outside the authentication lock and never contains an address or a credential.
        if (warnCapacity) onCapacityLimit()
        if (window == null) { blocked(); return false }
        var accepted = false
        try {
            accepted = authenticate()
            return accepted
        } finally {
            synchronized(windows) {
                window.pending--
                if (accepted) window.failures = 0 else window.failures++
            }
        }
    }

    private fun normalize(peer: InetAddress): InetAddress {
        val bytes = peer.address
        val mapped = bytes.size == 16 && (0 until 10).all { bytes[it] == 0.toByte() } &&
            bytes[10] == 0xff.toByte() && bytes[11] == 0xff.toByte()
        // getByAddress performs no DNS lookup. Explicit Inet6Address instances may retain this form.
        return if (mapped) InetAddress.getByAddress(bytes.copyOfRange(12, 16)) else peer
    }

    companion object {
        private const val WINDOW_NANOS = 60_000_000_000L
        private const val MAX_PEERS = 512
        private const val MAX_FAILURES = 5
        val shared = ProxyAuthLimiter(onCapacityLimit = {
            app.slipnet.util.AppLog.w("ProxyAuthLimiter",
                "LAN authentication tracking is at capacity; new peers are temporarily refused")
        })
    }
}
