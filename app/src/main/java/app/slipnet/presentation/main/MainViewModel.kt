package app.slipnet.presentation.main

import app.slipnet.domain.model.SAVED_CREDENTIALS_UNAVAILABLE

import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.CollectionInference
import app.slipnet.domain.model.ProfileCollections
import app.slipnet.domain.model.ConnectionChoice
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.cancelAndJoin
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.local.ConnectionHistoryStore
import app.slipnet.domain.model.ConnectionHistory
import app.slipnet.domain.model.ChainValidation
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.DnsResolver
import app.slipnet.domain.model.PingResult
import app.slipnet.domain.model.ProfileChain
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TrafficStats
import app.slipnet.domain.model.TunnelType
import app.slipnet.domain.model.isAvailable
import app.slipnet.domain.model.E2eTestResult
import app.slipnet.domain.repository.ChainRepository
import app.slipnet.domain.repository.ProfileRepository
import app.slipnet.domain.repository.ResolverScannerRepository
import app.slipnet.domain.usecase.ConnectVpnUseCase
import app.slipnet.domain.usecase.DeleteProfileUseCase
import app.slipnet.domain.usecase.DisconnectVpnUseCase
import app.slipnet.domain.usecase.GetActiveProfileUseCase
import app.slipnet.domain.usecase.GetProfilesUseCase
import app.slipnet.domain.usecase.SaveProfileUseCase
import app.slipnet.domain.usecase.SetActiveProfileUseCase
import app.slipnet.service.VpnConnectionManager
import app.slipnet.tunnel.SnowflakeBridge
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.async
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.net.InetSocketAddress
import java.net.Socket
import javax.inject.Inject

data class ImportPreview(
    val profiles: List<ServerProfile>,
    val warnings: List<String>,
    val groups: List<CollectionInference.Group> = CollectionInference.plan(profiles).map { it.copy(profiles = ProfileDuplicates.withinImport(it.profiles)) },
    val collectionName: String = groups.singleOrNull()?.name ?: ""
)

data class QrCodeData(
    val profileName: String,
    val configUri: String
)

data class MainUiState(
    // Connection state
    val connectionState: ConnectionState = ConnectionState.Disconnected,
    val activeProfile: ServerProfile? = null,
    val activeChain: ProfileChain? = null,
    val proxyOnlyMode: Boolean = false,
    val debugLogging: Boolean = false,
    val snowflakeBootstrapProgress: Int = -1,
    // Profile list state
    val profiles: List<ServerProfile> = emptyList(),
    val chains: List<ProfileChain> = emptyList(),
    val connectedProfileId: Long? = null,
    val connectingProfileId: Long? = null,
    val connectedChainId: Long? = null,
    val isLoading: Boolean = true,
    val error: String? = null,
    val exportedJson: String? = null,
    val importPreview: ImportPreview? = null,
    val isImporting: Boolean = false,
    val subscriptionUrl: String? = null,
    val subscriptionId: String? = null,
    val subscriptionPreview: app.slipnet.data.subscription.SubscriptionPreview? = null,
    val subscriptionIds: Set<String> = emptySet(),
    val smartStatus: app.slipnet.service.SmartConnectionController.Status = app.slipnet.service.SmartConnectionController.Status(),
    val smartVersion: Long = 0,
    val subscriptionVersion: Long = 0,
    val updatingSubscriptions: Set<String> = emptySet(),
    val subscriptionNotice: String? = null,
    val accountStatuses: Map<String, app.slipnet.data.subscription.AccountStatusEntity> = emptyMap(),
    val dataPathState: app.slipnet.domain.model.DataPathState = app.slipnet.domain.model.DataPathState.UNTESTED,
    val dataPathNoticeUntilElapsedMs: Long = 0,
    val regroupPreview: List<CollectionInference.Group>? = null,
    val regroupUndoAvailable: Boolean = false,
    val regroupBusy: Boolean = false,
    val duplicatePreview: List<ProfileDuplicates.Match> = emptyList(),
    /** Input awaiting a password to decrypt (user pasted a slipnet-bundle-enc:// URI). */
    val pendingEncryptedImport: String? = null,
    val qrCodeData: QrCodeData? = null,
    val showFirstLaunchAbout: Boolean = false,
    val trafficStats: TrafficStats = TrafficStats.EMPTY,
    val uploadSpeed: Long = 0,
    val downloadSpeed: Long = 0,
    // Session totals shown after disconnect
    val sessionTotalUpload: Long = 0,
    val sessionTotalDownload: Long = 0,
    // Ping results per profile ID
    val collectionPrompt: Boolean = false,
    val collectionConnect: Boolean = false,
    val collectionProgress: String = "",
    val selectedCollection: String? = null,
    val manualProfileId: Long? = null,
    val sortByHttps: Boolean = false,
    val sortByReliability: Boolean = false,
    val pingResults: Map<Long, PingResult> = emptyMap(),
    val isPingRunning: Boolean = false,
    /**
     * Resolvers used by the *current* on-screen ping results when the Global
     * DNS override was active. Set when a ping starts; cleared by
     * [MainViewModel.clearPingResults]. Empty otherwise — the indicator only
     * appears alongside actual ping activity, not whenever Global DNS is on.
     */
    val pingingViaGlobalDns: List<DnsResolver> = emptyList(),
    val sleepTimerRemainingSeconds: Int = 0,
    // Update checker
    val availableUpdate: app.slipnet.util.AppUpdate? = null,
    // DNS warning
    val dnsWarning: String? = null,
    // DNS pool scan progress (per-profile pool feature)
    val dnsPoolScan: app.slipnet.tunnel.DnsPoolScanState = app.slipnet.tunnel.DnsPoolScanState()
)

@HiltViewModel
class MainViewModel @Inject constructor(
    @dagger.hilt.android.qualifiers.ApplicationContext private val appContext: android.content.Context,
    private val connectionManager: VpnConnectionManager,
    private val getProfilesUseCase: GetProfilesUseCase,
    private val getActiveProfileUseCase: GetActiveProfileUseCase,
    private val connectVpnUseCase: ConnectVpnUseCase,
    private val disconnectVpnUseCase: DisconnectVpnUseCase,
    private val setActiveProfileUseCase: SetActiveProfileUseCase,
    private val deleteProfileUseCase: DeleteProfileUseCase,
    private val saveProfileUseCase: SaveProfileUseCase,
    internal val profileRepository: ProfileRepository,
    private val chainRepository: ChainRepository,
    private val resolverScannerRepository: ResolverScannerRepository,
    private val configExporter: ConfigExporter,
    private val configImporter: ConfigImporter,
    internal val subscriptionRepository: app.slipnet.data.subscription.SubscriptionRepository,
    private val preferencesDataStore: PreferencesDataStore,
    internal val connectionHistoryStore: ConnectionHistoryStore,
    internal val smartConnections: app.slipnet.service.SmartConnectionController,
    internal val activePathMonitor: app.slipnet.service.ActivePathMonitor
) : ViewModel() {

    private val _uiState = MutableStateFlow(MainUiState())
    val uiState: StateFlow<MainUiState> = _uiState.asStateFlow()

    private var dataPathJob: Job? = null
    private var dataPathExpiryJob: Job? = null
    private var dataPathGeneration = 0L
    private var dataPathOwner: Triple<Long, Long, Long>? = null
    fun verifyDataPath() {
        val connected = _uiState.value.connectionState as? ConnectionState.Connected ?: return
        val owner = Triple(connectionManager.currentSessionId, connected.connectedAt, connected.profile.id)
        val generation = ++dataPathGeneration
        dataPathJob?.cancel()
        dataPathExpiryJob?.cancel()
        _uiState.value = _uiState.value.copy(dataPathState = app.slipnet.domain.model.DataPathState.CHECKING,
            dataPathNoticeUntilElapsedMs = 0)
        dataPathJob = viewModelScope.launch {
            val verified = try {
                if (connected.chainId > 0) false else activePathMonitor.measure(connected.profile, owner.first)?.ms != null
            } catch (e: kotlinx.coroutines.CancellationException) { throw e } catch (_: Exception) { false }
            if (dataPathOwner == owner && generation == dataPathGeneration &&
                connectionManager.ownsSession(owner.first) && _uiState.value.connectionState is ConnectionState.Connected) {
                _uiState.value = _uiState.value.copy(
                    dataPathState = if (verified) app.slipnet.domain.model.DataPathState.VERIFIED else app.slipnet.domain.model.DataPathState.UNKNOWN,
                    dataPathNoticeUntilElapsedMs = if (verified) android.os.SystemClock.elapsedRealtime() + 3_000L else 0)
                if (verified) dataPathExpiryJob = viewModelScope.launch {
                    delay(3_000L)
                    if (dataPathOwner == owner && generation == dataPathGeneration) {
                        _uiState.value = _uiState.value.copy(dataPathNoticeUntilElapsedMs = 0)
                    }
                }
            }
        }
    }
    private var bootstrapPollingJob: Job? = null
    private var trafficPollingJob: Job? = null
    private var sleepTimerJob: Job? = null
    private var pingJob: Job? = null
    private val testBatchOffsets = mutableMapOf<String, Int>()
    private var importJob: Job? = null
    // Cached Room flows may emit synchronously during construction. State used
    // by observers must exist before the init block launches them.
    private val discoveredProfiles = mutableMapOf<Long, ServerProfile>()
    private var manualChoices: Map<String, Long> = emptyMap()

    private fun rememberHistory() {
        val ui = _uiState.value
        if (ui.isLoading) return
        connectionHistoryStore.remember(ConnectionHistory.Input(ui.profiles, ui.selectedCollection, manualChoices,
            ui.pingResults, ui.sortByHttps, ui.sortByReliability))
    }

    suspend fun refreshAccountStatuses() {
        try { subscriptionRepository.refreshAccountStatuses() }
        catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
        catch (_: Exception) { /* Status availability never blocks the main screen or connection. */ }
    }

    private val networkManager = appContext.getSystemService(android.content.Context.CONNECTIVITY_SERVICE) as android.net.ConnectivityManager
    private val physicalNetworkTracker = app.slipnet.domain.model.PhysicalNetworkTracker(physicalNetworkSnapshot())
    private var networkChangeJob: Job? = null
    private var networkCallbackRegistered = false
    private val networkCallback = object : android.net.ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: android.net.Network) { networkChanged(network, true) }
        override fun onLost(network: android.net.Network) { networkChanged(network, false) }
        override fun onCapabilitiesChanged(network: android.net.Network, capabilities: android.net.NetworkCapabilities) { networkChanged(network, true) }
        override fun onLinkPropertiesChanged(network: android.net.Network, properties: android.net.LinkProperties) { networkChanged(network, true) }
    }
    init {
        viewModelScope.launch { smartConnections.status.collect { _uiState.value = _uiState.value.copy(smartStatus = it) } }
        viewModelScope.launch { smartConnections.results.collect { results ->
            if (results.isNotEmpty()) _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + results)
        } }
        viewModelScope.launch { subscriptionRepository.observe().catch { _uiState.value = _uiState.value.copy(error = "DATABASE_UNAVAILABLE", isLoading = false) }.collect { rows ->
            _uiState.value = _uiState.value.copy(subscriptionIds = rows.map { it.id }.toSet(), subscriptionVersion = rows.maxOfOrNull { it.lastSuccess } ?: 0)
        } }

        viewModelScope.launch {
            subscriptionRepository.observeAccountStatus().catch { emit(emptyList()) }.collect { rows ->
                _uiState.value = _uiState.value.copy(accountStatuses = rows.associateBy { it.subscriptionId })
            }
        }
        viewModelScope.launch { activePathMonitor.latest.collect { sample ->
            if (sample != null && !_uiState.value.isPingRunning && connectionManager.ownsSession(sample.session) &&
                (_uiState.value.connectionState as? ConnectionState.Connected)?.profile?.id == sample.profile) {
                _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (sample.profile to sample.result),
                    dataPathState = if (sample.ms != null) app.slipnet.domain.model.DataPathState.VERIFIED else app.slipnet.domain.model.DataPathState.UNKNOWN)
            }
        } }
        observeConnectionState()
        observeDnsWarning()
        observeDnsPoolScan()
        observeProfiles()
        observeChains()
        observeProxyOnlyMode()
        observeDebugLogging()
        checkFirstLaunch()
        checkForUpdate()
        observeUnderlyingNetworks()
    }

    private fun observeUnderlyingNetworks() {
        // NOT_VPN excludes our temporary tunnels, so probing does not invalidate itself.
        val request = android.net.NetworkRequest.Builder()
            .addCapability(android.net.NetworkCapabilities.NET_CAPABILITY_INTERNET)
            .addCapability(android.net.NetworkCapabilities.NET_CAPABILITY_NOT_VPN).build()
        runCatching { networkManager.registerNetworkCallback(request, networkCallback); networkCallbackRegistered = true }
    }
    private fun physicalNetworkSnapshot(): String? = app.slipnet.service.PhysicalNetworkRoute.snapshot(networkManager)
    fun setScreenVisible(visible: Boolean) = activePathMonitor.setVisible(this, visible)
    private fun networkChanged(network: android.net.Network, available: Boolean) {
        // Ignore the event's network: only the selected physical route matters.
        networkChangeJob?.cancel()
        networkChangeJob = viewModelScope.launch {
            kotlinx.coroutines.delay(500)
            if (physicalNetworkSnapshot() == null) kotlinx.coroutines.delay(1000)
            applyPhysicalNetworkSnapshot(physicalNetworkSnapshot())
        }
    }
    internal suspend fun applyPhysicalNetworkSnapshot(snapshot: String?) {
        if (!physicalNetworkTracker.update(snapshot)) return
        val testing = _uiState.value.isPingRunning
        if (testing) app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.COLLECTION_NETWORK_CHANGED)
        smartConnections.clearResults()
        pingJob?.cancelAndJoin()
        discoveredProfiles.clear()
        _uiState.value = _uiState.value.copy(collectionPrompt = false,
            error = if (testing) "PROBE_NETWORK_CHANGED" else _uiState.value.error,
            pingResults = app.slipnet.domain.model.ProbeCache.historical(_uiState.value.pingResults))
        rememberHistory()
    }
    override fun onCleared() {
        activePathMonitor.setVisible(this, false)
        activePathMonitor.pause(this, false)
        rememberHistory()
        if (networkCallbackRegistered) runCatching { networkManager.unregisterNetworkCallback(networkCallback) }
        super.onCleared()
    }

    fun automaticMode(profile: ServerProfile) = smartConnections.mode(ProfileCollections.key(profile))
    fun setAutomaticMode(profile: ServerProfile, mode: app.slipnet.domain.model.SmartPathPolicy.Mode) {
        smartConnections.setMode(ProfileCollections.key(profile), mode)
        _uiState.value = _uiState.value.copy(smartVersion = _uiState.value.smartVersion + 1)
    }
    fun connectCollection(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        selectCollection(profile)
        val members = _uiState.value.profiles.filter { ProfileCollections.key(it) == ProfileCollections.key(profile) }
        if (automaticMode(profile) == app.slipnet.domain.model.SmartPathPolicy.Mode.MANUAL)
            preferredProfile(members)?.let { connect(it) }
        else smartConnections.start(ProfileCollections.key(profile), replaceCurrent = true)
    }
    fun addFreeTor() { viewModelScope.launch {
        val name = app.slipnet.presentation.theme.RovunetStrings.localizedContext(appContext).getString(app.slipnet.R.string.alpha17_free_tor)
        profileRepository.insertCollection(listOf(ServerProfile(name = name, domain = "tor", tunnelType = TunnelType.SNOWFLAKE,
            collectionId = java.util.UUID.randomUUID().toString(), collectionName = "Tor / Snowflake")))
    } }

    // ── Connection ──────────────────────────────────────────────────────

    private fun observeConnectionState() {
        viewModelScope.launch {
            connectionManager.connectionState.collect { state ->
                val connectedId = when (state) {
                    is ConnectionState.Connected -> state.profile.id
                    else -> null
                }
                val chainId = when (state) {
                    is ConnectionState.Connected -> if (state.chainId > 0) state.chainId else null
                    else -> null
                }
                _uiState.value = _uiState.value.copy(
                    connectionState = state,
                    connectingProfileId = if (state is ConnectionState.Connecting || state is ConnectionState.Disconnecting) _uiState.value.connectingProfileId else null,
                    connectedProfileId = if (chainId != null) null else connectedId,
                    connectedChainId = chainId,
                    pingResults = if (chainId == null && connectedId != null && _uiState.value.pingResults[connectedId] == PingResult.ReviewIdentity)
                        _uiState.value.pingResults - connectedId else _uiState.value.pingResults,
                    error = if (state is ConnectionState.Error && !_uiState.value.isPingRunning) state.message else _uiState.value.error
                )
                val nextOwner = (state as? ConnectionState.Connected)?.let {
                    Triple(connectionManager.currentSessionId, it.connectedAt, it.profile.id)
                }
                if (nextOwner != dataPathOwner) {
                    dataPathJob?.cancel(); dataPathExpiryJob?.cancel(); dataPathGeneration++; dataPathOwner = nextOwner
                    _uiState.value = _uiState.value.copy(dataPathState = app.slipnet.domain.model.DataPathState.UNTESTED,
                        dataPathNoticeUntilElapsedMs = 0)
                    if (nextOwner != null && !_uiState.value.isPingRunning) verifyDataPath()
                }
                if (state is ConnectionState.Connecting) {
                    startBootstrapPolling()
                } else {
                    stopBootstrapPolling()
                }
                if (state is ConnectionState.Connected) {
                    startTrafficPolling()
                    startSleepTimer()
                } else {
                    stopTrafficPolling()
                    cancelSleepTimer()
                }
            }
        }
    }

    private fun observeDnsWarning() {
        viewModelScope.launch {
            connectionManager.dnsWarning.collect { warning ->
                _uiState.value = _uiState.value.copy(dnsWarning = warning)
            }
        }
    }

    private fun observeDnsPoolScan() {
        viewModelScope.launch {
            connectionManager.dnsPoolScanState.collect { state ->
                _uiState.value = _uiState.value.copy(dnsPoolScan = state)
            }
        }
    }

    private fun startBootstrapPolling() {
        bootstrapPollingJob?.cancel()
        bootstrapPollingJob = viewModelScope.launch {
            while (true) {
                val progress = SnowflakeBridge.torBootstrapProgress
                _uiState.value = _uiState.value.copy(
                    snowflakeBootstrapProgress = if (progress > 0) progress else -1
                )
                delay(500)
            }
        }
    }

    private fun stopBootstrapPolling() {
        bootstrapPollingJob?.cancel()
        bootstrapPollingJob = null
        _uiState.value = _uiState.value.copy(snowflakeBootstrapProgress = -1)
    }

    private fun startTrafficPolling() {
        trafficPollingJob?.cancel()
        // Clear previous session totals when a new connection starts
        _uiState.value = _uiState.value.copy(sessionTotalUpload = 0, sessionTotalDownload = 0)
        trafficPollingJob = viewModelScope.launch {
            // Observe the stats that the VPN service's notification poller already
            // refreshes — no need to call refreshTrafficStats() a second time.
            connectionManager.trafficStats.collect { current ->
                _uiState.value = _uiState.value.copy(
                    trafficStats = current,
                    uploadSpeed = current.uploadSpeed,
                    downloadSpeed = current.downloadSpeed
                )
            }
        }
    }

    private fun stopTrafficPolling() {
        trafficPollingJob?.cancel()
        trafficPollingJob = null
        // Save session totals before clearing, but only if there are actual stats.
        // State transitions Connected→Disconnecting→Disconnected call this twice;
        // the second call must not overwrite saved totals with zeros.
        val lastStats = _uiState.value.trafficStats
        val hasStats = lastStats.bytesSent > 0 || lastStats.bytesReceived > 0
        _uiState.value = _uiState.value.copy(
            trafficStats = TrafficStats.EMPTY,
            uploadSpeed = 0,
            downloadSpeed = 0,
            sessionTotalUpload = if (hasStats) lastStats.bytesSent else _uiState.value.sessionTotalUpload,
            sessionTotalDownload = if (hasStats) lastStats.bytesReceived else _uiState.value.sessionTotalDownload
        )
    }

    private fun startSleepTimer() {
        sleepTimerJob?.cancel()
        sleepTimerJob = viewModelScope.launch {
            val minutes = preferencesDataStore.sleepTimerMinutes.first()
            if (minutes <= 0) return@launch
            var remaining = minutes * 60
            _uiState.value = _uiState.value.copy(sleepTimerRemainingSeconds = remaining)
            while (remaining > 0) {
                delay(1000)
                remaining--
                _uiState.value = _uiState.value.copy(sleepTimerRemainingSeconds = remaining)
            }
            disconnect()
        }
    }

    private fun cancelSleepTimer() {
        sleepTimerJob?.cancel()
        sleepTimerJob = null
        _uiState.value = _uiState.value.copy(sleepTimerRemainingSeconds = 0)
    }

    fun userCancelSleepTimer() {
        cancelSleepTimer()
    }

    private fun observeProxyOnlyMode() {
        viewModelScope.launch {
            preferencesDataStore.proxyOnlyMode.collect { enabled ->
                _uiState.value = _uiState.value.copy(proxyOnlyMode = enabled)
            }
        }
    }

    private fun observeDebugLogging() {
        viewModelScope.launch {
            preferencesDataStore.debugLogging.collect { enabled ->
                _uiState.value = _uiState.value.copy(debugLogging = enabled)
            }
        }
    }


    private fun checkFirstLaunch() {
        viewModelScope.launch {
            val done = preferencesDataStore.firstLaunchDone.first()
            if (!done) {
                _uiState.value = _uiState.value.copy(showFirstLaunchAbout = true)
            }
        }
    }

    fun dismissFirstLaunchAbout() {
        _uiState.value = _uiState.value.copy(showFirstLaunchAbout = false)
        viewModelScope.launch {
            preferencesDataStore.setFirstLaunchDone()
        }
    }

    // ── Update Checker ──────────────────────────────────────────────────

    private fun checkForUpdate() {
        if (app.slipnet.BuildConfig.PRIVATE_CANDIDATE) return
        viewModelScope.launch {
            // Optional network work must not contend with the first local profile load.
            uiState.first { !it.isLoading }
            // Throttle: skip if checked recently
            val lastCheck = preferencesDataStore.lastUpdateCheckTime.first()
            val now = System.currentTimeMillis()
            if (now - lastCheck < app.slipnet.util.UpdateChecker.CHECK_INTERVAL_MS) return@launch
            preferencesDataStore.setLastUpdateCheckTime(now)

            val skipped = preferencesDataStore.skippedUpdateVersion.first()
            val current = app.slipnet.BuildConfig.VERSION_NAME

            val update = try { app.slipnet.util.UpdateChecker.check(appContext, current, preferencesDataStore.previewUpdates.first()) } catch (e: kotlinx.coroutines.CancellationException) { throw e } catch (_: Exception) { null } ?: return@launch

            // Don't show if user already skipped this version
            if (update.versionName == skipped) return@launch

            _uiState.value = _uiState.value.copy(availableUpdate = update)
        }
    }

    fun dismissUpdate() {
        _uiState.value = _uiState.value.copy(availableUpdate = null)
    }

    fun skipUpdate() {
        val version = _uiState.value.availableUpdate?.versionName ?: return
        _uiState.value = _uiState.value.copy(availableUpdate = null)
        viewModelScope.launch {
            preferencesDataStore.setSkippedUpdateVersion(version)
        }
    }

    private fun observeProfiles() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true)
            // History IO and Room/decryption may run concurrently; selection is still
            // restored before any connect action becomes available.
            val savedHistory = async(Dispatchers.IO) { connectionHistoryStore.load() }
            var restored = false
            combine(
                getProfilesUseCase(),
                getActiveProfileUseCase()
            ) { profiles, activeProfile ->
                Pair(profiles, activeProfile)
            }.catch { _uiState.value = _uiState.value.copy(error = "DATABASE_UNAVAILABLE", isLoading = false) }.collect { (profiles, activeProfile) ->
                val saved = savedHistory.await()
                val unchanged = app.slipnet.domain.model.ProbeCache.unchanged(_uiState.value.profiles, profiles)
                val results = if (!restored) withContext(Dispatchers.Default) {
                    ConnectionHistory.restoreResults(saved, profiles)
                } else _uiState.value.pingResults.filterKeys { it in unchanged }
                if (!restored) manualChoices = ConnectionHistory.choices(saved, profiles).ifEmpty {
                    // Upgrade existing installations without losing the last selected/connected path.
                    activeProfile?.let { mapOf(ProfileCollections.key(it) to it.id) } ?: emptyMap()
                }
                manualChoices = manualChoices.values.mapNotNull { id -> profiles.firstOrNull { it.id == id } }
                    .associate { ProfileCollections.key(it) to it.id }
                val selected = if (!restored) profiles.firstOrNull { it.id == saved.selectedAnchorId }?.let(ProfileCollections::key)
                    ?: activeProfile?.let(ProfileCollections::key) else _uiState.value.selectedCollection
                val selectedKey = selected?.takeIf { key -> profiles.any { ProfileCollections.key(it) == key } }
                    ?: activeProfile?.let(ProfileCollections::key) ?: profiles.firstOrNull()?.let(ProfileCollections::key)
                val sorted = if (!restored) saved.sorted else _uiState.value.sortByHttps
                val reliability = if (!restored) saved.reliability else _uiState.value.sortByReliability
                discoveredProfiles.keys.retainAll(unchanged)
                _uiState.value = _uiState.value.copy(
                    profiles = if (sorted) app.slipnet.domain.model.ProfileRanking.sort(profiles, results, reliability) else profiles,
                    activeProfile = activeProfile,
                    pingResults = results,
                    selectedCollection = selectedKey,
                    manualProfileId = ConnectionChoice.rememberedPath(selectedKey, manualChoices, profiles),
                    sortByHttps = sorted, sortByReliability = reliability,
                    isLoading = false
                )
                restored = true
            }
        }
    }

    /** The main control acts on the live session; browsing another card never turns Stop into Connect. */
    fun toggleGlobalConnection(onConnect: () -> Unit) {
        if (_uiState.value.isPingRunning) { cancelPing(); return }
        when (connectionManager.connectionState.value) {
            is ConnectionState.Connected, is ConnectionState.Connecting -> disconnect()
            is ConnectionState.Disconnecting -> Unit
            else -> onConnect()
        }
    }

    fun connect(profile: ServerProfile? = null) {
        if (_uiState.value.isLoading) return
        if (_uiState.value.isPingRunning) { _uiState.value = _uiState.value.copy(error = "Stop the connection test first"); return }
        if (profile == null && _uiState.value.manualProfileId == null) {
            selectedMembers().firstOrNull()?.let { selected ->
                if (automaticMode(selected) != app.slipnet.domain.model.SmartPathPolicy.Mode.MANUAL) {
                    smartConnections.start(ProfileCollections.key(selected), replaceCurrent = connectionManager.connectionState.value is ConnectionState.Connected)
                    return
                }
            }
        }
        val targetProfile = profile ?: when (val choice = app.slipnet.domain.model.ConnectionChoice.resolve(
            _uiState.value.manualProfileId, selectedMembers())) {
            app.slipnet.domain.model.ConnectionChoice.Target.Automatic -> {
                val selected = selectedMembers()
                if (onlyUnavailable(selected)) return
                val members = selected.filter { !it.needsCredentials && !it.isExpired && it.tunnelType.isAvailable() }
                val candidate = preferredProfile(members)
                if (candidate == null) {
                    if (members.any { ProfileCollections.isDns(it) }) {
                        _uiState.value = _uiState.value.copy(collectionPrompt = true, collectionConnect = true)
                    } else _uiState.value = _uiState.value.copy(error = app.slipnet.presentation.theme.RovunetStrings
                        .localizedContext(appContext).getString(app.slipnet.R.string.connection_no_recent_path))
                    return
                }
                candidate
            }
            app.slipnet.domain.model.ConnectionChoice.Target.Missing -> {
                _uiState.value = _uiState.value.copy(error = "No profile available to connect")
                return
            }
            is app.slipnet.domain.model.ConnectionChoice.Target.Direct -> choice.profile
        }
        if (rejectUnavailableCredentials(targetProfile)) return
        if (targetProfile.isExpired) {
            _uiState.value = _uiState.value.copy(error = "This profile has expired")
            return
        }
        if (!targetProfile.tunnelType.isAvailable()) {
            _uiState.value = _uiState.value.copy(
                error = "${targetProfile.tunnelType.displayName} is not available in this edition"
            )
            return
        }
        connectSelectedPath(targetProfile)
    }

    private fun connectSelectedPath(profile: ServerProfile, profileResolvers: Boolean = false) {
        if (rejectUnavailableCredentials(profile)) return
        val current = connectionManager.connectionState.value
        if (current is ConnectionState.Connected) {
            if (current.profile.id != profile.id || current.chainId >= 0) {
                rememberPath(profile)
                _uiState.value = _uiState.value.copy(connectingProfileId = profile.id)
                connectionManager.reconnect(profile)
            }
        } else if (current.isDisconnected) {
            rememberPath(profile)
            _uiState.value = _uiState.value.copy(connectingProfileId = profile.id)
            connectionManager.connect(profile, profileResolvers)
        }
    }

    private fun rememberPath(profile: ServerProfile) {
        val key = ProfileCollections.key(profile)
        manualChoices = manualChoices + (key to profile.id)
        _uiState.value = _uiState.value.copy(selectedCollection = key, manualProfileId = profile.id)
        rememberHistory()
    }

    fun disconnect() {
        if (_uiState.value.isPingRunning) { cancelPing(); return }
        connectionManager.disconnect()
    }

    fun toggleConnection() {
        when (_uiState.value.connectionState) {
            is ConnectionState.Connected,
            is ConnectionState.Connecting -> disconnect()
            is ConnectionState.Error -> connect()
            is ConnectionState.Disconnecting -> { /* ignore — already disconnecting */ }
            else -> connect()
        }
    }

    private fun observeChains() {
        viewModelScope.launch {
            combine(
                chainRepository.getAllChains(),
                chainRepository.getActiveChain()
            ) { chains, activeChain ->
                Pair(chains, activeChain)
            }.catch { _uiState.value = _uiState.value.copy(error = "DATABASE_UNAVAILABLE", isLoading = false) }.collect { (chains, activeChain) ->
                _uiState.value = _uiState.value.copy(
                    chains = chains,
                    activeChain = activeChain
                )
            }
        }
    }

    fun setActiveChain(chain: ProfileChain) {
        viewModelScope.launch {
            chainRepository.setActiveChain(chain.id)
        }
    }

    fun connectChain(chain: ProfileChain) {
        if (_uiState.value.isPingRunning) return
        viewModelScope.launch {
            val profiles = chain.profileIds.mapNotNull { profileRepository.getProfileById(it) }
            if (profiles.size != chain.profileIds.size) {
                _uiState.value = _uiState.value.copy(error = "Some profiles in chain were deleted")
                return@launch
            }
            val validationError = ChainValidation.validate(profiles)
            if (validationError != null) {
                _uiState.value = _uiState.value.copy(error = validationError)
                return@launch
            }
            connectionManager.connectChain(chain, profiles.first())
        }
    }

    fun moveChain(fromIndex: Int, toIndex: Int) {
        val currentList = _uiState.value.chains.toMutableList()
        if (fromIndex < 0 || fromIndex >= currentList.size ||
            toIndex < 0 || toIndex >= currentList.size) return

        val item = currentList.removeAt(fromIndex)
        currentList.add(toIndex, item)
        _uiState.value = _uiState.value.copy(chains = currentList)

        viewModelScope.launch {
            chainRepository.updateChainOrder(currentList.map { it.id })
        }
    }

    fun deleteChain(chain: ProfileChain) {
        viewModelScope.launch {
            chainRepository.deleteChain(chain.id)
        }
    }

    fun preferredProfile(collection: List<ServerProfile>): ServerProfile? {
        val first = collection.firstOrNull() ?: return null
        val id = ConnectionChoice.rememberedPath(ProfileCollections.key(first), manualChoices, collection)
        return collection.firstOrNull { it.id == id } ?: ConnectionChoice.initialPath(
            collection.filter { it.tunnelType.isAvailable() }, _uiState.value.pingResults) ?:
            collection.firstOrNull { !it.isExpired && !it.needsCredentials && it.tunnelType.isAvailable() }
    }

    fun selectCollection(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        val key = ProfileCollections.key(profile)
        val members = _uiState.value.profiles.filter { ProfileCollections.key(it) == key }
        if (members.isEmpty()) return
        val remembered = ConnectionChoice.rememberedPath(key, manualChoices, members)
        _uiState.value = _uiState.value.copy(selectedCollection = key, manualProfileId = remembered)
        rememberHistory()
        viewModelScope.launch { setActiveProfileUseCase(remembered ?: members.first().id) }
    }

    fun selectProfileOnly(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        rememberPath(profile)
        viewModelScope.launch { setActiveProfileUseCase(profile.id) }
    }

    fun tapCollection(profile: ServerProfile): ConnectionChoice.CardTap {
        val ui = _uiState.value
        val key = ProfileCollections.key(profile)
        val state = connectionManager.connectionState.value
        val activeTarget = state is ConnectionState.Connected && state.chainId < 0 &&
            ProfileCollections.key(state.profile) == key && (ui.manualProfileId == null || ui.manualProfileId == state.profile.id)
        val action = ConnectionChoice.cardTap(ui.selectedCollection == key, ui.isPingRunning || ui.collectionPrompt, state, activeTarget)
        if (action == ConnectionChoice.CardTap.SELECT) {
            val remembered = ConnectionChoice.rememberedPath(key, manualChoices, ui.profiles)
            _uiState.value = ui.copy(selectedCollection = key, manualProfileId = remembered)
            rememberHistory()
            viewModelScope.launch { setActiveProfileUseCase(remembered ?: profile.id) }
        }
        return action
    }

    fun moveCollection(source: ServerProfile, target: ServerProfile) {
        if (_uiState.value.isPingRunning || !connectionManager.connectionState.value.isDisconnected) return
        viewModelScope.launch {
            val sourceKey = ProfileCollections.key(source)
            val targetKey = ProfileCollections.key(target)
            val id = target.collectionId.ifBlank { java.util.UUID.randomUUID().toString() }
            profileRepository.updateCollectionMemberships(_uiState.value.profiles.filter { ProfileCollections.key(it) == sourceKey || ProfileCollections.key(it) == targetKey }.map {
                it.copy(collectionId = id, collectionName = ProfileCollections.name(target))
            })
            _uiState.value = _uiState.value.copy(selectedCollection = id)
        }
    }

    fun renameCollection(profile: ServerProfile, name: String) {
        if (_uiState.value.isPingRunning || name.isBlank()) return
        viewModelScope.launch {
            val key = ProfileCollections.key(profile)
            profileRepository.updateCollectionMemberships(_uiState.value.profiles.filter { ProfileCollections.key(it) == key }.map {
                it.copy(collectionName = name.trim().take(120))
            })
        }
    }

    fun tapProfile(profile: ServerProfile): ConnectionChoice.CardTap {
        val ui = _uiState.value
        val state = connectionManager.connectionState.value
        val action = ConnectionChoice.cardTap(ui.manualProfileId == profile.id, ui.isPingRunning || ui.collectionPrompt,
            state, state is ConnectionState.Connected && state.chainId < 0 && state.profile.id == profile.id)
        if (action == ConnectionChoice.CardTap.SELECT) {
            rememberPath(profile)
            viewModelScope.launch { setActiveProfileUseCase(profile.id) }
        }
        return action
    }

    fun clearError() {
        _uiState.value = _uiState.value.copy(error = null)
    }

    // ── Profile Management ──────────────────────────────────────────────

    fun moveProfile(fromIndex: Int, toIndex: Int) {
        val currentList = _uiState.value.profiles.toMutableList()
        if (fromIndex < 0 || fromIndex >= currentList.size ||
            toIndex < 0 || toIndex >= currentList.size) return

        val item = currentList.removeAt(fromIndex)
        currentList.add(toIndex, item)
        _uiState.value = _uiState.value.copy(profiles = currentList)

        viewModelScope.launch {
            profileRepository.updateProfileOrder(currentList.map { it.id })
        }
    }

    fun togglePinProfile(profile: ServerProfile) {
        viewModelScope.launch {
            profileRepository.togglePinned(profile.id)
        }
    }

    fun deleteProfile(profile: ServerProfile) {
        if (_uiState.value.isPingRunning) return
        viewModelScope.launch {
            val result = deleteProfileUseCase(profile.id)
            if (result.isFailure) {
                _uiState.value = _uiState.value.copy(
                    error = result.exceptionOrNull()?.message ?: "Failed to delete profile"
                )
            }
        }
    }

    fun previewDuplicateProfiles() {
        if (_uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected) return
        val protected = _uiState.value.chains.flatMap { it.profileIds }.toSet()
        _uiState.value = _uiState.value.copy(duplicatePreview = ProfileDuplicates.plan(_uiState.value.profiles, protected))
    }
    fun deleteDuplicateProfiles() {
        if (_uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected) return
        val preview = _uiState.value.duplicatePreview
        if (preview.isEmpty()) return
        viewModelScope.launch {
            try {
                val protected = _uiState.value.chains.flatMap { it.profileIds }.toSet()
                // Re-check all reviewed records before deleting any. A changed record requires a new preview.
                for (m in preview) {
                    check(m.remove.id !in protected)
                    check(profileRepository.getProfileById(m.keep.id) == m.keep)
                    check(profileRepository.getProfileById(m.remove.id) == m.remove)
                    check(ProfileDuplicates.key(m.keep) == ProfileDuplicates.key(m.remove))
                }
                check(profileRepository.deleteReviewedProfiles(preview.flatMap { listOf(it.keep,it.remove) }.distinctBy { it.id }, preview.map { it.remove.id }))
                _uiState.value = _uiState.value.copy(duplicatePreview = emptyList())
            } catch (_: Exception) {
                _uiState.value = _uiState.value.copy(error = "Profiles changed or deletion failed. Review duplicates again.", duplicatePreview = emptyList())
            }
        }
    }

    fun deleteCollection(reviewed: List<ServerProfile>) {
        if (_uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected || reviewed.isEmpty()) return
        viewModelScope.launch {
            val ids = reviewed.map { it.id }.toSet()
            if (_uiState.value.chains.any { chain -> chain.profileIds.any { it in ids } }) {
                _uiState.value = _uiState.value.copy(error = app.slipnet.presentation.theme.RovunetStrings.localizedContext(appContext).getString(app.slipnet.R.string.collection_chain_protected))
                return@launch
            }
            try {
                check(profileRepository.deleteReviewedProfiles(reviewed, ids.toList()))
                if (_uiState.value.selectedCollection == ProfileCollections.key(reviewed.first())) {
                    _uiState.value = _uiState.value.copy(selectedCollection = null, manualProfileId = null)
                }
            } catch (_: Exception) {
                _uiState.value = _uiState.value.copy(error = app.slipnet.presentation.theme.RovunetStrings.localizedContext(appContext).getString(app.slipnet.R.string.collection_delete_failed))
            }
        }
    }

    fun deleteAllProfiles() {
        if (_uiState.value.isPingRunning) return
        viewModelScope.launch {
            if (_uiState.value.chains.isNotEmpty()) {
                _uiState.value = _uiState.value.copy(error = "Remove chains before deleting all profiles")
                return@launch
            }
            // Wait for acknowledged cleanup before deleting referenced data.
            if (_uiState.value.connectionState is ConnectionState.Connected ||
                _uiState.value.connectionState is ConnectionState.Connecting) {
                val id = connectionManager.currentSessionId
                connectionManager.disconnect()
                if (kotlinx.coroutines.withTimeoutOrNull(20_000) { connectionManager.awaitSessionDisconnected(id); true } != true) {
                    _uiState.value = _uiState.value.copy(error = "Connection cleanup is still running; no profiles were deleted")
                    return@launch
                }
            }
            for (profile in _uiState.value.profiles) {
                val result = deleteProfileUseCase(profile.id)
                if (result.isFailure) {
                    _uiState.value = _uiState.value.copy(error = result.exceptionOrNull()?.message ?: "Deletion failed")
                    return@launch
                }
            }
        }
    }

    // ── Import / Export ─────────────────────────────────────────────────

    private fun rejectUnavailableCredentials(profile: ServerProfile): Boolean {
        if (!profile.needsCredentials) return false
        _uiState.value = _uiState.value.copy(error = SAVED_CREDENTIALS_UNAVAILABLE)
        return true
    }

    private fun onlyUnavailable(profiles: List<ServerProfile>): Boolean =
        profiles.isNotEmpty() && profiles.all { it.needsCredentials } && rejectUnavailableCredentials(profiles.first())

    fun exportProfile(profile: ServerProfile, hideResolvers: Boolean = false) {
        if (rejectUnavailableCredentials(profile)) return
        if (profile.isLocked) {
            _uiState.value = _uiState.value.copy(error = "Cannot export a locked profile")
            return
        }
        val json = configExporter.exportSingleProfile(profile, hideResolvers)
        _uiState.value = _uiState.value.copy(exportedJson = json)
    }

    fun exportProfileLocked(
        profile: ServerProfile,
        password: String,
        expirationDate: Long = 0,
        allowSharing: Boolean = false,
        boundDeviceId: String = "",
        hideResolvers: Boolean = false
    ) {
        if (rejectUnavailableCredentials(profile)) return
        viewModelScope.launch {
        val json = withContext(Dispatchers.Default) { configExporter.exportSingleProfileLocked(
            profile, password, expirationDate, allowSharing, boundDeviceId, hideResolvers
        ) }
        _uiState.value = _uiState.value.copy(exportedJson = json)

        }
    }

    fun exportCollection(profile: ServerProfile) {
        viewModelScope.launch {
        try {
            val data = withContext(Dispatchers.Default) { configExporter.exportCollection(_uiState.value.profiles, profile) }
            _uiState.value = _uiState.value.copy(exportedJson = data)
        } catch (e: Exception) {
            val reason = e.message?.takeIf { it in setOf("No exportable profiles", "Collection contains profiles that do not allow sharing", SAVED_CREDENTIALS_UNAVAILABLE) } ?: "Collection export failed"
            _uiState.value = _uiState.value.copy(error = reason)
        }

        }
    }

    fun exportAllProfiles() {
        viewModelScope.launch {
        val profiles = _uiState.value.profiles.filter { !it.isLocked }
        if (profiles.any(::rejectUnavailableCredentials)) return@launch
        if (profiles.isEmpty()) {
            _uiState.value = _uiState.value.copy(error = "No exportable profiles")
            return@launch
        }
        val json = withContext(Dispatchers.Default) { configExporter.exportAllProfiles(profiles) }
        _uiState.value = _uiState.value.copy(exportedJson = json)

        }
    }

    fun exportAllProfilesEncrypted(
        bundlePassword: String,
        profilePassword: String = "",
        expirationDate: Long = 0,
        allowSharing: Boolean = false,
        boundDeviceId: String = "",
        hideResolvers: Boolean = false
    ) {
        viewModelScope.launch {
        val profiles = _uiState.value.profiles.filter { !it.isLocked }
        if (profiles.any(::rejectUnavailableCredentials)) return@launch
        if (profiles.isEmpty()) {
            _uiState.value = _uiState.value.copy(error = "No exportable profiles")
            return@launch
        }
        try {
            val encrypted = withContext(Dispatchers.Default) { configExporter.exportAllProfilesEncrypted(
                profiles,
                bundlePassword,
                profilePassword,
                expirationDate,
                allowSharing,
                boundDeviceId,
                hideResolvers
            ) }
            _uiState.value = _uiState.value.copy(exportedJson = encrypted)
        } catch (e: Exception) {
            _uiState.value = _uiState.value.copy(error = "Failed to encrypt: ${e.message}")
        }

        }
    }

    fun clearExportedJson() {
        _uiState.value = _uiState.value.copy(exportedJson = null)
    }

    fun closeSubscription() { _uiState.value = _uiState.value.copy(subscriptionUrl = null, subscriptionId = null, subscriptionPreview = null) }
    fun requestSubscription(id: String) {
        if (id in _uiState.value.updatingSubscriptions) return
        _uiState.value = _uiState.value.copy(updatingSubscriptions = _uiState.value.updatingSubscriptions + id)
        viewModelScope.launch {
            val resources = app.slipnet.presentation.theme.RovunetStrings.localizedContext(appContext)
            try {
                val row = subscriptionRepository.observe().first().firstOrNull { it.id == id } ?: error("SOURCE_NOT_FOUND")
                val preview = subscriptionRepository.prepare(row.name, subscriptionRepository.sourceUrl(row), id, row.autoUpdate)
                if (preview.unsupported > 0) {
                    _uiState.value = _uiState.value.copy(subscriptionId = id, subscriptionPreview = preview)
                    return@launch
                }
                subscriptionRepository.apply(preview)
                _uiState.value = _uiState.value.copy(subscriptionNotice = resources.getString(
                    app.slipnet.R.string.subscription_updated_inline, preview.profiles.size, preview.unsupported, preview.removed) +
                    if (preview.importWarnings.isNotEmpty()) " · " + resources.getString(app.slipnet.R.string.subscription_partial_notice) else "")
                subscriptionRepository.refreshAccountStatuses(force = true)
            } catch (e: kotlinx.coroutines.CancellationException) { throw e
            } catch (e: Exception) {
                val key = subscriptionErrorResource(e.message)
                _uiState.value = _uiState.value.copy(subscriptionNotice = resources.getString(key))
            } finally {
                _uiState.value = _uiState.value.copy(updatingSubscriptions = _uiState.value.updatingSubscriptions - id)
            }
        }
    }
    fun clearSubscriptionNotice() { _uiState.value = _uiState.value.copy(subscriptionNotice = null) }
    fun showCollectionQr(profile: ServerProfile) {
        viewModelScope.launch {
            try {
                val payload = withContext(Dispatchers.Default) {
                    val text = configExporter.exportCollection(_uiState.value.profiles, profile)
                    app.slipnet.data.export.CollectionLink.encode(text)
                }
                _uiState.value = _uiState.value.copy(qrCodeData = QrCodeData(ProfileCollections.name(profile), payload))
            } catch (_: Exception) { _uiState.value = _uiState.value.copy(error = "Collection export failed") }
        }
    }
    fun parseImportConfig(json: String, bundlePassword: String? = null) {
        if (json.trim().startsWith("https://") && !json.trim().contains('\n')) {
            _uiState.value = _uiState.value.copy(subscriptionUrl = json.trim()); return
        }
        if (_uiState.value.isImporting) return
        _uiState.value = _uiState.value.copy(isImporting = true)
        importJob = viewModelScope.launch {
            try {
        val result = withContext(Dispatchers.Default) { configImporter.parseAndImport(
            json, connectionManager.getDeviceId(), bundlePassword
        ) }
        kotlinx.coroutines.currentCoroutineContext().ensureActive()
        when (result) {
            is ImportResult.Success -> {
                _uiState.value = _uiState.value.copy(
                    importPreview = ImportPreview(result.profiles, result.warnings),
                    pendingEncryptedImport = null
                )
            }
            is ImportResult.Error -> {
                _uiState.value = _uiState.value.copy(error = result.message)
            }
            ImportResult.NeedsPassword -> {
                _uiState.value = _uiState.value.copy(pendingEncryptedImport = json)
            }
        }

            } finally { _uiState.value = _uiState.value.copy(isImporting = false) }
        }
    }

    fun cancelEncryptedImport() {
        _uiState.value = _uiState.value.copy(pendingEncryptedImport = null)
    }

    fun setImportCollectionName(name: String) {
        _uiState.value.importPreview?.let { _uiState.value = _uiState.value.copy(importPreview = it.copy(collectionName = name.take(120))) }
    }

    fun confirmImport() {
        if (_uiState.value.isImporting) return
        val preview = _uiState.value.importPreview ?: return
        _uiState.value = _uiState.value.copy(isImporting = true)
        viewModelScope.launch {
            try {
                // Importing the same list again must not add it a second time.
                val existing = profileRepository.getAllProfiles().first().toMutableList()
                var skipped = 0
                val importedProfiles = preview.groups.flatMap { group ->
                    val id = java.util.UUID.randomUUID().toString()
                    val name = if (preview.groups.size == 1) preview.collectionName else group.name
                    val fresh = ProfileDuplicates.withoutExisting(existing, group.profiles)
                    skipped += group.profiles.size - fresh.size
                    existing += fresh
                    fresh.map { it.copy(collectionId = id, collectionName = name.trim().take(120)) }
                }
                if (importedProfiles.isEmpty()) {
                    _uiState.value = _uiState.value.copy(importPreview = null,
                        error = "Every profile was already in the app; nothing was added")
                    return@launch
                }
                profileRepository.insertCollection(importedProfiles).firstOrNull()?.let { if (_uiState.value.connectionState.isDisconnected) setActiveProfileUseCase(it) }
                _uiState.value = _uiState.value.copy(selectedCollection = null, manualProfileId = null)
                _uiState.value = _uiState.value.copy(
                    importPreview = null,
                    error = if (skipped > 0) "Profiles already in the app were skipped" else null
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    error = "Failed to import profiles: ${e.message}",
                    importPreview = null
                )
            } finally { _uiState.value = _uiState.value.copy(isImporting = false) }
        }
    }

    fun cancelImport() {
        importJob?.cancel()
        _uiState.value = _uiState.value.copy(importPreview = null)
    }

    fun showQrCode(profile: ServerProfile, hideResolvers: Boolean = false) {
        if (rejectUnavailableCredentials(profile)) return
        if (profile.isLocked) {
            _uiState.value = _uiState.value.copy(error = "Cannot export a locked profile")
            return
        }
        val configUri = configExporter.exportSingleProfile(profile, hideResolvers)
        _uiState.value = _uiState.value.copy(
            qrCodeData = QrCodeData(profile.name, configUri)
        )
    }

    fun clearQrCode() {
        _uiState.value = _uiState.value.copy(qrCodeData = null)
    }

    fun showQrCodeLocked(
        profile: ServerProfile,
        password: String,
        expirationDate: Long = 0,
        allowSharing: Boolean = false,
        boundDeviceId: String = "",
        hideResolvers: Boolean = false
    ) {
        if (rejectUnavailableCredentials(profile)) return
        viewModelScope.launch {
        val configUri = withContext(Dispatchers.Default) { configExporter.exportSingleProfileLocked(
            profile, password, expirationDate, allowSharing, boundDeviceId, hideResolvers
        ) }
        _uiState.value = _uiState.value.copy(
            qrCodeData = QrCodeData(profile.name, configUri)
        )

        }
    }

    fun reExportLockedProfile(profile: ServerProfile) {
        if (rejectUnavailableCredentials(profile)) return
        viewModelScope.launch {
        try {
            val json = withContext(Dispatchers.Default) { configExporter.reExportLockedProfile(profile) }
            _uiState.value = _uiState.value.copy(exportedJson = json)
        } catch (e: Exception) {
            _uiState.value = _uiState.value.copy(error = e.message ?: "Re-export failed")
        }

        }
    }

    fun showQrCodeLockedReExport(profile: ServerProfile) {
        if (rejectUnavailableCredentials(profile)) return
        viewModelScope.launch {
        try {
            val configUri = withContext(Dispatchers.Default) { configExporter.reExportLockedProfile(profile) }
            _uiState.value = _uiState.value.copy(
                qrCodeData = QrCodeData(profile.name, configUri)
            )
        } catch (e: Exception) {
            _uiState.value = _uiState.value.copy(error = e.message ?: "Re-export failed")
        }

        }
    }

    fun getDeviceId(): String = connectionManager.getDeviceId()

    // ── Real Ping (full E2E tunnel handshake) ───────────────────────────

    companion object {
        /** Tunnel types that use DNS tunneling and support E2E testing */
        private val E2E_TUNNEL_TYPES = setOf(
            TunnelType.DNSTT, TunnelType.DNSTT_SSH,
            TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH,
            TunnelType.VAYDNS, TunnelType.VAYDNS_SSH,
            TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH
        )
        /** Tunnel types ping can't test: no server reachable over plain TCP, and no E2E probe
         *  (Soar's server is only reachable through recursive resolvers). */
        private val PING_UNSUPPORTED_TYPES = setOf(
            TunnelType.SNOWFLAKE, TunnelType.SOAR, TunnelType.SOAR_SSH
        )
        /** Per-resolver E2E test timeout. With HTTP verification removed and
         *  the warmup SOCKS5 CONNECT dropped, the only significant cost is
         *  the Noise/QUIC handshake (~2–5s typical). 8s leaves ~3s headroom
         *  for cold/loaded resolvers without dragging on dead ones. */
        private const val E2E_TIMEOUT_MS = 8000L
        /** Total budget across all resolvers for a single profile. A profile
         *  with many dead resolvers won't block the whole ping job — after
         *  this budget, remaining resolvers are skipped and the last error
         *  is surfaced. */
        private const val E2E_TOTAL_BUDGET_MS = 15000L
    }

    private fun selectedMembers(): List<ServerProfile> {
        val state = _uiState.value
        val key = state.selectedCollection ?: state.activeProfile?.let { ProfileCollections.key(it) }
            ?: state.profiles.firstOrNull()?.let { ProfileCollections.key(it) }
        return state.profiles.filter { ProfileCollections.key(it) == key }
    }

    fun pingAllProfiles() { startQuickTests(selectedMembers(), batched = true) }

    fun pingCollection(profile: ServerProfile, detailed: Boolean = false) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        val key = ProfileCollections.key(profile)
        val manual = ConnectionChoice.rememberedPath(key, manualChoices, _uiState.value.profiles)
        _uiState.value = _uiState.value.copy(selectedCollection = key, manualProfileId = manual)
        if (detailed) runCollection(connectAfter = false, detailed = true)
        else startQuickTests(selectedMembers(), batched = true)
    }

    private val regroupStore get() = appContext.getSharedPreferences("rovunet_regroup", android.content.Context.MODE_PRIVATE)
    fun refreshRegroupUndo() { _uiState.value = _uiState.value.copy(regroupUndoAvailable = regroupStore.contains("snapshot")) }
    fun undoRegroup() {
        if (_uiState.value.isLoading || _uiState.value.regroupBusy || _uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected) return
        val snapshot = regroupStore.getString("snapshot", null) ?: return
        _uiState.value = _uiState.value.copy(regroupBusy = true)
        viewModelScope.launch {
            try {
                val restored = app.slipnet.domain.model.RegroupHistory.restore(snapshot, _uiState.value.profiles)
                profileRepository.updateCollectionMemberships(restored)
                check(regroupStore.edit().remove("snapshot").commit())
                _uiState.value = _uiState.value.copy(regroupUndoAvailable = false, selectedCollection = null, manualProfileId = null)
            } catch (_: Exception) { _uiState.value = _uiState.value.copy(error = "Could not undo regrouping; later group changes are preserved") }
            finally { _uiState.value = _uiState.value.copy(regroupBusy = false) }
        }
    }
    fun previewRegroup() {
        if (_uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected) return
        val profiles = _uiState.value.profiles.map { it.copy(collectionId = "") }
        _uiState.value = _uiState.value.copy(regroupPreview = CollectionInference.plan(profiles), regroupUndoAvailable = regroupStore.contains("snapshot"))
    }
    fun cancelRegroup() { _uiState.value = _uiState.value.copy(regroupPreview = null) }
    fun confirmRegroup() {
        val groups = _uiState.value.regroupPreview ?: return
        if (_uiState.value.isLoading || _uiState.value.regroupBusy || _uiState.value.isPingRunning || !_uiState.value.connectionState.isDisconnected) return
        _uiState.value = _uiState.value.copy(regroupBusy = true)
        viewModelScope.launch {
            try {
                val current = _uiState.value.profiles.associateBy { it.id }
                val updated = groups.flatMap { group ->
                    val id = java.util.UUID.randomUUID().toString()
                    group.profiles.mapNotNull { p -> current[p.id]?.copy(collectionId = id, collectionName = group.name) }
                }
                val snapshot = app.slipnet.domain.model.RegroupHistory.encode(current.values.toList(), updated)
                check(regroupStore.edit().putString("snapshot", snapshot).commit()) { "Could not save undo" }
                profileRepository.updateCollectionMemberships(updated)
                _uiState.value = _uiState.value.copy(regroupPreview = null, selectedCollection = null, manualProfileId = null, regroupUndoAvailable = true)
            } catch (_: Exception) { _uiState.value = _uiState.value.copy(error = "Could not regroup profiles") }
            finally { _uiState.value = _uiState.value.copy(regroupBusy = false) }
        }
    }

    fun dismissCollectionPrompt() {
        _uiState.value = _uiState.value.copy(collectionPrompt = false, collectionProgress = "")
    }

    fun acceptCollectionPrompt() {
        if (_uiState.value.isPingRunning) return
        val connect = _uiState.value.collectionConnect
        _uiState.value = _uiState.value.copy(collectionPrompt = false)
        if (connect) {
            val paths = selectedMembers().filter { !it.needsCredentials && !it.isExpired && it.tunnelType.isAvailable() && ProfileCollections.isDns(it) }
            val target = ProfileCollections.ordered(paths, _uiState.value.pingResults).firstOrNull()
            if (target == null) _uiState.value = _uiState.value.copy(error = "No profile available to connect")
            else connectSelectedPath(target, profileResolvers = true)
        } else runCollection(connectAfter = false, dnsOnly = true)
    }

    private class ProbeInterrupted(val reason: Int) : Exception()

    private fun showInterruptedTest(e: ProbeInterrupted) {
        _uiState.value = _uiState.value.copy(error = when (e.reason) {
            app.slipnet.R.string.probe_busy -> "PROBE_BUSY"
            app.slipnet.R.string.probe_cleanup -> "PROBE_CLEANUP"
            else -> "PROBE_CHANGED"
        })
    }

    private fun finishTestResults(original: Map<Long, PingResult>): Map<Long, PingResult> =
        _uiState.value.pingResults.mapValues { (id, result) ->
            if (result is PingResult.Pending || result is PingResult.Testing)
                original[id]?.let { app.slipnet.domain.model.ProbeCache.historical(mapOf(id to it))[id] } ?: PingResult.Skipped else result
        }

    private fun runCollection(connectAfter: Boolean, dnsOnly: Boolean = false, detailed: Boolean = false) {
        if (_uiState.value.isPingRunning) { cancelPing(); return }
        if (!connectionManager.connectionState.value.isDisconnected) {
            _uiState.value = _uiState.value.copy(error = "Disconnect the VPN before testing a collection")
            return
        }
        if (!_uiState.value.proxyOnlyMode && android.net.VpnService.prepare(appContext) != null) {
            _uiState.value = _uiState.value.copy(error = "Grant VPN permission first")
            return
        }
        val selected = selectedMembers()
        if (onlyUnavailable(selected)) return
        val members = selected.filter { !it.needsCredentials && !it.isExpired && it.tunnelType.isAvailable() }
        if (members.isEmpty()) { _uiState.value = _uiState.value.copy(error = "Select a collection with an available, unexpired profile"); return }
        val ordered = ProfileCollections.ordered(members.filter { ProfileCollections.isDns(it) == dnsOnly }, _uiState.value.pingResults)
        val batchKey = ProfileCollections.key(members.first()) + ":" + dnsOnly
        val offset = (testBatchOffsets[batchKey] ?: 0).coerceAtMost((ordered.size - 1).coerceAtLeast(0))
        val candidates = (ordered.drop(offset) + ordered.take(offset)).take(50)
        val originalResults = _uiState.value.pingResults
        smartConnections.cancel(); smartConnections.clearResults()
        val memberIds = selected.map { it.id }.toSet()
        val historical = app.slipnet.domain.model.ProbeCache.historical(originalResults.filterKeys { it in memberIds })
        _uiState.value = _uiState.value.copy(isPingRunning = true, collectionConnect = connectAfter,
            selectedCollection = ProfileCollections.key(members.first()), collectionProgress = if (dnsOnly) "Testing configured DNS" else "Testing direct paths",
            pingResults = _uiState.value.pingResults + historical + candidates.associate { it.id to PingResult.Pending })
        activePathMonitor.pause(this, true)
        pingJob = viewModelScope.launch {
            app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.COLLECTION_START)
            var passed = false
            var completedInBatch = 0
            try {
                passed = app.slipnet.domain.model.CollectionPhase.run(candidates, connectAfter,
                    when {
                        dnsOnly -> app.slipnet.domain.model.ProbePolicy.testAllBudgetMs(candidates, detailed)
                        connectAfter -> 90_000L // stop at the first verified path
                        else -> app.slipnet.domain.model.ProbePolicy.testAllBudgetMs(candidates, detailed)
                    },
                    onProgress = { index, p -> _uiState.value = _uiState.value.copy(collectionProgress = "${index + 1}/${candidates.size} · ${p.name}") },
                    onResult = { p, result ->
                        completedInBatch++
                        if (ordered.isNotEmpty()) testBatchOffsets[batchKey] = (offset + completedInBatch) % ordered.size
                        _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (p.id to result))
                    },
                    pausedNanos = { app.slipnet.tunnel.TransportTrust.decisionWaitNanos },
                    probe = { p -> probeThroughService(p, keepConnected = connectAfter, profileResolvers = dnsOnly, detailed = detailed) })
                if (!passed && dnsOnly) passed = discoverCollectionDns(candidates, connectAfter, detailed)
                val untested = !connectAfter && candidates.any {
                    _uiState.value.pingResults[it.id].let { r -> r is PingResult.Pending || r is PingResult.Testing }
                }
                val needsReview = candidates.any { _uiState.value.pingResults[it.id] == PingResult.ReviewIdentity }
                when (app.slipnet.domain.model.CollectionPhase.completion(passed, untested, needsReview,
                        !dnsOnly && members.any { ProfileCollections.isDns(it) }, includeDnsAfterSuccess = false)) {
                    app.slipnet.domain.model.CollectionPhase.Completion.INCOMPLETE ->
                        _uiState.value = _uiState.value.copy(error = "The test stopped before every path was tested", collectionPrompt = false)
                    app.slipnet.domain.model.CollectionPhase.Completion.OFFER_DNS ->
                        _uiState.value = _uiState.value.copy(collectionPrompt = true)
                    app.slipnet.domain.model.CollectionPhase.Completion.NEEDS_REVIEW ->
                        _uiState.value = _uiState.value.copy(error = app.slipnet.presentation.theme.RovunetStrings.localizedContext(appContext)
                            .getString(app.slipnet.R.string.probe_review_identity))
                    app.slipnet.domain.model.CollectionPhase.Completion.NO_VERIFIED_PATH ->
                        _uiState.value = _uiState.value.copy(error = "No verified connection found within the test budget. Try again or edit this collection.")
                    app.slipnet.domain.model.CollectionPhase.Completion.COMPLETE -> Unit
                }
            } catch (e: kotlinx.coroutines.CancellationException) { throw e
            } catch (e: ProbeInterrupted) {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.COLLECTION_INTERRUPTED, code = when(e.reason) { app.slipnet.R.string.probe_busy -> 1; app.slipnet.R.string.probe_cleanup -> 2; else -> 3 })
                showInterruptedTest(e)
            } catch (error: Exception) {
                app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.COLLECTION_INTERRUPTED,
                    failure = app.slipnet.util.ConnectionDiagnostics.failure(error), code = 4)
                _uiState.value = _uiState.value.copy(error = "Collection test failed; no configuration was removed")
            } finally {
                withContext(kotlinx.coroutines.NonCancellable) {
                    _uiState.value = _uiState.value.copy(isPingRunning = false, collectionProgress = "",
                        pingResults = finishTestResults(originalResults))
                    activePathMonitor.pause(this@MainViewModel, false)
                    sortProfilesByPing()
                }
            }
        }
    }

    /** Bounded DNS preflight across all selected servers; real VPN tests stay serialized.
     * A DNS reply never becomes a successful ping. Originals remain unchanged.
     */
    private suspend fun discoverCollectionDns(profiles: List<ServerProfile>, connectAfter: Boolean, detailed: Boolean = false): Boolean = coroutineScope {
        val eligible = profiles.filter { it.dnsTransport == app.slipnet.domain.model.DnsTransport.UDP && !it.authoritativeMode && !it.dnsttAuthoritative }
        if (eligible.isEmpty()) return@coroutineScope false
        val hosts = resolverScannerRepository.getDefaultResolversShuffled().distinct().take(128)
        val tasks = Channel<Pair<ServerProfile, String>>(16)
        val ready = Channel<ServerProfile>(8)
        val producer = launch {
            try { for (host in hosts) for (p in eligible) tasks.send(p to host) } finally { tasks.close() }
        }
        val workers = List(4) { launch(Dispatchers.IO) {
            for ((p, host) in tasks) {
                val alive = try { resolverScannerRepository.isResolverAlive(host, 53, p.domain, 2000) }
                    catch (e: kotlinx.coroutines.CancellationException) { throw e } catch (_: Exception) { false }
                if (alive) ready.send(p.copy(resolvers = listOf(DnsResolver(host))))
            }
        } }
        val closer = launch { workers.forEach { it.join() }; ready.close() }
        var passed = false
        try {
            app.slipnet.domain.model.PausedTimeout.runOrNull(180_000, { app.slipnet.tunnel.TransportTrust.decisionWaitNanos }) {
                val attempts = mutableMapOf<Long, Int>()
                for (candidate in ready) {
                    if ((attempts[candidate.id] ?: 0) >= 3) continue
                    attempts[candidate.id] = (attempts[candidate.id] ?: 0) + 1
                    _uiState.value = _uiState.value.copy(collectionProgress = "DNS discovery · ${candidate.name}")
                    val result = probeThroughService(candidate, keepConnected = connectAfter, profileResolvers = true, detailed = detailed)
                    _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (candidate.id to result))
                    if (result is PingResult.Success && result.verified) {
                        // Remember only in this view-model session, never overwrite imported resolvers.
                        discoveredProfiles[candidate.id] = candidate
                        passed = true
                        if (connectAfter) break
                        attempts[candidate.id] = 3
                    }
                }
            }
        } finally {
            producer.cancel(); workers.forEach { it.cancel() }; closer.cancel()
            withContext(kotlinx.coroutines.NonCancellable) { producer.join(); workers.forEach { it.join() }; closer.join() }
        }
        passed
    }


    private fun startRealTests(profiles: List<ServerProfile>, detailed: Boolean = false) {
        if (profiles.isEmpty() || _uiState.value.isPingRunning) return
        if (!_uiState.value.proxyOnlyMode && android.net.VpnService.prepare(appContext) != null) {
            _uiState.value = _uiState.value.copy(error = "Connect once and grant Android VPN permission, then disconnect and run the HTTPS test.")
            return
        }
        val originalResults = _uiState.value.pingResults
        _uiState.value = _uiState.value.copy(isPingRunning = true,
            pingResults = _uiState.value.pingResults + profiles.associate { it.id to PingResult.Pending })
        activePathMonitor.pause(this, true)
        pingJob = viewModelScope.launch {
            try {
                for (profile in profiles) {
                    kotlinx.coroutines.currentCoroutineContext().ensureActive()
                    _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to PingResult.Testing("Connecting…")))
                    val result = probeThroughService(profile, detailed = detailed)
                    _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to result))
                }
            } catch (e: ProbeInterrupted) {
                showInterruptedTest(e)
            } finally {
                withContext(kotlinx.coroutines.NonCancellable) {
                    _uiState.value = _uiState.value.copy(isPingRunning = false,
                        pingResults = finishTestResults(originalResults))
                    activePathMonitor.pause(this@MainViewModel, false)
                    sortProfilesByPing()
                }
            }
        }
    }

    private suspend fun probeThroughService(profile: ServerProfile, keepConnected: Boolean = false, profileResolvers: Boolean = false, detailed: Boolean = false): PingResult {
        val before = connectionManager.connectionState.value
        val reuse = before is ConnectionState.Connected && before.profile.id == profile.id && before.chainId < 0
        if (!reuse && !before.isDisconnected) throw ProbeInterrupted(app.slipnet.R.string.probe_busy)
        if (profile.needsCredentials) return PingResult.Error(SAVED_CREDENTIALS_UNAVAILABLE)
        if (profile.isExpired) return PingResult.Error("Expired")
        val policy = app.slipnet.domain.model.ProbePolicy.forProfile(profile)
        val start = System.nanoTime()
        val initiallyPaused = app.slipnet.tunnel.TransportTrust.decisionWaitNanos
        var owned = false
        var attemptId = connectionManager.currentSessionId
        var verified = false
        try {
            if (!reuse) {
                // This test was explicitly requested on screen. Background resolver scans still cannot prompt.
                attemptId = connectionManager.connect(profile, profileResolvers, testMode = true, allowTrustPrompt = true)
                if (attemptId <= 0) throw ProbeInterrupted(app.slipnet.R.string.probe_busy)
                owned = true
                val state = app.slipnet.domain.model.PausedTimeout.runOrNull(policy.setupMs,
                    { app.slipnet.tunnel.TransportTrust.decisionWaitNanos }) {
                    connectionManager.connectionState.first { it is ConnectionState.Connected || it is ConnectionState.Error || it is ConnectionState.Disconnected }
                } ?: return PingResult.Error("Connection timed out")
                if (!connectionManager.ownsSession(attemptId)) throw ProbeInterrupted(app.slipnet.R.string.probe_changed)
                if (state is ConnectionState.Error) {
                    if (app.slipnet.core.ProbeFailure.isLocalServiceFailure(state.message)) throw ProbeInterrupted(app.slipnet.R.string.probe_busy)
                    val reason = app.slipnet.core.ProbeFailure.connection(state.message)
                    if (reason == app.slipnet.core.ProbeFailure.APPROVAL_REQUIRED) return PingResult.ReviewIdentity
                    app.slipnet.util.AppLog.w("CollectionProbe", reason)
                    return PingResult.Error(reason)
                }
                if (state !is ConnectionState.Connected || state.profile.id != profile.id || state.chainId >= 0)
                    throw ProbeInterrupted(app.slipnet.R.string.probe_changed)
            }
            val setup = if (reuse) 0L else ((System.nanoTime() - start -
                (app.slipnet.tunnel.TransportTrust.decisionWaitNanos - initiallyPaused)).coerceAtLeast(0L) / 1_000_000)
            val socks = if (profile.tunnelType == TunnelType.XRAY) {
                app.slipnet.tunnel.XrayBridge.activeEndpoint ?: throw ProbeInterrupted(app.slipnet.R.string.probe_changed)
            } else preferencesDataStore.configuredSocksEndpoint()
            val measurements = app.slipnet.core.TunnelProbe.sample(onAttempt = { n ->
                val current = connectionManager.connectionState.value
                if (!connectionManager.ownsSession(attemptId) || current !is ConnectionState.Connected ||
                    current.profile.id != profile.id || current.chainId >= 0) throw ProbeInterrupted(app.slipnet.R.string.probe_changed)
                _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to PingResult.Testing(
                    if (detailed) "HTTPS $n/3" else "HTTPS")))
            }, successTarget = if (detailed) 3 else 1) { host ->
                try {
                    app.slipnet.core.TunnelProbe.measureCancellable(socks.port, socks.user, socks.password, host, policy.httpsMs, diagnosticSession = attemptId).also {
                        app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.PROBE_HTTPS_OK, attemptId)
                    }
                } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
                catch (error: Exception) {
                    app.slipnet.util.ConnectionDiagnostics.record(app.slipnet.util.ConnectionDiagnostics.Event.PROBE_HTTPS_FAILED,
                        attemptId, app.slipnet.util.ConnectionDiagnostics.failure(error))
                    throw error
                }
            }
            kotlinx.coroutines.currentCoroutineContext().ensureActive()
            if (!connectionManager.ownsSession(attemptId) || connectionManager.connectionState.value !is ConnectionState.Connected)
                throw ProbeInterrupted(app.slipnet.R.string.probe_changed)
            val summary = measurements.summary ?: return PingResult.Error("HTTPS 0/${measurements.attempts} · ${measurements.failures}")
            verified = true
            return PingResult.Success(summary.medianMs, true, summary.successes, summary.attempts, summary.jitterMs, setup, quick = !detailed)
        } catch (_: kotlinx.coroutines.TimeoutCancellationException) {
            kotlinx.coroutines.currentCoroutineContext().ensureActive()
            return PingResult.Error("Connection timed out")
        } catch (e: kotlinx.coroutines.CancellationException) { throw e
        } catch (e: ProbeInterrupted) { throw e
        } catch (e: Exception) {
            val reason = app.slipnet.core.ProbeFailure.describe(e)
            app.slipnet.util.AppLog.w("CollectionProbe", reason)
            return PingResult.Error(reason)
        } finally {
            if (owned && !(keepConnected && verified)) withContext(kotlinx.coroutines.NonCancellable) {
                if (connectionManager.ownsSession(attemptId)) {
                    connectionManager.disconnect(expectedSessionId = attemptId)
                    try { kotlinx.coroutines.withTimeout(20000) { connectionManager.awaitSessionDisconnected(attemptId) } }
                    catch (_: kotlinx.coroutines.TimeoutCancellationException) {
                        // Never start another test over a backend that has not acknowledged teardown.
                        throw ProbeInterrupted(app.slipnet.R.string.probe_cleanup)
                    }
                    connectionManager.finishProbe(attemptId)
                }
            }
        }
    }

    fun pingAllProfilesSimple() {
        if (_uiState.value.isPingRunning) {
            cancelPing()
            return
        }

        startQuickTests(selectedMembers())
    }

    private fun startQuickTests(selected: List<ServerProfile>, batched: Boolean = false) {
        if (_uiState.value.isPingRunning || onlyUnavailable(selected)) return
        val eligible = selected.filterNot { it.needsCredentials || it.isExpired || !it.tunnelType.isAvailable() }
        if (eligible.isEmpty()) return
        val batchKey = ProfileCollections.key(eligible.first()) + ":quick"
        val offset = if (batched) (testBatchOffsets[batchKey] ?: 0).coerceAtMost(eligible.lastIndex) else 0
        val profiles = if (batched) (eligible.drop(offset) + eligible.take(offset)).take(50) else eligible
        val originalResults = _uiState.value.pingResults
        val network = physicalNetworkSnapshot()
        val skipped = PING_UNSUPPORTED_TYPES
        val initial = profiles.associate { profile ->
            profile.id to if (profile.tunnelType in skipped || (ProfileCollections.isUdp(profile) && !app.slipnet.core.ProtocolHandshake.supports(profile.coreLink))) {
                PingResult.Skipped
            } else {
                PingResult.Pending
            }
        }
        _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + initial, isPingRunning = true)

        activePathMonitor.pause(this, true)
        pingJob = viewModelScope.launch {
            var completed = initial.values.count { it == PingResult.Skipped }
            try {
                _uiState.value = _uiState.value.copy(pingingViaGlobalDns = preferencesDataStore.parsedGlobalResolvers())
                app.slipnet.util.scanWorkers(profiles.filter { initial[it.id] == PingResult.Pending }, 4) { profile ->
                    if (ProfileCollections.isUdp(profile)) {
                        val result = pingProtocol(profile)
                        completed++
                        val current = _uiState.value.profiles.firstOrNull { it.id == profile.id }
                        if (current != null && ProfileDuplicates.key(current) == ProfileDuplicates.key(profile)) {
                            val measurement = if (physicalNetworkSnapshot() == network) result
                                else app.slipnet.domain.model.ProbeCache.historical(mapOf(profile.id to result)).getValue(profile.id)
                            _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to measurement))
                        }
                    } else {
                        val target = getSimplePingTarget(profile)
                        val result = if (target != null) {
                            pingTcp(target.first, target.second)
                        } else {
                            PingResult.Error("No target")
                        }
                        completed++
                        val current = _uiState.value.profiles.firstOrNull { it.id == profile.id }
                        if (current != null && ProfileDuplicates.key(current) == ProfileDuplicates.key(profile)) {
                            val measurement = if (physicalNetworkSnapshot() == network) result
                                else app.slipnet.domain.model.ProbeCache.historical(mapOf(profile.id to result)).getValue(profile.id)
                            _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to measurement))
                        }
                    }
                }
            } finally {
                if (batched && completed > 0) testBatchOffsets[batchKey] = (offset + completed) % eligible.size
                _uiState.value = _uiState.value.copy(isPingRunning = false, pingResults = finishTestResults(originalResults))
                activePathMonitor.pause(this@MainViewModel, false)
                rememberHistory()
            }
        }
    }

    private suspend fun getSimplePingTarget(profile: ServerProfile): Pair<String, Int>? {
        // Try direct TCP target first, including legacy/SIP002 Shadowsocks authorities.
        if (profile.tunnelType == TunnelType.XRAY && profile.coreLink.startsWith("ss://", true)) return runCatching {
            val parsed = app.slipnet.core.CoreLink.parse(profile.coreLink)
            parsed.host to parsed.outbound.getJSONObject("settings").getJSONArray("servers").getJSONObject(0).getInt("port")
        }.getOrNull()
        getTcpTarget(profile)?.let { return it }
        ProfileCollections.coreTcpTarget(profile)?.let { return it }
        // For DNS-tunneled profiles, ping the first resolver — honoring the
        // global DNS override so the test mirrors what Connect actually uses.
        val resolver = effectiveResolvers(profile).firstOrNull() ?: return null
        return resolver.host to resolver.port
    }

    /**
     * Resolvers that Connect would actually use for [profile]: the user's
     * Global DNS override list when enabled and non-empty, otherwise the
     * profile's own resolvers.
     */
    private suspend fun effectiveResolvers(profile: ServerProfile): List<DnsResolver> {
        return preferencesDataStore.parsedGlobalResolvers().ifEmpty { profile.resolvers }
    }

    private suspend fun pingTcp(host: String, port: Int): PingResult {
        return try {
                PingResult.Success(app.slipnet.core.TcpPing.measure(host, port), quick = true)
            } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled
            } catch (e: Exception) {
                val msg = when (e) {
                    is java.net.SocketTimeoutException -> "Timeout"
                    is java.net.ConnectException -> "Refused"
                    is java.net.UnknownHostException -> "DNS failed"
                    else -> e.message?.take(20) ?: "Failed"
                }
                PingResult.Error(msg)
            }
    }

    private suspend fun pingProtocol(profile: ServerProfile): PingResult = try {
        val native = java.io.File(appContext.applicationInfo.nativeLibraryDir)
        val elapsed = app.slipnet.core.ProtocolHandshake.measure(profile.coreLink,
            java.io.File(native, "librovunet_awg.so"), java.io.File(native, "libveyra_core_launcher.so"),
            java.io.File(appContext.filesDir, "native-certificates"))
        PingResult.Success(elapsed, quick = true, handshake = true)
    } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
      catch (_: Exception) { PingResult.Error("Protocol handshake failed") }

    fun pingSingleProfile(profile: ServerProfile, detailed: Boolean = false) {
        if (rejectUnavailableCredentials(profile)) return
        if (_uiState.value.isPingRunning) return
        if (detailed) startRealTests(listOf(profile), detailed = true)
        else startQuickTests(listOf(profile))
    }

    fun cancelPing() {
        pingJob?.cancel()
        // Keep the running flag until owned service cleanup has finished.
    }

    fun clearPingResults() {
        _uiState.value = _uiState.value.copy(pingResults = emptyMap(), pingingViaGlobalDns = emptyList(), sortByHttps = false)
        rememberHistory()
    }

    /**
     * Persistently reorder the profile list ascending by last-measured ping
     * latency. Profiles without a successful ping keep their relative order
     * and sit at the bottom. Mirrors [moveProfile] by updating both in-memory
     * state and DB so the new order survives restarts.
     */
    fun sortProfilesByPing(reliability: Boolean = false) {
        val current = _uiState.value.profiles
        val pings = _uiState.value.pingResults
        val sorted = app.slipnet.domain.model.ProfileRanking.sort(current, pings, reliability)
        if (sorted == current) { _uiState.value = _uiState.value.copy(sortByHttps = true, sortByReliability = reliability); rememberHistory(); return }
        _uiState.value = _uiState.value.copy(profiles = sorted, sortByHttps = true, sortByReliability = reliability)
        rememberHistory()
        viewModelScope.launch {
            profileRepository.updateProfileOrder(sorted.map { it.id })
        }
    }

    private fun getTcpTarget(profile: ServerProfile): Pair<String, Int>? {
        return when (profile.tunnelType) {
            TunnelType.SSH -> profile.domain to profile.sshPort
            TunnelType.NAIVE_SSH -> profile.domain to profile.naivePort
            TunnelType.NAIVE -> profile.domain to profile.naivePort
            TunnelType.DOH -> {
                val host = try {
                    java.net.URL(profile.dohUrl).host
                } catch (_: Exception) { return null }
                host to 443
            }
            TunnelType.SOCKS5 -> profile.domain to profile.socks5ServerPort
            TunnelType.VLESS -> {
                // Test reachability to the CDN edge — that's what the TLS/WS
                // handshake will hit. Fall back to the configured domain on
                // 443 if the user didn't pin a specific CDN IP.
                val host = profile.cdnIp.takeIf { it.isNotBlank() } ?: profile.domain
                val port = if (profile.cdnPort > 0) profile.cdnPort else 443
                if (host.isBlank()) null else host to port
            }
            else -> null
        }
    }
}
