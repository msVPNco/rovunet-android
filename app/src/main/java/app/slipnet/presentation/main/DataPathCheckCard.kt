package app.slipnet.presentation.main

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.domain.model.DataPathState

/**
 * A short note for this session's background HTTPS check. A pass shows a small message the host
 * hides after a few seconds; only a check that did not pass stays, as one line with a retry.
 * The longer explanation lives in the connection details, not on the home screen.
 */
@Composable
internal fun DataPathCheckCard(state: DataPathState, retry: () -> Unit) {
    if (state == DataPathState.VERIFIED) {
        Text(
            stringResource(R.string.data_path_toast),
            style = MaterialTheme.typography.bodyMedium,
            color = app.slipnet.presentation.theme.ConnectionSuccessColor,
            modifier = Modifier.fillMaxWidth().padding(horizontal = 4.dp)
        )
    } else if (state == DataPathState.UNKNOWN) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(
                stringResource(R.string.data_path_unknown_short),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.weight(1f).padding(horizontal = 4.dp)
            )
            TextButton(onClick = retry) { Text(stringResource(R.string.data_path_retry)) }
        }
    }
}
