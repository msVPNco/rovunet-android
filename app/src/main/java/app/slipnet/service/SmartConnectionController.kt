package app.slipnet.service

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.PowerManager
import android.os.SystemClock
import app.slipnet.R
import app.slipnet.core.TunnelProbe
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.*
import app.slipnet.domain.repository.ProfileRepository
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*

/** Explicit collection opt-in. Every manual manager command cancels this owner. */
@Singleton
class SmartConnectionController @Inject constructor(
    @ApplicationContext private val context: Context,
    private val connection: VpnConnectionManager,
    private val repository: ProfileRepository,
    private val preferences: PreferencesDataStore,
    private val probe: StandaloneCoreProbe
) {
    data class Status(val running: Boolean = false, val message: Int = 0, val index: Int = 0, val total: Int = 0)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val store = context.getSharedPreferences("rovunet_smart_paths", Context.MODE_PRIVATE)
    private val network = context.getSystemService(ConnectivityManager::class.java)
    private val power = context.getSystemService(PowerManager::class.java)
    private var job: Job? = null
    private var generation = 0L
    private val cursor = mutableMapOf<String, Int>()
    private val _status = MutableStateFlow(Status())
    val status: StateFlow<Status> = _status.asStateFlow()
    private val _results = MutableStateFlow<Map<Long, PingResult>>(emptyMap())
    val results: StateFlow<Map<Long, PingResult>> = _results.asStateFlow()
    init { connection.cancelAutomatic = ::cancel }
    fun mode(key: String): SmartPathPolicy.Mode = SmartPathPolicy.Mode.entries.getOrElse(store.getInt(key, 0)) { SmartPathPolicy.Mode.MANUAL }
    fun setMode(key: String, mode: SmartPathPolicy.Mode) { cancel(); store.edit().putInt(key, mode.ordinal).apply() }
    fun cancel() { generation++; job?.cancel(); job = null; _status.value = Status() }
    fun clearResults() { _results.value = emptyMap() }
    private fun physicalNetwork(): String? {
        val active = network?.activeNetwork ?: return null
        val caps = network.getNetworkCapabilities(active) ?: return null
        return active.toString().takeIf { caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN) &&
            caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) }
    }
    private suspend fun members(key: String) = repository.getAllProfiles().first().filter {
        ProfileCollections.key(it) == key && !it.needsCredentials && !it.isExpired &&
            (it.boundDeviceId.isEmpty() || it.boundDeviceId == connection.getDeviceId()) && it.tunnelType != TunnelType.DOH
    }.sortedByDescending { it.lastConnectedAt }
    private fun record(profile: ServerProfile, value: Long?) {
        _results.value = _results.value + (profile.id to if (value == null) PingResult.Error("HTTPS_FAILED")
            else PingResult.Success(value, verified = true, successes = 3, attempts = 3))
    }
    private data class LivePing(val ms: Long, val host: String)
    private suspend fun live(profile: ServerProfile, session: Long): LivePing? {
        val endpoint = if (profile.tunnelType == TunnelType.XRAY) app.slipnet.tunnel.XrayBridge.activeEndpoint else preferences.configuredSocksEndpoint()
        if (endpoint == null) return null
        var target = TunnelProbe.HOST
        val measured = TunnelProbe.medianVerified({
            val state = connection.connectionState.value as? ConnectionState.Connected
            connection.ownsSession(session) && state?.profile?.id == profile.id && state.chainId <= 0L
        }) { host -> target = host; TunnelProbe.measureCancellable(endpoint.port, endpoint.user, endpoint.password, host, 8_000) }
        return measured?.let { LivePing(it, target) }
    }

    private suspend fun disconnectOwned(session: Long) {
        if (session <= 0 || !connection.ownsSession(session)) return
        connection.disconnect(expectedSessionId = session, automatic = true)
        withTimeout(25_000) { connection.awaitSessionDisconnected(session) }
        connection.finishProbe(session)
    }
    private suspend fun establish(profile: ServerProfile): Long {
        val id = connection.connect(profile, allowTrustPrompt = false, automatic = true)
        if (id <= 0) return 0
        var ready = false
        try {
            val state = withTimeoutOrNull(ProbePolicy.forProfile(profile).setupMs) {
                connection.connectionState.first { it is ConnectionState.Connected || it is ConnectionState.Error || it is ConnectionState.Disconnected }
            }
            ready = state is ConnectionState.Connected && state.profile.id == profile.id && connection.ownsSession(id)
            return if (ready) id else 0
        } finally { if (!ready) withContext(NonCancellable) { disconnectOwned(id) } }
    }

    fun start(key: String) {
        val selectedMode = mode(key)
        if (selectedMode == SmartPathPolicy.Mode.MANUAL || !connection.connectionState.value.isDisconnected) return
        cancel()
        val owner = generation
        job = scope.launch {
            var ownedSession = 0L
            var established = false
            try {
                var current: ServerProfile? = null
                val list = members(key)
                val offset = (cursor[key] ?: 0).coerceAtMost((list.size - 1).coerceAtLeast(0))
                val candidates = (list.drop(offset) + list.take(offset)).take(SmartPathPolicy.MAX_CANDIDATES)
                cursor[key] = if (list.isEmpty()) 0 else (offset + candidates.size) % list.size
                for ((index, candidate) in candidates.withIndex()) {
                    ensureActive()
                    if (owner != generation) return@launch
                    if (physicalNetwork() == null) { _status.value = Status(message = R.string.alpha17_offline); return@launch }
                    _status.value = Status(true, R.string.alpha17_trying, index + 1, candidates.size)
                    val profile = repository.getProfileById(candidate.id) ?: continue
                    ownedSession = establish(profile)
                    if (ownedSession == 0L) continue
                    val stamp = physicalNetwork()
                    val delay = live(profile, ownedSession)
                    if (stamp == null || stamp != physicalNetwork()) { disconnectOwned(ownedSession); ownedSession = 0; continue }
                    record(profile, delay?.ms)
                    if (delay != null) { current = profile; established = true; break }
                    disconnectOwned(ownedSession); ownedSession = 0
                }
                if (current == null) { _status.value = Status(message = R.string.alpha17_exhausted); return@launch }
                var switchedAt = SystemClock.elapsedRealtime()
                while (owner == generation && connection.ownsSession(ownedSession)) {
                    _status.value = Status(true, R.string.alpha17_monitoring)
                    // A real service failure wakes this immediately. Screen-off idle causes no extra probes.
                    val failed = withTimeoutOrNull(SmartPathPolicy.CYCLE_MS) {
                        connection.connectionState.first { it is ConnectionState.Error || it is ConnectionState.Disconnected }
                    }
                    if (owner != generation || !connection.ownsSession(ownedSession)) break
                    if (physicalNetwork() == null) { _status.value = Status(true, R.string.alpha17_offline); delay(10_000); continue }
                    if (failed == null && power?.isInteractive == false) continue
                    val stamp = physicalNetwork()
                    val active = current ?: break
                    var currentPing = if (failed == null) live(active, ownedSession) else null
                    if (currentPing == null && failed == null) { delay(3_000); currentPing = live(active, ownedSession) }
                    val currentDelay = currentPing?.ms
                    if (stamp != physicalNetwork()) { clearResults(); continue }
                    record(active, currentDelay)
                    if (currentDelay != null && (selectedMode == SmartPathPolicy.Mode.FAILOVER || currentDelay < SmartPathPolicy.SLOW_MS)) continue
                    val options = if (!ProbeBypass.allows(ownedSession)) emptyList() else members(key).filter { it.id != active.id && probe.supports(it) }
                    val start = (cursor[key] ?: 0).coerceAtMost((options.size - 1).coerceAtLeast(0))
                    val shortlist = (options.drop(start) + options.take(start)).take(SmartPathPolicy.MAX_CANDIDATES)
                    if (options.isNotEmpty()) cursor[key] = (start + shortlist.size) % options.size
                    var replacement: ServerProfile? = null
                    for (candidate in shortlist) {
                        val session = ownedSession
                        val result = probe.measure(candidate, currentPing?.host ?: TunnelProbe.HOST) {
                            owner == generation && connection.ownsSession(session) && stamp == physicalNetwork() &&
                                ProbeBypass.allows(session)
                        }
                        if (stamp != physicalNetwork()) { clearResults(); break }
                        record(candidate, result)
                        if (result != null && (currentDelay == null || SmartPathPolicy.better(currentDelay, result,
                                SystemClock.elapsedRealtime() - switchedAt))) { replacement = candidate; break }
                    }
                    if (replacement != null && owner == generation && stamp == physicalNetwork()) {
                        _status.value = Status(true, R.string.alpha17_switching)
                        disconnectOwned(ownedSession); ownedSession = 0
                        ownedSession = establish(replacement)
                        if (ownedSession == 0L) { _status.value = Status(message = R.string.alpha17_exhausted); break }
                        val confirmed = live(replacement, ownedSession)
                        record(replacement, confirmed?.ms)
                        if (confirmed == null) {
                            disconnectOwned(ownedSession); ownedSession = 0
                            start(key)
                            return@launch
                        }
                        current = replacement; switchedAt = SystemClock.elapsedRealtime()
                    } else if (currentDelay == null) {
                        disconnectOwned(ownedSession); ownedSession = 0
                        start(key) // bounded fresh batch; ordinary native paths are tried only after the live path died
                        return@launch
                    }
                }
            } catch (e: CancellationException) { throw e
            } catch (_: Exception) { if (owner == generation) _status.value = Status(message = R.string.alpha17_exhausted)
            } finally {
                // Canceling during setup drains only our own unfinished session; never a later manual owner.
                if (!established) withContext(NonCancellable) { disconnectOwned(ownedSession) }
                if (owner == generation) _status.value = _status.value.copy(running = false)
            }
        }
    }
}
