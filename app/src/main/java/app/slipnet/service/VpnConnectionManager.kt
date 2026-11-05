package app.slipnet.service

import android.content.Context
import android.content.Intent
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.repository.VpnRepositoryImpl
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.ProfileChain
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TrafficStats
import app.slipnet.domain.repository.ChainRepository
import app.slipnet.domain.repository.ProfileRepository
import app.slipnet.widget.VpnWidgetCompactProvider
import app.slipnet.widget.VpnWidgetProvider
import app.slipnet.util.DeviceIdUtil
import androidx.core.content.ContextCompat
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class VpnConnectionManager @Inject constructor(
    @ApplicationContext private val context: Context,
    private val vpnRepository: VpnRepositoryImpl,
    private val profileRepository: ProfileRepository,
    private val chainRepository: ChainRepository,
    private val preferencesDataStore: PreferencesDataStore
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val startupWatchdog = app.slipnet.domain.model.AttemptWatchdog(scope)
    private val abortedStarts = java.util.concurrent.ConcurrentHashMap.newKeySet<Long>()

    private fun watchStartup(id: Long) {
        val budget = when (pendingProfile?.tunnelType?.name) {
            "SNOWFLAKE", null -> 180_000L
            "DNSTT", "DNSTT_SSH", "NOIZDNS", "NOIZDNS_SSH", "VAYDNS", "VAYDNS_SSH", "SLIPSTREAM", "SLIPSTREAM_SSH", "SOAR", "SOAR_SSH" -> 120_000L
            else -> if (pendingChain != null) 180_000L else 90_000L
        }
        startupWatchdog.arm(budget,
            stillStarting = { ownsSession(id) && _connectionState.value is ConnectionState.Connecting },
            expire = {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_TIMEOUT)
                abortedStarts.add(id)
                disconnect(expectedSessionId = id)
                onVpnError("Connection setup timed out; see connection diagnostics", id)
            })
    }

    private val _connectionState = MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
    val connectionState: StateFlow<ConnectionState> = _connectionState.asStateFlow()

    private val _dnsWarning = MutableStateFlow<String?>(null)
    val dnsWarning: StateFlow<String?> = _dnsWarning.asStateFlow()

    val trafficStats: StateFlow<TrafficStats> = vpnRepository.trafficStats

    /** Distilled DNS-pool scan progress (per-profile pool feature). */
    val dnsPoolScanState: StateFlow<app.slipnet.tunnel.DnsPoolScanState> = vpnRepository.dnsPoolScanState

    private var dispatchedSessionId: Long = 0
    private val sessions = app.slipnet.domain.model.ConnectionSessions()
    val currentSessionId: Long get() = sessions.current
    fun ownsSession(id: Long) = sessions.owns(id)
    fun acceptsStart(id: Long) = ownsSession(id) && id !in abortedStarts
    fun onServiceStarting(id: Long) {
        if (!acceptsStart(id)) return
        _connectionState.value = ConnectionState.Connecting
        watchStartup(id)
    }
    private val stopping = java.util.concurrent.ConcurrentHashMap<Long, kotlinx.coroutines.CompletableDeferred<Unit>>()
    private val cleanedSession = MutableStateFlow(0L)
    private var destroyedUpTo = 0L
    private var stopBarrier = kotlinx.coroutines.CompletableDeferred(Unit)
    fun expectServiceStop(id: Long) {
        if (id <= destroyedUpTo) return
        val barrier = stopping.getOrPut(id) { kotlinx.coroutines.CompletableDeferred() }
        stopBarrier = barrier
    }
    fun onServiceDestroyed(id: Long) {
        scope.launch {
            destroyedUpTo = maxOf(destroyedUpTo, id)
            if (dispatchedSessionId == id) dispatchedSessionId = 0
            stopping.remove(id)?.complete(Unit)
            if (ownsSession(id)) onVpnDisconnected(id)
        }
    }
    fun claimServiceStart(): Long = sessions.begin().also { dispatchedSessionId = it; _connectionState.value = ConnectionState.Connecting; watchStartup(it) }
    suspend fun awaitSessionDisconnected(id: Long) {
        cleanedSession.first { it >= id || !ownsSession(id) }
    }
    private fun dispatchConnect(intent: Intent, id: Long) {
        val barrier = stopBarrier
        fun sendIfCurrent() {
            if (!ownsSession(id) || id in abortedStarts || _connectionState.value !is ConnectionState.Connecting) return
            dispatchedSessionId = id
            try { ContextCompat.startForegroundService(context, intent) }
            catch (_: Exception) {
                dispatchedSessionId = 0
                cleanedSession.value = maxOf(cleanedSession.value, id)
                onVpnError("Android could not start the VPN service", id)
            }
        }
        // Queue only when an old service is actually being destroyed. Otherwise
        // dispatch now, preserving CONNECT/DISCONNECT order on the Android service.
        if (barrier.isCompleted) sendIfCurrent()
        else scope.launch {
            if (withTimeoutOrNull(10_000L) { barrier.await(); true } == true) sendIfCurrent()
            else if (ownsSession(id) && _connectionState.value is ConnectionState.Connecting) {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.SERVICE_STOP_TIMEOUT)
                // Keep the old barrier closed. Never run a second native owner
                // while a previous service is still stopping.
                abortedStarts.add(id)
                cleanedSession.value = maxOf(cleanedSession.value, id)
                pendingProfile = null
                onVpnError("Previous tunnel is still stopping; connection was not started", id)
            }
        }
    }

    private var pendingProfile: ServerProfile? = null
    private var pendingChain: ProfileChain? = null

    init {
        // Observe VPN repository state
        scope.launch {
            vpnRepository.sessionState.collect { event ->
                if (ownsSession(event.sessionId) &&
                    !(event.sessionId in abortedStarts && (event.state is ConnectionState.Connecting || event.state is ConnectionState.Connected))) {
                    _connectionState.value = event.state
                    if (event.state !is ConnectionState.Connecting) startupWatchdog.cancel()
                }
            }
        }

        // Push state changes to home screen widget
        scope.launch {
            _connectionState.collect { state ->
                VpnWidgetProvider.notifyStateChanged(context, state)
                VpnWidgetCompactProvider.notifyStateChanged(context, state)
            }
        }
    }

    fun getDeviceId(): String {
        return DeviceIdUtil.getScrambledDeviceId(context)
    }

    fun connect(profile: ServerProfile, profileResolvers: Boolean = false, testMode: Boolean = false): Long {
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.CONNECT_REQUEST)
        if (_connectionState.value is ConnectionState.Connected ||
            _connectionState.value is ConnectionState.Connecting ||
            _connectionState.value is ConnectionState.Disconnecting) {
            return 0
        }

        if (profile.isExpired) {
            _connectionState.value = ConnectionState.Error("This profile has expired")
            return 0
        }
        if (profile.boundDeviceId.isNotEmpty() && profile.boundDeviceId != getDeviceId()) {
            _connectionState.value = ConnectionState.Error("This profile is bound to a different device")
            return 0
        }

        val sessionId = sessions.begin()
        abortedStarts.removeAll { it < sessionId - 16 }
        pendingProfile = profile
        pendingChain = null
        _connectionState.value = ConnectionState.Connecting
        _dnsWarning.value = null
        watchStartup(sessionId)

        // Set active profile immediately so it shows on the main screen
        scope.launch {
            profileRepository.setActiveProfile(profile.id)
        }

        // Start VPN service
        val intent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, profile.id)
            putExtra(SlipNetVpnService.EXTRA_SESSION_ID, sessionId)
            putExtra("rovunet_test_mode", testMode)
            if (profileResolvers) {
                putExtra("rovunet_resolver_hosts", profile.resolvers.map { it.host }.toTypedArray())
                putExtra("rovunet_resolver_ports", profile.resolvers.map { it.port }.toIntArray())
                putExtra("rovunet_resolver_auth", profile.resolvers.map { it.authoritative }.toBooleanArray())
            }
        }
        dispatchConnect(intent, sessionId)
        return sessionId
    }

    fun reconnect(profile: ServerProfile) {
        if (_connectionState.value is ConnectionState.Disconnecting) return
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.RECONNECT_REQUEST)
        if (profile.isExpired) {
            _connectionState.value = ConnectionState.Error("This profile has expired")
            return
        }
        if (profile.boundDeviceId.isNotEmpty() && profile.boundDeviceId != getDeviceId()) {
            _connectionState.value = ConnectionState.Error("This profile is bound to a different device")
            return
        }

        val sessionId = sessions.begin()
        abortedStarts.removeAll { it < sessionId - 16 }
        pendingProfile = profile
        pendingChain = null
        _connectionState.value = ConnectionState.Connecting
        _dnsWarning.value = null
        watchStartup(sessionId)

        scope.launch {
            profileRepository.setActiveProfile(profile.id)
        }

        // Send CONNECT directly — the service handles stopping the old connection
        // (disconnectJob?.join()) before starting the new one.
        val intent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_SESSION_ID, sessionId)
            putExtra(SlipNetVpnService.EXTRA_PROFILE_ID, profile.id)

        }
        dispatchConnect(intent, sessionId)
    }

    fun connectChain(chain: ProfileChain, firstProfile: ServerProfile) {
        if (_connectionState.value is ConnectionState.Connected ||
            _connectionState.value is ConnectionState.Connecting ||
            _connectionState.value is ConnectionState.Disconnecting) {
            return
        }

        val sessionId = sessions.begin()
        abortedStarts.removeAll { it < sessionId - 16 }
        pendingProfile = firstProfile
        pendingChain = chain
        _connectionState.value = ConnectionState.Connecting
        _dnsWarning.value = null
        watchStartup(sessionId)

        val intent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_CONNECT
            putExtra(SlipNetVpnService.EXTRA_SESSION_ID, sessionId)
            putExtra(SlipNetVpnService.EXTRA_CHAIN_ID, chain.id)
        }
        dispatchConnect(intent, sessionId)
    }

    fun disconnect(keepServiceAlive: Boolean = false, expectedSessionId: Long = currentSessionId) {
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.DISCONNECT_REQUEST)
        if (!ownsSession(expectedSessionId)) return
        startupWatchdog.cancel()
        if (dispatchedSessionId != expectedSessionId) {
            // A queued request owns no Android service yet. Cancel the queue
            // without replacing the old instance's destruction barrier.
            _connectionState.value = ConnectionState.Disconnected
            _dnsWarning.value = null
            pendingProfile = null
            pendingChain = null
            cleanedSession.value = maxOf(cleanedSession.value, expectedSessionId)
            return
        }
        if (keepServiceAlive && _connectionState.value is ConnectionState.Disconnected) return
        if (!keepServiceAlive) expectServiceStop(expectedSessionId)
        _connectionState.value = ConnectionState.Disconnecting

        val intent = Intent(context, SlipNetVpnService::class.java).apply {
            action = SlipNetVpnService.ACTION_DISCONNECT
            putExtra(SlipNetVpnService.EXTRA_SESSION_ID, expectedSessionId)
            putExtra(SlipNetVpnService.EXTRA_KEEP_SERVICE, keepServiceAlive)
        }
        try { context.startService(intent) }
        catch (_: Exception) { onVpnError("Android could not stop the VPN service", expectedSessionId) }
    }

    fun onVpnEstablished(sessionId: Long) {
        if (!ownsSession(sessionId)) return
        if (sessionId in abortedStarts) return
        startupWatchdog.cancel()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.VPN_ESTABLISHED)
        val profile = pendingProfile ?: return
        // The service reports readiness after the actual protocol and local
        // VPN/proxy are ready. No ping or HTTPS request gates this transition.
        val current = vpnRepository.connectionState.value
        val established = if (current is ConnectionState.Connected && current.profile.id == profile.id) current
            else ConnectionState.Connected(profile)
        vpnRepository.updateConnectionState(established)
        _connectionState.value = established

        // Tunnels are already started by SlipNetVpnService before calling this method.
        // Just do bookkeeping here - save the profile as last connected.
        scope.launch {
            preferencesDataStore.setLastConnectedProfileId(profile.id)
            profileRepository.updateLastConnectedAt(profile.id)
        }
    }

    fun onVpnDisconnected(sessionId: Long) {
        cleanedSession.value = maxOf(cleanedSession.value, sessionId)
        if (!ownsSession(sessionId)) return
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.VPN_DISCONNECTED)
        // Reset repository state without going through the full disconnect flow
        // (which would redundantly stop tunnels and emit Disconnecting state that
        // can race with a new Connecting state if the user reconnects quickly).
        //
        // Preserve an Error state from onVpnError (or a direct push like the
        // DNS pool exhaustion path) — the service teardown immediately follows
        // any connect-time failure, and blanket-resetting here would replace a
        // useful error message with a blank "Not Connected" before the user
        // gets to read it.
        val current = _connectionState.value
        if (current !is ConnectionState.Error) {
            vpnRepository.updateConnectionState(ConnectionState.Disconnected)
            _connectionState.value = ConnectionState.Disconnected
        }
        _dnsWarning.value = null
        pendingProfile = null
    }

    fun onVpnError(error: String, sessionId: Long) {
        if (!ownsSession(sessionId)) return
        startupWatchdog.cancel()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.VPN_ERROR)
        _connectionState.value = ConnectionState.Error(friendlyError(error))
    }

    private fun friendlyError(raw: String): String {
        // Map raw Java/Go exception messages to user-friendly text
        val lower = raw.lowercase()
        return when {
            // Timeouts (Java + Go)
            lower.contains("i/o timeout") || lower.contains("dial tcp") && lower.contains("timeout") ->
                "DNS tunnel timed out — server may be unreachable or blocked"
            lower.contains("context deadline exceeded") ->
                "Connection timed out — server took too long to respond"
            lower.contains("sockettimeoutexception") || lower.contains("read timed out") || lower.contains("connect timed out") ->
                "Connection timed out — server may be unreachable or blocked"
            // Connection errors
            lower.contains("connectionexception") || lower.contains("connection refused") ->
                "Connection refused — server may be down"
            lower.contains("unknownhostexception") || lower.contains("unable to resolve host") || lower.contains("no such host") ->
                "DNS lookup failed — check your internet connection"
            lower.contains("network is unreachable") || lower.contains("networkunreachable") || lower.contains("no route to host") ->
                "Network unreachable — check your internet connection"
            // TLS/SSL
            lower.contains("sslexception") || lower.contains("ssl handshake") || lower.contains("tls handshake") ->
                "Secure connection failed — TLS handshake error"
            // Resets & broken pipes
            lower.contains("econnreset") || lower.contains("connection reset") ->
                "Connection was reset — the server closed the connection"
            lower.contains("broken pipe") || lower.contains("eof") && lower.contains("unexpected") ->
                "Connection lost — the tunnel was interrupted"
            // Permission
            lower.contains("permission denied") ->
                "Permission denied — check VPN permissions"
            else -> raw
        }
    }

    fun setDnsWarning(message: String?) {
        _dnsWarning.value = message
    }

    suspend fun refreshTrafficStats() = kotlinx.coroutines.withContext(Dispatchers.IO) {
        vpnRepository.refreshTrafficStats()
    }

    suspend fun getProfileById(id: Long): ServerProfile? {
        return profileRepository.getProfileById(id)
    }

    suspend fun getActiveProfile(): ServerProfile? {
        return profileRepository.getActiveProfile().first()
    }

    suspend fun shouldAutoConnect(): Boolean {
        return preferencesDataStore.autoConnectOnBoot.first()
    }

    suspend fun getLastConnectedProfile(): ServerProfile? {
        val lastProfileId = preferencesDataStore.lastConnectedProfileId.first() ?: return null
        return profileRepository.getProfileById(lastProfileId)
    }
}
