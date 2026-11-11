package app.slipnet.presentation.common.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.domain.model.*
import app.slipnet.presentation.theme.ConnectionSuccessColor as ConnectedGreen
import app.slipnet.presentation.theme.collectionTones
import androidx.compose.ui.text.style.TextDirection

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun ProfileListItem(
    profile: ServerProfile, isSelected: Boolean, isConnected: Boolean,
    isConnecting: Boolean = false, connectionBusy: Boolean = false,
    pingResult: PingResult? = null, onClick: () -> Unit, onConnectClick: () -> Unit,
    onEditClick: () -> Unit, onDeleteClick: () -> Unit, onExportClick: () -> Unit,
    onShareQrClick: () -> Unit, onPinClick: () -> Unit, onPingClick: () -> Unit,
    onStabilityTestClick: () -> Unit = onPingClick, isPingRunning: Boolean = false,
    measurementNowMillis: Long = System.currentTimeMillis(),
    modifier: Modifier = Modifier
) {
    var menu by remember { mutableStateOf(false) }
    var details by rememberSaveable(profile.id) { mutableStateOf(false) }
    val colors = MaterialTheme.colorScheme
    val tones = collectionTones
    val canStart = !isPingRunning && !connectionBusy && !profile.needsCredentials && !profile.isExpired && profile.tunnelType.isAvailable()
    val canEdit = !isConnected && !isConnecting && !isPingRunning
    val border = when { isConnected -> ConnectedGreen; isSelected -> colors.primary; else -> colors.outlineVariant }
    Card(onClick = {
        if (!isPingRunning && !connectionBusy) {
            if (isSelected) { if (!isConnected && canStart) onConnectClick() } else onClick()
        }
    }, modifier = modifier.fillMaxWidth().testTag("profile-${profile.id}"), shape = RoundedCornerShape(14.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
        border = BorderStroke(if (isConnected || isSelected) 1.5.dp else 1.dp, if (isConnected || isSelected) border else tones.childOutline),
        colors = CardDefaults.cardColors(containerColor = if (isSelected || isConnected) tones.selectedChild else tones.child)) {
        Column(Modifier.fillMaxWidth().padding(start = 12.dp, end = 4.dp, top = 9.dp, bottom = 5.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                    MarqueeText(profile.name, modifier = Modifier.weight(1f),
                        style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                    if (profile.isPinned) Icon(Icons.Default.PushPin, stringResource(R.string.ui_pinned), Modifier.size(14.dp), tint = colors.primary)
                    if (profile.isLocked) Icon(Icons.Default.Lock, stringResource(R.string.ui_locked), Modifier.size(14.dp))
                    if (pingResult is PingResult.Success && pingResult.verified) {
                        val fresh = ProbeCache.fresh(pingResult, measurementNowMillis) && (!isConnected || measurementNowMillis - pingResult.measuredAt <= 65_000)
                        val green = if (fresh) ConnectedGreen else colors.onSurfaceVariant
                        Text(stringResource(R.string.alpha17_ping, pingResult.latencyMs), Modifier.testTag("profile-latency-${profile.id}")
                            .background(green.copy(alpha = .12f), RoundedCornerShape(8.dp)).padding(horizontal = 7.dp, vertical = 3.dp),
                            color = green, style = MaterialTheme.typography.labelSmall.copy(textDirection = TextDirection.Ltr),
                            fontWeight = FontWeight.SemiBold)
                        val time = java.text.DateFormat.getTimeInstance(java.text.DateFormat.MEDIUM,
                            androidx.compose.ui.platform.LocalResources.current.configuration.locales[0]).format(java.util.Date(pingResult.measuredAt))
                        MarqueeText(if (isConnected && fresh) stringResource(R.string.live_probe_age, ((measurementNowMillis - pingResult.measuredAt).coerceAtLeast(0) / 1000).toInt())
                            else stringResource(if (fresh) R.string.review02_ping_fresh else R.string.review02_ping_previous, time),
                            Modifier.widthIn(max = 140.dp).testTag("profile-measured-${profile.id}"), style = MaterialTheme.typography.labelSmall, color = green)
                    }
                }
                val status = when {
                    profile.needsCredentials -> stringResource(R.string.profile_credentials_unavailable)
                    profile.isExpired -> stringResource(R.string.ui_expired)
                    !profile.tunnelType.isAvailable() -> stringResource(R.string.ui_unavailable_edition)
                    isConnected -> stringResource(R.string.rv_connected_229655)
                    isConnecting -> stringResource(R.string.ui_connecting)
                    isSelected -> stringResource(R.string.rv_selected_57fd7a)
                    else -> ""
                }
                val test = when (pingResult) {
                    is PingResult.Success -> if (pingResult.verified) "" else stringResource(R.string.ui_not_verified)
                    is PingResult.Error -> stringResource(R.string.ui_last_test_failed)
                    is PingResult.Pending, is PingResult.Testing -> stringResource(R.string.ui_testing)
                    PingResult.ReviewIdentity -> stringResource(R.string.probe_review_identity)
                    else -> ""
                }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    val destination = if (profile.isLocked) stringResource(R.string.ui_locked) else when (profile.tunnelType) {
                        TunnelType.SSH -> "${profile.domain}:${profile.sshPort}"
                        TunnelType.SOCKS5 -> "${profile.domain}:${profile.socks5ServerPort}"
                        TunnelType.NAIVE, TunnelType.NAIVE_SSH -> "${profile.domain}:${profile.naivePort}"
                        else -> profile.domain
                    }
                    MarqueeText(destination, modifier = Modifier.fillMaxWidth(),
                        style = MaterialTheme.typography.bodySmall.copy(textDirection = TextDirection.Ltr), color = colors.onSurfaceVariant)
                    val protocolLabel = if (profile.tunnelType == TunnelType.XRAY) when (profile.coreLink.substringBefore("://").lowercase()) {
                        "anytls" -> "AnyTLS"
                        "tuic" -> "TUIC"
                        "mierus" -> "Mieru"
                        "ss" -> stringResource(R.string.review02_shadowsocks)
                        "trojan" -> stringResource(R.string.review03_trojan)
                        "vmess" -> stringResource(R.string.review03_vmess)
                        else -> profile.tunnelType.displayName
                    } else profile.tunnelType.displayName
                    MarqueeText(listOf(protocolLabel, status, test).filter { it.isNotEmpty() }.joinToString(" · "), modifier = Modifier.fillMaxWidth(), style = MaterialTheme.typography.bodySmall,
                        color = if (isConnected) ConnectedGreen else colors.onSurfaceVariant)
                }
                IconButton(onClick = onEditClick, enabled = canEdit, modifier = Modifier.testTag("profile-edit-${profile.id}")) {
                    Icon(Icons.Default.Edit, stringResource(R.string.ui_edit), Modifier.size(20.dp))
                }
            Box {
                IconButton(onClick = { menu = true }) { Icon(Icons.Default.MoreVert, stringResource(R.string.ui_more_actions)) }
                DropdownMenu(menu, onDismissRequest = { menu = false }) {
                    ActionMenuSectionLabel(R.string.menu_connection_tests)
                    DropdownMenuItem(text = { Text(stringResource(if (isConnecting) R.string.ui_cancel_connection else if (isConnected) R.string.ui_disconnect else R.string.rv_connect_1a2303)) },
                        modifier = Modifier.testTag("profile-connect-${profile.id}"),
                        enabled = !isPingRunning && (isConnecting || isConnected || canStart),
                        onClick = { menu = false; onConnectClick() })
                    DropdownMenuItem(text = { Text(stringResource(if (details) R.string.ui_hide_test_details else R.string.ui_test_details)) }, enabled = pingResult != null,
                        onClick = { menu = false; details = !details })
                    DropdownMenuItem(text = { Text(stringResource(R.string.probe_quick_test)) }, enabled = !isPingRunning,
                        onClick = { menu = false; onPingClick() })
                    DropdownMenuItem(text = { Text(stringResource(R.string.probe_stability_test)) }, enabled = !isPingRunning,
                        onClick = { menu = false; onStabilityTestClick() })
                    HorizontalDivider()
                    ActionMenuSectionLabel(R.string.menu_manage)
                    DropdownMenuItem(text = { Text(stringResource(R.string.ui_edit)) }, enabled = canEdit, onClick = { menu = false; onEditClick() })
                    DropdownMenuItem(text = { Text(stringResource(if (profile.isPinned) R.string.ui_unpin else R.string.ui_pin)) },
                        onClick = { menu = false; onPinClick() })
                    if (!profile.isLocked || profile.allowSharing) {
                        HorizontalDivider()
                        ActionMenuSectionLabel(R.string.menu_share)
                        DropdownMenuItem(text = { Text(stringResource(R.string.rv_export_366489)) }, onClick = { menu = false; onExportClick() })
                        DropdownMenuItem(text = { Text(stringResource(R.string.ui_share_qr)) }, onClick = { menu = false; onShareQrClick() })
                    }
                    HorizontalDivider()
                    DropdownMenuItem(text = { Text(stringResource(R.string.ui_delete), color = colors.error) },
                        enabled = !isConnected && !isConnecting && !isPingRunning,
                        onClick = { menu = false; onDeleteClick() })
                }
            }
                IconButton(onClick = onDeleteClick, enabled = canEdit, modifier = Modifier.testTag("profile-delete-${profile.id}")) {
                    Icon(Icons.Default.Delete, stringResource(R.string.ui_delete), Modifier.size(20.dp))
                }
            }
        }
        if (details) {
            HorizontalDivider(Modifier.padding(horizontal = 12.dp), color = colors.outlineVariant)
            val text = when (pingResult) {
                is PingResult.Success -> if (pingResult.quick) stringResource(R.string.probe_quick_details, pingResult.setupMs,
                    java.text.DateFormat.getDateTimeInstance().format(java.util.Date(pingResult.measuredAt)))
                else stringResource(R.string.probe_stability_details, pingResult.successes, pingResult.attempts, pingResult.jitterMs, pingResult.setupMs,
                    java.text.DateFormat.getDateTimeInstance().format(java.util.Date(pingResult.measuredAt)))
                is PingResult.Error -> stringResource(R.string.last_test_error,
                    java.text.DateFormat.getTimeInstance().format(java.util.Date(pingResult.measuredAt)), pingResult.message)
                else -> stringResource(R.string.ui_not_verified)
            }
            if (pingResult is PingResult.Success) Text(stringResource(R.string.alpha17_ping_hint), Modifier.padding(12.dp), style = MaterialTheme.typography.bodySmall)
            Text(text, Modifier.padding(12.dp), style = MaterialTheme.typography.bodySmall, color = colors.onSurfaceVariant)
            if ((pingResult is PingResult.Success && !pingResult.currentNetwork) || (pingResult is PingResult.Error && !pingResult.currentNetwork)) {
                Text(stringResource(R.string.probe_previous_network), Modifier.padding(start = 12.dp, end = 12.dp, bottom = 8.dp),
                    style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
            }
        }
    }
}
