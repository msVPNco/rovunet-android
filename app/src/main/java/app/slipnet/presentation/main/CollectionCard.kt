package app.slipnet.presentation.main

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.PowerSettingsNew
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Speed
import androidx.compose.ui.Alignment
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.background
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.zIndex
import app.slipnet.presentation.theme.collectionTones
import app.slipnet.presentation.theme.SoftPopIconButton
import app.slipnet.presentation.common.components.MarqueeText
import app.slipnet.R
import app.slipnet.domain.model.*

/** Browsing a collection never starts, selects or stops a connection. */
@Composable
fun CollectionCard(profiles: List<ServerProfile>, results: Map<Long, PingResult>, selected: Boolean,
    expanded: Boolean, connectedProfileId: Long?, manualProfileId: Long?, connecting: Boolean,
    busy: Boolean, canConnect: Boolean, onConnect: () -> Unit, onDetails: () -> Unit,
    onRename: () -> Unit, onMove: () -> Unit, onTest: () -> Unit, onStabilityTest: () -> Unit,
    onExport: () -> Unit, onDelete: () -> Unit, canDelete: Boolean,
    onShareQr: (() -> Unit)? = null, onUpdate: (() -> Unit)? = null,
    accountStatus: app.slipnet.data.subscription.AccountStatusView? = null, updating: Boolean = false,
    onAutomatic: (() -> Unit)? = null) {
    if (profiles.isEmpty()) return
    val connected = connectedProfileId != null
    val path = profiles.firstOrNull { it.id == (connectedProfileId ?: manualProfileId) }
    val completed = profiles.mapNotNull { results[it.id] }
    val verified = completed.count { it is PingResult.Success && it.verified && ProbeCache.fresh(it) }
    val reachable = completed.count { it is PingResult.Success && it.quick && !it.verified && !it.handshake && ProbeCache.fresh(it) }
    val handshakes = completed.count { it is PingResult.Success && it.handshake && ProbeCache.fresh(it) }
    val failed = completed.count { it is PingResult.Error && ProbeCache.fresh(it) }
    var menuOpen by remember { mutableStateOf(false) }
    val colors = MaterialTheme.colorScheme
    val tones = collectionTones
    val raised = expanded || selected
    val surface = if (raised) tones.raisedHeader else tones.header
    Card(onClick = onDetails, modifier = Modifier.fillMaxWidth().testTag("collection-${ProfileCollections.key(profiles.first())}"), shape = RoundedCornerShape(24.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = if (expanded) 10.dp else 2.dp),
        border = BorderStroke(if (selected || connected) 2.dp else 1.dp,
            if (connected) app.slipnet.presentation.theme.ConnectionSuccessColor else if (raised) colors.primary else colors.primary.copy(alpha = .25f)),
        colors = CardDefaults.cardColors(containerColor = surface, contentColor = tones.headerText)) {
        Column(Modifier.fillMaxWidth().background(Brush.verticalGradient(listOf(
            Color.White.copy(alpha = if (raised) .12f else .06f), Color.Transparent)))
            .padding(start = 14.dp, end = 4.dp, top = 5.dp, bottom = 7.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    MarqueeText(ProfileCollections.name(profiles.first()), modifier = Modifier.fillMaxWidth(),
                        style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    val count = androidx.compose.ui.platform.LocalResources.current.getQuantityString(R.plurals.connection_paths, profiles.size, profiles.size)
                    MarqueeText(if (path != null) "${path.name} · $count" else count, modifier = Modifier.fillMaxWidth(), style = MaterialTheme.typography.bodySmall,
                        color = tones.headerText.copy(alpha = .8f))
                }
                SoftPopIconButton(onClick = onDetails) {
                    Icon(if (expanded) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                        stringResource(if (expanded) R.string.ui_hide_paths else R.string.ui_show_paths))
                }
                SoftPopIconButton(onClick = onConnect, modifier = Modifier.testTag("collection-connect-${ProfileCollections.key(profiles.first())}"), enabled = !busy && canConnect) {
                    Icon(if (connecting) Icons.Default.Close else Icons.Default.PowerSettingsNew,
                        if (connecting) stringResource(R.string.ui_cancel_connection)
                        else if (connected) stringResource(R.string.ui_disconnect)
                        else if (path != null) stringResource(R.string.ui_connect_named, path.name)
                        else stringResource(R.string.ui_choose_path),
                        tint = if (connected) app.slipnet.presentation.theme.ConnectionSuccessColor else colors.primary)
                }
                Box {
                    SoftPopIconButton(onClick = { menuOpen = true }) { Icon(Icons.Default.MoreVert, stringResource(R.string.rv_collection_actions_06a9e8)) }
                    DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                        app.slipnet.presentation.common.components.ActionMenuSectionLabel(R.string.menu_connection_tests)
                        DropdownMenuItem(text = { Text(stringResource(if (connecting) R.string.ui_cancel_connection else if (connected) R.string.ui_disconnect else R.string.rv_connect_1a2303)) },
                            enabled = !busy && canConnect, modifier = Modifier.testTag("collection-menu-connect"),
                            onClick = { menuOpen = false; onConnect() })
                        DropdownMenuItem(text = { Text(stringResource(if (expanded) R.string.ui_hide_paths else R.string.ui_show_paths)) },
                            modifier = Modifier.testTag("collection-menu-paths"),
                            onClick = { menuOpen = false; onDetails() })
                        DropdownMenuItem(text = { Text(stringResource(R.string.probe_quick_test)) }, enabled = !busy,
                            modifier = Modifier.testTag("collection-menu-quick-test"),
                            onClick = { menuOpen = false; onTest() })
                        DropdownMenuItem(text = { Text(stringResource(R.string.probe_stability_test)) }, enabled = !busy,
                            modifier = Modifier.testTag("collection-menu-detailed-test"),
                            onClick = { menuOpen = false; onStabilityTest() })
                        onAutomatic?.let { action -> DropdownMenuItem(text = { Text(stringResource(R.string.alpha17_smart_title)) },
                            onClick = { menuOpen = false; action() }) }
                        HorizontalDivider()
                        app.slipnet.presentation.common.components.ActionMenuSectionLabel(R.string.menu_manage)
                        onUpdate?.let { action -> DropdownMenuItem(text = { Text(stringResource(R.string.subscription_update_action)) }, enabled = !busy && !updating, onClick = { menuOpen = false; action() }) }
                        DropdownMenuItem(text = { Text(stringResource(R.string.rv_rename_3064d7)) }, enabled = !busy,
                            onClick = { menuOpen = false; onRename() })
                        DropdownMenuItem(text = { Text(stringResource(R.string.rv_move_to_collection_b392f3)) }, enabled = !busy && !connected,
                            onClick = { menuOpen = false; onMove() })
                        HorizontalDivider()
                        app.slipnet.presentation.common.components.ActionMenuSectionLabel(R.string.menu_share)
                        DropdownMenuItem(text = { Text(stringResource(R.string.rv_export_share_collection_82ddc2)) }, enabled = !busy,
                            onClick = { menuOpen = false; onExport() })
                        onShareQr?.let { action -> DropdownMenuItem(text = { Text(stringResource(R.string.collection_share_qr)) }, enabled = !busy, onClick = { menuOpen = false; action() }) }
                        HorizontalDivider()
                        DropdownMenuItem(text = { Text(stringResource(R.string.delete_collection), color = colors.error) }, enabled = canDelete && !busy,
                            onClick = { menuOpen = false; onDelete() })
                    }
                }
            }
            if (updating) { Text(stringResource(R.string.subscription_updating_inline), style = MaterialTheme.typography.labelSmall); LinearProgressIndicator(Modifier.fillMaxWidth()) }
            accountStatus?.let { AccountStatusSummary(it) }
            TextButton(onClick = onTest, enabled = !busy, modifier = Modifier.testTag("collection-quick-test-${ProfileCollections.key(profiles.first())}"),
                colors = ButtonDefaults.textButtonColors(contentColor = tones.headerText)) {
                Icon(Icons.Default.Speed, null, Modifier.size(18.dp))
                Spacer(Modifier.width(6.dp))
                Text(stringResource(R.string.probe_quick_test))
            }
            if (profiles.size > 50) MarqueeText(stringResource(R.string.alpha17_large_batch), style = MaterialTheme.typography.labelSmall, color = tones.headerText.copy(alpha = .8f))
            if (profiles.any { it.needsCredentials }) MarqueeText(stringResource(R.string.collection_needs_credentials),
                color = colors.error, style = MaterialTheme.typography.bodySmall)
            if (completed.isNotEmpty()) MarqueeText(stringResource(R.string.probe_collection_results, verified, failed, profiles.size - verified - failed),
                style = MaterialTheme.typography.labelSmall, color = tones.headerText.copy(alpha = .8f))
            if (reachable > 0) MarqueeText(stringResource(R.string.probe_tcp_results, reachable),
                style = MaterialTheme.typography.labelSmall, color = app.slipnet.presentation.theme.ConnectionSuccessColor)
            if (handshakes > 0) MarqueeText(stringResource(R.string.probe_handshake_results, handshakes),
                style = MaterialTheme.typography.labelSmall, color = app.slipnet.presentation.theme.ConnectionSuccessColor)
        }
    }
}

/** The header casts its shadow over a quieter, inset surface containing its paths. */
@Composable
fun CollectionStack(expanded: Boolean, header: @Composable () -> Unit, paths: @Composable ColumnScope.() -> Unit) {
    Column(Modifier.fillMaxWidth()) {
        Box(Modifier.zIndex(1f)) { header() }
        if (expanded) Column(
            Modifier.fillMaxWidth().padding(horizontal = 10.dp).offset(y = (-6).dp)
                .background(collectionTones.tray, RoundedCornerShape(bottomStart = 20.dp, bottomEnd = 20.dp))
                .padding(start = 10.dp, end = 10.dp, top = 20.dp, bottom = 12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp), content = paths
        )
    }
}
