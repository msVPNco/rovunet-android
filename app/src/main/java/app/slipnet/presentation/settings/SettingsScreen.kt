package app.slipnet.presentation.settings

import app.slipnet.domain.model.VpnMtu
import app.slipnet.R
import android.content.Intent
import android.net.Uri
import android.os.PowerManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.material3.CircularProgressIndicator
import android.provider.Settings
import app.slipnet.BuildConfig
import app.slipnet.presentation.common.components.AboutDialogContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Apps
import androidx.compose.material.icons.filled.BatteryAlert
import androidx.compose.material.icons.filled.Block
import androidx.compose.material.icons.filled.BugReport
import androidx.compose.material.icons.filled.CallSplit
import androidx.compose.material.icons.filled.Compress
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Timer
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Help
import androidx.compose.material.icons.filled.Hub
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Language
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Public
import androidx.compose.material.icons.filled.TravelExplore
import androidx.compose.material.icons.filled.SettingsEthernet
import androidx.compose.material.icons.filled.RestartAlt
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.local.datastore.DnsWorkerMode
import app.slipnet.data.local.datastore.DomainRoutingMode
import app.slipnet.data.local.datastore.SplitTunnelingMode
import app.slipnet.data.local.datastore.SshCipher
import app.slipnet.tunnel.GeoBypassCountry
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.filled.AltRoute
import androidx.compose.material.icons.filled.AutoAwesome
import androidx.compose.material.icons.filled.Dns
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.Shuffle
import androidx.compose.material.icons.filled.UploadFile
import androidx.compose.material.icons.filled.Lan
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.Numbers
import androidx.compose.material.icons.filled.VerifiedUser
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.input.KeyboardType
import java.net.Inet4Address
import java.net.NetworkInterface
import kotlin.math.roundToInt

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onNavigateBack: () -> Unit,
    onNavigateToScanner: (() -> Unit)? = null,
    onNavigateToAppSelector: () -> Unit = {},
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    val uiState by viewModel.uiState.collectAsState()
    val effectiveMtu = VpnMtu.effective(uiState.vpnMtu, uiState.blockIpv6)
    val scrollState = rememberScrollState()

    var showStabilityReport by remember { mutableStateOf(false) }
    var showTrustResetDialog by remember { mutableStateOf(false) }
    var showDarkModeDialog by remember { mutableStateOf(false) }
    var showSshCipherDialog by remember { mutableStateOf(false) }
    var showSplitModeDialog by remember { mutableStateOf(false) }
    var showDomainRoutingModeDialog by remember { mutableStateOf(false) }
    var showDomainManagementDialog by remember { mutableStateOf(false) }
    var showGeoBypassCountryDialog by remember { mutableStateOf(false) }
    var showAppBypassHelp by remember { mutableStateOf(false) }
    var showAboutDialog by remember { mutableStateOf(false) }
    var showRemoteDnsDialog by remember { mutableStateOf(false) }
    var showGlobalResolverDialog by remember { mutableStateOf(false) }
    var showDnsPoolDialog by remember { mutableStateOf(false) }
    var showMtuDialog by remember { mutableStateOf(false) }
    var showBandwidthLimitDialog by remember { mutableStateOf(false) }
    var showDnsWorkerDialog by remember { mutableStateOf(false) }
    var showResetSettingsDialog by remember { mutableStateOf(false) }

    // Proxy settings - local state for text fields to avoid cursor jumps from async DataStore round-trip
    var proxyPort by remember { mutableStateOf(uiState.proxyListenPort.toString()) }
    var httpProxyPort by remember { mutableStateOf(uiState.httpProxyPort.toString()) }
    var proxyAuthUsername by remember { mutableStateOf(uiState.proxyAuthUsername) }
    var replacingProxyPassword by remember { mutableStateOf(false) }
    var proxyReplacementFailed by remember { mutableStateOf(false) }
    var proxyAuthPassword by remember { mutableStateOf(uiState.proxyAuthPassword) }

    // Sync local state when DataStore values load (initial default → actual saved value)
    LaunchedEffect(uiState.proxyListenPort) {
        proxyPort = uiState.proxyListenPort.toString()
    }
    LaunchedEffect(uiState.httpProxyPort) {
        httpProxyPort = uiState.httpProxyPort.toString()
    }
    LaunchedEffect(uiState.proxyAuthUsername) {
        if (uiState.proxyAuthUsername != proxyAuthUsername) proxyAuthUsername = uiState.proxyAuthUsername
    }
    LaunchedEffect(uiState.proxyAuthPassword) {
        if (uiState.proxyAuthPassword != proxyAuthPassword) proxyAuthPassword = uiState.proxyAuthPassword
    }

    val addressOptions = getAddressOptions()

    // Battery optimization state
    val context = LocalContext.current
    var isBatteryOptimized by remember { mutableStateOf(true) }
    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                val pm = context.getSystemService(android.content.Context.POWER_SERVICE) as PowerManager
                isBatteryOptimized = !pm.isIgnoringBatteryOptimizations(context.packageName)
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    val navBarPadding = WindowInsets.navigationBars.asPaddingValues()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_settings_74a883)) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = resources.getString(R.string.rv_back_76900f))
                    }
                },
                actions = {
                    IconButton(onClick = { showAboutDialog = true }) {
                        Icon(Icons.Default.Help, contentDescription = resources.getString(R.string.rv_about_4efca0))
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = Color.Transparent
                )
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
        contentWindowInsets = WindowInsets(0, 0, 0, 0)
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .verticalScroll(scrollState)
                .padding(
                    start = 16.dp,
                    end = 16.dp,
                    top = 12.dp,
                    bottom = 12.dp + navBarPadding.calculateBottomPadding()
                ),
            verticalArrangement = Arrangement.spacedBy(20.dp)
        ) {
            // Donate Card
            app.slipnet.presentation.common.components.TelegramCommunityLink()
            app.slipnet.presentation.common.components.SupportRovunetCard()

            // Connection Settings
            SettingsSection(title = resources.getString(R.string.settings_connection_6512ee)) {
                SwitchSettingItem(
                    icon = Icons.Default.PowerSettingsNew,
                    title = resources.getString(R.string.settings_auto_connect_on_boot_26ee01),
                    description = resources.getString(R.string.settings_automatically_connect_when_device_starts_27c102),
                    checked = uiState.autoConnectOnBoot,
                    onCheckedChange = { viewModel.setAutoConnectOnBoot(it) }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.SettingsEthernet,
                    title = resources.getString(R.string.settings_proxy_only_mode_a0147a),
                    description = resources.getString(R.string.settings_expose_socks5_proxy_without_creating_vpn_tunnel_d648ca),
                    checked = uiState.proxyOnlyMode,
                    onCheckedChange = { viewModel.setProxyOnlyMode(it) }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Shield,
                    title = resources.getString(R.string.settings_kill_switch_889a13),
                    description = resources.getString(R.string.settings_block_all_traffic_if_vpn_connection_drops_93874d),
                    checked = uiState.killSwitch,
                    onCheckedChange = { viewModel.setKillSwitch(it) }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Sync,
                    title = resources.getString(R.string.settings_auto_reconnect_9202fa),
                    description = resources.getString(R.string.settings_automatically_reconnect_if_vpn_drops_unexpectedly_8fae83),
                    checked = uiState.autoReconnect,
                    onCheckedChange = { viewModel.setAutoReconnect(it) }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Notifications,
                    title = resources.getString(R.string.settings_notification_traffic_counter_9114b0),
                    description = resources.getString(R.string.settings_show_upload_download_speed_and_data_usage_in_notification_fabedd),
                    checked = uiState.showNotificationTraffic,
                    onCheckedChange = { viewModel.setShowNotificationTraffic(it) }
                )

                SettingsDivider()

                StepperSettingItem(
                    icon = Icons.Default.Timer,
                    title = resources.getString(R.string.settings_sleep_timer_e90613),
                    description = resources.getString(R.string.settings_auto_disconnect_after_a_set_time_238066),
                    value = uiState.sleepTimerMinutes,
                    step = 5,
                    range = 0..120,
                    valueFormatter = { if (it == 0) resources.getString(R.string.settings_off_e3de5a) else resources.getString(R.string.settings_1_d_min_32b259, it) },
                    onValueChange = { viewModel.setSleepTimerMinutes(it) }
                )

                SettingsDivider()

                ClickableSettingItem(
                    icon = Icons.Default.BatteryAlert,
                    title = resources.getString(R.string.settings_battery_optimization_54d1a1),
                    description = if (isBatteryOptimized) resources.getString(R.string.settings_not_exempted_vpn_may_disconnect_in_background_4011b8)
                                  else resources.getString(R.string.settings_exempted_vpn_will_run_reliably_in_background_884ac3),
                    onClick = {
                        if (isBatteryOptimized) {
                            try {
                                val intent = Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                                    data = Uri.parse("package:${context.packageName}")
                                }
                                context.startActivity(intent)
                            } catch (_: Exception) {
                                val intent = Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS)
                                context.startActivity(intent)
                            }
                        } else {
                            // Already exempted — open system battery settings so user can change it
                            val intent = Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS)
                            context.startActivity(intent)
                        }
                    }
                )
            }

            // Tools Section
            if (onNavigateToScanner != null) {
                SettingsSection(title = resources.getString(R.string.settings_tools_4fa8cc)) {
                    ClickableSettingItem(
                        icon = Icons.Default.Search,
                        title = resources.getString(R.string.settings_dns_resolver_scanner_f68178),
                        description = resources.getString(R.string.settings_find_working_dns_resolvers_for_your_profiles_c0f246),
                        onClick = onNavigateToScanner
                    )
                }
            }

            // Proxy Settings
            SettingsSection(
                title = resources.getString(R.string.settings_proxy_settings_44f84e),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                AddressSettingItem(
                    value = uiState.proxyListenAddress,
                    options = addressOptions,
                    onValueChange = {
                        viewModel.setProxyListenAddress(it)
                    }
                )

                SettingsDivider()

                val portsConflict = proxyPort.toIntOrNull() == httpProxyPort.toIntOrNull() && proxyPort.isNotBlank()

                TextFieldSettingItem(
                    icon = Icons.Default.Numbers,
                    title = resources.getString(R.string.settings_listen_port_f56d6c),
                    value = proxyPort,
                    placeholder = "10880",
                    supportingText = if (portsConflict) resources.getString(R.string.settings_must_differ_from_http_proxy_port_c94839) else resources.getString(R.string.settings_local_socks5_proxy_port_3a3531),
                    isError = portsConflict,
                    keyboardType = KeyboardType.Number,
                    onValueChange = { text ->
                        proxyPort = text
                        text.toIntOrNull()?.let { viewModel.setProxyListenPort(it) }
                    }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Lock,
                    title = resources.getString(R.string.settings_proxy_authentication_8c6941),
                    description = if (uiState.proxyAuthEnabled) resources.getString(R.string.settings_username_password_required_to_use_the_proxy_f95aab)
                        else resources.getString(R.string.settings_any_app_can_use_the_local_proxy_without_credentials_5a4d43),
                    checked = uiState.proxyAuthEnabled,
                    onCheckedChange = { viewModel.setProxyAuthEnabled(it) }
                )

                if (uiState.proxyAuthEnabled) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 16.dp, vertical = 8.dp),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = proxyAuthUsername,
                            onValueChange = { text ->
                                proxyAuthUsername = text
                                viewModel.setProxyAuthUsername(text)
                            },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_username_e3b89e)) },
                            singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text),
                            modifier = Modifier.weight(1f)
                        )
                        OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = proxyAuthPassword,
                            onValueChange = { text ->
                                proxyAuthPassword = text
                                proxyReplacementFailed = false
                                if (!uiState.proxyPasswordUnavailable) viewModel.setProxyAuthPassword(text) { proxyReplacementFailed = !it }
                            },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e)) },
                            singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Text),
                            modifier = Modifier.weight(1f)
                        )
                    }
                    if (uiState.proxyPasswordUnavailable || proxyReplacementFailed) {
                        Column(Modifier.padding(horizontal = 16.dp)) {
                            Text(resources.getString(if (proxyReplacementFailed) R.string.profile_restore_failed else R.string.proxy_password_unavailable))
                            TextButton(enabled = !replacingProxyPassword && proxyAuthPassword.isNotBlank(), onClick = {
                                replacingProxyPassword = true
                                viewModel.replaceUnreadableProxyPassword(proxyAuthPassword) {
                                    proxyReplacementFailed = !it
                                    replacingProxyPassword = false
                                }
                            }) { Text(resources.getString(R.string.profile_restore_action)) }
                        }
                    }
                } else {
                    Text(
                        text = resources.getString(R.string.settings_keeping_authentication_enabled_is_recommended_for_security_758c1c),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp)
                    )
                }

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Lan,
                    title = resources.getString(R.string.settings_http_proxy_d87f38),
                    description = resources.getString(R.string.settings_enable_http_proxy_for_devices_that_don_t_support_socks5_fa1708),
                    checked = uiState.httpProxyEnabled,
                    onCheckedChange = { viewModel.setHttpProxyEnabled(it) }
                )

                if (uiState.httpProxyEnabled) {
                    SettingsDivider()

                    TextFieldSettingItem(
                        icon = Icons.Default.Numbers,
                        title = resources.getString(R.string.settings_http_proxy_port_39cdba),
                        value = httpProxyPort,
                        placeholder = "8080",
                        supportingText = if (portsConflict) resources.getString(R.string.settings_must_differ_from_socks5_listen_port_12e622) else resources.getString(R.string.settings_local_http_proxy_port_b86325),
                        isError = portsConflict,
                        keyboardType = KeyboardType.Number,
                        onValueChange = { text ->
                            httpProxyPort = text
                            text.toIntOrNull()?.let { viewModel.setHttpProxyPort(it) }
                        }
                    )
                }

                if (uiState.proxyListenAddress == "0.0.0.0") {
                    HotspotInfoCard(
                        socksPort = uiState.proxyListenPort,
                        httpProxyEnabled = uiState.httpProxyEnabled,
                        httpProxyPort = uiState.httpProxyPort
                    )
                } else {
                    LocalProxyInfoCard(
                        listenAddress = uiState.proxyListenAddress,
                        socksPort = uiState.proxyListenPort,
                        httpProxyEnabled = uiState.httpProxyEnabled,
                        httpProxyPort = uiState.httpProxyPort
                    )
                }
            }

            // DNS Settings
            SettingsSection(
                title = "DNS",
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                // The override only actually applies when the toggle is on AND
                // the IP list is non-empty (matches SlipNetVpnService's
                // `parsedGlobalResolvers().takeIf { it.isNotEmpty() }`). When
                // active, the pool scan is short-circuited in applyDnsPoolIfEnabled.
                val overrideActive = uiState.globalResolverEnabled &&
                    uiState.globalResolverList.isNotBlank()

                // Global resolver override
                SwitchSettingItem(
                    icon = Icons.Default.AltRoute,
                    title = resources.getString(R.string.settings_global_resolver_override_f17d7e),
                    description = if (uiState.globalResolverEnabled) {
                        uiState.globalResolverList.ifBlank { resources.getString(R.string.settings_no_ips_set_tap_edit_ips_below_e8c619) }
                    } else {
                        resources.getString(R.string.settings_force_a_fixed_resolver_list_across_all_profiles_d261d8)
                    },
                    checked = uiState.globalResolverEnabled,
                    onCheckedChange = { viewModel.setGlobalResolverEnabled(it) }
                )
                if (uiState.globalResolverEnabled) {
                    IndentedSettingItem(
                        icon = Icons.Default.Edit,
                        title = resources.getString(R.string.settings_edit_ips_d3241d),
                        description = uiState.globalResolverList.ifBlank { resources.getString(R.string.settings_tap_to_set_resolver_ips_c8fb52) },
                        onClick = { showGlobalResolverDialog = true }
                    )
                }

                // DNS pool — muted when the override is actually active
                SwitchSettingItem(
                    icon = Icons.Default.AutoAwesome,
                    title = resources.getString(R.string.settings_dns_pool_348ea2),
                    description = resources.getString(R.string.settings_auto_pick_the_fastest_resolvers_on_each_connect_3e7a2d),
                    checked = uiState.dnsPoolEnabled,
                    onCheckedChange = { viewModel.setDnsPoolEnabled(it) },
                    inactive = overrideActive
                )
                if (uiState.dnsPoolEnabled) {
                    IndentedSettingItem(
                        icon = Icons.Default.Edit,
                        title = resources.getString(R.string.settings_edit_pool_40a09a),
                        description = resources.getString(R.string.settings_tap_to_manage_the_candidate_list_e307d1),
                        onClick = { showDnsPoolDialog = true },
                        inactive = overrideActive
                    )
                    SwitchSettingItem(
                        icon = Icons.Default.VerifiedUser,
                        title = resources.getString(R.string.settings_http_ssh_verification_f2924f),
                        description = if (uiState.dnsPoolFullVerification)
                            resources.getString(R.string.settings_verifies_real_traffic_through_tunnel_18s_timeout_per_resolver_62e86e)
                        else
                            resources.getString(R.string.settings_handshake_only_faster_scan_10s_timeout_per_resolver_20d485),
                        checked = uiState.dnsPoolFullVerification,
                        onCheckedChange = { viewModel.setDnsPoolFullVerification(it) },
                        inactive = overrideActive
                    )
                }

                // Conflict notice — only when override is *actually* active
                if (overrideActive && uiState.dnsPoolEnabled) {
                    InfoNoticeRow(
                        text = resources.getString(R.string.settings_override_active_pool_is_ignored_5f6790)
                    )
                }

                // Remote DNS server (always shown — system-level fallback)
                ClickableSettingItem(
                    icon = Icons.Default.Dns,
                    title = resources.getString(R.string.settings_remote_dns_server_ded223),
                    description = if (uiState.remoteDnsMode == "custom") {
                        val primary = uiState.customRemoteDns.ifBlank { "8.8.8.8" }
                        val fallback = uiState.customRemoteDnsFallback.ifBlank { "1.1.1.1" }
                        resources.getString(R.string.settings_custom_1_s_2_s_0600b2, primary, fallback)
                    } else {
                        resources.getString(R.string.settings_default_8_8_8_8_1_1_1_1_d8be7c)
                    },
                    onClick = { showRemoteDnsDialog = true }
                )

                SettingsDivider()

                ClickableSettingItem(
                    icon = Icons.Default.Hub,
                    title = resources.getString(R.string.settings_dns_workers_5d7ce3),
                    description = buildString {
                        append(resources.getString(R.string.settings_1_s_dnstt_slipstream_ssh_always_uses_5_0f6c3e, dnsWorkerName(resources, uiState.dnsWorkerMode)))
                        if (uiState.dnsWorkerMode.poolSize >= 3) append(resources.getString(R.string.settings_may_increase_data_usage_28e00b))
                    },
                    onClick = { showDnsWorkerDialog = true }
                )
            }

            // Network Settings
            SettingsSection(
                title = resources.getString(R.string.rv_network_1744b9),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                SwitchSettingItem(
                    icon = Icons.Default.Block,
                    title = resources.getString(R.string.settings_disable_quic_5d846d),
                    description = resources.getString(R.string.settings_block_quic_protocol_to_force_tcp_faster_page_loads_over_tunnels_2ff978),
                    checked = uiState.disableQuic,
                    onCheckedChange = { viewModel.setDisableQuic(it) }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Block,
                    title = resources.getString(R.string.block_ipv6_traffic),
                    description = resources.getString(R.string.block_ipv6_traffic_hint),
                    checked = uiState.blockIpv6,
                    onCheckedChange = { viewModel.setBlockIpv6(it) }
                )
                SettingsDivider()

                ClickableSettingItem(
                    icon = Icons.Default.SettingsEthernet,
                    title = resources.getString(R.string.settings_vpn_mtu_ea6cc3),
                    description = if (uiState.vpnMtu != effectiveMtu) {
                        resources.getString(R.string.mtu_preference_with_ipv6, uiState.vpnMtu, effectiveMtu)
                    } else resources.getString(R.string.settings_vpn_packet_size_1_d_lower_values_improve_compatibility_on_mobile__e6c041, effectiveMtu),
                    onClick = { showMtuDialog = true }
                )

                SettingsDivider()

                ClickableSettingItem(
                    icon = Icons.Default.Speed,
                    title = resources.getString(R.string.settings_bandwidth_limit_b0796c),
                    description = run {
                        val ul = if (uiState.uploadLimitKbps > 0) resources.getString(R.string.settings_1_d_kb_s_c9c342, uiState.uploadLimitKbps) else resources.getString(R.string.settings_unlimited_b8bef3)
                        val dl = if (uiState.downloadLimitKbps > 0) resources.getString(R.string.settings_1_d_kb_s_c9c342, uiState.downloadLimitKbps) else resources.getString(R.string.settings_unlimited_b8bef3)
                        resources.getString(R.string.settings_upload_1_s_download_2_s_a14e32, ul, dl)
                    },
                    onClick = { showBandwidthLimitDialog = true }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Lan,
                    title = resources.getString(R.string.settings_append_http_proxy_to_vpn_b08207),
                    description = resources.getString(R.string.settings_route_app_traffic_through_http_proxy_directly_bypassing_tun_for_b_739608),
                    checked = uiState.appendHttpProxyToVpn,
                    onCheckedChange = { viewModel.setAppendHttpProxyToVpn(it) }
                )

                if (uiState.appendHttpProxyToVpn && !uiState.httpProxyEnabled) {
                    Text(
                        text = resources.getString(R.string.settings_to_also_share_the_http_proxy_with_other_devices_enable_http_proxy_fdab24),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(start = 56.dp, end = 16.dp, bottom = 8.dp)
                    )
                }
            }

            // Split Tunneling Settings
            SettingsSection(
                title = resources.getString(R.string.settings_split_tunneling_0073fb),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                SwitchSettingItem(
                    icon = Icons.Default.CallSplit,
                    title = resources.getString(R.string.settings_enable_split_tunneling_ccd14f),
                    description = resources.getString(R.string.settings_choose_which_apps_use_the_vpn_8a7aec),
                    checked = uiState.splitTunnelingEnabled,
                    onCheckedChange = { viewModel.setSplitTunnelingEnabled(it) }
                )

                if (uiState.splitTunnelingEnabled) {
                    SettingsDivider()

                    ClickableSettingItem(
                        icon = Icons.Default.FilterList,
                        title = resources.getString(R.string.settings_mode_a7b93d),
                        description = when (uiState.splitTunnelingMode) {
                            SplitTunnelingMode.DISALLOW -> resources.getString(R.string.settings_selected_apps_bypass_vpn_cd2a1b)
                            SplitTunnelingMode.ALLOW -> resources.getString(R.string.settings_only_selected_apps_use_vpn_e2368b)
                        },
                        onClick = { showSplitModeDialog = true }
                    )

                    SettingsDivider()

                    ClickableSettingItem(
                        icon = Icons.Default.Apps,
                        title = resources.getString(R.string.settings_select_apps_6b5084),
                        description = resources.getQuantityString(R.plurals.settings_selected_apps_count, uiState.splitTunnelingApps.size, uiState.splitTunnelingApps.size),
                        onClick = onNavigateToAppSelector
                    )
                }
            }

            // Domain Routing Settings
            SettingsSection(
                title = resources.getString(R.string.settings_domain_routing_712de8),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                SwitchSettingItem(
                    icon = Icons.Default.Language,
                    title = resources.getString(R.string.settings_enable_domain_routing_4fc3fc),
                    description = resources.getString(R.string.settings_route_specific_domains_through_or_around_the_vpn_7ea6a3),
                    checked = uiState.domainRoutingEnabled,
                    onCheckedChange = { viewModel.setDomainRoutingEnabled(it) }
                )

                if (uiState.domainRoutingEnabled) {
                    SettingsDivider()

                    ClickableSettingItem(
                        icon = Icons.Default.FilterList,
                        title = resources.getString(R.string.settings_routing_mode_91ad15),
                        description = when (uiState.domainRoutingMode) {
                            DomainRoutingMode.BYPASS -> resources.getString(R.string.settings_listed_domains_bypass_vpn_c18132)
                            DomainRoutingMode.ONLY_VPN -> resources.getString(R.string.settings_only_listed_domains_use_vpn_cb5f22)
                        },
                        onClick = { showDomainRoutingModeDialog = true }
                    )

                    SettingsDivider()

                    ClickableSettingItem(
                        icon = Icons.Default.TravelExplore,
                        title = resources.getString(R.string.settings_manage_domains_2ca69b),
                        description = resources.getQuantityString(R.plurals.settings_domains_count, uiState.domainRoutingDomains.size, uiState.domainRoutingDomains.size),
                        onClick = { showDomainManagementDialog = true }
                    )
                }
            }

            // Geo-Bypass Settings
            SettingsSection(
                title = resources.getString(R.string.settings_geo_bypass_15bba2),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                SwitchSettingItem(
                    icon = Icons.Default.Public,
                    title = resources.getString(R.string.settings_enable_geo_bypass_63858e),
                    description = resources.getString(R.string.settings_route_domestic_traffic_directly_bypass_vpn_for_local_sites_c49b23),
                    checked = uiState.geoBypassEnabled,
                    onCheckedChange = { viewModel.setGeoBypassEnabled(it) }
                )

                if (uiState.geoBypassEnabled) {
                    SettingsDivider()

                    ClickableSettingItem(
                        icon = Icons.Default.Language,
                        title = resources.getString(R.string.settings_country_d523eb),
                        description = countryName(resources, uiState.geoBypassCountry),
                        onClick = { showGeoBypassCountryDialog = true }
                    )
                    SettingsDivider()
                    ClickableSettingItem(icon = Icons.Default.Apps,
                        title = resources.getString(R.string.review02_app_bypass_title),
                        description = resources.getString(R.string.review02_app_bypass_hint),
                        onClick = { showAppBypassHelp = true })
                }
            }

            // SSH Tunnel Settings
            SettingsSection(
                title = resources.getString(R.string.settings_ssh_tunnel_1177ce),
                subtitle = resources.getString(R.string.settings_changes_apply_on_next_connection_30e26f)
            ) {
                ClickableSettingItem(
                    icon = Icons.Default.Lock,
                    title = resources.getString(R.string.settings_cipher_0ad179),
                    description = when (uiState.sshCipher) {
                        SshCipher.AUTO -> resources.getString(R.string.settings_auto_fastest_a3066d)
                        SshCipher.AES_128_GCM -> "AES-128-GCM"
                        SshCipher.CHACHA20 -> "ChaCha20-Poly1305"
                        SshCipher.AES_128_CTR -> resources.getString(R.string.settings_aes_128_ctr_legacy_086a2b)
                    },
                    onClick = { showSshCipherDialog = true }
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Compress,
                    title = resources.getString(R.string.settings_compression_46ead4),
                    description = resources.getString(R.string.settings_compress_data_through_ssh_helps_on_slow_links_hurts_with_https_b2be2a),
                    checked = uiState.sshCompression,
                    onCheckedChange = { viewModel.setSshCompression(it) }
                )

                SettingsDivider()

                SliderSettingItem(
                    icon = Icons.Default.Hub,
                    title = resources.getString(R.string.settings_max_channels_5f3cb0),
                    subtitle = when {
                        !uiState.sshMaxChannelsIsCustom -> resources.getString(R.string.settings_auto_adapts_per_tunnel_type_5c4385)
                        uiState.sshMaxChannels > 12 -> resources.getString(R.string.settings_high_values_may_cause_instability_on_dns_tunnels_1a0e87)
                        else -> null
                    },
                    subtitleColor = if (uiState.sshMaxChannelsIsCustom && uiState.sshMaxChannels > 12) Color(0xFFFF9800) else null,
                    value = uiState.sshMaxChannels,
                    valueRange = 1f..64f,
                    steps = 63,
                    valueFormatter = { "${it.roundToInt()}" },
                    onValueChange = { viewModel.setSshMaxChannels(it.roundToInt()) },
                    onReset = if (uiState.sshMaxChannelsIsCustom) {{ viewModel.resetSshMaxChannelsToAuto() }} else null
                )

                SettingsDivider()

                SwitchSettingItem(
                    icon = Icons.Default.Shield,
                    title = resources.getString(R.string.settings_prevent_dns_fallback_348572),
                    description = if (uiState.preventDnsFallback)
                        resources.getString(R.string.settings_dns_queries_fail_if_ssh_tunnel_is_down_no_leak_8f5089)
                    else
                        resources.getString(R.string.settings_falls_back_to_direct_dns_if_ssh_fails_may_expose_queries_02cda7),
                    checked = uiState.preventDnsFallback,
                    onCheckedChange = { viewModel.setPreventDnsFallback(it) }
                )
            }

            // Appearance Settings
            SettingsSection(title = resources.getString(R.string.rv_appearance_3907fa)) {
                ClickableSettingItem(
                    icon = Icons.Default.DarkMode,
                    title = resources.getString(R.string.rv_dark_mode_9b7878),
                    description = when (uiState.darkMode) {
                        DarkMode.LIGHT -> resources.getString(R.string.rv_light_dbcd5e)
                        DarkMode.DARK -> resources.getString(R.string.rv_dark_60acc5)
                        DarkMode.AMOLED -> resources.getString(R.string.settings_amoled_dark_73f346)
                        DarkMode.SYSTEM -> resources.getString(R.string.rv_follow_system_48b4e3)
                    },
                    onClick = { showDarkModeDialog = true }
                )
            }

            // Debug Settings
            SettingsSection(title = resources.getString(R.string.rv_debug_1a03bd)) {
                SwitchSettingItem(
                    icon = Icons.Default.BugReport,
                    title = resources.getString(R.string.rv_debug_logging_13bc63),
                    description = resources.getString(R.string.rv_enable_verbose_logging_for_troubleshooting_9719c9),
                    checked = uiState.debugLogging,
                    onCheckedChange = { viewModel.setDebugLogging(it) }
                )
            }

            SettingsSection(title = resources.getString(R.string.settings_stability_report_5f8404)) {
                ClickableSettingItem(icon = Icons.Default.BugReport, title = resources.getString(R.string.settings_stability_report_5f8404),
                    description = resources.getString(R.string.settings_view_or_share_a_private_report_without_connection_addresses_or_pa_60a0d8),
                    onClick = { showStabilityReport = true })
            }

            // Reset Settings
            SettingsSection(title = resources.getString(R.string.rv_reset_daee76)) {
                ClickableSettingItem(
                    icon = Icons.Default.RestartAlt,
                    title = resources.getString(R.string.rv_reset_all_settings_ef4d26),
                    description = resources.getString(R.string.rv_restore_all_settings_to_their_default_values_d1929e),
                    onClick = { showResetSettingsDialog = true }
                )
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Device ID
            val deviceId = remember {
                app.slipnet.util.DeviceIdUtil.getScrambledDeviceId(context)
            }
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.Center
            ) {
                Text(
                    text = resources.getString(R.string.settings_device_id_1_s_e076ae, deviceId),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                IconButton(
                    onClick = {
                        val clipboardManager = context.getSystemService(android.content.ClipboardManager::class.java)
                        clipboardManager?.setPrimaryClip(android.content.ClipData.newPlainText(resources.getString(R.string.settings_device_id_7c9f0f), deviceId))
                    },
                    modifier = Modifier.size(32.dp)
                ) {
                    Icon(
                        Icons.Default.ContentCopy,
                        contentDescription = resources.getString(R.string.settings_copy_device_id_6db03d),
                        modifier = Modifier.size(14.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            TextButton(onClick = { showTrustResetDialog = true }) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.trust_reset))
            }

            // App Info + Check for updates
            Column(
                modifier = Modifier.fillMaxWidth(),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    text = "Rovunet VPN v${BuildConfig.VERSION_NAME}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(modifier = Modifier.height(4.dp))
                if (BuildConfig.PRIVATE_CANDIDATE) {
                    Text(androidx.compose.ui.res.stringResource(R.string.private_test_channel))
                } else {
                    val preview by viewModel.previewUpdates.collectAsState(initial = false)
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(androidx.compose.ui.res.stringResource(R.string.preview_release_channel))
                        Switch(checked = preview, onCheckedChange = { viewModel.setPreviewUpdates(it) },
                            enabled = uiState.updateCheckResult != UpdateCheckResult.CHECKING)
                    }
                    TextButton(onClick = { viewModel.checkForUpdate() }, enabled = uiState.updateCheckResult != UpdateCheckResult.CHECKING) {
                        Text(androidx.compose.ui.res.stringResource(R.string.update_check))
                    }
                }
                when (uiState.updateCheckResult) {
                    UpdateCheckResult.CHECKING -> CircularProgressIndicator()
                    UpdateCheckResult.ERROR -> Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.update_check_failed))
                    UpdateCheckResult.UP_TO_DATE -> Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.update_current))
                    else -> Unit
                }
            }

            Spacer(modifier = Modifier.height(8.dp))
        }
    }

    if (showStabilityReport) StabilityReportDialog(onDismiss = { showStabilityReport = false })

    // Dark Mode Dialog
    if (showMtuDialog) {
        val mtuPresets = listOf(
            1500 to resources.getString(R.string.settings_best_throughput_on_clean_networks_b37fa0),
            1400 to resources.getString(R.string.settings_recommended_for_most_mobile_networks_3779b5),
            1350 to resources.getString(R.string.settings_conservative_for_double_nat_or_pppoe_e76285),
            1280 to resources.getString(R.string.settings_maximum_compatibility_60541c)
        )
        val isCustom = mtuPresets.none { it.first == effectiveMtu }
        var customMtuText by remember(effectiveMtu) { mutableStateOf(if (isCustom) effectiveMtu.toString() else "") }
        val range = VpnMtu.range(uiState.blockIpv6)
        var useCustom by remember(effectiveMtu) { mutableStateOf(isCustom) }
        AlertDialog(
            onDismissRequest = { showMtuDialog = false },
            title = { Text(resources.getString(R.string.settings_vpn_mtu_ea6cc3)) },
            text = {
                Column {
                    mtuPresets.forEach { (mtu, desc) ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    useCustom = false
                                    viewModel.setVpnMtu(mtu)
                                    showMtuDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = !useCustom && effectiveMtu == mtu,
                                onClick = {
                                    useCustom = false
                                    viewModel.setVpnMtu(mtu)
                                    showMtuDialog = false
                                }
                            )
                            Column(modifier = Modifier.padding(start = 8.dp)) {
                                Text(text = "$mtu")
                                Text(
                                    text = desc,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(8.dp))
                            .clickable { useCustom = true }
                            .padding(vertical = 12.dp, horizontal = 8.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        RadioButton(
                            selected = useCustom,
                            onClick = { useCustom = true }
                        )
                        OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = customMtuText,
                            onValueChange = { customMtuText = it.filter { c -> c.isDigit() }.take(5) },
                            enabled = useCustom,
                            label = { Text(resources.getString(R.string.settings_custom_081ae3)) },
                            placeholder = { Text("${range.first}–${range.last}") },
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            singleLine = true,
                            modifier = Modifier
                                .padding(start = 8.dp)
                                .fillMaxWidth()
                        )
                    }
                }
            },
            confirmButton = {
                if (useCustom) {
                    val value = customMtuText.toIntOrNull()
                    TextButton(
                        enabled = value != null && value in range,
                        onClick = {
                            value?.let(viewModel::setVpnMtu)
                            showMtuDialog = false
                        }
                    ) {
                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392))
                    }
                }
            },
            dismissButton = {
                TextButton(onClick = { showMtuDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    if (showBandwidthLimitDialog) {
        BandwidthLimitDialog(
            currentUploadKbps = uiState.uploadLimitKbps,
            currentDownloadKbps = uiState.downloadLimitKbps,
            onDismiss = { showBandwidthLimitDialog = false },
            onApply = { upKbps, downKbps ->
                viewModel.setUploadLimitKbps(upKbps)
                viewModel.setDownloadLimitKbps(downKbps)
                showBandwidthLimitDialog = false
            }
        )
    }

    if (showDnsWorkerDialog) {
        AlertDialog(
            onDismissRequest = { showDnsWorkerDialog = false },
            title = { Text(resources.getString(R.string.settings_dns_workers_62a5b4)) },
            text = {
                Column {
                    Text(
                        text = resources.getString(R.string.settings_controls_how_dns_is_resolved_through_the_tunnel_fewer_workers_mor_3c2587),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(bottom = 12.dp)
                    )
                    DnsWorkerMode.entries.forEach { mode ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setDnsWorkerMode(mode)
                                    showDnsWorkerDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.dnsWorkerMode == mode,
                                onClick = {
                                    viewModel.setDnsWorkerMode(mode)
                                    showDnsWorkerDialog = false
                                }
                            )
                            Text(
                                text = dnsWorkerName(resources, mode),
                                modifier = Modifier.padding(start = 8.dp)
                            )
                        }
                    }
                    if (uiState.dnsWorkerMode.poolSize >= 3) {
                        Text(
                            text = resources.getString(R.string.settings_higher_worker_counts_increase_background_data_usage_due_to_keepal_0efd92),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showDnsWorkerDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    if (showDarkModeDialog) {
        AlertDialog(
            onDismissRequest = { showDarkModeDialog = false },
            title = { Text(resources.getString(R.string.settings_dark_mode_e7f8b9)) },
            text = {
                Column {
                    DarkMode.entries.forEach { mode ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setDarkMode(mode)
                                    showDarkModeDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.darkMode == mode,
                                onClick = {
                                    viewModel.setDarkMode(mode)
                                    showDarkModeDialog = false
                                }
                            )
                            Text(
                                text = when (mode) {
                                    DarkMode.LIGHT -> resources.getString(R.string.rv_light_dbcd5e)
                                    DarkMode.DARK -> resources.getString(R.string.rv_dark_60acc5)
                                    DarkMode.AMOLED -> resources.getString(R.string.settings_amoled_dark_73f346)
                                    DarkMode.SYSTEM -> resources.getString(R.string.rv_follow_system_48b4e3)
                                },
                                modifier = Modifier.padding(start = 8.dp)
                            )
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showDarkModeDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // Split Tunneling Mode Dialog
    if (showSplitModeDialog) {
        AlertDialog(
            onDismissRequest = { showSplitModeDialog = false },
            title = { Text(resources.getString(R.string.settings_split_tunneling_mode_71325a)) },
            text = {
                Column {
                    SplitTunnelingMode.entries.forEach { mode ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setSplitTunnelingMode(mode)
                                    showSplitModeDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.splitTunnelingMode == mode,
                                onClick = {
                                    viewModel.setSplitTunnelingMode(mode)
                                    showSplitModeDialog = false
                                }
                            )
                            Column(modifier = Modifier.padding(start = 8.dp)) {
                                Text(
                                    text = when (mode) {
                                        SplitTunnelingMode.DISALLOW -> resources.getString(R.string.settings_bypass_497852)
                                        SplitTunnelingMode.ALLOW -> resources.getString(R.string.settings_only_8737f0)
                                    }
                                )
                                Text(
                                    text = when (mode) {
                                        SplitTunnelingMode.DISALLOW -> resources.getString(R.string.settings_selected_apps_bypass_vpn_cd2a1b)
                                        SplitTunnelingMode.ALLOW -> resources.getString(R.string.settings_only_selected_apps_use_vpn_e2368b)
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showSplitModeDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // Domain Routing Mode Dialog
    if (showDomainRoutingModeDialog) {
        AlertDialog(
            onDismissRequest = { showDomainRoutingModeDialog = false },
            title = { Text(resources.getString(R.string.settings_domain_routing_mode_fea5c7)) },
            text = {
                Column {
                    DomainRoutingMode.entries.forEach { mode ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setDomainRoutingMode(mode)
                                    showDomainRoutingModeDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.domainRoutingMode == mode,
                                onClick = {
                                    viewModel.setDomainRoutingMode(mode)
                                    showDomainRoutingModeDialog = false
                                }
                            )
                            Column(modifier = Modifier.padding(start = 8.dp)) {
                                Text(
                                    text = when (mode) {
                                        DomainRoutingMode.BYPASS -> resources.getString(R.string.settings_bypass_vpn_8dda37)
                                        DomainRoutingMode.ONLY_VPN -> resources.getString(R.string.settings_only_vpn_e2e1e6)
                                    }
                                )
                                Text(
                                    text = when (mode) {
                                        DomainRoutingMode.BYPASS -> resources.getString(R.string.settings_listed_domains_connect_directly_c5310d)
                                        DomainRoutingMode.ONLY_VPN -> resources.getString(R.string.settings_only_listed_domains_use_the_vpn_aaf1ab)
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showDomainRoutingModeDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // Domain Management Dialog
    if (showDomainManagementDialog) {
        DomainManagementDialog(
            domains = uiState.domainRoutingDomains,
            onAddDomain = { viewModel.addDomainRoutingDomain(it) },
            onRemoveDomain = { viewModel.removeDomainRoutingDomain(it) },
            onDismiss = { showDomainManagementDialog = false }
        )
    }

    // Geo-Bypass Country Dialog
    if (showAppBypassHelp) AppBypassHelpDialog(
        onConfirm = { viewModel.configureSelectedAppsBypass { showAppBypassHelp = false; onNavigateToAppSelector() } },
        onDismiss = { showAppBypassHelp = false })

    if (showGeoBypassCountryDialog) {
        AlertDialog(
            onDismissRequest = { showGeoBypassCountryDialog = false },
            title = { Text(resources.getString(R.string.settings_select_country_434b36)) },
            text = {
                Column {
                    GeoBypassCountry.entries.forEach { country ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setGeoBypassCountry(country)
                                    showGeoBypassCountryDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.geoBypassCountry == country,
                                onClick = {
                                    viewModel.setGeoBypassCountry(country)
                                    showGeoBypassCountryDialog = false
                                }
                            )
                            Text(
                                text = countryName(resources, country),
                                modifier = Modifier.padding(start = 8.dp)
                            )
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showGeoBypassCountryDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // Global Resolver Override Dialog
    if (showGlobalResolverDialog) {
        var resolverText by remember { mutableStateOf(uiState.globalResolverList) }
        val ipPattern = remember { Regex("""^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?$""") }
        val entries = resolverText.split(",", "\n").map { it.trim() }.filter { it.isNotBlank() }
        val resolverCount = entries.size
        val tooMany = resolverCount > 10
        val invalidEntries = entries.filter { !ipPattern.matches(it) }
        val hasInvalid = invalidEntries.isNotEmpty()
        AlertDialog(
            onDismissRequest = { showGlobalResolverDialog = false },
            title = { Text(resources.getString(R.string.settings_global_dns_resolvers_dabf90)) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        resources.getString(R.string.settings_enter_dns_resolver_ips_one_per_line_or_comma_separated_max_10_the_75f679),
                        style = MaterialTheme.typography.bodySmall
                    )
                    OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                        value = resolverText,
                        onValueChange = { resolverText = it },
                        modifier = Modifier.fillMaxWidth().height(120.dp),
                        placeholder = { Text(resources.getString(R.string.settings_e_g_8_8_8_8_1_1_1_1_f6c505)) },
                        singleLine = false,
                        maxLines = 8,
                        isError = tooMany || hasInvalid
                    )
                    if (tooMany) {
                        Text(
                            resources.getString(R.string.settings_maximum_10_resolvers_1_d_entered_3cac6d, resolverCount),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    } else if (hasInvalid) {
                        Text(
                            resources.getString(R.string.settings_invalid_ip_1_s_7a824a, invalidEntries.first()),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.error
                        )
                    } else if (resolverCount > 0) {
                        Text(
                            resources.getQuantityString(R.plurals.settings_resolvers_count, resolverCount, resolverCount),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        // Normalize: strip ports, clean up separators
                        val cleaned = entries.joinToString(", ") { it.split(":").first() }
                        viewModel.setGlobalResolverList(cleaned)
                        showGlobalResolverDialog = false
                    },
                    enabled = !tooMany && !hasInvalid && resolverCount > 0
                ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_save_1509f5)) }
            },
            dismissButton = {
                TextButton(onClick = { showGlobalResolverDialog = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
            }
        )
    }

    // DNS Pool Dialog
    if (showDnsPoolDialog) {
        DnsPoolDialog(
            initialText = uiState.dnsPoolText,
            onSave = { text ->
                viewModel.setDnsPoolText(text)
                showDnsPoolDialog = false
            },
            onDismiss = { showDnsPoolDialog = false }
        )
    }

    // Remote DNS Dialog
    if (showRemoteDnsDialog) {
        RemoteDnsDialog(
            currentMode = uiState.remoteDnsMode,
            currentCustomDns = uiState.customRemoteDns,
            currentCustomDnsFallback = uiState.customRemoteDnsFallback,
            onSelectDefault = {
                viewModel.setRemoteDnsMode("default")
                showRemoteDnsDialog = false
            },
            onSelectCustom = { dns, fallback ->
                viewModel.setRemoteDnsMode("custom")
                viewModel.setCustomRemoteDns(dns)
                viewModel.setCustomRemoteDnsFallback(fallback)
                showRemoteDnsDialog = false
            },
            onDismiss = { showRemoteDnsDialog = false }
        )
    }

    // SSH Cipher Dialog
    if (showSshCipherDialog) {
        AlertDialog(
            onDismissRequest = { showSshCipherDialog = false },
            title = { Text(resources.getString(R.string.settings_ssh_cipher_b5c323)) },
            text = {
                Column {
                    SshCipher.entries.forEach { cipher ->
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable {
                                    viewModel.setSshCipher(cipher)
                                    showSshCipherDialog = false
                                }
                                .padding(vertical = 12.dp, horizontal = 8.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = uiState.sshCipher == cipher,
                                onClick = {
                                    viewModel.setSshCipher(cipher)
                                    showSshCipherDialog = false
                                }
                            )
                            Text(
                                text = sshCipherName(resources, cipher),
                                modifier = Modifier.padding(start = 8.dp)
                            )
                        }
                    }
                }
            },
            confirmButton = {
                TextButton(onClick = { showSshCipherDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // Reset Settings Confirmation Dialog
    if (showResetSettingsDialog) {
        AlertDialog(
            onDismissRequest = { showResetSettingsDialog = false },
            title = { Text(resources.getString(R.string.settings_reset_all_settings_bbfe66)) },
            text = {
                Text(resources.getString(R.string.settings_this_will_restore_all_settings_to_their_default_values_your_profi_a087f6))
            },
            confirmButton = {
                TextButton(onClick = {
                    viewModel.resetAllSettings()
                    showResetSettingsDialog = false
                }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_reset_daee76), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { showResetSettingsDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                }
            }
        )
    }

    // About Dialog
    if (showAboutDialog) {
        AlertDialog(
            onDismissRequest = { showAboutDialog = false },
            title = { Text(resources.getString(R.string.settings_about_rovunet_ec746c)) },
            text = { AboutDialogContent() },
            confirmButton = {
                TextButton(onClick = { showAboutDialog = false }) {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_close_7d9eb7))
                }
            }
        )
    }

    if (showTrustResetDialog) {
        AlertDialog(
            onDismissRequest = { showTrustResetDialog = false },
            title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.trust_reset)) },
            text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.trust_reset_message)) },
            confirmButton = { TextButton(onClick = { app.slipnet.tunnel.TransportTrust.clear(); showTrustResetDialog = false }) { Text(androidx.compose.ui.res.stringResource(android.R.string.ok)) } },
            dismissButton = { TextButton(onClick = { showTrustResetDialog = false }) { Text(androidx.compose.ui.res.stringResource(android.R.string.cancel)) } }
        )
    }

    if (uiState.updateCheckResult == UpdateCheckResult.UPDATE_AVAILABLE) {
        viewModel.availableUpdate?.let { update ->
            app.slipnet.presentation.common.components.UpdateDialog(update) { viewModel.clearUpdateCheck() }
        }
    }

}

@Composable
private fun DomainManagementDialog(
    domains: Set<String>,
    onAddDomain: (String) -> Unit,
    onRemoveDomain: (String) -> Unit,
    onDismiss: () -> Unit
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    var newDomain by remember { mutableStateOf("") }
    val sortedDomains = remember(domains) { domains.sorted() }

    val addDomain = {
        val trimmed = newDomain.trim().lowercase()
        if (trimmed.isNotEmpty()) {
            onAddDomain(trimmed)
            newDomain = ""
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(resources.getString(R.string.settings_manage_domains_2f51ef)) },
        text = {
            Column(
                modifier = Modifier.fillMaxWidth()
            ) {
                // Add domain input
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                        value = newDomain,
                        onValueChange = { newDomain = it },
                        placeholder = { Text(resources.getString(R.string.settings_e_g_google_com_c1dd6e)) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions.Default,
                        keyboardActions = KeyboardActions(onDone = { addDomain() }),
                        modifier = Modifier.weight(1f)
                    )
                    IconButton(onClick = { addDomain() }) {
                        Icon(Icons.Default.Add, contentDescription = resources.getString(R.string.settings_add_domain_76d740))
                    }
                }

                Spacer(modifier = Modifier.height(12.dp))

                if (sortedDomains.isEmpty()) {
                    Text(
                        text = resources.getString(R.string.settings_no_domains_configured_59613e),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(vertical = 8.dp)
                    )
                } else {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(240.dp)
                    ) {
                        items(sortedDomains) { domain ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(
                                    text = domain,
                                    style = MaterialTheme.typography.bodyMedium,
                                    modifier = Modifier.weight(1f)
                                )
                                IconButton(
                                    onClick = { onRemoveDomain(domain) },
                                    modifier = Modifier.size(32.dp)
                                ) {
                                    Icon(
                                        Icons.Default.Delete,
                                        contentDescription = resources.getString(R.string.settings_remove_1_s_4d28b5, domain),
                                        tint = MaterialTheme.colorScheme.error,
                                        modifier = Modifier.size(18.dp)
                                    )
                                }
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = { addDomain(); onDismiss() }) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_done_11a676))
            }
        }
    )
}

@Composable
private fun RemoteDnsDialog(
    currentMode: String,
    currentCustomDns: String,
    currentCustomDnsFallback: String,
    onSelectDefault: () -> Unit,
    onSelectCustom: (String, String) -> Unit,
    onDismiss: () -> Unit
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    var selectedMode by remember { mutableStateOf(currentMode) }
    var customDns by remember { mutableStateOf(currentCustomDns) }
    var customDnsFallback by remember { mutableStateOf(currentCustomDnsFallback) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(resources.getString(R.string.settings_remote_dns_server_f2f414)) },
        text = {
            Column {
                Text(
                    text = resources.getString(R.string.settings_dns_servers_used_on_the_remote_side_of_the_tunnel_for_resolving_d_107df7),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(bottom = 12.dp)
                )

                // Default option
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .clickable { selectedMode = "default" }
                        .padding(vertical = 12.dp, horizontal = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    RadioButton(
                        selected = selectedMode == "default",
                        onClick = { selectedMode = "default" }
                    )
                    Column(modifier = Modifier.padding(start = 8.dp)) {
                        Text(resources.getString(R.string.settings_default_8_8_8_8_1_1_1_1_d8be7c))
                        Text(
                            text = resources.getString(R.string.settings_google_primary_cloudflare_fallback_1d930c),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }

                // Custom option
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .clickable { selectedMode = "custom" }
                        .padding(vertical = 12.dp, horizontal = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    RadioButton(
                        selected = selectedMode == "custom",
                        onClick = { selectedMode = "custom" }
                    )
                    Text(
                        text = resources.getString(R.string.settings_custom_081ae3),
                        modifier = Modifier.padding(start = 8.dp)
                    )
                }

                if (selectedMode == "custom") {
                    OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                        value = customDns,
                        onValueChange = { customDns = it },
                        placeholder = { Text(resources.getString(R.string.settings_e_g_9_9_9_9_fa7540)) },
                        supportingText = { Text(resources.getString(R.string.settings_primary_dns_server_99c8e7)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(start = 40.dp, top = 4.dp)
                    )

                    OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                        value = customDnsFallback,
                        onValueChange = { customDnsFallback = it },
                        placeholder = { Text(resources.getString(R.string.settings_e_g_8_8_8_8_447b12)) },
                        supportingText = { Text(resources.getString(R.string.settings_fallback_dns_server_86893a)) },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        singleLine = true,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(start = 40.dp, top = 4.dp)
                    )
                }
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    if (selectedMode == "default") {
                        onSelectDefault()
                    } else {
                        onSelectCustom(customDns.trim(), customDnsFallback.trim())
                    }
                }
            ) {
                Text(resources.getString(R.string.settings_ok_9ce3bd))
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
            }
        }
    )
}

@Composable
private fun SettingsSection(
    title: String,
    subtitle: String? = null,
    content: @Composable () -> Unit
) {
    Column {
        Row(
            modifier = Modifier.padding(start = 4.dp, bottom = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.primary
            )
            if (subtitle != null) {
                Text(
                    text = " · $subtitle",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.surfaceContainerLow
            ),
            elevation = CardDefaults.cardElevation(defaultElevation = 0.dp)
        ) {
            Column(
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp)
            ) {
                content()
            }
        }
    }
}

@Composable
private fun SettingsDivider() {
    HorizontalDivider(
        modifier = Modifier.padding(vertical = 12.dp),
        color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f)
    )
}

@Composable
private fun SwitchSettingItem(
    icon: ImageVector,
    title: String,
    description: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    inactive: Boolean = false,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .alpha(if (inactive) 0.5f else 1f)
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(24.dp)
        )
        Column(
            modifier = Modifier
                .weight(1f)
                .padding(horizontal = 16.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Switch(
            checked = checked,
            onCheckedChange = onCheckedChange
        )
    }
}

@Composable
private fun ClickableSettingItem(
    icon: ImageVector,
    title: String,
    description: String,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .clickable { onClick() }
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(24.dp)
        )
        Column(
            modifier = Modifier
                .weight(1f)
                .padding(horizontal = 16.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun IndentedSettingItem(
    icon: ImageVector,
    title: String,
    description: String,
    onClick: () -> Unit,
    inactive: Boolean = false,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = 40.dp)
            .alpha(if (inactive) 0.5f else 1f)
            .clip(RoundedCornerShape(8.dp))
            .clickable { onClick() }
            .padding(vertical = 6.dp, horizontal = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(18.dp)
        )
        Column(
            modifier = Modifier
                .weight(1f)
                .padding(horizontal = 12.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyMedium
            )
            Text(
                text = description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Icon(
            imageVector = Icons.AutoMirrored.Filled.KeyboardArrowRight,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
            modifier = Modifier.size(18.dp)
        )
    }
}

@Composable
private fun InfoNoticeRow(text: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = 40.dp, top = 4.dp, bottom = 8.dp, end = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = Icons.Default.Info,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.8f),
            modifier = Modifier.size(16.dp)
        )
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(start = 8.dp)
        )
    }
}

@Composable
private fun SliderSettingItem(
    icon: ImageVector,
    title: String,
    subtitle: String? = null,
    subtitleColor: Color? = null,
    value: Int,
    valueRange: ClosedFloatingPointRange<Float>,
    steps: Int,
    valueFormatter: (Float) -> String,
    onValueChange: (Float) -> Unit,
    onReset: (() -> Unit)? = null
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(24.dp)
            )
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 16.dp)
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.bodyLarge
                )
                if (subtitle != null) {
                    Text(
                        text = subtitle,
                        style = MaterialTheme.typography.bodySmall,
                        color = subtitleColor ?: MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            if (onReset != null) {
                TextButton(onClick = onReset) {
                    Text(resources.getString(R.string.settings_auto_c614ba), style = MaterialTheme.typography.labelSmall)
                }
            }
            Text(
                text = valueFormatter(value.toFloat()),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.primary
            )
        }
        Spacer(modifier = Modifier.height(4.dp))
        Slider(
            value = value.toFloat(),
            onValueChange = onValueChange,
            valueRange = valueRange,
            steps = steps,
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 40.dp, top = 4.dp)
        )
    }
}

@Composable
private fun StepperSettingItem(
    icon: ImageVector,
    title: String,
    description: String,
    value: Int,
    step: Int,
    range: IntRange,
    valueFormatter: (Int) -> String,
    onValueChange: (Int) -> Unit
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(24.dp)
        )
        Column(
            modifier = Modifier
                .weight(1f)
                .padding(horizontal = 16.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(
                text = description,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Row(
            verticalAlignment = Alignment.CenterVertically
        ) {
            IconButton(
                onClick = { onValueChange((value - step).coerceIn(range)) },
                enabled = value > range.first,
                modifier = Modifier.size(36.dp)
            ) {
                Icon(
                    Icons.Default.Remove,
                    contentDescription = resources.getString(R.string.settings_decrease_67e19d),
                    modifier = Modifier.size(20.dp)
                )
            }
            Text(
                text = valueFormatter(value),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.primary,
                textAlign = TextAlign.Center,
                modifier = Modifier.widthIn(min = 48.dp)
            )
            IconButton(
                onClick = { onValueChange((value + step).coerceIn(range)) },
                enabled = value < range.last,
                modifier = Modifier.size(36.dp)
            ) {
                Icon(
                    Icons.Default.Add,
                    contentDescription = resources.getString(R.string.settings_increase_5a7fec),
                    modifier = Modifier.size(20.dp)
                )
            }
        }
    }
}

@Composable
/**
 * Detect available network addresses for the listen address picker.
 * Returns list of (label, ip) pairs.
 */
private fun getAddressOptions(): List<Pair<String, String>> {
    val resources = androidx.compose.ui.platform.LocalResources.current
    return listOf(
        resources.getString(R.string.settings_localhost_298087) to "127.0.0.1",
        resources.getString(R.string.settings_all_interfaces_8d0587) to "0.0.0.0"
    )
}

/**
 * Detect the device's shareable IP address.
 * Priority: hotspot interface > Wi-Fi > any non-loopback IPv4 interface.
 * Returns a pair of (ip, isHotspot) or null if no suitable interface is found.
 */
private fun detectShareableIp(): Pair<String, Boolean>? {
    try {
        val interfaces = NetworkInterface.getNetworkInterfaces()?.toList() ?: return null
        val hotspotPrefixes = listOf("wlan1", "ap0", "swlan0", "softap", "wlan-ap", "rndis")

        // First try hotspot interfaces
        for (iface in interfaces) {
            if (!iface.isUp) continue
            if (hotspotPrefixes.any { iface.name.startsWith(it) }) {
                val ip = iface.inetAddresses.toList()
                    .firstOrNull { it is Inet4Address && !it.isLoopbackAddress }
                    ?.hostAddress
                if (ip != null) return ip to true
            }
        }

        // Fall back to Wi-Fi (wlan0)
        for (iface in interfaces) {
            if (!iface.isUp) continue
            if (iface.name.startsWith("wlan0") || iface.name.startsWith("wlan")) {
                val ip = iface.inetAddresses.toList()
                    .firstOrNull { it is Inet4Address && !it.isLoopbackAddress }
                    ?.hostAddress
                if (ip != null) return ip to false
            }
        }

        // Fall back to any non-loopback IPv4 interface (mobile data, USB, ethernet, etc.)
        for (iface in interfaces) {
            if (!iface.isUp || iface.isLoopback) continue
            val ip = iface.inetAddresses.toList()
                .firstOrNull { it is Inet4Address && !it.isLoopbackAddress }
                ?.hostAddress
            if (ip != null) return ip to false
        }
    } catch (_: Exception) { }
    return null
}

@Composable
private fun HotspotInfoCard(
    socksPort: Int,
    httpProxyEnabled: Boolean = false,
    httpProxyPort: Int = 8080
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    val shareableIp = remember { detectShareableIp() }
    if (shareableIp == null) return

    val (ip, isHotspot) = shareableIp
    val socksAddress = "$ip:$socksPort"
    val httpAddress = "$ip:$httpProxyPort"
    val copyText = if (httpProxyEnabled) "SOCKS5: $socksAddress | HTTP: $httpAddress" else socksAddress
    val clipboardManager = LocalClipboardManager.current

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (isHotspot)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerHigh
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Default.Wifi,
                contentDescription = null,
                tint = if (isHotspot)
                    MaterialTheme.colorScheme.onPrimaryContainer
                else
                    MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp)
            )
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(
                    text = if (isHotspot) resources.getString(R.string.settings_hotspot_proxy_address_588ac2) else resources.getString(R.string.settings_device_ip_bb795b),
                    style = MaterialTheme.typography.labelMedium,
                    color = if (isHotspot)
                        MaterialTheme.colorScheme.onPrimaryContainer
                    else
                        MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = "SOCKS5: $socksAddress",
                    style = MaterialTheme.typography.bodyMedium,
                    color = if (isHotspot)
                        MaterialTheme.colorScheme.onPrimaryContainer
                    else
                        MaterialTheme.colorScheme.onSurface
                )
                if (httpProxyEnabled) {
                    Text(
                        text = "HTTP: $httpAddress",
                        style = MaterialTheme.typography.bodyMedium,
                        color = if (isHotspot)
                            MaterialTheme.colorScheme.onPrimaryContainer
                        else
                            MaterialTheme.colorScheme.onSurface
                    )
                }
                if (isHotspot) {
                    Text(
                        text = resources.getString(R.string.settings_use_as_proxy_on_connected_devices_0b53ab),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onPrimaryContainer.copy(alpha = 0.7f)
                    )
                } else {
                    Text(
                        text = resources.getString(R.string.settings_enable_hotspot_to_share_with_other_devices_1540b6),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            IconButton(
                onClick = { clipboardManager.setText(AnnotatedString(copyText)) },
                modifier = Modifier.size(32.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.ContentCopy,
                    contentDescription = resources.getString(R.string.rv_copy_address_7c4e52),
                    tint = if (isHotspot)
                        MaterialTheme.colorScheme.onPrimaryContainer
                    else
                        MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}

@Composable
private fun LocalProxyInfoCard(
    listenAddress: String,
    socksPort: Int,
    httpProxyEnabled: Boolean = false,
    httpProxyPort: Int = 8080
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    val socksAddress = "$listenAddress:$socksPort"
    val httpAddress = "$listenAddress:$httpProxyPort"
    val copyText = if (httpProxyEnabled) "SOCKS5: $socksAddress | HTTP: $httpAddress" else socksAddress
    val clipboardManager = LocalClipboardManager.current

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerHigh
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Default.SettingsEthernet,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp)
            )
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(
                    text = resources.getString(R.string.settings_proxy_address_da7c90),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Text(
                    text = "SOCKS5: $socksAddress",
                    style = MaterialTheme.typography.bodyMedium
                )
                if (httpProxyEnabled) {
                    Text(
                        text = "HTTP: $httpAddress",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
                Text(
                    text = resources.getString(R.string.settings_configure_this_in_your_app_s_proxy_settings_39db39),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            IconButton(
                onClick = { clipboardManager.setText(AnnotatedString(copyText)) },
                modifier = Modifier.size(32.dp)
            ) {
                Icon(
                    imageVector = Icons.Default.ContentCopy,
                    contentDescription = resources.getString(R.string.rv_copy_address_7c4e52),
                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.size(18.dp)
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AddressSettingItem(
    value: String,
    options: List<Pair<String, String>>,
    onValueChange: (String) -> Unit
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    var expanded by remember { mutableStateOf(false) }

    // Find the label for the current value, if it matches a known option
    val displayText = options.find { it.second == value }?.let { (label, ip) ->
        if (label == resources.getString(R.string.settings_all_interfaces_8d0587) || label == resources.getString(R.string.settings_localhost_298087)) "$label ($ip)" else "$label: $ip"
    } ?: value

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Default.Lan,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(24.dp)
            )
            Text(
                text = resources.getString(R.string.settings_listen_address_633c69),
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier.padding(start = 16.dp)
            )
        }
        Spacer(modifier = Modifier.height(8.dp))
        ExposedDropdownMenuBox(
            expanded = expanded,
            onExpandedChange = { expanded = it },
            modifier = Modifier.padding(start = 40.dp)
        ) {
            OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                value = displayText,
                onValueChange = { },
                readOnly = true,
                trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
                singleLine = true,
                modifier = Modifier
                    .fillMaxWidth()
                    .menuAnchor()
            )

            ExposedDropdownMenu(
                expanded = expanded,
                onDismissRequest = { expanded = false }
            ) {
                options.forEach { (label, ip) ->
                    DropdownMenuItem(
                        text = {
                            Text(
                                text = "$label ($ip)",
                                style = MaterialTheme.typography.bodyMedium
                            )
                        },
                        onClick = {
                            onValueChange(ip)
                            expanded = false
                        }
                    )
                }
            }
        }
    }
}

@Composable
private fun TextFieldSettingItem(
    icon: ImageVector,
    title: String,
    value: String,
    placeholder: String,
    supportingText: String,
    keyboardType: KeyboardType = KeyboardType.Text,
    isError: Boolean = false,
    onValueChange: (String) -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = icon,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(24.dp)
            )
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier.padding(start = 16.dp)
            )
        }
        Spacer(modifier = Modifier.height(8.dp))
        OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
            value = value,
            onValueChange = onValueChange,
            placeholder = { Text(placeholder) },
            supportingText = { Text(supportingText) },
            isError = isError,
            keyboardOptions = KeyboardOptions(keyboardType = keyboardType),
            singleLine = true,
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 40.dp)
        )
    }
}


@Composable
private fun BandwidthLimitDialog(
    currentUploadKbps: Int,
    currentDownloadKbps: Int,
    onDismiss: () -> Unit,
    onApply: (uploadKbps: Int, downloadKbps: Int) -> Unit
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    var uploadText by remember { mutableStateOf(if (currentUploadKbps > 0) currentUploadKbps.toString() else "") }
    var downloadText by remember { mutableStateOf(if (currentDownloadKbps > 0) currentDownloadKbps.toString() else "") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(resources.getString(R.string.settings_bandwidth_limit_b0796c)) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text(
                    text = resources.getString(R.string.settings_set_speed_limits_in_kb_s_leave_empty_or_0_for_unlimited_applies_o_69a450),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                    value = uploadText,
                    onValueChange = { uploadText = it.filter { c -> c.isDigit() }.take(6) },
                    label = { Text(resources.getString(R.string.settings_upload_kb_s_741344)) },
                    placeholder = { Text(resources.getString(R.string.settings_unlimited_b8bef3)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedTextField(
                            textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                    value = downloadText,
                    onValueChange = { downloadText = it.filter { c -> c.isDigit() }.take(6) },
                    label = { Text(resources.getString(R.string.settings_download_kb_s_a32131)) },
                    placeholder = { Text(resources.getString(R.string.settings_unlimited_b8bef3)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    val up = uploadText.toIntOrNull() ?: 0
                    val down = downloadText.toIntOrNull() ?: 0
                    onApply(up.coerceAtLeast(0), down.coerceAtLeast(0))
                }
            ) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392))
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
            }
        }
    )
}


@Composable
private fun DnsPoolDialog(
    initialText: String,
    onSave: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val resources = androidx.compose.ui.platform.LocalResources.current
    val context = LocalContext.current
    val max = app.slipnet.tunnel.DnsPoolScanner.MAX_POOL_SIZE
    var text by remember { mutableStateOf(initialText) }
    val parsedCount = remember(text) {
        app.slipnet.tunnel.DnsPoolScanner.parsePool(text).size
    }

    val importLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri: Uri? ->
        uri?.let {
            try {
                val content = context.contentResolver.openInputStream(it)
                    ?.bufferedReader()?.readText() ?: ""
                text = capPoolText(content, max)
            } catch (_: Exception) {}
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = resources.getString(R.string.settings_dns_resolver_pool_993c79),
                    style = MaterialTheme.typography.titleLarge,
                    modifier = Modifier.weight(1f)
                )
                CountBadge(count = parsedCount, max = max)
            }
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    resources.getString(R.string.settings_candidates_scanned_on_each_connect_top_10_lowest_latency_win_1869aa),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(6.dp)
                ) {
                    PoolActionButton(
                        icon = Icons.Default.UploadFile,
                        label = resources.getString(R.string.rv_import_2cff9b),
                        onClick = { importLauncher.launch(arrayOf("text/plain", "*/*")) },
                        modifier = Modifier.weight(1f)
                    )
                    PoolActionButton(
                        icon = Icons.Default.Shuffle,
                        label = resources.getString(R.string.settings_random_58d888),
                        onClick = {
                            val sampled = sampleRandomResolversFromBuiltIn(context, count = max)
                            if (sampled.isNotBlank()) text = sampled
                        },
                        modifier = Modifier.weight(1f)
                    )
                    PoolActionButton(
                        icon = Icons.Default.Delete,
                        label = resources.getString(R.string.rv_clear_83b12c),
                        onClick = { text = "" },
                        modifier = Modifier.weight(1f),
                        enabled = text.isNotEmpty()
                    )
                }
                OutlinedTextField(
                    value = text,
                    onValueChange = { text = capPoolText(it, max) },
                    placeholder = { Text(resources.getString(R.string.settings_one_ip_or_host_port_per_line_or_comma_separated_lines_starting_wi_628aae)) },
                    minLines = 8,
                    maxLines = 12,
                    textStyle = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace, textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(text) }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_save_1509f5)) }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
        }
    )
}

@Composable
private fun CountBadge(count: Int, max: Int) {
    val nearLimit = count >= max
    val containerColor = if (nearLimit)
        MaterialTheme.colorScheme.errorContainer
    else
        MaterialTheme.colorScheme.secondaryContainer
    val textColor = if (nearLimit)
        MaterialTheme.colorScheme.onErrorContainer
    else
        MaterialTheme.colorScheme.onSecondaryContainer
    Text(
        text = "$count / $max",
        style = MaterialTheme.typography.labelMedium,
        color = textColor,
        modifier = Modifier
            .clip(RoundedCornerShape(12.dp))
            .background(containerColor)
            .padding(horizontal = 10.dp, vertical = 4.dp)
    )
}

@Composable
private fun PoolActionButton(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 8.dp, vertical = 6.dp),
        modifier = modifier
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(16.dp)
        )
        Spacer(Modifier.size(6.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium
        )
    }
}

private fun capPoolText(text: String, max: Int): String {
    val lines = text.lines()
    var validCount = 0
    val out = mutableListOf<String>()
    for (line in lines) {
        val trimmed = line.trim()
        val isEntry = trimmed.isNotEmpty() && !trimmed.startsWith("#")
        if (isEntry) {
            if (validCount >= max) continue
            validCount++
        }
        out.add(line)
    }
    return out.joinToString("\n")
}

private val POOL_PINNED_DNS = listOf(
    "208.67.222.222", "208.67.220.220",
    "8.8.8.8", "8.8.4.4",
    "77.88.8.2", "77.88.8.88",
    "1.1.1.2", "1.0.0.2",
    "223.5.5.5", "223.6.6.6",
    "80.80.80.80", "80.80.81.81",
    "9.9.9.9", "149.112.112.112"
)

private fun sampleRandomResolversFromBuiltIn(
    context: android.content.Context,
    count: Int,
): String = try {
    // Collect tier 1 + tier 2 only (stop at the second "# SHUFFLE_BELOW" marker).
    // Tier 3 has 55k+ IPs that are not useful for random pool picks.
    val pool = ArrayList<String>(count * 2)
    var shuffleBelowCount = 0
    context.resources.openRawResource(app.slipnet.R.raw.resolvers)
        .bufferedReader().useLines { lines ->
            for (raw in lines) {
                val line = raw.trim()
                if (line == "# SHUFFLE_BELOW") {
                    shuffleBelowCount++
                    if (shuffleBelowCount >= 2) return@useLines  // stop after tier 2
                    continue
                }
                if (line.isEmpty() || line.startsWith("#")) continue
                pool.add(line)
            }
        }
    val sampled = pool.shuffled().take(count).toMutableList()
    val sampledSet = sampled.toHashSet()
    val missing = POOL_PINNED_DNS.filter { it !in sampledSet }
    // Prepend pinned IPs, trim tail to stay within cap
    val result = (missing + sampled).take(count)
    result.joinToString("\n")
} catch (_: Exception) {
    ""
}

private fun dnsWorkerName(resources: android.content.res.Resources, mode: DnsWorkerMode): String = when (mode) {
    DnsWorkerMode.PER_QUERY -> resources.getString(R.string.settings_per_query_default_4f9b01)
    DnsWorkerMode.TWO -> resources.getString(R.string.settings_2_workers_f6611e)
    DnsWorkerMode.THREE -> resources.getString(R.string.settings_3_workers_56c416)
    DnsWorkerMode.FIVE -> resources.getString(R.string.settings_5_workers_fastest_205969)
}

private fun sshCipherName(resources: android.content.res.Resources, cipher: SshCipher): String = when (cipher) {
    SshCipher.AUTO -> resources.getString(R.string.settings_auto_fastest_a3066d)
    SshCipher.AES_128_CTR -> resources.getString(R.string.settings_aes_128_ctr_legacy_086a2b)
    else -> cipher.displayName
}

private fun countryName(resources: android.content.res.Resources, country: GeoBypassCountry): String = when (country) {
    GeoBypassCountry.IR -> resources.getString(R.string.settings_iran_889224)
    GeoBypassCountry.CN -> resources.getString(R.string.settings_china_d2eaf2)
    GeoBypassCountry.RU -> resources.getString(R.string.settings_russia_6754fe)
}
