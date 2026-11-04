package app.slipnet.presentation.main

import app.slipnet.BuildConfig
import app.slipnet.domain.model.TunnelType
import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.net.Uri
import android.net.VpnService
import android.os.PowerManager
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.expandVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.asPaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Air
import androidx.compose.material.icons.filled.FlightTakeoff
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Link
import androidx.compose.material.icons.filled.BugReport
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Dns
import androidx.compose.material.icons.filled.FileDownload
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Language
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Waves
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import androidx.core.content.FileProvider
import androidx.hilt.navigation.compose.hiltViewModel
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.PingResult
import app.slipnet.domain.model.ProfileChain
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TrafficStats
import app.slipnet.presentation.common.components.AboutDialogContent
import app.slipnet.presentation.common.components.ProfileListItem
import app.slipnet.presentation.theme.rv
import app.slipnet.domain.model.ProfileCollections
import app.slipnet.presentation.common.components.QrCodeDialog
import app.slipnet.presentation.common.icons.TorIcon
import app.slipnet.presentation.common.icons.VlessIcon
import app.slipnet.presentation.home.DebugLogSheet
import app.slipnet.presentation.scanner.QrScannerActivity
import androidx.compose.material.icons.filled.Timer
import app.slipnet.presentation.theme.ConnectedGreen
import app.slipnet.presentation.theme.ConnectingOrange
import app.slipnet.presentation.theme.DisconnectedRed
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import kotlinx.coroutines.launch
import sh.calvin.reorderable.ReorderableItem
import sh.calvin.reorderable.rememberReorderableLazyListState

private fun Context.findActivity(): Activity? {
    var context = this
    while (context is ContextWrapper) {
        if (context is Activity) return context
        context = context.baseContext
    }
    return null
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    onNavigateToAddProfile: (tunnelType: String) -> Unit,
    onNavigateToEditProfile: (Long) -> Unit,
    onNavigateToSettings: () -> Unit,
    onNavigateToAddChain: () -> Unit = {},
    onNavigateToEditChain: (Long) -> Unit = {},
    viewModel: MainViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val trustRequest by app.slipnet.tunnel.TransportTrust.request.collectAsState()
    trustRequest?.let { request ->
        AlertDialog(onDismissRequest = { app.slipnet.tunnel.TransportTrust.answer(false) },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.server_identity_title)) },
            text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.server_identity_message, request.endpoint, request.fingerprint)) },
            confirmButton = { TextButton(onClick = { app.slipnet.tunnel.TransportTrust.answer(true) }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.server_identity_accept)) } },
            dismissButton = { TextButton(onClick = { app.slipnet.tunnel.TransportTrust.answer(false) }) { Text(androidx.compose.ui.res.stringResource(android.R.string.cancel)) } })
    }
    var showConnectionGuide by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val clipboardManager = LocalClipboardManager.current
    val activity = context.findActivity()

    // Handle deep link (slipnet:// URI from external QR scanner or link)
    val mainActivity = activity as? app.slipnet.presentation.MainActivity
    val deepLinkUri by mainActivity?.deepLinkUri?.collectAsState() ?: remember { mutableStateOf(null) }
    LaunchedEffect(deepLinkUri) {
        deepLinkUri?.let { uri ->
            viewModel.parseImportConfig(uri)
            mainActivity?.consumeDeepLink()
        }
    }

    // VPN permission flow
    var pendingConnect by remember { mutableStateOf(false) }
    var pendingProfile by remember { mutableStateOf<ServerProfile?>(null) }
    var pendingChain by remember { mutableStateOf<ProfileChain?>(null) }

    // Battery optimization prompt (shown once on first connect)
    var showBatteryOptDialog by remember { mutableStateOf(false) }
    var pendingConnectAfterBatteryPrompt by remember { mutableStateOf(false) }
    var pendingProfileAfterBatteryPrompt by remember { mutableStateOf<ServerProfile?>(null) }
    var pendingChainAfterBatteryPrompt by remember { mutableStateOf<ProfileChain?>(null) }

    // Dialog/sheet state
    var showLanguage by remember { mutableStateOf(false) }
    var expandedCollection by remember { mutableStateOf<String?>(null) }
    var moveTarget by remember { mutableStateOf<ServerProfile?>(null) }
    var renameTarget by remember { mutableStateOf<ServerProfile?>(null) }
    var renameText by remember { mutableStateOf("") }
    var showShareDialog by remember { mutableStateOf(false) }
    var showLogSheet by remember { mutableStateOf(false) }
    var showOverflowMenu by remember { mutableStateOf(false) }
    var showImportDialog by remember { mutableStateOf(false) }
    var importText by remember { mutableStateOf("") }
    var showExportAllEncryptedDialog by remember { mutableStateOf(false) }
    var showAddMenu by remember { mutableStateOf(false) }
    var showDeleteAllDialog by remember { mutableStateOf(false) }
    var showDeleteDuplicatesDialog by remember { mutableStateOf(false) }
    var deleteCollectionTarget by remember { mutableStateOf<List<ServerProfile>?>(null) }
    var profileToDelete by remember { mutableStateOf<ServerProfile?>(null) }
    // Export lock dialog state
    var exportLockProfile by remember { mutableStateOf<ServerProfile?>(null) }
    var exportLockEnabled by remember { mutableStateOf(false) }
    var exportLockPassword by remember { mutableStateOf("") }
    var exportLockMode by remember { mutableStateOf("export") } // "export" or "qr"
    var exportLockExpiry by remember { mutableStateOf(false) }
    var exportLockExpiryDays by remember { mutableStateOf("30") }
    var exportLockAllowSharing by remember { mutableStateOf(false) }
    var exportLockDeviceId by remember { mutableStateOf("") }
    var exportLockPasswordVisible by remember { mutableStateOf(false) }
    var exportHideResolvers by remember { mutableStateOf(false) }

    // Tab state (hoisted so FAB and content can both access it)
    val hasChains = uiState.chains.isNotEmpty()
    var selectedTab by remember { mutableIntStateOf(0) }
    if (!hasChains && selectedTab == 1) selectedTab = 0
    var showLiteInfoDialog by remember { mutableStateOf(false) }

    val vpnPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && pendingConnect) {
            val chain = pendingChain
            val profile = pendingProfile
            if (chain != null) {
                viewModel.connectChain(chain)
            } else if (profile != null) {
                viewModel.connect(profile)
            } else {
                viewModel.connect()
            }
        }
        pendingConnect = false
        pendingProfile = null
        pendingChain = null
    }

    val importFileLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.OpenDocument()
    ) { uri ->
        uri?.let {
            try {
                context.contentResolver.openInputStream(it)?.use { inputStream ->
                    val json = inputStream.bufferedReader().readText()
                    viewModel.parseImportConfig(json)
                }
            } catch (_: Exception) {}
        }
    }

    val qrScanLauncher = rememberLauncherForActivityResult(
        contract = ScanContract()
    ) { result ->
        val contents = result.contents
        if (contents != null) {
            if (app.slipnet.data.export.ConfigSchemes.accepts(contents)) {
                viewModel.parseImportConfig(contents)
            } else {
                scope.launch {
                    snackbarHostState.showSnackbar("Unsupported QR code: use a supported link or WireGuard configuration")
                }
            }
        }
    }

    if (showLanguage) AlertDialog(
        onDismissRequest = { showLanguage = false }, title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_language_a4fe65)) },
        text = { Column {
            listOf("en" to "English", "fa" to "فارسی", "zh" to "简体中文", "ru" to "Русский").forEach { (code, label) ->
                TextButton(onClick = {
                    context.getSharedPreferences("rovunet_ui", Context.MODE_PRIVATE).edit().putString("language", code).apply()
                    activity?.recreate()
                }) { Text(label) }
            }
        } }, confirmButton = { TextButton(onClick = { showLanguage = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_close_7d9eb7)) } }
    )
    if (uiState.collectionPrompt) AlertDialog(
        onDismissRequest = viewModel::dismissCollectionPrompt,
        title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_try_dns_fallback_a41a7b)) },
        text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_direct_paths_failed_in_this_collection_test_its_configured_dns_first_then_search_the_bundl_c79e01)) },
        confirmButton = { TextButton(enabled = !uiState.isPingRunning, onClick = viewModel::acceptCollectionPrompt) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_continue_31fbef)) } },
        dismissButton = { TextButton(onClick = viewModel::dismissCollectionPrompt) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) } }
    )
    moveTarget?.let { source -> AlertDialog(
        onDismissRequest = { moveTarget = null }, title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_move_to_collection_b392f3)) },
        text = { Column(Modifier.verticalScroll(rememberScrollState())) {
            uiState.profiles.distinctBy { ProfileCollections.key(it) }.filter { ProfileCollections.key(it) != ProfileCollections.key(source) }.forEach { target ->
                TextButton(enabled = uiState.connectionState.isDisconnected, onClick = { viewModel.moveCollection(source, target); moveTarget = null }) { Text(ProfileCollections.name(target)) }
            }
        } }, confirmButton = { TextButton(onClick = { moveTarget = null }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) } }
    ) }
    renameTarget?.let { target -> AlertDialog(
        onDismissRequest = { renameTarget = null }, title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_collection_name_922214)) },
        text = { OutlinedTextField(value = renameText, onValueChange = { renameText = it.take(120) }, singleLine = true) },
        confirmButton = { TextButton(enabled = renameText.isNotBlank(), onClick = { viewModel.renameCollection(target, renameText); renameTarget = null }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_save_1509f5)) } },
        dismissButton = { TextButton(onClick = { renameTarget = null }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) } }
    ) }

    // Handle export — show dialog with Copy and Share options
    uiState.exportedJson?.let { json ->
        AlertDialog(
            onDismissRequest = { viewModel.clearExportedJson() },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_export_366489)) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_config_is_ready_to_share_39820f), style = MaterialTheme.typography.bodyMedium)
                    OutlinedTextField(
                        value = json,
                        onValueChange = {},
                        readOnly = true,
                        modifier = Modifier.fillMaxWidth().height(100.dp),
                        maxLines = 4,
                        textStyle = MaterialTheme.typography.bodySmall
                    )
                }
            },
            confirmButton = {
                TextButton(onClick = {
                    val sendIntent = Intent().apply {
                        action = Intent.ACTION_SEND
                        putExtra(Intent.EXTRA_TEXT, json)
                        type = "text/plain"
                    }
                    context.startActivity(Intent.createChooser(sendIntent, "Export Profile"))
                    viewModel.clearExportedJson()
                }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_share_29887a)) }
            },
            dismissButton = {
                Row {
                    TextButton(onClick = {
                        clipboardManager.setText(androidx.compose.ui.text.AnnotatedString(json))
                        viewModel.clearExportedJson()
                        scope.launch { snackbarHostState.showSnackbar("Copied to clipboard") }
                    }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_copy_e21f93)) }
                    TextButton(onClick = { viewModel.clearExportedJson() }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
                }
            }
        )
    }

    val localizedError = uiState.error?.let { rv(it) }
    LaunchedEffect(localizedError) {
        localizedError?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    val navBarPadding = WindowInsets.navigationBars.asPaddingValues()

    val showTorProgressFab = uiState.connectionState is ConnectionState.Connecting &&
            uiState.snowflakeBootstrapProgress in 0..99

    val sleepTimerActive = uiState.connectionState is ConnectionState.Connected &&
            uiState.sleepTimerRemainingSeconds > 0

    val dnsWarningActive = uiState.connectionState is ConnectionState.Connected &&
            uiState.dnsWarning != null

    val fabExtraPadding by animateDpAsState(
        targetValue = when {
            uiState.connectionState is ConnectionState.Connected && sleepTimerActive && dnsWarningActive -> 72.dp
            uiState.connectionState is ConnectionState.Connected && sleepTimerActive -> 52.dp
            uiState.connectionState is ConnectionState.Connected && dnsWarningActive -> 48.dp
            uiState.connectionState is ConnectionState.Connected -> 28.dp
            showTorProgressFab -> 30.dp
            else -> 0.dp
        },
        animationSpec = tween(300),
        label = "fabPadding"
    )

    // Helper: proceed with VPN permission check and connect
    fun proceedWithConnect(profile: ServerProfile? = null) {
        if (activity != null) {
            // In proxy-only mode, skip VPN permission check — no TUN interface is created,
            // so VpnService.prepare() is unnecessary and would fail when another app
            // holds "Always-on VPN".
            if (uiState.proxyOnlyMode) {
                if (profile != null) viewModel.connect(profile) else viewModel.connect()
                return
            }
            val vpnIntent = VpnService.prepare(activity)
            if (vpnIntent != null) {
                pendingConnect = true
                pendingProfile = profile
                pendingChain = null
                vpnPermissionLauncher.launch(vpnIntent)
            } else {
                if (profile != null) viewModel.connect(profile) else viewModel.connect()
            }
        }
    }

    // Helper: proceed with VPN permission check and connect a chain
    fun proceedWithChainConnect(chain: ProfileChain) {
        if (activity != null) {
            if (uiState.proxyOnlyMode) {
                viewModel.connectChain(chain)
                return
            }
            val vpnIntent = VpnService.prepare(activity)
            if (vpnIntent != null) {
                pendingConnect = true
                pendingChain = chain
                pendingProfile = null
                vpnPermissionLauncher.launch(vpnIntent)
            } else {
                viewModel.connectChain(chain)
            }
        }
    }

    // Helper to request VPN permission and connect
    fun requestConnectOrToggle() {
        if (uiState.isPingRunning) { viewModel.cancelPing(); return }
        when (uiState.connectionState) {
            is ConnectionState.Connected,
            is ConnectionState.Connecting,
            is ConnectionState.Error -> viewModel.disconnect()
            else -> {
                if (activity != null) {
                    // Check battery optimization on first connect
                    val prefs = context.getSharedPreferences("vpn_service_state", Context.MODE_PRIVATE)
                    val prompted = prefs.getBoolean("battery_opt_prompted", false)
                    val pm = context.getSystemService(Context.POWER_SERVICE) as PowerManager
                    if (!prompted && !pm.isIgnoringBatteryOptimizations(context.packageName)) {
                        pendingConnectAfterBatteryPrompt = true
                        pendingProfileAfterBatteryPrompt = null
                        // If on Chains tab, remember the active chain for after the battery dialog
                        pendingChainAfterBatteryPrompt = if (selectedTab == 1 && hasChains) uiState.activeChain else null
                        showBatteryOptDialog = true
                        return
                    }

                    // If on Chains tab, connect the active chain; otherwise connect active profile
                    if (selectedTab == 1 && hasChains) {
                        val chain = uiState.activeChain
                        if (chain != null) {
                            proceedWithChainConnect(chain)
                        }
                    } else {
                        proceedWithConnect()
                    }
                }
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        Text(
                            text = "Rovunet",
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.Bold
                        )
                        if (BuildConfig.FLAVOR == "lite") {
                            Text(
                                text = "Lite",
                                style = MaterialTheme.typography.labelLarge,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        if (BuildConfig.FLAVOR == "lite") {
                            IconButton(onClick = { showLiteInfoDialog = true }) {
                                Icon(
                                    Icons.Default.Info,
                                    contentDescription = "Lite version info",
                                    modifier = Modifier.size(20.dp)
                                )
                            }
                        }
                    }
                },
                actions = {
                    if (uiState.debugLogging) {
                        IconButton(onClick = { showLogSheet = true }) {
                            Icon(Icons.Default.BugReport, contentDescription = "Debug Logs")
                        }
                    }
                    IconButton(onClick = { showShareDialog = true }) {
                        Icon(Icons.Default.Share, contentDescription = "Share App")
                    }
                    IconButton(onClick = onNavigateToSettings) {
                        Icon(Icons.Default.Settings, contentDescription = "Settings")
                    }
                    // Overflow menu (three-dot, rightmost)
                    Box {
                        IconButton(onClick = { showOverflowMenu = true }) {
                            Icon(Icons.Default.MoreVert, contentDescription = "More")
                        }
                        DropdownMenu(
                            expanded = showOverflowMenu,
                            onDismissRequest = { showOverflowMenu = false }
                        ) {
                            DropdownMenuItem(
                            text = { Text("Language · زبان · 语言 · Язык") },
                            onClick = { showOverflowMenu = false; showLanguage = true }
                        )
                        if (uiState.regroupUndoAvailable) DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_undo_regrouping_dbd472)) },
                            enabled = !uiState.regroupBusy && !uiState.isPingRunning && uiState.connectionState.isDisconnected,
                            onClick = viewModel::undoRegroup)
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_regroup_existing_profiles_fdf9b0)) },
                            enabled = !uiState.isPingRunning && uiState.connectionState.isDisconnected,
                            onClick = { viewModel.previewRegroup() })
                        DropdownMenuItem(text = { Text("Choosing a profile / Test guide") }, onClick = {
                                showOverflowMenu = false; showConnectionGuide = true
                            })
                            DropdownMenuItem(
                                text = {
                                    Text(if (uiState.isPingRunning) "Stop HTTPS Test" else "Test selected collection")
                                },
                                onClick = {
                                    showOverflowMenu = false
                                    mainActivity?.requestVpnPermissionAndConnect { viewModel.pingAllProfiles() } ?: viewModel.pingAllProfiles()
                                },
                                enabled = uiState.profiles.isNotEmpty()
                            )
                            DropdownMenuItem(
                                text = {
                                    Text(if (uiState.isPingRunning) "Stop TCP Check" else "TCP check · selected collection")
                                },
                                onClick = {
                                    showOverflowMenu = false
                                    viewModel.pingAllProfilesSimple()
                                },
                                enabled = uiState.profiles.isNotEmpty()
                            )
                            if (uiState.pingResults.isNotEmpty() && !uiState.isPingRunning) {
                                DropdownMenuItem(
                                    text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_clear_ping_results_ad461f)) },
                                    onClick = {
                                        showOverflowMenu = false
                                        viewModel.clearPingResults()
                                    }
                                )
                            }
                            // Only show "Sort by HTTPS latency" when at least one profile has a
                            // successful measurement — otherwise there's nothing to sort by.
                            if (uiState.pingResults.values.any { it is PingResult.Success && it.verified }) {
                                DropdownMenuItem(text = { Text("Sort by short-test reliability") }, onClick = {
                                    showOverflowMenu = false; viewModel.sortProfilesByPing(reliability = true)
                                })
                            }
                            if (uiState.pingResults.values.any { it is PingResult.Success }) {
                                DropdownMenuItem(
                                    text = { Text("Sort by HTTPS latency") },
                                    onClick = {
                                        showOverflowMenu = false
                                        viewModel.sortProfilesByPing()
                                    }
                                )
                            }
                            DropdownMenuItem(
                                text = { Text("Export All Profiles") },
                                onClick = {
                                    showOverflowMenu = false
                                    viewModel.exportAllProfiles()
                                },
                                enabled = uiState.profiles.isNotEmpty()
                            )
                            DropdownMenuItem(
                                text = { Text("Export All (Encrypted)") },
                                onClick = {
                                    showOverflowMenu = false
                                    showExportAllEncryptedDialog = true
                                },
                                enabled = uiState.profiles.any { !it.isLocked }
                            )
                            DropdownMenuItem(
                                text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_profiles_d47ce9)) },
                                onClick = {
                                    showOverflowMenu = false
                                    showImportDialog = true
                                }
                            )
                            DropdownMenuItem(
                                text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_duplicate_profiles)) },
                                onClick = {
                                    showOverflowMenu = false
                                    viewModel.previewDuplicateProfiles()
                                    showDeleteDuplicatesDialog = true
                                },
                                enabled = uiState.profiles.size > 1 && !uiState.isPingRunning && uiState.connectionState.isDisconnected
                            )
                            DropdownMenuItem(
                                text = { Text("Delete All Profiles") },
                                onClick = {
                                    showOverflowMenu = false
                                    showDeleteAllDialog = true
                                },
                                enabled = uiState.profiles.isNotEmpty()
                            )
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent
                )
            )
        },
        floatingActionButton = {
            Column(
                horizontalAlignment = Alignment.End
            ) {
                FloatingActionButton(
                    onClick = { showAddMenu = !showAddMenu },
                    containerColor = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.padding(end = 8.dp)
                ) {
                    Icon(Icons.Default.Add, contentDescription = "Add Profile")
                }

                Spacer(modifier = Modifier.height(12.dp))

                // Connect / Disconnect FAB
                ConnectFab(
                    connectionState = uiState.connectionState,
                    hasProfile = uiState.activeProfile != null || uiState.activeChain != null || uiState.profiles.isNotEmpty(),
                    snowflakeBootstrapProgress = uiState.snowflakeBootstrapProgress,
                    onToggleConnection = { requestConnectOrToggle() },
                    modifier = Modifier.padding(
                        bottom = 24.dp + navBarPadding.calculateBottomPadding() + fabExtraPadding,
                        end = 8.dp
                    )
                )
            }
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
        containerColor = MaterialTheme.colorScheme.background,
        contentWindowInsets = WindowInsets(0, 0, 0, 0)
    ) { paddingValues ->
        // Auto-switch to chains tab when a chain is connected
        LaunchedEffect(uiState.connectedChainId) {
            if (uiState.connectedChainId != null && hasChains) selectedTab = 1
        }

        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            Column(modifier = Modifier.fillMaxSize()) {
                // ── Tab Row (only when chains exist) ────────────────────
                if (hasChains) {
                    SingleChoiceSegmentedButtonRow(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp)
                    ) {
                        SegmentedButton(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            shape = SegmentedButtonDefaults.itemShape(0, 2)
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_profiles_535e52)) }
                        SegmentedButton(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            shape = SegmentedButtonDefaults.itemShape(1, 2)
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_chains_79aaa1)) }
                    }
                }

                if (uiState.isPingRunning) {
                    Row(Modifier.fillMaxWidth().padding(horizontal = 20.dp), verticalAlignment = Alignment.CenterVertically) {
                        androidx.compose.material3.CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
                        Text(rv(uiState.collectionProgress), modifier = Modifier.weight(1f).padding(12.dp), style = MaterialTheme.typography.bodySmall)
                        TextButton(onClick = { viewModel.cancelPing() }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_stop_cae7d5)) }
                    }
                }

                // ── Tab Content ─────────────────────────────────────────
                when {
                    selectedTab == 1 && hasChains -> {
                        // ── Chains Tab ──────────────────────────────────
                        val chainListState = rememberLazyListState()
                        val reorderableChainState = rememberReorderableLazyListState(chainListState) { from, to ->
                            viewModel.moveChain(from.index, to.index)
                        }

                        LazyColumn(
                            state = chainListState,
                            contentPadding = PaddingValues(
                                start = 16.dp, end = 16.dp,
                                top = 8.dp,
                                bottom = 200.dp + navBarPadding.calculateBottomPadding()
                            ),
                            verticalArrangement = Arrangement.spacedBy(12.dp),
                            modifier = Modifier.fillMaxSize()
                        ) {
                            items(
                                items = uiState.chains,
                                key = { "chain_${it.id}" }
                            ) { chain ->
                                ReorderableItem(reorderableChainState, key = "chain_${chain.id}") { isDragging ->
                                    val elevation = if (isDragging) 8.dp else 0.dp

                                    ChainListItem(
                                        chain = chain,
                                        profiles = uiState.profiles,
                                        isSelected = uiState.activeChain?.id == chain.id,
                                        isConnected = uiState.connectedChainId == chain.id,
                                        onClick = { viewModel.setActiveChain(chain) },
                                        onEditClick = { onNavigateToEditChain(chain.id) },
                                        onDeleteClick = {
                                            val isConnected = uiState.connectedChainId == chain.id
                                            if (isConnected) {
                                                scope.launch {
                                                    snackbarHostState.showSnackbar(
                                                        "Disconnect VPN before deleting this chain"
                                                    )
                                                }
                                            } else {
                                                viewModel.deleteChain(chain)
                                            }
                                        },
                                        modifier = Modifier
                                            .longPressDraggableHandle()
                                            .shadow(elevation, RoundedCornerShape(12.dp))
                                            .zIndex(if (isDragging) 1f else 0f)
                                    )
                                }
                            }
                        }
                    }
                    uiState.isLoading -> {
                        Box(
                            modifier = Modifier.fillMaxSize(),
                            contentAlignment = Alignment.Center
                        ) {
                            androidx.compose.material3.CircularProgressIndicator()
                        }
                    }
                    uiState.profiles.isEmpty() -> {
                        Box(
                            modifier = Modifier.fillMaxSize(),
                            contentAlignment = Alignment.Center
                        ) {
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                modifier = Modifier.padding(32.dp)
                            ) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_no_profiles_yet_bd4729),
                                    style = MaterialTheme.typography.bodyLarge,
                                    fontWeight = FontWeight.Medium
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_tap_to_add_your_first_profile_3bb680),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                    else -> {
                        // ── Profiles Tab ────────────────────────────────
                        val lazyListState = rememberLazyListState()
                        val reorderableLazyListState = rememberReorderableLazyListState(lazyListState) { from, to ->
                            viewModel.moveProfile(from.index, to.index)
                        }

                        Column(modifier = Modifier.fillMaxSize()) {
                            // Pinned indicator: always visible (above the scrollable list) so
                            // users see the active Global DNS context even after scrolling.
                            if (uiState.pingingViaGlobalDns.isNotEmpty()) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(horizontal = 16.dp, vertical = 4.dp)
                                        .background(
                                            MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.35f),
                                            RoundedCornerShape(8.dp)
                                        )
                                        .padding(horizontal = 12.dp, vertical = 8.dp)
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Dns,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.primary,
                                        modifier = Modifier.size(16.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                    Text(
                                        text = "Pinging via Global DNS: " +
                                            uiState.pingingViaGlobalDns.joinToString { "${it.host}:${it.port}" },
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        maxLines = 2
                                    )
                                }
                            }

                            LazyColumn(
                                state = lazyListState,
                                contentPadding = PaddingValues(
                                    start = 16.dp, end = 16.dp,
                                    top = 8.dp,
                                    bottom = 200.dp + navBarPadding.calculateBottomPadding()
                                ),
                                verticalArrangement = Arrangement.spacedBy(12.dp),
                                modifier = Modifier.fillMaxSize()
                            ) {
                                items(
                                    items = uiState.profiles.groupBy { ProfileCollections.key(it) }.values.toList(),
                                    key = { ProfileCollections.key(it.first()) }
                                ) { collection ->
                                    val representative = collection.first()
                                    val collectionKey = ProfileCollections.key(representative)
                                    val selected = collectionKey == (uiState.selectedCollection ?: uiState.activeProfile?.let { ProfileCollections.key(it) })
                                    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                        CollectionCard(
                                            profiles = collection, results = uiState.pingResults,
                                            selected = selected, expanded = expandedCollection == collectionKey,
                                            connectedProfileId = uiState.connectedProfileId?.takeIf { id -> collection.any { it.id == id } },
                                            manualProfileId = uiState.manualProfileId?.takeIf { id -> collection.any { it.id == id } },
                                            busy = uiState.isPingRunning || uiState.collectionPrompt,
                                            onSelect = { viewModel.selectCollection(representative) },
                                            onDetails = { expandedCollection = if (expandedCollection == collectionKey) null else collectionKey },
                                            onRename = { renameTarget = representative; renameText = ProfileCollections.name(representative) },
                                            onMove = { moveTarget = representative },
                                            onExport = { viewModel.exportCollection(representative) },
                                            onDelete = { deleteCollectionTarget = collection.toList() },
                                            canDelete = uiState.connectionState.isDisconnected,
                                            onTest = {
                                                if (uiState.proxyOnlyMode) viewModel.pingCollection(representative)
                                                else mainActivity?.requestVpnPermissionAndConnect { viewModel.pingCollection(representative) }
                                            }
                                        )
                                        if (expandedCollection == collectionKey) collection.forEach { profile ->
                                            val isConnected = uiState.connectedProfileId == profile.id
                                    ProfileListItem(
                                        profile = profile,
                                        isSelected = profile.id == uiState.manualProfileId,
                                        isConnected = isConnected,
                                        pingResult = uiState.pingResults[profile.id],
                                        onClick = { viewModel.setActiveProfile(profile) },
                                        onEditClick = { onNavigateToEditProfile(profile.id) },
                                        onDeleteClick = {
                                            if (isConnected) {
                                                scope.launch {
                                                    snackbarHostState.showSnackbar(
                                                        "Disconnect VPN before deleting this profile"
                                                    )
                                                }
                                            } else {
                                                profileToDelete = profile
                                            }
                                        },
                                        onExportClick = {
                                            exportLockProfile = profile
                                            exportLockEnabled = false
                                            exportLockPassword = ""
                                            exportLockMode = "export"
                                        },
                                        onShareQrClick = {
                                            exportLockProfile = profile
                                            exportLockEnabled = false
                                            exportLockPassword = ""
                                            exportLockMode = "qr"
                                        },
                                        onPinClick = { viewModel.togglePinProfile(profile) },
                                        onPingClick = { viewModel.pingSingleProfile(profile) },
                                        isPingRunning = uiState.isPingRunning,
                                        modifier = Modifier.padding(start = 12.dp)
                                    )
                                        }
                                    }
                            }
                            }
                        }
                    }
                }
            }

            // ── Connection Status Strip (bottom, behind FAB) ────────
            ConnectionStatusStrip(
                connectionState = uiState.connectionState,
                activeProfile = uiState.activeProfile,
                activeChain = uiState.activeChain,
                isProxyOnly = uiState.proxyOnlyMode,
                snowflakeBootstrapProgress = uiState.snowflakeBootstrapProgress,
                uploadSpeed = uiState.uploadSpeed,
                downloadSpeed = uiState.downloadSpeed,
                totalUpload = uiState.trafficStats.bytesSent,
                totalDownload = uiState.trafficStats.bytesReceived,
                sleepTimerRemainingSeconds = uiState.sleepTimerRemainingSeconds,
                onCancelSleepTimer = { viewModel.userCancelSleepTimer() },
                dnsWarning = uiState.dnsWarning,
                dnsPoolScan = uiState.dnsPoolScan,
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = navBarPadding.calculateBottomPadding())
            )

            // Bottom sheet for adding profiles
            if (showAddMenu) {
                val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
                ModalBottomSheet(
                    onDismissRequest = { showAddMenu = false },
                    sheetState = sheetState,
                    dragHandle = null
                ) {
                    Column(modifier = Modifier.verticalScroll(rememberScrollState())) {
                    Text(
                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_new_profile_cfeed6),
                        style = MaterialTheme.typography.titleMedium,
                        modifier = Modifier.padding(start = 24.dp, top = 20.dp, bottom = 8.dp)
                    )
                    @OptIn(ExperimentalLayoutApi::class)
                    FlowRow(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 12.dp, vertical = 4.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                        maxItemsInEachRow = 3
                    ) {
                        val groups = listOf(
                            "Web / TCP paths" to listOf(Triple("REALITY", "reality", Icons.Default.Shield), Triple("XHTTP", "xhttp", Icons.Default.Language), Triple("VLESS WS", "vless", VlessIcon), Triple("SSH", "ssh", Icons.Default.Lock), Triple("SOCKS5", "socks5", Icons.Default.Share)) +
                                (if (BuildConfig.INCLUDE_NAIVE) listOf(Triple("NaiveProxy", "naive", Icons.Default.Shield)) else emptyList()),
                            "UDP paths" to listOf(Triple("Hysteria2", "hysteria2", Icons.Default.Waves), Triple("WireGuard", "wireguard", Icons.Default.Lock)),
                            "DNS / alternative paths" to listOf(Triple("DNSTT", "dnstt", Icons.Default.Dns), Triple("NoizDNS", TunnelType.NOIZDNS.value, Icons.Default.VisibilityOff), Triple("VayDNS", "vaydns", Icons.Default.Air), Triple("Soar", "soar", Icons.Default.FlightTakeoff), Triple("Slipstream", "slipstream", Icons.Default.Waves), Triple("DOH", "doh", Icons.Default.Language)) +
                                (if (BuildConfig.INCLUDE_TOR) listOf(Triple("Tor", "snowflake", TorIcon)) else emptyList())
                        )
                        TextButton(onClick = { showConnectionGuide = true }, modifier = Modifier.fillMaxWidth()) { Text("Which should I use? • Speed is unknown until tested") }
                        groups.forEach { (heading, entries) ->
                            Text(heading, style = MaterialTheme.typography.titleSmall, modifier = Modifier.fillMaxWidth().padding(12.dp))
                            entries.forEach { (label, route, tileIcon) ->
                                AddMenuGridItem(modifier = Modifier.weight(1f), icon = tileIcon, title = label,
                                    onClick = { showAddMenu = false; onNavigateToAddProfile(route) })
                            }
                        }

                    }
                    HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                    AddMenuOption(
                        icon = Icons.Default.Link,
                        title = "Chain",
                        description = "Chain multiple profiles",
                        onClick = {
                            showAddMenu = false
                            onNavigateToAddChain()
                        }
                    )
                    AddMenuOption(
                        icon = Icons.Default.FileDownload,
                        title = "Import",
                        description = "",
                        onClick = {
                            showAddMenu = false
                            showImportDialog = true
                        }
                    )
                    Spacer(Modifier.height(24.dp))
                    }
                }
            }
        }
    }

    if (showConnectionGuide) {
        AlertDialog(onDismissRequest = { showConnectionGuide = false }, title = { Text("Choose a connection") },
            text = { Text(app.slipnet.domain.model.ProfileGuidance.GUIDE, modifier = Modifier.verticalScroll(rememberScrollState())) },
            confirmButton = { TextButton(onClick = { showConnectionGuide = false }) { Text("OK") } })
    }
    // ── Dialogs ─────────────────────────────────────────────────────────

    // Debug log sheet
    if (showLogSheet) {
        DebugLogSheet(onDismiss = { showLogSheet = false })
    }

    // Lite version info dialog
    if (showLiteInfoDialog) {
        AlertDialog(
            onDismissRequest = { showLiteInfoDialog = false },
            title = { Text("Rovunet Lite") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Rovunet Lite is a lightweight version with a smaller app size.")
                    Text("Included protocols:", fontWeight = FontWeight.Bold)
                    Text("• Slipstream / Slipstream + SSH")
                    Text("• DNSTT / DNSTT + SSH")
                    Text("• NoizDNS / NoizDNS + SSH")
                    Text("• VayDNS / VayDNS + SSH")
                    Text("• SSH")
                    Text("• DOH (DNS over HTTPS)")
                    Text("Not included (full version only):", fontWeight = FontWeight.Bold)
                    Text("• Tor (Snowflake)")
                    Text("• NaïveProxy / NaïveProxy + SSH")
                }
            },
            confirmButton = {
                TextButton(onClick = { showLiteInfoDialog = false }) {
                    Text("OK")
                }
            }
        )
    }

    // Share dialog
    if (showShareDialog) {
        AlertDialog(
            onDismissRequest = { showShareDialog = false },
            title = { Text("Share Rovunet") },
            text = {
                Column(modifier = Modifier.fillMaxWidth()) {
                    Text(
                        "How would you like to share the app?",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Spacer(Modifier.height(16.dp))
                    TextButton(
                        onClick = { showShareDialog = false; shareApk(context) },
                        modifier = Modifier.fillMaxWidth()
                    ) { Text("APK File") }

                }
            },
            confirmButton = {}
        )
    }

    // Delete confirmation dialog
    profileToDelete?.let { profile ->
        AlertDialog(
            onDismissRequest = { profileToDelete = null },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_delete_profile_4e5fc6)) },
            text = { Text("Are you sure you want to delete \"${profile.name}\"?") },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.deleteProfile(profile)
                        profileToDelete = null
                    }
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_delete_e2d0a5)) }
            },
            dismissButton = {
                TextButton(onClick = { profileToDelete = null }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // Delete all confirmation dialog
    if (showDeleteAllDialog) {
        AlertDialog(
            onDismissRequest = { showDeleteAllDialog = false },
            title = { Text("Delete All Profiles") },
            text = {
                Text("Are you sure you want to delete all profiles? This cannot be undone.")
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.deleteAllProfiles()
                        showDeleteAllDialog = false
                    }
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_delete_all_ef85af)) }
            },
            dismissButton = {
                TextButton(onClick = { showDeleteAllDialog = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    deleteCollectionTarget?.let { reviewed ->
        AlertDialog(onDismissRequest = { deleteCollectionTarget = null },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_collection)) },
            text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_collection_confirm, ProfileCollections.name(reviewed.first()), reviewed.size)) },
            confirmButton = { TextButton(enabled = !uiState.isPingRunning && uiState.connectionState.isDisconnected,
                onClick = { viewModel.deleteCollection(reviewed); deleteCollectionTarget = null }) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_collection), color = MaterialTheme.colorScheme.error)
            } },
            dismissButton = { TextButton(onClick = { deleteCollectionTarget = null }) { Text(rv("Cancel")) } })
    }

    // Delete duplicate profiles confirmation dialog
    if (showDeleteDuplicatesDialog) {
        AlertDialog(
            onDismissRequest = { showDeleteDuplicatesDialog = false },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_duplicate_profiles)) },
            text = {
                Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_only_exact_duplicates_within_the_same_collection_are_listed_locked_pinned_and_chain_profil_6064c6))
                    if (uiState.duplicatePreview.isEmpty()) Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_no_exact_duplicates_found_b3a2b7))
                    uiState.duplicatePreview.forEach { match ->
                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.duplicate_review_entry, ProfileCollections.name(match.keep), match.remove.name, match.keep.name))
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.deleteDuplicateProfiles()
                        showDeleteDuplicatesDialog = false
                    }, enabled = uiState.duplicatePreview.isNotEmpty()
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_delete_duplicates_b89a2e)) }
            },
            dismissButton = {
                TextButton(onClick = { showDeleteDuplicatesDialog = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // Battery optimization dialog (shown once on first connect)
    if (showBatteryOptDialog) {
        AlertDialog(
            onDismissRequest = {
                showBatteryOptDialog = false
                // Mark as prompted so we don't ask again
                context.getSharedPreferences("vpn_service_state", Context.MODE_PRIVATE)
                    .edit().putBoolean("battery_opt_prompted", true).apply()
                // Proceed with connect regardless
                if (pendingConnectAfterBatteryPrompt) {
                    pendingConnectAfterBatteryPrompt = false
                    val chain = pendingChainAfterBatteryPrompt
                    if (chain != null) {
                        pendingChainAfterBatteryPrompt = null
                        proceedWithChainConnect(chain)
                    } else {
                        proceedWithConnect(pendingProfileAfterBatteryPrompt)
                        pendingProfileAfterBatteryPrompt = null
                    }
                }
            },
            title = { Text("Disable Battery Optimization") },
            text = {
                Text("For reliable VPN operation, disable battery optimization for Rovunet. " +
                     "Without this, Android may suspend the VPN when the screen is off.")
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        showBatteryOptDialog = false
                        context.getSharedPreferences("vpn_service_state", Context.MODE_PRIVATE)
                            .edit().putBoolean("battery_opt_prompted", true).apply()
                        // Open battery optimization settings
                        try {
                            val intent = Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                                data = Uri.parse("package:${context.packageName}")
                            }
                            context.startActivity(intent)
                        } catch (_: Exception) {
                            try {
                                context.startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
                            } catch (_: Exception) { }
                        }
                        // Proceed with connect
                        if (pendingConnectAfterBatteryPrompt) {
                            pendingConnectAfterBatteryPrompt = false
                            val chain = pendingChainAfterBatteryPrompt
                            if (chain != null) {
                                pendingChainAfterBatteryPrompt = null
                                proceedWithChainConnect(chain)
                            } else {
                                proceedWithConnect(pendingProfileAfterBatteryPrompt)
                                pendingProfileAfterBatteryPrompt = null
                            }
                        }
                    }
                ) { Text("Disable") }
            },
            dismissButton = {
                TextButton(
                    onClick = {
                        showBatteryOptDialog = false
                        context.getSharedPreferences("vpn_service_state", Context.MODE_PRIVATE)
                            .edit().putBoolean("battery_opt_prompted", true).apply()
                        // Proceed with connect without disabling
                        if (pendingConnectAfterBatteryPrompt) {
                            pendingConnectAfterBatteryPrompt = false
                            val chain = pendingChainAfterBatteryPrompt
                            if (chain != null) {
                                pendingChainAfterBatteryPrompt = null
                                proceedWithChainConnect(chain)
                            } else {
                                proceedWithConnect(pendingProfileAfterBatteryPrompt)
                                pendingProfileAfterBatteryPrompt = null
                            }
                        }
                    }
                ) { Text("Skip") }
            }
        )
    }

    LaunchedEffect(Unit) { viewModel.refreshRegroupUndo() }
    uiState.regroupPreview?.let { groups ->
        AlertDialog(onDismissRequest = viewModel::cancelRegroup,
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_regroup_existing_profiles_fdf9b0)) },
            text = { Column(Modifier.verticalScroll(rememberScrollState())) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_only_group_names_and_membership_change_connection_settings_are_preserved_66d347))
                groups.forEach { group ->
                    val oldNames = group.profiles.map { ProfileCollections.name(it) }.distinct().joinToString("، ")
                    Text("$oldNames → ${group.name} · ${group.profiles.size}")
                }
            } },
            confirmButton = { TextButton(onClick = viewModel::confirmRegroup, enabled = !uiState.regroupBusy) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392)) } },
            dismissButton = { TextButton(onClick = viewModel::cancelRegroup) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) } })
    }

    // Import preview dialog
    uiState.importPreview?.let { preview ->
        AlertDialog(
            onDismissRequest = { viewModel.cancelImport() },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_profiles_d47ce9)) },
            text = {
                Column(
                    modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text(
                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.import_profile_count, preview.profiles.size),
                        style = MaterialTheme.typography.bodyMedium
                    )
                    if (preview.groups.size == 1) OutlinedTextField(value = preview.collectionName, onValueChange = viewModel::setImportCollectionName,
                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_collection_name_922214)) }, placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_example_collection_name)) }, singleLine = true)
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_grouped_by_account_and_server_speed_and_stability_are_untested_d7280c), style = MaterialTheme.typography.bodySmall)
                    TextButton(onClick = { showConnectionGuide = true }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_which_should_i_use_8e8eed)) }
                    val omitted = preview.profiles.size - preview.groups.sumOf { it.profiles.size }
                    if (omitted > 0) Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.import_duplicate_count, omitted))
                    preview.groups.forEach { group ->
                        Text("${group.name} · ${group.profiles.size}", style = MaterialTheme.typography.titleSmall)
                    }
                    if (preview.warnings.isNotEmpty()) {
                        Text(
                            text = "Warnings:",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                        preview.warnings.forEach { warning ->
                            Text(
                                text = "\u2022 $warning",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.error
                            )
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { viewModel.confirmImport() }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_2cff9b)) }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.cancelImport() }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // QR code share dialog
    uiState.qrCodeData?.let { qrData ->
        QrCodeDialog(
            profileName = qrData.profileName,
            configUri = qrData.configUri,
            onDismiss = { viewModel.clearQrCode() }
        )
    }

    // Export lock dialog
    exportLockProfile?.let { profile ->
        // If profile is already locked+sharable, skip dialog and re-export directly
        if (profile.isLocked && profile.allowSharing) {
            LaunchedEffect(profile) {
                if (exportLockMode == "qr") {
                    viewModel.showQrCodeLockedReExport(profile)
                } else {
                    viewModel.reExportLockedProfile(profile)
                }
                exportLockProfile = null
            }
        } else if (!profile.isLocked) {
            AlertDialog(
                onDismissRequest = { exportLockProfile = null },
                title = { Text(if (exportLockMode == "qr") "Share QR Code" else "Export Profile") },
                text = {
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        // SSH security warning when sharing with root credentials
                        if (profile.sshUsername.equals("root", ignoreCase = true) &&
                            profile.tunnelType in listOf(
                                TunnelType.SSH, TunnelType.DNSTT_SSH,
                                TunnelType.SLIPSTREAM_SSH, TunnelType.NOIZDNS_SSH,
                                TunnelType.VAYDNS_SSH, TunnelType.NAIVE_SSH,
                                TunnelType.SOAR_SSH
                            )
                        ) {
                            Card(
                                colors = CardDefaults.cardColors(
                                    containerColor = MaterialTheme.colorScheme.errorContainer
                                )
                            ) {
                                Row(
                                    modifier = Modifier.padding(12.dp),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    Icon(
                                        Icons.Default.Warning,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.error,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Text(
                                        text = "This profile contains SSH credentials. Avoid using a root account or password auth — anyone with this profile can access your server. Use a restricted user with key-based authentication instead.",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onErrorContainer
                                    )
                                }
                            }
                        }

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Text(
                                text = "Lock for distribution",
                                style = MaterialTheme.typography.bodyMedium
                            )
                            Switch(
                                checked = exportLockEnabled,
                                onCheckedChange = {
                                    exportLockEnabled = it
                                    if (!it) exportHideResolvers = false
                                }
                            )
                        }
                        if (exportLockEnabled) {
                            OutlinedTextField(
                                value = exportLockPassword,
                                onValueChange = { exportLockPassword = it },
                                label = { Text("Lock Password") },
                                visualTransformation = if (exportLockPasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                trailingIcon = {
                                    IconButton(onClick = { exportLockPasswordVisible = !exportLockPasswordVisible }) {
                                        Icon(
                                            imageVector = if (exportLockPasswordVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                            contentDescription = if (exportLockPasswordVisible) "Hide password" else "Show password"
                                        )
                                    }
                                },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = "Hide DNS resolver",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Switch(
                                    checked = exportHideResolvers,
                                    onCheckedChange = { exportHideResolvers = it }
                                )
                            }
                            if (exportHideResolvers) {
                                Text(
                                    text = "Resolver addresses will be hidden. Old app versions won't be able to import this profile.",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }

                            // Expiration toggle + days
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = "Set expiration",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Switch(
                                    checked = exportLockExpiry,
                                    onCheckedChange = { exportLockExpiry = it }
                                )
                            }
                            if (exportLockExpiry) {
                                OutlinedTextField(
                                    value = exportLockExpiryDays,
                                    onValueChange = { exportLockExpiryDays = it.filter { c -> c.isDigit() } },
                                    label = { Text("Expires in (days)") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                            }

                            // Allow re-sharing
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = "Allow re-sharing",
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Switch(
                                    checked = exportLockAllowSharing,
                                    onCheckedChange = { exportLockAllowSharing = it }
                                )
                            }

                            // Target device ID
                            OutlinedTextField(
                                value = exportLockDeviceId,
                                onValueChange = { exportLockDeviceId = it },
                                label = { Text("Target Device ID (optional)") },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                            Text(
                                text = "Device ID can be found in Settings on the target device",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(start = 4.dp, top = 4.dp)
                            )
                        }
                    }
                },
                confirmButton = {
                    TextButton(
                        onClick = {
                            val p = exportLockProfile ?: return@TextButton
                            if (exportLockEnabled) {
                                val expiryMs = if (exportLockExpiry) {
                                    val days = exportLockExpiryDays.toLongOrNull() ?: 30
                                    System.currentTimeMillis() + days * 24 * 60 * 60 * 1000L
                                } else 0L
                                if (exportLockMode == "qr") {
                                    viewModel.showQrCodeLocked(p, exportLockPassword, expiryMs, exportLockAllowSharing, exportLockDeviceId, exportHideResolvers)
                                } else {
                                    viewModel.exportProfileLocked(p, exportLockPassword, expiryMs, exportLockAllowSharing, exportLockDeviceId, exportHideResolvers)
                                }
                            } else {
                                if (exportLockMode == "qr") {
                                    viewModel.showQrCode(p, exportHideResolvers)
                                } else {
                                    viewModel.exportProfile(p, exportHideResolvers)
                                }
                            }
                            exportHideResolvers = false
                            exportLockProfile = null
                        },
                        enabled = !exportLockEnabled || exportLockPassword.isNotBlank()
                    ) { Text(if (exportLockMode == "qr") "Share" else "Export") }
                },
                dismissButton = {
                    TextButton(onClick = { exportLockProfile = null }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
                }
            )
        }
    }

    // Password prompt for encrypted export — mirrors the single-profile lock dialog.
    if (showExportAllEncryptedDialog) {
        var pw by remember { mutableStateOf("") }
        var pwVisible by remember { mutableStateOf(false) }
        var profilePw by remember { mutableStateOf("") }
        var profilePwVisible by remember { mutableStateOf(false) }
        var hideRes by remember { mutableStateOf(false) }
        var expiry by remember { mutableStateOf(false) }
        var expiryDays by remember { mutableStateOf("30") }
        var allowSharing by remember { mutableStateOf(false) }
        var deviceId by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { showExportAllEncryptedDialog = false },
            title = { Text("Encrypted Export") },
            text = {
                Column(
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.verticalScroll(rememberScrollState())
                ) {
                    OutlinedTextField(
                        value = pw,
                        onValueChange = { pw = it },
                        label = { Text("Bundle Password") },
                        visualTransformation = if (pwVisible) VisualTransformation.None else PasswordVisualTransformation(),
                        trailingIcon = {
                            IconButton(onClick = { pwVisible = !pwVisible }) {
                                Icon(
                                    imageVector = if (pwVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                    contentDescription = if (pwVisible) "Hide password" else "Show password"
                                )
                            }
                        },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Text(
                        text = "Required to decrypt the bundle on import.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    OutlinedTextField(
                        value = profilePw,
                        onValueChange = { profilePw = it },
                        label = { Text("Per-Config Password (optional)") },
                        visualTransformation = if (profilePwVisible) VisualTransformation.None else PasswordVisualTransformation(),
                        trailingIcon = {
                            IconButton(onClick = { profilePwVisible = !profilePwVisible }) {
                                Icon(
                                    imageVector = if (profilePwVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                    contentDescription = if (profilePwVisible) "Hide password" else "Show password"
                                )
                            }
                        },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Text(
                        text = "Locks each profile after bundle decryption. Leave blank to reuse the bundle password.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(text = "Hide DNS resolver", style = MaterialTheme.typography.bodyMedium)
                        Switch(checked = hideRes, onCheckedChange = { hideRes = it })
                    }
                    if (hideRes) {
                        Text(
                            text = "Resolver addresses will be hidden. Old app versions won't be able to import this bundle.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(text = "Set expiration", style = MaterialTheme.typography.bodyMedium)
                        Switch(checked = expiry, onCheckedChange = { expiry = it })
                    }
                    if (expiry) {
                        OutlinedTextField(
                            value = expiryDays,
                            onValueChange = { expiryDays = it.filter { c -> c.isDigit() } },
                            label = { Text("Expires in (days)") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(text = "Allow re-sharing", style = MaterialTheme.typography.bodyMedium)
                        Switch(checked = allowSharing, onCheckedChange = { allowSharing = it })
                    }
                    OutlinedTextField(
                        value = deviceId,
                        onValueChange = { deviceId = it },
                        label = { Text("Target Device ID (optional)") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Text(
                        text = "Device ID can be found in Settings on the target device",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(start = 4.dp, top = 4.dp)
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        val expiryMs = if (expiry) {
                            val days = expiryDays.toLongOrNull() ?: 30
                            System.currentTimeMillis() + days * 24 * 60 * 60 * 1000L
                        } else 0L
                        viewModel.exportAllProfilesEncrypted(
                            bundlePassword = pw,
                            profilePassword = profilePw,
                            expirationDate = expiryMs,
                            allowSharing = allowSharing,
                            boundDeviceId = deviceId,
                            hideResolvers = hideRes
                        )
                        showExportAllEncryptedDialog = false
                    },
                    enabled = pw.isNotBlank()
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_export_366489)) }
            },
            dismissButton = {
                TextButton(onClick = { showExportAllEncryptedDialog = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // Password prompt for encrypted import (the user pasted a slipnet-bundle-enc:// URI)
    uiState.pendingEncryptedImport?.let { encryptedInput ->
        var pw by remember { mutableStateOf("") }
        var pwVisible by remember { mutableStateOf(false) }
        AlertDialog(
            onDismissRequest = { viewModel.cancelEncryptedImport() },
            title = { Text("Encrypted Bundle") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "This bundle is password-protected. Enter the password to decrypt.",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    OutlinedTextField(
                        value = pw,
                        onValueChange = { pw = it },
                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e)) },
                        singleLine = true,
                        visualTransformation = if (pwVisible) {
                            androidx.compose.ui.text.input.VisualTransformation.None
                        } else {
                            androidx.compose.ui.text.input.PasswordVisualTransformation()
                        },
                        trailingIcon = {
                            IconButton(onClick = { pwVisible = !pwVisible }) {
                                Icon(
                                    if (pwVisible) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                    contentDescription = if (pwVisible) "Hide password" else "Show password"
                                )
                            }
                        },
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = { viewModel.parseImportConfig(encryptedInput, pw) },
                    enabled = pw.isNotEmpty()
                ) { Text("Decrypt") }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.cancelEncryptedImport() }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // Import input dialog
    if (showImportDialog) {
        AlertDialog(
            onDismissRequest = {
                showImportDialog = false
                importText = ""
            },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_profiles_d47ce9)) },
            text = {
                Column(
                    modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState()),
                    verticalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    Text(
                        text = "Paste a link or WireGuard configuration. Speed is unknown until tested:",
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        text = "VLESS: REALITY / XHTTP / WebSocket • Hysteria2 • WireGuard (.conf)\nLegacy slipnet:// profiles are also supported.",
                        style = MaterialTheme.typography.bodySmall
                    )
                    OutlinedTextField(
                        value = importText,
                        onValueChange = { importText = it },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(120.dp),
                        placeholder = { Text("rovunet://...  vless://...\nhysteria2://...  hy2://...\n[Interface] (WireGuard)") },
                        singleLine = false,
                        maxLines = 5
                    )
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceEvenly
                    ) {
                        TextButton(
                            onClick = {
                                clipboardManager.getText()?.text?.let { clip ->
                                    if (clip.isNotBlank()) importText = clip
                                }
                            }
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_paste_f3380f)) }
                        TextButton(
                            onClick = {
                                importFileLauncher.launch(arrayOf("text/plain", "*/*"))
                                showImportDialog = false
                                importText = ""
                            }
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_file_50009c)) }
                        TextButton(
                            onClick = {
                                showImportDialog = false
                                importText = ""
                                qrScanLauncher.launch(ScanOptions().apply {
                                    setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                                    setPrompt("")
                                    setBeepEnabled(false)
                                    setCaptureActivity(QrScannerActivity::class.java)
                                })
                            }
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_qr_code_5a7791)) }
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        if (importText.isNotBlank()) {
                            viewModel.parseImportConfig(importText)
                            showImportDialog = false
                            importText = ""
                        }
                    },
                    enabled = importText.isNotBlank()
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_import_2cff9b)) }
            },
            dismissButton = {
                TextButton(
                    onClick = {
                        showImportDialog = false
                        importText = ""
                    }
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // First launch About dialog
    if (uiState.showFirstLaunchAbout) {
        AlertDialog(
            onDismissRequest = { viewModel.dismissFirstLaunchAbout() },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_welcome_to_rovunet_20056d)) },
            text = { AboutDialogContent() },
            confirmButton = {
                TextButton(onClick = { viewModel.dismissFirstLaunchAbout() }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_get_started_983f31))
                }
            }
        )
    }

    // Update available dialog
    uiState.availableUpdate?.let { update ->
        val context = LocalContext.current
        AlertDialog(
            onDismissRequest = { viewModel.dismissUpdate() },
            title = { Text("Update Available") },
            text = {
                Text("Version ${update.versionName} is available. You are on ${BuildConfig.VERSION_NAME}.")
            },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.dismissUpdate()
                    val intent = android.content.Intent(
                        android.content.Intent.ACTION_VIEW,
                        android.net.Uri.parse(update.downloadUrl)
                    )
                    context.startActivity(intent)
                }) {
                    Text("Download")
                }
            },
            dismissButton = {
                Row {
                    TextButton(onClick = { viewModel.skipUpdate() }) {
                        Text("Skip")
                    }
                    TextButton(onClick = { viewModel.dismissUpdate() }) {
                        Text("Later")
                    }
                }
            }
        )
    }
}

// ── ChainListItem ───────────────────────────────────────────────────────

@Composable
private fun ChainListItem(
    chain: ProfileChain,
    profiles: List<ServerProfile>,
    isSelected: Boolean = false,
    isConnected: Boolean = false,
    onClick: () -> Unit,
    onEditClick: () -> Unit,
    onDeleteClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val layerNames = chain.profileIds.mapNotNull { id ->
        profiles.find { it.id == id }?.let { "${it.name} (${it.tunnelType.displayName})" }
    }

    val borderColor = when {
        isConnected -> ConnectedGreen
        isSelected -> MaterialTheme.colorScheme.primary
        else -> null
    }
    val cardShape = RoundedCornerShape(12.dp)

    val containerColor = when {
        isConnected -> ConnectedGreen.copy(alpha = 0.08f)
        isSelected -> MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f)
        else -> MaterialTheme.colorScheme.surface
    }

    Card(
        modifier = modifier
            .fillMaxWidth()
            .then(
                if (borderColor != null) Modifier.border(2.dp, borderColor, cardShape)
                else Modifier
            )
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(containerColor = containerColor),
        shape = cardShape,
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 16.dp, end = 4.dp, top = 12.dp, bottom = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(2.dp)
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = chain.name,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(1f, fill = false)
                    )
                    if (isConnected) {
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_connected_229655),
                            style = MaterialTheme.typography.labelSmall,
                            color = ConnectedGreen,
                            modifier = Modifier
                                .background(
                                    ConnectedGreen.copy(alpha = 0.15f),
                                    RoundedCornerShape(4.dp)
                                )
                                .padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    } else if (isSelected) {
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_selected_57fd7a),
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier
                                .background(
                                    MaterialTheme.colorScheme.primaryContainer,
                                    RoundedCornerShape(4.dp)
                                )
                                .padding(horizontal = 6.dp, vertical = 2.dp)
                        )
                    }
                }
                Text(
                    text = layerNames.joinToString(" \u2192 "),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = "${chain.profileIds.size}-layer chain",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }

            Row(verticalAlignment = Alignment.CenterVertically) {
                IconButton(
                    onClick = onEditClick,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Edit,
                        contentDescription = "Edit chain",
                        modifier = Modifier.size(18.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                IconButton(
                    onClick = onDeleteClick,
                    enabled = !isConnected,
                    modifier = Modifier.size(36.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Delete,
                        contentDescription = "Delete chain",
                        modifier = Modifier.size(18.dp),
                        tint = if (!isConnected)
                            MaterialTheme.colorScheme.onSurfaceVariant
                        else
                            MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.38f)
                    )
                }
            }
        }
    }
}

// ── ConnectionStatusStrip ───────────────────────────────────────────────

@Composable
private fun ConnectionStatusStrip(
    connectionState: ConnectionState,
    activeProfile: ServerProfile?,
    activeChain: ProfileChain? = null,
    isProxyOnly: Boolean,
    snowflakeBootstrapProgress: Int,
    uploadSpeed: Long = 0,
    downloadSpeed: Long = 0,
    totalUpload: Long = 0,
    totalDownload: Long = 0,
    sleepTimerRemainingSeconds: Int = 0,
    onCancelSleepTimer: () -> Unit = {},
    dnsWarning: String? = null,
    dnsPoolScan: app.slipnet.tunnel.DnsPoolScanState = app.slipnet.tunnel.DnsPoolScanState(),
    modifier: Modifier = Modifier
) {
    val isConnected = connectionState is ConnectionState.Connected
    val isConnecting = connectionState is ConnectionState.Connecting ||
            connectionState is ConnectionState.Disconnecting
    val isError = connectionState is ConnectionState.Error
    val showTorProgress = connectionState is ConnectionState.Connecting &&
            snowflakeBootstrapProgress in 0..99
    val showDnsPoolProgress = dnsPoolScan.isRunning && dnsPoolScan.total > 0

    val statusColor by animateColorAsState(
        targetValue = when {
            isConnected -> ConnectedGreen
            isConnecting -> ConnectingOrange
            isError -> DisconnectedRed
            else -> MaterialTheme.colorScheme.outline
        },
        animationSpec = tween(300),
        label = "statusColor"
    )

    Surface(
        modifier = modifier.fillMaxWidth(),
        tonalElevation = 2.dp,
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Status indicator dot
                Box(
                    modifier = Modifier
                        .size(10.dp)
                        .clip(CircleShape)
                        .background(statusColor)
                )

                Spacer(modifier = Modifier.width(12.dp))

                // Status text + profile name
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = when {
                            isConnected && isProxyOnly -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_proxy_active_26396f)
                            isConnected -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_connected_229655)
                            connectionState is ConnectionState.Connecting -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_connecting_5f04ae)
                            connectionState is ConnectionState.Disconnecting -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_disconnecting_cb5a85)
                            isError -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_connection_failed_42aa4e)
                            else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_not_connected_87d82c)
                        },
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        color = if (isConnected || isConnecting) statusColor
                        else MaterialTheme.colorScheme.onBackground
                    )
                    Text(
                        text = when {
                            isConnected && connectionState is ConnectionState.Connected ->
                                connectionState.chainName ?: ProfileCollections.name(connectionState.profile)
                            isError && connectionState is ConnectionState.Error ->
                                connectionState.message
                            activeChain != null -> activeChain.name
                            activeProfile != null -> ProfileCollections.name(activeProfile)
                            else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_no_profile_selected_d87214)
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = if (isError) DisconnectedRed
                        else MaterialTheme.colorScheme.onSurfaceVariant,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }

            // Traffic stats: live speed + totals when connected, session totals when disconnected
            AnimatedVisibility(
                visible = isConnected,
                enter = expandVertically(animationSpec = tween(300)) + fadeIn(animationSpec = tween(300)),
                exit = shrinkVertically(animationSpec = tween(200)) + fadeOut(animationSpec = tween(200))
            ) {
                Column {
                    Spacer(modifier = Modifier.height(8.dp))
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(start = 22.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        // Live speed
                        Text(
                            text = "\u2191 ${TrafficStats.formatBytes(uploadSpeed)}/s",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.width(16.dp))
                        Text(
                            text = "\u2193 ${TrafficStats.formatBytes(downloadSpeed)}/s",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.weight(1f))
                        // Session totals
                        Text(
                            text = "\u2191 ${TrafficStats.formatBytes(totalUpload)}  \u2193 ${TrafficStats.formatBytes(totalDownload)}",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)
                        )
                    }
                }
            }

            // DNS warning
            AnimatedVisibility(
                visible = isConnected && dnsWarning != null,
                enter = expandVertically(animationSpec = tween(300)) + fadeIn(animationSpec = tween(300)),
                exit = shrinkVertically(animationSpec = tween(200)) + fadeOut(animationSpec = tween(200))
            ) {
                Text(
                    text = dnsWarning ?: "",
                    style = MaterialTheme.typography.labelSmall,
                    color = Color(0xFFFF9800),
                    modifier = Modifier.padding(start = 22.dp, top = 6.dp)
                )
            }

            // Sleep timer countdown
            AnimatedVisibility(
                visible = isConnected && sleepTimerRemainingSeconds > 0,
                enter = expandVertically(animationSpec = tween(300)) + fadeIn(animationSpec = tween(300)),
                exit = shrinkVertically(animationSpec = tween(200)) + fadeOut(animationSpec = tween(200))
            ) {
                Column {
                    Spacer(modifier = Modifier.height(8.dp))
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(start = 22.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            Icons.Default.Timer,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = "Sleep: ${formatCountdown(sleepTimerRemainingSeconds)}",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.weight(1f))
                        TextButton(
                            onClick = onCancelSleepTimer,
                            contentPadding = PaddingValues(horizontal = 8.dp, vertical = 0.dp),
                            modifier = Modifier.height(28.dp)
                        ) {
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e),
                                style = MaterialTheme.typography.labelSmall
                            )
                        }
                    }
                }
            }

            // DNS pool scan progress (per-profile auto-pick).
            // Bar always starts at 0 each scan and animates up to the real
            // tested/total fraction — the Animatable is keyed to the scan's
            // running flag so it resets to 0 every time a scan begins, no
            // matter how many results have already landed in the state flow.
            AnimatedVisibility(
                visible = showDnsPoolProgress,
                enter = expandVertically(animationSpec = tween(300)) + fadeIn(animationSpec = tween(300)),
                exit = shrinkVertically(animationSpec = tween(200)) + fadeOut(animationSpec = tween(200))
            ) {
                val targetProgress = if (dnsPoolScan.total > 0)
                    dnsPoolScan.probed.toFloat() / dnsPoolScan.total
                else 0f
                val progressAnim = remember(dnsPoolScan.isRunning) { Animatable(0f) }
                LaunchedEffect(dnsPoolScan.isRunning, targetProgress) {
                    if (dnsPoolScan.isRunning) {
                        progressAnim.animateTo(targetProgress, tween(durationMillis = 400))
                    }
                }
                Column {
                    Spacer(modifier = Modifier.height(12.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        LinearProgressIndicator(
                            progress = { progressAnim.value },
                            modifier = Modifier
                                .weight(1f)
                                .height(6.dp)
                                .clip(RoundedCornerShape(3.dp)),
                            color = ConnectingOrange,
                            trackColor = ConnectingOrange.copy(alpha = 0.2f),
                        )
                        Spacer(modifier = Modifier.width(12.dp))
                        Text(
                            text = "DNS ${dnsPoolScan.probed}/${dnsPoolScan.total} · ${dnsPoolScan.alive} alive · ${dnsPoolScan.verified}✓",
                            style = MaterialTheme.typography.labelSmall,
                            color = ConnectingOrange
                        )
                    }
                }
            }

            // Tor bootstrap progress
            AnimatedVisibility(
                visible = showTorProgress,
                enter = expandVertically(animationSpec = tween(300)) + fadeIn(animationSpec = tween(300)),
                exit = shrinkVertically(animationSpec = tween(200)) + fadeOut(animationSpec = tween(200))
            ) {
                Column {
                    Spacer(modifier = Modifier.height(12.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        LinearProgressIndicator(
                            progress = { snowflakeBootstrapProgress / 100f },
                            modifier = Modifier
                                .weight(1f)
                                .height(6.dp)
                                .clip(RoundedCornerShape(3.dp)),
                            color = ConnectingOrange,
                            trackColor = ConnectingOrange.copy(alpha = 0.2f),
                        )
                        Spacer(modifier = Modifier.width(12.dp))
                        Text(
                            text = "Tor: $snowflakeBootstrapProgress%",
                            style = MaterialTheme.typography.labelSmall,
                            color = ConnectingOrange
                        )
                    }
                }
            }
        }
    }
}

private fun formatCountdown(totalSeconds: Int): String {
    return "%d:%02d".format(totalSeconds / 60, totalSeconds % 60)
}

// ── Connect FAB ─────────────────────────────────────────────────────────

@Composable
private fun ConnectFab(
    connectionState: ConnectionState,
    hasProfile: Boolean,
    snowflakeBootstrapProgress: Int,
    onToggleConnection: () -> Unit,
    modifier: Modifier = Modifier
) {
    val isConnected = connectionState is ConnectionState.Connected
    val isConnecting = connectionState is ConnectionState.Connecting ||
            connectionState is ConnectionState.Disconnecting

    val statusColor by animateColorAsState(
        targetValue = when {
            isConnected -> ConnectedGreen
            isConnecting -> ConnectingOrange
            connectionState is ConnectionState.Error -> DisconnectedRed
            else -> MaterialTheme.colorScheme.primary
        },
        animationSpec = tween(300),
        label = "connectFabColor"
    )

    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseScale by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = 1.08f,
        animationSpec = infiniteRepeatable(
            animation = tween(1000, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "pulseScale"
    )

    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = modifier
    ) {
        FloatingActionButton(
            onClick = onToggleConnection,
            containerColor = statusColor,
            modifier = Modifier
                .size(56.dp)
                .then(if (isConnecting) Modifier.scale(pulseScale) else Modifier),
        ) {
            Icon(
                imageVector = Icons.Default.PowerSettingsNew,
                contentDescription = if (isConnected) "Disconnect" else "Connect",
                tint = when {
                    isConnected || isConnecting -> Color.White
                    hasProfile -> MaterialTheme.colorScheme.onPrimary
                    else -> MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.5f)
                }
            )
        }
    }
}

// ── FAB menu option ─────────────────────────────────────────────────────

@Composable
private fun AddMenuOption(
    icon: ImageVector,
    title: String,
    description: String,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(28.dp)
        )
        Spacer(Modifier.width(16.dp))
        Column {
            Text(
                text = rv(title),
                style = MaterialTheme.typography.titleSmall
            )
            if (description.isNotEmpty()) {
                Text(
                    text = rv(description),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
private fun AddMenuGridItem(
    icon: ImageVector,
    title: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(12.dp))
            .clickable(onClick = onClick)
            .padding(vertical = 12.dp, horizontal = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(28.dp)
        )
        Text(
            text = rv(title),
            style = MaterialTheme.typography.labelMedium,
            maxLines = 1
        )
    }
}

// ── Share helpers ────────────────────────────────────────────────────────

private fun shareApk(context: Context) {
    try {
        val appInfo = context.applicationInfo
        val sharedDir = java.io.File(context.cacheDir, "shared")
        sharedDir.mkdirs()

        val splits = appInfo.splitSourceDirs
        if (splits.isNullOrEmpty()) {
            val sourceApk = java.io.File(appInfo.sourceDir)
            val sharedApk = java.io.File(sharedDir, "Rovunet-v${app.slipnet.BuildConfig.VERSION_NAME}.apk")
            sourceApk.copyTo(sharedApk, overwrite = true)

            val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", sharedApk)
            val intent = Intent(Intent.ACTION_SEND).apply {
                type = "application/vnd.android.package-archive"
                putExtra(Intent.EXTRA_STREAM, uri)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }
            context.startActivity(Intent.createChooser(intent, "Share Rovunet"))
        } else {
            val apksFile = java.io.File(sharedDir, "Rovunet-v${app.slipnet.BuildConfig.VERSION_NAME}.apks")
            java.util.zip.ZipOutputStream(apksFile.outputStream().buffered()).use { zip ->
                val allApks = listOf(appInfo.sourceDir) + splits
                for (path in allApks) {
                    val file = java.io.File(path)
                    zip.putNextEntry(java.util.zip.ZipEntry(file.name))
                    file.inputStream().buffered().use { it.copyTo(zip) }
                    zip.closeEntry()
                }
            }

            val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apksFile)
            val intent = Intent(Intent.ACTION_SEND).apply {
                type = "application/zip"
                putExtra(Intent.EXTRA_STREAM, uri)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }
            context.startActivity(Intent.createChooser(intent, "Share Rovunet"))
        }
    } catch (_: Exception) { }
}
