package app.slipnet.presentation.scanner

import app.slipnet.presentation.theme.rv
import androidx.compose.ui.res.stringResource
import android.net.Uri
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.SizeTransform
import androidx.compose.animation.togetherWith
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.ClickableText
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Dns
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.FileUpload
import androidx.compose.material.icons.filled.NetworkCheck
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.ShareLocation
import androidx.compose.material.icons.filled.Shuffle
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.hilt.navigation.compose.hiltViewModel
import app.slipnet.domain.model.DnsTransport
import app.slipnet.domain.model.ResolverStatus
import app.slipnet.tunnel.GeoBypassCountry

private val WorkingGreen = Color(0xFF4CAF50)

private enum class ResolverPanel { NONE, COUNTRY, CUSTOM, IR_DNS, IR_DNS_LITE }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DnsScannerScreen(
    profileId: Long? = null,
    onNavigateBack: () -> Unit,
    onNavigateToResults: () -> Unit,
    onResolversSelected: (String, String?) -> Unit,
    viewModel: DnsScannerViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val localizedResources = androidx.compose.ui.platform.LocalResources.current
    val snackbarHostState = remember { SnackbarHostState() }

    BackHandler {
        viewModel.stopAll()
        onNavigateBack()
    }

    val filePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let { viewModel.importList(it) }
    }

    LaunchedEffect(uiState.error, localizedResources) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(scannerErrorText(localizedResources, error))
            viewModel.clearError()
        }
    }

    LaunchedEffect(uiState.scannerState.isScanning, uiState.simpleModeE2eState.isRunning) {
        if (uiState.scannerState.isScanning || uiState.simpleModeE2eState.isRunning) {
            onNavigateToResults()
        }
    }

    // Resume dialog
    if (uiState.showResumeDialog) {
        val ss = uiState.scannerState
        val isSimple = uiState.scanMode == ScanMode.SIMPLE
        val isE2eOnly = uiState.scanMode == ScanMode.E2E
        val scannerResources = androidx.compose.ui.platform.LocalResources.current
        val e2e = uiState.simpleModeE2eState
        val dialogText = buildString {
            if (!isE2eOnly) {
                append(scannerResources.getString(app.slipnet.R.string.scanner_resume_dns,
                    ss.scannedCount, ss.totalCount, ss.focusRangeCount, ss.workingCount))
                append("\n")
            }
            if (isE2eOnly || (isSimple && e2e.testedCount > 0)) {
                append(scannerResources.getString(app.slipnet.R.string.scanner_resume_e2e,
                    e2e.testedCount, e2e.queuedCount, e2e.passedCount))
                append("\n")
            }
            append(scannerResources.getString(app.slipnet.R.string.scanner_resume_question))
        }
        AlertDialog(
            onDismissRequest = { viewModel.dismissResumeDialog() },
            title = { Text(stringResource(app.slipnet.R.string.scanner_resume_title)) },
            text = { Text(dialogText) },
            confirmButton = {
                Button(onClick = { viewModel.resumeScan() }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_continue_31fbef))
                }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.startFreshScan() }) {
                    Text(stringResource(app.slipnet.R.string.scanner_start_fresh))
                }
            }
        )
    }

    // Last scan IPs dialog
    if (uiState.showLastScanIpsDialog) {
        val workingCount = uiState.lastScanWorkingIps.size
        val e2eCount = uiState.lastScanE2ePassedIps.size
        AlertDialog(
            onDismissRequest = { viewModel.dismissLastScanIpsDialog() },
            title = { Text(stringResource(app.slipnet.R.string.scanner_load_previous)) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(stringResource(app.slipnet.R.string.scanner_choose_previous))
                    FilledTonalButton(
                        onClick = { viewModel.loadLastScanWorkingIps() },
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Text(stringResource(app.slipnet.R.string.scanner_counters_working , workingCount))
                    }
                    FilledTonalButton(
                        onClick = { viewModel.loadLastScanE2ePassedIps() },
                        enabled = e2eCount > 0,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Text(stringResource(app.slipnet.R.string.scanner_counters_e2e , e2eCount))
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { viewModel.dismissLastScanIpsDialog() }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    val navBarPadding = WindowInsets.navigationBars.asPaddingValues()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_dns_scanner_a08849)) },
                navigationIcon = {
                    IconButton(onClick = { viewModel.stopAll(); onNavigateBack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(app.slipnet.R.string.rv_back_76900f))
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface
                )
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        contentWindowInsets = WindowInsets(0, 0, 0, 0)
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .imePadding()
                .verticalScroll(rememberScrollState())
                .padding(
                    start = 16.dp,
                    end = 16.dp,
                    top = 8.dp,
                    bottom = 16.dp + navBarPadding.calculateBottomPadding()
                ),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Hero
            HeroCard()

            // Scan Mode Toggle — hide when only Advanced is available (no profile)
            if (uiState.canUseSimpleMode || uiState.canUsePrismMode) {
                ScanModeToggle(
                    scanMode = uiState.scanMode,
                    canUseSimpleMode = uiState.canUseSimpleMode,
                    canUsePrismMode = uiState.canUsePrismMode,
                    isSoarProfile = uiState.isSoarProfile,
                    enabled = !uiState.scannerState.isScanning && !uiState.simpleModeE2eState.isRunning,
                    onModeChange = { viewModel.setScanMode(it) }
                )
            }

            // Start Scan + View Results
            ActionSection(
                canStartScan = uiState.resolverList.isNotEmpty() && !uiState.scannerState.isScanning && !uiState.simpleModeE2eState.isRunning,
                hasResults = uiState.scannerState.results.isNotEmpty(),
                workingCount = when (uiState.scanMode) {
                    ScanMode.SIMPLE, ScanMode.E2E -> uiState.scannerState.results.count { it.e2eTestResult?.success == true }
                    ScanMode.PRISM -> uiState.scannerState.results.count { it.prismVerified == true }
                    else -> uiState.scannerState.results.count {
                        it.status == ResolverStatus.WORKING &&
                            (it.tunnelTestResult?.score ?: 0) >= 1
                    }
                },
                scanMode = uiState.scanMode,
                onStartScan = { viewModel.startScan() },
                onViewResults = onNavigateToResults
            )

            // Configuration
            val isProfileLocked = uiState.profile?.isLocked == true
            ConfigurationSection(
                testDomain = uiState.testDomain,
                isProfileLocked = isProfileLocked,
                scanPort = uiState.scanPort,
                timeoutMs = uiState.timeoutMs,
                concurrency = uiState.concurrency,
                shuffleList = uiState.shuffleList,
                expandNeighbors = uiState.expandNeighbors,
                testUrl = uiState.testUrl,
                e2eTimeoutMs = uiState.e2eTimeoutMs,
                e2eConcurrency = uiState.e2eConcurrency,
                showTestUrl = uiState.profileId != null && uiState.scanMode != ScanMode.PRISM &&
                    (if (uiState.isSoarProfile) uiState.scanMode == ScanMode.E2E else uiState.scanMode != ScanMode.E2E),
                e2eFullVerification = uiState.e2eFullVerification,
                scanMode = uiState.scanMode,
                prismTimeoutMs = uiState.prismTimeoutMs,
                prismProbeCount = uiState.prismProbeCount,
                prismPassThreshold = uiState.prismPassThreshold,
                prismResponseSize = uiState.prismResponseSize,
                prismPrefilter = uiState.prismPrefilter,
                prismPrefilterTimeoutMs = uiState.prismPrefilterTimeoutMs,
                scanTransport = uiState.scanTransport,
                profileDnsTransport = if (uiState.isSoarProfile) null else uiState.profile?.dnsTransport,
                isSoarProfile = uiState.isSoarProfile,
                scanRunning = uiState.scannerState.isScanning || uiState.simpleModeE2eState.isRunning || uiState.e2eScannerState.isRunning,
                onTestDomainChange = { viewModel.updateTestDomain(it) },
                onScanPortChange = { viewModel.updateScanPort(it) },
                onTimeoutChange = { viewModel.updateTimeout(it) },
                onConcurrencyChange = { viewModel.updateConcurrency(it) },
                onShuffleListChange = { viewModel.updateShuffleList(it) },
                onExpandNeighborsChange = { viewModel.updateExpandNeighbors(it) },
                onTestUrlChange = { viewModel.updateTestUrl(it) },
                onE2eTimeoutChange = { viewModel.updateE2eTimeout(it) },
                onE2eConcurrencyChange = { viewModel.updateE2eConcurrency(it) },
                onE2eFullVerificationChange = { viewModel.updateE2eFullVerification(it) },
                onPrismTimeoutChange = { viewModel.updatePrismTimeout(it) },
                onPrismProbeCountChange = { viewModel.updatePrismProbeCount(it) },
                onPrismPassThresholdChange = { viewModel.updatePrismPassThreshold(it) },
                onPrismResponseSizeChange = { viewModel.updatePrismResponseSize(it) },
                onPrismPrefilterChange = { viewModel.updatePrismPrefilter(it) },
                onPrismPrefilterTimeoutChange = { viewModel.updatePrismPrefilterTimeout(it) },
                onResetPrismSettings = { viewModel.resetPrismSettings() },
                onScanTransportChange = { viewModel.updateScanTransport(it) }
            )

            // Resolver List
            ResolverListSection(
                resolverCount = uiState.resolverList.size,
                listSource = uiState.listSource,
                importedFileName = uiState.importedFileName,
                isLoading = uiState.isLoadingList,
                selectedCountry = uiState.selectedCountry,
                sampleCount = uiState.sampleCount,
                effectiveSampleCount = uiState.effectiveSampleCount,
                useCustomSampleCount = uiState.useCustomSampleCount,
                customSampleCountText = uiState.customSampleCountText,
                cidrGroups = uiState.cidrGroups,
                selectedOctets = uiState.selectedOctets,
                countryTotalIps = uiState.countryTotalIps,
                selectedTotalIps = uiState.selectedTotalIps,
                customRangeInput = uiState.customRangeInput,
                customRangePreviewCount = uiState.customRangePreviewCount,
                canStartScan = uiState.resolverList.isNotEmpty() && !uiState.scannerState.isScanning && !uiState.simpleModeE2eState.isRunning,
                onStartScan = { viewModel.startScan() },
                onLoadDefault = { viewModel.loadDefaultList() },
                onImportFile = { filePickerLauncher.launch("text/*") },
                onSelectCountry = { viewModel.updateSelectedCountry(it) },
                onSelectSampleCount = { viewModel.updateSampleCount(it) },
                onUseCustomSampleCount = { viewModel.setUseCustomSampleCount(it) },
                onCustomSampleCountChange = { viewModel.updateCustomSampleCount(it) },
                onGenerateCountryList = { viewModel.loadCountryRangeList() },
                onToggleOctet = { viewModel.toggleOctetGroup(it) },
                onSelectAllOctets = { viewModel.selectAllOctetGroups() },
                onDeselectAllOctets = { viewModel.deselectAllOctetGroups() },
                onCustomRangeInputChange = { viewModel.updateCustomRangeInput(it) },
                onLoadCustomRange = { viewModel.loadCustomRangeList() },
                onLoadIrDnsCidrInfo = { viewModel.loadIrDnsCidrInfo() },
                onLoadIrDnsRange = { viewModel.loadIrDnsRangeList() },
                onLoadIrDnsLiteCidrInfo = { viewModel.loadIrDnsLiteCidrInfo() },
                onLoadIrDnsLiteRange = { viewModel.loadIrDnsLiteRangeList() },
                hasLastScanIps = uiState.hasLastScanIps,
                onLoadLastScanIps = { viewModel.showLastScanIpsDialog() },
                savedPoolSize = uiState.savedPoolSize,
                savedPoolLabel = uiState.savedPoolLabel,
                onLoadSavedPool = { viewModel.loadSavedPool() }
            )

            // Recent DNS
            if (uiState.recentDnsResolvers.isNotEmpty()) {
                val canSelect = uiState.profileId != null
                RecentDnsSection(
                    recentResolvers = uiState.recentDnsResolvers,
                    selectedResolvers = uiState.selectedResolvers,
                    isLimitReached = uiState.isSelectionLimitReached,
                    maxCount = DnsScannerUiState.MAX_SELECTED_RESOLVERS,
                    canSelect = canSelect,
                    onToggleSelection = { viewModel.toggleResolverSelection(it) },
                    onApply = {
                        viewModel.saveRecentDns()
                        val resolvers = viewModel.getSelectedResolversString()
                        val hint = viewModel.getRecommendedTransportHint()
                        viewModel.stopAll()
                        onResolversSelected(resolvers, hint)
                    }
                )
            }
        }
    }
}

@Composable
internal fun HeroCard() {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.primaryContainer
        ),
        shape = RoundedCornerShape(16.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.spacedBy(14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(36.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    Icons.Default.NetworkCheck,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onPrimary,
                    modifier = Modifier.size(20.dp)
                )
            }

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = stringResource(app.slipnet.R.string.scanner_hero_title),
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onPrimaryContainer
                )
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = stringResource(app.slipnet.R.string.scanner_hero_hint),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.8f)
                )
            }
        }
    }
}

@Composable
internal fun ActionSection(
    canStartScan: Boolean,
    hasResults: Boolean,
    workingCount: Int,
    scanMode: ScanMode = ScanMode.ADVANCED,
    onStartScan: () -> Unit,
    onViewResults: () -> Unit
) {
    val buttonLabel = when (scanMode) {
        ScanMode.SIMPLE -> stringResource(app.slipnet.R.string.scanner_start_simple)
        ScanMode.E2E -> stringResource(app.slipnet.R.string.scanner_start_e2e)
        else -> stringResource(app.slipnet.R.string.rv_start_scan_1f5473)
    }
    val resultsLabel = when (scanMode) {
        ScanMode.SIMPLE, ScanMode.E2E -> stringResource(app.slipnet.R.string.scanner_results_passed , workingCount)
        ScanMode.PRISM -> stringResource(app.slipnet.R.string.scanner_results_verified , workingCount)
        else -> stringResource(app.slipnet.R.string.scanner_results_working , workingCount)
    }

    Column(
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Button(
            onClick = onStartScan,
            enabled = canStartScan,
            modifier = Modifier
                .fillMaxWidth()
                .height(48.dp),
            shape = RoundedCornerShape(12.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = MaterialTheme.colorScheme.primary
            )
        ) {
            Icon(
                Icons.Default.PlayArrow,
                contentDescription = null,
                modifier = Modifier.size(20.dp)
            )
            Spacer(Modifier.width(10.dp))
            Text(
                buttonLabel,
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.SemiBold
            )
        }

        AnimatedVisibility(
            visible = hasResults,
            enter = fadeIn() + slideInVertically(),
            exit = fadeOut() + slideOutVertically()
        ) {
            FilledTonalButton(
                onClick = onViewResults,
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp)
            ) {
                Icon(
                    Icons.Default.CheckCircle,
                    contentDescription = null,
                    tint = WorkingGreen,
                    modifier = Modifier.size(18.dp)
                )
                Spacer(Modifier.width(8.dp))
                Text(resultsLabel)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
internal fun ScanModeToggle(
    scanMode: ScanMode,
    canUseSimpleMode: Boolean,
    canUsePrismMode: Boolean = false,
    enabled: Boolean,
    isSoarProfile: Boolean = false,
    onModeChange: (ScanMode) -> Unit
) {
    Column(
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        FlowRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            if (canUseSimpleMode) {
                FilterChip(
                    selected = scanMode == ScanMode.SIMPLE,
                    onClick = { onModeChange(ScanMode.SIMPLE) },
                    enabled = enabled,
                    label = { Text(stringResource(app.slipnet.R.string.scanner_simple)) },
                    leadingIcon = if (scanMode == ScanMode.SIMPLE) {
                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                    } else null,
                    colors = FilterChipDefaults.filterChipColors(
                        selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                        selectedLabelColor = MaterialTheme.colorScheme.primary,
                        selectedLeadingIconColor = MaterialTheme.colorScheme.primary
                    )
                )
            }
            FilterChip(
                selected = scanMode == ScanMode.ADVANCED,
                onClick = { onModeChange(ScanMode.ADVANCED) },
                enabled = enabled,
                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_advanced_9f088d)) },
                leadingIcon = if (scanMode == ScanMode.ADVANCED) {
                    { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                } else null,
                colors = FilterChipDefaults.filterChipColors(
                    selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                    selectedLabelColor = MaterialTheme.colorScheme.primary,
                    selectedLeadingIconColor = MaterialTheme.colorScheme.primary
                )
            )
            if (canUseSimpleMode) {
                FilterChip(
                    selected = scanMode == ScanMode.E2E,
                    onClick = { onModeChange(ScanMode.E2E) },
                    enabled = enabled,
                    label = { Text(stringResource(app.slipnet.R.string.scanner_e2e)) },
                    leadingIcon = if (scanMode == ScanMode.E2E) {
                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                    } else null,
                    colors = FilterChipDefaults.filterChipColors(
                        selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                        selectedLabelColor = MaterialTheme.colorScheme.primary,
                        selectedLeadingIconColor = MaterialTheme.colorScheme.primary
                    )
                )
            }
            if (canUsePrismMode) {
                FilterChip(
                    selected = scanMode == ScanMode.PRISM,
                    onClick = { onModeChange(ScanMode.PRISM) },
                    enabled = enabled,
                    label = { Text("Prism") },
                    leadingIcon = if (scanMode == ScanMode.PRISM) {
                        { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                    } else null,
                    colors = FilterChipDefaults.filterChipColors(
                        selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                        selectedLabelColor = MaterialTheme.colorScheme.primary,
                        selectedLeadingIconColor = MaterialTheme.colorScheme.primary
                    )
                )
            }
        }

        if (scanMode == ScanMode.PRISM && !isSoarProfile) {
            val uriHandler = LocalUriHandler.current
            val linkColor = MaterialTheme.colorScheme.primary
            val textColor = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
            val textStyle = MaterialTheme.typography.bodySmall
            val verificationIntro = androidx.compose.ui.res.stringResource(app.slipnet.R.string.prism_server_verification_intro)
            val verificationSuffix = androidx.compose.ui.res.stringResource(app.slipnet.R.string.prism_server_verification_suffix)
            val annotated = buildAnnotatedString {
                withStyle(SpanStyle(color = textColor, fontSize = textStyle.fontSize)) {
                    append(verificationIntro)
                }
                pushStringAnnotation(tag = "URL", annotation = "https://github.com/msVPNco/rovunet-panel")
                withStyle(SpanStyle(color = linkColor, fontSize = textStyle.fontSize)) {
                    append("Rovunet Panel")
                }
                pop()
                withStyle(SpanStyle(color = textColor, fontSize = textStyle.fontSize)) {
                    append(verificationSuffix)
                }
            }
            ClickableText(
                text = annotated,
                style = textStyle,
                onClick = { offset ->
                    annotated.getStringAnnotations(tag = "URL", start = offset, end = offset)
                        .firstOrNull()?.let { uriHandler.openUri(it.item) }
                }
            )
        } else {
            Text(
                text = if (isSoarProfile) stringResource(when (scanMode) {
                    ScanMode.SIMPLE -> app.slipnet.R.string.scanner_soar_simple_hint
                    ScanMode.E2E -> app.slipnet.R.string.scanner_soar_e2e_hint
                    ScanMode.PRISM -> app.slipnet.R.string.scanner_soar_prism_hint
                    ScanMode.ADVANCED -> app.slipnet.R.string.scanner_soar_advanced_hint
                }) else when (scanMode) {
                    ScanMode.SIMPLE -> stringResource(app.slipnet.R.string.scanner_simple_hint)
                    ScanMode.E2E -> stringResource(app.slipnet.R.string.scanner_e2e_hint)
                    else -> stringResource(app.slipnet.R.string.scanner_advanced_hint)
                },
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
            )
        }
    }
}

@Composable
internal fun ConfigurationSection(
    testDomain: String,
    isProfileLocked: Boolean = false,
    scanPort: String,
    timeoutMs: String,
    concurrency: String,
    shuffleList: Boolean = false,
    expandNeighbors: Boolean = true,
    testUrl: String = "",
    e2eTimeoutMs: String = "15000",
    e2eConcurrency: String = "6",
    showTestUrl: Boolean = false,
    e2eFullVerification: Boolean = false,
    scanMode: ScanMode = ScanMode.ADVANCED,
    prismTimeoutMs: String = "2000",
    prismProbeCount: String = "5",
    prismPassThreshold: String = "2",
    prismResponseSize: String = "0",
    prismPrefilter: Boolean = false,
    prismPrefilterTimeoutMs: String = "1500",
    scanTransport: ScanTransportMode = ScanTransportMode.UDP,
    profileDnsTransport: DnsTransport? = null,
    isSoarProfile: Boolean = false,
    scanRunning: Boolean = false,
    onTestDomainChange: (String) -> Unit,
    onScanPortChange: (String) -> Unit,
    onTimeoutChange: (String) -> Unit,
    onConcurrencyChange: (String) -> Unit,
    onShuffleListChange: (Boolean) -> Unit = {},
    onExpandNeighborsChange: (Boolean) -> Unit = {},
    onTestUrlChange: (String) -> Unit = {},
    onE2eTimeoutChange: (String) -> Unit = {},
    onE2eConcurrencyChange: (String) -> Unit = {},
    onE2eFullVerificationChange: (Boolean) -> Unit = {},
    onPrismTimeoutChange: (String) -> Unit = {},
    onPrismProbeCountChange: (String) -> Unit = {},
    onPrismPassThresholdChange: (String) -> Unit = {},
    onPrismResponseSizeChange: (String) -> Unit = {},
    onPrismPrefilterChange: (Boolean) -> Unit = {},
    onPrismPrefilterTimeoutChange: (String) -> Unit = {},
    onResetPrismSettings: () -> Unit = {},
    onScanTransportChange: (ScanTransportMode) -> Unit = {}
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerLow
        ),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            SectionHeader(
                icon = Icons.Default.Settings,
                title = stringResource(app.slipnet.R.string.scanner_configuration)
            )

            // Prism/E2E modes use the profile's tunnel domain — hide the editable field.
            if (scanMode != ScanMode.PRISM && scanMode != ScanMode.E2E && (!isSoarProfile || scanMode == ScanMode.ADVANCED)) {
                OutlinedTextField(
                    value = testDomain,
                    onValueChange = onTestDomainChange,
                    label = { Text(stringResource(app.slipnet.R.string.scanner_test_domain)) },
                    placeholder = {
                        Text(if (isProfileLocked) stringResource(app.slipnet.R.string.scanner_profile_domain) else "google.com")
                    },
                    leadingIcon = {
                        Icon(
                            Icons.Default.Search,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                    },
                    supportingText = {
                        Text(
                            if (isProfileLocked && testDomain.isBlank()) stringResource(app.slipnet.R.string.scanner_override_domain)
                            else stringResource(app.slipnet.R.string.scanner_test_domain_hint)
                        )
                    },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )
            }

            if (scanMode == ScanMode.E2E) {
                // E2E mode: only port, E2E timeout, E2E concurrency
                OutlinedTextField(
                    value = scanPort,
                    onValueChange = onScanPortChange,
                    label = { Text(stringResource(app.slipnet.R.string.scanner_resolver_port)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedTextField(
                        value = e2eTimeoutMs,
                        onValueChange = { onE2eTimeoutChange(it.filter { c -> c.isDigit() }) },
                        label = { Text(stringResource(app.slipnet.R.string.scanner_e2e_timeout)) },
                        suffix = { Text(stringResource(app.slipnet.R.string.scanner_ms)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_timeout_per_resolver)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )

                    OutlinedTextField(
                        value = e2eConcurrency,
                        onValueChange = { onE2eConcurrencyChange(it.filter { c -> c.isDigit() }.take(2)) },
                        label = { Text(stringResource(app.slipnet.R.string.scanner_workers)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_worker_limit)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )
                }

                if (!isSoarProfile) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_http_ssh_verification),
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_verification_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = e2eFullVerification,
                        onCheckedChange = onE2eFullVerificationChange
                    )
                }
                }
                if (e2eFullVerification) {
                    OutlinedTextField(
                        value = testUrl,
                        onValueChange = onTestUrlChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_test_url)) },
                        placeholder = { Text("http://www.gstatic.com/generate_204") },
                        leadingIcon = {
                            Icon(
                                Icons.Default.NetworkCheck,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp)
                            )
                        },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_test_url_hint)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    )
                }
            } else {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedTextField(
                        value = scanPort,
                        onValueChange = onScanPortChange,
                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_port_72e9a5)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(0.7f),
                        shape = RoundedCornerShape(12.dp)
                    )

                    if (scanMode != ScanMode.PRISM) {
                        OutlinedTextField(
                            value = timeoutMs,
                            onValueChange = onTimeoutChange,
                            label = { Text(stringResource(app.slipnet.R.string.scanner_timeout)) },
                            suffix = { Text(stringResource(app.slipnet.R.string.scanner_ms)) },
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            singleLine = true,
                            modifier = Modifier.weight(1f),
                            shape = RoundedCornerShape(12.dp)
                        )
                    }

                    OutlinedTextField(
                        value = concurrency,
                        onValueChange = onConcurrencyChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_workers)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(0.8f),
                        shape = RoundedCornerShape(12.dp)
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = stringResource(app.slipnet.R.string.scanner_transport),
                        style = MaterialTheme.typography.bodyMedium
                    )
                    ScanTransportMode.entries.forEach { mode ->
                        FilterChip(
                            selected = scanTransport == mode,
                            enabled = !scanRunning,
                            onClick = { onScanTransportChange(mode) },
                            label = { Text(scannerTransportName(mode.displayName)) },
                            leadingIcon = if (scanTransport == mode) {
                                { Icon(Icons.Default.Check, contentDescription = null, modifier = Modifier.size(16.dp)) }
                            } else null,
                            colors = FilterChipDefaults.filterChipColors(
                                selectedContainerColor = MaterialTheme.colorScheme.primaryContainer,
                                selectedLabelColor = MaterialTheme.colorScheme.primary,
                                selectedLeadingIconColor = MaterialTheme.colorScheme.primary
                            )
                        )
                    }
                }

                val profileTcpBased = profileDnsTransport != null && profileDnsTransport != DnsTransport.UDP
                val scanTcpBased = scanTransport == ScanTransportMode.TCP
                val transportMismatch = scanTransport != ScanTransportMode.BOTH &&
                    profileDnsTransport != null && scanTcpBased != profileTcpBased
                AnimatedVisibility(visible = transportMismatch) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            Icons.Default.Warning,
                            contentDescription = null,
                            modifier = Modifier.size(14.dp),
                            tint = MaterialTheme.colorScheme.tertiary
                        )
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_transport_hint , profileDnsTransport?.displayName.orEmpty(), scannerTransportName(scanTransport.displayName)),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.tertiary
                        )
                    }
                }
            }

            if (scanMode == ScanMode.PRISM) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    OutlinedTextField(
                        value = prismProbeCount,
                        onValueChange = onPrismProbeCountChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_probes)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_requests_per_resolver)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )

                    OutlinedTextField(
                        value = prismPassThreshold,
                        onValueChange = onPrismPassThresholdChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_pass_threshold)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_min_to_pass)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )
                }

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    val rawPerProbe = (prismTimeoutMs.toLongOrNull() ?: 2000L) / (prismPassThreshold.toIntOrNull()?.coerceAtLeast(1) ?: 2)
                    val perProbeTooLow = rawPerProbe < 200
                    OutlinedTextField(
                        value = prismTimeoutMs,
                        onValueChange = { onPrismTimeoutChange(it.filter { c -> c.isDigit() }) },
                        label = { Text(stringResource(app.slipnet.R.string.scanner_resolver_timeout)) },
                        suffix = { Text(stringResource(app.slipnet.R.string.scanner_ms)) },
                        isError = perProbeTooLow,
                        supportingText = {
                            if (perProbeTooLow) {
                                Text(stringResource(app.slipnet.R.string.scanner_probe_ms_min , rawPerProbe))
                            } else {
                                Text(stringResource(app.slipnet.R.string.scanner_probe_ms , rawPerProbe))
                            }
                        },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )

                    if (!isSoarProfile) {
                    OutlinedTextField(
                        value = prismResponseSize,
                        onValueChange = onPrismResponseSizeChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_response_size)) },
                        suffix = { Text(stringResource(app.slipnet.R.string.scanner_byte)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_server_default)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.weight(1f),
                        shape = RoundedCornerShape(12.dp)
                    )
                    }
                }

                if (!isSoarProfile) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(stringResource(app.slipnet.R.string.scanner_prefilter), style = MaterialTheme.typography.bodyMedium)
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_prefilter_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = prismPrefilter,
                        onCheckedChange = onPrismPrefilterChange
                    )
                }

                if (prismPrefilter) {
                    OutlinedTextField(
                        value = prismPrefilterTimeoutMs,
                        onValueChange = { onPrismPrefilterTimeoutChange(it.filter { c -> c.isDigit() }) },
                        label = { Text(stringResource(app.slipnet.R.string.scanner_prefilter_timeout)) },
                        suffix = { Text(stringResource(app.slipnet.R.string.scanner_ms)) },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_min_500)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    )
                }

                }

                OutlinedTextField(
                    value = e2eTimeoutMs,
                    onValueChange = { onE2eTimeoutChange(it.filter { c -> c.isDigit() }) },
                    label = { Text(stringResource(app.slipnet.R.string.scanner_e2e_timeout)) },
                    suffix = { Text(stringResource(app.slipnet.R.string.scanner_ms)) },
                    supportingText = { Text(stringResource(app.slipnet.R.string.scanner_tunnel_timeout_hint)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )

                TextButton(
                    onClick = onResetPrismSettings,
                    modifier = Modifier.align(Alignment.End)
                ) {
                    Text(stringResource(app.slipnet.R.string.scanner_reset_defaults))
                }
            }

            // DNS-specific options — hidden in E2E mode since there's no DNS scan phase
            if (scanMode != ScanMode.E2E) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            Icons.Default.Shuffle,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_shuffle),
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                    Switch(
                        checked = shuffleList,
                        onCheckedChange = onShuffleListChange
                    )
                }

                // Expand neighbors toggle
                Column {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                Icons.Default.ShareLocation,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Text(
                                text = stringResource(app.slipnet.R.string.scanner_nearby),
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                        Switch(
                            checked = expandNeighbors,
                            onCheckedChange = onExpandNeighborsChange
                        )
                    }
                    Text(
                        text = stringResource(app.slipnet.R.string.scanner_nearby_hint),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            if (showTestUrl) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_http_ssh_verification),
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Text(
                            text = stringResource(app.slipnet.R.string.scanner_verification_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Switch(
                        checked = e2eFullVerification,
                        onCheckedChange = onE2eFullVerificationChange
                    )
                }
                if (e2eFullVerification) {
                    OutlinedTextField(
                        value = testUrl,
                        onValueChange = onTestUrlChange,
                        label = { Text(stringResource(app.slipnet.R.string.scanner_test_url)) },
                        placeholder = { Text("http://www.gstatic.com/generate_204") },
                        leadingIcon = {
                            Icon(
                                Icons.Default.NetworkCheck,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp)
                            )
                        },
                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_test_url_hint)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    )
                }
            }

            // E2E timeout and concurrency — shown for both E2E scan and Prism modes
            if (showTestUrl || scanMode == ScanMode.PRISM) {
                OutlinedTextField(
                    value = e2eTimeoutMs,
                    onValueChange = { onE2eTimeoutChange(it.filter { c -> c.isDigit() }) },
                    label = { Text(stringResource(app.slipnet.R.string.scanner_e2e_timeout_ms)) },
                    placeholder = { Text("10000") },
                    leadingIcon = {
                        Icon(
                            Icons.Default.Schedule,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                    },
                    supportingText = { Text(stringResource(app.slipnet.R.string.scanner_tunnel_timeout_hint)) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )
                OutlinedTextField(
                    value = e2eConcurrency,
                    onValueChange = { onE2eConcurrencyChange(it.filter { c -> c.isDigit() }.take(2)) },
                    label = { Text(stringResource(app.slipnet.R.string.scanner_e2e_concurrency)) },
                    placeholder = { Text("6") },
                    leadingIcon = {
                        Icon(
                            Icons.Default.Speed,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp)
                        )
                    },
                    supportingText = { Text(stringResource(app.slipnet.R.string.scanner_parallel_limit)) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                )
            }

        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun ResolverListSection(
    resolverCount: Int,
    listSource: ListSource,
    importedFileName: String?,
    isLoading: Boolean,
    selectedCountry: GeoBypassCountry,
    sampleCount: Int,
    effectiveSampleCount: Int,
    useCustomSampleCount: Boolean,
    customSampleCountText: String,
    cidrGroups: List<CidrGroup>,
    selectedOctets: Set<Int>,
    countryTotalIps: Long,
    selectedTotalIps: Long,
    customRangeInput: String,
    customRangePreviewCount: Long = 0,
    canStartScan: Boolean,
    onStartScan: () -> Unit,
    onLoadDefault: () -> Unit,
    onImportFile: () -> Unit,
    onSelectCountry: (GeoBypassCountry) -> Unit,
    onSelectSampleCount: (Int) -> Unit,
    onUseCustomSampleCount: (Boolean) -> Unit,
    onCustomSampleCountChange: (String) -> Unit,
    onGenerateCountryList: () -> Unit,
    onToggleOctet: (Int) -> Unit,
    onSelectAllOctets: () -> Unit,
    onDeselectAllOctets: () -> Unit,
    onCustomRangeInputChange: (String) -> Unit,
    onLoadCustomRange: () -> Unit,
    onLoadIrDnsCidrInfo: () -> Unit,
    onLoadIrDnsRange: () -> Unit,
    onLoadIrDnsLiteCidrInfo: () -> Unit,
    onLoadIrDnsLiteRange: () -> Unit,
    hasLastScanIps: Boolean = false,
    onLoadLastScanIps: () -> Unit = {},
    savedPoolSize: Int = 0,
    savedPoolLabel: String? = null,
    onLoadSavedPool: () -> Unit = {}
) {
    var activePanel by remember {
        mutableStateOf(
            when (listSource) {
                ListSource.COUNTRY_RANGE -> ResolverPanel.COUNTRY
                ListSource.CUSTOM_RANGE -> ResolverPanel.CUSTOM
                ListSource.IR_DNS_RANGE -> ResolverPanel.IR_DNS
                ListSource.IR_DNS_LITE_RANGE -> ResolverPanel.IR_DNS_LITE
                else -> ResolverPanel.NONE
            }
        )
    }

    // Sync panel when listSource changes after async restore
    LaunchedEffect(listSource) {
        val expected = when (listSource) {
            ListSource.COUNTRY_RANGE -> ResolverPanel.COUNTRY
            ListSource.CUSTOM_RANGE -> ResolverPanel.CUSTOM
            ListSource.IR_DNS_RANGE -> ResolverPanel.IR_DNS
            ListSource.IR_DNS_LITE_RANGE -> ResolverPanel.IR_DNS_LITE
            else -> null
        }
        if (expected != null && activePanel == ResolverPanel.NONE) {
            activePanel = expected
            if (expected == ResolverPanel.IR_DNS) onLoadIrDnsCidrInfo()
            if (expected == ResolverPanel.IR_DNS_LITE) onLoadIrDnsLiteCidrInfo()
        }
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerLow
        ),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            SectionHeader(
                icon = Icons.Default.Storage,
                title = stringResource(app.slipnet.R.string.scanner_resolver_list)
            )

            // Resolver count row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .clip(RoundedCornerShape(10.dp))
                        .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)),
                    contentAlignment = Alignment.Center
                ) {
                    if (isLoading) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(18.dp),
                            strokeWidth = 2.dp
                        )
                    } else {
                        Icon(
                            Icons.Default.Dns,
                            contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }

                Column {
                    Text(
                        text = stringResource(app.slipnet.R.string.scanner_resolver_count , resolverCount),
                        style = MaterialTheme.typography.bodyLarge,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(
                        text = when (listSource) {
                            ListSource.DEFAULT -> stringResource(app.slipnet.R.string.scanner_builtin)
                            ListSource.IMPORTED -> if (importedFileName != null) stringResource(app.slipnet.R.string.scanner_imported_name , importedFileName.orEmpty()) else stringResource(app.slipnet.R.string.scanner_imported_file)
                            ListSource.COUNTRY_RANGE -> stringResource(app.slipnet.R.string.scanner_country_sample , scannerCountryName(selectedCountry.code), effectiveSampleCount)
                            ListSource.CUSTOM_RANGE -> stringResource(app.slipnet.R.string.scanner_custom_sample , resolverCount)
                            ListSource.IR_DNS_RANGE -> stringResource(app.slipnet.R.string.scanner_iran_sample , effectiveSampleCount)
                            ListSource.IR_DNS_LITE_RANGE -> stringResource(app.slipnet.R.string.scanner_iran_lite_sample , effectiveSampleCount)
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(
                    onClick = {
                        activePanel = ResolverPanel.NONE
                        onLoadDefault()
                    },
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    contentPadding = PaddingValues(vertical = 10.dp)
                ) {
                    Icon(
                        Icons.Default.Refresh,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(stringResource(app.slipnet.R.string.scanner_default), maxLines = 1)
                }

                OutlinedButton(
                    onClick = {
                        activePanel = ResolverPanel.NONE
                        onImportFile()
                    },
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    contentPadding = PaddingValues(vertical = 10.dp)
                ) {
                    Icon(
                        Icons.Default.FileUpload,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_2cff9b), maxLines = 1)
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                OutlinedButton(
                    onClick = {
                        activePanel = if (activePanel == ResolverPanel.COUNTRY) ResolverPanel.NONE else ResolverPanel.COUNTRY
                    },
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    contentPadding = PaddingValues(vertical = 10.dp),
                    colors = if (activePanel == ResolverPanel.COUNTRY || listSource == ListSource.COUNTRY_RANGE) {
                        ButtonDefaults.outlinedButtonColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer
                        )
                    } else {
                        ButtonDefaults.outlinedButtonColors()
                    }
                ) {
                    Icon(
                        Icons.Default.Search,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(stringResource(app.slipnet.R.string.scanner_country), maxLines = 1)
                }

                OutlinedButton(
                    onClick = {
                        activePanel = if (activePanel == ResolverPanel.CUSTOM) ResolverPanel.NONE else ResolverPanel.CUSTOM
                    },
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(12.dp),
                    contentPadding = PaddingValues(vertical = 10.dp),
                    colors = if (activePanel == ResolverPanel.CUSTOM || listSource == ListSource.CUSTOM_RANGE) {
                        ButtonDefaults.outlinedButtonColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer
                        )
                    } else {
                        ButtonDefaults.outlinedButtonColors()
                    }
                ) {
                    Icon(
                        Icons.Default.Dns,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp)
                    )
                    Spacer(Modifier.width(6.dp))
                    Text(stringResource(app.slipnet.R.string.scanner_custom), maxLines = 1)
                }
            }

            OutlinedButton(
                onClick = {
                    val newPanel = if (activePanel == ResolverPanel.IR_DNS) ResolverPanel.NONE else ResolverPanel.IR_DNS
                    activePanel = newPanel
                    if (newPanel == ResolverPanel.IR_DNS) onLoadIrDnsCidrInfo()
                },
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                contentPadding = PaddingValues(vertical = 10.dp),
                colors = if (activePanel == ResolverPanel.IR_DNS || listSource == ListSource.IR_DNS_RANGE) {
                    ButtonDefaults.outlinedButtonColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer
                    )
                } else {
                    ButtonDefaults.outlinedButtonColors()
                }
            ) {
                Icon(
                    Icons.Default.ShareLocation,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp)
                )
                Spacer(Modifier.width(6.dp))
                Text(stringResource(app.slipnet.R.string.scanner_iran_ranges), maxLines = 1)
            }

            OutlinedButton(
                onClick = {
                    val newPanel = if (activePanel == ResolverPanel.IR_DNS_LITE) ResolverPanel.NONE else ResolverPanel.IR_DNS_LITE
                    activePanel = newPanel
                    if (newPanel == ResolverPanel.IR_DNS_LITE) onLoadIrDnsLiteCidrInfo()
                },
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                contentPadding = PaddingValues(vertical = 10.dp),
                colors = if (activePanel == ResolverPanel.IR_DNS_LITE || listSource == ListSource.IR_DNS_LITE_RANGE) {
                    ButtonDefaults.outlinedButtonColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer
                    )
                } else {
                    ButtonDefaults.outlinedButtonColors()
                }
            ) {
                Icon(
                    Icons.Default.ShareLocation,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp)
                )
                Spacer(Modifier.width(6.dp))
                Text(stringResource(app.slipnet.R.string.scanner_iran_lite), maxLines = 1)
            }

            // Country / Custom / IR DNS range panel (single AnimatedContent to avoid double-layout)
            AnimatedContent(
                targetState = activePanel,
                transitionSpec = {
                    fadeIn(tween(250)) togetherWith fadeOut(tween(150)) using
                            SizeTransform(clip = false, sizeAnimationSpec = { _, _ -> tween(300) })
                },
                label = "resolverPanel"
            ) { panel ->
                when (panel) {
                    ResolverPanel.CUSTOM -> {
                        Column(
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            OutlinedTextField(
                                value = customRangeInput,
                                onValueChange = onCustomRangeInputChange,
                                label = { Text(stringResource(app.slipnet.R.string.scanner_ip_ranges)) },
                                placeholder = { Text("8.8.8.0/24\n1.1.1.1-1.1.1.10\n9.9.9.9\n1.2.3.4:53,5.6.7.8:53") },
                                supportingText = {
                                    Text(
                                        if (customRangePreviewCount > 0) stringResource(app.slipnet.R.string.scanner_range_preview , customRangePreviewCount)
                                        else stringResource(app.slipnet.R.string.scanner_range_hint)
                                    )
                                },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(min = 140.dp, max = 320.dp),
                                shape = RoundedCornerShape(12.dp),
                                maxLines = 50
                            )

                            FilledTonalButton(
                                onClick = onLoadCustomRange,
                                enabled = !isLoading && customRangeInput.isNotBlank(),
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp)
                            ) {
                                if (isLoading) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(18.dp),
                                        strokeWidth = 2.dp
                                    )
                                } else {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                Text(stringResource(app.slipnet.R.string.scanner_load_ips))
                            }

                            AnimatedVisibility(
                                visible = listSource == ListSource.CUSTOM_RANGE && !isLoading && resolverCount > 0
                            ) {
                                Button(
                                    onClick = onStartScan,
                                    enabled = canStartScan,
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(12.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_start_scan_1f5473), fontWeight = FontWeight.SemiBold)
                                }
                            }
                        }
                    }

                    ResolverPanel.COUNTRY -> {
                        Column(
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            // Country selector
                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Text(
                                    text = stringResource(app.slipnet.R.string.scanner_country),
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                FlowRow(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    GeoBypassCountry.entries.forEach { country ->
                                        OptionChip(
                                            selected = selectedCountry == country,
                                            onClick = { onSelectCountry(country) },
                                            label = scannerCountryName(country.code)
                                        )
                                    }
                                }
                            }

                            // Stats row
                            if (cidrGroups.isNotEmpty()) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .background(
                                            MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                                            RoundedCornerShape(8.dp)
                                        )
                                        .padding(horizontal = 12.dp, vertical = 8.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Column {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_range_count , cidrGroups.sumOf { it.rangeCount }),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_total_ips , countryTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                    Column(horizontalAlignment = Alignment.End) {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_groups_count , selectedOctets.size, cidrGroups.size),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_selected_ips , selectedTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                            }

                            // Sample count selector
                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Text(
                                    text = stringResource(app.slipnet.R.string.scanner_sample_size),
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                FlowRow(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    listOf(1000, 2000, 5000, 10000).forEach { count ->
                                        OptionChip(
                                            selected = !useCustomSampleCount && sampleCount == count,
                                            onClick = { onSelectSampleCount(count) },
                                            label = count.toString()
                                        )
                                    }
                                    OptionChip(
                                        selected = useCustomSampleCount,
                                        onClick = { onUseCustomSampleCount(true) },
                                        label = stringResource(app.slipnet.R.string.scanner_custom)
                                    )
                                }
                                AnimatedVisibility(visible = useCustomSampleCount) {
                                    OutlinedTextField(
                                        value = customSampleCountText,
                                        onValueChange = onCustomSampleCountChange,
                                        label = { Text(stringResource(app.slipnet.R.string.scanner_count)) },
                                        placeholder = { Text(stringResource(app.slipnet.R.string.scanner_example_count)) },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp),
                                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_count_bounds , DnsScannerUiState.MAX_SAMPLE_COUNT)) },
                                        trailingIcon = if (selectedTotalIps > 0) {
                                            {
                                                Text(
                                                    text = stringResource(app.slipnet.R.string.scanner_max),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary,
                                                    modifier = Modifier
                                                        .clickable {
                                                            onCustomSampleCountChange(
                                                                selectedTotalIps.coerceAtMost(DnsScannerUiState.MAX_SAMPLE_COUNT.toLong()).toString()
                                                            )
                                                        }
                                                        .padding(end = 12.dp)
                                                )
                                            }
                                        } else null
                                    )
                                }
                            }

                            // Range browser (collapsed by default)
                            if (cidrGroups.isNotEmpty()) {
                                var showRangeGroups by remember { mutableStateOf(false) }
                                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .clip(RoundedCornerShape(8.dp))
                                            .clickable { showRangeGroups = !showRangeGroups }
                                            .padding(vertical = 4.dp),
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Row(
                                            horizontalArrangement = Arrangement.spacedBy(6.dp),
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Icon(
                                                if (showRangeGroups) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                                                contentDescription = null,
                                                modifier = Modifier.size(18.dp),
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_range_groups , selectedOctets.size, cidrGroups.size),
                                                style = MaterialTheme.typography.labelMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_all),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onSelectAllOctets() }
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_none),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onDeselectAllOctets() }
                                            )
                                        }
                                    }

                                    AnimatedVisibility(visible = showRangeGroups) {
                                        Box(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .height(200.dp)
                                                .border(
                                                    1.dp,
                                                    MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
                                                    RoundedCornerShape(12.dp)
                                                )
                                                .clip(RoundedCornerShape(12.dp))
                                        ) {
                                            LazyColumn(
                                                modifier = Modifier.fillMaxSize(),
                                                contentPadding = PaddingValues(4.dp)
                                            ) {
                                                items(cidrGroups.size) { index ->
                                                    val group = cidrGroups[index]
                                                    val isSelected = group.firstOctet in selectedOctets
                                                    Row(
                                                        modifier = Modifier
                                                            .fillMaxWidth()
                                                            .clickable { onToggleOctet(group.firstOctet) }
                                                            .padding(horizontal = 8.dp, vertical = 4.dp),
                                                        verticalAlignment = Alignment.CenterVertically,
                                                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                                                    ) {
                                                        Checkbox(
                                                            checked = isSelected,
                                                            onCheckedChange = { onToggleOctet(group.firstOctet) },
                                                            modifier = Modifier.size(20.dp)
                                                        )
                                                        Text(
                                                            text = group.label,
                                                            style = MaterialTheme.typography.bodyMedium,
                                                            fontWeight = FontWeight.Medium,
                                                            modifier = Modifier.width(80.dp)
                                                        )
                                                        Text(
                                                            text = stringResource(app.slipnet.R.string.scanner_range_count , group.rangeCount),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                            modifier = Modifier.weight(1f)
                                                        )
                                                        Text(
                                                            text = "%,d".format(group.totalIps),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                                        )
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Generate button
                            FilledTonalButton(
                                onClick = onGenerateCountryList,
                                enabled = !isLoading && selectedOctets.isNotEmpty(),
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp)
                            ) {
                                if (isLoading) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(18.dp),
                                        strokeWidth = 2.dp
                                    )
                                } else {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                Text(stringResource(app.slipnet.R.string.scanner_generate_country , effectiveSampleCount, scannerCountryName(selectedCountry.code)))
                            }

                            // Start Scan button after generation
                            AnimatedVisibility(
                                visible = listSource == ListSource.COUNTRY_RANGE && !isLoading && resolverCount > 0
                            ) {
                                Button(
                                    onClick = onStartScan,
                                    enabled = canStartScan,
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(12.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_start_scan_1f5473), fontWeight = FontWeight.SemiBold)
                                }
                            }
                        }
                    }

                    ResolverPanel.IR_DNS -> {
                        Column(
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            // Stats row
                            if (cidrGroups.isNotEmpty()) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .background(
                                            MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                                            RoundedCornerShape(8.dp)
                                        )
                                        .padding(horizontal = 12.dp, vertical = 8.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Column {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_range_count , cidrGroups.sumOf { it.rangeCount }),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_total_ips , countryTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                    Column(horizontalAlignment = Alignment.End) {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_groups_count , selectedOctets.size, cidrGroups.size),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_selected_ips , selectedTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                            }

                            // Range browser
                            if (cidrGroups.isNotEmpty()) {
                                var showRangeGroups by remember { mutableStateOf(false) }
                                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .clip(RoundedCornerShape(8.dp))
                                            .clickable { showRangeGroups = !showRangeGroups }
                                            .padding(vertical = 4.dp),
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Row(
                                            horizontalArrangement = Arrangement.spacedBy(6.dp),
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Icon(
                                                if (showRangeGroups) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                                                contentDescription = null,
                                                modifier = Modifier.size(18.dp),
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_range_groups , selectedOctets.size, cidrGroups.size),
                                                style = MaterialTheme.typography.labelMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_all),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onSelectAllOctets() }
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_none),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onDeselectAllOctets() }
                                            )
                                        }
                                    }

                                    AnimatedVisibility(visible = showRangeGroups) {
                                        Box(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .height(200.dp)
                                                .border(
                                                    1.dp,
                                                    MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
                                                    RoundedCornerShape(12.dp)
                                                )
                                                .clip(RoundedCornerShape(12.dp))
                                        ) {
                                            LazyColumn(
                                                modifier = Modifier.fillMaxSize(),
                                                contentPadding = PaddingValues(4.dp)
                                            ) {
                                                items(cidrGroups.size) { index ->
                                                    val group = cidrGroups[index]
                                                    val isSelected = group.firstOctet in selectedOctets
                                                    Row(
                                                        modifier = Modifier
                                                            .fillMaxWidth()
                                                            .clickable { onToggleOctet(group.firstOctet) }
                                                            .padding(horizontal = 8.dp, vertical = 4.dp),
                                                        verticalAlignment = Alignment.CenterVertically,
                                                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                                                    ) {
                                                        Checkbox(
                                                            checked = isSelected,
                                                            onCheckedChange = { onToggleOctet(group.firstOctet) },
                                                            modifier = Modifier.size(20.dp)
                                                        )
                                                        Text(
                                                            text = group.label,
                                                            style = MaterialTheme.typography.bodyMedium,
                                                            fontWeight = FontWeight.Medium,
                                                            modifier = Modifier.width(80.dp)
                                                        )
                                                        Text(
                                                            text = stringResource(app.slipnet.R.string.scanner_range_count , group.rangeCount),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                            modifier = Modifier.weight(1f)
                                                        )
                                                        Text(
                                                            text = "%,d".format(group.totalIps),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                                        )
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Sample count selector
                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Text(
                                    text = stringResource(app.slipnet.R.string.scanner_sample_size),
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                FlowRow(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    listOf(1000, 2000, 5000, 10000).forEach { count ->
                                        OptionChip(
                                            selected = !useCustomSampleCount && sampleCount == count,
                                            onClick = { onSelectSampleCount(count) },
                                            label = count.toString()
                                        )
                                    }
                                    OptionChip(
                                        selected = useCustomSampleCount,
                                        onClick = { onUseCustomSampleCount(true) },
                                        label = stringResource(app.slipnet.R.string.scanner_custom)
                                    )
                                }
                                AnimatedVisibility(visible = useCustomSampleCount) {
                                    OutlinedTextField(
                                        value = customSampleCountText,
                                        onValueChange = onCustomSampleCountChange,
                                        label = { Text(stringResource(app.slipnet.R.string.scanner_count)) },
                                        placeholder = { Text(stringResource(app.slipnet.R.string.scanner_example_count)) },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp),
                                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_count_bounds , DnsScannerUiState.MAX_SAMPLE_COUNT)) },
                                        trailingIcon = if (selectedTotalIps > 0) {
                                            {
                                                Text(
                                                    text = stringResource(app.slipnet.R.string.scanner_max),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary,
                                                    modifier = Modifier
                                                        .clickable {
                                                            onCustomSampleCountChange(
                                                                selectedTotalIps.coerceAtMost(DnsScannerUiState.MAX_SAMPLE_COUNT.toLong()).toString()
                                                            )
                                                        }
                                                        .padding(end = 12.dp)
                                                )
                                            }
                                        } else null
                                    )
                                }
                            }

                            // Generate button
                            FilledTonalButton(
                                onClick = onLoadIrDnsRange,
                                enabled = !isLoading && selectedOctets.isNotEmpty(),
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp)
                            ) {
                                if (isLoading) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(18.dp),
                                        strokeWidth = 2.dp
                                    )
                                } else {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                Text(stringResource(app.slipnet.R.string.scanner_generate_iran , effectiveSampleCount))
                            }

                            // Start Scan button after loading
                            AnimatedVisibility(
                                visible = listSource == ListSource.IR_DNS_RANGE && !isLoading && resolverCount > 0
                            ) {
                                Button(
                                    onClick = onStartScan,
                                    enabled = canStartScan,
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(12.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_start_scan_1f5473), fontWeight = FontWeight.SemiBold)
                                }
                            }
                        }
                    }

                    ResolverPanel.IR_DNS_LITE -> {
                        Column(
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            // Stats row
                            if (cidrGroups.isNotEmpty()) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .background(
                                            MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                                            RoundedCornerShape(8.dp)
                                        )
                                        .padding(horizontal = 12.dp, vertical = 8.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween
                                ) {
                                    Column {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_range_count , cidrGroups.sumOf { it.rangeCount }),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_total_ips , countryTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                    }
                                    Column(horizontalAlignment = Alignment.End) {
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_groups_count , selectedOctets.size, cidrGroups.size),
                                            style = MaterialTheme.typography.labelMedium,
                                            fontWeight = FontWeight.SemiBold
                                        )
                                        Text(
                                            text = stringResource(app.slipnet.R.string.scanner_selected_ips , selectedTotalIps),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                            }

                            // Range browser
                            if (cidrGroups.isNotEmpty()) {
                                var showRangeGroups by remember { mutableStateOf(false) }
                                Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .clip(RoundedCornerShape(8.dp))
                                            .clickable { showRangeGroups = !showRangeGroups }
                                            .padding(vertical = 4.dp),
                                        horizontalArrangement = Arrangement.SpaceBetween,
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        Row(
                                            horizontalArrangement = Arrangement.spacedBy(6.dp),
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Icon(
                                                if (showRangeGroups) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                                                contentDescription = null,
                                                modifier = Modifier.size(18.dp),
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_range_groups , selectedOctets.size, cidrGroups.size),
                                                style = MaterialTheme.typography.labelMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_all),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onSelectAllOctets() }
                                            )
                                            Text(
                                                text = stringResource(app.slipnet.R.string.scanner_none),
                                                style = MaterialTheme.typography.labelSmall,
                                                color = MaterialTheme.colorScheme.primary,
                                                modifier = Modifier.clickable { onDeselectAllOctets() }
                                            )
                                        }
                                    }

                                    AnimatedVisibility(visible = showRangeGroups) {
                                        Box(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .height(200.dp)
                                                .border(
                                                    1.dp,
                                                    MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
                                                    RoundedCornerShape(12.dp)
                                                )
                                                .clip(RoundedCornerShape(12.dp))
                                        ) {
                                            LazyColumn(
                                                modifier = Modifier.fillMaxSize(),
                                                contentPadding = PaddingValues(4.dp)
                                            ) {
                                                items(cidrGroups.size) { index ->
                                                    val group = cidrGroups[index]
                                                    val isSelected = group.firstOctet in selectedOctets
                                                    Row(
                                                        modifier = Modifier
                                                            .fillMaxWidth()
                                                            .clickable { onToggleOctet(group.firstOctet) }
                                                            .padding(horizontal = 8.dp, vertical = 4.dp),
                                                        verticalAlignment = Alignment.CenterVertically,
                                                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                                                    ) {
                                                        Checkbox(
                                                            checked = isSelected,
                                                            onCheckedChange = { onToggleOctet(group.firstOctet) },
                                                            modifier = Modifier.size(20.dp)
                                                        )
                                                        Text(
                                                            text = group.label,
                                                            style = MaterialTheme.typography.bodyMedium,
                                                            fontWeight = FontWeight.Medium,
                                                            modifier = Modifier.width(80.dp)
                                                        )
                                                        Text(
                                                            text = stringResource(app.slipnet.R.string.scanner_range_count , group.rangeCount),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                            modifier = Modifier.weight(1f)
                                                        )
                                                        Text(
                                                            text = "%,d".format(group.totalIps),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                                        )
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // Sample count selector
                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                Text(
                                    text = stringResource(app.slipnet.R.string.scanner_sample_size),
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                FlowRow(
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    listOf(1000, 2000, 5000, 10000).forEach { count ->
                                        OptionChip(
                                            selected = !useCustomSampleCount && sampleCount == count,
                                            onClick = { onSelectSampleCount(count) },
                                            label = count.toString()
                                        )
                                    }
                                    OptionChip(
                                        selected = useCustomSampleCount,
                                        onClick = { onUseCustomSampleCount(true) },
                                        label = stringResource(app.slipnet.R.string.scanner_custom)
                                    )
                                }
                                AnimatedVisibility(visible = useCustomSampleCount) {
                                    OutlinedTextField(
                                        value = customSampleCountText,
                                        onValueChange = onCustomSampleCountChange,
                                        label = { Text(stringResource(app.slipnet.R.string.scanner_count)) },
                                        placeholder = { Text(stringResource(app.slipnet.R.string.scanner_example_count)) },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp),
                                        supportingText = { Text(stringResource(app.slipnet.R.string.scanner_count_bounds , DnsScannerUiState.MAX_SAMPLE_COUNT)) },
                                        trailingIcon = if (selectedTotalIps > 0) {
                                            {
                                                Text(
                                                    text = stringResource(app.slipnet.R.string.scanner_max),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary,
                                                    modifier = Modifier
                                                        .clickable {
                                                            onCustomSampleCountChange(
                                                                selectedTotalIps.coerceAtMost(DnsScannerUiState.MAX_SAMPLE_COUNT.toLong()).toString()
                                                            )
                                                        }
                                                        .padding(end = 12.dp)
                                                )
                                            }
                                        } else null
                                    )
                                }
                            }

                            // Generate button
                            FilledTonalButton(
                                onClick = onLoadIrDnsLiteRange,
                                enabled = !isLoading && selectedOctets.isNotEmpty(),
                                modifier = Modifier.fillMaxWidth(),
                                shape = RoundedCornerShape(12.dp)
                            ) {
                                if (isLoading) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(18.dp),
                                        strokeWidth = 2.dp
                                    )
                                } else {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                Text(stringResource(app.slipnet.R.string.scanner_generate_iran_lite , effectiveSampleCount))
                            }

                            // Start Scan button after loading
                            AnimatedVisibility(
                                visible = listSource == ListSource.IR_DNS_LITE_RANGE && !isLoading && resolverCount > 0
                            ) {
                                Button(
                                    onClick = onStartScan,
                                    enabled = canStartScan,
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(12.dp),
                                    colors = ButtonDefaults.buttonColors(
                                        containerColor = MaterialTheme.colorScheme.primary
                                    )
                                ) {
                                    Icon(
                                        Icons.Default.PlayArrow,
                                        contentDescription = null,
                                        modifier = Modifier.size(18.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_start_scan_1f5473), fontWeight = FontWeight.SemiBold)
                                }
                            }
                        }
                    }

                    ResolverPanel.NONE -> {
                        // Empty — no extra panel shown
                    }
                }

                // Load Last Scan button — hide when a panel is open to avoid overlap
                AnimatedVisibility(visible = hasLastScanIps && !isLoading && activePanel == ResolverPanel.NONE) {
                    OutlinedButton(
                        onClick = onLoadLastScanIps,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Icon(
                            Icons.Default.History,
                            contentDescription = null,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(Modifier.width(8.dp))
                        Text(stringResource(app.slipnet.R.string.scanner_load_previous))
                    }
                }

                // Saved Pool button — show when user has explicitly saved a pool
                AnimatedVisibility(visible = savedPoolSize > 0 && !isLoading && activePanel == ResolverPanel.NONE) {
                    OutlinedButton(
                        onClick = onLoadSavedPool,
                        modifier = Modifier.fillMaxWidth(),
                        shape = RoundedCornerShape(12.dp)
                    ) {
                        Icon(
                            Icons.Default.Storage,
                            contentDescription = null,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(Modifier.width(8.dp))
                        val label = if (savedPoolLabel != null) "$savedPoolLabel" else stringResource(app.slipnet.R.string.scanner_saved_pool)
                        Text(stringResource(app.slipnet.R.string.scanner_pool_count , label, savedPoolSize))
                    }
                }
            }
        }
    }
}

@Composable
private fun RecentDnsSection(
    recentResolvers: List<String>,
    selectedResolvers: Set<String>,
    isLimitReached: Boolean,
    maxCount: Int,
    canSelect: Boolean,
    onToggleSelection: (String) -> Unit,
    onApply: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerLow
        ),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                SectionHeader(
                    icon = Icons.Default.History,
                    title = stringResource(app.slipnet.R.string.scanner_recent_dns)
                )
                if (canSelect && selectedResolvers.isNotEmpty()) {
                    Text(
                        text = "${selectedResolvers.size} / $maxCount",
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = if (isLimitReached) Color(0xFFFF9800)
                                else MaterialTheme.colorScheme.primary
                    )
                }
            }

            recentResolvers.forEach { ip ->
                val isSelected = canSelect && selectedResolvers.contains(ip)
                val isDisabled = isLimitReached && !isSelected
                val backgroundColor by animateColorAsState(
                    targetValue = if (isSelected) MaterialTheme.colorScheme.primaryContainer
                    else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                    label = "recentBg_$ip"
                )

                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .then(
                            if (canSelect && !isDisabled) Modifier.clickable { onToggleSelection(ip) }
                            else Modifier
                        ),
                    colors = CardDefaults.cardColors(containerColor = backgroundColor),
                    shape = RoundedCornerShape(12.dp),
                    elevation = CardDefaults.cardElevation(
                        defaultElevation = if (isSelected) 1.dp else 0.dp
                    )
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 14.dp, vertical = 10.dp),
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(34.dp)
                                .clip(RoundedCornerShape(8.dp))
                                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                Icons.Default.Dns,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(18.dp)
                            )
                        }

                        Text(
                            text = ip,
                            modifier = Modifier.weight(1f),
                            style = MaterialTheme.typography.bodyMedium.copy(
                                fontFamily = FontFamily.Monospace,
                                letterSpacing = 0.5.sp
                            ),
                            fontWeight = FontWeight.Medium
                        )

                        if (canSelect) {
                            Checkbox(
                                checked = isSelected,
                                onCheckedChange = { if (!isDisabled) onToggleSelection(ip) },
                                enabled = !isDisabled
                            )
                        }
                    }
                }
            }

            if (canSelect && selectedResolvers.any { it in recentResolvers }) {
                Button(
                    onClick = onApply,
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(12.dp)
                ) {
                    Icon(
                        Icons.Default.Check,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(stringResource(app.slipnet.R.string.scanner_apply_selected))
                }
            }
        }
    }
}

@Composable
private fun OptionChip(
    selected: Boolean,
    onClick: () -> Unit,
    label: String
) {
    val backgroundColor by animateColorAsState(
        targetValue = if (selected) MaterialTheme.colorScheme.primaryContainer
        else MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
        label = "optionChipBg"
    )
    val borderColor by animateColorAsState(
        targetValue = if (selected) MaterialTheme.colorScheme.primary
        else MaterialTheme.colorScheme.outline.copy(alpha = 0.3f),
        label = "optionChipBorder"
    )

    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(10.dp))
            .border(
                width = if (selected) 1.5.dp else 1.dp,
                color = borderColor,
                shape = RoundedCornerShape(10.dp)
            )
            .background(backgroundColor)
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp)
    ) {
        Row(
            horizontalArrangement = Arrangement.spacedBy(4.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (selected) {
                Icon(
                    Icons.Default.Check,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.size(14.dp)
                )
            }
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Medium,
                color = if (selected) MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.onSurface
            )
        }
    }
}

@Composable
private fun SectionHeader(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String
) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Icon(
            icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(18.dp)
        )
        Text(
            text = title,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.primary
        )
    }
}
