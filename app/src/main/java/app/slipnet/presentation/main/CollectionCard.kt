package app.slipnet.presentation.main

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.ui.Alignment
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import app.slipnet.domain.model.*
import app.slipnet.presentation.theme.rv

@Composable
fun CollectionCard(profiles: List<ServerProfile>, results: Map<Long, PingResult>, selected: Boolean,
    expanded: Boolean, connected: Boolean, busy: Boolean, onSelect: () -> Unit, onDetails: () -> Unit, onRename: () -> Unit, onMove: () -> Unit, onTest: () -> Unit, onExport: () -> Unit, onDelete: () -> Unit, canDelete: Boolean) {
    val best = profiles.mapNotNull { results[it.id] as? PingResult.Success }.filter { it.verified }.minByOrNull { it.latencyMs }
    Card(onClick = onSelect, enabled = !busy, shape = RoundedCornerShape(24.dp),
        border = BorderStroke(if (selected) 2.dp else 1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant),
        colors = CardDefaults.cardColors(containerColor = if (selected) MaterialTheme.colorScheme.primaryContainer.copy(alpha = .35f) else MaterialTheme.colorScheme.surface)) {
        var menuOpen by remember { mutableStateOf(false) }
        Column(Modifier.fillMaxWidth().padding(horizontal = 14.dp, vertical = 6.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(ProfileCollections.name(profiles.first()), Modifier.weight(1f), maxLines = 1,
                    overflow = TextOverflow.Ellipsis, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                if (connected) Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_connected_229655), style = MaterialTheme.typography.labelSmall)
                Box {
                    IconButton(onClick = { menuOpen = true }) { Icon(Icons.Default.MoreVert, contentDescription = androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_collection_actions_06a9e8)) }
                    DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_test_this_collection_fa3ff3)) }, enabled = !busy,
                            onClick = { menuOpen = false; onTest() })
                        DropdownMenuItem(text = { Text(rv(if (expanded) "Hide paths" else "Show paths")) },
                            onClick = { menuOpen = false; onDetails() })
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_export_share_collection_82ddc2)) }, enabled = !busy,
                            onClick = { menuOpen = false; onExport() })
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_rename_3064d7)) }, enabled = !busy,
                            onClick = { menuOpen = false; onRename() })
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.delete_collection), color = MaterialTheme.colorScheme.error) },
                            enabled = canDelete && !busy,
                            onClick = { menuOpen = false; onDelete() })
                        DropdownMenuItem(text = { Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_move_to_collection_b392f3)) }, enabled = !busy && !connected,
                            onClick = { menuOpen = false; onMove() })
                    }
                }
            }
            Text(androidx.compose.ui.platform.LocalContext.current.resources.getQuantityString(app.slipnet.R.plurals.connection_paths, profiles.size, profiles.size) + " · " +
                (best?.let { "HTTPS ${it.latencyMs} ms · ${it.successes}/${it.attempts}" } ?: androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_not_tested_on_this_network_38ef7b)),
                modifier = Modifier.padding(bottom = 8.dp), style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
