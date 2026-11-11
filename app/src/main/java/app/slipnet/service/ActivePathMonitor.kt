package app.slipnet.service

import android.content.Context
import android.net.ConnectivityManager
import android.os.PowerManager
import android.os.SystemClock
import app.slipnet.core.TunnelProbe
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.*
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/** Shared by display and smart mode: no wake lock, no throughput downloads, no overlapping samples. */
@Singleton
class ActivePathMonitor @Inject constructor(
    @ApplicationContext context: Context,
    private val connection: VpnConnectionManager,
    private val preferences: PreferencesDataStore
) {
    data class Sample(val session: Long, val profile: Long, val route: String, val host: String,
        val ms: Long?, val elapsed: Long, val result: PingResult, val health: LivePathHealth.State)
    private val network = context.getSystemService(ConnectivityManager::class.java)
    private val power = context.getSystemService(PowerManager::class.java)
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val owners = mutableSetOf<Any>()
    private val paused = mutableSetOf<Any>()
    private val enabled = MutableStateFlow(false)
    private val _latest = MutableStateFlow<Sample?>(null)
    val latest = _latest.asStateFlow()
    val visible: Boolean get() = enabled.value && power?.isInteractive != false
    private val slot = Mutex()
    private var health = LivePathHealth()
    private var healthOwner: Pair<Long, String>? = null
    fun setVisible(owner: Any, value: Boolean) { if (value) owners.add(owner) else owners.remove(owner); refresh() }
    fun pause(owner: Any, value: Boolean) { if (value) paused.add(owner) else paused.remove(owner); refresh() }
    private fun refresh() { enabled.value = owners.isNotEmpty() && paused.isEmpty() }
    init {
        scope.launch {
            combine(connection.connectionState, enabled) { state, shown -> state to shown }.collectLatest { (state, shown) ->
                if (!shown || state !is ConnectionState.Connected || state.chainId > 0) return@collectLatest
                val session = connection.currentSessionId
                while (currentCoroutineContext().isActive && connection.ownsSession(session)) {
                    try { if (visible) measure(state.profile, session) }
                    catch (cancelled: CancellationException) { throw cancelled }
                    catch (_: Exception) { app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.LIVE_PROBE_FAILED, session) }
                    delay(SmartPathPolicy.CYCLE_MS)
                }
            }
        }
    }
    suspend fun measure(profile: ServerProfile, session: Long): Sample? = slot.withLock {
        val route = network?.let(PhysicalNetworkRoute::snapshot) ?: return@withLock null
        fun valid(): Boolean {
            val state = connection.connectionState.value as? ConnectionState.Connected
            return connection.ownsSession(session) && state?.profile?.id == profile.id && state.chainId <= 0 &&
                network?.let(PhysicalNetworkRoute::snapshot) == route
        }
        if (!valid()) return@withLock null
        val now = SystemClock.elapsedRealtime()
        _latest.value?.takeIf { it.session == session && it.profile == profile.id && it.route == route && now - it.elapsed < 20_000 }?.let { return@withLock it }
        val endpoint = if (profile.tunnelType == TunnelType.XRAY) app.slipnet.tunnel.XrayBridge.activeEndpoint else preferences.configuredSocksEndpoint()
        if (endpoint == null) return@withLock null
        var target = TunnelProbe.HOST
        val measurements = TunnelProbe.sample({}, successTarget = 1) { host ->
            if (!valid()) error("Probe ownership changed")
            target = host
            TunnelProbe.measureCancellable(endpoint.port, endpoint.user, endpoint.password, host, 5_000)
        }
        if (!valid()) return@withLock null
        val ms = measurements.summary?.medianMs
        val owner = session to route
        if (owner != healthOwner) { healthOwner = owner; health = LivePathHealth() }
        val elapsed = SystemClock.elapsedRealtime()
        val result = if (ms == null) PingResult.Error("HTTPS_FAILED") else PingResult.Success(ms, true, quick = true)
        val sample = Sample(session, profile.id, route, target, ms, elapsed, result, health.add(ms, target, elapsed))
        _latest.value = sample
        app.slipnet.util.ConnectionDiagnostics.record(if (ms == null) app.slipnet.util.ConnectionDiagnostics.Event.LIVE_PROBE_FAILED else app.slipnet.util.ConnectionDiagnostics.Event.LIVE_PROBE_OK, session)
        sample
    }
}
