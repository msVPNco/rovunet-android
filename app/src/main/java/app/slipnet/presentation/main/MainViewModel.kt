package app.slipnet.presentation.main

import app.slipnet.domain.model.ProfileDuplicates
import app.slipnet.domain.model.CollectionInference
import app.slipnet.domain.model.ProfileCollections
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.cancelAndJoin
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.local.datastore.PreferencesDataStore
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
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
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
    val connectedChainId: Long? = null,
    val isLoading: Boolean = false,
    val error: String? = null,
    val exportedJson: String? = null,
    val importPreview: ImportPreview? = null,
    val isImporting: Boolean = false,
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
    private val profileRepository: ProfileRepository,
    private val chainRepository: ChainRepository,
    private val resolverScannerRepository: ResolverScannerRepository,
    private val configExporter: ConfigExporter,
    private val configImporter: ConfigImporter,
    private val preferencesDataStore: PreferencesDataStore
) : ViewModel() {

    private val _uiState = MutableStateFlow(MainUiState())
    val uiState: StateFlow<MainUiState> = _uiState.asStateFlow()

    private var bootstrapPollingJob: Job? = null
    private var trafficPollingJob: Job? = null
    private var sleepTimerJob: Job? = null
    private var pingJob: Job? = null
    private var importJob: Job? = null

    private val networkManager = appContext.getSystemService(android.content.Context.CONNECTIVITY_SERVICE) as android.net.ConnectivityManager
    private val underlyingNetworks = mutableSetOf<android.net.Network>()
    private var networkCallbackRegistered = false
    private val networkCallback = object : android.net.ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: android.net.Network) { networkChanged(network, true) }
        override fun onLost(network: android.net.Network) { networkChanged(network, false) }
    }
    init {
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
    private fun networkChanged(network: android.net.Network, available: Boolean) {
        viewModelScope.launch {
            val changed = if (available) underlyingNetworks.add(network) else underlyingNetworks.remove(network)
            if (!changed) return@launch
            pingJob?.cancelAndJoin()
            discoveredProfiles.clear()
            clearPingResults()
            _uiState.value = _uiState.value.copy(collectionPrompt = false)
        }
    }
    override fun onCleared() {
        if (networkCallbackRegistered) runCatching { networkManager.unregisterNetworkCallback(networkCallback) }
        super.onCleared()
    }

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
                    connectedProfileId = if (chainId != null) null else connectedId,
                    connectedChainId = chainId,
                    error = if (state is ConnectionState.Error && !_uiState.value.isPingRunning) state.message else _uiState.value.error
                )
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
        viewModelScope.launch {
            // Throttle: skip if checked recently
            val lastCheck = preferencesDataStore.lastUpdateCheckTime.first()
            val now = System.currentTimeMillis()
            if (now - lastCheck < app.slipnet.util.UpdateChecker.CHECK_INTERVAL_MS) return@launch
            preferencesDataStore.setLastUpdateCheckTime(now)

            val skipped = preferencesDataStore.skippedUpdateVersion.first()
            val current = app.slipnet.BuildConfig.VERSION_NAME

            val update = app.slipnet.util.UpdateChecker.check(current) ?: return@launch

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
            combine(
                getProfilesUseCase(),
                getActiveProfileUseCase()
            ) { profiles, activeProfile ->
                Pair(profiles, activeProfile)
            }.collect { (profiles, activeProfile) ->
                val previous = _uiState.value.profiles.associateBy { it.id }
                val unchanged = profiles.filter { p -> previous[p.id]?.let { ProfileDuplicates.key(it) == ProfileDuplicates.key(p) } == true }.map { it.id }.toSet()
                val results = _uiState.value.pingResults.filterKeys { it in unchanged }
                discoveredProfiles.keys.retainAll(unchanged)
                _uiState.value = _uiState.value.copy(
                    profiles = if (_uiState.value.sortByHttps) app.slipnet.domain.model.ProfileRanking.sort(profiles, _uiState.value.pingResults, _uiState.value.sortByReliability) else profiles,
                    activeProfile = activeProfile,
                    pingResults = results,
                    manualProfileId = _uiState.value.manualProfileId?.takeIf { id -> profiles.any { it.id == id } },
                    isLoading = false
                )
            }
        }
    }

    fun connect(profile: ServerProfile? = null) {
        if (_uiState.value.isPingRunning) { _uiState.value = _uiState.value.copy(error = "Stop the connection test first"); return }
        val targetProfile = profile ?: when (val choice = app.slipnet.domain.model.ConnectionChoice.resolve(
            _uiState.value.manualProfileId, _uiState.value.profiles)) {
            app.slipnet.domain.model.ConnectionChoice.Target.Automatic -> { runCollection(connectAfter = true); return }
            app.slipnet.domain.model.ConnectionChoice.Target.Missing -> {
                _uiState.value = _uiState.value.copy(error = "No profile available to connect")
                return
            }
            is app.slipnet.domain.model.ConnectionChoice.Target.Direct -> choice.profile
        }
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
        connectionManager.connect(targetProfile)
    }

    fun disconnect() {
        if (_uiState.value.isPingRunning) { cancelPing(); return }
        connectionManager.disconnect()
    }

    fun toggleConnection() {
        when (_uiState.value.connectionState) {
            is ConnectionState.Connected,
            is ConnectionState.Connecting,
            is ConnectionState.Error -> disconnect()
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
            }.collect { (chains, activeChain) ->
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

    fun selectCollection(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        _uiState.value = _uiState.value.copy(selectedCollection = ProfileCollections.key(profile), manualProfileId = null)
        viewModelScope.launch { setActiveProfileUseCase(profile.id) }
    }

    fun moveCollection(source: ServerProfile, target: ServerProfile) {
        if (_uiState.value.isPingRunning || !connectionManager.connectionState.value.isDisconnected) return
        viewModelScope.launch {
            val sourceKey = ProfileCollections.key(source)
            val targetKey = ProfileCollections.key(target)
            val id = target.collectionId.ifBlank { java.util.UUID.randomUUID().toString() }
            profileRepository.updateProfiles(_uiState.value.profiles.filter { ProfileCollections.key(it) == sourceKey || ProfileCollections.key(it) == targetKey }.map {
                it.copy(collectionId = id, collectionName = ProfileCollections.name(target))
            })
            _uiState.value = _uiState.value.copy(selectedCollection = id)
        }
    }

    fun renameCollection(profile: ServerProfile, name: String) {
        if (_uiState.value.isPingRunning || name.isBlank()) return
        viewModelScope.launch {
            val key = ProfileCollections.key(profile)
            profileRepository.updateProfiles(_uiState.value.profiles.filter { ProfileCollections.key(it) == key }.map {
                it.copy(collectionName = name.trim().take(120))
            })
        }
    }

    fun setActiveProfile(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        _uiState.value = _uiState.value.copy(selectedCollection = ProfileCollections.key(profile), manualProfileId = profile.id)
        val state = _uiState.value.connectionState
        val isConnectedOrConnecting = state is ConnectionState.Connected ||
                state is ConnectionState.Connecting

        viewModelScope.launch {
            setActiveProfileUseCase(profile.id)
        }

        if (isConnectedOrConnecting && _uiState.value.connectedProfileId != profile.id) {
            connectionManager.reconnect(profile)
        }
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

    fun exportProfile(profile: ServerProfile, hideResolvers: Boolean = false) {
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
        viewModelScope.launch(Dispatchers.Default) {
        val json = configExporter.exportSingleProfileLocked(
            profile, password, expirationDate, allowSharing, boundDeviceId, hideResolvers
        )
        _uiState.value = _uiState.value.copy(exportedJson = json)
    
        }
    }

    fun exportCollection(profile: ServerProfile) {
        viewModelScope.launch(Dispatchers.Default) {
        try {
            val data = configExporter.exportCollection(_uiState.value.profiles, profile)
            _uiState.value = _uiState.value.copy(exportedJson = data)
        } catch (e: Exception) {
            val reason = e.message?.takeIf { it in setOf("No exportable profiles", "Collection contains profiles that do not allow sharing") } ?: "Collection export failed"
            _uiState.value = _uiState.value.copy(error = reason)
        }
    
        }
    }

    fun exportAllProfiles() {
        viewModelScope.launch(Dispatchers.Default) {
        val profiles = _uiState.value.profiles.filter { !it.isLocked }
        if (profiles.isEmpty()) {
            _uiState.value = _uiState.value.copy(error = "No exportable profiles")
            return@launch
        }
        val json = configExporter.exportAllProfiles(profiles)
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
        viewModelScope.launch(Dispatchers.Default) {
        val profiles = _uiState.value.profiles.filter { !it.isLocked }
        if (profiles.isEmpty()) {
            _uiState.value = _uiState.value.copy(error = "No exportable profiles")
            return@launch
        }
        try {
            val encrypted = configExporter.exportAllProfilesEncrypted(
                profiles,
                bundlePassword,
                profilePassword,
                expirationDate,
                allowSharing,
                boundDeviceId,
                hideResolvers
            )
            _uiState.value = _uiState.value.copy(exportedJson = encrypted)
        } catch (e: Exception) {
            _uiState.value = _uiState.value.copy(error = "Failed to encrypt: ${e.message}")
        }
    
        }
    }

    fun clearExportedJson() {
        _uiState.value = _uiState.value.copy(exportedJson = null)
    }

    fun parseImportConfig(json: String, bundlePassword: String? = null) {
        if (_uiState.value.isImporting) return
        _uiState.value = _uiState.value.copy(isImporting = true)
        importJob = viewModelScope.launch(Dispatchers.Default) {
            try {
        val result = configImporter.parseAndImport(
            json, connectionManager.getDeviceId(), bundlePassword
        )
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
        val preview = _uiState.value.importPreview ?: return
        viewModelScope.launch {
            try {
                val importedProfiles = preview.groups.flatMap { group ->
                    val id = java.util.UUID.randomUUID().toString()
                    val name = if (preview.groups.size == 1) preview.collectionName else group.name
                    group.profiles.map { it.copy(collectionId = id, collectionName = name.trim().take(120)) }
                }
                profileRepository.insertCollection(importedProfiles).firstOrNull()?.let { setActiveProfileUseCase(it) }
                _uiState.value = _uiState.value.copy(selectedCollection = null, manualProfileId = null)
                _uiState.value = _uiState.value.copy(
                    importPreview = null,
                    error = null
                )
            } catch (e: Exception) {
                _uiState.value = _uiState.value.copy(
                    error = "Failed to import profiles: ${e.message}",
                    importPreview = null
                )
            }
        }
    }

    fun cancelImport() {
        importJob?.cancel()
        _uiState.value = _uiState.value.copy(importPreview = null)
    }

    fun showQrCode(profile: ServerProfile, hideResolvers: Boolean = false) {
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
        viewModelScope.launch(Dispatchers.Default) {
        val configUri = configExporter.exportSingleProfileLocked(
            profile, password, expirationDate, allowSharing, boundDeviceId, hideResolvers
        )
        _uiState.value = _uiState.value.copy(
            qrCodeData = QrCodeData(profile.name, configUri)
        )
    
        }
    }

    fun reExportLockedProfile(profile: ServerProfile) {
        viewModelScope.launch(Dispatchers.Default) {
        try {
            val json = configExporter.reExportLockedProfile(profile)
            _uiState.value = _uiState.value.copy(exportedJson = json)
        } catch (e: Exception) {
            _uiState.value = _uiState.value.copy(error = e.message ?: "Re-export failed")
        }
    
        }
    }

    fun showQrCodeLockedReExport(profile: ServerProfile) {
        viewModelScope.launch(Dispatchers.Default) {
        try {
            val configUri = configExporter.reExportLockedProfile(profile)
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

    fun pingAllProfiles() { runCollection(connectAfter = false) }

    fun pingCollection(profile: ServerProfile) {
        if (_uiState.value.isPingRunning || _uiState.value.collectionPrompt) return
        val key = ProfileCollections.key(profile)
        val manual = _uiState.value.manualProfileId?.takeIf { id ->
            _uiState.value.profiles.any { it.id == id && ProfileCollections.key(it) == key }
        }
        _uiState.value = _uiState.value.copy(selectedCollection = key, manualProfileId = manual)
        runCollection(connectAfter = false)
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
                profileRepository.updateProfiles(restored)
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
                profileRepository.updateProfiles(updated)
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
        runCollection(connectAfter = connect, dnsOnly = true)
    }

    private fun runCollection(connectAfter: Boolean, dnsOnly: Boolean = false) {
        if (_uiState.value.isPingRunning) { cancelPing(); return }
        if (!connectionManager.connectionState.value.isDisconnected) {
            _uiState.value = _uiState.value.copy(error = "Disconnect the VPN before testing a collection")
            return
        }
        if (!_uiState.value.proxyOnlyMode && android.net.VpnService.prepare(appContext) != null) {
            _uiState.value = _uiState.value.copy(error = "Grant VPN permission first")
            return
        }
        val members = selectedMembers().filter { !it.isExpired && it.tunnelType.isAvailable() }
        if (members.isEmpty()) { _uiState.value = _uiState.value.copy(error = "Select a collection with an available, unexpired profile"); return }
        val candidates = ProfileCollections.ordered(members, _uiState.value.pingResults).filter { ProfileCollections.isDns(it) == dnsOnly }
        val originalSelection = _uiState.value.activeProfile?.id
        _uiState.value = _uiState.value.copy(isPingRunning = true, collectionConnect = connectAfter,
            selectedCollection = ProfileCollections.key(members.first()), collectionProgress = if (dnsOnly) "Testing configured DNS" else "Testing direct paths",
            pingResults = _uiState.value.pingResults + candidates.associate { it.id to PingResult.Pending })
        pingJob = viewModelScope.launch {
            var passed = false
            try {
                passed = app.slipnet.domain.model.CollectionPhase.run(candidates, connectAfter,
                    if (dnsOnly) 180_000L else 90_000L,
                    onProgress = { index, p -> _uiState.value = _uiState.value.copy(collectionProgress = "${index + 1}/${candidates.size} · ${p.name}") },
                    onResult = { p, result -> _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (p.id to result)) },
                    probe = { p -> probeThroughService(p, keepConnected = connectAfter, profileResolvers = dnsOnly) })
                if (!passed && dnsOnly) passed = discoverCollectionDns(candidates, connectAfter)
                if (!passed && !dnsOnly && members.any { ProfileCollections.isDns(it) }) {
                    _uiState.value = _uiState.value.copy(collectionPrompt = true)
                } else if (!passed) {
                    _uiState.value = _uiState.value.copy(error = "No verified connection found within the test budget. Try again or edit this collection.")
                }
            } catch (e: kotlinx.coroutines.CancellationException) { throw e
            } catch (_: Exception) {
                _uiState.value = _uiState.value.copy(error = "Collection test failed; no configuration was removed")
            } finally {
                withContext(kotlinx.coroutines.NonCancellable) {
                    if (connectionManager.connectionState.value.isDisconnected) {
                        if (originalSelection != null) profileRepository.setActiveProfile(originalSelection) else profileRepository.clearActiveProfile()
                    }
                    if (connectionManager.connectionState.value.isDisconnected) connectionManager.disconnect()
                    _uiState.value = _uiState.value.copy(isPingRunning = false, collectionProgress = "",
                        pingResults = _uiState.value.pingResults.mapValues { (_, v) -> if (v is PingResult.Pending || v is PingResult.Testing) PingResult.Skipped else v })
                    sortProfilesByPing()
                }
            }
        }
    }

    /** Bounded DNS preflight across all selected servers; real VPN tests stay serialized.
     * A DNS reply never becomes a successful ping. Originals remain unchanged.
     */
    private suspend fun discoverCollectionDns(profiles: List<ServerProfile>, connectAfter: Boolean): Boolean = coroutineScope {
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
            kotlinx.coroutines.withTimeoutOrNull(180_000) {
                val attempts = mutableMapOf<Long, Int>()
                for (candidate in ready) {
                    if ((attempts[candidate.id] ?: 0) >= 3) continue
                    attempts[candidate.id] = (attempts[candidate.id] ?: 0) + 1
                    _uiState.value = _uiState.value.copy(collectionProgress = "DNS discovery · ${candidate.name}")
                    val result = probeThroughService(candidate, keepConnected = connectAfter, profileResolvers = true)
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

    private val discoveredProfiles = mutableMapOf<Long, ServerProfile>()

    private fun startRealTests(profiles: List<ServerProfile>) {
        if (profiles.isEmpty() || _uiState.value.isPingRunning) return
        if (!_uiState.value.proxyOnlyMode && android.net.VpnService.prepare(appContext) != null) {
            _uiState.value = _uiState.value.copy(error = "Connect once and grant Android VPN permission, then disconnect and run the HTTPS test.")
            return
        }
        val originalSelection = _uiState.value.activeProfile?.id
        _uiState.value = _uiState.value.copy(isPingRunning = true,
            pingResults = _uiState.value.pingResults + profiles.associate { it.id to PingResult.Pending })
        pingJob = viewModelScope.launch {
            try {
                for (profile in profiles) {
                    kotlinx.coroutines.currentCoroutineContext().ensureActive()
                    _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to PingResult.Testing("Connecting…")))
                    val result = probeThroughService(profile)
                    _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to result))
                }
            } finally {
                withContext(kotlinx.coroutines.NonCancellable) {
                    if (connectionManager.connectionState.value.isDisconnected) {
                        if (originalSelection != null) profileRepository.setActiveProfile(originalSelection) else profileRepository.clearActiveProfile()
                    }
                    if (connectionManager.connectionState.value.isDisconnected) connectionManager.disconnect()
                    _uiState.value = _uiState.value.copy(isPingRunning = false,
                        pingResults = _uiState.value.pingResults.mapValues { (_, v) -> if (v is PingResult.Pending || v is PingResult.Testing) PingResult.Skipped else v })
                    sortProfilesByPing()
                }
            }
        }
    }

    private suspend fun probeThroughService(profile: ServerProfile, keepConnected: Boolean = false, profileResolvers: Boolean = false): PingResult {
        val before = connectionManager.connectionState.value
        val reuse = before is ConnectionState.Connected && before.profile.id == profile.id && before.chainId < 0
        if (!reuse && !before.isDisconnected) return PingResult.Error("Disconnect VPN first")
        if (profile.isExpired) return PingResult.Error("Expired")
        val start = System.nanoTime()
        var owned = false
        var attemptId = connectionManager.currentSessionId
        var verified = false
        try {
            if (!reuse) {
                owned = true
                attemptId = connectionManager.connect(profile, profileResolvers, testMode = true)
                check(attemptId > 0) { "Connection request rejected" }
                val state = kotlinx.coroutines.withTimeout(if (ProfileCollections.isDns(profile) || profile.tunnelType == TunnelType.SNOWFLAKE) 45000 else 20000) {
                    connectionManager.connectionState.first { it is ConnectionState.Connected || it is ConnectionState.Error || it is ConnectionState.Disconnected }
                }
                check(connectionManager.ownsSession(attemptId)) { "Connection changed during test" }
                if (state is ConnectionState.Error) {
                    val reason = app.slipnet.core.ProbeFailure.connection(state.message)
                    app.slipnet.util.AppLog.w("CollectionProbe", reason)
                    return PingResult.Error(reason)
                }
                if (state is ConnectionState.Disconnected) return PingResult.Error("Tunnel stopped during setup")
                check(state is ConnectionState.Connected && state.profile.id == profile.id && state.chainId < 0) { "Connection changed during test" }
            }
            val setup = if (reuse) 0L else (System.nanoTime() - start) / 1_000_000
            val port = preferencesDataStore.proxyListenPort.first()
            val auth = preferencesDataStore.proxyAuthEnabled.first()
            val user = if (auth) preferencesDataStore.proxyAuthUsername.first() else null
            val pass = if (auth) preferencesDataStore.proxyAuthPassword.first() else null
            val samples = mutableListOf<Long?>()
            var lastFailure = "Unknown probe failure"
            var probeHost = app.slipnet.core.TunnelProbe.HOST
            repeat(3) { n ->
                kotlinx.coroutines.currentCoroutineContext().ensureActive()
                val current = connectionManager.connectionState.value
                check(current is ConnectionState.Connected && current.profile.id == profile.id && current.chainId < 0) { "Connection changed during test" }
                _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + (profile.id to PingResult.Testing("HTTPS ${n + 1}/3")))
                val latency = withContext(Dispatchers.IO) {
                    try {
                        // A single blocked destination must not mark a usable tunnel as dead.
                        // Cache the successful target for the remaining samples of this attempt.
                        try { app.slipnet.core.TunnelProbe.measure(port, user, pass, host = probeHost) }
                        catch (e: Exception) {
                            kotlinx.coroutines.currentCoroutineContext().ensureActive()
                            if (probeHost != app.slipnet.core.TunnelProbe.HOST) throw e
                            val elapsed = app.slipnet.core.TunnelProbe.measure(port, user, pass, host = "cp.cloudflare.com")
                            probeHost = "cp.cloudflare.com"
                            elapsed
                        }
                    }
                    catch (e: kotlinx.coroutines.CancellationException) { throw e }
                    catch (e: Exception) { lastFailure = app.slipnet.core.ProbeFailure.describe(e); app.slipnet.util.AppLog.w("CollectionProbe", "Sample ${n + 1}/3: $lastFailure"); null }
                }
                kotlinx.coroutines.currentCoroutineContext().ensureActive()
                samples += latency
                delay(300)
            }
            val summary = app.slipnet.core.ProbeSummary.of(samples) ?: return PingResult.Error("HTTPS failed 0/3: $lastFailure")
            verified = true
            return PingResult.Success(summary.medianMs, true, summary.successes, summary.attempts, summary.jitterMs, setup)
        } catch (_: kotlinx.coroutines.TimeoutCancellationException) {
            kotlinx.coroutines.currentCoroutineContext().ensureActive()
            return PingResult.Error("Connection timed out")
        } catch (e: kotlinx.coroutines.CancellationException) { throw e
        } catch (e: Exception) {
            val reason = app.slipnet.core.ProbeFailure.describe(e)
            app.slipnet.util.AppLog.w("CollectionProbe", reason)
            return PingResult.Error(reason)
        } finally {
            if (owned && !(keepConnected && verified)) withContext(kotlinx.coroutines.NonCancellable) {
                val current = connectionManager.connectionState.value
                if (connectionManager.ownsSession(attemptId)) {
                    connectionManager.disconnect(keepServiceAlive = true, expectedSessionId = attemptId)
                    try { kotlinx.coroutines.withTimeout(20000) { connectionManager.awaitSessionDisconnected(attemptId) } }
                    catch (_: kotlinx.coroutines.TimeoutCancellationException) {
                        // Never start another test over a backend that has not acknowledged teardown.
                        throw IllegalStateException("Tunnel cleanup timed out; stop the VPN before testing again")
                    }
                }
            }
        }
    }

    fun pingAllProfilesSimple() {
        if (_uiState.value.isPingRunning) {
            cancelPing()
            return
        }

        val profiles = selectedMembers()
        if (profiles.isEmpty()) return

        val skipped = PING_UNSUPPORTED_TYPES
        val initial = profiles.associate { profile ->
            profile.id to if (profile.tunnelType in skipped) {
                PingResult.Skipped
            } else {
                PingResult.Pending
            }
        }
        _uiState.value = _uiState.value.copy(pingResults = _uiState.value.pingResults + initial, isPingRunning = true)

        pingJob = viewModelScope.launch {
            _uiState.value = _uiState.value.copy(
                pingingViaGlobalDns = preferencesDataStore.parsedGlobalResolvers()
            )
            try {
                val jobs = profiles.filter { it.tunnelType !in skipped }.map { profile ->
                    launch {
                        val target = getSimplePingTarget(profile)
                        val result = if (target != null) {
                            pingTcp(target.first, target.second)
                        } else {
                            PingResult.Error("No target")
                        }
                        _uiState.value = _uiState.value.copy(
                            pingResults = _uiState.value.pingResults + (profile.id to result)
                        )
                    }
                }
                jobs.forEach { it.join() }
            } finally {
                _uiState.value = _uiState.value.copy(isPingRunning = false)
            }
        }
    }

    private suspend fun getSimplePingTarget(profile: ServerProfile): Pair<String, Int>? {
        // Try direct TCP target first
        getTcpTarget(profile)?.let { return it }
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
        return withContext(Dispatchers.IO) {
            try {
                val socket = Socket()
                val start = System.nanoTime()
                socket.connect(InetSocketAddress(host, port), 5000)
                val elapsed = (System.nanoTime() - start) / 1_000_000
                socket.close()
                PingResult.Success(elapsed)
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
    }

    fun pingSingleProfile(profile: ServerProfile) {
        if (_uiState.value.isPingRunning) return
        startRealTests(listOf(profile))
    }

    fun cancelPing() {
        pingJob?.cancel()
        // Keep the running flag until owned service cleanup has finished.
    }

    fun clearPingResults() {
        _uiState.value = _uiState.value.copy(pingResults = emptyMap(), pingingViaGlobalDns = emptyList(), sortByHttps = false)
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
        if (sorted == current) { _uiState.value = _uiState.value.copy(sortByHttps = true, sortByReliability = reliability); return }
        _uiState.value = _uiState.value.copy(profiles = sorted, sortByHttps = true, sortByReliability = reliability)
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
