package app.slipnet.presentation.profiles

import app.slipnet.presentation.theme.rv
import android.net.Uri
import kotlin.math.roundToInt
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.FileUpload
import androidx.compose.material.icons.filled.Dns
import androidx.compose.material.icons.filled.Key
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.LockOpen
import androidx.compose.material.icons.filled.Language
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Share
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material.icons.filled.OpenInNew
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Send
import androidx.compose.material.icons.filled.SmartToy
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.ui.draw.rotate
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.KeyboardArrowRight
import androidx.compose.ui.draw.clip
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.RadioButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Slider
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import app.slipnet.domain.model.CongestionControl
import app.slipnet.domain.model.DnsTransport
import app.slipnet.domain.model.ResolverMode
import app.slipnet.domain.model.SoarConfig
import app.slipnet.domain.model.SshAuthType
import app.slipnet.tunnel.DOH_SERVERS
import app.slipnet.tunnel.DohServer

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun EditProfileScreen(
    profileId: Long?,
    onNavigateBack: () -> Unit,
    onNavigateToScanner: ((Long?) -> Unit)? = null,
    selectedResolvers: String? = null,
    /** "UDP" / "TCP" / "MIXED" from a BOTH-mode scan, or null when no hint is available. */
    selectedResolversTransportHint: String? = null,
    viewModel: EditProfileViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    val messageResources = androidx.compose.ui.platform.LocalResources.current
    val globalResolverEnabled by viewModel.globalResolverEnabled.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scrollState = rememberScrollState()
    var showUnlockDialog by remember { mutableStateOf(false) }
    var unlockPassword by remember { mutableStateOf("") }
    var unlockError by remember { mutableStateOf(false) }

    // Apply selected resolvers from scanner
    LaunchedEffect(selectedResolvers) {
        selectedResolvers?.let { resolvers ->
            if (resolvers.isNotBlank()) {
                viewModel.updateResolvers(resolvers)
            }
        }
    }

    // Apply transport hint from BOTH-mode scan. Only acts when the profile's current
    // transport is UDP/TCP (DoT/DoH choices are left alone). MIXED triggers a warning.
    LaunchedEffect(selectedResolversTransportHint) {
        if (uiState.isSoarBased && selectedResolversTransportHint != null) {
            // Soar reads its TCP/53 policy, not the DNS transport the hint is meant for.
            if (viewModel.applySoarScanHint(selectedResolversTransportHint)) {
                snackbarHostState.showSnackbar(
                    messageResources.getString(app.slipnet.R.string.editor_soar_scan_tcp_applied)
                )
            }
            return@LaunchedEffect
        }
        when (selectedResolversTransportHint) {
            "UDP", "TCP" -> viewModel.applyScanTransportHint(selectedResolversTransportHint)
            "MIXED" -> snackbarHostState.showSnackbar(
                messageResources.getString(app.slipnet.R.string.editor_resolver_transport_conflict)
            )
            else -> {}
        }
    }

    LaunchedEffect(uiState.saveSuccess) {
        if (uiState.saveSuccess) {
            if (uiState.showRestartVpnMessage) {
                android.widget.Toast.makeText(
                    context,
                    messageResources.getString(app.slipnet.R.string.editor_saved_reconnect_hint),
                    android.widget.Toast.LENGTH_LONG
                ).show()
            }
            onNavigateBack()
        }
    }

    // Navigate to scanner after profile is saved
    LaunchedEffect(uiState.savedProfileIdForScanner) {
        uiState.savedProfileIdForScanner?.let { savedId ->
            viewModel.clearScannerNavigation()
            onNavigateToScanner?.invoke(savedId)
        }
    }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { error ->
            snackbarHostState.showSnackbar(error)
            viewModel.clearError()
        }
    }

    if (uiState.needsCredentials) {
        ProfileCredentialRecovery(uiState, onNavigateBack, viewModel::retryCredentials, viewModel::restoreCredentials, snackbarHostState)
        return
    }

    if (uiState.tunnelType == app.slipnet.domain.model.TunnelType.XRAY && !uiState.isLocked && !uiState.isLoading) {
        CoreProfileEditor(uiState, viewModel::updateCoreField, viewModel::updateName, viewModel::save,
            onNavigateBack, snackbarHostState)
        return
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        if (uiState.isLocked) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_locked_profile)
                        else if (profileId != null) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_edit_profile_fec2ac)
                        else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_add_profile)
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.recovery_back))
                    }
                },
                actions = {
                    val lockedCanEditDns = uiState.isDnsttOrNoizOrVaydnsBased || uiState.isSlipstreamBased || uiState.isSoarBased
                    val lockedCanEditCreds = (uiState.useSsh && uiState.sshAuthType == SshAuthType.PASSWORD) ||
                            uiState.isSocks5 ||
                            uiState.isNaiveBased ||
                            (uiState.showConnectionMethod && !uiState.useSsh && !uiState.isNaiveBased && !uiState.isSoarBased)
                    val lockedCanEdit = uiState.isLocked && (lockedCanEditDns || lockedCanEditCreds)
                    if (!uiState.isLocked || lockedCanEdit) {
                        if (uiState.isSaving) {
                            CircularProgressIndicator(
                                modifier = Modifier.padding(16.dp),
                                strokeWidth = 2.dp
                            )
                        } else {
                            IconButton(onClick = { viewModel.save() }) {
                                Icon(Icons.Default.Check, contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_save_1509f5))
                            }
                        }
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) }
    ) { paddingValues ->
        if (uiState.isLoading) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator()
            }
        } else if (uiState.isLocked) {
            // Locked profile view — only the header is in a Card; the rest of
            // the sections (DNS, Credentials) match the non-locked screen's
            // flat titleMedium-header style.
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Header card: name + tunnel type + locked-state info rows
                EditorSection {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.surfaceContainerLow
                        ),
                        shape = RoundedCornerShape(16.dp)
                    ) {
                        Column(
                            modifier = Modifier.padding(20.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(12.dp)
                            ) {
                                Surface(
                                    shape = CircleShape,
                                    color = MaterialTheme.colorScheme.primaryContainer,
                                    modifier = Modifier.size(44.dp)
                                ) {
                                    Box(contentAlignment = Alignment.Center) {
                                        Icon(
                                            imageVector = Icons.Default.Lock,
                                            contentDescription = null,
                                            modifier = Modifier.size(22.dp),
                                            tint = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                }
                                Column {
                                    Text(
                                        text = uiState.name,
                                        style = MaterialTheme.typography.titleLarge,
                                        fontWeight = FontWeight.SemiBold
                                    )
                                    Text(
                                        text = uiState.tunnelType.displayName,
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                }
                            }

                            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))

                            if (uiState.expirationDate > 0) {
                                val isExpired = System.currentTimeMillis() > uiState.expirationDate
                                val dateStr = java.text.SimpleDateFormat("MMM dd, yyyy HH:mm", java.util.Locale.getDefault())
                                    .format(java.util.Date(uiState.expirationDate))
                                LockedInfoRow(
                                    icon = if (isExpired) Icons.Default.Warning else Icons.Default.Schedule,
                                    label = if (isExpired) androidx.compose.ui.res.stringResource(app.slipnet.R.string.ui_expired) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_expires),
                                    value = dateStr,
                                    valueColor = if (isExpired) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurface
                                )
                            }
                            if (uiState.boundDeviceId.isNotEmpty()) {
                                LockedInfoRow(
                                    icon = Icons.Default.PhoneAndroid,
                                    label = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_device),
                                    value = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_bound)
                                )
                            }
                            val displayUsername = when {
                                uiState.useSsh && uiState.sshUsername.isNotBlank() -> uiState.sshUsername
                                uiState.isNaiveBased && uiState.naiveUsername.isNotBlank() -> uiState.naiveUsername
                                uiState.socksUsername.isNotBlank() -> uiState.socksUsername
                                else -> null
                            }
                            if (displayUsername != null) {
                                LockedInfoRow(
                                    icon = Icons.Default.Person,
                                    label = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_username_e3b89e),
                                    value = displayUsername
                                )
                            }
                            LockedInfoRow(
                                icon = Icons.Default.Share,
                                label = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_re_sharing),
                                value = if (uiState.allowSharing) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_allowed) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_disabled_75081b)
                            )
                        }
                    }
                }

                // DNS settings section
                EditorSection {
                    if (uiState.isDnsttOrNoizOrVaydnsBased || uiState.isSlipstreamBased || uiState.isSoarBased) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_settings),
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_locked_dns_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            verticalArrangement = Arrangement.spacedBy(14.dp)
                        ) {

                                // DNS Transport selector (DNSTT-based profiles only)
                                EditorSection {
                                    if (uiState.isDnsttOrNoizOrVaydnsBased) {
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_transport),
                                            style = MaterialTheme.typography.titleMedium,
                                            modifier = Modifier.padding(top = 8.dp)
                                        )

                                        Row(
                                            modifier = Modifier.fillMaxWidth(),
                                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                                        ) {
                                            DnsTransport.entries.forEach { transport ->
                                                if (uiState.dnsTransport == transport) {
                                                    Button(
                                                        onClick = { },
                                                        modifier = Modifier.weight(1f)
                                                    ) {
                                                        Text(transport.displayName)
                                                    }
                                                } else {
                                                    OutlinedButton(
                                                        onClick = { viewModel.updateDnsTransport(transport) },
                                                        modifier = Modifier.weight(1f)
                                                    ) {
                                                        Text(transport.displayName)
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // DoH URL for DNSTT with DoH transport
                                EditorSection {
                                    if (uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOH) {
                                        DohServerSelector(
                                            dohUrl = uiState.dohUrl,
                                            dohUrlError = uiState.dohUrlError,
                                            onUrlChange = { viewModel.updateDohUrl(it) },
                                            onPresetSelected = { viewModel.selectDohPreset(it) },
                                            onTestServers = { scope -> viewModel.testDohServers(scope) },
                                            customDohUrls = uiState.customDohUrls,
                                            onCustomDohUrlsChange = { viewModel.updateCustomDohUrls(it) }
                                        )
                                    }
                                }

                                // Resolver field (not shown when DNSTT with DoH transport)
                                EditorSection {
                                    if (!(uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOH)) {
                                        if (globalResolverEnabled) {
                                            Card(
                                                colors = CardDefaults.cardColors(
                                                    containerColor = MaterialTheme.colorScheme.primaryContainer
                                                ),
                                                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp)
                                            ) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_global_dns_hint),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                                                    modifier = Modifier.padding(12.dp)
                                                )
                                            }
                                        }
                                        if (uiState.resolversHidden) {
                                            // Hidden resolver: show count and toggle for custom override
                                            val resolverCount = uiState.defaultResolversList.size
                                            Text(
                                                text = if (resolverCount > 0) androidx.compose.ui.res.pluralStringResource(app.slipnet.R.plurals.editor_hidden_resolvers, resolverCount, resolverCount)
                                                       else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver_default_hidden),
                                                style = MaterialTheme.typography.bodyMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                            Row(
                                                modifier = Modifier.fillMaxWidth(),
                                                verticalAlignment = Alignment.CenterVertically,
                                                horizontalArrangement = Arrangement.SpaceBetween
                                            ) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_use_custom_resolver),
                                                    style = MaterialTheme.typography.bodyMedium
                                                )
                                                Switch(
                                                    checked = uiState.useCustomResolver,
                                                    onCheckedChange = { viewModel.updateUseCustomResolver(it) }
                                                )
                                            }
                                            if (uiState.useCustomResolver) {
                                                val isDoT = uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOT
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.resolvers,
                                                    onValueChange = { viewModel.updateResolvers(it) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver)) },
                                                    placeholder = { Text(if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_e_g_dns_google_853) else "e.g. 8.8.8.8:53") },
                                                    isError = uiState.resolversError != null,
                                                    supportingText = {
                                                        Text(uiState.resolversError ?: if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_853) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_port))
                                                    },
                                                    trailingIcon = {
                                                        IconButton(
                                                            onClick = { viewModel.autoDetectResolver() },
                                                            enabled = !uiState.isAutoDetecting
                                                        ) {
                                                            if (uiState.isAutoDetecting) {
                                                                CircularProgressIndicator(
                                                                    modifier = Modifier.size(18.dp),
                                                                    strokeWidth = 2.dp
                                                                )
                                                            } else {
                                                                Text(
                                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_local),
                                                                    style = MaterialTheme.typography.labelMedium,
                                                                    color = MaterialTheme.colorScheme.primary
                                                                )
                                                            }
                                                        }
                                                    },
                                                    singleLine = true,
                                                    modifier = Modifier.fillMaxWidth(),
                                                    shape = RoundedCornerShape(12.dp)
                                                )
                                                if (onNavigateToScanner != null) {
                                                    Button(
                                                        onClick = { viewModel.saveForScanner() },
                                                        modifier = Modifier.fillMaxWidth(),
                                                        shape = RoundedCornerShape(10.dp)
                                                    ) {
                                                        Icon(Icons.Default.Search, contentDescription = null, modifier = Modifier.size(18.dp))
                                                        Spacer(Modifier.width(8.dp))
                                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_scan_resolvers))
                                                    }
                                                }
                                            }
                                        } else {
                                            val isDoT = uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOT
                                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                value = uiState.resolvers,
                                                onValueChange = { viewModel.updateResolvers(it) },
                                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver)) },
                                                placeholder = { Text(if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_e_g_dns_google_853) else "e.g. 8.8.8.8:53") },
                                                isError = uiState.resolversError != null,
                                                supportingText = {
                                                    Text(uiState.resolversError ?: if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_853) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_port))
                                                },
                                                trailingIcon = {
                                                    IconButton(
                                                        onClick = { viewModel.autoDetectResolver() },
                                                        enabled = !uiState.isAutoDetecting
                                                    ) {
                                                        if (uiState.isAutoDetecting) {
                                                            CircularProgressIndicator(
                                                                modifier = Modifier.size(18.dp),
                                                                strokeWidth = 2.dp
                                                            )
                                                        } else {
                                                            Text(
                                                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_local),
                                                                style = MaterialTheme.typography.labelMedium,
                                                                color = MaterialTheme.colorScheme.primary
                                                            )
                                                        }
                                                    }
                                                },
                                                singleLine = true,
                                                modifier = Modifier.fillMaxWidth(),
                                                shape = RoundedCornerShape(12.dp)
                                            )

                                            if (onNavigateToScanner != null) {
                                                Button(
                                                    onClick = { viewModel.saveForScanner() },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    shape = RoundedCornerShape(10.dp)
                                                ) {
                                                    Icon(Icons.Default.Search, contentDescription = null, modifier = Modifier.size(18.dp))
                                                    Spacer(Modifier.width(8.dp))
                                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_scan_resolvers))
                                                }
                                            }
                                        }
                                        // Multi-resolver mode + spread count (only shown when 2+ resolvers)
                                        // Check both visible resolvers and hidden defaults
                                        val hasMultipleResolvers = uiState.resolvers.contains(",") ||
                                            (!uiState.useCustomResolver && uiState.defaultResolversList.size >= 2)
                                        if (hasMultipleResolvers && !uiState.isSlipstreamBased && !uiState.isSoarBased) {
                                            MultiResolverSettings(
                                                resolverMode = uiState.resolverMode,
                                                rrSpreadCount = uiState.rrSpreadCount,
                                                onResolverModeChange = { viewModel.updateResolverMode(it) },
                                                onSpreadCountChange = { viewModel.updateRrSpreadCount(it) }
                                            )
                                        }
                                    }
                                }

                                // Soar discovery / TCP settings (locked profiles)
                                EditorSection {
                                    if (uiState.isSoarBased) {
                                        SoarSettings(
                                            discovery = uiState.soarDiscovery,
                                            country = uiState.soarCountry,
                                            discoveryList = uiState.soarDiscoveryList,
                                            customList = uiState.soarDiscoveryCustom,
                                            customListError = uiState.soarDiscoveryCustomError,
                                            customListHidden = uiState.soarDiscoveryCustomHidden,
                                            discoveryError = uiState.soarDiscoveryError,
                                            tcpMode = uiState.soarTcpMode,
                                            onDiscoveryChange = { viewModel.updateSoarDiscovery(it) },
                                            onCountryChange = { viewModel.updateSoarCountry(it) },
                                            onDiscoveryListChange = { viewModel.updateSoarDiscoveryList(it) },
                                            onCustomListChange = { viewModel.updateSoarDiscoveryCustom(it) },
                                            onTcpModeChange = { viewModel.updateSoarTcpMode(it) }
                                        )
                                    }
                                }

                                // DNS Query Size (locked profiles, not for VayDNS — it uses QNAME length instead).
                                // Hidden when Auto is on.
                                EditorSection {
                                    if (uiState.isDnsttOrNoizOrVaydnsBased && !uiState.isVaydnsBased && !uiState.dnsAutoTune) {
                                        var showMtuDialogLocked by remember { mutableStateOf(false) }
                                        HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clip(RoundedCornerShape(8.dp))
                                                .clickable { showMtuDialogLocked = true }
                                                .padding(vertical = 12.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_query_size),
                                                    style = MaterialTheme.typography.bodyLarge
                                                )
                                                Text(
                                                    text = if (uiState.dnsPayloadSize == 0) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_full_capacity_fastest)
                                                           else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes_per_query, (uiState.dnsPayloadSize).toString()),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Icon(
                                                Icons.AutoMirrored.Filled.KeyboardArrowRight,
                                                contentDescription = null,
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }

                                        if (showMtuDialogLocked) {
                                            val mtuPresetsLocked = listOf(
                                                0 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_size_full_hint),
                                                100 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_large_good_balance),
                                                80 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_medium_less_conspicuous),
                                                60 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_small_stealthier_slower),
                                                50 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_size_minimum_hint)
                                            )
                                            val isCustomLocked = mtuPresetsLocked.none { it.first == uiState.dnsPayloadSize }
                                            var customMtuTextLocked by remember { mutableStateOf(if (isCustomLocked) uiState.dnsPayloadSize.toString() else "") }
                                            var useCustomLocked by remember { mutableStateOf(isCustomLocked) }
                                            AlertDialog(
                                                onDismissRequest = { showMtuDialogLocked = false },
                                                title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_query_size)) },
                                                text = {
                                                    Column {
                                                        Text(
                                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_size_hint),
                                                            style = MaterialTheme.typography.bodySmall,
                                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                            modifier = Modifier.padding(bottom = 12.dp)
                                                        )
                                                        mtuPresetsLocked.forEach { (size, desc) ->
                                                            Row(
                                                                modifier = Modifier
                                                                    .fillMaxWidth()
                                                                    .clip(RoundedCornerShape(8.dp))
                                                                    .clickable {
                                                                        useCustomLocked = false
                                                                        viewModel.updateDnsPayloadSize(size)
                                                                        showMtuDialogLocked = false
                                                                    }
                                                                    .padding(vertical = 10.dp, horizontal = 8.dp),
                                                                verticalAlignment = Alignment.CenterVertically
                                                            ) {
                                                                RadioButton(
                                                                    selected = !useCustomLocked && uiState.dnsPayloadSize == size,
                                                                    onClick = {
                                                                        useCustomLocked = false
                                                                        viewModel.updateDnsPayloadSize(size)
                                                                        showMtuDialogLocked = false
                                                                    }
                                                                )
                                                                Column(modifier = Modifier.padding(start = 8.dp)) {
                                                                    Text(text = if (size == 0) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_full) else "$size")
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
                                                                .clickable { useCustomLocked = true }
                                                                .padding(vertical = 10.dp, horizontal = 8.dp),
                                                            verticalAlignment = Alignment.CenterVertically
                                                        ) {
                                                            RadioButton(
                                                                selected = useCustomLocked,
                                                                onClick = { useCustomLocked = true }
                                                            )
                                                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                                value = customMtuTextLocked,
                                                                onValueChange = { customMtuTextLocked = it.filter { c -> c.isDigit() }.take(3) },
                                                                enabled = useCustomLocked,
                                                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_custom_081ae3)) },
                                                                placeholder = { Text("50–120") },
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
                                                    if (useCustomLocked) {
                                                        TextButton(
                                                            onClick = {
                                                                val value = customMtuTextLocked.toIntOrNull()
                                                                if (value != null && value in 50..120) {
                                                                    viewModel.updateDnsPayloadSize(value)
                                                                    showMtuDialogLocked = false
                                                                }
                                                            }
                                                        ) {
                                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392))
                                                        }
                                                    }
                                                },
                                                dismissButton = {
                                                    TextButton(onClick = { showMtuDialogLocked = false }) {
                                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                                                    }
                                                }
                                            )
                                        }
                                    }
                                }

                                // Auto toggle for DNSTT/NoizDNS (locked) — sits below DNS Query Size.
                                // Hidden when Authoritative is on (probe would be a no-op).
                                EditorSection {
                                    if (uiState.isDnsttOrNoizBased && !uiState.dnsttAuthoritative) {
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .padding(vertical = 8.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_auto_tune),
                                                    style = MaterialTheme.typography.bodyLarge
                                                )
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_autotune_length_hint),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Switch(
                                                checked = uiState.dnsAutoTune,
                                                onCheckedChange = { viewModel.updateDnsAutoTune(it) }
                                            )
                                        }
                                    }
                                }

                                // Stealth mode (locked profiles, NoizDNS only)
                                EditorSection {
                                    if (uiState.isNoizdnsBased) {
                                        HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .padding(vertical = 4.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_stealth_mode),
                                                    style = MaterialTheme.typography.bodyLarge
                                                )
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_slower_speed_harder_to_detect),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Switch(
                                                checked = uiState.noizdnsStealth,
                                                onCheckedChange = { viewModel.updateNoizdnsStealth(it) }
                                            )
                                        }
                                        if (uiState.noizdnsStealth && !uiState.dnsttAuthoritative) {
                                            Text(
                                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_internet_speed_will_be_reduced),
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.error
                                            )
                                        }
                                    }
                                }

                                // VayDNS settings (locked profiles)
                                EditorSection {
                                    if (uiState.isVaydnsBased) {
                                        HorizontalDivider(modifier = Modifier.padding(vertical = 4.dp))

                                        // Response Record Type selector
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_response_record_type),
                                            style = MaterialTheme.typography.titleMedium,
                                            modifier = Modifier.padding(top = 8.dp)
                                        )
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_record_type_hint),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            modifier = Modifier.padding(bottom = 4.dp)
                                        )
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .horizontalScroll(rememberScrollState()),
                                            horizontalArrangement = Arrangement.spacedBy(6.dp)
                                        ) {
                                            listOf("txt", "cname", "a", "aaaa", "mx", "ns", "srv", "null", "caa").forEach { type ->
                                                if (uiState.vaydnsRecordType == type) {
                                                    Button(onClick = { }) {
                                                        Text(type.uppercase())
                                                    }
                                                } else {
                                                    OutlinedButton(onClick = { viewModel.updateVaydnsRecordType(type) }) {
                                                        Text(type.uppercase())
                                                    }
                                                }
                                            }
                                        }

                                        // Auto toggle (locked, VayDNS) — sits below Record Type.
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .padding(vertical = 8.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_auto_tune),
                                                    style = MaterialTheme.typography.bodyLarge
                                                )
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_autotune_rate_hint),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Switch(
                                                checked = uiState.dnsAutoTune,
                                                onCheckedChange = { viewModel.updateDnsAutoTune(it) }
                                            )
                                        }

                                        // QNAME Length — hidden when Auto is on.
                                        var showQnameDialogLocked by remember { mutableStateOf(false) }
                                        if (!uiState.dnsAutoTune) {
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clip(RoundedCornerShape(8.dp))
                                                .clickable { showQnameDialogLocked = true }
                                                .padding(vertical = 12.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Column(modifier = Modifier.weight(1f)) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_length),
                                                    style = MaterialTheme.typography.bodyLarge
                                                )
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes, (uiState.vaydnsMaxQnameLen).toString()) + when {
                                                        uiState.vaydnsMaxQnameLen <= 80 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_stealthy)
                                                        uiState.vaydnsMaxQnameLen <= 120 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_balanced)
                                                        uiState.vaydnsMaxQnameLen <= 180 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fast)
                                                        else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_maximum)
                                                    },
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                                )
                                            }
                                            Icon(
                                                Icons.Default.KeyboardArrowRight,
                                                contentDescription = null,
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                        if (showQnameDialogLocked) {
                                            val minQname = 60
                                            val maxQname = 253
                                            val midQname = 101
                                            fun toSlider(qname: Int): Float {
                                                val clamped = qname.coerceIn(minQname, maxQname)
                                                return if (clamped <= midQname) {
                                                    (clamped - minQname).toFloat() / (midQname - minQname) * 0.5f
                                                } else {
                                                    0.5f + (clamped - midQname).toFloat() / (maxQname - midQname) * 0.5f
                                                }
                                            }
                                            fun fromSlider(value: Float): Int = if (value <= 0.5f) {
                                                (minQname + (value / 0.5f) * (midQname - minQname)).roundToInt()
                                            } else {
                                                (midQname + ((value - 0.5f) / 0.5f) * (maxQname - midQname)).roundToInt()
                                            }
                                            var sliderValue by remember { mutableStateOf(toSlider(uiState.vaydnsMaxQnameLen)) }
                                            val displayValue = fromSlider(sliderValue)
                                            var customText by remember { mutableStateOf("") }
                                            var customError by remember { mutableStateOf(false) }
                                            AlertDialog(
                                                onDismissRequest = { showQnameDialogLocked = false },
                                                title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_length)) },
                                                text = {
                                                    Column {
                                                        Text(
                                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes, (displayValue).toString()),
                                                            style = MaterialTheme.typography.titleMedium,
                                                            modifier = Modifier.fillMaxWidth(),
                                                            textAlign = TextAlign.Center
                                                        )
                                                        Slider(
                                                            value = sliderValue,
                                                            onValueChange = { sliderValue = it; customText = ""; customError = false },
                                                            valueRange = 0f..1f,
                                                            steps = 19,
                                                            modifier = Modifier.fillMaxWidth()
                                                        )
                                                        Row(
                                                            modifier = Modifier.fillMaxWidth(),
                                                            horizontalArrangement = Arrangement.SpaceBetween
                                                        ) {
                                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_60_stealthy), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_101_default), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_253_fastest), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                                        }
                                                        Spacer(modifier = Modifier.height(12.dp))
                                                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                            value = customText,
                                                            onValueChange = { text ->
                                                                customText = text
                                                                val v = text.toIntOrNull()
                                                                if (v != null && v in minQname..maxQname) {
                                                                    sliderValue = toSlider(v)
                                                                    customError = false
                                                                } else {
                                                                    customError = text.isNotEmpty()
                                                                }
                                                            },
                                                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_value)) },
                                                            placeholder = { Text("$minQname–$maxQname") },
                                                            isError = customError,
                                                            supportingText = if (customError) {{ Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_must_be_value_value, (minQname).toString(), (maxQname).toString())) }} else null,
                                                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                            singleLine = true,
                                                            modifier = Modifier.fillMaxWidth()
                                                        )
                                                    }
                                                },
                                                confirmButton = {
                                                    TextButton(onClick = {
                                                        val custom = customText.toIntOrNull()
                                                        val finalValue = if (custom != null && custom in minQname..maxQname) custom else displayValue
                                                        viewModel.updateVaydnsMaxQnameLen(finalValue)
                                                        showQnameDialogLocked = false
                                                    }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392)) }
                                                },
                                                dismissButton = {
                                                    Row {
                                                        if (displayValue != midQname) {
                                                            TextButton(onClick = {
                                                                sliderValue = 0.5f; customText = ""; customError = false
                                                                viewModel.updateVaydnsMaxQnameLen(midQname)
                                                                showQnameDialogLocked = false
                                                            }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_reset_daee76)) }
                                                        }
                                                        TextButton(onClick = { showQnameDialogLocked = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
                                                    }
                                                }
                                            )
                                        }
                                        } // end if (!uiState.dnsAutoTune)

                                        // Rate limit — hidden when Auto is on.
                                        if (!uiState.dnsAutoTune) {
                                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                value = uiState.vaydnsRps,
                                                onValueChange = { viewModel.updateVaydnsRps(it.filter { c -> c.isDigit() || c == '.' }.take(6)) },
                                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_rate_limit_q_s)) },
                                                supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_rate_hint)) },
                                                modifier = Modifier.fillMaxWidth(),
                                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                                                singleLine = true
                                            )
                                        }

                                        // Advanced settings (hidden by default)
                                        Row(
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clip(RoundedCornerShape(8.dp))
                                                .clickable { viewModel.toggleVaydnsAdvanced() }
                                                .padding(vertical = 12.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically
                                        ) {
                                            Text(
                                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_advanced_9f088d),
                                                style = MaterialTheme.typography.titleMedium
                                            )
                                            Icon(
                                                Icons.Default.KeyboardArrowRight,
                                                contentDescription = if (uiState.vaydnsAdvancedExpanded) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_collapse) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_expand),
                                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                                modifier = Modifier.rotate(if (uiState.vaydnsAdvancedExpanded) 90f else 0f)
                                            )
                                        }

                                        AnimatedVisibility(visible = uiState.vaydnsAdvancedExpanded) {
                                            Column(
                                                modifier = Modifier.fillMaxWidth(),
                                                verticalArrangement = Arrangement.spacedBy(4.dp)
                                            ) {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_advanced_hint),
                                                    style = MaterialTheme.typography.bodySmall,
                                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                    modifier = Modifier.padding(bottom = 4.dp)
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.vaydnsIdleTimeout,
                                                    onValueChange = { viewModel.updateVaydnsIdleTimeout(it.filter { c -> c.isDigit() }.take(4)) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_idle_timeout_seconds)) },
                                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_10_default)) },
                                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_session_idle_timeout)) },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                    singleLine = true
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.vaydnsKeepalive,
                                                    onValueChange = { viewModel.updateVaydnsKeepalive(it.filter { c -> c.isDigit() }.take(4)) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_keepalive_seconds)) },
                                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_2_default)) },
                                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_keepalive_interval)) },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                    singleLine = true
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.vaydnsUdpTimeout,
                                                    onValueChange = { viewModel.updateVaydnsUdpTimeout(it.filter { c -> c.isDigit() }.take(5)) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_udp_timeout_ms)) },
                                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_500_default)) },
                                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_udp_timeout_hint)) },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                    singleLine = true
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.vaydnsMaxNumLabels,
                                                    onValueChange = { viewModel.updateVaydnsMaxNumLabels(it.filter { c -> c.isDigit() }.take(2)) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_max_labels)) },
                                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlimited)) },
                                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_max_labels_hint)) },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                    singleLine = true
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = uiState.vaydnsClientIdSize,
                                                    onValueChange = { viewModel.updateVaydnsClientIdSize(it.filter { c -> c.isDigit() }.take(2)) },
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_client_id_size_bytes)) },
                                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_2_default)) },
                                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_client_id_hint)) },
                                                    modifier = Modifier.fillMaxWidth(),
                                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                                    singleLine = true
                                                )
                                            }
                                        }
                                    }
                                }
                        }
                    }
                }

                // Credentials section — editable username and password (masked)
                val showSshCreds = uiState.useSsh && uiState.sshAuthType == SshAuthType.PASSWORD
                val showSocksCreds = uiState.isSocks5 ||
                        (uiState.showConnectionMethod && !uiState.useSsh && !uiState.isNaiveBased && !uiState.isSoarBased)
                val showNaiveCreds = uiState.isNaiveBased
                EditorSection {
                    if (showSshCreds || showSocksCreds || showNaiveCreds) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_credentials),
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_editable_credentials_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            verticalArrangement = Arrangement.spacedBy(14.dp)
                        ) {

                                if (showSocksCreds) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.socksUsername,
                                        onValueChange = { viewModel.updateSocksUsername(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks5_username)) },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                    var lockedSocksPasswordVisible by remember { mutableStateOf(false) }
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.socksPassword,
                                        onValueChange = { viewModel.updateSocksPassword(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks5_password)) },
                                        visualTransformation = if (lockedSocksPasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                        trailingIcon = {
                                            IconButton(onClick = { lockedSocksPasswordVisible = !lockedSocksPasswordVisible }) {
                                                Text(
                                                    text = if (lockedSocksPasswordVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary
                                                )
                                            }
                                        },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                }

                                if (showNaiveCreds) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.naiveUsername,
                                        onValueChange = { viewModel.updateNaiveUsername(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_username)) },
                                        isError = uiState.naiveUsernameError != null,
                                        supportingText = uiState.naiveUsernameError?.let { { Text(it) } },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.naivePassword,
                                        onValueChange = { viewModel.updateNaivePassword(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_password)) },
                                        visualTransformation = PasswordVisualTransformation(),
                                        isError = uiState.naivePasswordError != null,
                                        supportingText = uiState.naivePasswordError?.let { { Text(it) } },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                }

                                if (showSshCreds) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sshUsername,
                                        onValueChange = { viewModel.updateSshUsername(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_username)) },
                                        isError = uiState.sshUsernameError != null,
                                        supportingText = uiState.sshUsernameError?.let { { Text(it) } },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                    var lockedSshPasswordVisible by remember { mutableStateOf(false) }
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sshPassword,
                                        onValueChange = { viewModel.updateSshPassword(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_password)) },
                                        visualTransformation = if (lockedSshPasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                        trailingIcon = {
                                            IconButton(onClick = { lockedSshPasswordVisible = !lockedSshPasswordVisible }) {
                                                Text(
                                                    text = if (lockedSshPasswordVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary
                                                )
                                            }
                                        },
                                        isError = uiState.sshPasswordError != null,
                                        supportingText = uiState.sshPasswordError?.let { { Text(it) } },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(12.dp)
                                    )
                                }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(24.dp))

                // Unlock button
                EditorSection {
                    OutlinedButton(
                        onClick = {
                            unlockPassword = ""
                            unlockError = false
                            showUnlockDialog = true
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(bottom = 8.dp),
                        shape = RoundedCornerShape(12.dp),
                        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline)
                    ) {
                        Icon(
                            imageVector = Icons.Default.LockOpen,
                            contentDescription = null,
                            modifier = Modifier.size(18.dp)
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlock_profile))
                    }
                }
            }

            // Unlock dialog
            if (showUnlockDialog) {
                AlertDialog(
                    onDismissRequest = { showUnlockDialog = false },
                    title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlock_profile)) },
                    text = {
                        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlock_hint))
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = unlockPassword,
                                onValueChange = {
                                    unlockPassword = it
                                    unlockError = false
                                },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e)) },
                                visualTransformation = PasswordVisualTransformation(),
                                isError = unlockError,
                                supportingText = if (unlockError) {
                                    { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_incorrect_password)) }
                                } else null,
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                        }
                    },
                    confirmButton = {
                        TextButton(
                            onClick = {
                                viewModel.unlockProfile(unlockPassword) { success ->
                                    if (success) {
                                        showUnlockDialog = false
                                    } else {
                                        unlockError = true
                                    }
                                }
                            },
                            enabled = unlockPassword.isNotBlank()
                        ) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlock)) }
                    },
                    dismissButton = {
                        TextButton(onClick = { showUnlockDialog = false }) { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e)) }
                    }
                )
            }
        } else {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
                    .imePadding()
                    .verticalScroll(scrollState)
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Name
                EditorSection {
                    OutlinedTextField(
                        value = uiState.name,
                        onValueChange = { viewModel.updateName(it) },
                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_profile_name)) },
                        placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_my_vpn_server)) },
                        isError = uiState.nameError != null,
                        supportingText = uiState.nameError?.let { { Text(it) } },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                }

                // Domain / SSH Server (hidden for DOH, Snowflake, SOCKS5, and VLESS profiles)
                EditorSection {
                    if (!uiState.isDoh && !uiState.isSnowflake && !uiState.isSocks5 && !uiState.isVless) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.domain,
                            onValueChange = { viewModel.updateDomain(it) },
                            label = {
                                Text(
                                    when {
                                        uiState.isSshOnly -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_server)
                                        uiState.isNaiveBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_server_aef7de)
                                        else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_domain)
                                    }
                                )
                            },
                            placeholder = {
                                Text(
                                    when {
                                        uiState.isDnsttOrNoizOrVaydnsBased || uiState.isSoarBased -> "t.example.com"
                                        uiState.isSshOnly -> "ssh.example.com"
                                        uiState.isNaiveBased -> "proxy.example.com"
                                        else -> "vpn.example.com"
                                    }
                                )
                            },
                            isError = uiState.domainError != null,
                            supportingText = {
                                Text(
                                    uiState.domainError ?: when {
                                        uiState.isNoizdnsBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_noizdns_tunnel_domain)
                                        uiState.isVaydnsBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_vaydns_tunnel_domain)
                                        uiState.isSoarBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_soar_zone_hint)
                                        uiState.isDnsttBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dnstt_tunnel_domain)
                                        uiState.isSlipstreamBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_slipstream_tunnel_domain)
                                        uiState.isNaiveBased -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_caddy_server_hostname)
                                        else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_host_hint)
                                    }
                                )
                            },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // SOCKS5 Proxy fields
                EditorSection {
                    if (uiState.isSocks5) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.domain,
                            onValueChange = { viewModel.updateDomain(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks5_server)) },
                            placeholder = { Text("proxy.example.com") },
                            isError = uiState.domainError != null,
                            supportingText = { Text(uiState.domainError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks_host_hint)) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.socks5ServerPort,
                            onValueChange = { viewModel.updateSocks5ServerPort(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_port_72e9a5)) },
                            placeholder = { Text("1080") },
                            isError = uiState.socks5ServerPortError != null,
                            supportingText = { Text(uiState.socks5ServerPortError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks5_proxy_port)) },
                            singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.socksUsername,
                            onValueChange = { viewModel.updateSocksUsername(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_username_optional)) },
                            placeholder = { Text("") },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        var socks5PasswordVisible by remember { mutableStateOf(false) }
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.socksPassword,
                            onValueChange = { viewModel.updateSocksPassword(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_password_optional)) },
                            placeholder = { Text("") },
                            singleLine = true,
                            visualTransformation = if (socks5PasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                            trailingIcon = {
                                IconButton(onClick = { socks5PasswordVisible = !socks5PasswordVisible }) {
                                    Text(
                                        text = if (socks5PasswordVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                        style = MaterialTheme.typography.labelMedium,
                                        color = MaterialTheme.colorScheme.primary
                                    )
                                }
                            },
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // VLESS fields
                EditorSection {
                    if (uiState.isVless) {
                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_vless_websocket_settings), style = MaterialTheme.typography.titleMedium)
                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_core_editor_hint), style = MaterialTheme.typography.bodySmall)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            TextButton(onClick = { viewModel.openCoreEditor("reality") }) { Text("REALITY") }
                            TextButton(onClick = { viewModel.openCoreEditor("xhttp") }) { Text("XHTTP") }
                        }

                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.vlessUuid,
                            onValueChange = { viewModel.updateVlessUuid(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_uuid)) },
                            placeholder = { Text("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx") },
                            isError = uiState.vlessUuidError != null,
                            supportingText = { Text(uiState.vlessUuidError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_vless_user_id)) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.domain,
                            onValueChange = { viewModel.updateDomain(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_server_domain)) },
                            placeholder = { Text("your-domain.example.com") },
                            isError = uiState.domainError != null,
                            supportingText = { Text(uiState.domainError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_cloudflare_domain_hint)) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.cdnIp,
                            onValueChange = { viewModel.updateCdnIp(it) },
                            label = { Text("CDN IP") },
                            placeholder = { Text("188.114.98.0") },
                            isError = uiState.cdnIpError != null,
                            supportingText = { Text(uiState.cdnIpError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_cloudflare_clean_ip_address)) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        // Security + Transport selector
                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            listOf("tls" to "TLS", "none" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_none)).forEach { (value, label) ->
                                FilterChip(
                                    selected = uiState.vlessSecurity == value,
                                    onClick = { viewModel.updateVlessSecurity(value) },
                                    label = { Text(label) }
                                )
                            }
                            Spacer(modifier = Modifier.weight(1f))
                        }
                        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.cdnPort,
                                onValueChange = { viewModel.updateCdnPort(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_cdn_port)) },
                                placeholder = { Text("443") },
                                isError = uiState.cdnPortError != null,
                                supportingText = { Text(uiState.cdnPortError ?: "") },
                                singleLine = true,
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                modifier = Modifier.weight(1f)
                            )
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.vlessWsPath,
                                onValueChange = { viewModel.updateVlessWsPath(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ws_path)) },
                                placeholder = { Text("/") },
                                singleLine = true,
                                modifier = Modifier.weight(1f)
                            )
                        }

                        // TLS DPI bypass options (only relevant when security=tls)
                        if (uiState.vlessSecurity == "tls") {
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.vlessSni,
                                onValueChange = { viewModel.updateVlessSni(it) },
                                label = { Text("TLS SNI") },
                                placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_leave_empty_to_use_ws_host)) },
                                supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tls_sni_hint)) },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )

                            // SNI Fragmentation section
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_sni_fragmentation), style = MaterialTheme.typography.bodyLarge)
                                Switch(
                                    checked = uiState.sniFragmentEnabled,
                                    onCheckedChange = { viewModel.updateSniFragmentEnabled(it) }
                                )
                            }

                            if (uiState.sniFragmentEnabled) {
                                // Strategies are ordered by effectiveness against modern
                                // reassembling DPIs — chunkier and reorder-based modes tend
                                // to outperform simple splits.
                                FlowRow(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                    verticalArrangement = Arrangement.spacedBy(8.dp)
                                ) {
                                    listOf(
                                        "micro" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_micro_mode),
                                        "multi" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_multi_mode),
                                        "disorder" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_disorder_mode),
                                        "fake" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fake_mode),
                                        "sni_split" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_sni_split_mode),
                                        "half" to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_half_mode)
                                    ).forEach { (value, label) ->
                                        FilterChip(
                                            selected = uiState.sniFragmentStrategy == value,
                                            onClick = { viewModel.updateSniFragmentStrategy(value) },
                                            label = { Text(label) }
                                        )
                                    }
                                }
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fragment_strength_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sniFragmentDelayMs,
                                    onValueChange = { viewModel.updateSniFragmentDelayMs(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fragment_delay_ms)) },
                                    placeholder = { Text("300") },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fragment_delay_hint)) },
                                    singleLine = true,
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    modifier = Modifier.fillMaxWidth()
                                )
                                if (uiState.sniFragmentStrategy == "fake" || uiState.sniFragmentStrategy == "disorder") {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sniSpoofTtl,
                                        onValueChange = { viewModel.updateSniSpoofTtl(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_decoy_ttl_hops)) },
                                        placeholder = { Text("8") },
                                        supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_decoy_ttl_hint)) },
                                        singleLine = true,
                                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                        modifier = Modifier.fillMaxWidth()
                                    )
                                }
                                if (uiState.sniFragmentStrategy == "fake") {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.fakeDecoyHost,
                                        onValueChange = { viewModel.updateFakeDecoyHost(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_decoy_hostname)) },
                                        placeholder = { Text("www.google.com") },
                                        supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_decoy_hostname_hint)) },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth()
                                    )
                                }
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.tcpMaxSeg,
                                    onValueChange = { viewModel.updateTcpMaxSeg(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_force_tcp_mss_advanced)) },
                                    placeholder = { Text("0") },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tcp_mss_hint)) },
                                    singleLine = true,
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    modifier = Modifier.fillMaxWidth()
                                )
                            }

                            // ClientHello padding
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_clienthello_padding), style = MaterialTheme.typography.bodyLarge)
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_clienthello_padding_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                                Switch(
                                    checked = uiState.chPaddingEnabled,
                                    onCheckedChange = { viewModel.updateChPaddingEnabled(it) }
                                )
                            }

                            // Header obfuscation (only for WS transport)
                            if (uiState.vlessTransport == "ws") {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_header_obfuscation), style = MaterialTheme.typography.bodyLarge)
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_header_obfuscation_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                    Switch(
                                        checked = uiState.wsHeaderObfuscation,
                                        onCheckedChange = { viewModel.updateWsHeaderObfuscation(it) }
                                    )
                                }

                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_cover_traffic), style = MaterialTheme.typography.bodyLarge)
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_cover_traffic_hint), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                    Switch(
                                        checked = uiState.wsPaddingEnabled,
                                        onCheckedChange = { viewModel.updateWsPaddingEnabled(it) }
                                    )
                                }
                            }
                        }
                    }
                }

                // DoH Server URL (shown for DOH profiles)
                EditorSection {
                    if (uiState.isDoh) {
                        DohServerSelector(
                            dohUrl = uiState.dohUrl,
                            dohUrlError = uiState.dohUrlError,
                            onUrlChange = { viewModel.updateDohUrl(it) },
                            onPresetSelected = { viewModel.selectDohPreset(it) },
                            onTestServers = { scope -> viewModel.testDohServers(scope) },
                            customDohUrls = uiState.customDohUrls,
                            onCustomDohUrlsChange = { viewModel.updateCustomDohUrls(it) }
                        )

                        // DoH warning
                        Surface(
                            shape = MaterialTheme.shapes.small,
                            color = MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f),
                            modifier = Modifier.fillMaxWidth()
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
                                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                    Text(
                                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_only_encryption),
                                        style = MaterialTheme.typography.labelMedium,
                                        fontWeight = FontWeight.SemiBold,
                                        color = MaterialTheme.colorScheme.onErrorContainer
                                    )
                                    Text(
                                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_scope_hint),
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onErrorContainer
                                    )
                                }
                            }
                        }
                    }
                }

                // NaiveProxy fields (shown for NAIVE and NAIVE_SSH)
                EditorSection {
                    if (uiState.isNaiveBased) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.naivePort,
                            onValueChange = { viewModel.updateNaivePort(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_server_port)) },
                            placeholder = { Text("443") },
                            isError = uiState.naivePortError != null,
                            supportingText = uiState.naivePortError?.let { { Text(it) } },
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.naiveUsername,
                            onValueChange = { viewModel.updateNaiveUsername(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_username)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_http_proxy_auth_username)) },
                            isError = uiState.naiveUsernameError != null,
                            supportingText = uiState.naiveUsernameError?.let { { Text(it) } },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.naivePassword,
                            onValueChange = { viewModel.updateNaivePassword(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_password)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_http_proxy_auth_password)) },
                            isError = uiState.naivePasswordError != null,
                            supportingText = uiState.naivePasswordError?.let { { Text(it) } },
                            singleLine = true,
                            visualTransformation = PasswordVisualTransformation(),
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // SSH Port (shown only for SSH-only, near domain)
                EditorSection {
                    if (uiState.isSshOnly) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.sshPort,
                            onValueChange = { viewModel.updateSshPort(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_port)) },
                            placeholder = { Text("22") },
                            isError = uiState.sshPortError != null,
                            supportingText = uiState.sshPortError?.let { { Text(it) } },
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // Soar Public Key (Ed25519, base64 from `soar-server pubkey`)
                EditorSection {
                    if (uiState.isSoarBased) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.dnsttPublicKey,
                            onValueChange = { viewModel.updateDnsttPublicKey(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_public_key)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ed25519_key_hint)) },
                            isError = uiState.dnsttPublicKeyError != null,
                            supportingText = {
                                Text(uiState.dnsttPublicKeyError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_soar_key_command_hint))
                            },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // DNSTT Public Key
                EditorSection {
                    if (uiState.isDnsttOrNoizOrVaydnsBased) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.dnsttPublicKey,
                            onValueChange = { viewModel.updateDnsttPublicKey(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_public_key)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_noise_key_placeholder)) },
                            isError = uiState.dnsttPublicKeyError != null,
                            supportingText = {
                                Text(uiState.dnsttPublicKeyError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_noise_key_hint))
                            },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // DNS Transport selector (DNSTT-based profiles only)
                EditorSection {
                    if (uiState.isDnsttOrNoizOrVaydnsBased) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_transport),
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            DnsTransport.entries.forEach { transport ->
                                if (uiState.dnsTransport == transport) {
                                    Button(
                                        onClick = { },
                                        modifier = Modifier.weight(1f)
                                    ) {
                                        Text(transport.displayName)
                                    }
                                } else {
                                    OutlinedButton(
                                        onClick = { viewModel.updateDnsTransport(transport) },
                                        modifier = Modifier.weight(1f)
                                    ) {
                                        Text(transport.displayName)
                                    }
                                }
                            }
                        }
                    }
                }

                // DoH URL for DNSTT with DoH transport
                EditorSection {
                    if (uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOH) {
                        DohServerSelector(
                            dohUrl = uiState.dohUrl,
                            dohUrlError = uiState.dohUrlError,
                            onUrlChange = { viewModel.updateDohUrl(it) },
                            onPresetSelected = { viewModel.selectDohPreset(it) },
                            onTestServers = { scope -> viewModel.testDohServers(scope) },
                            customDohUrls = uiState.customDohUrls,
                            onCustomDohUrlsChange = { viewModel.updateCustomDohUrls(it) }
                        )
                    }
                }

                // DNS Resolver
                val showResolvers = !uiState.isSshOnly && !uiState.isDoh && !uiState.isSnowflake && !uiState.isNaiveBased &&
                        !uiState.isSocks5 && !uiState.isVless && !(uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOH)
                EditorSection {
                    if (showResolvers) {
                        if (globalResolverEnabled) {
                            Card(
                                colors = CardDefaults.cardColors(
                                    containerColor = MaterialTheme.colorScheme.primaryContainer
                                ),
                                modifier = Modifier.fillMaxWidth().padding(bottom = 8.dp)
                            ) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_global_dns_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                                    modifier = Modifier.padding(12.dp)
                                )
                            }
                        }
                        if (uiState.resolversHidden) {
                            val resolverCount = uiState.defaultResolversList.size
                            Text(
                                text = if (resolverCount > 0) androidx.compose.ui.res.pluralStringResource(app.slipnet.R.plurals.editor_hidden_resolvers, resolverCount, resolverCount)
                                       else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver_default_hidden),
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_use_custom_resolver),
                                    style = MaterialTheme.typography.bodyMedium
                                )
                                Switch(
                                    checked = uiState.useCustomResolver,
                                    onCheckedChange = { viewModel.updateUseCustomResolver(it) }
                                )
                            }
                            if (uiState.useCustomResolver) {
                                val isDoT = uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOT
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.resolvers,
                                    onValueChange = { viewModel.updateResolvers(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver)) },
                                    placeholder = { Text(if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_e_g_dns_google_853) else "e.g. 8.8.8.8:53") },
                                    isError = uiState.resolversError != null,
                                    supportingText = {
                                        Text(uiState.resolversError ?: if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_853) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_port))
                                    },
                                    trailingIcon = {
                                        IconButton(
                                            onClick = { viewModel.autoDetectResolver() },
                                            enabled = !uiState.isAutoDetecting
                                        ) {
                                            if (uiState.isAutoDetecting) {
                                                CircularProgressIndicator(
                                                    modifier = Modifier.size(18.dp),
                                                    strokeWidth = 2.dp
                                                )
                                            } else {
                                                Text(
                                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_local),
                                                    style = MaterialTheme.typography.labelMedium,
                                                    color = MaterialTheme.colorScheme.primary
                                                )
                                            }
                                        }
                                    },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                if (onNavigateToScanner != null) {
                                    Button(
                                        onClick = { viewModel.saveForScanner() },
                                        modifier = Modifier.fillMaxWidth(),
                                        shape = RoundedCornerShape(10.dp)
                                    ) {
                                        Icon(Icons.Default.Search, contentDescription = null)
                                        Spacer(Modifier.width(8.dp))
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_scan_resolvers))
                                    }
                                }
                            }
                        } else {
                            val isDoT = uiState.isDnsttOrNoizOrVaydnsBased && uiState.dnsTransport == DnsTransport.DOT
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.resolvers,
                                onValueChange = { viewModel.updateResolvers(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_resolver)) },
                                placeholder = { Text(if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_e_g_dns_google_853) else "e.g. 8.8.8.8:53") },
                                isError = uiState.resolversError != null,
                                supportingText = {
                                    Text(uiState.resolversError ?: if (isDoT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_853) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ip_or_domain_host_port))
                                },
                                trailingIcon = {
                                    IconButton(
                                        onClick = { viewModel.autoDetectResolver() },
                                        enabled = !uiState.isAutoDetecting
                                    ) {
                                        if (uiState.isAutoDetecting) {
                                            CircularProgressIndicator(
                                                modifier = Modifier.size(18.dp),
                                                strokeWidth = 2.dp
                                            )
                                        } else {
                                            Text(
                                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_local),
                                                style = MaterialTheme.typography.labelMedium,
                                                color = MaterialTheme.colorScheme.primary
                                            )
                                        }
                                    }
                                },
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )

                            if (onNavigateToScanner != null) {
                                Button(
                                    onClick = { viewModel.saveForScanner() },
                                    modifier = Modifier.fillMaxWidth(),
                                    shape = RoundedCornerShape(10.dp)
                                ) {
                                    Icon(Icons.Default.Search, contentDescription = null)
                                    Spacer(Modifier.width(8.dp))
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_scan_resolvers))
                                }
                            }
                        }
                        // Multi-resolver mode (only shown when 2+ resolvers, not for slipstream which uses QUIC multipath)
                        if (uiState.resolvers.contains(",") && !uiState.isSlipstreamBased && !uiState.isSoarBased) {
                            MultiResolverSettings(
                                resolverMode = uiState.resolverMode,
                                rrSpreadCount = uiState.rrSpreadCount,
                                onResolverModeChange = { viewModel.updateResolverMode(it) },
                                onSpreadCountChange = { viewModel.updateRrSpreadCount(it) }
                            )
                        }

                    }
                }

                // Soar-specific settings
                EditorSection {
                    if (uiState.isSoarBased) {
                        SoarSettings(
                            discovery = uiState.soarDiscovery,
                            country = uiState.soarCountry,
                            discoveryList = uiState.soarDiscoveryList,
                            customList = uiState.soarDiscoveryCustom,
                            customListError = uiState.soarDiscoveryCustomError,
                            customListHidden = false,
                            discoveryError = uiState.soarDiscoveryError,
                            tcpMode = uiState.soarTcpMode,
                            onDiscoveryChange = { viewModel.updateSoarDiscovery(it) },
                            onCountryChange = { viewModel.updateSoarCountry(it) },
                            onDiscoveryListChange = { viewModel.updateSoarDiscoveryList(it) },
                            onCustomListChange = { viewModel.updateSoarDiscoveryCustom(it) },
                            onTcpModeChange = { viewModel.updateSoarTcpMode(it) }
                        )
                    }
                }

                // Authoritative Mode toggle (DNSTT/NoizDNS only — VayDNS has no authoritative mode)
                EditorSection {
                    if (uiState.isDnsttOrNoizBased) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_authoritative_mode),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_aggressive_query_rate_for_faster_speeds),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Switch(
                                checked = uiState.dnsttAuthoritative,
                                onCheckedChange = { viewModel.updateDnsttAuthoritative(it) }
                            )
                        }
                        if (uiState.dnsttAuthoritative) {
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_authoritative_warning),
                                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                                color = MaterialTheme.colorScheme.error,
                                modifier = Modifier.padding(bottom = 8.dp)
                            )
                        }
                    }
                }

                // VayDNS-specific settings
                EditorSection {
                    if (uiState.isVaydnsBased) {
                        // Response Record Type selector
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_response_record_type),
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp)
                        )
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_record_type_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(bottom = 4.dp)
                        )
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .horizontalScroll(rememberScrollState()),
                            horizontalArrangement = Arrangement.spacedBy(6.dp)
                        ) {
                            listOf("txt", "cname", "a", "aaaa", "mx", "ns", "srv", "null", "caa").forEach { type ->
                                if (uiState.vaydnsRecordType == type) {
                                    Button(onClick = { }) {
                                        Text(type.uppercase())
                                    }
                                } else {
                                    OutlinedButton(onClick = { viewModel.updateVaydnsRecordType(type) }) {
                                        Text(type.uppercase())
                                    }
                                }
                            }
                        }
                    }
                }

                // Auto-tune DNS query parameters (DNSTT/NoizDNS/VayDNS).
                // Hidden when Authoritative is on — probe would be a no-op there.
                val autoToggleVisible = uiState.isDnsttOrNoizOrVaydnsBased &&
                    !(uiState.isDnsttOrNoizBased && uiState.dnsttAuthoritative)
                EditorSection {
                    if (autoToggleVisible) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_auto_tune),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = if (uiState.isVaydnsBased)
                                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_autotune_rate_hint)
                                    else
                                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_autotune_length_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Switch(
                                checked = uiState.dnsAutoTune,
                                onCheckedChange = { viewModel.updateDnsAutoTune(it) }
                            )
                        }
                    }
                }

                // VayDNS: QNAME length slider (controls query size on the wire).
                // Hidden when Auto is on — value is probed at connect time.
                EditorSection {
                    if (uiState.isVaydnsBased && !uiState.dnsAutoTune) {
                        var showQnameDialog by remember { mutableStateOf(false) }
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable { showQnameDialog = true }
                                .padding(vertical = 12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_length),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes, (uiState.vaydnsMaxQnameLen).toString()) + when {
                                        uiState.vaydnsMaxQnameLen <= 80 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_stealthy)
                                        uiState.vaydnsMaxQnameLen <= 120 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_balanced)
                                        uiState.vaydnsMaxQnameLen <= 180 -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_fast)
                                        else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_maximum)
                                    },
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Icon(
                                Icons.Default.KeyboardArrowRight,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }

                        if (showQnameDialog) {
                            val minQname = 60
                            val maxQname = 253
                            val midQname = 101
                            // Non-linear slider: 0..1 with midQname (101) at center (0.5)
                            fun toSlider(qname: Int): Float {
                                val clamped = qname.coerceIn(minQname, maxQname)
                                return if (clamped <= midQname) {
                                    (clamped - minQname).toFloat() / (midQname - minQname) * 0.5f
                                } else {
                                    0.5f + (clamped - midQname).toFloat() / (maxQname - midQname) * 0.5f
                                }
                            }
                            fun fromSlider(value: Float): Int = if (value <= 0.5f) {
                                (minQname + (value / 0.5f) * (midQname - minQname)).roundToInt()
                            } else {
                                (midQname + ((value - 0.5f) / 0.5f) * (maxQname - midQname)).roundToInt()
                            }
                            var sliderValue by remember { mutableStateOf(toSlider(uiState.vaydnsMaxQnameLen)) }
                            val displayValue = fromSlider(sliderValue)
                            var customText by remember { mutableStateOf("") }
                            var customError by remember { mutableStateOf(false) }
                            AlertDialog(
                                onDismissRequest = { showQnameDialog = false },
                                title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_length)) },
                                text = {
                                    Column {
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_length_hint),
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            modifier = Modifier.padding(bottom = 12.dp)
                                        )
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes, (displayValue).toString()),
                                            style = MaterialTheme.typography.titleMedium,
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .padding(top = 4.dp),
                                            textAlign = TextAlign.Center
                                        )
                                        Slider(
                                            value = sliderValue,
                                            onValueChange = {
                                                sliderValue = it
                                                customText = ""
                                                customError = false
                                            },
                                            valueRange = 0f..1f,
                                            steps = 19,
                                            modifier = Modifier.fillMaxWidth()
                                        )
                                        Row(
                                            modifier = Modifier.fillMaxWidth(),
                                            horizontalArrangement = Arrangement.SpaceBetween
                                        ) {
                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_60_stealthy), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_101_default), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_253_fastest), style = MaterialTheme.typography.labelSmall, textAlign = TextAlign.Center)
                                        }
                                        Spacer(modifier = Modifier.height(12.dp))
                                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                            value = customText,
                                            onValueChange = { text ->
                                                customText = text
                                                val v = text.toIntOrNull()
                                                if (v != null && v in minQname..maxQname) {
                                                    sliderValue = toSlider(v)
                                                    customError = false
                                                } else {
                                                    customError = text.isNotEmpty()
                                                }
                                            },
                                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_value)) },
                                            placeholder = { Text("$minQname–$maxQname") },
                                            isError = customError,
                                            supportingText = if (customError) {{ Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_must_be_value_value, (minQname).toString(), (maxQname).toString())) }} else null,
                                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                            singleLine = true,
                                            modifier = Modifier.fillMaxWidth()
                                        )
                                    }
                                },
                                confirmButton = {
                                    TextButton(
                                        onClick = {
                                            val custom = customText.toIntOrNull()
                                            val finalValue = if (custom != null && custom in minQname..maxQname) custom else displayValue
                                            viewModel.updateVaydnsMaxQnameLen(finalValue)
                                            showQnameDialog = false
                                        }
                                    ) {
                                        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_apply_31e392))
                                    }
                                },
                                dismissButton = {
                                    Row {
                                        if (displayValue != midQname) {
                                            TextButton(onClick = {
                                                sliderValue = 0.5f
                                                customText = ""
                                                customError = false
                                                viewModel.updateVaydnsMaxQnameLen(midQname)
                                                showQnameDialog = false
                                            }) {
                                                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_reset_daee76))
                                            }
                                        }
                                        TextButton(onClick = { showQnameDialog = false }) {
                                            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_cancel_19766e))
                                        }
                                    }
                                }
                            )
                        }
                    }
                }

                // VayDNS: Query rate limit. Hidden when Auto is on.
                EditorSection {
                    if (uiState.isVaydnsBased && !uiState.dnsAutoTune) {
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.vaydnsRps,
                            onValueChange = { viewModel.updateVaydnsRps(it.filter { c -> c.isDigit() || c == '.' }.take(6)) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_rate_limit_q_s)) },
                            supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_query_rate_hint)) },
                            modifier = Modifier.fillMaxWidth(),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                            singleLine = true
                        )
                    }
                }

                // VayDNS: Advanced settings (hidden by default)
                EditorSection {
                    if (uiState.isVaydnsBased) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable { viewModel.toggleVaydnsAdvanced() }
                                .padding(vertical = 12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_advanced_9f088d),
                                style = MaterialTheme.typography.titleMedium
                            )
                            Icon(
                                Icons.Default.KeyboardArrowRight,
                                contentDescription = if (uiState.vaydnsAdvancedExpanded) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_collapse) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_expand),
                                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.rotate(if (uiState.vaydnsAdvancedExpanded) 90f else 0f)
                            )
                        }

                        AnimatedVisibility(visible = uiState.vaydnsAdvancedExpanded) {
                            Column(
                                modifier = Modifier.fillMaxWidth(),
                                verticalArrangement = Arrangement.spacedBy(4.dp)
                            ) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_advanced_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.padding(bottom = 4.dp)
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.vaydnsIdleTimeout,
                                    onValueChange = { viewModel.updateVaydnsIdleTimeout(it.filter { c -> c.isDigit() }.take(4)) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_idle_timeout_seconds)) },
                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_10_default)) },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_session_idle_timeout)) },
                                    modifier = Modifier.fillMaxWidth(),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.vaydnsKeepalive,
                                    onValueChange = { viewModel.updateVaydnsKeepalive(it.filter { c -> c.isDigit() }.take(4)) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_keepalive_seconds)) },
                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_2_default)) },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_keepalive_interval)) },
                                    modifier = Modifier.fillMaxWidth(),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.vaydnsUdpTimeout,
                                    onValueChange = { viewModel.updateVaydnsUdpTimeout(it.filter { c -> c.isDigit() }.take(5)) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_udp_timeout_ms)) },
                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_500_default)) },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_udp_timeout_hint)) },
                                    modifier = Modifier.fillMaxWidth(),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.vaydnsMaxNumLabels,
                                    onValueChange = { viewModel.updateVaydnsMaxNumLabels(it.filter { c -> c.isDigit() }.take(2)) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_max_labels)) },
                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_unlimited)) },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_max_labels_hint)) },
                                    modifier = Modifier.fillMaxWidth(),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.vaydnsClientIdSize,
                                    onValueChange = { viewModel.updateVaydnsClientIdSize(it.filter { c -> c.isDigit() }.take(2)) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_client_id_size_bytes)) },
                                    placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_2_default)) },
                                    supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_client_id_hint)) },
                                    modifier = Modifier.fillMaxWidth(),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true
                                )
                            }
                        }
                    }
                }

                // DNS MTU selector (DNSTT/NoizDNS only). Hidden when Auto is on.
                EditorSection {
                    if (uiState.isDnsttOrNoizBased && !uiState.dnsAutoTune) {
                        var showMtuDialog by remember { mutableStateOf(false) }
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(8.dp))
                                .clickable { showMtuDialog = true }
                                .padding(vertical = 12.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_query_size),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = if (uiState.dnsPayloadSize == 0) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_full_capacity_fastest)
                                           else androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_bytes_per_query, (uiState.dnsPayloadSize).toString()),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Icon(
                                Icons.Default.KeyboardArrowRight,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }

                        if (showMtuDialog) {
                                // DNSTT/NoizDNS: preset list
                                val mtuPresets = listOf(
                                    0 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_size_full_hint),
                                    100 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_large_good_balance),
                                    80 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_medium_less_conspicuous),
                                    60 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_small_stealthier_slower),
                                    50 to androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_size_minimum_hint)
                                )
                                val isCustom = mtuPresets.none { it.first == uiState.dnsPayloadSize }
                                var customMtuText by remember { mutableStateOf(if (isCustom) uiState.dnsPayloadSize.toString() else "") }
                                var useCustom by remember { mutableStateOf(isCustom) }
                                AlertDialog(
                                    onDismissRequest = { showMtuDialog = false },
                                    title = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_query_size)) },
                                    text = {
                                        Column {
                                            Text(
                                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_size_hint),
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                modifier = Modifier.padding(bottom = 12.dp)
                                            )
                                            mtuPresets.forEach { (size, desc) ->
                                                Row(
                                                    modifier = Modifier
                                                        .fillMaxWidth()
                                                        .clip(RoundedCornerShape(8.dp))
                                                        .clickable {
                                                            useCustom = false
                                                            viewModel.updateDnsPayloadSize(size)
                                                            showMtuDialog = false
                                                        }
                                                        .padding(vertical = 10.dp, horizontal = 8.dp),
                                                    verticalAlignment = Alignment.CenterVertically
                                                ) {
                                                    RadioButton(
                                                        selected = !useCustom && uiState.dnsPayloadSize == size,
                                                        onClick = {
                                                            useCustom = false
                                                            viewModel.updateDnsPayloadSize(size)
                                                            showMtuDialog = false
                                                        }
                                                    )
                                                    Column(modifier = Modifier.padding(start = 8.dp)) {
                                                        Text(text = if (size == 0) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_full) else "$size")
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
                                                    .padding(vertical = 10.dp, horizontal = 8.dp),
                                                verticalAlignment = Alignment.CenterVertically
                                            ) {
                                                RadioButton(
                                                    selected = useCustom,
                                                    onClick = { useCustom = true }
                                                )
                                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                                    value = customMtuText,
                                                    onValueChange = { customMtuText = it.filter { c -> c.isDigit() }.take(3) },
                                                    enabled = useCustom,
                                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_custom_081ae3)) },
                                                    placeholder = { Text("50–120") },
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
                                            TextButton(
                                                onClick = {
                                                    val value = customMtuText.toIntOrNull()
                                                    if (value != null && value in 50..120) {
                                                        viewModel.updateDnsPayloadSize(value)
                                                        showMtuDialog = false
                                                    }
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
                    }
                }

                // NoizDNS stealth mode toggle
                EditorSection {
                    if (uiState.isNoizdnsBased) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_stealth_mode),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_slower_speed_harder_to_detect),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Switch(
                                checked = uiState.noizdnsStealth,
                                onCheckedChange = { viewModel.updateNoizdnsStealth(it) }
                            )
                        }
                        if (uiState.noizdnsStealth && !uiState.dnsttAuthoritative) {
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_stealth_performance_hint),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.error,
                                modifier = Modifier.padding(bottom = 8.dp)
                            )
                        }

                    }
                }

                // Snowflake / Tor bridge config
                EditorSection {
                    if (uiState.isSnowflake) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_your_bridges),
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        // "Not sure? Ask Tor" auto-detect button
                        OutlinedButton(
                            onClick = { viewModel.askTor() },
                            enabled = !uiState.isAskingTor,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            if (uiState.isAskingTor) {
                                CircularProgressIndicator(
                                    modifier = Modifier.size(18.dp),
                                    strokeWidth = 2.dp
                                )
                                Spacer(Modifier.width(8.dp))
                                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_detecting))
                            } else {
                                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_auto_detect_best_bridge))
                            }
                        }
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_bridge_detection_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )

                        // Bridge type selector — vertical radio list
                        val bridgeOptions = listOf(
                            TorBridgeType.SNOWFLAKE_AMP to Pair("Snowflake (AMP)", androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_amp_bridge_hint)),
                            TorBridgeType.DIRECT to Pair(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_direct), androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_direct_tor_hint)),
                            TorBridgeType.OBFS4 to Pair(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_obfs4_built_in), androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_obfs4_hint)),
                            TorBridgeType.MEEK_AZURE to Pair("Meek (Azure)", androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_meek_hint)),
                            TorBridgeType.SNOWFLAKE to Pair(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_snowflake_built_in), androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_snowflake_hint)),
                            TorBridgeType.SMART to Pair(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_smart_connect), androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_smart_bridge_hint)),
                            TorBridgeType.CUSTOM to Pair(
                                androidx.compose.ui.res.stringResource(app.slipnet.R.string.connection_manual_path),
                                if (uiState.torBridgeLines.isNotBlank() && uiState.torBridgeType == TorBridgeType.CUSTOM) {
                                    val count = uiState.torBridgeLines.lines().count { it.isNotBlank() }
                                    androidx.compose.ui.res.pluralStringResource(app.slipnet.R.plurals.editor_bridges_added, count, count)
                                } else {
                                    androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_enter_your_own_bridge_lines)
                                }
                            )
                        )

                        Column(
                            modifier = Modifier.fillMaxWidth(),
                            verticalArrangement = Arrangement.spacedBy(0.dp)
                        ) {
                            bridgeOptions.forEach { (type, labels) ->
                                val (title, description) = labels
                                Surface(
                                    onClick = { viewModel.selectTorBridgeType(type) },
                                    shape = MaterialTheme.shapes.small,
                                    color = if (uiState.torBridgeType == type)
                                        MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                                    else
                                        MaterialTheme.colorScheme.surface,
                                    modifier = Modifier.fillMaxWidth()
                                ) {
                                    Row(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .padding(vertical = 12.dp, horizontal = 4.dp),
                                        verticalAlignment = Alignment.CenterVertically
                                    ) {
                                        RadioButton(
                                            selected = uiState.torBridgeType == type,
                                            onClick = { viewModel.selectTorBridgeType(type) }
                                        )
                                        Column(modifier = Modifier.weight(1f)) {
                                            Text(
                                                text = title,
                                                style = MaterialTheme.typography.bodyLarge
                                            )
                                            Text(
                                                text = description,
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant
                                            )
                                        }
                                    }
                                }
                            }
                        }

                        // Bridge lines text field (Custom mode only)
                        if (uiState.torBridgeType == TorBridgeType.CUSTOM) {
                            OutlinedTextField(
                                value = uiState.torBridgeLines,
                                onValueChange = { viewModel.updateTorBridgeLines(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_bridge_lines)) },
                                placeholder = {
                                    Text("obfs4 IP:PORT FINGERPRINT cert=... iat-mode=0")
                                },
                                isError = uiState.torBridgeLinesError != null,
                                supportingText = {
                                    Text(
                                        uiState.torBridgeLinesError
                                            ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_supported_bridges_hint)
                                    )
                                },
                                minLines = 3,
                                maxLines = 6,
                                textStyle = MaterialTheme.typography.bodySmall.copy(
                                    fontFamily = FontFamily.Monospace
                                ),
                                modifier = Modifier.fillMaxWidth()
                            )
                        }

                        // Find more bridges section
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_find_more_bridges),
                            style = MaterialTheme.typography.titleMedium,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(top = 16.dp)
                        )

                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_request_bridge_hint),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(bottom = 4.dp)
                        )

                        // Request bridges button (Moat API)
                        Surface(
                            onClick = { if (!uiState.isRequestingBridges) viewModel.requestBridges() },
                            shape = MaterialTheme.shapes.small,
                            color = MaterialTheme.colorScheme.surface,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 12.dp, horizontal = 4.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Spacer(Modifier.width(4.dp))
                                Icon(
                                    Icons.Default.SmartToy,
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.size(20.dp)
                                )
                                Spacer(Modifier.width(12.dp))
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_bridge_bot),
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant
                                    )
                                    Text(
                                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_request_bridges),
                                        style = MaterialTheme.typography.bodyLarge
                                    )
                                }
                                if (uiState.isRequestingBridges) {
                                    CircularProgressIndicator(
                                        modifier = Modifier.size(20.dp),
                                        strokeWidth = 2.dp
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                            }
                        }

                        val context = LocalContext.current
                        data class BridgeLink(val label: String, val description: String, val url: String, val icon: androidx.compose.ui.graphics.vector.ImageVector)
                        val bridgeLinks = listOf(
                            BridgeLink("Telegram", androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_message_getbridgesbot), "https://t.me/GetBridgesBot", Icons.Default.Send),
                            BridgeLink(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_web), "bridges.torproject.org", "https://bridges.torproject.org", Icons.Default.Language),
                            BridgeLink("Gmail or Riseup", "bridges@torproject.org", "mailto:bridges@torproject.org", Icons.Default.Email)
                        )
                        bridgeLinks.forEach { link ->
                            Surface(
                                onClick = {
                                    val intent = android.content.Intent(
                                        android.content.Intent.ACTION_VIEW,
                                        android.net.Uri.parse(link.url)
                                    )
                                    context.startActivity(intent)
                                },
                                shape = MaterialTheme.shapes.small,
                                color = MaterialTheme.colorScheme.surface,
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(vertical = 12.dp, horizontal = 4.dp),
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Spacer(Modifier.width(4.dp))
                                    Icon(
                                        link.icon,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Spacer(Modifier.width(12.dp))
                                    Column(modifier = Modifier.weight(1f)) {
                                        Text(
                                            text = link.label,
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant
                                        )
                                        Text(
                                            text = link.description,
                                            style = MaterialTheme.typography.bodyLarge
                                        )
                                    }
                                    Icon(
                                        Icons.Default.OpenInNew,
                                        contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_open),
                                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                                        modifier = Modifier.size(20.dp)
                                    )
                                    Spacer(Modifier.width(8.dp))
                                }
                            }
                        }
                    }
                }

                // Slipstream-specific settings
                EditorSection {
                    if (uiState.isSlipstreamBased) {
                        OutlinedTextField(value = uiState.slipstreamCertificate,
                            onValueChange = viewModel::updateSlipstreamCertificate,
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.alpha18_slipstream_certificate)) },
                            supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.alpha18_slipstream_certificate_hint)) },
                            minLines = 2, maxLines = 5, modifier = Modifier.fillMaxWidth())
                        // Keep-Alive Interval (hidden in authoritative mode — polling subsumes keep-alive)
                        if (!uiState.authoritativeMode) {
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.keepAliveInterval,
                                onValueChange = { viewModel.updateKeepAliveInterval(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_keep_alive_interval_ms)) },
                                placeholder = { Text("5000") },
                                supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_quic_keepalive_hint)) },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                        }

                        // Congestion Control
                        CongestionControlDropdown(
                            selected = uiState.congestionControl,
                            onSelect = { viewModel.updateCongestionControl(it) }
                        )

                        // Authoritative Mode
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_authoritative_mode),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_authoritative_flag_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Switch(
                                checked = uiState.authoritativeMode,
                                onCheckedChange = { viewModel.updateAuthoritativeMode(it) }
                            )
                        }
                        if (uiState.authoritativeMode) {
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_authoritative_warning),
                                style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Bold),
                                color = MaterialTheme.colorScheme.error,
                                modifier = Modifier.padding(bottom = 8.dp)
                            )
                        }

                        // GSO Mode
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_gso_label),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_gso_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                            Switch(
                                checked = uiState.gsoEnabled,
                                onCheckedChange = { viewModel.updateGsoEnabled(it) }
                            )
                        }
                    }
                }

                // Connection Method section (DNSTT & Slipstream only, not SSH-only)
                EditorSection {
                    if (uiState.showConnectionMethod) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_connection_method),
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            // NaiveProxy and Soar servers dial destinations themselves: no SOCKS server to log in to
                            val directLabel = if (uiState.isNaiveBased || uiState.isSoarBased) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_direct) else "SOCKS"
                            val sshLabel = if (uiState.isNaiveBased || uiState.isSoarBased) "+ SSH" else "SSH"

                            if (uiState.useSsh) {
                                OutlinedButton(
                                    onClick = { viewModel.setUseSsh(false) },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(directLabel)
                                }
                                Button(
                                    onClick = { },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(sshLabel)
                                }
                            } else {
                                Button(
                                    onClick = { },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(directLabel)
                                }
                                OutlinedButton(
                                    onClick = { viewModel.setUseSsh(true) },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(sshLabel)
                                }
                            }
                        }
                    }
                }

                // Soar+SSH: the Soar server dials SSH itself, and a stock soar-server refuses it
                if (uiState.isSoarBased && uiState.useSsh) {
                    Text(
                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_soar_ssh_backend_hint, (uiState.sshPort.ifBlank { "22" }).toString()),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                // SOCKS5 Credentials (optional, when SOCKS selected for DNSTT/Slipstream)
                EditorSection {
                    if (uiState.showConnectionMethod && !uiState.useSsh && !uiState.isNaiveBased && !uiState.isSoarBased) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_socks5_credentials_optional),
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.socksUsername,
                            onValueChange = { viewModel.updateSocksUsername(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_username_e3b89e)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_enter_socks_username)) },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )

                        var passwordVisible by remember { mutableStateOf(false) }
                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.socksPassword,
                            onValueChange = { viewModel.updateSocksPassword(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_enter_socks_password)) },
                            singleLine = true,
                            visualTransformation = if (passwordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                            trailingIcon = {
                                IconButton(onClick = { passwordVisible = !passwordVisible }) {
                                    Text(
                                        text = if (passwordVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                        style = MaterialTheme.typography.labelMedium,
                                        color = MaterialTheme.colorScheme.primary
                                    )
                                }
                            },
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }

                // SSH Credentials (SSH-only, or when SSH selected for DNSTT/Slipstream)
                EditorSection {
                    if (uiState.useSsh) {
                        Text(
                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_credentials),
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp)
                        )

                        // SSH Port (only for DNSTT+SSH / Slipstream+SSH / NAIVE_SSH, not SSH-only which has it near domain)
                        if (uiState.showConnectionMethod) {
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.sshPort,
                                onValueChange = { viewModel.updateSshPort(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_port)) },
                                placeholder = { Text("22") },
                                isError = uiState.sshPortError != null,
                                supportingText = uiState.sshPortError?.let { { Text(it) } },
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                singleLine = true,
                                modifier = Modifier.fillMaxWidth()
                            )
                        }

                        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                            value = uiState.sshUsername,
                            onValueChange = { viewModel.updateSshUsername(it) },
                            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_username)) },
                            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_enter_ssh_username)) },
                            isError = uiState.sshUsernameError != null,
                            supportingText = uiState.sshUsernameError?.let { { Text(it) } },
                            singleLine = true,
                            modifier = Modifier.fillMaxWidth()
                        )

                        // SSH Auth Type Toggle
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            if (uiState.sshAuthType == SshAuthType.PASSWORD) {
                                Button(
                                    onClick = { },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e))
                                }
                                OutlinedButton(
                                    onClick = { viewModel.updateSshAuthType(SshAuthType.KEY) },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_key))
                                }
                            } else {
                                OutlinedButton(
                                    onClick = { viewModel.updateSshAuthType(SshAuthType.PASSWORD) },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_password_e7cf3e))
                                }
                                Button(
                                    onClick = { },
                                    modifier = Modifier.weight(1f)
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_key))
                                }
                            }
                        }

                        if (uiState.sshAuthType == SshAuthType.PASSWORD) {
                            // Password auth
                            var sshPasswordVisible by remember { mutableStateOf(false) }
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.sshPassword,
                                onValueChange = { viewModel.updateSshPassword(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_password)) },
                                placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_enter_ssh_password)) },
                                isError = uiState.sshPasswordError != null,
                                supportingText = uiState.sshPasswordError?.let { { Text(it) } },
                                singleLine = true,
                                visualTransformation = if (sshPasswordVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                                trailingIcon = {
                                    IconButton(onClick = { sshPasswordVisible = !sshPasswordVisible }) {
                                        Text(
                                            text = if (sshPasswordVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                },
                                modifier = Modifier.fillMaxWidth()
                            )
                        } else {
                            // Key auth
                            val context = LocalContext.current
                            val keyFileLauncher = app.slipnet.presentation.common.components.rememberTextFileImport(128 * 1024) {
                                viewModel.updateSshPrivateKey(it)
                            }

                            OutlinedTextField(
                                value = uiState.sshPrivateKey,
                                onValueChange = { viewModel.updateSshPrivateKey(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_private_key)) },
                                placeholder = { Text("-----BEGIN OPENSSH PRIVATE KEY-----") },
                                isError = uiState.sshPrivateKeyError != null,
                                supportingText = uiState.sshPrivateKeyError?.let { { Text(it) } },
                                minLines = 3,
                                maxLines = 8,
                                textStyle = MaterialTheme.typography.bodySmall.copy(
                                    fontFamily = FontFamily.Monospace
                                ),
                                modifier = Modifier.fillMaxWidth()
                            )

                            OutlinedButton(
                                onClick = { keyFileLauncher.launch(arrayOf("*/*")) },
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_import_key_file))
                            }

                            var passphraseVisible by remember { mutableStateOf(false) }
                            OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                value = uiState.sshKeyPassphrase,
                                onValueChange = { viewModel.updateSshKeyPassphrase(it) },
                                label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_key_passphrase_optional)) },
                                placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_key_passphrase_hint)) },
                                singleLine = true,
                                visualTransformation = if (passphraseVisible) VisualTransformation.None else PasswordVisualTransformation(),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                                trailingIcon = {
                                    IconButton(onClick = { passphraseVisible = !passphraseVisible }) {
                                        Text(
                                            text = if (passphraseVisible) androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_hide_ac20a5) else androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_show_0df6f1),
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                },
                                modifier = Modifier.fillMaxWidth()
                            )
                        }

                        // SSH Transport settings — only for SSH-only tunnel type
                        if (uiState.isSshOnly) {
                            Spacer(modifier = Modifier.height(12.dp))
                            Text(
                                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_transport),
                                style = MaterialTheme.typography.titleSmall,
                                modifier = Modifier.padding(top = 4.dp)
                            )
                            // Transport selector (mutually exclusive)
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                SshTransport.entries.forEach { transport ->
                                    FilterChip(
                                        selected = uiState.sshTransport == transport,
                                        onClick = { viewModel.updateSshTransport(transport) },
                                        label = { Text(if (transport == SshTransport.DIRECT) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_direct) else if (transport == SshTransport.HTTP_PROXY) androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_http_proxy_label) else transport.displayName) }
                                    )
                                }
                            }

                            // === Direct mode options ===
                            if (uiState.sshTransport == SshTransport.DIRECT) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_wrap_in_tls_stunnel), style = MaterialTheme.typography.bodyMedium)
                                    Switch(
                                        checked = uiState.sshTlsEnabled,
                                        onCheckedChange = { viewModel.updateSshTlsEnabled(it) }
                                    )
                                }
                                if (uiState.sshTlsEnabled) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sshTlsSni,
                                        onValueChange = { viewModel.updateSshTlsSni(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tls_sni_optional)) },
                                        placeholder = { Text("example.com") },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth()
                                    )
                                }
                                Spacer(modifier = Modifier.height(8.dp))
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshPayload,
                                    onValueChange = { viewModel.updateSshPayload(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_payload_optional)) },
                                    placeholder = { Text("GET / HTTP/1.1[crlf]Host: [host][crlf][crlf]") },
                                    singleLine = false,
                                    minLines = 2,
                                    maxLines = 4,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_ssh_payload_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }

                            // === HTTP CONNECT Proxy options ===
                            if (uiState.sshTransport == SshTransport.HTTP_PROXY) {
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshHttpProxyHost,
                                    onValueChange = { viewModel.updateSshHttpProxyHost(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_host)) },
                                    placeholder = { Text("proxy.example.com") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshHttpProxyPort,
                                    onValueChange = { viewModel.updateSshHttpProxyPort(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_proxy_port)) },
                                    placeholder = { Text("8080") },
                                    isError = uiState.sshHttpProxyPortError != null,
                                    supportingText = uiState.sshHttpProxyPortError?.let { { Text(it) } },
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshHttpProxyCustomHost,
                                    onValueChange = { viewModel.updateSshHttpProxyCustomHost(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_host_header_optional)) },
                                    placeholder = { Text("cdn.example.com") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tls_after_connect), style = MaterialTheme.typography.bodyMedium)
                                    Switch(
                                        checked = uiState.sshTlsEnabled,
                                        onCheckedChange = { viewModel.updateSshTlsEnabled(it) }
                                    )
                                }
                                if (uiState.sshTlsEnabled) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sshTlsSni,
                                        onValueChange = { viewModel.updateSshTlsSni(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tls_sni_optional)) },
                                        placeholder = { Text("example.com") },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth()
                                    )
                                }
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_http_connect_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }

                            // === WebSocket options ===
                            if (uiState.sshTransport == SshTransport.WEBSOCKET) {
                                Row(
                                    modifier = Modifier.fillMaxWidth(),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically
                                ) {
                                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_use_tls_wss), style = MaterialTheme.typography.bodyMedium)
                                    Switch(
                                        checked = uiState.sshWsUseTls,
                                        onCheckedChange = { viewModel.updateSshWsUseTls(it) }
                                    )
                                }
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshWsPath,
                                    onValueChange = { viewModel.updateSshWsPath(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_websocket_path)) },
                                    placeholder = { Text("/") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                    value = uiState.sshWsCustomHost,
                                    onValueChange = { viewModel.updateSshWsCustomHost(it) },
                                    label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_host_header_optional)) },
                                    placeholder = { Text("cdn.example.com") },
                                    singleLine = true,
                                    modifier = Modifier.fillMaxWidth()
                                )
                                if (uiState.sshWsUseTls) {
                                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                                        value = uiState.sshTlsSni,
                                        onValueChange = { viewModel.updateSshTlsSni(it) },
                                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_tls_sni_optional)) },
                                        placeholder = { Text("example.com") },
                                        singleLine = true,
                                        modifier = Modifier.fillMaxWidth()
                                    )
                                }
                                Text(
                                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_websocket_hint),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }
                        }
                    }
                }

                // Server setup guide
                Spacer(modifier = Modifier.height(8.dp))
                HorizontalDivider()
                Spacer(modifier = Modifier.height(12.dp))

                EditorSection {
                    Surface(
                        onClick = {
                            val intent = android.content.Intent(
                                android.content.Intent.ACTION_VIEW,
                                android.net.Uri.parse("https://github.com/msVPNco/rovunet-panel")
                            )
                            context.startActivity(intent)
                        },
                        shape = RoundedCornerShape(12.dp),
                        color = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 14.dp, horizontal = 16.dp),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(
                                Icons.Default.OpenInNew,
                                contentDescription = null,
                                modifier = Modifier.size(20.dp),
                                tint = MaterialTheme.colorScheme.primary
                            )
                            Spacer(Modifier.width(12.dp))
                            Text(
                                androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_server_setup_guide),
                                style = MaterialTheme.typography.titleSmall,
                                color = MaterialTheme.colorScheme.primary
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(32.dp))
            }
        }
    }

    // DoH test results dialog
    if (uiState.showDohTestDialog) {
        DohTestDialog(
            isTestingDoh = uiState.isTestingDoh,
            results = uiState.dohTestResults,
            onDismiss = { viewModel.dismissDohTestDialog() },
            onSelectResult = { viewModel.selectDohTestResult(it) }
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun CongestionControlDropdown(
    selected: CongestionControl,
    onSelect: (CongestionControl) -> Unit,
    modifier: Modifier = Modifier
) {
    var expanded by remember { mutableStateOf(false) }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it },
        modifier = modifier.fillMaxWidth()
    ) {
        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
            value = selected.value.uppercase(),
            onValueChange = { },
            readOnly = true,
            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_congestion_control)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor()
        )

        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            CongestionControl.entries.forEach { option ->
                DropdownMenuItem(
                    text = { Text(option.value.uppercase()) },
                    onClick = {
                        onSelect(option)
                        expanded = false
                    }
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DohServerSelector(
    dohUrl: String,
    dohUrlError: String?,
    onUrlChange: (String) -> Unit,
    onPresetSelected: (DohServer) -> Unit,
    onTestServers: (DohTestScope) -> Unit,
    customDohUrls: String = "",
    onCustomDohUrlsChange: (String) -> Unit = {}
) {
    var expanded by remember { mutableStateOf(false) }
    val matchingPreset = DOH_SERVERS.find { it.url == dohUrl }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it },
        modifier = Modifier.fillMaxWidth()
    ) {
        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
            value = matchingPreset?.name ?: if (dohUrl.isNotBlank()) androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_custom_081ae3) else "",
            onValueChange = { },
            readOnly = true,
            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_server)) },
            placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_select_a_server)) },
            isError = dohUrlError != null,
            supportingText = {
                Text(dohUrlError ?: (matchingPreset?.url ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_selection_hint)))
            },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
            modifier = Modifier
                .fillMaxWidth()
                .menuAnchor()
        )

        ExposedDropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false }
        ) {
            DOH_SERVERS.forEach { preset ->
                DropdownMenuItem(
                    text = {
                        Column {
                            Text(preset.name, style = MaterialTheme.typography.bodyLarge)
                            Text(
                                preset.url,
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    },
                    onClick = {
                        onPresetSelected(preset)
                        expanded = false
                    }
                )
            }
            DropdownMenuItem(
                text = {
                    Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_url), style = MaterialTheme.typography.bodyLarge)
                },
                onClick = {
                    onUrlChange("")
                    expanded = false
                }
            )
        }
    }

    // Show custom URL field when no preset matches (and not empty)
    if (matchingPreset == null) {
        OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
            value = dohUrl,
            onValueChange = onUrlChange,
            label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_doh_url)) },
            placeholder = { Text("https://example.com/dns-query") },
            isError = dohUrlError != null,
            supportingText = if (dohUrl.isNotBlank()) {
                { Text(dohUrlError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_endpoint_hint)) }
            } else null,
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
    }

    // Multi-line custom URLs for batch testing
    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
        value = customDohUrls,
        onValueChange = onCustomDohUrlsChange,
        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_doh_urls_to_test)) },
        placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_url_examples)) },
        supportingText = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_list_hint)) },
        singleLine = false,
        minLines = 2,
        maxLines = 5,
        modifier = Modifier.fillMaxWidth()
    )

    // Import + Test buttons
    val context = LocalContext.current
    val importLauncher = app.slipnet.presentation.common.components.rememberTextFileImport { content ->
        val urls = content.lineSequence().map(String::trim)
            .filter { it.startsWith("https://", ignoreCase = true) }.toList()
        if (urls.isNotEmpty()) {
            val merged = (customDohUrls.trim().lineSequence() + urls.asSequence()).filter(String::isNotBlank).distinct().joinToString("\n")
            require(merged.length <= 2 * 1024 * 1024 && merged.lines().size <= 1000) { "Input too large" }
            onCustomDohUrlsChange(merged)
        }
    }

    OutlinedButton(
        onClick = { importLauncher.launch(arrayOf("text/*")) },
        modifier = Modifier.fillMaxWidth()
    ) {
        Icon(Icons.Default.FileUpload, contentDescription = null, modifier = Modifier.size(18.dp))
        Spacer(Modifier.width(8.dp))
        Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_import_list))
    }

    val presetUrls = remember { DOH_SERVERS.map { it.url }.toSet() }
    val hasCustom = customDohUrls.lines().any { it.trim().startsWith("https://") }
            || (dohUrl.startsWith("https://") && dohUrl !in presetUrls)
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        OutlinedButton(
            onClick = { onTestServers(DohTestScope.PRESETS) },
            modifier = Modifier.weight(1f)
        ) {
            Icon(Icons.Default.Speed, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(6.dp))
            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_presets))
        }

        OutlinedButton(
            onClick = { onTestServers(DohTestScope.CUSTOM) },
            enabled = hasCustom,
            modifier = Modifier.weight(1f)
        ) {
            Icon(Icons.Default.Speed, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(6.dp))
            Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_custom_081ae3))
        }
    }
}

@Composable
private fun DohTestDialog(
    isTestingDoh: Boolean,
    results: List<DohTestResult>,
    onDismiss: () -> Unit,
    onSelectResult: (DohTestResult) -> Unit
) {
    val total = results.size
    val completed = results.count { it.latencyMs != null || it.error != null }
    val reachable = results.count { it.isSuccess }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_doh_server_test))
                if (isTestingDoh) {
                    Spacer(Modifier.width(12.dp))
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                }
            }
        },
        text = {
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                Text(
                    text = if (isTestingDoh) {
                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_testing_value_value_servers, (completed).toString(), (total).toString())
                    } else {
                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_value_value_reachable_tap_to_select, (reachable).toString(), (total).toString())
                    },
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Spacer(Modifier.height(8.dp))
                LazyColumn(
                    modifier = Modifier.height(400.dp),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    items(results, key = { it.url }) { result ->
                        Surface(
                            onClick = { if (result.isSuccess) onSelectResult(result) },
                            enabled = result.isSuccess,
                            shape = MaterialTheme.shapes.small,
                            tonalElevation = if (result.isSuccess) 2.dp else 0.dp,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(horizontal = 12.dp, vertical = 8.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Column(modifier = Modifier.weight(1f)) {
                                    Text(
                                        text = result.name,
                                        style = MaterialTheme.typography.bodyMedium
                                    )
                                    Text(
                                        text = if (result.error != null) result.error else result.url,
                                        style = MaterialTheme.typography.bodySmall,
                                        color = if (result.error != null)
                                            MaterialTheme.colorScheme.error
                                        else
                                            MaterialTheme.colorScheme.onSurfaceVariant,
                                        maxLines = 1
                                    )
                                }
                                Spacer(Modifier.width(8.dp))
                                when {
                                    result.latencyMs == null && result.error == null -> {
                                        CircularProgressIndicator(
                                            modifier = Modifier.size(16.dp),
                                            strokeWidth = 2.dp
                                        )
                                    }
                                    result.isSuccess -> {
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_valuems, (result.latencyMs).toString()),
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.primary
                                        )
                                    }
                                    result.error != null -> {
                                        Text(
                                            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_failed),
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.error
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_close_7d9eb7))
            }
        }
    )
}

@Composable
private fun LockedInfoRow(
    icon: ImageVector,
    label: String,
    value: String,
    valueColor: Color = MaterialTheme.colorScheme.onSurface
) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(18.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.weight(1f))
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            color = valueColor
        )
    }
}

@Composable
private fun MultiResolverSettings(
    resolverMode: ResolverMode,
    rrSpreadCount: Int,
    onResolverModeChange: (ResolverMode) -> Unit,
    onSpreadCountChange: (Int) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_resolver_mode),
                style = MaterialTheme.typography.bodyLarge
            )
            Text(
                text = if (resolverMode == ResolverMode.FANOUT)
                    androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_sends_to_all_resolvers)
                else
                    androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_distributes_load_across_resolvers),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Row {
            ResolverMode.entries.forEach { mode ->
                FilterChip(
                    selected = resolverMode == mode,
                    onClick = { onResolverModeChange(mode) },
                    label = { Text(androidx.compose.ui.res.stringResource(if (mode == ResolverMode.FANOUT) app.slipnet.R.string.editor_reliable_mode else app.slipnet.R.string.editor_fast_mode)) },
                    modifier = Modifier.padding(start = 4.dp)
                )
            }
        }
    }
    if (resolverMode == ResolverMode.ROUND_ROBIN) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(vertical = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_spread_count),
                    style = MaterialTheme.typography.bodyLarge
                )
                Text(
                    text = if (rrSpreadCount == 1)
                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_round_robin_hint)
                    else
                        androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_spread_hint, (rrSpreadCount).toString()),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                IconButton(
                    onClick = { onSpreadCountChange(rrSpreadCount - 1) },
                    enabled = rrSpreadCount > 1
                ) {
                    Icon(Icons.Default.Remove, contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_decrease_67e19d))
                }
                Text(
                    text = "$rrSpreadCount",
                    style = MaterialTheme.typography.bodyLarge,
                    modifier = Modifier.padding(horizontal = 4.dp)
                )
                IconButton(
                    onClick = { onSpreadCountChange(rrSpreadCount + 1) },
                    enabled = rrSpreadCount < 5
                ) {
                    Icon(Icons.Default.Add, contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_increase_5a7fec))
                }
            }
        }
    }
}

/**
 * Soar settings: resolver discovery (from Soar's bundled lists for a country,
 * the app's resolver list, or a custom list) and TCP/53 paths.
 */
@Composable
private fun SoarSettings(
    discovery: Boolean,
    country: String,
    discoveryList: String,
    customList: String,
    customListError: String?,
    customListHidden: Boolean,
    discoveryError: String?,
    tcpMode: String,
    onDiscoveryChange: (Boolean) -> Unit,
    onCountryChange: (String) -> Unit,
    onDiscoveryListChange: (String) -> Unit,
    onCustomListChange: (String) -> Unit,
    onTcpModeChange: (String) -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_resolver_discovery),
                style = MaterialTheme.typography.bodyLarge
            )
            Text(
                text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_discovery_verification_hint),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Switch(
            checked = discovery,
            onCheckedChange = onDiscoveryChange
        )
    }
    if (discoveryError != null) {
        Text(
            text = discoveryError,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.error
        )
    }
    if (discovery) {
        Text(
            text = when (discoveryList) {
                SoarConfig.LIST_APP -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_discovery_app_hint)
                SoarConfig.LIST_CUSTOM -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_discovery_custom_hint)
                else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_discovery_builtin_hint)
            },
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .horizontalScroll(rememberScrollState())
        ) {
            SoarConfig.DISCOVERY_LISTS.forEach { list ->
                FilterChip(
                    selected = discoveryList == list,
                    onClick = { onDiscoveryListChange(list) },
                    label = {
                        Text(
                            when (list) {
                                SoarConfig.LIST_APP -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_app_list)
                                SoarConfig.LIST_CUSTOM -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.settings_custom_081ae3)
                                else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_built_in)
                            }
                        )
                    },
                    modifier = Modifier.padding(end = 4.dp)
                )
            }
        }
        when (discoveryList) {
            SoarConfig.LIST_SOAR -> {
                Text(
                    text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_discovery_country_hint),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState())
                ) {
                    SoarConfig.DISCOVERY_COUNTRIES.forEach { cc ->
                        FilterChip(
                            selected = country == cc,
                            onClick = { onCountryChange(cc) },
                            label = { Text(cc.ifEmpty { androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_global) }) },
                            modifier = Modifier.padding(end = 4.dp)
                        )
                    }
                }
            }
            SoarConfig.LIST_CUSTOM -> {
                if (customListHidden) {
                    Text(
                        text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_profile_discovery_list_hint),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                } else {
                    OutlinedTextField(textStyle = androidx.compose.material3.LocalTextStyle.current.copy(textDirection = androidx.compose.ui.text.style.TextDirection.Ltr),
                        value = customList,
                        onValueChange = onCustomListChange,
                        label = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_resolvers)) },
                        placeholder = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_custom_resolver_examples)) },
                        isError = customListError != null,
                        supportingText = { Text(customListError ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_one_per_line_ip_or_ip_port)) },
                        minLines = 3,
                        maxLines = 8,
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            }
        }
    }

    Column(modifier = Modifier.padding(top = 8.dp)) {
        Text(
            text = androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_over_tcp),
            style = MaterialTheme.typography.bodyLarge
        )
        Text(
            text = when (tcpMode) {
                SoarConfig.TCP_ON -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_tcp_parallel_hint)
                SoarConfig.TCP_ONLY -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_tcp_only_hint)
                SoarConfig.TCP_OFF -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_udp_53_only)
                else -> androidx.compose.ui.res.stringResource(app.slipnet.R.string.editor_dns_tcp_fallback_hint)
            },
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
    SoarTcpModeSelector(tcpMode, onTcpModeChange)
}

/** Composition boundary only: uses the parent ColumnScope and adds no layout or transport work. */
@Composable
private fun androidx.compose.foundation.layout.ColumnScope.EditorSection(
    content: @Composable androidx.compose.foundation.layout.ColumnScope.() -> Unit
) { content(this) }
