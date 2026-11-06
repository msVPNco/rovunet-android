package app.slipnet.tunnel

/** One service attempt owns this token. Invalidate before joining blocking startup work. */
class StartupCancellation(val diagnosticSessionId: Long = 0) {
    private val lock = Any()
    private var cancelled = false
    private val callbacks = linkedSetOf<() -> Unit>()

    fun register(close: () -> Unit): AutoCloseable {
        val accepted = synchronized(lock) {
            if (cancelled) false else { callbacks.add(close); true }
        }
        if (!accepted) close()
        return AutoCloseable { synchronized(lock) { callbacks.remove(close) } }
    }

    /** Marks cancellation synchronously; socket closure may then run off the UI thread. */
    fun requestCancel(): () -> Unit {
        val pending = synchronized(lock) {
            cancelled = true
            // Keep registrations until their owner detaches them: the coroutine carrying an
            // earlier close action may itself be cancelled before it reaches Dispatchers.IO.
            callbacks.toList()
        }
        return { pending.forEach { action -> runCatching(action) } }
    }
}
