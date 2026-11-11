package app.slipnet.presentation.profiles

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.domain.model.SoarConfig

/** Distinct labels for the actual wire values; two columns also fit large fonts and RTL. */
@Composable
internal fun SoarTcpModeSelector(selected: String, onSelect: (String) -> Unit) {
    Column(Modifier.fillMaxWidth().testTag("soar-tcp-modes"), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        SoarConfig.TCP_MODES.chunked(2).forEach { pair ->
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                pair.forEach { mode ->
                    FilterChip(selected = selected == mode, onClick = { onSelect(mode) },
                        modifier = Modifier.weight(1f).heightIn(min = 48.dp).testTag("soar-tcp-$mode"),
                        leadingIcon = if (selected == mode) { { Icon(Icons.Default.Check, null, Modifier.size(18.dp)) } } else null,
                        label = { Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                            Text(stringResource(when (mode) {
                                SoarConfig.TCP_ON -> R.string.editor_soar_tcp_parallel
                                SoarConfig.TCP_ONLY -> R.string.editor_soar_tcp_only
                                SoarConfig.TCP_OFF -> R.string.editor_soar_udp_only
                                else -> R.string.editor_tcp_auto
                            }))
                        } })
                }
            }
        }
    }
}
