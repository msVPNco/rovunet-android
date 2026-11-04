package app.slipnet.util

/** Bounded, in-memory diagnostic events. Never accepts credentials or free-form errors. */
object ConnectionDiagnostics {
    enum class Event { CONNECT_REQUEST, RECONNECT_REQUEST, DISCONNECT_REQUEST, VPN_ESTABLISHED, VPN_DISCONNECTED, VPN_ERROR, CORE_START, CORE_READY, CORE_STOP }
    private val started = System.nanoTime()
    private val events = java.util.ArrayDeque<String>()
    @Synchronized fun record(event: Event) {
        if (events.size == 256) events.removeFirst()
        events.addLast("${(System.nanoTime() - started) / 1_000_000}ms ${event.name}")
    }
    @Synchronized fun report(): String = "Rovunet diagnostic events v1\nElapsed time since process initialization; no endpoint or credential data.\n" + events.joinToString("\n")
    @Synchronized fun clear() { events.clear() }
}
