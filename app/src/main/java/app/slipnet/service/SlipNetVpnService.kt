package app.slipnet.service

import app.slipnet.domain.model.SAVED_CREDENTIALS_UNAVAILABLE

import android.app.NotificationManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.SharedPreferences
import android.net.ConnectivityManager
import android.net.LinkProperties
import android.net.Network
import android.net.NetworkCapabilities
import android.net.NetworkRequest
import android.net.ProxyInfo
import android.net.VpnService
import android.net.wifi.WifiManager
import android.os.Build
import app.slipnet.domain.model.VpnMtu
import app.slipnet.tunnel.EstablishedTun
import android.os.PowerManager
import app.slipnet.util.AppLog as Log
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.local.datastore.SplitTunnelingMode
import app.slipnet.data.local.datastore.SshCipher
import app.slipnet.tunnel.DomainRouter
import app.slipnet.tunnel.GeoBypassCountry
import app.slipnet.tunnel.GeoBypassData
import app.slipnet.data.repository.VpnRepositoryImpl
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.TunnelType
import app.slipnet.domain.model.isAvailable
import app.slipnet.tunnel.DnsttBridge
import app.slipnet.tunnel.DnsttSocksBridge
import app.slipnet.tunnel.VaydnsBridge
import app.slipnet.tunnel.DohBridge
import app.slipnet.tunnel.HevSocks5Tunnel
import app.slipnet.tunnel.HttpProxyServer
import app.slipnet.tunnel.NaiveBridge
import app.slipnet.tunnel.NaiveSocksBridge
import app.slipnet.tunnel.RateLimiter
import app.slipnet.tunnel.SlipstreamBridge
import app.slipnet.tunnel.SlipstreamSocksBridge
import app.slipnet.tunnel.SoarBridge
import app.slipnet.tunnel.Socks5ProxyBridge
import app.slipnet.tunnel.XrayBridge
import app.slipnet.tunnel.VlessBridge
import app.slipnet.tunnel.SnowflakeBridge
import app.slipnet.tunnel.SshTunnelBridge
import app.slipnet.tunnel.TorSocksBridge
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import javax.inject.Inject
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.cancelAndJoin

@AndroidEntryPoint
class SlipNetVpnService : VpnService() {

    companion object {
        private const val TAG = "SlipNetVpnService"
        const val EXTRA_SESSION_ID = "rovunet_session_id"
        const val EXTRA_KEEP_SERVICE = "rovunet_keep_service"
        const val ACTION_CONNECT = "app.slipnet.CONNECT"
        const val ACTION_DISCONNECT = "app.slipnet.DISCONNECT"
        const val ACTION_RECONNECT = "app.slipnet.RECONNECT"
        const val EXTRA_PROFILE_ID = "profile_id"
        const val EXTRA_CHAIN_ID = "chain_id"
        const val EXTRA_BOOT_TRIGGERED = "boot_triggered"

        private const val VPN_ADDRESS = "10.255.255.1"
        private const val VPN_ROUTE = "0.0.0.0"
        private const val DEFAULT_DNS = "8.8.8.8"
        private const val WAKELOCK_TIMEOUT_MS = 10 * 60 * 1000L  // 10 minutes (Chinese OEM ROMs kill indefinite WakeLocks)
        private const val WAKELOCK_RENEW_INTERVAL_MS = 9 * 60 * 1000L  // renew 1 min before expiry
        private const val HEALTH_CHECK_INTERVAL_MS = 15000L
        private const val QUIC_DOWN_THRESHOLD = 2 // Reconnect after 2 checks (~30s) with QUIC down
        private const val SSH_PROBE_INTERVAL = 2 // Probe SSH session every 2 health checks (~30s)
        private const val DNS_POOL_DEAD_THRESHOLD = 3 // Warn after 3 consecutive checks (~45s) with all workers dead
        private const val DNS_POOL_DEAD_THRESHOLD_SOCKS = 2 // Faster warning for SOCKS profiles (~30s)
        private const val TUNNEL_STALL_CHECK_INTERVAL = 4 // Check traffic flow every 4 health checks (~60s)
        private const val TUNNEL_STALL_CHECK_INTERVAL_SOCKS = 2 // Faster stall check for SOCKS profiles (~30s)
        private const val TUNNEL_STALL_THRESHOLD = 2 // Reconnect after 2 consecutive stalls
        private const val TUNNEL_STALL_THRESHOLD_SOCKS = 1 // Faster reconnect for SOCKS profiles (~30s)

        // Persistence keys for auto-restart
        private const val PREFS_NAME = "vpn_service_state"
        private const val PREF_LAST_PROFILE_ID = "last_profile_id"
        private const val PREF_WAS_CONNECTED = "was_connected"

        // Auto-reconnect settings
        private const val AUTO_RECONNECT_MAX_RETRIES = 5
        private val AUTO_RECONNECT_DELAYS_MS = longArrayOf(3000, 3000, 3000, 3000, 3000)

        // Seamless reconnect: try restarting just the proxy (keeping TUN + tun2socks alive)
        // before escalating to kill-switch / auto-reconnect / stop.
        private const val MAX_SEAMLESS_RECONNECTS = 3
        private const val MAX_SEAMLESS_RECONNECTS_DNSTT = 4 // DNSTT is slower — give it more attempts
        private val SEAMLESS_RECONNECT_DELAYS_MS = longArrayOf(1000, 3000, 5000, 8000)

        // Boot retry settings (exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s, 30s, …)
        // 10 retries ≈ 3 minutes total before giving up
        private const val BOOT_RETRY_INITIAL_DELAY_MS = 1000L
        private const val BOOT_RETRY_MAX_DELAY_MS = 30_000L
        private const val BOOT_RETRY_MAX_ATTEMPTS = 10

        // SSH over tunnel (DNSTT/NoizDNS/Slipstream) retry count.
        // DNS tunnels can drop the first connection due to DPI or packet loss.
        private const val SSH_OVER_TUNNEL_RETRIES = 3
    }

    @Inject
    lateinit var connectionManager: VpnConnectionManager

    @Inject
    lateinit var vpnRepository: VpnRepositoryImpl

    @Inject
    lateinit var notificationHelper: NotificationHelper

    @Inject
    lateinit var preferencesDataStore: PreferencesDataStore

    @Inject
    lateinit var chainRepository: app.slipnet.domain.repository.ChainRepository

    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    // Every health check, recovery and delayed callback belongs to one connection.
    // Setup/teardown jobs stay outside this group so they can join its cancellation.
    private var startupCancellation = app.slipnet.tunnel.StartupCancellation()
    private var runtimeJob: Job = SupervisorJob(serviceScope.coroutineContext[Job])
    private var runtimeScope = CoroutineScope(serviceScope.coroutineContext + runtimeJob)
    private fun resetRuntimeScope() {
        startupCancellation = app.slipnet.tunnel.StartupCancellation(serviceSessionId)
        runtimeJob = SupervisorJob(serviceScope.coroutineContext[Job])
        runtimeScope = CoroutineScope(serviceScope.coroutineContext + runtimeJob)
    }
    private var coreEndpoint: app.slipnet.core.LocalSocksEndpoint? = null
    private var vpnInterface: EstablishedTun? = null
    private var healthCheckJob: Job? = null
    private var currentProfileId: Long = -1
    private var currentTunnelType: TunnelType = TunnelType.SLIPSTREAM
    private var connectivityManager: ConnectivityManager? = null
    private var networkCallback: ConnectivityManager.NetworkCallback? = null
    private var lastNetworkAddresses: Set<String> = emptySet()
    private var reconnectDebounceJob: Job? = null
    private var networkLostJob: Job? = null
    private var serviceSessionId: Long = 0
    private var serviceStartId: Int = 0
    private var requestedSessionId: Long = 0
    private var requestAllowTrustPrompt: Boolean = false
    private var requestIsConnectionTest: Boolean = false
    private fun reportConnectionError(error: String) = connectionManager.onVpnError(error, serviceSessionId)
    private fun reportDisconnected() = connectionManager.onVpnDisconnected(serviceSessionId)
    private fun reportEstablished() = connectionManager.onVpnEstablished(serviceSessionId)
    private var retiringRequest: Long? = null

    /** Runs once per delivered session. stopSelfResult only controls Android's
     * started-service lifetime. A bound instance can outlive its native owner. */
    private fun retireSession() {
        val owner = serviceSessionId
        val requested = requestedSessionId.takeIf { it > 0 } ?: owner
        if (requested <= 0 || retiringRequest == requested) return
        retiringRequest = requested
        connectionManager.expectServiceStop(requested)
        val setup = connectJob
        val teardown = disconnectJob
        val runtime = runtimeJob
        app.slipnet.tunnel.TransportTrust.cancelPending()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_CANCEL_REQUESTED, serviceSessionId)
        val abortStartup = startupCancellation.requestCancel()
        setup?.cancel()
        runtime.cancel()
        teardown?.cancel()
        // This job must survive onDestroy cancelling the service scope.
        CoroutineScope(Dispatchers.IO).launch {
            StartupTeardown.await(abortStartup, setup, runtime, teardown)
            TunnelOperations.mutex.withLock {
                if (owner > 0 && TunnelOperations.owner == owner) {
                    cleanupConnectionSync()
                    TunnelOperations.owner = 0
                }
            }
            connectionManager.onServiceReleased(owner)
            if (requested != owner) connectionManager.onServiceReleased(requested)
        }
    }
    private fun stopOwnedService() {
        // A newer manager request may be queued behind this instance's stop barrier.
        // It must not prevent the last delivered request from finishing teardown.
        if (app.slipnet.domain.model.ServiceCommandPolicy.mayStopService(serviceSessionId, requestedSessionId)) {
            retireSession()
            stopSelfResult(serviceStartId)
        }
    }

    private fun announceConnecting(session: Long): Boolean {
        if (!connectionManager.acceptsStart(session)) return false
        connectionManager.onServiceStarting(session)
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.SERVICE_CONNECT_RECEIVED)
        return try {
            startForeground(NotificationHelper.VPN_NOTIFICATION_ID,
                notificationHelper.createVpnNotification(ConnectionState.Connecting))
            true
        } catch (_: Exception) {
            connectionManager.onVpnError("Android could not start the VPN foreground service", session)
            retireSession()
            stopSelfResult(serviceStartId)
            false
        }
    }

    private var connectJob: Job? = null
    private var disconnectJob: Job? = null
    private var stateObserverJob: Job? = null
    @Volatile
    private var isReconnecting = false
    @Volatile
    private var resetTrafficBaseline = false
    @Volatile
    private var isKillSwitchActive = false
    private var isProxyOnly = false
    private var isUserInitiatedDisconnect = false
    private var currentProfileName = ""
    private var currentChainId: Long = -1
    /** Tunnel types active in the current chain (outermost first), for cleanup ordering. */
    private var activeChainLayers: List<TunnelType> = emptyList()

    // Auto-reconnect state
    @Volatile
    private var isAutoReconnecting = false
    private var autoReconnectJob: Job? = null
    private var autoReconnectAttempt = 0
    private var connectionWasSuccessful = false

    // Boot-triggered retry state (network may not be ready after device boot)
    @Volatile
    private var isBootTriggered = false
    private var bootRetryJob: Job? = null
    private var bootRetryAttempt = 0
    private var bootNetworkCallback: ConnectivityManager.NetworkCallback? = null

    // Health check state
    private var quicDownChecks = 0
    private var healthCheckCount = 0
    private var dnsPoolDeadChecks = 0
    private var tunnelStallChecks = 0
    private var lastTxBytes = 0L
    private var lastRxBytes = 0L

    // Notification traffic stats polling
    private var trafficNotificationJob: Job? = null
    private var lastNotifTotalBytes = -1L  // -1 forces first update
    private var lastNotifHadSpeed = false

    // Seamless reconnect state: tracks how many times we've tried a lightweight
    // proxy-only restart before escalating to full teardown.
    private var seamlessReconnectAttempts = 0

    // Timestamp when the connection was fully established. Network change events
    // arriving within a few seconds of this are spurious (common on Chinese OEM
    // ROMs like MIUI/HyperOS that fire network callbacks when the VPN interface
    // is first created) and should be ignored.
    private var connectionEstablishedAt = 0L
    private val NETWORK_CHANGE_GRACE_MS = 5_000L

    // Persistence for service resilience
    private lateinit var prefs: SharedPreferences

    // WakeLock to prevent CPU sleep during VPN session
    private var wakeLock: PowerManager.WakeLock? = null

    // WifiLock to prevent Wi-Fi radio from entering low-power mode when screen is off
    private var wifiLock: WifiManager.WifiLock? = null
    private var wakeLockRenewJob: Job? = null
    private var needsContinuousCpuWake = true
    private var lastUserTrafficAtElapsed = 0L
    private var lastPowerTx = 0L
    private var lastPowerRx = 0L
    private var underlyingDefaultDeclared = false
    private val trafficPollSignal = kotlinx.coroutines.channels.Channel<Unit>(kotlinx.coroutines.channels.Channel.CONFLATED)

    // Doze mode receiver to detect idle state exits
    private var dozeReceiver: BroadcastReceiver? = null

    // DNS servers tracked for the current network (detects DNS changes during handoff)
    private var lastNetworkDnsServers: Set<String> = emptySet()

    override fun onCreate() {
        super.onCreate()
        connectivityManager = getSystemService(ConnectivityManager::class.java)
        prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        serviceStartId = startId
        when (intent?.action) {
            ACTION_CONNECT -> {
                val requestId = intent.getLongExtra(EXTRA_SESSION_ID, 0).let {
                    if (it > 0) it else connectionManager.claimServiceStart()
                }
                if (!connectionManager.acceptsStart(requestId)) {
                    if (serviceSessionId == 0L && requestedSessionId == 0L) stopSelfResult(startId)
                    return START_NOT_STICKY
                }
                requestedSessionId = requestId
                requestIsConnectionTest = intent.getBooleanExtra("rovunet_test_mode", false)
                requestAllowTrustPrompt = intent.getBooleanExtra("rovunet_allow_trust_prompt", !requestIsConnectionTest)
                serviceStartId = startId
                collectionResolvers = intent.getStringArrayExtra("rovunet_resolver_hosts")?.let { hosts ->
                    val ports = intent.getIntArrayExtra("rovunet_resolver_ports")
                    val authoritative = intent.getBooleanArrayExtra("rovunet_resolver_auth")
                    hosts.mapIndexed { i, host -> app.slipnet.domain.model.DnsResolver(host, ports?.getOrNull(i) ?: 53, authoritative?.getOrNull(i) ?: false) }
                }
                val chainId = intent.getLongExtra(EXTRA_CHAIN_ID, -1)
                val profileId = intent.getLongExtra(EXTRA_PROFILE_ID, -1)
                isBootTriggered = intent.getBooleanExtra(EXTRA_BOOT_TRIGGERED, false)
                if (isBootTriggered) {
                    Log.i(TAG, "Boot-triggered connection requested")
                    bootRetryAttempt = 0
                }
                if (chainId != -1L) {
                    connectChain(chainId)
                } else if (profileId != -1L) {
                    connect(profileId)
                }
            }
            ACTION_DISCONNECT -> {
                val delivered = requestedSessionId.takeIf { it > 0 } ?: serviceSessionId
                val expected = intent.getLongExtra(EXTRA_SESSION_ID, delivered)
                if (delivered == 0L || delivered == retiringRequest) {
                    // A stop intent can be delivered after the previous instance
                    // stopped. Do not leave an empty service sticky, and do not
                    // acknowledge another instance's pending native cleanup.
                    stopSelfResult(startId)
                    return START_NOT_STICKY
                } else if (app.slipnet.domain.model.ServiceCommandPolicy.acceptsStop(expected, delivered)) {
                    disconnect(intent.getBooleanExtra(EXTRA_KEEP_SERVICE, false), expected)
                }
            }
            ACTION_RECONNECT -> {
                // Update notification immediately so user sees feedback
                val notification = notificationHelper.createVpnNotification(
                    ConnectionState.Connecting,
                    isProxyOnly = isProxyOnly
                )
                startForeground(NotificationHelper.VPN_NOTIFICATION_ID, notification)
                handleNetworkChange("manual reconnect")
            }
            null -> {
                // Service was restarted by the system after being killed
                // Try to reconnect using the last profile
                handleServiceRestart(flags)
            }
        }

        return START_STICKY
    }

    /**
     * Handle service restart after being killed by Android.
     * Attempts to reconnect using the last connected profile.
     */
    private fun handleServiceRestart(flags: Int) {
        val wasConnected = prefs.getBoolean(PREF_WAS_CONNECTED, false)
        val lastProfileId = prefs.getLong(PREF_LAST_PROFILE_ID, -1)

        Log.i(TAG, "Service restarted by system (flags=$flags, wasConnected=$wasConnected, lastProfileId=$lastProfileId)")

        if (wasConnected && lastProfileId != -1L) {
            Log.i(TAG, "Attempting to auto-reconnect with profile $lastProfileId")
            connect(lastProfileId)
        } else {
            Log.d(TAG, "No previous connection to restore, stopping service")
            stopOwnedService()
        }
    }

    /**
     * Save connection state for auto-restart.
     */
    private fun saveConnectionState(profileId: Long, connected: Boolean) {
        prefs.edit()
            .putLong(PREF_LAST_PROFILE_ID, profileId)
            .putBoolean(PREF_WAS_CONNECTED, connected)
            .apply()
        Log.d(TAG, "Saved connection state: profileId=$profileId, connected=$connected")
    }

    /**
     * Clear saved connection state.
     */
    private fun clearConnectionState() {
        prefs.edit()
            .putBoolean(PREF_WAS_CONNECTED, false)
            .apply()
        connectionEstablishedAt = 0
        Log.d(TAG, "Cleared connection state")
    }

    private var collectionResolvers: List<app.slipnet.domain.model.DnsResolver>? = null
    private fun collectionProfile(p: app.slipnet.domain.model.ServerProfile?): app.slipnet.domain.model.ServerProfile? =
        p?.let { if (collectionResolvers != null) it.copy(resolvers = collectionResolvers!!) else it }

    private fun connect(profileId: Long) {
        // Cancel stale state observer from a previous connection to prevent it
        // from calling stopOwnedService() while the new connection is starting.
        stateObserverJob?.cancel()
        stateObserverJob = null

        app.slipnet.tunnel.TransportTrust.cancelPending()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_CANCEL_REQUESTED, serviceSessionId)
        val abortStartup = startupCancellation.requestCancel()
        val previousRuntime = runtimeJob
        previousRuntime.cancel()
        val previousConnect = connectJob
        previousConnect?.cancel()
        val session = requestedSessionId.takeIf { it > 0 } ?: connectionManager.claimServiceStart().also { requestedSessionId = it }
        val allowTrustPrompt = requestAllowTrustPrompt
        val isTest = requestIsConnectionTest
        if (!announceConnecting(session)) return
        connectJob = serviceScope.launch {
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_SETUP)
            StartupTeardown.await(abortStartup, previousConnect)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_WORKERS_STOPPED, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_RUNTIME)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_WAIT, serviceSessionId)
            previousRuntime.join()
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_DONE, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_TEARDOWN)
            disconnectJob?.join()
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_NATIVE_OWNER)
            TunnelOperations.mutex.withLock {
            if (!connectionManager.acceptsStart(session)) return@launch
            val hadNativeOwner = TunnelOperations.owner != 0L || currentProfileId != -1L
            serviceSessionId = session
            TunnelOperations.owner = session
            app.slipnet.tunnel.TransportTrust.allowPrompt = allowTrustPrompt
            if (hadNativeOwner) {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.NATIVE_CLEANUP)
                cleanupConnection()
            }
            resetRuntimeScope()
            vpnRepository.beginSession(session)
            vpnRepository.setPreConnectionScanAllowed(isTest)

            val profile = collectionProfile(connectionManager.getProfileById(profileId))
            if (profile == null) {
                reportConnectionError("Profile not found")
                stopOwnedService()
                return@launch
            }

            if (!preferencesDataStore.proxyCredentialsReadable()) {
                reportConnectionError(app.slipnet.presentation.theme.RovunetStrings.localizedContext(this@SlipNetVpnService)
                    .getString(app.slipnet.R.string.proxy_password_unavailable))
                stopOwnedService()
                return@launch
            }
            if (profile.needsCredentials) {
                reportConnectionError(SAVED_CREDENTIALS_UNAVAILABLE)
                stopOwnedService()
                return@launch
            }
            if (profile.isExpired) {
                reportConnectionError("This profile has expired")
                stopOwnedService()
                return@launch
            }
            if (profile.boundDeviceId.isNotEmpty() && profile.boundDeviceId != connectionManager.getDeviceId()) {
                reportConnectionError("This profile is bound to a different device")
                stopOwnedService()
                return@launch
            }

            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.PROFILE_LOADED)
            needsContinuousCpuWake = VpnPowerPolicy.continuous(profile)
            currentProfileId = profileId
            currentProfileName = profile.name
            isUserInitiatedDisconnect = false

            // Log connection summary before enabling redaction so it's visible for locked profiles
            if (profile.isLocked) {
                val user = profile.sshUsername.ifBlank { profile.socksUsername ?: profile.naiveUsername.ifBlank { "" } }
                Log.i(TAG, "Connecting locked profile: tunnel=${profile.tunnelType.displayName}" +
                        if (user.isNotBlank()) ", user=$user" else "")
            }

            // Redact sensitive config from in-app debug log for locked profiles
            app.slipnet.util.AppLog.redactSensitive = profile.isLocked

            // Dismiss any previous reconnect/disconnect notifications
            getSystemService(NotificationManager::class.java).apply {
                cancel(NotificationHelper.RECONNECT_NOTIFICATION_ID)
                cancel(NotificationHelper.DISCONNECT_NOTIFICATION_ID)
            }

            // Show connecting notification
            val notification = notificationHelper.createVpnNotification(ConnectionState.Connecting)
            startForeground(NotificationHelper.VPN_NOTIFICATION_ID, notification)

            val pm = getSystemService(Context.POWER_SERVICE) as PowerManager

            // Acquire WakeLock with timeout (Chinese OEM ROMs kill indefinite WakeLocks)
            if (wakeLock == null) {
                wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "Rovunet:VpnWakeLock").apply {
                    setReferenceCounted(false)
                    acquire(WAKELOCK_TIMEOUT_MS)
                }
                Log.d(TAG, "WakeLock acquired (${WAKELOCK_TIMEOUT_MS / 60000}min timeout)")
            }
            startWakeLockRenewal()

            // Acquire WifiLock to keep Wi-Fi radio active when screen is off.
            // Chinese OEMs (Xiaomi, Huawei, etc.) kill apps using WIFI_MODE_FULL_LOW_LATENCY,
            // so fall back to WIFI_MODE_FULL on those devices.
            if (wifiLock == null && defaultPhysicalNetwork()?.let { connectivityManager?.getNetworkCapabilities(it)?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) } == true) {
                val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
                @Suppress("DEPRECATION")
                val wifiMode = when {
                    isChineseOem() -> WifiManager.WIFI_MODE_FULL
                    Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q -> WifiManager.WIFI_MODE_FULL_LOW_LATENCY
                    else -> WifiManager.WIFI_MODE_FULL_HIGH_PERF
                }
                wifiLock = wifiManager?.createWifiLock(wifiMode, "Rovunet:VpnWifiLock")?.apply {
                    setReferenceCounted(false)
                    acquire()
                }
                if (wifiLock != null) Log.d(TAG, "WifiLock acquired")
            }

            // Warn if battery optimization is not disabled
            if (!pm.isIgnoringBatteryOptimizations(packageName)) {
                Log.w(TAG, "Battery optimization is enabled - VPN may be interrupted by Doze mode")
            }

            // Check Private DNS mode. Only "hostname" (user-set provider) is dangerous —
            // it forces DoT and bypasses VPN DNS entirely. "opportunistic" is the Android
            // default and falls back to plain DNS through the VPN when the resolver
            // doesn't support DoT (which is most ISP resolvers).
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                try {
                    val privateDnsMode = android.provider.Settings.Global.getString(
                        contentResolver, "private_dns_mode"
                    )
                    when (privateDnsMode) {
                        "hostname" -> Log.w(TAG, "Private DNS is set to a custom provider - DNS queries will bypass the VPN tunnel")
                        "opportunistic" -> Log.d(TAG, "Private DNS: opportunistic (default, safe for most networks)")
                    }
                } catch (_: Exception) {}
            }

            try {
                // Read proxy-only mode setting
                isProxyOnly = preferencesDataStore.proxyOnlyMode.first()

                // Set debug logging on tunnel bridges
                val debug = preferencesDataStore.debugLogging.first()
                SshTunnelBridge.debugLogging = debug
                DohBridge.debugLogging = debug
                SlipstreamSocksBridge.debugLogging = debug
                DnsttSocksBridge.debugLogging = debug
                NaiveSocksBridge.debugLogging = debug
                TorSocksBridge.debugLogging = debug
                HttpProxyServer.debugLogging = debug

                // Configure domain routing on bridges
                val domainRouter = buildDomainRouter()
                VlessBridge.domainRouter = domainRouter
                DnsttSocksBridge.domainRouter = domainRouter
                SshTunnelBridge.domainRouter = domainRouter
                DohBridge.domainRouter = domainRouter
                SlipstreamSocksBridge.domainRouter = domainRouter
                NaiveSocksBridge.domainRouter = domainRouter
                TorSocksBridge.domainRouter = domainRouter

                // Bandwidth limiting
                val ulKbps = preferencesDataStore.uploadLimitKbps.first()
                val dlKbps = preferencesDataStore.downloadLimitKbps.first()
                val ulLimiter = if (ulKbps > 0) RateLimiter(ulKbps.toLong() * 1024) else null
                val dlLimiter = if (dlKbps > 0) RateLimiter(dlKbps.toLong() * 1024) else null
                DnsttSocksBridge.uploadLimiter = ulLimiter
                DnsttSocksBridge.downloadLimiter = dlLimiter
                SlipstreamSocksBridge.uploadLimiter = ulLimiter
                SlipstreamSocksBridge.downloadLimiter = dlLimiter
                NaiveSocksBridge.uploadLimiter = ulLimiter
                NaiveSocksBridge.downloadLimiter = dlLimiter
                SshTunnelBridge.uploadLimiter = ulLimiter
                SshTunnelBridge.downloadLimiter = dlLimiter
                DohBridge.uploadLimiter = ulLimiter
                DohBridge.downloadLimiter = dlLimiter
                HttpProxyServer.uploadLimiter = ulLimiter
                HttpProxyServer.downloadLimiter = dlLimiter
                Socks5ProxyBridge.uploadLimiter = ulLimiter
                Socks5ProxyBridge.downloadLimiter = dlLimiter

                // Local proxy auth: set credentials on SSH bridge
                val proxyAuthEnabled = preferencesDataStore.proxyAuthEnabled.first()
                if (proxyAuthEnabled) {
                    val authUser = preferencesDataStore.proxyAuthUsername.first().ifEmpty { null }
                    val authPass = preferencesDataStore.proxyAuthPassword.first().ifEmpty { null }
                    require(!authUser.isNullOrEmpty() && !authPass.isNullOrEmpty() &&
                        authUser.toByteArray(Charsets.UTF_8).size <= 255 && authPass.toByteArray(Charsets.UTF_8).size <= 255) { "Set a valid local proxy username and password in Settings" }
                    SshTunnelBridge.localAuthUsername = authUser
                    SshTunnelBridge.localAuthPassword = authPass
                } else {
                    SshTunnelBridge.localAuthUsername = null
                    SshTunnelBridge.localAuthPassword = null
                }

                // Track the tunnel type for this connection
                currentTunnelType = profile.tunnelType
                Log.i(TAG, "Starting VPN with tunnel type: $currentTunnelType")

                // Global resolver override: replace profile resolvers with user's global list
                val globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? =
                    (collectionResolvers ?: preferencesDataStore.parsedGlobalResolvers()).takeIf { it.isNotEmpty() }
                        ?.also { Log.i(TAG, "Using global DNS resolver override: ${it.joinToString { r -> "${r.host}:${r.port}" }}") }

                // Extract global DNS IP for hostname resolution (bypasses ISP DNS filtering)
                val globalDnsIp = globalResolverOverride?.firstOrNull()?.host?.takeIf {
                    DomainRouter.isIpAddress(it)
                }

                val effectiveResolverHost = globalResolverOverride?.firstOrNull()?.host
                    ?: profile.resolvers.firstOrNull()?.host
                val dnsServer = resolveToIp(effectiveResolverHost, globalDnsIp)
                // Remote DNS: the DNS servers used on the remote side of the tunnel
                var remoteDns = preferencesDataStore.getEffectiveRemoteDns().first()
                var remoteDnsFallback = preferencesDataStore.getEffectiveRemoteDnsFallback().first()

                // DNSTT+SSH / NoizDNS+SSH: use reliable public DNS by default.
                // 127.0.0.53 (systemd-resolved) is unavailable on most servers, causing
                // DNS workers to fail and fall back — adding ~1s latency at startup.
                // Only override when user hasn't set a custom remote DNS.
                if (currentTunnelType == TunnelType.DNSTT_SSH || currentTunnelType == TunnelType.NOIZDNS_SSH || currentTunnelType == TunnelType.VAYDNS_SSH) {
                    val dnsMode = preferencesDataStore.remoteDnsMode.first()
                    if (dnsMode == "default") {
                        remoteDns = "8.8.8.8"
                        remoteDnsFallback = "1.1.1.1"
                    }
                }

                Log.i(TAG, "Remote DNS: $remoteDns (fallback: $remoteDnsFallback)")

                // Check if tunnel type is available in this build flavor
                if (!currentTunnelType.isAvailable()) {
                    handleTunnelFailure("${currentTunnelType.displayName} is not available in this edition")
                    return@launch
                }

                // The startup order differs between tunnel types:
                // - Slipstream: Start proxy first (Rust uses protect_socket JNI callback)
                // - DNSTT: Establish VPN first (uses addDisallowedApplication for socket protection)
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.TRANSPORT_START)
                when (currentTunnelType) {
                    TunnelType.SLIPSTREAM -> connectSlipstream(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.SLIPSTREAM_SSH -> connectSlipstreamSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.DNSTT -> connectDnstt(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.NOIZDNS -> connectDnstt(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.SSH -> connectSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalDnsIp)
                    TunnelType.DNSTT_SSH -> connectDnsttSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.NOIZDNS_SSH -> connectDnsttSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.DOH -> connectDoh(profile, dnsServer)
                    TunnelType.SNOWFLAKE -> connectSnowflake(profile, dnsServer)
                    TunnelType.NAIVE_SSH -> connectNaiveSsh(profile, dnsServer, remoteDns, remoteDnsFallback)
                    TunnelType.NAIVE -> connectNaive(profile, dnsServer, remoteDns, remoteDnsFallback)
                    TunnelType.VAYDNS -> connectVaydns(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.VAYDNS_SSH -> connectVaydnsSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.SOCKS5 -> connectSocks5(profile, dnsServer, remoteDns, remoteDnsFallback)
                    TunnelType.XRAY -> connectXray(profile, dnsServer)
                    TunnelType.VLESS -> connectVless(profile, dnsServer)
                    TunnelType.SOAR -> connectSoar(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                    TunnelType.SOAR_SSH -> connectSoarSsh(profile, dnsServer, remoteDns, remoteDnsFallback, globalResolverOverride)
                }

            } catch (e: kotlinx.coroutines.CancellationException) {
                // Coroutine was cancelled (user disconnected, service stopped, or new connect).
                // Do NOT treat as an error — let the cancelling code handle cleanup.
                Log.d(TAG, "Connection coroutine cancelled")
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Exception during connection", e)

                // Boot-triggered retry: network may not be ready yet after device boot.
                // Retry with exponential backoff regardless of connectionWasSuccessful.
                if (isBootTriggered && !isUserInitiatedDisconnect) {
                    enterBootRetryMode(currentProfileId, "boot connect failed: ${e.message}")
                    return@launch
                }

                // If this was an auto-reconnect attempt and we can still retry, re-enter the retry loop
                val autoReconnectEnabled = try { preferencesDataStore.autoReconnect.first() } catch (_: Exception) { false }
                if (autoReconnectEnabled && connectionWasSuccessful && !isUserInitiatedDisconnect
                    && autoReconnectAttempt < AUTO_RECONNECT_MAX_RETRIES) {
                    handleTunnelFailure("reconnect failed: ${e.message}")
                } else {
                    reportConnectionError(e.message ?: "Unknown error")
                    cleanupConnection()
                    stopForeground(STOP_FOREGROUND_REMOVE)
                    stopOwnedService()
                }
            }
            }
        }
    }

    // ── Chain Connection ──────────────────────────────────────────────

    private fun connectChain(chainId: Long) {
        stateObserverJob?.cancel()
        stateObserverJob = null

        app.slipnet.tunnel.TransportTrust.cancelPending()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_CANCEL_REQUESTED, serviceSessionId)
        val abortStartup = startupCancellation.requestCancel()
        val previousRuntime = runtimeJob
        previousRuntime.cancel()
        val previousConnect = connectJob
        previousConnect?.cancel()
        val session = requestedSessionId.takeIf { it > 0 } ?: connectionManager.claimServiceStart().also { requestedSessionId = it }
        val allowTrustPrompt = requestAllowTrustPrompt
        val isTest = requestIsConnectionTest
        if (!announceConnecting(session)) return
        connectJob = serviceScope.launch {
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_SETUP)
            StartupTeardown.await(abortStartup, previousConnect)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_WORKERS_STOPPED, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_RUNTIME)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_WAIT, serviceSessionId)
            previousRuntime.join()
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_DONE, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_PREVIOUS_TEARDOWN)
            disconnectJob?.join()
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.WAIT_NATIVE_OWNER)
            TunnelOperations.mutex.withLock {
            if (!connectionManager.acceptsStart(session)) return@launch
            val hadNativeOwner = TunnelOperations.owner != 0L || currentProfileId != -1L
            serviceSessionId = session
            TunnelOperations.owner = session
            app.slipnet.tunnel.TransportTrust.allowPrompt = allowTrustPrompt
            if (hadNativeOwner) {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.NATIVE_CLEANUP)
                cleanupConnection()
            }
            resetRuntimeScope()
            vpnRepository.beginSession(session)
            vpnRepository.setPreConnectionScanAllowed(isTest)

            val chain = chainRepository.getChainById(chainId)
            if (chain == null) {
                reportConnectionError("Chain not found")
                stopOwnedService()
                return@launch
            }

            // Resolve all profiles in the chain
            val profiles = chain.profileIds.mapNotNull { connectionManager.getProfileById(it) }
            if (profiles.size != chain.profileIds.size) {
                reportConnectionError("Some profiles in chain were deleted")
                stopOwnedService()
                return@launch
            }

            if (!preferencesDataStore.proxyCredentialsReadable()) {
                reportConnectionError(app.slipnet.presentation.theme.RovunetStrings.localizedContext(this@SlipNetVpnService)
                    .getString(app.slipnet.R.string.proxy_password_unavailable))
                stopOwnedService()
                return@launch
            }
            if (profiles.any { it.needsCredentials }) {
                reportConnectionError(SAVED_CREDENTIALS_UNAVAILABLE)
                stopOwnedService()
                return@launch
            }
            if (profiles.any { it.isExpired || (it.boundDeviceId.isNotEmpty() && it.boundDeviceId != connectionManager.getDeviceId()) }) {
                reportConnectionError("A chain profile has expired or belongs to another device")
                stopOwnedService()
                return@launch
            }
            // Validate the chain
            val validationError = app.slipnet.domain.model.ChainValidation.validate(profiles)
            if (validationError != null) {
                reportConnectionError(validationError)
                stopOwnedService()
                return@launch
            }

            currentChainId = chainId
            // Use first profile for connection state tracking
            val primaryProfile = profiles.first()
            needsContinuousCpuWake = profiles.any { VpnPowerPolicy.continuous(it) }
            currentProfileId = primaryProfile.id
            currentProfileName = chain.name
            isUserInitiatedDisconnect = false

            // Log chain summary before enabling redaction so it's visible for locked profiles
            if (profiles.any { it.isLocked }) {
                for (p in profiles) {
                    if (p.isLocked) {
                        val user = p.sshUsername.ifBlank { p.socksUsername ?: p.naiveUsername.ifBlank { "" } }
                        Log.i(TAG, "Chain locked layer: tunnel=${p.tunnelType.displayName}" +
                                if (user.isNotBlank()) ", user=$user" else "")
                    }
                }
            }
            app.slipnet.util.AppLog.redactSensitive = profiles.any { it.isLocked }

            getSystemService(android.app.NotificationManager::class.java).apply {
                cancel(NotificationHelper.RECONNECT_NOTIFICATION_ID)
                cancel(NotificationHelper.DISCONNECT_NOTIFICATION_ID)
            }

            val notification = notificationHelper.createVpnNotification(ConnectionState.Connecting)
            startForeground(NotificationHelper.VPN_NOTIFICATION_ID, notification)

            val pm = getSystemService(Context.POWER_SERVICE) as android.os.PowerManager
            if (wakeLock == null) {
                wakeLock = pm.newWakeLock(android.os.PowerManager.PARTIAL_WAKE_LOCK, "Rovunet:VpnWakeLock").apply {
                    setReferenceCounted(false)
                    acquire(WAKELOCK_TIMEOUT_MS)
                }
            }
            startWakeLockRenewal()
            if (wifiLock == null && defaultPhysicalNetwork()?.let { connectivityManager?.getNetworkCapabilities(it)?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) } == true) {
                val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as? android.net.wifi.WifiManager
                @Suppress("DEPRECATION")
                val wifiMode = when {
                    isChineseOem() -> android.net.wifi.WifiManager.WIFI_MODE_FULL
                    android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q -> android.net.wifi.WifiManager.WIFI_MODE_FULL_LOW_LATENCY
                    else -> android.net.wifi.WifiManager.WIFI_MODE_FULL_HIGH_PERF
                }
                wifiLock = wifiManager?.createWifiLock(wifiMode, "Rovunet:VpnWifiLock")?.apply {
                    setReferenceCounted(false)
                    acquire()
                }
            }

            try {
                isProxyOnly = preferencesDataStore.proxyOnlyMode.first()
                val debug = preferencesDataStore.debugLogging.first()
                SshTunnelBridge.debugLogging = debug
                DohBridge.debugLogging = debug
                SlipstreamSocksBridge.debugLogging = debug
                DnsttSocksBridge.debugLogging = debug
                NaiveSocksBridge.debugLogging = debug
                TorSocksBridge.debugLogging = debug
                HttpProxyServer.debugLogging = debug

                val domainRouter = buildDomainRouter()
                VlessBridge.domainRouter = domainRouter
                DnsttSocksBridge.domainRouter = domainRouter
                SshTunnelBridge.domainRouter = domainRouter
                DohBridge.domainRouter = domainRouter
                SlipstreamSocksBridge.domainRouter = domainRouter
                NaiveSocksBridge.domainRouter = domainRouter
                TorSocksBridge.domainRouter = domainRouter

                // Bandwidth limiting
                val ulKbps = preferencesDataStore.uploadLimitKbps.first()
                val dlKbps = preferencesDataStore.downloadLimitKbps.first()
                val ulLimiter = if (ulKbps > 0) RateLimiter(ulKbps.toLong() * 1024) else null
                val dlLimiter = if (dlKbps > 0) RateLimiter(dlKbps.toLong() * 1024) else null
                DnsttSocksBridge.uploadLimiter = ulLimiter
                DnsttSocksBridge.downloadLimiter = dlLimiter
                SlipstreamSocksBridge.uploadLimiter = ulLimiter
                SlipstreamSocksBridge.downloadLimiter = dlLimiter
                NaiveSocksBridge.uploadLimiter = ulLimiter
                NaiveSocksBridge.downloadLimiter = dlLimiter
                SshTunnelBridge.uploadLimiter = ulLimiter
                SshTunnelBridge.downloadLimiter = dlLimiter
                DohBridge.uploadLimiter = ulLimiter
                DohBridge.downloadLimiter = dlLimiter
                HttpProxyServer.uploadLimiter = ulLimiter
                HttpProxyServer.downloadLimiter = dlLimiter

                // Local proxy auth: set credentials on SSH bridge
                val proxyAuthEnabled2 = preferencesDataStore.proxyAuthEnabled.first()
                if (proxyAuthEnabled2) {
                    val authUser = preferencesDataStore.proxyAuthUsername.first().ifEmpty { null }
                    val authPass = preferencesDataStore.proxyAuthPassword.first().ifEmpty { null }
                    require(!authUser.isNullOrEmpty() && !authPass.isNullOrEmpty() &&
                        authUser.toByteArray(Charsets.UTF_8).size <= 255 && authPass.toByteArray(Charsets.UTF_8).size <= 255) { "Set a valid local proxy username and password in Settings" }
                    SshTunnelBridge.localAuthUsername = authUser
                    SshTunnelBridge.localAuthPassword = authPass
                } else {
                    SshTunnelBridge.localAuthUsername = null
                    SshTunnelBridge.localAuthPassword = null
                }

                executeChain(profiles)
            } catch (e: kotlinx.coroutines.CancellationException) {
                Log.d(TAG, "Chain connection coroutine cancelled")
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Exception during chain connection", e)
                reportConnectionError(e.message ?: "Chain connection failed")
                cleanupConnection()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
            }
            }
        }
    }

    /**
     * Execute a chain of profiles, starting each layer and wiring them together.
     *
     * Port allocation: final layer on proxyPort, each preceding layer on proxyPort + offset.
     * Example for 3-layer chain: layer[0] on proxyPort+2, layer[1] on proxyPort+1, layer[2] on proxyPort.
     */
    private suspend fun executeChain(profiles: List<app.slipnet.domain.model.ServerProfile>) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val chainSize = profiles.size
        val dnsServer = resolveToIp(profiles.first().resolvers.firstOrNull()?.host)
        var remoteDns = preferencesDataStore.getEffectiveRemoteDns().first()
        var remoteDnsFallback = preferencesDataStore.getEffectiveRemoteDnsFallback().first()

        // Track started layers for cleanup on failure
        val startedLayers = mutableListOf<TunnelType>()

        // Determine if VPN is needed before outermost layer
        val outermost = profiles.first()
        val needsVpnFirst = app.slipnet.domain.model.ChainValidation.needsVpnFirst(outermost.tunnelType)

        if (!isProxyOnly && needsVpnFirst && vpnInterface == null) {
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }
            delay(200)
        }

        // Port the previous layer actually listens on — a Go bridge may bind an
        // alternative port when its preferred one is still draining.
        var prevLayerPort = -1

        // Start each layer from outermost to innermost
        for (i in profiles.indices) {
            val profile = profiles[i]
            val layerPort = proxyPort + (chainSize - 1 - i)
            val layerHost = if (i < chainSize - 1) "127.0.0.1" else proxyHost
            val isLast = i == chainSize - 1

            // Previous layer's output (null for outermost)
            val prevPort = if (i > 0) prevLayerPort else -1

            Log.i(TAG, "Chain layer $i/${chainSize - 1}: ${profile.tunnelType.displayName} on port $layerPort" +
                    if (prevPort > 0) " (through port $prevPort)" else " (outermost)")

            val prevLayerType = if (i > 0) profiles[i - 1].tunnelType else null
            val prevProfile = if (i > 0) profiles[i - 1] else null

            // Internal ports (for bridges that need a separate Go/Rust process port)
            // must be above the chain port range [proxyPort, proxyPort+chainSize-1]
            // to avoid collisions between chain layers.
            val internalPortBase = proxyPort + chainSize

            val result = startChainLayer(
                profile = profile,
                layerIndex = i,
                layerPort = layerPort,
                layerHost = layerHost,
                prevPort = prevPort,
                prevHost = "127.0.0.1",
                prevLayerType = prevLayerType,
                prevProfile = prevProfile,
                isLast = isLast,
                remoteDns = remoteDns,
                remoteDnsFallback = remoteDnsFallback,
                internalPortBase = internalPortBase
            )
            if (result.isFailure) {
                val error = result.exceptionOrNull()?.message ?: "Failed to start ${profile.tunnelType.displayName}"
                reportConnectionError(error)
                // Clean up started layers in reverse
                for (layer in startedLayers.reversed()) {
                    withContext(Dispatchers.IO) { stopLayer(layer) }
                }
                activeChainLayers = emptyList()
                currentChainId = -1
                vpnInterface?.close()
                vpnInterface = null
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }
            startedLayers.add(profile.tunnelType)
            // Keep activeChainLayers in sync so stopCurrentProxy() can clean up on retry
            activeChainLayers = startedLayers.toList()

            // Wait for this layer's port to be ready
            val actualPort = if (profile.tunnelType == TunnelType.DNSTT || profile.tunnelType == TunnelType.NOIZDNS) {
                DnsttBridge.getClientPort().also {
                    if (it != layerPort) Log.i(TAG, "DNSTT bound to alternative port $it (preferred $layerPort)")
                }
            } else if (profile.tunnelType == TunnelType.VAYDNS) {
                VaydnsBridge.getClientPort().also {
                    if (it != layerPort) Log.i(TAG, "VayDNS bound to alternative port $it (preferred $layerPort)")
                }
            } else if (profile.tunnelType == TunnelType.SOAR && !isLast) {
                // As the final layer Soar sits behind Socks5ProxyBridge on layerPort
                SoarBridge.getClientPort().also {
                    if (it != layerPort) Log.i(TAG, "Soar bound to alternative port $it (preferred $layerPort)")
                }
            } else layerPort

            // NaiveProxy needs its own readiness check (log-based, not TCP probe)
            // Also temporarily set currentTunnelType so waitForProxyReady checks the right bridge
            val savedTunnelType = currentTunnelType
            currentTunnelType = profile.tunnelType
            val layerReady = if (profile.tunnelType == TunnelType.NAIVE || profile.tunnelType == TunnelType.NAIVE_SSH) {
                waitForNaiveReady(maxAttempts = 30, delayMs = 100)
            } else {
                waitForProxyReady(actualPort, maxAttempts = 30, delayMs = 100)
            }
            currentTunnelType = savedTunnelType

            if (!layerReady) {
                Log.e(TAG, "Chain layer $i failed to become ready on port $actualPort")
                reportConnectionError("${profile.tunnelType.displayName} failed to start")
                for (layer in startedLayers.reversed()) withContext(Dispatchers.IO) { stopLayer(layer) }
                activeChainLayers = emptyList()
                currentChainId = -1
                vpnInterface?.close()
                vpnInterface = null
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // For Snowflake, wait for Tor to finish bootstrapping. Without this,
            // the chain only checks that TorSocksBridge's listener is open, which
            // happens before Tor has reached any relay — the UI flips to
            // "Connected" while Tor is still stalled on PT/bridge contact.
            if (profile.tunnelType == TunnelType.SNOWFLAKE) {
                Log.i(TAG, "Chain layer $i: waiting for Tor to bootstrap...")
                if (!waitForTorReady(maxWaitMs = 300000)) {
                    reportConnectionError(
                        "Tor failed to bootstrap within timeout " +
                        "(${SnowflakeBridge.torBootstrapProgress}%)"
                    )
                    for (layer in startedLayers.reversed()) withContext(Dispatchers.IO) { stopLayer(layer) }
                    activeChainLayers = emptyList()
                    currentChainId = -1
                    vpnInterface?.close()
                    vpnInterface = null
                    stopForeground(STOP_FOREGROUND_REMOVE)
                    stopOwnedService()
                    return
                }
                Log.i(TAG, "Chain layer $i: Tor bootstrapped")
            }

            prevLayerPort = actualPort

            // For Slipstream, wait for QUIC handshake
            if (profile.tunnelType == TunnelType.SLIPSTREAM) {
                if (!waitForQuicReady(maxAttempts = 50, delayMs = 200)) {
                    reportConnectionError("Slipstream QUIC handshake failed")
                    for (layer in startedLayers.reversed()) withContext(Dispatchers.IO) { stopLayer(layer) }
                    activeChainLayers = emptyList()
                    currentChainId = -1
                    vpnInterface?.close()
                    vpnInterface = null
                    stopForeground(STOP_FOREGROUND_REMOVE)
                    stopOwnedService()
                    return
                }

            }

            // Slipstream and Snowflake can require their outer transport before establishing TUN.
            if (i == 0 && !isProxyOnly && vpnInterface == null) {
                vpnInterface = establishVpnInterface(dnsServer)
                if (vpnInterface == null) {
                    reportConnectionError("Failed to establish VPN interface")
                    for (layer in startedLayers.reversed()) withContext(Dispatchers.IO) { stopLayer(layer) }
                    activeChainLayers = emptyList()
                    currentChainId = -1
                    stopForeground(STOP_FOREGROUND_REMOVE)
                    stopOwnedService()
                    return
                }
                delay(200)
            }
        }

        // Store chain layers for cleanup
        activeChainLayers = startedLayers.toList()
        // Track the final layer's tunnel type for stats
        currentTunnelType = profiles.last().tunnelType
        vpnRepository.setCurrentTunnelType(currentTunnelType)

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profiles.first())
            Log.i(TAG, "Proxy-only mode: chain ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Start tun2socks on the final proxy port
        val tun2socksResult = vpnRepository.startTun2Socks(profiles.first(), requireNotNull(vpnInterface) { "Chain VPN interface unavailable" })
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            for (layer in activeChainLayers.reversed()) withContext(Dispatchers.IO) { stopLayer(layer) }
            activeChainLayers = emptyList()
            currentChainId = -1
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.i(TAG, "Chain started: ${profiles.joinToString(" → ") { it.tunnelType.displayName }}")
        finishConnection()
    }

    /**
     * Start a single layer in the chain.
     */
    private suspend fun startChainLayer(
        profile: app.slipnet.domain.model.ServerProfile,
        layerIndex: Int = 0,
        layerPort: Int,
        layerHost: String,
        prevPort: Int,
        prevHost: String,
        prevLayerType: TunnelType?,
        prevProfile: app.slipnet.domain.model.ServerProfile? = null,
        isLast: Boolean,
        remoteDns: String,
        remoteDnsFallback: String,
        internalPortBase: Int = layerPort + 1
    ): Result<Unit> {
        return when (profile.tunnelType) {
            TunnelType.XRAY -> {
                if (layerIndex != 0 || isLast || prevPort > 0)
                    return Result.failure(IllegalArgumentException("Xray must be the outer layer before SSH"))
                val endpoint = app.slipnet.core.LocalSocksEndpoint(layerPort,
                    java.util.UUID.randomUUID().toString(), java.util.UUID.randomUUID().toString())
                coreEndpoint = endpoint
                withContext(Dispatchers.IO) { XrayBridge.start(this@SlipNetVpnService, profile.coreLink, endpoint, null) }
            }
            TunnelType.DNSTT, TunnelType.NOIZDNS -> {
                DnsttBridge.setVpnService(this@SlipNetVpnService)
                val isNoizdns = profile.tunnelType == TunnelType.NOIZDNS

                // Build upstream SOCKS5 proxy params if previous layer provides SOCKS5.
                // Skip for SSH: SOCKS5 forces TCP promotion but most DNS resolvers
                // only accept UDP. DNSTT's protected sockets bypass the VPN,
                // so direct DNS queries work without routing through SSH.
                var socksAddr: String? = null
                var socksUser: String? = null
                var socksPass: String? = null
                if (prevPort > 0 && prevLayerType != null && prevLayerType != TunnelType.SSH) {
                    val prevOutput = app.slipnet.domain.model.ChainValidation.outputType(prevLayerType)
                    if (prevOutput == app.slipnet.domain.model.LayerOutput.SOCKS5) {
                        socksAddr = "$prevHost:$prevPort"
                        socksUser = prevProfile?.socksUsername
                        socksPass = prevProfile?.socksPassword
                        Log.i(TAG, "DNSTT[$layerIndex]: routing DNS through SOCKS5 proxy $socksAddr")
                    }
                }

                if (isLast) {
                    val bridgePort = layerPort
                    val internalPort = internalPortBase
                    val proxyResult = if (isNoizdns)
                        vpnRepository.startNoizdnsProxy(profile, portOverride = internalPort, hostOverride = "127.0.0.1",
                            socksProxyAddr = socksAddr, socksProxyUser = socksUser, socksProxyPass = socksPass)
                    else
                        vpnRepository.startDnsttProxy(profile, portOverride = internalPort, hostOverride = "127.0.0.1",
                            socksProxyAddr = socksAddr, socksProxyUser = socksUser, socksProxyPass = socksPass)
                    if (proxyResult.isFailure) return proxyResult
                    val actualPort = DnsttBridge.getClientPort()
                    DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
                    DnsttSocksBridge.proxyOnlyMode = isProxyOnly
                    DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    vpnRepository.startDnsttSocksBridge(
                        dnsttPort = actualPort, dnsttHost = "127.0.0.1",
                        bridgePort = bridgePort, bridgeHost = layerHost,
                        socksUsername = profile.socksUsername, socksPassword = profile.socksPassword,
                        dnsServer = remoteDns, dnsFallback = remoteDnsFallback
                    )
                } else {
                    if (isNoizdns)
                        vpnRepository.startNoizdnsProxy(profile, portOverride = layerPort, hostOverride = "127.0.0.1",
                            socksProxyAddr = socksAddr, socksProxyUser = socksUser, socksProxyPass = socksPass)
                    else
                        vpnRepository.startDnsttProxy(profile, portOverride = layerPort, hostOverride = "127.0.0.1",
                            socksProxyAddr = socksAddr, socksProxyUser = socksUser, socksProxyPass = socksPass)
                }
            }
            TunnelType.VAYDNS -> {
                if (isLast) {
                    val bridgePort = layerPort
                    val internalPort = internalPortBase
                    val proxyResult = vpnRepository.startVaydnsProxy(profile, portOverride = internalPort, hostOverride = "127.0.0.1")
                    if (proxyResult.isFailure) return proxyResult
                    val actualPort = VaydnsBridge.getClientPort()
                    DnsttSocksBridge.upstreamRunningCheck = { VaydnsBridge.isRunning() }
                    DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
                    DnsttSocksBridge.proxyOnlyMode = isProxyOnly
                    DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    vpnRepository.startDnsttSocksBridge(
                        dnsttPort = actualPort, dnsttHost = "127.0.0.1",
                        bridgePort = bridgePort, bridgeHost = layerHost,
                        socksUsername = profile.socksUsername, socksPassword = profile.socksPassword,
                        dnsServer = remoteDns, dnsFallback = remoteDnsFallback
                    )
                } else {
                    vpnRepository.startVaydnsProxy(profile, portOverride = layerPort, hostOverride = "127.0.0.1")
                }
            }
            TunnelType.SOAR -> {
                // Behind a SOCKS5 layer, Soar sends its resolver queries through it as DNS over
                // TCP (a CONNECT-only proxy can't carry UDP).
                val upstream = if (prevPort > 0 && prevLayerType != null &&
                    app.slipnet.domain.model.ChainValidation.outputType(prevLayerType) ==
                    app.slipnet.domain.model.LayerOutput.SOCKS5
                ) {
                    "$prevHost:$prevPort".also {
                        Log.i(TAG, "Soar[$layerIndex]: routing resolver traffic through SOCKS5 $it")
                    }
                } else null

                if (isLast) {
                    val proxyResult = vpnRepository.startSoarProxy(
                        profile, portOverride = internalPortBase, hostOverride = "127.0.0.1",
                        socksProxyAddr = upstream, socksProxyUser = prevProfile?.socksUsername,
                        socksProxyPass = prevProfile?.socksPassword
                    )
                    if (proxyResult.isFailure) return proxyResult
                    // Final layer faces tun2socks: Socks5ProxyBridge (default instance) handles
                    // its UDP/DNS, as in a single-profile Soar connection.
                    Socks5ProxyBridge.debugLogging = preferencesDataStore.debugLogging.first()
                    val ulKbpsChain = preferencesDataStore.uploadLimitKbps.first()
                    val dlKbpsChain = preferencesDataStore.downloadLimitKbps.first()
                    Socks5ProxyBridge.uploadLimiter = if (ulKbpsChain > 0) RateLimiter(ulKbpsChain.toLong() * 1024) else null
                    Socks5ProxyBridge.downloadLimiter = if (dlKbpsChain > 0) RateLimiter(dlKbpsChain.toLong() * 1024) else null
                    withContext(Dispatchers.IO) {
                        Socks5ProxyBridge.start(
                            remoteHost = "127.0.0.1",
                            remotePort = SoarBridge.getClientPort(),
                            remoteUsername = null,
                            remotePassword = null,
                            listenPort = layerPort,
                            listenHost = layerHost,
                            dnsHost = remoteDns,
                            dnsFallback = remoteDnsFallback
                        )
                    }
                } else {
                    vpnRepository.startSoarProxy(
                        profile, portOverride = layerPort, hostOverride = "127.0.0.1",
                        socksProxyAddr = upstream, socksProxyUser = prevProfile?.socksUsername,
                        socksProxyPass = prevProfile?.socksPassword
                    )
                }
            }
            TunnelType.SLIPSTREAM -> {
                SlipstreamBridge.setVpnService(this@SlipNetVpnService)
                if (isLast) {
                    val internalPort = internalPortBase
                    val proxyResult = vpnRepository.startSlipstreamProxy(profile, portOverride = internalPort, hostOverride = "127.0.0.1")
                    if (proxyResult.isFailure) return proxyResult
                    SlipstreamSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    vpnRepository.startSlipstreamSocksBridge(
                        slipstreamPort = internalPort, slipstreamHost = "127.0.0.1",
                        bridgePort = layerPort, bridgeHost = layerHost,
                        socksUsername = profile.socksUsername, socksPassword = profile.socksPassword,
                        dnsServer = remoteDns, dnsFallback = remoteDnsFallback
                    )
                } else {
                    vpnRepository.startSlipstreamProxy(profile, portOverride = layerPort, hostOverride = "127.0.0.1")
                }
            }
            TunnelType.SSH -> {
                // Create a named SSH instance for this chain layer
                val sshInstance = SshTunnelBridge.createInstance("chain-ssh-$layerIndex")
                configureSshInstance(sshInstance, profile)

                if (prevPort > 0) {
                    val resolvedPrevType = prevLayerType
                        ?: return Result.failure(Exception("No previous layer for SSH"))
                    val output = app.slipnet.domain.model.ChainValidation.outputType(resolvedPrevType)

                    // In a chain, SSH connects to the remote server through the previous
                    // layer's proxy, so use `domain` (the actual server address).
                    // sshHost defaults to 127.0.0.1 for DNSTT+SSH co-located scenarios
                    // which is wrong when chaining through an external proxy.
                    val chainSshHost = profile.domain.ifEmpty { profile.sshHost }

                    withContext(Dispatchers.IO) {
                        when (output) {
                            app.slipnet.domain.model.LayerOutput.RAW_TCP -> {
                                Log.i(TAG, "Starting SSH[$layerIndex] over raw TCP ($chainSshHost:${profile.sshPort} via $prevHost:$prevPort)")
                                sshInstance.startOverProxy(
                                    sshHost = chainSshHost,
                                    sshPort = profile.sshPort,
                                    sshUsername = profile.sshUsername,
                                    sshPassword = profile.sshPassword,
                                    proxyHost = prevHost,
                                    proxyPort = prevPort,
                                    listenPort = layerPort,
                                    listenHost = layerHost,
                                    blockDirectDns = true,
                                    sshAuthType = profile.sshAuthType,
                                    sshPrivateKey = profile.sshPrivateKey,
                                    sshKeyPassphrase = profile.sshKeyPassphrase,
                                    remoteDnsHost = remoteDns,
                                    remoteDnsFallback = remoteDnsFallback
                                )
                            }
                            app.slipnet.domain.model.LayerOutput.SOCKS5 -> {
                                Log.i(TAG, "Starting SSH[$layerIndex] over SOCKS5 ($chainSshHost:${profile.sshPort} via $prevHost:$prevPort)")
                                sshInstance.startOverSocks5Proxy(
                                    sshHost = chainSshHost,
                                    sshPort = profile.sshPort,
                                    sshUsername = profile.sshUsername,
                                    sshPassword = profile.sshPassword,
                                    proxyHost = prevHost,
                                    proxyPort = prevPort,
                                    socksUsername = if (prevLayerType == TunnelType.XRAY) coreEndpoint?.user else prevProfile?.socksUsername,
                                    socksPassword = if (prevLayerType == TunnelType.XRAY) coreEndpoint?.password else prevProfile?.socksPassword,
                                    listenPort = layerPort,
                                    listenHost = layerHost,
                                    blockDirectDns = preferencesDataStore.preventDnsFallback.first(),
                                    sshAuthType = profile.sshAuthType,
                                    sshPrivateKey = profile.sshPrivateKey,
                                    sshKeyPassphrase = profile.sshKeyPassphrase,
                                    remoteDnsHost = remoteDns,
                                    remoteDnsFallback = remoteDnsFallback
                                )
                            }
                            null -> Result.failure(Exception("Previous layer cannot provide output for SSH"))
                        }
                    }
                } else {
                    withContext(Dispatchers.IO) {
                        sshInstance.startDirect(
                            tunnelHost = profile.domain,
                            tunnelPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            listenPort = layerPort,
                            listenHost = layerHost,
                            forwardDnsThroughSsh = true,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback,
                            tlsEnabled = profile.sshTlsEnabled,
                            tlsSni = profile.sshTlsSni,
                            sshPayload = profile.sshPayload
                        )
                    }
                }
            }
            TunnelType.NAIVE -> {
                if (isLast && prevPort <= 0) {
                    val internalPort = internalPortBase
                    val naiveResult = withContext(Dispatchers.IO) {
                        NaiveBridge.start(
                            context = this@SlipNetVpnService,
                            listenPort = internalPort,
                            serverHost = profile.domain,
                            serverPort = profile.naivePort,
                            username = profile.naiveUsername,
                            password = profile.naivePassword
                        )
                    }
                    if (naiveResult.isFailure) return naiveResult
                    vpnRepository.startNaiveSocksBridge(
                        naivePort = internalPort, naiveHost = "127.0.0.1",
                        bridgePort = layerPort, bridgeHost = layerHost,
                        dnsServer = remoteDns, dnsFallback = remoteDnsFallback
                    )
                } else {
                    withContext(Dispatchers.IO) {
                        NaiveBridge.start(
                            context = this@SlipNetVpnService,
                            listenPort = layerPort,
                            serverHost = profile.domain,
                            serverPort = profile.naivePort,
                            username = profile.naiveUsername,
                            password = profile.naivePassword
                        )
                    }
                }
            }
            TunnelType.SNOWFLAKE -> {
                val ptPort = internalPortBase + 1
                val torPort = internalPortBase
                // If the previous layer provides SOCKS5 (e.g. a DoH layer),
                // route Tor + lyrebird PTs through it so bridge/CDN contact
                // rides that layer instead of going direct.
                val upstream = if (prevPort > 0 && prevLayerType != null &&
                    app.slipnet.domain.model.ChainValidation.outputType(prevLayerType) ==
                    app.slipnet.domain.model.LayerOutput.SOCKS5
                ) {
                    java.net.InetSocketAddress(prevHost, prevPort).also {
                        Log.i(TAG, "Snowflake[$layerIndex]: routing upstream through SOCKS5 $it")
                    }
                } else null
                // The built-in Snowflake Go client ignores TOR_PT_PROXY, so chaining
                // it behind another SOCKS5 layer gives no censorship benefit — the
                // broker contact still goes direct. Surface this as a hard error
                // instead of letting Tor stall forever on unreachable brokers.
                if (upstream != null) {
                    val lines = profile.torBridgeLines.trim()
                    val isBuiltIn = lines.isBlank() || lines == "SNOWFLAKE_AMP" || lines == "SMART"
                    if (isBuiltIn) {
                        return Result.failure(RuntimeException(
                            "Built-in Snowflake cannot be chained behind another layer. " +
                            "Use an obfs4/meek_lite/webtunnel bridge line for ${profile.name}, " +
                            "or remove the preceding layer."
                        ))
                    }
                }
                vpnRepository.startSnowflakeProxy(profile, ptPort, torPort, layerPort, upstream)
            }
            TunnelType.DOH -> {
                // DoH is final-only in chains. If the previous layer provides
                // SOCKS5 (e.g. Snowflake/Tor), thread it through so DoH HTTPS
                // and TCP passthrough ride the outer circuit.
                val upstream = if (prevPort > 0 && prevLayerType != null &&
                    app.slipnet.domain.model.ChainValidation.outputType(prevLayerType) ==
                    app.slipnet.domain.model.LayerOutput.SOCKS5
                ) {
                    java.net.InetSocketAddress(prevHost, prevPort).also {
                        Log.i(TAG, "DoH[$layerIndex]: routing upstream through SOCKS5 $it")
                    }
                } else null
                vpnRepository.startDohProxy(
                    profile = profile,
                    portOverride = layerPort,
                    hostOverride = layerHost,
                    upstreamSocksAddr = upstream
                )
            }
            TunnelType.SOCKS5 -> {
                val socksInstance = Socks5ProxyBridge.createInstance("chain-socks5-$layerIndex")
                socksInstance.debugLogging = Socks5ProxyBridge.debugLogging
                socksInstance.domainRouter = Socks5ProxyBridge.domainRouter
                val ulKbpsChain = preferencesDataStore.uploadLimitKbps.first()
                val dlKbpsChain = preferencesDataStore.downloadLimitKbps.first()
                socksInstance.uploadLimiter = if (ulKbpsChain > 0) RateLimiter(ulKbpsChain.toLong() * 1024) else null
                socksInstance.downloadLimiter = if (dlKbpsChain > 0) RateLimiter(dlKbpsChain.toLong() * 1024) else null
                withContext(Dispatchers.IO) {
                    socksInstance.start(
                        remoteHost = profile.domain,
                        remotePort = profile.socks5ServerPort,
                        remoteUsername = profile.socksUsername,
                        remotePassword = profile.socksPassword,
                        listenPort = layerPort,
                        listenHost = layerHost,
                        dnsHost = remoteDns
                    )
                }
            }
            else -> Result.failure(Exception("${profile.tunnelType.displayName} cannot be used in a chain"))
        }
    }

    /** Stop a single tunnel layer by type. */
    private fun stopLayer(type: TunnelType) {
        when (type) {
            TunnelType.DNSTT, TunnelType.NOIZDNS -> {
                DnsttSocksBridge.stop()
                DnsttBridge.stopClient()
            }
            TunnelType.VAYDNS -> {
                DnsttSocksBridge.stop()
                VaydnsBridge.stopClient()
            }
            TunnelType.SOAR -> {
                Socks5ProxyBridge.stop()
                SoarBridge.stopClient()
            }
            TunnelType.SLIPSTREAM -> {
                SlipstreamSocksBridge.stop()
                SlipstreamBridge.stopClient()
            }
            TunnelType.SSH -> SshTunnelBridge.stopAll()
            TunnelType.NAIVE -> {
                NaiveSocksBridge.stop()
                NaiveBridge.stop()
            }
            TunnelType.SNOWFLAKE -> {
                TorSocksBridge.stop()
                SnowflakeBridge.stopClient()
            }
            TunnelType.DOH -> DohBridge.stop()
            TunnelType.SOCKS5 -> Socks5ProxyBridge.stopAll()
            TunnelType.XRAY -> XrayBridge.stop()
            TunnelType.VLESS -> VlessBridge.stop()
            else -> {}
        }
    }

    /**
     * Connect using Slipstream tunnel type.
     * Order: Set VpnService ref -> Start proxy -> Wait for ready -> Wait for QUIC
     *        -> VPN interface (with addDisallowedApplication) -> Start bridge -> tun2socks on bridge port
     *
     * Slipstream's SOCKS5 proxy only supports CONNECT (no auth, no FWD_UDP).
     * SlipstreamSocksBridge sits between hev-socks5-tunnel and Slipstream:
     * - CONNECT: chains to Slipstream's SOCKS5 proxy
     * - FWD_UDP: forwards DNS/UDP directly via DatagramSocket
     *
     * addDisallowedApplication ensures the bridge's DatagramSockets bypass VPN.
     */
    private suspend fun connectSlipstream(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val slipstreamPort = proxyPort + 1

        // Step 1: Set VpnService reference for socket protection via JNI
        SlipstreamBridge.proxyOnlyMode = isProxyOnly
        SlipstreamBridge.setVpnService(this@SlipNetVpnService)

        // Step 2: Start Slipstream proxy on internal port (127.0.0.1 only)
        val proxyResult = vpnRepository.startSlipstreamProxy(profile, portOverride = slipstreamPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start proxy")
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualSlipstreamPort = SlipstreamBridge.getClientPort()
        if (actualSlipstreamPort != slipstreamPort) {
            Log.i(TAG, "Slipstream bound to alternative port $actualSlipstreamPort (preferred $slipstreamPort was stuck)")
        }

        // Step 2.5: Verify proxy is listening
        if (!waitForProxyReady(actualSlipstreamPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualSlipstreamPort)
            return
        }

        // Step 2.6: Wait for QUIC handshake before VPN setup
        val quicReady = waitForQuicReady(maxAttempts = 150, delayMs = 200)
        if (!quicReady) {
            reportConnectionError("Slipstream tunnel failed to connect (QUIC timeout)")
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Start SlipstreamSocksBridge on proxyPort (user-facing, with auth for Dante)
        // DNS target resolved at the REMOTE server (through Dante),
        // not locally. Using local/ISP DNS would give poisoned results in censored networks.
        SlipstreamSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
        val bridgeResult = vpnRepository.startSlipstreamSocksBridge(
            slipstreamPort = actualSlipstreamPort,
            slipstreamHost = "127.0.0.1",
            bridgePort = proxyPort,
            bridgeHost = proxyHost,
            socksUsername = profile.socksUsername,
            socksPassword = profile.socksPassword,
            dnsServer = remoteDns,
            dnsFallback = remoteDnsFallback
        )
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start SOCKS5 bridge")
            stopCurrentProxy()
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3.5: Verify bridge is listening
        if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
            Log.e(TAG, "SlipstreamSocksBridge failed to become ready on port $proxyPort")
            reportConnectionError("SOCKS5 bridge failed to start")
            stopCurrentProxy()
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip VPN interface and tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Slipstream SOCKS5 bridge ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 4: Establish VPN interface (with addDisallowedApplication)
        // App exclusion ensures SlipstreamSocksBridge's DNS DatagramSockets bypass VPN
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopCurrentProxy()
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 5: Start tun2socks pointing at bridge on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        finishConnection()
    }

    /**
     * Connect using Slipstream+SSH tunnel type.
     * Order: Set VpnService ref -> Start Slipstream -> Wait for ready -> Wait for QUIC (hard req)
     *        -> Start SSH directly through Slipstream tunnel (loopback, no VPN needed)
     *        -> Wait for SSH ready -> VPN interface -> tun2socks on SSH port
     *
     * Slipstream in SSH mode is a raw TCP tunnel to the SSH server (like DNSTT).
     * The server's --target-address points to SSH port 22, so all tunnel traffic
     * goes directly to SSH. JSch connects directly to Slipstream's local port —
     * no ProxySOCKS5 or Dante needed.
     *
     * VPN interface is established AFTER SSH connects. The SSH connection goes
     * through loopback (JSch -> 127.0.0.1:proxyPort+1 -> Slipstream), so VPN routing
     * isn't needed. Establishing VPN too early can disrupt QUIC establishment.
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> SSH SOCKS5 (proxyPort)
     *   -> SSH direct-tcpip -> Slipstream (proxyPort+1, 127.0.0.1, raw TCP tunnel)
     *   -> DNS tunnel (UDP 53) -> Slipstream Server -> SSH Server -> Internet
     */
    /**
     * Build a DomainRouter from DataStore preferences.
     */
    private suspend fun buildDomainRouter(): DomainRouter {
        val domainRoutingEnabled = preferencesDataStore.domainRoutingEnabled.first()
        val geoBypassEnabled = preferencesDataStore.geoBypassEnabled.first()

        if (!domainRoutingEnabled && !geoBypassEnabled) return DomainRouter.DISABLED

        val mode = preferencesDataStore.domainRoutingMode.first()
        val domains = if (domainRoutingEnabled) preferencesDataStore.domainRoutingDomains.first() else emptySet()

        val geoData = if (geoBypassEnabled) {
            val countryCode = preferencesDataStore.geoBypassCountry.first()
            val country = GeoBypassCountry.fromCode(countryCode)
            Log.i(TAG, "Geo-bypass enabled: country=${country.displayName}")
            DomainRouter.loadGeoData(this, country)
        } else {
            GeoBypassData.EMPTY
        }

        if (domainRoutingEnabled) {
            Log.i(TAG, "Domain routing enabled: mode=$mode, ${domains.size} domains")
        }

        return DomainRouter(
            enabled = domainRoutingEnabled || geoBypassEnabled,
            mode = mode,
            domains = domains,
            geoBypassEnabled = geoBypassEnabled,
            geoBypass = geoData
        )
    }

    /**
     * Read SSH tunnel settings from DataStore and apply to SshTunnelBridge.
     * Must be called before any SshTunnelBridge.start* method.
     *
     * If the user hasn't manually set Max Channels, uses a tunnel-type-aware default:
     * - SSH-only: 32 (direct TCP, good bandwidth)
     * - Slipstream+SSH: 24 (moderate throughput)
     * - DNSTT+SSH: 12 (limited DNS throughput, too many channels choke the tunnel)
     */
    private suspend fun configureSshBridge() {
        SshTunnelBridge.startupCancellation = startupCancellation
        connectionManager.getProfileById(currentProfileId)?.let { p ->
            SshTunnelBridge.trustIdentity = p.sshTrustIdentity
        }
        val cipher = preferencesDataStore.sshCipher.first()
        val compression = preferencesDataStore.sshCompression.first()
        val isCustom = preferencesDataStore.sshMaxChannelsIsCustom.first()
        val maxChannels = if (isCustom) {
            preferencesDataStore.sshMaxChannels.first()
        } else {
            when (currentTunnelType) {
                TunnelType.SSH -> 32
                TunnelType.NAIVE_SSH -> 32
                TunnelType.SLIPSTREAM_SSH -> 24
                TunnelType.DNSTT_SSH -> 12
                TunnelType.NOIZDNS_SSH -> 12
                TunnelType.VAYDNS_SSH -> 12
                else -> 16
            }
        }
        SshTunnelBridge.configure(
            cipher = cipher.jschConfig,
            compression = compression,
            maxChannels = maxChannels
        )
    }

    private suspend fun configureSshInstance(instance: app.slipnet.tunnel.SshTunnelInstance, profile: app.slipnet.domain.model.ServerProfile) {
        instance.startupCancellation = startupCancellation
        instance.trustIdentity = profile.sshTrustIdentity
        val cipher = preferencesDataStore.sshCipher.first()
        val compression = preferencesDataStore.sshCompression.first()
        val isCustom = preferencesDataStore.sshMaxChannelsIsCustom.first()
        val maxChannels = if (isCustom) preferencesDataStore.sshMaxChannels.first() else 32
        instance.configure(
            cipher = cipher.jschConfig,
            compression = compression,
            maxChannels = maxChannels
        )
        // Bandwidth limiting
        val ulKbps = preferencesDataStore.uploadLimitKbps.first()
        val dlKbps = preferencesDataStore.downloadLimitKbps.first()
        instance.uploadLimiter = if (ulKbps > 0) RateLimiter(ulKbps.toLong() * 1024) else null
        instance.downloadLimiter = if (dlKbps > 0) RateLimiter(dlKbps.toLong() * 1024) else null
    }

    private suspend fun connectSlipstreamSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val slipstreamPort = proxyPort + 1

        // Step 1: Set VpnService reference for socket protection via JNI
        SlipstreamBridge.proxyOnlyMode = isProxyOnly
        SlipstreamBridge.setVpnService(this@SlipNetVpnService)

        // Step 2: Start Slipstream tunnel on internal port (127.0.0.1 only)
        val proxyResult = vpnRepository.startSlipstreamProxy(profile, portOverride = slipstreamPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start Slipstream proxy")
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualSlipstreamPort = SlipstreamBridge.getClientPort()
        if (actualSlipstreamPort != slipstreamPort) {
            Log.i(TAG, "Slipstream bound to alternative port $actualSlipstreamPort (preferred $slipstreamPort was stuck)")
        }

        // Step 2.5: Verify Slipstream is listening
        if (!waitForProxyReady(actualSlipstreamPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualSlipstreamPort)
            return
        }

        // Step 2.6: Wait for QUIC handshake — REQUIRED for Slipstream+SSH.
        // SSH needs a working QUIC tunnel before JSch can connect through it.
        // Wait longer than plain Slipstream (30s vs 5s) since this is a hard requirement.
        val quicReady = waitForQuicReady(maxAttempts = 150, delayMs = 200)
        if (!quicReady) {
            Log.e(TAG, "QUIC connection not ready — cannot establish SSH through Slipstream")
            reportConnectionError("Slipstream tunnel failed to connect (QUIC timeout)")
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Switch tunnel type to SLIPSTREAM_SSH before starting SSH
        vpnRepository.setCurrentTunnelType(TunnelType.SLIPSTREAM_SSH)
        currentTunnelType = TunnelType.SLIPSTREAM_SSH

        // Step 4: Start SSH tunnel directly through Slipstream (with retry)
        // Slipstream in SSH mode is a raw TCP tunnel (like DNSTT) — the server's
        // --target-address forwards all traffic to SSH. JSch connects directly to
        // Slipstream's local port, no SOCKS5 proxy wrapper needed.
        configureSshBridge()
        var sshResult: Result<Unit> = Result.failure(RuntimeException("SSH not attempted"))
        for (attempt in 1..SSH_OVER_TUNNEL_RETRIES) {
            sshResult = withContext(Dispatchers.IO) {
                Log.i(TAG, "Starting SSH tunnel through Slipstream (${profile.sshHost}:${profile.sshPort} via 127.0.0.1:$actualSlipstreamPort) attempt $attempt/$SSH_OVER_TUNNEL_RETRIES")
                SshTunnelBridge.startOverProxy(
                    sshHost = profile.sshHost,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = "127.0.0.1",
                    proxyPort = actualSlipstreamPort,
                    listenPort = proxyPort,
                    listenHost = proxyHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback
                )
            }
            if (sshResult.isSuccess) break
            if (attempt < SSH_OVER_TUNNEL_RETRIES) {
                Log.w(TAG, "SSH over Slipstream attempt $attempt failed: ${sshResult.exceptionOrNull()?.message}, retrying...")
                delay(1000L * attempt)
            }
        }
        if (sshResult.isFailure) {
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over Slipstream")
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 4.5: Wait for SSH SOCKS5 proxy to be ready
        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready on port $proxyPort")
            reportConnectionError("SSH tunnel failed to start over Slipstream")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip VPN interface and tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Slipstream+SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 5: Establish VPN interface (with addDisallowedApplication — needed for geo-bypass
        // direct sockets; Slipstream QUIC sockets are also protected via JNI)
        // Done after SSH is connected to avoid disrupting QUIC during startup
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 6: Start tun2socks pointing at SSH SOCKS5 on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { SlipstreamBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            SlipstreamBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(500)
        Log.d(TAG, "Slipstream+SSH tunnel started")
        finishConnection()
    }

    /**
     * Connect using DNSTT tunnel type.
     * Order: Establish VPN interface (with app exclusion) -> Start proxy -> Wait for ready -> tun2socks
     *
     * IMPORTANT: VPN must be established BEFORE starting DNSTT so that addDisallowedApplication
     * is in effect when DNSTT creates its UDP sockets. This prevents a routing loop where
     * DNSTT's DNS queries would be captured by the VPN.
     */
    private suspend fun connectDnstt(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val dnsttPort = proxyPort + 1

        // Step 1: Set VpnService reference (for potential future use)
        DnsttBridge.setVpnService(this@SlipNetVpnService)

        // Step 2: Establish VPN interface FIRST (with addDisallowedApplication for this app)
        // This ensures DNSTT's sockets bypass the VPN when created
        if (!isProxyOnly) {
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                DnsttBridge.setVpnService(null)
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Brief delay to let VPN routing settle (as reference app does)
            delay(200)
        }

        // Step 3: Start DNSTT/NoizDNS on internal port (its sockets bypass VPN due to app exclusion)
        val isNoizdns = profile.tunnelType == TunnelType.NOIZDNS || profile.tunnelType == TunnelType.NOIZDNS_SSH
        val proxyResult = if (isNoizdns) {
            vpnRepository.startNoizdnsProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        } else {
            vpnRepository.startDnsttProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        }
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start ${if (isNoizdns) "NoizDNS" else "DNSTT"} proxy")
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualDnsttPort = DnsttBridge.getClientPort()
        if (actualDnsttPort != dnsttPort) {
            Log.i(TAG, "DNSTT bound to alternative port $actualDnsttPort (preferred $dnsttPort was stuck)")
        }

        // Step 3.5: Verify DNSTT is listening on internal port
        if (!waitForProxyReady(actualDnsttPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualDnsttPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 4: Start DnsttSocksBridge on proxyPort (user-facing, with auth for Dante)
        // DNS target resolved at the REMOTE server (through Dante),
        // not locally. Using local/ISP DNS would give poisoned results in censored networks.
        DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
        DnsttSocksBridge.proxyOnlyMode = isProxyOnly
        DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
        val bridgeResult = vpnRepository.startDnsttSocksBridge(
            dnsttPort = actualDnsttPort,
            dnsttHost = "127.0.0.1",
            bridgePort = proxyPort,
            bridgeHost = proxyHost,
            socksUsername = profile.socksUsername,
            socksPassword = profile.socksPassword,
            dnsServer = remoteDns,
            dnsFallback = remoteDnsFallback
        )
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start DNSTT SOCKS5 bridge")
            stopCurrentProxy()
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 4.5: Verify bridge is listening
        if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
            Log.e(TAG, "DnsttSocksBridge failed to become ready on port $proxyPort")
            reportConnectionError("DNSTT SOCKS5 bridge failed to start")
            stopCurrentProxy()
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: DNSTT SOCKS5 bridge ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 5: Start tun2socks pointing at bridge on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Give DNSTT a moment to establish the connection
        delay(500)
        Log.d(TAG, "DNSTT tunnel started with bridge")

        finishConnection()
    }

    /**
     * Connect using SSH-only tunnel type.
     * Order: Establish VPN interface (with app exclusion) -> Start SSH tunnel -> Wait for ready -> tun2socks
     *
     * SSH connects directly to the remote server (no DNS tunneling).
     * DNS queries go direct via DatagramSocket (bypasses VPN via addDisallowedApplication).
     */
    private suspend fun connectSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalDnsIp: String? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        vpnRepository.setCurrentTunnelType(TunnelType.SSH)

        // Resolve SSH hostname via global DNS if set (bypasses ISP DNS filtering)
        val resolvedProfile = if (globalDnsIp != null && !DomainRouter.isIpAddress(profile.domain)) {
            val resolvedDomain = VpnRepositoryImpl.resolveHost(profile.domain, globalDnsIp)
            if (resolvedDomain != profile.domain) {
                Log.i(TAG, "Resolved SSH host ${profile.domain} → $resolvedDomain via global DNS")
                profile.copy(domain = resolvedDomain)
            } else profile
        } else profile

        if (isProxyOnly) {
            // Proxy-only: no VPN needed, start SSH directly (sockets go direct anyway)
            configureSshBridge()
            val sshResult = withContext(Dispatchers.IO) {
                startSshWithTransport(resolvedProfile, proxyHost, proxyPort, remoteDns, remoteDnsFallback)
            }
            if (sshResult.isFailure) {
                reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
                Log.e(TAG, "SSH SOCKS5 proxy failed to become ready")
                reportConnectionError("SSH tunnel failed to start")
                withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface with addDisallowedApplication
        // This ensures SSH's socket and DNS queries bypass the VPN
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Brief delay to let VPN routing settle
        delay(200)

        // Step 2: Start SSH tunnel using the configured transport
        configureSshBridge()
        val sshResult = withContext(Dispatchers.IO) {
            startSshWithTransport(resolvedProfile, proxyHost, proxyPort, remoteDns, remoteDnsFallback)
        }
        if (sshResult.isFailure) {
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 2.5: Wait for SSH SOCKS5 proxy to be ready
        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready")
            reportConnectionError("SSH tunnel failed to start")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "SSH tunnel started")
        finishConnection()
    }

    /**
     * Start SSH tunnel using the transport configured in the profile:
     * WebSocket, HTTP CONNECT proxy, or direct (with optional TLS and payload).
     */
    private fun startSshWithTransport(
        profile: app.slipnet.domain.model.ServerProfile,
        listenHost: String,
        listenPort: Int,
        remoteDns: String,
        remoteDnsFallback: String
    ): Result<Unit> {
        return when {
            profile.sshWsEnabled -> {
                Log.i(TAG, "Starting SSH tunnel (WebSocket to ${profile.domain}:${profile.sshPort})")
                SshTunnelBridge.startOverWebSocket(
                    sshHost = profile.domain,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    wsPath = profile.sshWsPath,
                    wsUseTls = profile.sshWsUseTls,
                    wsCustomHost = profile.sshWsCustomHost,
                    wsTlsSni = profile.sshTlsSni,
                    listenPort = listenPort,
                    listenHost = listenHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback
                )
            }
            profile.sshHttpProxyHost.isNotBlank() -> {
                Log.i(TAG, "Starting SSH tunnel (HTTP proxy ${profile.sshHttpProxyHost}:${profile.sshHttpProxyPort} -> ${profile.domain}:${profile.sshPort})")
                SshTunnelBridge.startOverHttpProxy(
                    sshHost = profile.domain,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = profile.sshHttpProxyHost,
                    proxyPort = profile.sshHttpProxyPort,
                    customHostHeader = profile.sshHttpProxyCustomHost,
                    listenPort = listenPort,
                    listenHost = listenHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback,
                    tlsEnabled = profile.sshTlsEnabled,
                    tlsSni = profile.sshTlsSni
                )
            }
            else -> {
                Log.i(TAG, "Starting SSH tunnel (direct to ${profile.domain}:${profile.sshPort})")
                SshTunnelBridge.startDirect(
                    tunnelHost = profile.domain,
                    tunnelPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    listenPort = listenPort,
                    listenHost = listenHost,
                    forwardDnsThroughSsh = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback,
                    tlsEnabled = profile.sshTlsEnabled,
                    tlsSni = profile.sshTlsSni,
                    sshPayload = profile.sshPayload
                )
            }
        }
    }

    /**
     * Connect using NaiveProxy + SSH tunnel type.
     * NaiveProxy provides a local SOCKS5 proxy that tunnels through Caddy's
     * forwardproxy with authentic Chrome TLS fingerprinting. JSch connects
     * through NaiveProxy's SOCKS5 to reach the SSH server.
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> SshTunnelBridge SOCKS5 (proxyPort)
     *   -> SSH direct-tcpip -> [JSch via ProxySOCKS5] -> NaiveProxy SOCKS5 (proxyPort+1)
     *   -> Chrome TLS/HTTP2 CONNECT -> Caddy Server (forwardproxy) -> SSH:22
     */
    private suspend fun connectNaiveSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String) {
        vpnRepository.setCurrentTunnelType(TunnelType.NAIVE_SSH)
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val naivePort = proxyPort + 1

        if (isProxyOnly) {
            // Proxy-only: no VPN needed, start NaiveProxy + SSH directly
            val naiveResult = withContext(Dispatchers.IO) {
                NaiveBridge.start(
                    context = this@SlipNetVpnService,
                    listenPort = naivePort,
                    listenHost = "127.0.0.1",
                    serverHost = profile.domain,
                    serverPort = profile.naivePort,
                    username = profile.naiveUsername,
                    password = profile.naivePassword,
                )
            }
            if (naiveResult.isFailure) {
                reportConnectionError(naiveResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
                Log.e(TAG, "NaiveProxy failed to become ready on port $naivePort")
                reportConnectionError("NaiveProxy failed to start")
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // For NaiveProxy: use server domain instead of 127.0.0.1 as SSH target.
            // NaiveProxy (Chromium) may bypass the HTTPS proxy for loopback addresses,
            // causing SOCKS5 CONNECT to 127.0.0.1 to connect locally instead of remotely.
            val naiveSshHost = if (profile.sshHost == "127.0.0.1" || profile.sshHost.isBlank()) {
                profile.domain
            } else {
                profile.sshHost
            }

            configureSshBridge()
            val sshResult = withContext(Dispatchers.IO) {
                Log.i(TAG, "Starting SSH tunnel through NaiveProxy ($naiveSshHost:${profile.sshPort} via 127.0.0.1:$naivePort)")
                val blockDns = preferencesDataStore.preventDnsFallback.first()
                SshTunnelBridge.startOverSocks5Proxy(
                    sshHost = naiveSshHost,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = "127.0.0.1",
                    proxyPort = naivePort,
                    socksUsername = null,
                    socksPassword = null,
                    listenPort = proxyPort,
                    listenHost = proxyHost,
                    blockDirectDns = blockDns,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback,
                    naiveMode = true
                )
            }
            if (sshResult.isFailure) {
                reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over NaiveProxy")
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
                Log.e(TAG, "SSH SOCKS5 proxy failed to become ready")
                reportConnectionError("SSH tunnel failed to start over NaiveProxy")
                withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: NaiveProxy+SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface with addDisallowedApplication
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(200)

        // Step 2: Start NaiveProxy
        val naiveResult = withContext(Dispatchers.IO) {
            NaiveBridge.start(
                context = this@SlipNetVpnService,
                listenPort = naivePort,
                listenHost = "127.0.0.1",
                serverHost = profile.domain,
                serverPort = profile.naivePort,
                username = profile.naiveUsername,
                password = profile.naivePassword
            )
        }
        if (naiveResult.isFailure) {
            reportConnectionError(naiveResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Wait for NaiveProxy ready (use isReady flag, not TCP connect, to avoid
        // ERR_SOCKS_CONNECTION_FAILED noise from non-SOCKS5 probe)
        if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
            Log.e(TAG, "NaiveProxy failed to become ready on port $naivePort")
            reportConnectionError("NaiveProxy failed to start")
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: SSH over NaiveProxy's SOCKS5
        // Use server domain instead of 127.0.0.1 to avoid Chromium loopback bypass
        val naiveSshHost = if (profile.sshHost == "127.0.0.1" || profile.sshHost.isBlank()) {
            profile.domain
        } else {
            profile.sshHost
        }

        configureSshBridge()
        val blockDns = preferencesDataStore.preventDnsFallback.first()
        val sshResult = withContext(Dispatchers.IO) {
            Log.i(TAG, "Starting SSH tunnel through NaiveProxy ($naiveSshHost:${profile.sshPort} via 127.0.0.1:$naivePort)")
            SshTunnelBridge.startOverSocks5Proxy(
                sshHost = naiveSshHost,
                sshPort = profile.sshPort,
                sshUsername = profile.sshUsername,
                sshPassword = profile.sshPassword,
                proxyHost = "127.0.0.1",
                proxyPort = naivePort,
                socksUsername = null,
                socksPassword = null,
                listenPort = proxyPort,
                listenHost = proxyHost,
                blockDirectDns = blockDns,
                sshAuthType = profile.sshAuthType,
                sshPrivateKey = profile.sshPrivateKey,
                sshKeyPassphrase = profile.sshKeyPassphrase,
                remoteDnsHost = remoteDns,
                remoteDnsFallback = remoteDnsFallback,
                naiveMode = true
            )
        }
        if (sshResult.isFailure) {
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over NaiveProxy")
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3.5: Wait for SSH SOCKS5 proxy to be ready
        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready on port $proxyPort")
            reportConnectionError("SSH tunnel failed to start over NaiveProxy")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 4: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "NaiveProxy+SSH tunnel started")
        finishConnection()
    }

    /**
     * Connect using standalone NaiveProxy tunnel type.
     * Order: VPN interface (with addDisallowedApplication) -> NaiveProxy on proxyPort+1
     *        -> Wait for ready -> NaiveSocksBridge on proxyPort -> tun2socks on proxyPort
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> NaiveSocksBridge (proxyPort, NO_AUTH)
     *   TCP CONNECT: -> NaiveProxy SOCKS5 (proxyPort+1, NO_AUTH) -> Caddy HTTPS -> Internet
     *   DNS FWD_UDP: -> worker pool (SOCKS5 CONNECT through NaiveProxy to DNS) -> DNS-over-TCP
     */
    private suspend fun connectNaive(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String) {
        vpnRepository.setCurrentTunnelType(TunnelType.NAIVE)
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val naivePort = proxyPort + 1

        if (isProxyOnly) {
            // Proxy-only: no VPN needed, start NaiveProxy + bridge directly
            val naiveResult = withContext(Dispatchers.IO) {
                NaiveBridge.start(
                    context = this@SlipNetVpnService,
                    listenPort = naivePort,
                    listenHost = "127.0.0.1",
                    serverHost = profile.domain,
                    serverPort = profile.naivePort,
                    username = profile.naiveUsername,
                    password = profile.naivePassword,
                )
            }
            if (naiveResult.isFailure) {
                reportConnectionError(naiveResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
                Log.e(TAG, "NaiveProxy failed to become ready on port $naivePort")
                reportConnectionError("NaiveProxy failed to start")
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Start NaiveSocksBridge on proxyPort
            val bridgeResult = vpnRepository.startNaiveSocksBridge(
                naivePort = naivePort,
                naiveHost = "127.0.0.1",
                bridgePort = proxyPort,
                bridgeHost = proxyHost,
                dnsServer = remoteDns,
                dnsFallback = remoteDnsFallback
            )
            if (bridgeResult.isFailure) {
                reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy bridge")
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
                Log.e(TAG, "NaiveSocksBridge failed to become ready on port $proxyPort")
                reportConnectionError("NaiveProxy bridge failed to start")
                withContext(Dispatchers.IO) { NaiveSocksBridge.stop() }
                withContext(Dispatchers.IO) { NaiveBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: NaiveProxy SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface with addDisallowedApplication
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(200)

        // Step 2: Start NaiveProxy on internal port
        val naiveResult = withContext(Dispatchers.IO) {
            NaiveBridge.start(
                context = this@SlipNetVpnService,
                listenPort = naivePort,
                listenHost = "127.0.0.1",
                serverHost = profile.domain,
                serverPort = profile.naivePort,
                username = profile.naiveUsername,
                password = profile.naivePassword
            )
        }
        if (naiveResult.isFailure) {
            reportConnectionError(naiveResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
            Log.e(TAG, "NaiveProxy failed to become ready on port $naivePort")
            reportConnectionError("NaiveProxy failed to start")
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Start NaiveSocksBridge on proxyPort
        val bridgeResult = vpnRepository.startNaiveSocksBridge(
            naivePort = naivePort,
            naiveHost = "127.0.0.1",
            bridgePort = proxyPort,
            bridgeHost = proxyHost,
            dnsServer = remoteDns,
            dnsFallback = remoteDnsFallback
        )
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start NaiveProxy bridge")
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "NaiveSocksBridge failed to become ready on port $proxyPort")
            reportConnectionError("NaiveProxy bridge failed to start")
            withContext(Dispatchers.IO) { NaiveSocksBridge.stop() }
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 4: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { NaiveSocksBridge.stop() }
            withContext(Dispatchers.IO) { NaiveBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "Standalone NaiveProxy tunnel started")
        finishConnection()
    }

    /**
     * Connect using DNSTT+SSH tunnel type.
     * Order: Set DNSTT ref -> VPN interface (with app exclusion) -> Start DNSTT -> Wait for DNSTT ready
     *        -> Start SSH over DNSTT proxy -> Wait for SSH ready -> Start tun2socks on proxyPort
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> SSH SOCKS5 (proxyPort)
     *   -> SSH direct-tcpip -> DNSTT (proxyPort+1, 127.0.0.1, raw TCP tunnel)
     *   -> DNS tunnel (UDP 53) -> DNSTT Server -> SSH Server -> Internet
     */
    private suspend fun connectDnsttSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val dnsttPort = proxyPort + 1

        // Step 1: Set VpnService reference for DNSTT
        DnsttBridge.setVpnService(this@SlipNetVpnService)

        if (!isProxyOnly) {
            // Step 2: Establish VPN interface with addDisallowedApplication
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                DnsttBridge.setVpnService(null)
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Brief delay to let VPN routing settle
            delay(200)
        }

        // Step 3: Start DNSTT/NoizDNS proxy on internal port (127.0.0.1 only)
        val isNoizdns = profile.tunnelType == TunnelType.NOIZDNS || profile.tunnelType == TunnelType.NOIZDNS_SSH
        val proxyResult = if (isNoizdns) {
            vpnRepository.startNoizdnsProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        } else {
            vpnRepository.startDnsttProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        }
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start ${if (isNoizdns) "NoizDNS" else "DNSTT"} proxy")
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualDnsttPort = DnsttBridge.getClientPort()
        if (actualDnsttPort != dnsttPort) {
            Log.i(TAG, "DNSTT bound to alternative port $actualDnsttPort (preferred $dnsttPort was stuck)")
        }

        // Step 3.5: Verify proxy is listening
        if (!waitForProxyReady(actualDnsttPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualDnsttPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 4: Switch tunnel type to DNSTT_SSH/NOIZDNS_SSH before starting SSH
        val sshTunnelType = if (isNoizdns) TunnelType.NOIZDNS_SSH else TunnelType.DNSTT_SSH
        vpnRepository.setCurrentTunnelType(sshTunnelType)
        currentTunnelType = sshTunnelType

        // Step 5: Start SSH tunnel through DNSTT (with retry)
        // DNSTT is a raw TCP tunnel — JSch connects directly to its local port.
        // DNS tunnels can drop the first connection (DPI, packet loss), so retry
        // up to 3 times before giving up.
        configureSshBridge()
        val tunnelLabel = if (isNoizdns) "NoizDNS" else "DNSTT"
        var sshResult: Result<Unit> = Result.failure(RuntimeException("SSH not attempted"))
        for (attempt in 1..SSH_OVER_TUNNEL_RETRIES) {
            sshResult = withContext(Dispatchers.IO) {
                Log.i(TAG, "Starting SSH tunnel through $tunnelLabel (${profile.sshHost}:${profile.sshPort} via 127.0.0.1:$actualDnsttPort) attempt $attempt/$SSH_OVER_TUNNEL_RETRIES")
                SshTunnelBridge.startOverProxy(
                    sshHost = profile.sshHost,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = "127.0.0.1",
                    proxyPort = actualDnsttPort,
                    listenPort = proxyPort,
                    listenHost = proxyHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback
                )
            }
            if (sshResult.isSuccess) break
            if (attempt < SSH_OVER_TUNNEL_RETRIES) {
                Log.w(TAG, "SSH over $tunnelLabel attempt $attempt failed: ${sshResult.exceptionOrNull()?.message}, retrying...")
                delay(1000L * attempt)
            }
        }
        if (sshResult.isFailure) {
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over $tunnelLabel")
            withContext(Dispatchers.IO) { DnsttBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 5.5: Wait for SSH SOCKS5 proxy to be ready
        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready on port $proxyPort")
            reportConnectionError("SSH tunnel failed to start over DNSTT")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { DnsttBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: DNSTT+SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 6: Start tun2socks pointing at SSH SOCKS5 on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { DnsttBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            DnsttBridge.setVpnService(null)
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(500)
        Log.d(TAG, "DNSTT+SSH tunnel started")
        finishConnection()
    }

    /**
     * Connect using VayDNS tunnel type.
     * Order: Establish VPN interface (with app exclusion) -> Start VayDNS proxy -> Wait for ready
     *        -> Start DnsttSocksBridge (reused) -> tun2socks
     *
     * VayDNS follows the same 4-step connection pattern as DNSTT but uses VaydnsBridge
     * instead of DnsttBridge.
     */
    private suspend fun connectVaydns(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val vaydnsPort = proxyPort + 1

        // Step 1: Establish VPN interface FIRST (with addDisallowedApplication for this app)
        // This ensures VayDNS's sockets bypass the VPN when created
        if (!isProxyOnly) {
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Brief delay to let VPN routing settle
            delay(200)
        }

        // Step 2: Start VayDNS proxy on internal port
        val proxyResult = vpnRepository.startVaydnsProxy(profile, portOverride = vaydnsPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start VayDNS proxy")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualVaydnsPort = VaydnsBridge.getClientPort()
        if (actualVaydnsPort != vaydnsPort) {
            Log.i(TAG, "VayDNS bound to alternative port $actualVaydnsPort (preferred $vaydnsPort was stuck)")
        }

        // Step 2.5: Verify VayDNS is listening on internal port
        if (!waitForProxyReady(actualVaydnsPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualVaydnsPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 3: Start DnsttSocksBridge on proxyPort (reused, with VayDNS upstream check)
        DnsttSocksBridge.upstreamRunningCheck = { VaydnsBridge.isRunning() }
        DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
        DnsttSocksBridge.proxyOnlyMode = isProxyOnly
        DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
        val bridgeResult = vpnRepository.startDnsttSocksBridge(
            dnsttPort = actualVaydnsPort,
            dnsttHost = "127.0.0.1",
            bridgePort = proxyPort,
            bridgeHost = proxyHost,
            socksUsername = profile.socksUsername,
            socksPassword = profile.socksPassword,
            dnsServer = remoteDns,
            dnsFallback = remoteDnsFallback
        )
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start VayDNS SOCKS5 bridge")
            stopCurrentProxy()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3.5: Verify bridge is listening
        if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
            Log.e(TAG, "DnsttSocksBridge failed to become ready on port $proxyPort")
            reportConnectionError("VayDNS SOCKS5 bridge failed to start")
            stopCurrentProxy()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: VayDNS SOCKS5 bridge ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 4: Start tun2socks pointing at bridge on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Give VayDNS a moment to establish the connection
        delay(500)
        Log.d(TAG, "VayDNS tunnel started with bridge")

        finishConnection()
    }

    /**
     * Connect using VayDNS+SSH tunnel type.
     * Order: VPN interface (with app exclusion) -> Start VayDNS proxy -> Wait for ready
     *        -> Start SSH over VayDNS proxy -> Wait for SSH ready -> tun2socks on proxyPort
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> SSH SOCKS5 (proxyPort)
     *   -> SSH direct-tcpip -> VayDNS (proxyPort+1, 127.0.0.1, raw TCP tunnel)
     *   -> DNS tunnel (UDP 53) -> VayDNS Server -> SSH Server -> Internet
     */
    private suspend fun connectVaydnsSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val vaydnsPort = proxyPort + 1

        if (!isProxyOnly) {
            // Step 1: Establish VPN interface with addDisallowedApplication
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Brief delay to let VPN routing settle
            delay(200)
        }

        // Step 2: Start VayDNS proxy on internal port (127.0.0.1 only)
        val proxyResult = vpnRepository.startVaydnsProxy(profile, portOverride = vaydnsPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start VayDNS proxy")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualVaydnsPort = VaydnsBridge.getClientPort()
        if (actualVaydnsPort != vaydnsPort) {
            Log.i(TAG, "VayDNS bound to alternative port $actualVaydnsPort (preferred $vaydnsPort was stuck)")
        }

        // Step 2.5: Verify proxy is listening
        if (!waitForProxyReady(actualVaydnsPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualVaydnsPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 3: Switch tunnel type to VAYDNS_SSH before starting SSH
        vpnRepository.setCurrentTunnelType(TunnelType.VAYDNS_SSH)
        currentTunnelType = TunnelType.VAYDNS_SSH

        // Step 4: Start SSH tunnel through VayDNS (with retry)
        configureSshBridge()
        var sshResult: Result<Unit> = Result.failure(RuntimeException("SSH not attempted"))
        for (attempt in 1..SSH_OVER_TUNNEL_RETRIES) {
            sshResult = withContext(Dispatchers.IO) {
                Log.i(TAG, "Starting SSH tunnel through VayDNS (${profile.sshHost}:${profile.sshPort} via 127.0.0.1:$actualVaydnsPort) attempt $attempt/$SSH_OVER_TUNNEL_RETRIES")
                SshTunnelBridge.startOverProxy(
                    sshHost = profile.sshHost,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = "127.0.0.1",
                    proxyPort = actualVaydnsPort,
                    listenPort = proxyPort,
                    listenHost = proxyHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback
                )
            }
            if (sshResult.isSuccess) break
            if (attempt < SSH_OVER_TUNNEL_RETRIES) {
                Log.w(TAG, "SSH over VayDNS attempt $attempt failed: ${sshResult.exceptionOrNull()?.message}, retrying...")
                delay(1000L * attempt)
            }
        }
        if (sshResult.isFailure) {
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over VayDNS")
            withContext(Dispatchers.IO) { VaydnsBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 4.5: Wait for SSH SOCKS5 proxy to be ready
        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready on port $proxyPort")
            reportConnectionError("SSH tunnel failed to start over VayDNS")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { VaydnsBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: VayDNS+SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 5: Start tun2socks pointing at SSH SOCKS5 on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { VaydnsBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(500)
        Log.d(TAG, "VayDNS+SSH tunnel started")
        finishConnection()
    }

    /**
     * Connect using Soar tunnel type.
     * Order: VPN interface (with app exclusion) -> Start Soar (SOCKS5 on proxyPort+1) -> Wait for ready
     *        -> Socks5ProxyBridge on proxyPort -> tun2socks
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> Socks5ProxyBridge (proxyPort)
     *   -> Soar SOCKS5 (proxyPort+1) -> DNS tunnel over every resolver at once
     *   -> Soar server (dials the destination) -> Internet
     *
     * Socks5ProxyBridge resolves TUN DNS as DNS-over-TCP CONNECTs to remoteDns:53; the Soar
     * client answers those with DoH through the tunnel, since a stock soar-server only
     * lets streams reach ports 80 and 443.
     */
    private suspend fun connectSoar(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val soarPort = proxyPort + 1
        val localAuthUser = if (preferencesDataStore.proxyAuthEnabled.first()) preferencesDataStore.proxyAuthUsername.first().ifEmpty { null } else null
        val localAuthPass = if (preferencesDataStore.proxyAuthEnabled.first()) preferencesDataStore.proxyAuthPassword.first().ifEmpty { null } else null

        // Step 1: Establish VPN interface FIRST (with addDisallowedApplication for this app)
        // so Soar's resolver sockets bypass the VPN when created
        if (!isProxyOnly) {
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }
            delay(200)
        }

        // Step 2: Start Soar on internal port (127.0.0.1 only)
        val soarResult = vpnRepository.startSoarProxy(profile, portOverride = soarPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (soarResult.isFailure) {
            reportConnectionError(soarResult.exceptionOrNull()?.message ?: "Failed to start Soar")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Read actual port — may differ from requested if preferred port was stuck
        val actualSoarPort = SoarBridge.getClientPort()
        if (actualSoarPort != soarPort) {
            Log.i(TAG, "Soar bound to alternative port $actualSoarPort (preferred $soarPort was stuck)")
        }

        if (!waitForProxyReady(actualSoarPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualSoarPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 3: Start the user-facing SOCKS5 bridge on proxyPort (handles UDP/DNS from tun2socks)
        Socks5ProxyBridge.debugLogging = preferencesDataStore.debugLogging.first()
        val bridgeResult = withContext(Dispatchers.IO) {
            Socks5ProxyBridge.start(
                remoteHost = "127.0.0.1",
                remotePort = actualSoarPort,
                remoteUsername = null,
                remotePassword = null,
                listenPort = proxyPort,
                listenHost = proxyHost,
                dnsHost = remoteDns,
                dnsFallback = remoteDnsFallback,
                localAuthUsername = localAuthUser,
                localAuthPassword = localAuthPass
            )
        }
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start Soar SOCKS5 bridge")
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "Socks5ProxyBridge failed to become ready on port $proxyPort")
            reportConnectionError("Soar SOCKS5 bridge failed to start")
            withContext(Dispatchers.IO) { Socks5ProxyBridge.stop() }
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Soar SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 4: Start tun2socks pointing at the bridge on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { Socks5ProxyBridge.stop() }
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "Soar tunnel started")
        finishConnection()
    }

    /**
     * Connect using Soar+SSH tunnel type.
     * Order: VPN interface (with app exclusion) -> Start Soar (SOCKS5 on proxyPort+1) -> Wait for ready
     *        -> SSH through Soar's SOCKS5 -> Wait for SSH ready -> tun2socks on proxyPort
     *
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> SSH SOCKS5 (proxyPort)
     *   -> SSH direct-tcpip -> Soar SOCKS5 (proxyPort+1) -> DNS tunnel
     *   -> Soar server -> sshHost:sshPort (as seen from the Soar server) -> Internet
     *
     * The Soar server must be allowed to reach the SSH server: a stock soar-server refuses
     * port 22 and its own/private addresses (run it with -allow-ports and -allow-private).
     */
    private suspend fun connectSoarSsh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String, globalResolverOverride: List<app.slipnet.domain.model.DnsResolver>? = null) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val soarPort = proxyPort + 1

        if (!isProxyOnly) {
            // Step 1: Establish VPN interface with addDisallowedApplication
            vpnInterface = establishVpnInterface(dnsServer)
            if (vpnInterface == null) {
                reportConnectionError("Failed to establish VPN interface")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }
            delay(200)
        }

        // Step 2: Start Soar on internal port (127.0.0.1 only)
        val soarResult = vpnRepository.startSoarProxy(profile, portOverride = soarPort, hostOverride = "127.0.0.1", resolverOverride = globalResolverOverride)
        if (soarResult.isFailure) {
            reportConnectionError(soarResult.exceptionOrNull()?.message ?: "Failed to start Soar")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        val actualSoarPort = SoarBridge.getClientPort()
        if (actualSoarPort != soarPort) {
            Log.i(TAG, "Soar bound to alternative port $actualSoarPort (preferred $soarPort was stuck)")
        }

        if (!waitForProxyReady(actualSoarPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(actualSoarPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 3: Switch tunnel type to SOAR_SSH before starting SSH
        vpnRepository.setCurrentTunnelType(TunnelType.SOAR_SSH)
        currentTunnelType = TunnelType.SOAR_SSH

        // Step 4: Start SSH through Soar's SOCKS5 (with retry)
        configureSshBridge()
        var sshResult: Result<Unit> = Result.failure(RuntimeException("SSH not attempted"))
        for (attempt in 1..SSH_OVER_TUNNEL_RETRIES) {
            sshResult = withContext(Dispatchers.IO) {
                Log.i(TAG, "Starting SSH tunnel through Soar (${profile.sshHost}:${profile.sshPort} via 127.0.0.1:$actualSoarPort) attempt $attempt/$SSH_OVER_TUNNEL_RETRIES")
                SshTunnelBridge.startOverSocks5Proxy(
                    sshHost = profile.sshHost,
                    sshPort = profile.sshPort,
                    sshUsername = profile.sshUsername,
                    sshPassword = profile.sshPassword,
                    proxyHost = "127.0.0.1",
                    proxyPort = actualSoarPort,
                    socksUsername = null,
                    socksPassword = null,
                    listenPort = proxyPort,
                    listenHost = proxyHost,
                    blockDirectDns = true,
                    sshAuthType = profile.sshAuthType,
                    sshPrivateKey = profile.sshPrivateKey,
                    sshKeyPassphrase = profile.sshKeyPassphrase,
                    remoteDnsHost = remoteDns,
                    remoteDnsFallback = remoteDnsFallback
                )
            }
            if (sshResult.isSuccess) break
            if (attempt < SSH_OVER_TUNNEL_RETRIES) {
                Log.w(TAG, "SSH over Soar attempt $attempt failed: ${sshResult.exceptionOrNull()?.message}, retrying...")
                delay(1000L * attempt)
            }
        }
        if (sshResult.isFailure) {
            Log.w(TAG, "SSH over Soar failed — the Soar server must allow ${profile.sshHost}:${profile.sshPort} (soar-server -allow-ports / -allow-private)")
            reportConnectionError(sshResult.exceptionOrNull()?.message ?: "Failed to start SSH tunnel over Soar")
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            Log.e(TAG, "SSH SOCKS5 proxy failed to become ready on port $proxyPort")
            reportConnectionError("SSH tunnel failed to start over Soar")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Proxy-only mode: skip tun2socks
        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Soar+SSH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 5: Start tun2socks pointing at SSH SOCKS5 on proxyPort
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { SshTunnelBridge.stop() }
            withContext(Dispatchers.IO) { SoarBridge.stopClient() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(500)
        Log.d(TAG, "Soar+SSH tunnel started")
        finishConnection()
    }

    /**
     * Connect using DoH (DNS-over-HTTPS) tunnel type.
     * Order: Establish VPN interface (with app exclusion) -> Start DoH proxy -> Wait for ready -> tun2socks
     *
     * Only DNS queries are encrypted via HTTPS. All other traffic flows directly.
     * Uses addDisallowedApplication so DoH HTTPS requests and direct TCP/UDP bypass VPN.
     */
    private suspend fun connectDoh(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()

        if (isProxyOnly) {
            // Proxy-only: no VPN needed, start DoH proxy directly
            val proxyResult = vpnRepository.startDohProxy(profile)
            if (proxyResult.isFailure) {
                reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start DoH proxy")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
                handleProxyStartupFailure(proxyPort)
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: DoH SOCKS5 proxy ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface with addDisallowedApplication
        // Use a virtual DNS address so Android can't DoT to a real server.
        // All DNS goes through FWD_UDP → DohBridge → DoH HTTPS instead.
        vpnInterface = establishVpnInterface("10.255.255.2")
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Brief delay to let VPN routing settle
        delay(200)

        // Step 2: Start DoH proxy
        val proxyResult = vpnRepository.startDohProxy(profile)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start DoH proxy")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 2.5: Verify proxy is listening
        if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(proxyPort)
            vpnInterface?.close()
            vpnInterface = null
            return
        }

        // Step 3: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "DoH tunnel started")
        finishConnection()
    }

    /**
     * Connect using Snowflake (Tor) tunnel type.
     * Order: VPN interface (virtual DNS, addDisallowedApplication)
     *        -> Start Snowflake PT + Tor + TorSocksBridge
     *        -> Wait for Tor bootstrap (60s)
     *        -> Wait for bridge ready -> tun2socks on bridge port
     *
     * Snowflake routes traffic through Tor's network using WebRTC volunteer proxies.
     * TorSocksBridge handles CONNECT (chain to Tor SOCKS5) and FWD_UDP DNS
     * (DNS-over-TCP through Tor).
     */
    private suspend fun connectSnowflake(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String) {
        // Route SMART profiles to the Smart Connect orchestrator
        if (profile.torBridgeLines.trim() == "SMART") {
            connectSnowflakeSmart(profile, dnsServer)
            return
        }

        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val torSocksPort = proxyPort + 1
        val snowflakePtPort = proxyPort + 2

        if (isProxyOnly) {
            // Start proxy stack directly
            val proxyResult = vpnRepository.startSnowflakeProxy(profile, snowflakePtPort, torSocksPort, proxyPort)
            if (proxyResult.isFailure) {
                reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start Snowflake proxy")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            // Wait for Tor to bootstrap (webtunnel/obfs4 can be slow)
            if (!waitForTorReady(maxWaitMs = 300000)) {
                reportConnectionError("Tor failed to bootstrap within timeout (${SnowflakeBridge.torBootstrapProgress}%)")
                stopCurrentProxy()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
                handleProxyStartupFailure(proxyPort)
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Snowflake proxy stack ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Start Snowflake proxy stack (Snowflake PT + Tor + TorSocksBridge)
        // VPN interface is NOT established yet — no VPN key icon during bootstrap
        val proxyResult = vpnRepository.startSnowflakeProxy(profile, snowflakePtPort, torSocksPort, proxyPort)
        if (proxyResult.isFailure) {
            reportConnectionError(proxyResult.exceptionOrNull()?.message ?: "Failed to start Snowflake proxy")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 2: Wait for Tor to bootstrap (webtunnel/obfs4 can be slow)
        Log.i(TAG, "Waiting for Tor to bootstrap...")
        if (!waitForTorReady(maxWaitMs = 300000)) {
            reportConnectionError("Tor failed to bootstrap within timeout (${SnowflakeBridge.torBootstrapProgress}%)")
            stopCurrentProxy()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }
        Log.i(TAG, "Tor bootstrapped successfully")

        // Step 3: Verify bridge is listening
        if (!waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
            handleProxyStartupFailure(proxyPort)
            return
        }

        // Step 4: Establish VPN interface now that Tor is ready (VPN key icon appears here)
        vpnInterface = establishVpnInterface("10.255.255.2")
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopCurrentProxy()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Brief delay to let VPN routing settle
        delay(200)

        // Step 5: Start tun2socks pointing at TorSocksBridge port
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "Snowflake tunnel started")
        finishConnection()
    }

    /**
     * Smart Connect: tries multiple Tor transports sequentially until one works.
     * Transport sequence: Direct → obfs4 → Meek Azure → Snowflake.
     * On reconnect, SMART profiles fall back to built-in Snowflake (SnowflakeBridge
     * treats "SMART" sentinel as built-in Snowflake).
     */

    /**
     * Connect using a remote SOCKS5 proxy.
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> Socks5ProxyBridge (proxyPort)
     *   -> Remote SOCKS5 Proxy (domain:socks5ServerPort) -> Internet
     */
    private suspend fun startSocks5Bridge(profile: app.slipnet.domain.model.ServerProfile, remoteDns: String, remoteDnsFallback: String): Result<Unit> {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val localAuthUser = if (preferencesDataStore.proxyAuthEnabled.first()) preferencesDataStore.proxyAuthUsername.first().ifEmpty { null } else null
        val localAuthPass = if (preferencesDataStore.proxyAuthEnabled.first()) preferencesDataStore.proxyAuthPassword.first().ifEmpty { null } else null
        Socks5ProxyBridge.debugLogging = preferencesDataStore.debugLogging.first()
        return withContext(Dispatchers.IO) {
            Socks5ProxyBridge.start(
                remoteHost = profile.domain,
                remotePort = profile.socks5ServerPort,
                remoteUsername = profile.socksUsername,
                remotePassword = profile.socksPassword,
                listenPort = proxyPort,
                listenHost = proxyHost,
                dnsHost = remoteDns,
                dnsFallback = remoteDnsFallback,
                localAuthUsername = localAuthUser,
                localAuthPassword = localAuthPass
            )
        }
    }

    private suspend fun connectSocks5(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String, remoteDns: String, remoteDnsFallback: String) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        vpnRepository.setCurrentTunnelType(TunnelType.SOCKS5)

        if (isProxyOnly) {
            val bridgeResult = startSocks5Bridge(profile, remoteDns, remoteDnsFallback)
            if (bridgeResult.isFailure) {
                reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start SOCKS5 proxy bridge")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
                reportConnectionError("SOCKS5 proxy bridge failed to start")
                withContext(Dispatchers.IO) { Socks5ProxyBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: SOCKS5 proxy bridge ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }
        delay(200)

        // Step 2: Start SOCKS5 proxy bridge
        val bridgeResult = startSocks5Bridge(profile, remoteDns, remoteDnsFallback)
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start SOCKS5 proxy bridge")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            reportConnectionError("SOCKS5 proxy bridge failed to start")
            withContext(Dispatchers.IO) { Socks5ProxyBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { Socks5ProxyBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "SOCKS5 proxy tunnel started")
        finishConnection()
    }

    /**
     * Connect using VLESS tunnel type with SNI fragmentation.
     * Traffic flow:
     * App -> TUN -> hev-socks5-tunnel -> VlessBridge SOCKS5 (proxyPort)
     *   -> SniFragmentForwarder (proxyPort+1) -> CDN IP:443
     *     -> TLS (fragmented ClientHello) -> WebSocket -> VLESS -> CDN -> Server
     */
    private suspend fun connectXray(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String) {
        vpnRepository.setCurrentTunnelType(TunnelType.XRAY)
        try {
            val configured = preferencesDataStore.configuredSocksEndpoint()
            if (isProxyOnly && (configured.user.isNullOrEmpty() || configured.password.isNullOrEmpty())) {
                reportConnectionError("Enable local SOCKS authentication for Xray proxy-only mode")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }
            val endpoint = if (configured.user != null) configured else withContext(Dispatchers.IO) {
                app.slipnet.core.LocalSocksEndpoint.privateCore()
            }
            coreEndpoint = endpoint
            val tun = if (isProxyOnly) null else {
                val established = establishVpnInterface(dnsServer)
                if (established == null) {
                    reportConnectionError(app.slipnet.presentation.theme.RovunetStrings.localizedContext(this).getString(app.slipnet.R.string.vpn_tun_unavailable))
                    stopForeground(STOP_FOREGROUND_REMOVE)
                    stopOwnedService()
                    return
                }
                vpnInterface = established
                established
            }
            val routing = buildDomainRouter().coreRules()
            withContext(Dispatchers.IO) { XrayBridge.start(this@SlipNetVpnService, profile.coreLink, endpoint, routing).getOrThrow() }
            if (tun == null) vpnRepository.setProxyConnected(profile)
            else vpnRepository.startTun2Socks(profile, tun).getOrThrow()
            finishConnection()
        } catch (e: Exception) {
            withContext(kotlinx.coroutines.NonCancellable + Dispatchers.IO) { XrayBridge.stop() }
            vpnInterface?.close(); vpnInterface = null
            if (e is kotlinx.coroutines.CancellationException) throw e
            reportConnectionError("Xray startup failed; check core binary and profile settings")
            stopForeground(STOP_FOREGROUND_REMOVE); stopOwnedService()
        }
    }

    private suspend fun startVlessBridge(profile: app.slipnet.domain.model.ServerProfile): Result<Unit> {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val auth = preferencesDataStore.proxyAuthEnabled.first()
        VlessBridge.localAuthUsername = if (auth) preferencesDataStore.proxyAuthUsername.first() else null
        VlessBridge.localAuthPassword = if (auth) preferencesDataStore.proxyAuthPassword.first() else null
        VlessBridge.debugLogging = preferencesDataStore.debugLogging.first()
        return withContext(Dispatchers.IO) {
            VlessBridge.start(
                listenPort = proxyPort,
                listenHost = proxyHost,
                cdnIp = profile.cdnIp,
                cdnPort = profile.cdnPort,
                serverDomain = profile.domain,
                vlessUuid = profile.vlessUuid,
                security = profile.vlessSecurity,
                transport = profile.vlessTransport,
                wsPath = profile.vlessWsPath,
                fragmentEnabled = profile.sniFragmentEnabled,
                fragmentStrategy = profile.sniFragmentStrategy,
                fragmentDelayMs = profile.sniFragmentDelayMs,
                sniSpoofTtl = profile.sniSpoofTtl,
                fakeDecoyHost = profile.fakeDecoyHost,
                tcpMaxSeg = profile.tcpMaxSeg,
                vlessSni = profile.vlessSni,
                chPaddingEnabled = profile.chPaddingEnabled,
                wsHeaderObfuscation = profile.wsHeaderObfuscation,
                wsPaddingEnabled = profile.wsPaddingEnabled
            )
        }
    }

    private suspend fun connectVless(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        vpnRepository.setCurrentTunnelType(app.slipnet.domain.model.TunnelType.VLESS)

        if (isProxyOnly) {
            val bridgeResult = startVlessBridge(profile)
            if (bridgeResult.isFailure) {
                reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start VLESS bridge")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
                reportConnectionError("VLESS bridge failed to start")
                withContext(Dispatchers.IO) { VlessBridge.stop() }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
                return
            }

            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: VLESS bridge ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Step 1: Establish VPN interface
        vpnInterface = establishVpnInterface(dnsServer)
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        delay(200)

        // Step 2: Start VLESS bridge
        val bridgeResult = startVlessBridge(profile)
        if (bridgeResult.isFailure) {
            reportConnectionError(bridgeResult.exceptionOrNull()?.message ?: "Failed to start VLESS bridge")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 100)) {
            reportConnectionError("VLESS bridge failed to start")
            withContext(Dispatchers.IO) { VlessBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Step 3: Start tun2socks
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            withContext(Dispatchers.IO) { VlessBridge.stop() }
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "VLESS tunnel started")
        finishConnection()
    }

    private suspend fun connectSnowflakeSmart(profile: app.slipnet.domain.model.ServerProfile, dnsServer: String) {
        val proxyPort = preferencesDataStore.proxyListenPort.first()
        val proxyHost = preferencesDataStore.proxyListenAddress.first()
        val torSocksPort = proxyPort + 1
        val snowflakePtPort = proxyPort + 2

        data class SmartTransport(val label: String, val bridgeLines: String, val timeoutMs: Long)
        val transports = listOf(
            SmartTransport("Direct", "DIRECT", 30000),
            SmartTransport("obfs4", app.slipnet.presentation.profiles.EditProfileViewModel.DEFAULT_OBFS4_BRIDGES, 60000),
            SmartTransport("Meek Azure", app.slipnet.presentation.profiles.EditProfileViewModel.DEFAULT_MEEK_BRIDGE, 60000),
            SmartTransport("Snowflake", "", 60000)
        )

        val notificationManager = getSystemService(NotificationManager::class.java)
        var success = false

        for ((index, transport) in transports.withIndex()) {
            Log.i(TAG, "Smart Connect: Trying ${transport.label} (${index + 1}/${transports.size})")

            // Update notification with current transport attempt
            val notification = notificationHelper.createSmartConnectNotification(
                transport.label, index + 1, transports.size
            )
            notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)

            // Create modified profile with this transport's bridge lines
            val modifiedProfile = profile.copy(torBridgeLines = transport.bridgeLines)

            // Try starting proxy stack
            val proxyResult = vpnRepository.startSnowflakeProxy(modifiedProfile, snowflakePtPort, torSocksPort, proxyPort)
            if (proxyResult.isFailure) {
                Log.w(TAG, "Smart Connect: ${transport.label} proxy start failed: ${proxyResult.exceptionOrNull()?.message}")
                continue
            }

            // Wait for Tor to bootstrap with transport-specific timeout
            if (waitForTorReady(maxWaitMs = transport.timeoutMs)) {
                if (waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 100)) {
                    Log.i(TAG, "Smart Connect: ${transport.label} succeeded!")
                    success = true
                    break
                }
            }

            // This transport failed — stop proxy stack and try the next one
            Log.w(TAG, "Smart Connect: ${transport.label} failed (bootstrap: ${SnowflakeBridge.torBootstrapProgress}%)")
            withContext(Dispatchers.IO) { TorSocksBridge.stop() }
            withContext(Dispatchers.IO) { SnowflakeBridge.stopClient() }
        }

        if (!success) {
            reportConnectionError("Smart Connect: All transports failed")
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        if (isProxyOnly) {
            vpnRepository.setProxyConnected(profile)
            Log.i(TAG, "Proxy-only mode: Smart Connect proxy stack ready on $proxyHost:$proxyPort")
            finishConnection()
            return
        }

        // Establish VPN interface now that a transport succeeded (VPN key icon appears here)
        vpnInterface = establishVpnInterface("10.255.255.2")
        if (vpnInterface == null) {
            reportConnectionError("Failed to establish VPN interface")
            stopCurrentProxy()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        // Brief delay to let VPN routing settle
        delay(200)

        // Start tun2socks pointing at TorSocksBridge port
        val tun2socksResult = vpnRepository.startTun2Socks(profile, vpnInterface!!)
        if (tun2socksResult.isFailure) {
            reportConnectionError(tun2socksResult.exceptionOrNull()?.message ?: "Failed to start tunnel")
            vpnInterface?.close()
            vpnInterface = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        Log.d(TAG, "Smart Connect tunnel started")
        finishConnection()
    }

    /**
     * Wait for Tor to finish bootstrapping (isTorReady == true).
     */
    private suspend fun waitForTorReady(maxWaitMs: Long = 90000): Boolean {
        val startTime = System.currentTimeMillis()
        while (System.currentTimeMillis() - startTime < maxWaitMs) {
            if (SnowflakeBridge.isTorReady) return true
            if (!SnowflakeBridge.isRunning()) {
                Log.e(TAG, "Snowflake/Tor died during bootstrap")
                return false
            }
            delay(1000)
        }
        return SnowflakeBridge.isTorReady
    }

    /**
     * Handle proxy startup failure - common to both tunnel types.
     */
    private suspend fun handleProxyStartupFailure(port: Int) {
        val nativeRunning = when (currentTunnelType) {
            TunnelType.SLIPSTREAM -> try { SlipstreamBridge.isNativeRunning() } catch (e: Exception) { false }
            TunnelType.SLIPSTREAM_SSH -> try { SlipstreamBridge.isNativeRunning() } catch (e: Exception) { false }
            TunnelType.DNSTT -> try { DnsttBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.NOIZDNS -> try { DnsttBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.VAYDNS -> try { VaydnsBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.SSH -> SshTunnelBridge.isRunning()
            TunnelType.DNSTT_SSH -> try { DnsttBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.NOIZDNS_SSH -> try { DnsttBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.VAYDNS_SSH -> try { VaydnsBridge.isRunning() } catch (e: Exception) { false }
            TunnelType.DOH -> DohBridge.isRunning()
            TunnelType.NAIVE_SSH -> NaiveBridge.isRunning()
            TunnelType.NAIVE -> NaiveBridge.isRunning()
            TunnelType.SNOWFLAKE -> SnowflakeBridge.isRunning()
            TunnelType.SOCKS5 -> Socks5ProxyBridge.isRunning()
            TunnelType.XRAY -> XrayBridge.isRunning()
            TunnelType.VLESS -> VlessBridge.isRunning()
            TunnelType.SOAR, TunnelType.SOAR_SSH -> SoarBridge.isRunning()
        }
        Log.e(TAG, "Proxy failed to become ready on port $port, nativeRunning=$nativeRunning")

        val errorMsg = if (!nativeRunning) {
            "Proxy failed to start - client crashed"
        } else {
            "Proxy failed to start - port not listening"
        }
        reportConnectionError(errorMsg)
        stopCurrentProxy()
        clearVpnServiceRef()
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopOwnedService()
    }

    /**
     * Finish connection setup - common to all tunnel types.
     */
    private suspend fun startHttpProxy(host: String, port: Int): Result<Unit> {
        val configured = preferencesDataStore.configuredSocksEndpoint()
        val socks = if (currentTunnelType == TunnelType.XRAY) requireNotNull(coreEndpoint) else configured
        return HttpProxyServer.start("127.0.0.1", socks.port, host, port,
            socks.user, socks.password, configured.user, configured.password)
    }

    private fun finishConnection() {
        // Mark connection as successful for auto-reconnect eligibility
        connectionWasSuccessful = true
        autoReconnectAttempt = 0
        vpnRepository.setAutoReconnect(false)
        connectionEstablishedAt = System.currentTimeMillis()

        // Clear boot-triggered state — connection succeeded, normal auto-reconnect takes over
        isBootTriggered = false
        bootRetryAttempt = 0
        bootRetryJob?.cancel()
        bootRetryJob = null
        unregisterBootNetworkCallback()

        // Notify connection manager for bookkeeping (profile preferences, etc.)
        reportEstablished()

        // Tag the Connected state with chain info so the UI can highlight the chain
        if (currentChainId > 0) {
            val current = vpnRepository.connectionState.value
            if (current is ConnectionState.Connected) {
                vpnRepository.updateConnectionState(
                    current.copy(chainId = currentChainId, chainName = currentProfileName)
                )
            }
        }

        // Save connection state for auto-restart if killed by system
        saveConnectionState(currentProfileId, connected = true)

        // Start HTTP proxy if enabled (chains through existing SOCKS5 proxy).
        // Skip if already running (started earlier by establishVpnInterface for VPN append).
        runtimeScope.launch {
            try {
                if (!HttpProxyServer.isRunning()) {
                    val httpEnabled = preferencesDataStore.httpProxyEnabled.first()
                    if (httpEnabled) {
                        val httpPort = preferencesDataStore.httpProxyPort.first()
                        val listenHost = preferencesDataStore.proxyListenAddress.first()

                        val result = startHttpProxy(listenHost, httpPort)
                        if (result.isFailure) {
                            Log.w(TAG, "HTTP proxy failed to start: ${result.exceptionOrNull()?.message}")
                        }
                    }
                }
            } catch (e: Exception) {
                Log.w(TAG, "Error starting HTTP proxy: ${e.message}")
            }
        }

        // Start health monitoring and network change detection
        lastUserTrafficAtElapsed = android.os.SystemClock.elapsedRealtime()
        lastPowerTx = 0
        lastPowerRx = 0
        startHealthCheck()
        registerNetworkCallback()
        registerDozeReceiver()

        // Tell Android which physical networks the VPN uses for seamless failover
        updateUnderlyingNetworks()

        // Update notification to connected state
        observeConnectionState()

        // Warm up the tunnel with a few DNS queries so the KCP session is active
        // before apps like Telegram try to connect. Without this, the first
        // connections through a cold tunnel are slow and apps with aggressive
        // timeouts may fail and enter exponential backoff.
        if (!isProxyOnly && currentTunnelType in setOf(TunnelType.DNSTT, TunnelType.NOIZDNS)) {
            runtimeScope.launch(Dispatchers.IO) {
                try {
                    if (preferencesDataStore.proxyAuthEnabled.first()) return@launch
                    val proxyPort = preferencesDataStore.proxyListenPort.first()
                    repeat(3) {
                        try {
                            val token = startupCancellation
                            java.net.Socket().use { s ->
                                val registration = token.register { runCatching { s.close() } }
                                try {
                                kotlinx.coroutines.currentCoroutineContext().ensureActive()
                                s.connect(java.net.InetSocketAddress("127.0.0.1", proxyPort), 3000)
                                s.soTimeout = 3000
                                val out = s.getOutputStream()
                                // SOCKS5 handshake + CONNECT to 8.8.8.8:53 to prime the tunnel
                                out.write(byteArrayOf(0x05, 0x01, 0x00)) // SOCKS5 no-auth
                                out.flush()
                                s.getInputStream().read(ByteArray(2)) // auth response
                                out.write(byteArrayOf(
                                    0x05, 0x01, 0x00, 0x01,        // SOCKS5 CONNECT IPv4
                                    0x08, 0x08, 0x08, 0x08,        // 8.8.8.8
                                    0x00, 0x35                      // port 53
                                ))
                                out.flush()
                                s.getInputStream().read(ByteArray(10)) // connect response
                                } finally { registration.close() }
                            }
                        } catch (_: Exception) {}
                        delay(100)
                    }
                    Log.d(TAG, "Tunnel warm-up complete")
                } catch (_: Exception) {}
            }
        }
    }

    /**
     * Wait for the QUIC connection to be established (handshake complete).
     * This ensures the tunnel is fully ready before routing traffic through it.
     */
    private suspend fun waitForQuicReady(maxAttempts: Int, delayMs: Long): Boolean {
        Log.d(TAG, "Waiting for QUIC connection to be ready (max ${maxAttempts * delayMs}ms)")

        return kotlinx.coroutines.withContext(Dispatchers.IO) {
            repeat(maxAttempts) { attempt ->
                // Check if native client is still running
                val nativeRunning = try {
                    SlipstreamBridge.isNativeRunning()
                } catch (e: Exception) {
                    Log.e(TAG, "Error checking native state: ${e.message}")
                    true // Assume it's running if we can't check
                }

                if (!nativeRunning) {
                    Log.e(TAG, "Native client stopped while waiting for QUIC (attempt ${attempt + 1})")
                    return@withContext false
                }

                // Check if QUIC is ready
                val quicReady = try {
                    SlipstreamBridge.isQuicReady()
                } catch (e: Exception) {
                    Log.e(TAG, "Error checking QUIC ready state: ${e.message}")
                    false
                }

                if (quicReady) {
                    Log.i(TAG, "QUIC connection ready after ${attempt + 1} attempts (${(attempt + 1) * delayMs}ms)")
                    return@withContext true
                }

                if (attempt % 10 == 0) {
                    Log.d(TAG, "QUIC not ready yet (attempt ${attempt + 1})")
                }

                if (attempt < maxAttempts - 1) {
                    Thread.sleep(delayMs)
                }
            }

            Log.w(TAG, "QUIC connection not ready after $maxAttempts attempts (${maxAttempts * delayMs}ms)")
            false
        }
    }

    /**
     * Wait for the SOCKS5 proxy to be ready by checking if the port is listening.
     */
    private suspend fun waitForProxyReady(port: Int, maxAttempts: Int, delayMs: Long): Boolean {
        Log.d(TAG, "Waiting for proxy to be ready on port $port (max ${maxAttempts * delayMs}ms, type=$currentTunnelType)")

        return kotlinx.coroutines.withContext(Dispatchers.IO) {
            repeat(maxAttempts) { attempt ->
                // Check if native client is still running based on tunnel type
                val nativeRunning = when (currentTunnelType) {
                    TunnelType.SLIPSTREAM -> try {
                        SlipstreamBridge.isNativeRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking Slipstream native state: ${e.message}")
                        true // Assume it's running if we can't check
                    }
                    TunnelType.SLIPSTREAM_SSH -> try {
                        SlipstreamBridge.isNativeRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking Slipstream native state: ${e.message}")
                        true
                    }
                    TunnelType.DNSTT -> try {
                        DnsttBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking DNSTT state: ${e.message}")
                        true // Assume it's running if we can't check
                    }
                    TunnelType.NOIZDNS -> try {
                        DnsttBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking NoizDNS state: ${e.message}")
                        true
                    }
                    TunnelType.VAYDNS -> try {
                        VaydnsBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking VayDNS state: ${e.message}")
                        true
                    }
                    TunnelType.SSH -> {
                        // Check default instance or any chain instance
                        SshTunnelBridge.isRunning() || (0..3).any {
                            SshTunnelBridge.getInstance("chain-ssh-$it")?.isRunning() == true
                        }
                    }
                    TunnelType.DNSTT_SSH -> try {
                        DnsttBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking DNSTT state: ${e.message}")
                        true
                    }
                    TunnelType.NOIZDNS_SSH -> try {
                        DnsttBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking NoizDNS state: ${e.message}")
                        true
                    }
                    TunnelType.VAYDNS_SSH -> try {
                        VaydnsBridge.isRunning()
                    } catch (e: Exception) {
                        Log.e(TAG, "Error checking VayDNS state: ${e.message}")
                        true
                    }
                    TunnelType.DOH -> DohBridge.isRunning()
                    TunnelType.NAIVE_SSH -> NaiveBridge.isRunning()
                    TunnelType.NAIVE -> NaiveBridge.isRunning()
                    TunnelType.SNOWFLAKE -> SnowflakeBridge.isRunning()
                    TunnelType.SOCKS5 -> {
                        Socks5ProxyBridge.isRunning() || (0..3).any {
                            Socks5ProxyBridge.getInstance("chain-socks5-$it")?.isRunning() == true
                        }
                    }
                    TunnelType.XRAY -> XrayBridge.isRunning()
                    TunnelType.VLESS -> VlessBridge.isRunning()
                    TunnelType.SOAR, TunnelType.SOAR_SSH -> SoarBridge.isRunning()
                }

                if (!nativeRunning) {
                    Log.e(TAG, "Native client stopped during startup (attempt ${attempt + 1}, type=$currentTunnelType)")
                    return@withContext false
                }

                try {
                    java.net.Socket().use { socket ->
                        socket.connect(java.net.InetSocketAddress("127.0.0.1", port), 200)
                        Log.i(TAG, "Proxy ready on port $port after ${attempt + 1} attempts (${(attempt + 1) * delayMs}ms)")
                        return@withContext true
                    }
                } catch (e: java.net.ConnectException) {
                    // Connection refused - port not listening yet
                    if (attempt % 10 == 0) {
                        Log.d(TAG, "Proxy not ready yet (attempt ${attempt + 1}): connection refused")
                    }
                } catch (e: java.net.SocketTimeoutException) {
                    // Timeout - port might be in weird state
                    Log.d(TAG, "Proxy not ready yet (attempt ${attempt + 1}): timeout")
                } catch (e: Exception) {
                    Log.d(TAG, "Proxy not ready yet (attempt ${attempt + 1}): ${e.javaClass.simpleName} - ${e.message}")
                }

                if (attempt < maxAttempts - 1) {
                    Thread.sleep(delayMs)
                }
            }

            Log.e(TAG, "Proxy failed to become ready after $maxAttempts attempts (${maxAttempts * delayMs}ms)")
            false
        }
    }

    /**
     * Wait for NaiveProxy to report readiness via its "Listening on" log line.
     * Unlike waitForProxyReady (which opens a TCP connection), this checks the
     * NaiveBridge.isReady flag set by the output reader thread. This avoids
     * sending a non-SOCKS5 probe that causes ERR_SOCKS_CONNECTION_FAILED noise.
     */
    private suspend fun waitForNaiveReady(maxAttempts: Int = 30, delayMs: Long = 200): Boolean {
        Log.d(TAG, "Waiting for NaiveProxy to become ready (max ${maxAttempts * delayMs}ms)")
        return kotlinx.coroutines.withContext(kotlinx.coroutines.Dispatchers.IO) {
            repeat(maxAttempts) { attempt ->
                if (!NaiveBridge.isRunning()) {
                    Log.e(TAG, "NaiveProxy process died during startup (attempt ${attempt + 1})")
                    return@withContext false
                }
                if (NaiveBridge.isReady) {
                    Log.i(TAG, "NaiveProxy ready after ${attempt + 1} attempts (${(attempt + 1) * delayMs}ms)")
                    return@withContext true
                }
                if (attempt < maxAttempts - 1) {
                    Thread.sleep(delayMs)
                }
            }
            Log.e(TAG, "NaiveProxy failed to become ready after $maxAttempts attempts")
            false
        }
    }

    /**
     * Start periodic health check to detect if the Rust client has crashed
     * or if the connection has become stale.
     */
    private fun startHealthCheck() {
        healthCheckJob?.cancel()
        quicDownChecks = 0
        healthCheckCount = 0
        dnsPoolDeadChecks = 0
        tunnelStallChecks = 0
        lastTxBytes = 0L
        lastRxBytes = 0L
        seamlessReconnectAttempts = 0  // Reset on successful reconnection

        // Event-driven DNS pool death: react in ~8s instead of waiting 3 polls (45s).
        // The polled check in the loop below remains as a safety net.
        if (currentTunnelType == TunnelType.DNSTT || currentTunnelType == TunnelType.NOIZDNS || currentTunnelType == TunnelType.VAYDNS) {
            DnsttSocksBridge.onDnsPoolDead = {
                runtimeScope.launch(Dispatchers.Main) {
                    if (healthCheckJob?.isActive == true) {
                        handleTunnelFailure("DNS workers dead")
                    }
                }
            }
        }

        healthCheckJob = runtimeScope.launch(Dispatchers.IO) {
            // Give the connection time to establish before monitoring
            delay(10_000L)

            while (isActive) {
                delay(HEALTH_CHECK_INTERVAL_MS)
                healthCheckCount++

                // Check if both native clients are still running and healthy
                val proxyHealthy = isCurrentProxyHealthy()
                val tunnelRunning = if (isProxyOnly) true else HevSocks5Tunnel.isRunning()

                if (!proxyHealthy || !tunnelRunning) {
                    Log.e(TAG, "Health check failed: proxy=$proxyHealthy (type=$currentTunnelType), tunnel=$tunnelRunning")
                    runtimeScope.launch(Dispatchers.Main) {
                        handleTunnelFailure("health check failed")
                    }
                    break
                }

                // For Slipstream types: check if QUIC connection is alive.
                if (currentTunnelType == TunnelType.SLIPSTREAM || currentTunnelType == TunnelType.SLIPSTREAM_SSH) {
                    if (SlipstreamBridge.isQuicReady()) {
                        if (quicDownChecks > 0) {
                            Log.i(TAG, "QUIC connection recovered after $quicDownChecks checks")
                        }
                        quicDownChecks = 0
                    } else {
                        quicDownChecks++
                        Log.w(TAG, "QUIC not ready ($quicDownChecks/${QUIC_DOWN_THRESHOLD})")
                        if (quicDownChecks >= QUIC_DOWN_THRESHOLD) {
                            Log.e(TAG, "QUIC connection dead for ${quicDownChecks * HEALTH_CHECK_INTERVAL_MS}ms, triggering reconnect")
                            quicDownChecks = 0
                            runtimeScope.launch(Dispatchers.Main) {
                                handleTunnelFailure("QUIC connection lost")
                            }
                            break
                        }
                    }
                }

                // For SSH-based tunnels: actively probe the SSH session every ~30s.
                val usesSsh = currentTunnelType == TunnelType.SSH ||
                        currentTunnelType == TunnelType.DNSTT_SSH ||
                        currentTunnelType == TunnelType.NOIZDNS_SSH ||
                        currentTunnelType == TunnelType.VAYDNS_SSH ||
                        currentTunnelType == TunnelType.SOAR_SSH ||
                        currentTunnelType == TunnelType.SLIPSTREAM_SSH ||
                        currentTunnelType == TunnelType.NAIVE_SSH
                if (usesSsh && healthCheckCount % SSH_PROBE_INTERVAL == 0) {
                    val alive = SshTunnelBridge.probeSessionAlive()
                    if (!alive) {
                        Log.e(TAG, "SSH session probe failed — session is dead")
                        runtimeScope.launch(Dispatchers.Main) {
                            handleTunnelFailure("SSH session unresponsive")
                        }
                        break
                    }
                }

                // For tunnels with DNS worker pools: warn when all workers are dead.
                val dnsPoolDead = when (currentTunnelType) {
                    TunnelType.DNSTT, TunnelType.NOIZDNS, TunnelType.VAYDNS -> DnsttSocksBridge.isDnsPoolDead()
                    TunnelType.SLIPSTREAM -> SlipstreamSocksBridge.isDnsPoolDead()
                    TunnelType.NAIVE -> NaiveSocksBridge.isDnsPoolDead()
                    TunnelType.SSH, TunnelType.DNSTT_SSH, TunnelType.NOIZDNS_SSH,
                    TunnelType.VAYDNS_SSH, TunnelType.SLIPSTREAM_SSH, TunnelType.NAIVE_SSH,
                    TunnelType.SOAR_SSH -> SshTunnelBridge.isDnsPoolDead()
                    else -> false
                }
                if (dnsPoolDead) {
                    dnsPoolDeadChecks++
                    val isDnsTunneled = currentTunnelType in listOf(
                        TunnelType.DNSTT, TunnelType.DNSTT_SSH,
                        TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH,
                        TunnelType.VAYDNS, TunnelType.VAYDNS_SSH,
                        TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH,
                        TunnelType.SOAR, TunnelType.SOAR_SSH
                    )
                    val threshold = if (isDnsTunneled) DNS_POOL_DEAD_THRESHOLD else DNS_POOL_DEAD_THRESHOLD_SOCKS
                    if (dnsPoolDeadChecks >= threshold) {
                        Log.e(TAG, "All DNS workers dead for ${dnsPoolDeadChecks * HEALTH_CHECK_INTERVAL_MS / 1000}s, triggering reconnect")
                        runtimeScope.launch(Dispatchers.Main) {
                            handleTunnelFailure("DNS workers dead")
                        }
                        break
                    }
                } else {
                    dnsPoolDeadChecks = 0
                }

                // Traffic stall detection: if VPN is forwarding outgoing packets but
                // getting nothing back, the tunnel is dead (connected but can't transfer data).
                val isDnsTunneledStall = currentTunnelType in listOf(
                    TunnelType.DNSTT, TunnelType.DNSTT_SSH,
                    TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH,
                    TunnelType.VAYDNS, TunnelType.VAYDNS_SSH,
                    TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH,
                    TunnelType.SOAR, TunnelType.SOAR_SSH
                )
                val stallCheckInterval = if (isDnsTunneledStall) TUNNEL_STALL_CHECK_INTERVAL else TUNNEL_STALL_CHECK_INTERVAL_SOCKS
                val stallThreshold = if (isDnsTunneledStall) TUNNEL_STALL_THRESHOLD else TUNNEL_STALL_THRESHOLD_SOCKS
                // DoH: only DNS is routed (/32), so TX-without-RX is normal during
                // idle periods — skip stall detection to avoid false positives on TV/idle devices.
                if (!isProxyOnly && currentTunnelType != TunnelType.DOH && healthCheckCount % stallCheckInterval == 0) {
                    val stats = HevSocks5Tunnel.getStats()
                    if (stats != null) {
                        val txIncreased = stats.txBytes > lastTxBytes
                        val rxIncreased = stats.rxBytes > lastRxBytes

                        if (txIncreased && !rxIncreased) {
                            tunnelStallChecks++
                            Log.w(TAG, "Tunnel stall detected ($tunnelStallChecks/$stallThreshold): tx flowing but no rx")
                            if (tunnelStallChecks >= stallThreshold) {
                                Log.e(TAG, "Tunnel stalled — data sent but no response for ~${tunnelStallChecks * stallCheckInterval * HEALTH_CHECK_INTERVAL_MS / 1000}s")
                                tunnelStallChecks = 0
                                runtimeScope.launch(Dispatchers.Main) {
                                    handleTunnelFailure("tunnel not responding")
                                }
                                break
                            }
                        } else {
                            if (tunnelStallChecks > 0) {
                                Log.i(TAG, "Tunnel stall recovered after $tunnelStallChecks checks")
                            }
                            tunnelStallChecks = 0
                        }

                        lastTxBytes = stats.txBytes
                        lastRxBytes = stats.rxBytes
                    }
                }

                // Capacity exhaustion: all CONNECT semaphore slots stuck for >60s.
                // This happens when the transport dies (e.g. QUIC) but localhost TCP
                // sockets stay open, causing handshake reads to hang indefinitely.
                val capacityExhausted = when (currentTunnelType) {
                    TunnelType.SLIPSTREAM -> SlipstreamSocksBridge.isCapacityExhausted()
                    TunnelType.DNSTT, TunnelType.NOIZDNS, TunnelType.VAYDNS -> DnsttSocksBridge.isCapacityExhausted()
                    else -> false
                }
                if (capacityExhausted) {
                    Log.e(TAG, "Bridge capacity exhausted — all CONNECT slots stuck, triggering reconnect")
                    runtimeScope.launch(Dispatchers.Main) {
                        handleTunnelFailure("bridge capacity exhausted")
                    }
                    break
                }

            }
        }
    }

    /**
     * Update the VPN's underlying networks for seamless handover.
     * Tells Android which physical networks the VPN uses, enabling
     * automatic failover during WiFi↔cellular switches without full reconnection.
     */
    private fun updateUnderlyingNetworks() {
        // All unbound upstream sockets use Android's default network. This is a
        // declaration, not a route selector: listing every available radio was inaccurate.
        if (!underlyingDefaultDeclared) underlyingDefaultDeclared = runCatching { setUnderlyingNetworks(null) }.getOrDefault(false)
    }

    private fun defaultPhysicalNetwork(): Network? {
        val cm = connectivityManager ?: return null
        val network = cm.activeNetwork ?: return null
        val capabilities = cm.getNetworkCapabilities(network) ?: return null
        return network.takeIf { capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN) &&
            capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) }
    }

    /**
     * Register a BroadcastReceiver to detect when the device exits Doze mode.
     * Doze suspends network access — when it lifts, connections may be stale
     * and need immediate refresh.
     */
    private fun registerDozeReceiver() {
        if (dozeReceiver != null) return
        dozeReceiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context, intent: Intent) {
                val pm = context.getSystemService(Context.POWER_SERVICE) as PowerManager
                if (intent.action == Intent.ACTION_SCREEN_ON || intent.action == Intent.ACTION_SCREEN_OFF) {
                    updateRuntimePowerPolicy(pm.isInteractive)
                    trafficPollSignal.trySend(Unit)
                    return
                }
                if (pm.isDeviceIdleMode) {
                    Log.d(TAG, "Device entered Doze mode")
                } else {
                    Log.i(TAG, "Device exited Doze mode — checking connection health")
                    // Update underlying networks immediately (network state may have changed in Doze)
                    updateUnderlyingNetworks()
                    // Only reconnect if the proxy is actually unhealthy.
                    // Unconditional reconnect kills working DNSTT/SSH connections
                    // and causes unnecessary downtime on every Doze cycle.
                    if (!isCurrentProxyHealthy()) {
                        Log.i(TAG, "Proxy unhealthy after Doze — reconnecting")
                        debouncedReconnect("doze mode exit")
                    } else {
                        Log.d(TAG, "Proxy healthy after Doze — no reconnect needed")
                    }
                }
            }
        }
        val filter = IntentFilter(PowerManager.ACTION_DEVICE_IDLE_MODE_CHANGED).apply {
            addAction(Intent.ACTION_SCREEN_ON)
            addAction(Intent.ACTION_SCREEN_OFF)
        }
        @Suppress("UnspecifiedRegisterReceiverFlag")
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(dozeReceiver, filter, Context.RECEIVER_NOT_EXPORTED)
        } else registerReceiver(dozeReceiver, filter)
        Log.d(TAG, "Doze mode receiver registered")
    }

    private fun unregisterDozeReceiver() {
        dozeReceiver?.let {
            try {
                unregisterReceiver(it)
                Log.d(TAG, "Doze mode receiver unregistered")
            } catch (e: Exception) {
                Log.w(TAG, "Failed to unregister Doze receiver", e)
            }
        }
        dozeReceiver = null
    }

    /**
     * Register for network connectivity changes to detect when we need to reconnect.
     */
    private fun registerNetworkCallback() {
        val callbackRuntime = runtimeJob
        unregisterNetworkCallback()
        networkCallback = object : ConnectivityManager.NetworkCallback() {
            private var currentNetwork: Network? = defaultPhysicalNetwork()
            private var initialized = currentNetwork != null

            private fun consider(network: Network, capabilities: NetworkCapabilities) {
                if (!callbackRuntime.isActive || !capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN) ||
                    !capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)) return
                networkLostJob?.cancel()
                networkLostJob = null
                val previous = currentNetwork
                currentNetwork = network
                if (previous != network) {
                    lastNetworkAddresses = emptySet()
                    lastNetworkDnsServers = emptySet()
                    if (initialized) debouncedReconnect(if (previous == null) "physical network restored" else "physical network change")
                }
                initialized = true
                val kind = when {
                    capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> app.slipnet.util.ConnectionDiagnostics.NetworkKind.WIFI
                    capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> app.slipnet.util.ConnectionDiagnostics.NetworkKind.CELLULAR
                    capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> app.slipnet.util.ConnectionDiagnostics.NetworkKind.ETHERNET
                    else -> app.slipnet.util.ConnectionDiagnostics.NetworkKind.OTHER
                }
                app.slipnet.util.ConnectionDiagnostics.network(serviceSessionId, kind)
                if (previous != network) trafficPollSignal.trySend(Unit)
            }

            override fun onCapabilitiesChanged(network: Network, capabilities: NetworkCapabilities) = consider(network, capabilities)

            override fun onLost(network: Network) {
                if (!callbackRuntime.isActive || network != currentNetwork) return
                currentNetwork = null
                lastNetworkAddresses = emptySet()
                lastNetworkDnsServers = emptySet()
                networkLostJob?.cancel()
                networkLostJob = runtimeScope.launch {
                    delay(3000)
                    if (!callbackRuntime.isActive || currentNetwork != null) return@launch
                    val replacement = defaultPhysicalNetwork()
                    if (replacement != null) {
                        connectivityManager?.getNetworkCapabilities(replacement)?.let { consider(replacement, it) }
                    } else {
                        val anyPhysical = connectivityManager?.allNetworks?.any { candidate ->
                            connectivityManager?.getNetworkCapabilities(candidate)?.let { caps ->
                                caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN) &&
                                    caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                            } == true
                        } == true
                        if (!anyPhysical) handleTunnelFailure("network lost")
                    }
                }
            }

            override fun onLinkPropertiesChanged(network: Network, properties: LinkProperties) {
                if (!callbackRuntime.isActive || network != currentNetwork) return
                val addresses = properties.linkAddresses.mapNotNull { it.address?.hostAddress }.toSet()
                val dns = properties.dnsServers.mapNotNull { it.hostAddress }.toSet()
                val changed = lastNetworkAddresses.isNotEmpty() && lastNetworkAddresses != addresses ||
                    lastNetworkDnsServers.isNotEmpty() && lastNetworkDnsServers != dns
                lastNetworkAddresses = addresses
                lastNetworkDnsServers = dns
                if (changed) debouncedReconnect("physical network properties change")
            }
        }
        try { connectivityManager?.registerDefaultNetworkCallback(requireNotNull(networkCallback)) }
        catch (error: Exception) { Log.e(TAG, "Failed to register default network callback", error) }
    }

    private fun unregisterNetworkCallback() {
        networkCallback?.let { callback ->
            try {
                connectivityManager?.unregisterNetworkCallback(callback)
                Log.d(TAG, "Network callback unregistered")
            } catch (e: Exception) {
                Log.e(TAG, "Failed to unregister network callback", e)
            }
        }
        networkCallback = null
    }

    /**
     * Debounced reconnection to avoid thrashing on rapid network changes.
     * Waits 2s before triggering reconnection in case more changes come in.
     */
    private fun debouncedReconnect(reason: String) {
        // A reconnect supersedes any pending "network lost" disconnect
        networkLostJob?.cancel()
        networkLostJob = null
        reconnectDebounceJob?.cancel()
        reconnectDebounceJob = runtimeScope.launch {
            Log.d(TAG, "Debouncing reconnect for: $reason")
            delay(2000) // Wait 2s for network to stabilize
            handleNetworkChange(reason)
        }
    }

    /**
     * Handle network change by restarting the tunnel connection.
     * Keeps HevSocks5Tunnel (tun2socks) running during the proxy restart to minimize
     * the traffic gap. The TUN interface stays alive and tun2socks buffers/retries
     * connections until the new proxy is ready.
     */
    private fun handleNetworkChange(reason: String = "unknown") {
        if (isReconnecting) return
        val session = serviceSessionId
        runtimeScope.launch {
            var failure: String? = null
            TunnelOperations.mutex.withLock {
            if (!connectionManager.ownsSession(session) || TunnelOperations.owner != session) return@launch
            // Ignore spurious network changes fired shortly after connection.
            // Chinese OEM ROMs (MIUI/HyperOS, EMUI) trigger network callbacks
            // when the VPN interface is first created, causing the proxy to be
            // torn down before any data flows. Skip unless this is a tunnel
            // recovery (seamless reconnect), which must always proceed.
            if (!reason.startsWith("tunnel recovery") && !reason.startsWith("physical network") && connectionEstablishedAt > 0) {
                val elapsed = System.currentTimeMillis() - connectionEstablishedAt
                if (elapsed < NETWORK_CHANGE_GRACE_MS) {
                    Log.d(TAG, "Ignoring network change '$reason' — ${elapsed}ms after connection (grace period ${NETWORK_CHANGE_GRACE_MS}ms)")
                    return@launch
                }
            }

            // Prevent concurrent reconnection attempts
            if (isReconnecting) {
                Log.d(TAG, "Skipping reconnection for '$reason' - already reconnecting")
                return@launch
            }
            isReconnecting = true

            try {
                Log.i(TAG, "Handling network change ($reason) - restarting connection")

                // Update underlying networks immediately for the new network
                updateUnderlyingNetworks()

                // Stop health check during reconnection
                healthCheckJob?.cancel()

                val previousConnection = vpnRepository.connectionState.value as? ConnectionState.Connected
                vpnRepository.updateConnectionState(ConnectionState.Connecting)

                // Get the current profile
                val profile = collectionProfile(connectionManager.getProfileById(currentProfileId))
                if (profile == null) {
                    throw RecoveryFailure("profile not found during reconnect")
                }

                if (profile.needsCredentials) throw RecoveryFailure(SAVED_CREDENTIALS_UNAVAILABLE)

                // Save chain state before stopCurrentProxy clears it
                val savedChainId = currentChainId
                val wasChain = activeChainLayers.isNotEmpty()

                // Stop proxy on IO but keep HevSocks5Tunnel (tun2socks) running.
                // The TUN interface stays alive and tun2socks will retry connections
                // once the new proxy is ready, minimizing the traffic interruption gap.
                withContext(Dispatchers.IO) {
                    stopCurrentProxy()
                }

                // Chain reconnection: re-execute the full chain.
                // Chains rebuild the VPN interface, so stop tun2socks first.
                if (wasChain && savedChainId > 0) {
                    if (!isProxyOnly) {
                        withContext(Dispatchers.IO) {
                            try { HevSocks5Tunnel.stop() } catch (_: Exception) {}
                        }
                    }
                    Log.i(TAG, "Reconnecting chain $savedChainId after network change")
                    val chain = chainRepository.getChainById(savedChainId)
                    if (chain == null) {
                        Log.e(TAG, "Cannot reconnect chain: chain not found")
                        throw RecoveryFailure("chain not found during reconnect")
                    }
                    val chainProfiles = chain.profileIds.mapNotNull { connectionManager.getProfileById(it) }
                    if (chainProfiles.any { it.needsCredentials }) throw RecoveryFailure(SAVED_CREDENTIALS_UNAVAILABLE)
                    if (chainProfiles.size != chain.profileIds.size) {
                        Log.e(TAG, "Cannot reconnect chain: some profiles were deleted")
                        throw RecoveryFailure("chain profiles missing during reconnect")
                    }
                    currentChainId = savedChainId
                    executeChain(chainProfiles)
                    Log.i(TAG, "Chain reconnected successfully after network change")
                    return@launch
                }

                // For DNSTT-based tunnels, explicitly wait for the Go runtime to
                // fully release the port.  stopCurrentProxy() already waits inside
                // DnsttBridge.stopClient(), but we add an extra coroutine-friendly
                // check here to be safe — this avoids spawning a second DNSTT
                // instance on a fallback port (which caused massive upload leaks).
                val isDnstt = currentTunnelType in listOf(
                    TunnelType.DNSTT, TunnelType.DNSTT_SSH,
                    TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH
                )
                if (isDnstt) {
                    DnsttBridge.stopClientBlocking()  // no-op if already stopped, but ensures port is released
                }
                val isVaydns = currentTunnelType in listOf(
                    TunnelType.VAYDNS, TunnelType.VAYDNS_SSH
                )
                if (isVaydns) {
                    VaydnsBridge.stopClientBlocking()  // no-op if already stopped, but ensures port is released
                }
                if (currentTunnelType == TunnelType.SOAR || currentTunnelType == TunnelType.SOAR_SSH) {
                    SoarBridge.stopClientBlocking()  // no-op if already stopped, but ensures port is released
                }

                val proxyPort = preferencesDataStore.proxyListenPort.first()
                val proxyHost = preferencesDataStore.proxyListenAddress.first()
                var remoteDns = preferencesDataStore.getEffectiveRemoteDns().first()
                var remoteDnsFallback = preferencesDataStore.getEffectiveRemoteDnsFallback().first()

                // DNSTT+SSH / NoizDNS+SSH / VayDNS+SSH: default to server's local resolver (same override as initial connect)
                if (currentTunnelType == TunnelType.DNSTT_SSH || currentTunnelType == TunnelType.NOIZDNS_SSH || currentTunnelType == TunnelType.VAYDNS_SSH) {
                    val dnsMode = preferencesDataStore.remoteDnsMode.first()
                    if (dnsMode == "default") {
                        remoteDns = "127.0.0.53"
                        remoteDnsFallback = "8.8.8.8"
                    }
                }

                // Resolve SSH hostname via global DNS if set (same as initial connect)
                val reconnectGlobalDnsIp = if (preferencesDataStore.globalResolverEnabled.first()) {
                    preferencesDataStore.globalResolverList.first()
                        .split(",", "\n").firstOrNull()?.trim()?.split(":")?.firstOrNull()
                        ?.takeIf { DomainRouter.isIpAddress(it) }
                } else null
                val reconnectProfile = if (reconnectGlobalDnsIp != null && !DomainRouter.isIpAddress(profile.domain)) {
                    val resolved = VpnRepositoryImpl.resolveHost(profile.domain, reconnectGlobalDnsIp)
                    if (resolved != profile.domain) profile.copy(domain = resolved) else profile
                } else profile

                // Restart the appropriate proxy
                if (currentTunnelType == TunnelType.SSH) {
                    // SSH-only: restart SSH tunnel using configured transport
                    configureSshBridge()
                    val sshResult = withContext(Dispatchers.IO) {
                        startSshWithTransport(reconnectProfile, proxyHost, proxyPort, remoteDns, remoteDnsFallback)
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH tunnel after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect SSH after network change")
                    }

                    // Wait for SSH SOCKS5 proxy to be ready
                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.DNSTT_SSH || currentTunnelType == TunnelType.NOIZDNS_SSH) {
                    // DNSTT+SSH / NoizDNS+SSH: restart tunnel on internal port, then SSH on proxyPort
                    val dnsttPort = proxyPort + 1
                    val isNoizdns = currentTunnelType == TunnelType.NOIZDNS_SSH

                    val dnsttResult = if (isNoizdns) {
                        vpnRepository.startNoizdnsProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1")
                    } else {
                        vpnRepository.startDnsttProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1")
                    }
                    if (dnsttResult.isFailure) {
                        Log.e(TAG, "Failed to restart ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change", dnsttResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"}+SSH after network change")
                    }

                    val actualDnsttPort = DnsttBridge.getClientPort()

                    if (!waitForProxyReady(actualDnsttPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "${if (isNoizdns) "NoizDNS" else "DNSTT"} proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"}+SSH after network change")
                    }

                    vpnRepository.setCurrentTunnelType(currentTunnelType!!)

                    // Connect SSH directly through the tunnel's local port
                    configureSshBridge()
                    val sshResult = withContext(Dispatchers.IO) {
                        SshTunnelBridge.startOverProxy(
                            sshHost = profile.sshHost,
                            sshPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            proxyHost = "127.0.0.1",
                            proxyPort = actualDnsttPort,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            blockDirectDns = true,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback
                        )
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH over ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"}+SSH after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"}+SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.VAYDNS_SSH) {
                    // VayDNS+SSH: restart VayDNS on internal port, then SSH on proxyPort
                    val vaydnsPort = proxyPort + 1

                    val vaydnsResult = vpnRepository.startVaydnsProxy(profile, portOverride = vaydnsPort, hostOverride = "127.0.0.1")
                    if (vaydnsResult.isFailure) {
                        Log.e(TAG, "Failed to restart VayDNS after network change", vaydnsResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect VayDNS+SSH after network change")
                    }

                    val actualVaydnsPort = VaydnsBridge.getClientPort()

                    if (!waitForProxyReady(actualVaydnsPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "VayDNS proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect VayDNS+SSH after network change")
                    }

                    vpnRepository.setCurrentTunnelType(currentTunnelType!!)

                    // Connect SSH directly through the tunnel's local port
                    configureSshBridge()
                    val sshResult = withContext(Dispatchers.IO) {
                        SshTunnelBridge.startOverProxy(
                            sshHost = profile.sshHost,
                            sshPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            proxyHost = "127.0.0.1",
                            proxyPort = actualVaydnsPort,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            blockDirectDns = true,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback
                        )
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH over VayDNS after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect VayDNS+SSH after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect VayDNS+SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.SOAR_SSH) {
                    // Soar+SSH: restart Soar on internal port, then SSH through its SOCKS5 on proxyPort
                    val soarResult = vpnRepository.startSoarProxy(
                        profile, portOverride = proxyPort + 1, hostOverride = "127.0.0.1",
                        resolverOverride = (collectionResolvers ?: preferencesDataStore.parsedGlobalResolvers()).takeIf { it.isNotEmpty() }
                    )
                    if (soarResult.isFailure) {
                        Log.e(TAG, "Failed to restart Soar after network change", soarResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Soar+SSH after network change")
                    }

                    val actualSoarPort = SoarBridge.getClientPort()

                    if (!waitForProxyReady(actualSoarPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Soar proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect Soar+SSH after network change")
                    }

                    vpnRepository.setCurrentTunnelType(currentTunnelType!!)

                    configureSshBridge()
                    val sshResult = withContext(Dispatchers.IO) {
                        SshTunnelBridge.startOverSocks5Proxy(
                            sshHost = profile.sshHost,
                            sshPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            proxyHost = "127.0.0.1",
                            proxyPort = actualSoarPort,
                            socksUsername = null,
                            socksPassword = null,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            blockDirectDns = true,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback
                        )
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH over Soar after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Soar+SSH after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect Soar+SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.SLIPSTREAM_SSH) {
                    // Slipstream+SSH: restart Slipstream on internal port, wait QUIC, then SSH on proxyPort
                    val slipstreamPort = proxyPort + 1

                    val slipResult = vpnRepository.startSlipstreamProxy(profile, portOverride = slipstreamPort, hostOverride = "127.0.0.1")
                    if (slipResult.isFailure) {
                        Log.e(TAG, "Failed to restart Slipstream after network change", slipResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Slipstream+SSH after network change")
                    }

                    val actualSlipstreamPort = SlipstreamBridge.getClientPort()

                    if (!waitForProxyReady(actualSlipstreamPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Slipstream proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect Slipstream+SSH after network change")
                    }

                    val quicReady = waitForQuicReady(maxAttempts = 50, delayMs = 100)
                    if (!quicReady) {
                        Log.w(TAG, "QUIC connection not ready after reconnect, continuing anyway")
                    }

                    vpnRepository.setCurrentTunnelType(TunnelType.SLIPSTREAM_SSH)

                    configureSshBridge()
                    val sshResult = withContext(Dispatchers.IO) {
                        SshTunnelBridge.startOverProxy(
                            sshHost = profile.sshHost,
                            sshPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            proxyHost = "127.0.0.1",
                            proxyPort = actualSlipstreamPort,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            blockDirectDns = true,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback
                        )
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH over Slipstream after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Slipstream+SSH after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect Slipstream+SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.SLIPSTREAM) {
                    // Slipstream: restart proxy on internal port + bridge on proxyPort
                    val slipstreamPort = proxyPort + 1

                    val slipResult = vpnRepository.startSlipstreamProxy(profile, portOverride = slipstreamPort, hostOverride = "127.0.0.1")
                    if (slipResult.isFailure) {
                        Log.e(TAG, "Failed to restart Slipstream after network change", slipResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Slipstream after network change")
                    }

                    val actualSlipstreamPort = SlipstreamBridge.getClientPort()

                    if (!waitForProxyReady(actualSlipstreamPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Slipstream proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect Slipstream after network change")
                    }

                    val quicReady = waitForQuicReady(maxAttempts = 50, delayMs = 100)
                    if (!quicReady) {
                        Log.w(TAG, "QUIC not ready after reconnect, continuing anyway")
                    }

                    // Restart bridge on proxyPort (with auth for Dante)
                    SlipstreamSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    val bridgeResult = vpnRepository.startSlipstreamSocksBridge(
                        slipstreamPort = actualSlipstreamPort,
                        slipstreamHost = "127.0.0.1",
                        bridgePort = proxyPort,
                        bridgeHost = proxyHost,
                        socksUsername = profile.socksUsername,
                        socksPassword = profile.socksPassword
                    )
                    if (bridgeResult.isFailure) {
                        Log.e(TAG, "Failed to restart bridge after network change")
                        throw RecoveryFailure("failed to reconnect after network change")
                    }

                    if (currentTunnelType != TunnelType.XRAY && !waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Bridge failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect after network change")
                    }
                } else if (currentTunnelType == TunnelType.DNSTT || currentTunnelType == TunnelType.NOIZDNS) {
                    // DNSTT / NoizDNS: restart tunnel on internal port + bridge on proxyPort
                    val dnsttPort = proxyPort + 1
                    val isNoizdns = currentTunnelType == TunnelType.NOIZDNS

                    val dnsttResult = if (isNoizdns) {
                        vpnRepository.startNoizdnsProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1")
                    } else {
                        vpnRepository.startDnsttProxy(profile, portOverride = dnsttPort, hostOverride = "127.0.0.1")
                    }
                    if (dnsttResult.isFailure) {
                        Log.e(TAG, "Failed to restart ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change", dnsttResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change")
                    }

                    val actualDnsttPort = DnsttBridge.getClientPort()

                    if (!waitForProxyReady(actualDnsttPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "${if (isNoizdns) "NoizDNS" else "DNSTT"} proxy failed to restart on port $actualDnsttPort")
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change")
                    }

                    // Restart bridge on proxyPort (with auth for Dante)
                    DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
                    DnsttSocksBridge.proxyOnlyMode = isProxyOnly
                    DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    val bridgeResult = vpnRepository.startDnsttSocksBridge(
                        dnsttPort = actualDnsttPort,
                        dnsttHost = "127.0.0.1",
                        bridgePort = proxyPort,
                        bridgeHost = proxyHost,
                        socksUsername = profile.socksUsername,
                        socksPassword = profile.socksPassword,
                        dnsServer = remoteDns,
                        dnsFallback = remoteDnsFallback
                    )
                    if (bridgeResult.isFailure) {
                        Log.e(TAG, "Failed to restart ${if (isNoizdns) "NoizDNS" else "DNSTT"} bridge after network change")
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change")
                    }

                    if (currentTunnelType != TunnelType.XRAY && !waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "${if (isNoizdns) "NoizDNS" else "DNSTT"} bridge failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect ${if (isNoizdns) "NoizDNS" else "DNSTT"} after network change")
                    }
                } else if (currentTunnelType == TunnelType.VAYDNS) {
                    // VayDNS: restart tunnel on internal port + bridge on proxyPort
                    val vaydnsPort = proxyPort + 1

                    val vaydnsResult = vpnRepository.startVaydnsProxy(profile, portOverride = vaydnsPort, hostOverride = "127.0.0.1")
                    if (vaydnsResult.isFailure) {
                        Log.e(TAG, "Failed to restart VayDNS after network change", vaydnsResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect VayDNS after network change")
                    }

                    val actualVaydnsPort = VaydnsBridge.getClientPort()

                    if (!waitForProxyReady(actualVaydnsPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "VayDNS proxy failed to restart on port $actualVaydnsPort")
                        throw RecoveryFailure("failed to reconnect VayDNS after network change")
                    }

                    // Restart bridge on proxyPort (with auth for Dante)
                    DnsttSocksBridge.upstreamRunningCheck = { VaydnsBridge.isRunning() }
                    DnsttSocksBridge.authoritativeMode = profile.dnsttAuthoritative
                    DnsttSocksBridge.proxyOnlyMode = isProxyOnly
                    DnsttSocksBridge.dnsWorkerPoolSize = preferencesDataStore.dnsWorkerMode.first().poolSize
                    val bridgeResult = vpnRepository.startDnsttSocksBridge(
                        dnsttPort = actualVaydnsPort,
                        dnsttHost = "127.0.0.1",
                        bridgePort = proxyPort,
                        bridgeHost = proxyHost,
                        socksUsername = profile.socksUsername,
                        socksPassword = profile.socksPassword,
                        dnsServer = remoteDns,
                        dnsFallback = remoteDnsFallback
                    )
                    if (bridgeResult.isFailure) {
                        Log.e(TAG, "Failed to restart VayDNS bridge after network change")
                        throw RecoveryFailure("failed to reconnect VayDNS after network change")
                    }

                    if (currentTunnelType != TunnelType.XRAY && !waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "VayDNS bridge failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect VayDNS after network change")
                    }
                } else if (currentTunnelType == TunnelType.SOAR) {
                    // Soar: restart Soar on internal port + SOCKS5 bridge on proxyPort
                    val soarResult = vpnRepository.startSoarProxy(
                        profile, portOverride = proxyPort + 1, hostOverride = "127.0.0.1",
                        resolverOverride = (collectionResolvers ?: preferencesDataStore.parsedGlobalResolvers()).takeIf { it.isNotEmpty() }
                    )
                    if (soarResult.isFailure) {
                        Log.e(TAG, "Failed to restart Soar after network change", soarResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect Soar after network change")
                    }

                    val actualSoarPort = SoarBridge.getClientPort()

                    if (!waitForProxyReady(actualSoarPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Soar proxy failed to restart on port $actualSoarPort")
                        throw RecoveryFailure("failed to reconnect Soar after network change")
                    }

                    val authEnabled = preferencesDataStore.proxyAuthEnabled.first()
                    val bridgeResult = withContext(Dispatchers.IO) {
                        Socks5ProxyBridge.start(
                            remoteHost = "127.0.0.1",
                            remotePort = actualSoarPort,
                            remoteUsername = null,
                            remotePassword = null,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            dnsHost = remoteDns,
                            dnsFallback = remoteDnsFallback,
                            localAuthUsername = if (authEnabled) preferencesDataStore.proxyAuthUsername.first().ifEmpty { null } else null,
                            localAuthPassword = if (authEnabled) preferencesDataStore.proxyAuthPassword.first().ifEmpty { null } else null
                        )
                    }
                    if (bridgeResult.isFailure) {
                        Log.e(TAG, "Failed to restart Soar bridge after network change")
                        throw RecoveryFailure("failed to reconnect Soar after network change")
                    }

                    if (currentTunnelType != TunnelType.XRAY && !waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Soar bridge failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect Soar after network change")
                    }
                } else if (currentTunnelType == TunnelType.NAIVE_SSH) {
                    // NaiveProxy+SSH: restart NaiveProxy on internal port, then SSH on proxyPort
                    val naivePort = proxyPort + 1

                    val naiveResult = withContext(Dispatchers.IO) {
                        NaiveBridge.start(
                            context = this@SlipNetVpnService,
                            listenPort = naivePort,
                            listenHost = "127.0.0.1",
                            serverHost = profile.domain,
                            serverPort = profile.naivePort,
                            username = profile.naiveUsername,
                            password = profile.naivePassword,
                                )
                    }
                    if (naiveResult.isFailure) {
                        Log.e(TAG, "Failed to restart NaiveProxy after network change", naiveResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect NaiveProxy+SSH after network change")
                    }

                    if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
                        Log.e(TAG, "NaiveProxy failed to become ready after network change")
                        throw RecoveryFailure("failed to reconnect NaiveProxy+SSH after network change")
                    }

                    val naiveSshHost = if (profile.sshHost == "127.0.0.1" || profile.sshHost.isBlank()) {
                        profile.domain
                    } else {
                        profile.sshHost
                    }

                    configureSshBridge()
                    val blockDns = preferencesDataStore.preventDnsFallback.first()
                    val sshResult = withContext(Dispatchers.IO) {
                        SshTunnelBridge.startOverSocks5Proxy(
                            sshHost = naiveSshHost,
                            sshPort = profile.sshPort,
                            sshUsername = profile.sshUsername,
                            sshPassword = profile.sshPassword,
                            proxyHost = "127.0.0.1",
                            proxyPort = naivePort,
                            socksUsername = null,
                            socksPassword = null,
                            listenPort = proxyPort,
                            listenHost = proxyHost,
                            blockDirectDns = blockDns,
                            sshAuthType = profile.sshAuthType,
                            sshPrivateKey = profile.sshPrivateKey,
                            sshKeyPassphrase = profile.sshKeyPassphrase,
                            remoteDnsHost = remoteDns,
                            remoteDnsFallback = remoteDnsFallback,
                            naiveMode = true
                        )
                    }
                    if (sshResult.isFailure) {
                        Log.e(TAG, "Failed to restart SSH over NaiveProxy after network change", sshResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect NaiveProxy+SSH after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "SSH SOCKS5 proxy failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect NaiveProxy+SSH after network change")
                    }
                } else if (currentTunnelType == TunnelType.NAIVE) {
                    // Standalone NaiveProxy: restart NaiveProxy on internal port, then bridge on proxyPort
                    val naivePort = proxyPort + 1

                    val naiveResult = withContext(Dispatchers.IO) {
                        NaiveBridge.start(
                            context = this@SlipNetVpnService,
                            listenPort = naivePort,
                            listenHost = "127.0.0.1",
                            serverHost = profile.domain,
                            serverPort = profile.naivePort,
                            username = profile.naiveUsername,
                            password = profile.naivePassword,
                        )
                    }
                    if (naiveResult.isFailure) {
                        Log.e(TAG, "Failed to restart NaiveProxy after network change", naiveResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect NaiveProxy after network change")
                    }

                    if (!waitForNaiveReady(maxAttempts = 30, delayMs = 200)) {
                        Log.e(TAG, "NaiveProxy failed to become ready after network change")
                        throw RecoveryFailure("failed to reconnect NaiveProxy after network change")
                    }

                    val bridgeResult = vpnRepository.startNaiveSocksBridge(
                        naivePort = naivePort,
                        naiveHost = "127.0.0.1",
                        bridgePort = proxyPort,
                        bridgeHost = proxyHost,
                        dnsServer = remoteDns,
                        dnsFallback = remoteDnsFallback
                    )
                    if (bridgeResult.isFailure) {
                        Log.e(TAG, "Failed to restart NaiveSocksBridge after network change")
                        throw RecoveryFailure("failed to reconnect NaiveProxy after network change")
                    }

                    if (!waitForProxyReady(proxyPort, maxAttempts = 30, delayMs = 50)) {
                        Log.e(TAG, "NaiveSocksBridge failed to restart on port $proxyPort")
                        throw RecoveryFailure("failed to reconnect NaiveProxy after network change")
                    }
                } else {
                    val proxyResult = when (currentTunnelType) {
                        TunnelType.XRAY -> withContext(Dispatchers.IO) {
                            XrayBridge.start(this@SlipNetVpnService, profile.coreLink, requireNotNull(coreEndpoint), buildDomainRouter().coreRules())
                        }
                        TunnelType.SOCKS5 -> startSocks5Bridge(profile, remoteDns, remoteDnsFallback)
                        TunnelType.VLESS -> startVlessBridge(profile)
                        TunnelType.DOH -> vpnRepository.startDohProxy(profile)
                        TunnelType.SNOWFLAKE -> {
                            val torSocksPort = proxyPort + 1
                            val snowflakePtPort = proxyPort + 2
                            vpnRepository.startSnowflakeProxy(profile, snowflakePtPort, torSocksPort, proxyPort)
                        }
                        else -> error("No recovery path for ${currentTunnelType.displayName}")
                    }
                    if (proxyResult.isFailure) {
                        Log.e(TAG, "Failed to restart proxy after network change", proxyResult.exceptionOrNull())
                        throw RecoveryFailure("failed to reconnect after network change")
                    }

                    // Wait for proxy to be ready
                    if (currentTunnelType != TunnelType.XRAY && !waitForProxyReady(proxyPort, maxAttempts = 20, delayMs = 50)) {
                        Log.e(TAG, "Proxy failed to restart")
                        throw RecoveryFailure("failed to reconnect after network change")
                    }
                }

                if (isProxyOnly) {
                    vpnRepository.setProxyConnected(profile)
                } else if (!withContext(Dispatchers.IO) { HevSocks5Tunnel.isRunning() }) {
                    val fd = vpnInterface ?: throw RecoveryFailure("VPN interface unavailable during recovery")
                    vpnRepository.startTun2Socks(profile, fd).getOrElse { throw RecoveryFailure("could not restart tun2socks") }
                }

                // Wait for tunnel to be re-established
                if (currentTunnelType == TunnelType.SLIPSTREAM) {
                    // QUIC wait already done during Slipstream reconnection above
                    delay(500)
                } else if (currentTunnelType == TunnelType.SLIPSTREAM_SSH) {
                    // QUIC wait already done during Slipstream+SSH reconnection above
                    delay(500)
                } else if (currentTunnelType == TunnelType.SNOWFLAKE) {
                    // Wait for Tor to re-bootstrap after network change
                    if (!waitForTorReady(maxWaitMs = 90000)) {
                        Log.e(TAG, "Tor failed to re-bootstrap after network change")
                        throw RecoveryFailure("Tor failed to reconnect after network change")
                    }
                } else if (currentTunnelType == TunnelType.NAIVE_SSH || currentTunnelType == TunnelType.NAIVE) {
                    delay(500)
                } else {
                    // For DNSTT/DNSTT+SSH, give it a moment to re-establish
                    delay(500)
                }

                // Restart HTTP proxy if it was running before (stopped by stopCurrentProxy)
                if (!HttpProxyServer.isRunning()) {
                    val appendProxy = preferencesDataStore.appendHttpProxyToVpn.first()
                    val httpEnabled = preferencesDataStore.httpProxyEnabled.first()
                    if (appendProxy || httpEnabled) {
                        val httpPort = preferencesDataStore.httpProxyPort.first()

                        // Use proxyListenAddress when HTTP proxy (LAN sharing) is enabled so other
                        // devices can reach it. Use 127.0.0.1 only when appendProxy is the sole
                        // reason (local VPN use only, no LAN sharing needed).
                        val listenHost = if (httpEnabled) preferencesDataStore.proxyListenAddress.first() else "127.0.0.1"

                        val result = startHttpProxy(listenHost, httpPort)
                        if (result.isFailure) {
                            Log.w(TAG, "HTTP proxy failed to restart after reconnect: ${result.exceptionOrNull()?.message}")
                        } else {
                            Log.i(TAG, "HTTP proxy restarted on $listenHost:$httpPort after reconnect")
                        }
                    }
                }

                vpnRepository.updateConnectionState(previousConnection ?: ConnectionState.Connected(profile))
                // Restart health check
                startHealthCheck()

                // Refresh underlying networks after successful reconnection
                updateUnderlyingNetworks()

                // Clear kill switch state and restore connected notification
                if (isKillSwitchActive) {
                    isKillSwitchActive = false
                    val state = vpnRepository.connectionState.first()
                    val notification = notificationHelper.createVpnNotification(state, isProxyOnly)
                    val notificationManager = getSystemService(NotificationManager::class.java)
                    notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)
                }

                Log.i(TAG, "Successfully reconnected after network change (tunnel type: $currentTunnelType)")
                resetTrafficBaseline = true
            } catch (e: kotlinx.coroutines.CancellationException) {
                throw e
            } catch (e: Exception) {
                failure = if (e is RecoveryFailure) e.message else "transport restart failed"
                Log.w(TAG, "Recovery failed (${e.javaClass.simpleName})")
            } finally {
                isReconnecting = false
                vpnRepository.setAutoReconnect(false)
            }
            }
            failure?.let {
                if (!isUserInitiatedDisconnect && connectionManager.acceptsStart(session) && TunnelOperations.owner == session) {
                    handleTunnelFailure(it)
                }
            }
        }
    }

    private class RecoveryFailure(reason: String) : Exception(reason)

    /**
     * Stop the currently running proxy based on tunnel type.
     * Stops SSH first if enabled, then the DNS tunnel.
     * Synchronized to prevent double-stop from onDestroy + coroutine cleanup.
     */
    @Synchronized
    private fun stopCurrentProxy() {
        // Always stop HTTP proxy (it chains through whatever SOCKS5 proxy is running)
        HttpProxyServer.stop()

        // If this was a chain connection, stop all layers in reverse order
        if (activeChainLayers.isNotEmpty()) {
            Log.d(TAG, "Stopping chain layers: ${activeChainLayers.reversed().joinToString(" → ") { it.displayName }}")
            for (layer in activeChainLayers.reversed()) {
                stopLayer(layer)
            }
            activeChainLayers = emptyList()
            currentChainId = -1
            return
        }

        when (currentTunnelType) {
            TunnelType.SLIPSTREAM -> {
                Log.d(TAG, "Stopping Slipstream proxy and bridge")
                SlipstreamSocksBridge.stop()
                SlipstreamBridge.stopClient()
            }
            TunnelType.SLIPSTREAM_SSH -> {
                Log.d(TAG, "Stopping Slipstream+SSH: SSH first, then Slipstream")
                SshTunnelBridge.stop()
                SlipstreamBridge.stopClient()
            }
            TunnelType.DNSTT -> {
                Log.d(TAG, "Stopping DNSTT proxy and bridge")
                DnsttSocksBridge.onDnsPoolDead = null
                DnsttSocksBridge.stop()
                DnsttBridge.stopClient()
            }
            TunnelType.NOIZDNS -> {
                Log.d(TAG, "Stopping NoizDNS proxy and bridge")
                DnsttSocksBridge.onDnsPoolDead = null
                DnsttSocksBridge.stop()
                DnsttBridge.stopClient()
            }
            TunnelType.VAYDNS -> {
                Log.d(TAG, "Stopping VayDNS proxy and bridge")
                DnsttSocksBridge.onDnsPoolDead = null
                DnsttSocksBridge.stop()
                VaydnsBridge.stopClient()
            }
            TunnelType.SSH -> {
                Log.d(TAG, "Stopping SSH tunnel")
                SshTunnelBridge.stop()
            }
            TunnelType.DNSTT_SSH -> {
                Log.d(TAG, "Stopping DNSTT+SSH: SSH first, then bridge, then DNSTT")
                SshTunnelBridge.stop()
                DnsttSocksBridge.stop()
                DnsttBridge.stopClient()
            }
            TunnelType.NOIZDNS_SSH -> {
                Log.d(TAG, "Stopping NoizDNS+SSH: SSH first, then bridge, then NoizDNS")
                SshTunnelBridge.stop()
                DnsttSocksBridge.stop()
                DnsttBridge.stopClient()
            }
            TunnelType.VAYDNS_SSH -> {
                Log.d(TAG, "Stopping VayDNS+SSH: SSH first, then VayDNS")
                SshTunnelBridge.stop()
                VaydnsBridge.stopClient()
            }
            TunnelType.DOH -> {
                Log.d(TAG, "Stopping DoH proxy")
                DohBridge.stop()
            }
            TunnelType.NAIVE_SSH -> {
                Log.d(TAG, "Stopping NaiveProxy+SSH: SSH first, then NaiveProxy")
                SshTunnelBridge.stop()
                NaiveBridge.stop()
            }
            TunnelType.NAIVE -> {
                Log.d(TAG, "Stopping standalone NaiveProxy: bridge first, then NaiveProxy")
                NaiveSocksBridge.stop()
                NaiveBridge.stop()
            }
            TunnelType.SNOWFLAKE -> {
                Log.d(TAG, "Stopping Snowflake: TorSocksBridge first, then Snowflake+Tor")
                TorSocksBridge.stop()
                SnowflakeBridge.stopClient()
            }
            TunnelType.SOCKS5 -> {
                Log.d(TAG, "Stopping SOCKS5 proxy bridge")
                Socks5ProxyBridge.stop()
            }
            TunnelType.XRAY -> XrayBridge.stop()
            TunnelType.VLESS -> {
                Log.d(TAG, "Stopping VLESS bridge")
                VlessBridge.stop()
            }
            TunnelType.SOAR -> {
                Log.d(TAG, "Stopping Soar: bridge first, then Soar")
                Socks5ProxyBridge.stop()
                SoarBridge.stopClient()
            }
            TunnelType.SOAR_SSH -> {
                Log.d(TAG, "Stopping Soar+SSH: SSH first, then Soar")
                SshTunnelBridge.stop()
                SoarBridge.stopClient()
            }
        }
    }

    /**
     * Clear VPN service reference from the current bridge.
     */
    private fun clearVpnServiceRef() {
        when (currentTunnelType) {
            TunnelType.SLIPSTREAM -> { SlipstreamBridge.proxyOnlyMode = false; SlipstreamBridge.setVpnService(null) }
            TunnelType.SLIPSTREAM_SSH -> { SlipstreamBridge.proxyOnlyMode = false; SlipstreamBridge.setVpnService(null) }
            TunnelType.DNSTT -> DnsttBridge.setVpnService(null)
            TunnelType.NOIZDNS -> DnsttBridge.setVpnService(null)
            TunnelType.VAYDNS -> { /* VayDNS: no VpnService reference to clear */ }
            TunnelType.SSH -> { /* SSH-only: no bridge reference to clear */ }
            TunnelType.DNSTT_SSH -> DnsttBridge.setVpnService(null)
            TunnelType.NOIZDNS_SSH -> DnsttBridge.setVpnService(null)
            TunnelType.VAYDNS_SSH -> { /* VayDNS+SSH: no VpnService reference to clear */ }
            TunnelType.DOH -> { /* DOH: no bridge reference to clear */ }
            TunnelType.NAIVE_SSH -> { /* NaiveProxy+SSH: no bridge reference to clear */ }
            TunnelType.NAIVE -> { /* NaiveProxy standalone: no bridge reference to clear */ }
            TunnelType.SNOWFLAKE -> { /* Snowflake: no bridge reference to clear */ }
            TunnelType.SOCKS5 -> { /* SOCKS5: no bridge reference to clear */ }
            TunnelType.XRAY -> { /* Child process is stopped by stopCurrentProxy. */ }
            TunnelType.VLESS -> { /* VLESS: no VpnService reference to clear */ }
            TunnelType.SOAR, TunnelType.SOAR_SSH -> { /* Soar: no VpnService reference to clear */ }
        }
    }

    /**
     * Check if the current proxy is healthy.
     * When SSH is enabled, also checks SSH tunnel health.
     */
    private fun isCurrentProxyHealthy(): Boolean {
        // If HTTP proxy is running, it must also be healthy
        if (HttpProxyServer.isRunning() && !HttpProxyServer.isHealthy()) {
            return false
        }

        return when (currentTunnelType) {
            TunnelType.SLIPSTREAM -> SlipstreamBridge.isClientHealthy() && SlipstreamSocksBridge.isClientHealthy()
            TunnelType.SLIPSTREAM_SSH -> SlipstreamBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
            TunnelType.DNSTT -> DnsttBridge.isClientHealthy() && DnsttSocksBridge.isClientHealthy()
            TunnelType.NOIZDNS -> DnsttBridge.isClientHealthy() && DnsttSocksBridge.isClientHealthy()
            TunnelType.VAYDNS -> VaydnsBridge.isClientHealthy() && DnsttSocksBridge.isClientHealthy()
            TunnelType.SSH -> SshTunnelBridge.isClientHealthy()
            TunnelType.DNSTT_SSH -> DnsttBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
            TunnelType.NOIZDNS_SSH -> DnsttBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
            TunnelType.VAYDNS_SSH -> VaydnsBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
            TunnelType.DOH -> DohBridge.isClientHealthy()
            TunnelType.NAIVE_SSH -> NaiveBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
            TunnelType.NAIVE -> NaiveBridge.isClientHealthy() && NaiveSocksBridge.isClientHealthy()
            TunnelType.SNOWFLAKE -> SnowflakeBridge.isClientHealthy() && TorSocksBridge.isClientHealthy()
            TunnelType.SOCKS5 -> Socks5ProxyBridge.isRunning() && Socks5ProxyBridge.isClientHealthy()
            TunnelType.XRAY -> XrayBridge.isRunning()
            TunnelType.VLESS -> VlessBridge.isRunning() && VlessBridge.isClientHealthy()
            TunnelType.SOAR -> SoarBridge.isClientHealthy() && Socks5ProxyBridge.isClientHealthy()
            TunnelType.SOAR_SSH -> SoarBridge.isClientHealthy() && SshTunnelBridge.isClientHealthy()
        }
    }

    /**
     * Resolve a resolver host to a numeric IP for [Builder.addDnsServer].
     * Falls back to [DEFAULT_DNS] if resolution fails or host is null.
     */
    private suspend fun resolveToIp(host: String?, customDnsServer: String? = null): String = withContext(Dispatchers.IO) {
        if (host.isNullOrBlank()) return@withContext DEFAULT_DNS
        val resolved = VpnRepositoryImpl.resolveHost(host, customDnsServer)
        if (DomainRouter.isIpAddress(resolved)) resolved else DEFAULT_DNS
    }

    private suspend fun establishVpnInterface(dnsServer: String): EstablishedTun? {
        val blockV6 = preferencesDataStore.blockIpv6.first()
        val requestedMtu = try {
            preferencesDataStore.vpnMtu.first()
        } catch (_: java.io.IOException) {
            VpnMtu.DEFAULT
        }
        val mtu = VpnMtu.effective(requestedMtu, blockV6)
        val dnsOnly = currentTunnelType == TunnelType.DOH
        // An IPv6 DNS address itself enables that address family in VpnService.Builder.
        // Keep the actual tunnel resolver untouched. Advertise an IPv4 server to client
        // apps in blocked mode so Android blocks all IPv6 from VPN-covered apps.
        val appDns = if (blockV6 && dnsServer.contains(':')) {
            val fallback = preferencesDataStore.getEffectiveRemoteDnsFallback().first()
            fallback.takeIf { !it.contains(':') && DomainRouter.isIpAddress(it) } ?: DEFAULT_DNS
        } else dnsServer
        var selfExcludedForProbe = false
        val builder = Builder()
            .setSession("Rovunet VPN")
            .addAddress(VPN_ADDRESS, 32)
            .addDnsServer(appDns)
            .setBlocking(false)

        if (dnsOnly) {
            builder.addRoute(appDns, if (appDns.contains(':')) 128 else 32)
            if (!blockV6) builder.allowFamily(android.system.OsConstants.AF_INET6)
        } else {
            builder.addRoute(VPN_ROUTE, 0)
            if (!blockV6) {
                builder.addAddress("fd00::1", 128)
                builder.addRoute("::", 0)
            }
        }

        val needsSelfExclusion = currentTunnelType == TunnelType.DNSTT ||
                currentTunnelType == TunnelType.NOIZDNS ||
                currentTunnelType == TunnelType.VAYDNS ||
                currentTunnelType == TunnelType.SSH ||
                currentTunnelType == TunnelType.DNSTT_SSH ||
                currentTunnelType == TunnelType.NOIZDNS_SSH ||
                currentTunnelType == TunnelType.VAYDNS_SSH ||
                currentTunnelType == TunnelType.DOH ||
                currentTunnelType == TunnelType.SLIPSTREAM ||
                currentTunnelType == TunnelType.SLIPSTREAM_SSH ||
                currentTunnelType == TunnelType.NAIVE_SSH ||
                currentTunnelType == TunnelType.NAIVE ||
                currentTunnelType == TunnelType.SNOWFLAKE ||
                currentTunnelType == TunnelType.SOCKS5 ||
                currentTunnelType == TunnelType.XRAY ||
                currentTunnelType == TunnelType.VLESS ||
                currentTunnelType == TunnelType.SOAR ||
                currentTunnelType == TunnelType.SOAR_SSH

        val splitEnabled = preferencesDataStore.splitTunnelingEnabled.first()
        val splitMode = preferencesDataStore.splitTunnelingMode.first()
        val splitApps = preferencesDataStore.splitTunnelingApps.first()

        if (splitEnabled && splitApps.isNotEmpty()) {
            when (splitMode) {
                SplitTunnelingMode.DISALLOW -> {
                    // Selected apps bypass VPN. Always include self if tunnel type needs it.
                    if (needsSelfExclusion) {
                        try {
                            builder.addDisallowedApplication(packageName)
                            selfExcludedForProbe = true
                        } catch (e: Exception) {
                            Log.e(TAG, "Failed to exclude self from VPN", e)
                        }
                    }
                    for (pkg in splitApps) {
                        if (pkg == packageName) continue // already handled above
                        try {
                            builder.addDisallowedApplication(pkg)
                        } catch (_: Exception) { }
                    }
                    Log.d(TAG, "Split tunneling: disallow mode, ${splitApps.size} apps bypass VPN")
                }
                SplitTunnelingMode.ALLOW -> {
                    // Only selected apps use VPN. Self is automatically excluded
                    // (not in the allowed list) — which is what tunnel types need.
                    var allowedApps = 0
                    for (pkg in splitApps) {
                        if (pkg == packageName) continue // don't route our own traffic through VPN
                        try {
                            builder.addAllowedApplication(pkg)
                            allowedApps++
                        } catch (_: Exception) { }
                    }
                    selfExcludedForProbe = allowedApps > 0
                    Log.d(TAG, "Split tunneling: allow mode, ${splitApps.size} apps use VPN")
                }
            }
        } else {
            // Original behavior: exclude self for tunnel types that need it
            if (needsSelfExclusion) {
                try {
                    builder.addDisallowedApplication(packageName)
                            selfExcludedForProbe = true
                    Log.d(TAG, "Excluded app from VPN: $packageName")
                } catch (e: Exception) {
                    Log.e(TAG, "Failed to exclude app from VPN", e)
                }
            }
        }

        // Append HTTP proxy to VPN so apps route HTTP/HTTPS through it directly,
        // bypassing TUN/tun2socks overhead. Requires API 29+.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val appendProxy = preferencesDataStore.appendHttpProxyToVpn.first()
            if (appendProxy && !preferencesDataStore.proxyAuthEnabled.first()) {
                // Android ProxyInfo cannot supply HTTP Basic credentials. Authenticated
                // LAN sharing remains available; local apps use the normal VPN route.
                val httpPort = preferencesDataStore.httpProxyPort.first()

                // Start HTTP proxy now (before VPN) so it's ready when apps connect.
                // If "HTTP proxy" (LAN sharing) is also enabled, bind to proxyListenAddress
                // (e.g. 0.0.0.0) so other devices can reach it. setHttpProxy still uses
                // 127.0.0.1 which is reachable on a 0.0.0.0-bound socket.
                // If only appendProxy is active (no LAN sharing), 127.0.0.1 is sufficient.
                if (!HttpProxyServer.isRunning()) {
                    val httpEnabled = preferencesDataStore.httpProxyEnabled.first()
                    val httpListenHost = if (httpEnabled) preferencesDataStore.proxyListenAddress.first() else "127.0.0.1"

                    val result = startHttpProxy(httpListenHost, httpPort)
                    if (result.isFailure) {
                        Log.w(TAG, "HTTP proxy failed to start for VPN append: ${result.exceptionOrNull()?.message}")
                    }
                }

                if (HttpProxyServer.isRunning()) {
                    builder.setHttpProxy(ProxyInfo.buildDirectProxy("127.0.0.1", httpPort))
                    Log.i(TAG, "HTTP proxy appended to VPN on 127.0.0.1:$httpPort")
                }
            }
        }

        val established = EstablishedTun.establish(builder, mtu)
        ProbeBypass.established(serviceSessionId, established != null && selfExcludedForProbe)
        return established
    }

    private fun observeConnectionState() {
        stateObserverJob?.cancel()
        stateObserverJob = runtimeScope.launch {
            vpnRepository.connectionState.collect { state ->
                val notification = notificationHelper.createVpnNotification(state, isProxyOnly)
                val notificationManager = getSystemService(NotificationManager::class.java)
                notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)

                when (state) {
                    is ConnectionState.Connected -> {
                        startTrafficNotificationPolling()
                    }
                    is ConnectionState.Error -> {
                        stopTrafficNotificationPolling()
                        // Don't stop service during kill switch — we're blocking traffic and reconnecting
                        if (isKillSwitchActive) return@collect
                        // Already handling reconnection — don't interfere
                        if (isAutoReconnecting || isReconnecting) return@collect

                        // If connection was previously successful, route through
                        // handleTunnelFailure so auto-reconnect / kill-switch can trigger.
                        if (connectionWasSuccessful && !isUserInitiatedDisconnect) {
                            handleTunnelFailure("connection error: ${state.message}")
                            return@collect
                        }

                        // Startup failure — show reconnect notification and stop
                        if (currentProfileId != -1L) {
                            val reconnectNotification = notificationHelper.createReconnectNotification(
                                message = state.message,
                                profileId = currentProfileId
                            )
                            notificationManager.notify(NotificationHelper.RECONNECT_NOTIFICATION_ID, reconnectNotification)
                        }
                        stopForeground(STOP_FOREGROUND_REMOVE)
                        stopOwnedService()
                    }
                    // Disconnected is handled by the disconnect() coroutine which already
                    // calls stopForeground/stopSelf. Do NOT call stopOwnedService() here — if the
                    // user reconnects quickly on the same service instance, this observer
                    // may still be processing the old Disconnected state and would kill
                    // the new connection.
                    else -> {
                        stopTrafficNotificationPolling()
                    }
                }
            }
        }
    }

    private fun startTrafficNotificationPolling() {
        trafficNotificationJob?.cancel()
        vpnRepository.resetSpeedTracking()
        trafficNotificationJob = runtimeScope.launch {
            val showTrafficInNotification = preferencesDataStore.showNotificationTraffic.first()
            var idleCount = 0
            while (isActive) {
                // Adaptive interval: 1s when active, 5s when idle, 10s when screen off.
                val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
                val screenOn = pm.isInteractive
                val interval = VpnPowerPolicy.sampleInterval(screenOn, idleCount)
                kotlinx.coroutines.withTimeoutOrNull(interval) { trafficPollSignal.receive() }

                // Some backends enter JNI or take a bridge lock when reading stats.
                // Never make the Android main looper wait for a native backend.
                withContext(Dispatchers.IO) { vpnRepository.refreshTrafficStats() }
                val current = vpnRepository.trafficStats.value
                if (current.bytesSent != lastPowerTx || current.bytesReceived != lastPowerRx) {
                    lastUserTrafficAtElapsed = android.os.SystemClock.elapsedRealtime()
                    lastPowerTx = current.bytesSent
                    lastPowerRx = current.bytesReceived
                }
                updateRuntimePowerPolicy(pm.isInteractive)
                val upSpeed = current.uploadSpeed
                val downSpeed = current.downloadSpeed

                // Track idle: no bytes transferred in this tick.
                if (upSpeed == 0L && downSpeed == 0L) {
                    idleCount++
                } else {
                    idleCount = 0
                }

                // After a reconnect, bridge byte counters are reset to 0.
                // Reset the rate baseline after recovery.
                if (resetTrafficBaseline) {
                    resetTrafficBaseline = false
                    connectionManager.setDnsWarning(null)
                    vpnRepository.resetSpeedTracking()
                }

                // Idle counters are not a failed connection. Protocol health
                // and TUN TX-without-RX monitoring run after establishment.

                // Skip notification updates while screen is off — nobody can see them,
                // and they waste IPC / cause MIUI reordering issues.
                // Health checks above still run regardless.
                if (screenOn) {
                    val state = vpnRepository.connectionState.first()
                    if (state is ConnectionState.Connected) {
                        // Only update notification when displayed values change.
                        // Redundant updates cause notification reordering on MIUI/HyperOS.
                        val newTotal = current.totalBytes
                        val newSpeed = upSpeed + downSpeed
                        if (showTrafficInNotification && (newTotal != lastNotifTotalBytes || (newSpeed > 0) != (lastNotifHadSpeed))) {
                            lastNotifTotalBytes = newTotal
                            lastNotifHadSpeed = newSpeed > 0
                            val notification = notificationHelper.createVpnNotification(
                                state = state,
                                isProxyOnly = isProxyOnly,
                                trafficStats = if (showTrafficInNotification) current else null,
                                uploadSpeed = if (showTrafficInNotification) upSpeed else 0,
                                downloadSpeed = if (showTrafficInNotification) downSpeed else 0
                            )
                            val notificationManager = getSystemService(NotificationManager::class.java)
                            notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)
                        }
                    }
                } else {
                    // Force refresh on next screen-on by invalidating cached state
                    lastNotifTotalBytes = -1L
                }
            }
        }
    }

    private fun stopTrafficNotificationPolling() {
        trafficNotificationJob?.cancel()
        trafficNotificationJob = null
        vpnRepository.resetSpeedTracking()
        lastNotifTotalBytes = -1L
        lastNotifHadSpeed = false
    }

    private fun disconnect(keepServiceAlive: Boolean = false, session: Long = requestedSessionId.takeIf { it > 0 } ?: serviceSessionId) {
        app.slipnet.tunnel.TransportTrust.cancelPending()
        // Mark as user-initiated so onDestroy() doesn't show disconnect notification
        isUserInitiatedDisconnect = true

        // Stop traffic stats polling
        stopTrafficNotificationPolling()

        // Clear kill switch so teardown proceeds normally
        isKillSwitchActive = false

        // Cancel auto-reconnect
        autoReconnectJob?.cancel()
        autoReconnectJob = null
        isAutoReconnecting = false
        autoReconnectAttempt = 0
        connectionWasSuccessful = false
        vpnRepository.setAutoReconnect(false)

        // Cancel boot retry
        bootRetryJob?.cancel()
        bootRetryJob = null
        isBootTriggered = false
        bootRetryAttempt = 0
        unregisterBootNetworkCallback()

        // Cancel any in-progress DNS pool scan so it stops immediately
        // instead of running each probe to its 8 s timeout.
        vpnRepository.cancelPoolScan()

        // Cancel any in-progress connection attempt
        app.slipnet.tunnel.TransportTrust.cancelPending()
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_CANCEL_REQUESTED, serviceSessionId)
        val abortStartup = startupCancellation.requestCancel()
        val previousRuntime = runtimeJob
        previousRuntime.cancel()
        val previousConnect = connectJob
        previousConnect?.cancel()
        connectJob = null

        // Cancel any in-progress reconnection immediately — before the coroutine.
        // This prevents a race where the reconnect coroutine is mid-flight in native
        // code (e.g., starting Slipstream on port 1081) while disconnect also tries
        // to stop/start, leading to "port already in use" on the next connect.
        reconnectDebounceJob?.cancel()
        reconnectDebounceJob = null
        networkLostJob?.cancel()
        networkLostJob = null
        isReconnecting = false
        resetTrafficBaseline = false

        // Cancel state observer to prevent stale stopOwnedService() calls
        stateObserverJob?.cancel()
        stateObserverJob = null

        // Reset accumulated traffic stats so next connection starts fresh
        SlipstreamSocksBridge.resetTrafficStats()
        DnsttSocksBridge.resetTrafficStats()
        SshTunnelBridge.resetTrafficStats()
        Socks5ProxyBridge.resetTrafficStats()
        NaiveSocksBridge.resetTrafficStats()

        disconnectJob = serviceScope.launch {
            StartupTeardown.await(abortStartup, previousConnect)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STARTUP_WORKERS_STOPPED, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_WAIT, serviceSessionId)
            previousRuntime.join()
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_RUNTIME_DONE, serviceSessionId)
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_LOCK_WAIT, serviceSessionId)
            TunnelOperations.mutex.withLock {
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_LOCK_ACQUIRED, serviceSessionId)
            if (!app.slipnet.domain.model.ServiceCommandPolicy.acceptsStop(session, requestedSessionId)) return@launch
            serviceSessionId = session
            TunnelOperations.owner = session
            Log.i(TAG, "Disconnecting VPN")
            // Clear saved state so we don't auto-reconnect on restart
            clearConnectionState()
            cleanupConnection()
            TunnelOperations.owner = 0
            if (keepServiceAlive) {
                reportDisconnected()
            } else {
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
            }
            }
        }
    }

    /**
     * Handle tunnel failure with seamless reconnect, kill switch, and auto-reconnect support.
     *
     * First tries a lightweight "seamless reconnect": restarts only the proxy while keeping
     * the TUN interface and tun2socks alive. This handles transient failures such as QUIC
     * idle timeouts on devices where the OS throttles background network (e.g. Chinese OEM
     * phones like Honor/Huawei), causing keepalive pings to fail and the transport connection
     * to drop. Seamless reconnect minimizes the traffic gap — tun2socks buffers/retries
     * connections until the new proxy is ready.
     *
     * If seamless reconnect is not possible (tun2socks crashed) or has been exhausted
     * (MAX_SEAMLESS_RECONNECTS attempts), escalates to kill switch or auto-reconnect.
     */
    private suspend fun handleTunnelFailure(reason: String) {
        val owner = serviceSessionId
        if (isUserInitiatedDisconnect || !connectionManager.acceptsStart(owner)) return
        // Try seamless proxy restart first if tun2socks is still alive.
        // Skip if: tun2socks is dead, already in kill-switch/auto-reconnect,
        // or we've exhausted seamless attempts (prevents infinite loop).
        val tunnelAlive = withContext(Dispatchers.IO) { if (isProxyOnly) true else HevSocks5Tunnel.isRunning() }
        val isDnstt = currentTunnelType in listOf(
            TunnelType.DNSTT, TunnelType.NOIZDNS,
            TunnelType.DNSTT_SSH, TunnelType.NOIZDNS_SSH,
            TunnelType.VAYDNS, TunnelType.VAYDNS_SSH,
            TunnelType.SOAR, TunnelType.SOAR_SSH
        )
        val maxSeamless = if (isDnstt) MAX_SEAMLESS_RECONNECTS_DNSTT else MAX_SEAMLESS_RECONNECTS
        if (tunnelAlive && seamlessReconnectAttempts < maxSeamless
            && !isKillSwitchActive && !isAutoReconnecting) {
            seamlessReconnectAttempts++
            // Wait before retrying so the network has time to recover.
            // Without this delay, back-to-back attempts on a flaky network
            // burn through the budget instantly and escalate to full disconnect.
            val delayIdx = (seamlessReconnectAttempts - 1).coerceAtMost(SEAMLESS_RECONNECT_DELAYS_MS.size - 1)
            val delayMs = SEAMLESS_RECONNECT_DELAYS_MS[delayIdx]
            Log.i(TAG, "Attempting seamless reconnect ($seamlessReconnectAttempts/$maxSeamless) in ${delayMs}ms: $reason")
            delay(delayMs)
            if (isUserInitiatedDisconnect || serviceSessionId != owner || !connectionManager.acceptsStart(owner)) return
            // Skip the DNS pool scan during seamless reconnect — the resolvers
            // are already validated and saved from the initial connect.
            vpnRepository.setAutoReconnect(true)
            handleNetworkChange("tunnel recovery: $reason")
            return
        }

        // Seamless reconnect exhausted or not possible — reset counter and escalate.
        Log.i(TAG, "Escalating tunnel failure (seamless attempts=$seamlessReconnectAttempts): $reason")
        seamlessReconnectAttempts = 0

        val killSwitchEnabled = preferencesDataStore.killSwitch.first()
        if (killSwitchEnabled && !isProxyOnly && vpnInterface != null) {
            enterKillSwitchMode(reason)
        } else {
            val autoReconnectEnabled = preferencesDataStore.autoReconnect.first()
            if (autoReconnectEnabled && connectionWasSuccessful && !isUserInitiatedDisconnect
                && autoReconnectAttempt < AUTO_RECONNECT_MAX_RETRIES) {
                enterAutoReconnectMode(reason)
            } else {
                reportConnectionError("VPN connection lost - $reason")
                cleanupConnection()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopOwnedService()
            }
        }
    }

    /**
     * Enter kill switch mode: keep VPN interface alive (blocking all traffic),
     * stop proxy/tunnel, show kill switch notification, and attempt reconnection.
     */
    private suspend fun enterKillSwitchMode(reason: String) {
        Log.i(TAG, "Kill switch activated: $reason")
        isKillSwitchActive = true

        // Stop health check during kill switch
        healthCheckJob?.cancel()
        healthCheckJob = null

        // Stop proxy/tunnel but NOT vpnInterface — TUN stays alive to block traffic
        withContext(Dispatchers.IO) {
            if (!isProxyOnly) {
                try { HevSocks5Tunnel.stop() } catch (_: Exception) {}
            }
            try { stopCurrentProxy() } catch (_: Exception) {}
        }

        // Update foreground notification to kill switch notification
        val profile = collectionProfile(connectionManager.getProfileById(currentProfileId))
        val profileName = profile?.name ?: "VPN"
        val notification = notificationHelper.createKillSwitchNotification(profileName)
        val notificationManager = getSystemService(NotificationManager::class.java)
        notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)

        // Attempt reconnection after a brief delay
        runtimeScope.launch {
            delay(2000)
            if (isKillSwitchActive) {
                handleNetworkChange("kill switch reconnect")
            }
        }
    }

    /**
     * Enter auto-reconnect mode: tear down VPN (traffic flows directly during retry),
     * show reconnecting notification, and attempt reconnection with exponential backoff.
     */
    private suspend fun enterAutoReconnectMode(reason: String) {
        autoReconnectAttempt++
        isAutoReconnecting = true
        Log.i(TAG, "Auto-reconnect attempt $autoReconnectAttempt/$AUTO_RECONNECT_MAX_RETRIES: $reason")

        // Capture profile info BEFORE cleanup resets currentProfileId
        val profileId = currentProfileId
        val profileName = currentProfileName

        // Tear down the connection (VPN goes down, traffic flows directly)
        cleanupConnection()

        // Show auto-reconnect foreground notification to keep service alive
        val notification = notificationHelper.createAutoReconnectNotification(
            profileName, autoReconnectAttempt, AUTO_RECONNECT_MAX_RETRIES
        )
        val notificationManager = getSystemService(NotificationManager::class.java)
        notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)

        // Schedule reconnect with exponential backoff
        val delayMs = AUTO_RECONNECT_DELAYS_MS[
            (autoReconnectAttempt - 1).coerceAtMost(AUTO_RECONNECT_DELAYS_MS.size - 1)
        ]
        autoReconnectJob = serviceScope.launch {
            Log.d(TAG, "Auto-reconnect: waiting ${delayMs}ms before attempt $autoReconnectAttempt")
            delay(delayMs)
            if (isUserInitiatedDisconnect) {
                Log.i(TAG, "Auto-reconnect cancelled: user-initiated disconnect")
                return@launch
            }
            isAutoReconnecting = false
            // Tell the repo this is an auto-reconnect so the DNS-pool feature
            // reuses the resolvers already on the profile instead of running
            // a fresh E2E scan on every network blip.
            vpnRepository.setAutoReconnect(true)
            connect(profileId)
        }
    }

    /**
     * Boot-triggered retry: wait for network with exponential backoff.
     * Also registers a one-shot network callback for instant retry when network arrives.
     *
     * @param needsCleanup true on first entry (failed connect), false on subsequent timer retries
     */
    private suspend fun enterBootRetryMode(profileId: Long, reason: String, needsCleanup: Boolean = true) {
        bootRetryAttempt++

        if (bootRetryAttempt > BOOT_RETRY_MAX_ATTEMPTS) {
            Log.w(TAG, "Boot retry exhausted ($BOOT_RETRY_MAX_ATTEMPTS attempts): $reason")
            isBootTriggered = false
            unregisterBootNetworkCallback()
            reportConnectionError("Auto-connect failed \u2014 no network after boot")
            if (needsCleanup) cleanupConnection()
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopOwnedService()
            return
        }

        val delayMs = (BOOT_RETRY_INITIAL_DELAY_MS shl (bootRetryAttempt - 1).coerceAtMost(14))
            .coerceAtMost(BOOT_RETRY_MAX_DELAY_MS)

        Log.i(TAG, "Boot retry attempt $bootRetryAttempt/$BOOT_RETRY_MAX_ATTEMPTS (delay ${delayMs}ms): $reason")

        val profileName = currentProfileName

        // Only clean up on first entry (after a failed connect attempt).
        // Subsequent timer-driven retries have nothing to clean up.
        if (needsCleanup) {
            cleanupConnection()
        }

        val notification = notificationHelper.createBootRetryNotification(
            profileName, bootRetryAttempt, BOOT_RETRY_MAX_ATTEMPTS
        )
        val notificationManager = getSystemService(NotificationManager::class.java)
        notificationManager.notify(NotificationHelper.VPN_NOTIFICATION_ID, notification)

        // Register a one-shot network callback to connect immediately when network arrives.
        // Supplements the timer — whichever fires first wins.
        if (bootNetworkCallback == null) {
            val request = NetworkRequest.Builder()
                .addCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                .addCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN)
                .build()

            val callback = object : ConnectivityManager.NetworkCallback() {
                override fun onAvailable(network: Network) {
                    Log.i(TAG, "Boot retry: network became available, triggering immediate connect")
                    bootRetryJob?.cancel()
                    unregisterBootNetworkCallback()
                    runtimeScope.launch {
                        if (!isUserInitiatedDisconnect) {
                            connect(profileId)
                        }
                    }
                }
            }
            try {
                connectivityManager?.registerNetworkCallback(request, callback)
                bootNetworkCallback = callback
                Log.d(TAG, "Boot retry network callback registered")
            } catch (e: Exception) {
                Log.e(TAG, "Failed to register boot retry network callback", e)
            }
        }

        bootRetryJob = runtimeScope.launch {
            delay(delayMs)

            if (isUserInitiatedDisconnect) {
                Log.i(TAG, "Boot retry cancelled: user-initiated disconnect")
                unregisterBootNetworkCallback()
                return@launch
            }

            // Check if network is now available before attempting connection
            val cm = connectivityManager
            val capabilities = cm?.activeNetwork?.let { cm.getNetworkCapabilities(it) }
            val hasInternet = capabilities?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true

            if (hasInternet) {
                Log.i(TAG, "Network available after boot retry delay, attempting connection")
                unregisterBootNetworkCallback()
                connect(profileId)
            } else {
                Log.i(TAG, "Network still unavailable, scheduling next boot retry")
                enterBootRetryMode(profileId, "network still unavailable", needsCleanup = false)
            }
        }
    }

    private fun unregisterBootNetworkCallback() {
        bootNetworkCallback?.let { callback ->
            try {
                connectivityManager?.unregisterNetworkCallback(callback)
                Log.d(TAG, "Boot retry network callback unregistered")
            } catch (_: Exception) {}
        }
        bootNetworkCallback = null
    }

    @Synchronized private fun updateRuntimePowerPolicy(screenOn: Boolean) {
        val idle = (android.os.SystemClock.elapsedRealtime() - lastUserTrafficAtElapsed).coerceAtLeast(0)
        val hold = VpnPowerPolicy.holdCpu(needsContinuousCpuWake, screenOn, idle)
        wakeLock?.let { lock ->
            if (hold && !lock.isHeld) lock.acquire(WAKELOCK_TIMEOUT_MS)
            else if (!hold && lock.isHeld) lock.release()
        }
        // Modern LOW_LATENCY cannot preserve screen-off Wi-Fi. Preserve the older
        // HIGH_PERF DNS path; don't hold a Wi-Fi lock on a cellular-only connection.
        val physical = defaultPhysicalNetwork()
        val caps = physical?.let { connectivityManager?.getNetworkCapabilities(it) }
        val wifi = caps?.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) == true
        val keepWifi = VpnPowerPolicy.holdWifi(wifi, needsContinuousCpuWake, screenOn, Build.VERSION.SDK_INT)
        if (keepWifi && wifiLock == null) {
            val manager = applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            @Suppress("DEPRECATION")
            val mode = when {
                isChineseOem() -> WifiManager.WIFI_MODE_FULL
                Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q -> WifiManager.WIFI_MODE_FULL_LOW_LATENCY
                else -> WifiManager.WIFI_MODE_FULL_HIGH_PERF
            }
            wifiLock = manager?.createWifiLock(mode, "Rovunet:VpnWifiLock")?.apply { setReferenceCounted(false) }
        }
        wifiLock?.let { lock ->
            if (keepWifi && !lock.isHeld) lock.acquire()
            else if (!keepWifi && lock.isHeld) lock.release()
        }
        app.slipnet.util.ConnectionDiagnostics.power(serviceSessionId, screenOn, wakeLock?.isHeld == true,
            wifiLock?.isHeld == true, needsContinuousCpuWake)
    }

    /** Release WakeLock and WifiLock if held. */
    @Synchronized private fun releaseLocks() {
        app.slipnet.util.ConnectionDiagnostics.power(serviceSessionId,
            (getSystemService(Context.POWER_SERVICE) as PowerManager).isInteractive, false, false, needsContinuousCpuWake)
        underlyingDefaultDeclared = false
        wakeLockRenewJob?.cancel()
        wakeLockRenewJob = null
        wakeLock?.let {
            if (it.isHeld) {
                it.release()
                Log.d(TAG, "WakeLock released")
            }
        }
        wakeLock = null
        wifiLock?.let {
            if (it.isHeld) {
                it.release()
                Log.d(TAG, "WifiLock released")
            }
        }
        wifiLock = null
    }

    /** Periodically re-acquires the WakeLock before it expires. */
    private fun startWakeLockRenewal() {
        wakeLockRenewJob?.cancel()
        wakeLockRenewJob = runtimeScope.launch(Dispatchers.IO) {
            while (isActive) {
                kotlinx.coroutines.delay(WAKELOCK_RENEW_INTERVAL_MS)
                synchronized(this@SlipNetVpnService) {
                    wakeLock?.let {
                        if (it.isHeld) {
                            it.acquire(WAKELOCK_TIMEOUT_MS)
                            Log.d(TAG, "WakeLock renewed")
                        }
                    }
                }
            }
        }
    }

    /** Chinese OEM ROMs aggressively kill apps using high-power WifiLock modes. */
    private fun isChineseOem(): Boolean {
        val manufacturer = Build.MANUFACTURER.lowercase()
        return manufacturer in listOf(
            "xiaomi", "redmi", "poco", "huawei", "honor",
            "oppo", "vivo", "realme", "oneplus", "meizu", "zte", "lenovo"
        )
    }

    /**
     * Clean up all resources - must be called before stopping service.
     * This is a suspend function to run blocking operations on IO dispatcher.
     */
    private suspend fun cleanupConnection() {
        app.slipnet.util.AppLog.redactSensitive = false
        Log.d(TAG, "Cleaning up connection resources")

        releaseLocks()

        // Stop health monitoring
        healthCheckJob?.cancel()
        healthCheckJob = null

        // Cancel any pending reconnect / network-loss timer
        reconnectDebounceJob?.cancel()
        reconnectDebounceJob = null
        networkLostJob?.cancel()
        networkLostJob = null
        isReconnecting = false

        // Unregister network callback and Doze receiver
        unregisterNetworkCallback()
        unregisterBootNetworkCallback()
        unregisterDozeReceiver()
        lastNetworkAddresses = emptySet()
        lastNetworkDnsServers = emptySet()

        // Stop native tunnels on IO thread to avoid ANR.
        // Cooperative budget only: an uninterruptible JNI stop can outlast it.
        // Stage events below distinguish runtime, lock, native and proxy waits.
        try {
            withTimeout(8000) {
                withContext(Dispatchers.IO) {
                    if (!isProxyOnly) {
                        try {
                            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_HEV_BEGIN, serviceSessionId)
                            HevSocks5Tunnel.stop()
                            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_HEV_END, serviceSessionId)
                        } catch (e: Exception) {
                            Log.e(TAG, "Error stopping HevSocks5Tunnel", e)
                        }
                    }

                    try {
                        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_PROXY_BEGIN, serviceSessionId)
                        stopCurrentProxy()
                        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_PROXY_END, serviceSessionId)
                    } catch (e: Exception) {
                        Log.e(TAG, "Error stopping proxy", e)
                    }
                }
            }
        } catch (_: kotlinx.coroutines.TimeoutCancellationException) {
            Log.w(TAG, "Cleanup timed out after 8s — bridge may be stuck, proceeding with disconnect")
        }

        // Clear VPN service reference AFTER native code has stopped (or timed out).
        // Must come after stopCurrentProxy() to avoid crashing native protectSocket() calls.
        clearVpnServiceRef()

        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.STOP_TUN_BEGIN, serviceSessionId)
        // Close VPN interface
        try {
            vpnInterface?.close()
        } catch (e: Exception) {
            Log.e(TAG, "Error closing VPN interface", e)
        }
        vpnInterface = null
        ProbeBypass.clear(serviceSessionId)
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.TUN_CLOSED, serviceSessionId)

        currentProfileId = -1
    }

    override fun onRevoke() {
        super.onRevoke()
        Log.i(TAG, "VPN permission revoked (another VPN took over)")
        // Use the same joined teardown as a user stop; retain the revocation notification.
        disconnect()
        isUserInitiatedDisconnect = false

    }

    override fun onDestroy() {
        Log.i(TAG, "Service onDestroy")

        // Capture state before cleanup resets currentProfileId to -1
        val wasActive = currentProfileId != -1L
        val profileName = currentProfileName
        val profileId = currentProfileId

        val session = serviceSessionId
        retireSession()

        // Ensure connection state is always reset when the service dies.
        // This is critical for onRevoke() (another VPN app takes over):
        // disconnect() runs cleanup in a coroutine on serviceScope, but onDestroy()
        // cancels serviceScope before the coroutine reaches onVpnDisconnected().
        // Without this, the UI would still show "Connected" after another VPN connects.
        if (connectionManager.ownsSession(session)) clearConnectionState()

        // Show disconnect notification if the connection was active and not user-initiated.
        // Skip if kill switch is active (it has its own notification) or if reconnecting
        // (the reconnect/kill-switch flow handles notifications).
        if (wasActive && !isUserInitiatedDisconnect && !isKillSwitchActive && !isReconnecting && !isAutoReconnecting) {
            Log.i(TAG, "Unexpected disconnect detected, showing notification")
            val notification = notificationHelper.createDisconnectedNotification(profileName, profileId)
            val notificationManager = getSystemService(NotificationManager::class.java)
            notificationManager.notify(NotificationHelper.DISCONNECT_NOTIFICATION_ID, notification)
        }

        serviceScope.cancel()
        super.onDestroy()
    }

    /**
     * Synchronous cleanup that doesn't wait for native threads.
     * Used in onDestroy where we can't suspend.
     */
    private fun cleanupConnectionSync() {
        app.slipnet.util.AppLog.redactSensitive = false
        Log.d(TAG, "Quick cleanup (sync)")

        releaseLocks()

        // Stop health monitoring
        healthCheckJob?.cancel()
        healthCheckJob = null

        // Cancel auto-reconnect
        autoReconnectJob?.cancel()
        autoReconnectJob = null
        isAutoReconnecting = false
        vpnRepository.setAutoReconnect(false)

        // Cancel any in-progress DNS pool scan
        vpnRepository.cancelPoolScan()

        // Cancel any pending reconnect / network-loss timer
        reconnectDebounceJob?.cancel()
        reconnectDebounceJob = null
        networkLostJob?.cancel()
        networkLostJob = null
        isReconnecting = false

        // Unregister network callback and Doze receiver
        unregisterNetworkCallback()
        unregisterDozeReceiver()
        lastNetworkAddresses = emptySet()
        lastNetworkDnsServers = emptySet()

        // Stop HTTP proxy
        try {
            HttpProxyServer.stop()
        } catch (e: Exception) {
            Log.e(TAG, "Error stopping HTTP proxy", e)
        }

        // Request native tunnels to stop (non-blocking)
        // The native code will handle the actual shutdown
        if (!isProxyOnly) {
            try {
                HevSocks5Tunnel.stop()
            } catch (e: Exception) {
                Log.e(TAG, "Error stopping HevSocks5Tunnel", e)
            }
        }

        try {
            // Just send stop signal, don't wait
            stopCurrentProxy()
        } catch (e: Exception) {
            Log.e(TAG, "Error stopping proxy", e)
        }

        // Clear VPN service reference
        clearVpnServiceRef()

        // Close VPN interface
        try {
            vpnInterface?.close()
        } catch (e: Exception) {
            Log.e(TAG, "Error closing VPN interface", e)
        }
        vpnInterface = null
        ProbeBypass.clear(serviceSessionId)
        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.TUN_CLOSED, serviceSessionId)

        currentProfileId = -1
    }
}
