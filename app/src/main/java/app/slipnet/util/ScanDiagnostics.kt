package app.slipnet.util

/** In-memory, bounded timings only. No resolver IPs, domains, keys, URLs or periodic disk writes. */
internal object ScanDiagnostics {
    private data class Run(val ordinal: Long, val started: Long, var work: Long? = null,
        var firstProbe: Long? = null, var firstResult: Long? = null, var ended: Long? = null,
        var probes: Int = 0, var nativePeak: Int = 0)
    private val runs = linkedMapOf<String, Run>()
    private var current: String? = null
    private var ordinal = 0L
    private var activeNative = 0
    private fun now() = System.nanoTime() / 1_000_000
    @Synchronized fun begin(id: String) {
        if (runs.size >= 4) runs.remove(runs.keys.first())
        runs[id] = Run(++ordinal, now())
        current = id
    }
    @Synchronized fun working(id: String) { runs[id]?.let { if (it.work == null) it.work = now() } }
    @Synchronized fun probe() { runs[current]?.let { if (it.firstProbe == null) it.firstProbe = now(); it.probes++ } }
    @Synchronized fun result() { runs[current]?.let { if (it.firstResult == null) it.firstResult = now() } }
    @Synchronized fun finish(id: String) { runs[id]?.let { if (it.ended == null) it.ended = now() } }
    @Synchronized fun nativeStarted() {
        activeNative++
        runs[current]?.let { it.nativePeak = maxOf(it.nativePeak, activeNative) }
    }
    @Synchronized fun nativeFinished() { activeNative = (activeNative - 1).coerceAtLeast(0) }
    @Synchronized fun activeNativeCount() = activeNative
    @Synchronized fun report(): String = buildString {
        if (runs.isNotEmpty()) appendLine("\nSCANNER_TIMINGS active_native=$activeNative")
        runs.values.forEach { run ->
            fun offset(at: Long?) = at?.let { (it - run.started).coerceAtLeast(0) }?.toString() ?: "pending"
            appendLine("SCAN ${run.ordinal} total_ms=${offset(run.ended ?: now())} cleanup_wait_ms=${offset(run.work)} first_probe_ms=${offset(run.firstProbe)} first_result_ms=${offset(run.firstResult)} probes=${run.probes} native_peak=${run.nativePeak}")
        }
    }
}
