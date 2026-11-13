package app.slipnet.util

import java.io.File
import java.net.ConnectException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledFuture
import java.util.concurrent.TimeUnit

/** Safe diagnostics: typed stages/codes only. Never accepts endpoint, credential or free-form text. */
object ConnectionDiagnostics {
    enum class Event {
        COLLECTION_START, COLLECTION_INTERRUPTED, COLLECTION_NETWORK_CHANGED,
        LIVE_PROBE_OK, LIVE_PROBE_FAILED, PROBE_HTTPS_OK, PROBE_HTTPS_FAILED, SMART_CANDIDATE, SMART_SWITCH,
        XRAY_HYSTERIA2_START, XRAY_WIREGUARD_START, CORE_UPSTREAM_FAILED,
        AUXILIARY_MIERU_START, CORE_PROCESS_STARTED, CORE_CONFIG_WRITTEN,
        PROBE_SOCKS_REQUEST_WRITTEN, PROBE_HTTP_REQUEST_WRITTEN, PROBE_RESPONSE_FIRST_BYTE,
        SLIPSTREAM_START, SLIPSTREAM_PIN_CONFIGURED, SLIPSTREAM_UNPINNED,
        SLIPSTREAM_LISTENER_READY, SLIPSTREAM_QUIC_READY, SLIPSTREAM_QUIC_TIMEOUT,
        CONNECT_REQUEST, RECONNECT_REQUEST, DISCONNECT_REQUEST, VPN_ESTABLISHED, VPN_DISCONNECTED, VPN_ERROR,
        CORE_START, CORE_READY, CORE_STOP, SERVICE_CONNECT_RECEIVED, WAIT_PREVIOUS_SETUP, WAIT_PREVIOUS_RUNTIME,
        WAIT_PREVIOUS_TEARDOWN, WAIT_NATIVE_OWNER, NATIVE_CLEANUP, PROFILE_LOADED, TRANSPORT_START,
        STARTUP_TIMEOUT, SERVICE_STOP_TIMEOUT, STARTUP_CANCEL_REQUESTED, STARTUP_IO_CLOSED, STARTUP_WORKERS_STOPPED,
        TUN_CLOSED, SERVICE_RELEASED, SSH_START, SSH_CONNECTED, SSH_FAILED, SSH_CANCELLED,
        SOCKS_TCP_READY, SOCKS_GREETING_WAIT, SOCKS_GREETING_FAILED, SOCKS_GREETING, SOCKS_AUTH, SOCKS_CONNECT,
        DNS_TCP_READY, DNS_QUERY_SENT, DNS_CONNECT, DNS_PRIMARY, DNS_FALLBACK, DNS_ANSWER, DNS_FAILED,
        STOP_RUNTIME_WAIT, STOP_RUNTIME_DONE, STOP_LOCK_WAIT, STOP_LOCK_ACQUIRED,
        STOP_HEV_BEGIN, STOP_HEV_END, STOP_PROXY_BEGIN, STOP_PROXY_END, STOP_TUN_BEGIN
    }
    enum class Failure { NONE, CANCELLED, TIMEOUT, AUTH_REJECTED, REMOTE_CLOSED, CONNECTION_REFUSED, DNS_LOOKUP, TLS, OTHER }
    enum class NetworkKind { WIFI, CELLULAR, ETHERNET, OTHER }
    private data class Power(var changedAt: Long, var screenOn: Boolean, var cpu: Boolean, var wifi: Boolean,
        var continuous: Boolean, var cpuMs: Long = 0, var wifiMs: Long = 0, var screenOffMs: Long = 0)
    enum class SoarMode {
        AUTO, UDP_AND_TCP, TCP_ONLY, UDP_ONLY;

        companion object {
            /** Exact semantics of soar-mobile SetTCPMode, including its fallback for unknown values. */
            fun fromNative(value: String): SoarMode = when (value) {
                "on" -> UDP_AND_TCP; "only" -> TCP_ONLY; "off" -> UDP_ONLY; else -> AUTO
            }
        }
    }
    private data class SoarPolicy(val requested: SoarMode, val configured: SoarMode)
    private data class Entry(val at: Long, val event: Event, val failure: Failure, val code: Int?)
    private data class Flow(var first: Long, var last: Long, var count: Long)
    private data class Attempt(val id: Long, val started: Long, var transport: String = "UNKNOWN",
        val entries: ArrayDeque<Entry> = ArrayDeque(), val flows: LinkedHashMap<String, Flow> = linkedMapOf(),
        var sentBytes: Long? = null, var receivedBytes: Long? = null,
        var soarPolicy: SoarPolicy? = null, var trafficPersistedAt: Long = Long.MIN_VALUE,
        val startedUtc: Long = System.currentTimeMillis(), var sampledAt: Long? = null,
        var network: NetworkKind? = null, var power: Power? = null, var endedAt: Long? = null)
    private val lock = Any()
    private val started = System.nanoTime()
    private val attempts = linkedMapOf<Long, Attempt>()
    @Volatile private var active = 0L
    @Volatile private var previous = ""
    @Volatile private var destination: File? = null
    private val writer = Executors.newSingleThreadScheduledExecutor { task ->
        Thread(task, "connection-diagnostics").apply { isDaemon = true }
    }
    private val persistenceGeneration = java.util.concurrent.atomic.AtomicLong(0)
    private var pendingWrite: ScheduledFuture<*>? = null
    private fun now() = (System.nanoTime() - started) / 1_000_000
    fun currentSessionId(): Long = active

    fun network(sessionId: Long, kind: NetworkKind) {
        val changed = synchronized(lock) {
            val attempt = attempts[sessionId]?.takeIf { sessionId == active } ?: return
            (attempt.network != kind).also { attempt.network = kind }
        }
        if (changed) scheduleWrite()
    }

    /** Held/requested lock durations, not a battery percentage or proof of Wi-Fi low-latency activation. */
    fun power(sessionId: Long, screenOn: Boolean, cpu: Boolean, wifi: Boolean, continuous: Boolean) {
        val changed = synchronized(lock) {
            val attempt = attempts[sessionId]?.takeIf { sessionId == active } ?: return
            val at = now()
            val old = attempt.power
            if (old == null) { attempt.power = Power(at, screenOn, cpu, wifi, continuous); true }
            else {
                val span = (at - old.changedAt).coerceAtLeast(0)
                if (old.cpu) old.cpuMs += span
                if (old.wifi) old.wifiMs += span
                if (!old.screenOn) old.screenOffMs += span
                val changed = old.screenOn != screenOn || old.cpu != cpu || old.wifi != wifi || old.continuous != continuous
                old.changedAt = at
                old.screenOn = screenOn; old.cpu = cpu; old.wifi = wifi; old.continuous = continuous
                changed
            }
        }
        if (changed) scheduleWrite()
    }

    /** Configuration only: neither the selected network path nor proof of data passing. */
    fun soarPolicy(sessionId: Long, requested: SoarMode, configured: SoarMode) {
        synchronized(lock) {
            if (sessionId != active) return
            val attempt = attempts[sessionId] ?: return
            attempt.soarPolicy = SoarPolicy(requested, configured)
        }
        scheduleWrite()
    }

    /** Snapshots of the same counters shown by the UI, not a claim of verified internet access. */
    fun traffic(sessionId: Long, sent: Long, received: Long) {
        val persist = synchronized(lock) {
            if (sessionId != active) return
            val attempt = attempts[sessionId] ?: return
            val tx = maxOf(attempt.sentBytes ?: 0, sent.coerceAtLeast(0))
            val rx = maxOf(attempt.receivedBytes ?: 0, received.coerceAtLeast(0))
            val changed = tx != attempt.sentBytes || rx != attempt.receivedBytes
            attempt.sentBytes = tx
            attempt.receivedBytes = rx
            attempt.sampledAt = now()
            // Snapshots remain current in memory. Idle polling never rewrites the cache;
            // busy traffic is checkpointed at most once a minute, alongside immediate events.
            (changed && (attempt.trafficPersistedAt == Long.MIN_VALUE || now() - attempt.trafficPersistedAt >= 60_000))
                .also { if (it) attempt.trafficPersistedAt = now() }
        }
        if (persist) scheduleWrite()
    }

    fun begin(sessionId: Long, transport: String) {
        val safeTransport = transport.uppercase(java.util.Locale.ROOT).takeIf {
            it in setOf("SSH", "SOCKS5", "SOAR", "SOAR_SSH", "DNSTT", "DNSTT_SSH", "NOIZDNS", "NOIZDNS_SSH",
                "VAYDNS", "VAYDNS_SSH", "SLIPSTREAM", "SLIPSTREAM_SSH", "NAIVE", "NAIVE_SSH", "VLESS", "XRAY", "DOH", "SNOWFLAKE", "CHAIN")
        } ?: "UNKNOWN"
        synchronized(lock) {
            active = sessionId
            attempt(sessionId).transport = safeTransport
        }
        record(Event.CONNECT_REQUEST, sessionId)
    }

    private fun attempt(id: Long): Attempt = attempts.getOrPut(id) {
        if (attempts.size >= 8) attempts.remove(attempts.keys.first())
        Attempt(id, now())
    }

    fun record(event: Event, sessionId: Long = active, failure: Failure = Failure.NONE, code: Int? = null) {
        synchronized(lock) {
            if (sessionId == 0L && active > 0) return
            if (sessionId !in attempts && attempts.size >= 8 && sessionId < attempts.keys.min()) return
            val attempt = attempt(sessionId)
            if (attempt.entries.size >= 96) attempt.entries.removeFirst()
            attempt.entries.addLast(Entry(now(), event, failure, code?.coerceIn(0, 255)))
            if (event == Event.VPN_DISCONNECTED) attempt.endedAt = now()
        }
        scheduleWrite()
    }

    /** Aggregate traffic stages, so busy browsing cannot evict the startup/cancel timeline. */
    fun flow(event: Event, sessionId: Long, code: Int? = null, failure: Failure = Failure.NONE) {
        synchronized(lock) {
            if (sessionId == 0L && active > 0) return
            if (sessionId !in attempts && attempts.size >= 8 && sessionId < attempts.keys.min()) return
            val attempt = attempt(sessionId)
            val reply = code?.coerceIn(0, 255)
            val meaning = if (event == Event.SOCKS_CONNECT || event == Event.DNS_CONNECT) when(reply) {
                0 -> "CONNECT_ACCEPTED"; 1 -> "GENERAL_FAILURE"; 2 -> "POLICY_DENIED"; 3 -> "NETWORK_UNREACHABLE"
                4 -> "HOST_UNREACHABLE"; 5 -> "CONNECTION_REFUSED"; 6 -> "TTL_EXPIRED"; 7 -> "COMMAND_UNSUPPORTED"
                8 -> "ADDRESS_UNSUPPORTED"; else -> "UNKNOWN_REPLY"
            } else ""
            val key = event.name + (if (failure != Failure.NONE) ":${failure.name}" else "") +
                (reply?.let { ":$it" } ?: "") + (if (meaning.isNotEmpty() && reply != null) ":$meaning" else "")
            val at = now()
            attempt.flows[key]?.let { it.last = at; it.count++ } ?: run {
                if (attempt.flows.size < 32) attempt.flows[key] = Flow(at, at, 1)
            }
        }
        scheduleWrite()
    }

    fun failure(error: Throwable): Failure {
        val causes = generateSequence(error) { it.cause }.take(8).toList()
        if (causes.any { it is java.util.concurrent.CancellationException }) return Failure.CANCELLED
        if (causes.any { it is SocketTimeoutException }) return Failure.TIMEOUT
        if (causes.any { it is UnknownHostException }) return Failure.DNS_LOOKUP
        if (causes.any { it is ConnectException }) return Failure.CONNECTION_REFUSED
        if (causes.any { it is javax.net.ssl.SSLException }) return Failure.TLS
        // Only classify known markers; never retain or serialize their source messages.
        if (causes.any { it.message?.contains("Auth fail", ignoreCase = true) == true }) return Failure.AUTH_REJECTED
        if (causes.any { it is java.io.EOFException || it.message?.let { m ->
            m.contains("foreign host", true) || m.contains("End of IO Stream", true) } == true }) return Failure.REMOTE_CLOSED
        return Failure.OTHER
    }

    private fun currentReport(): String = synchronized(lock) {
        buildString {
            appendLine("Rovunet safe connection report v5")
            appendLine("Elapsed milliseconds; no server addresses, account names, keys or configuration links.")
            appendLine("REPORT_UTC ${java.time.Instant.ofEpochMilli(System.currentTimeMillis())}")
            attempts.values.forEach { attempt ->
                appendLine("\nATTEMPT ${attempt.id} transport=${attempt.transport}")
                appendLine("STARTED_UTC ${java.time.Instant.ofEpochMilli(attempt.startedUtc)}")
                attempt.network?.let { appendLine("DEFAULT_NETWORK $it") }
                attempt.power?.let { power ->
                    val span = ((attempt.endedAt ?: now()) - power.changedAt).coerceAtLeast(0)
                    appendLine("POWER cpu_lock_held_ms=${power.cpuMs + if (power.cpu) span else 0} wifi_lock_held_ms=${power.wifiMs + if (power.wifi) span else 0} screen_off_ms=${power.screenOffMs + if (!power.screenOn) span else 0} continuous_timing=${power.continuous}")
                }
                attempt.soarPolicy?.let {
                    appendLine("SOAR_POLICY requested=${it.requested} configured=${it.configured}")
                }
                attempt.entries.forEach { entry ->
                    append("+${(entry.at - attempt.started).coerceAtLeast(0)}ms ${entry.event}")
                    if (entry.failure != Failure.NONE) append(" result=${entry.failure}")
                    if (entry.code != null) append(" code=${entry.code}")
                    appendLine()
                }
                attempt.flows.forEach { (event, flow) ->
                    appendLine("FLOW $event count=${flow.count} first=+${(flow.first-attempt.started).coerceAtLeast(0)}ms last=+${(flow.last-attempt.started).coerceAtLeast(0)}ms")
                }
                if (attempt.sentBytes != null) appendLine("COUNTERS sent=${attempt.sentBytes} received=${attempt.receivedBytes} sampled=+${((attempt.sampledAt ?: attempt.started) - attempt.started).coerceAtLeast(0)}ms")
            }
        }
    }
    fun report(): String = currentReport() + ScanDiagnostics.report() + previous.takeIf { it.isNotEmpty() }?.let { "\nPREVIOUS PROCESS\n$it" }.orEmpty()

    /** Private, bounded cache. Disk I/O is never performed while holding the event lock or on Main. */
    fun enablePersistence(file: File) {
        destination = file
        val generation = persistenceGeneration.get()
        writer.execute {
            val loaded = runCatching {
                if (file.isFile && file.length() <= 131072) file.readText() else ""
            }.getOrDefault("")
            if (persistenceGeneration.get() == generation) previous = loaded
        }
    }
    @Synchronized private fun scheduleWrite() {
        val file = destination ?: return
        if (pendingWrite?.isDone == false) return
        pendingWrite = writer.schedule({
            synchronized(this) { pendingWrite = null }
            runCatching {
                val text = currentReport().take(131072)
                val temp = File(file.parentFile, file.name + ".tmp")
                temp.writeText(text)
                if (!temp.renameTo(file)) temp.delete()
            }
        }, 500, TimeUnit.MILLISECONDS)
    }
    fun clear() {
        persistenceGeneration.incrementAndGet()
        synchronized(lock) { attempts.clear(); previous = "" }
        scheduleWrite()
    }
}
