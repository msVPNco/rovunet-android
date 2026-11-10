package app.slipnet.domain.model

/** One small sample per visible cycle. A spike cannot force an elective handover. */
class LivePathHealth {
    data class State(val failed: Int, val slow: Int, val medianMs: Long?, val host: String) {
        val unstable: Boolean get() = failed >= 3 || slow >= 3
    }
    private val samples = ArrayDeque<Long>()
    private var failed = 0
    private var slow = 0
    private var host = ""
    private var lastAt = Long.MIN_VALUE
    fun add(ms: Long?, target: String, at: Long): State {
        if (target != host) { samples.clear(); failed = 0; slow = 0; host = target; lastAt = Long.MIN_VALUE }
        if (lastAt != Long.MIN_VALUE && at - lastAt > 90_000L) { samples.clear(); failed = 0; slow = 0 }
        if (lastAt != Long.MIN_VALUE && at - lastAt < 20_000L) return state()
        lastAt = at
        if (ms == null) { failed++; slow = 0; samples.clear() }
        else {
            failed = 0
            slow = if (ms >= SmartPathPolicy.SLOW_MS) slow + 1 else 0
            samples.addLast(ms)
            while (samples.size > 3) samples.removeFirst()
        }
        return state()
    }
    fun state() = State(failed, slow, if (samples.size == 3) samples.sorted()[1] else null, host)
}
