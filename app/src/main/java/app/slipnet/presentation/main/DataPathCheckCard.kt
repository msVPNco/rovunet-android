package app.slipnet.presentation.main

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.domain.model.DataPathState

@Composable
internal fun DataPathCheckCard(state: DataPathState, retry: () -> Unit) {
    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(12.dp)) {
        Text(stringResource(when (state) {
            DataPathState.UNTESTED -> R.string.data_path_untested
            DataPathState.CHECKING -> R.string.data_path_checking
            DataPathState.VERIFIED -> R.string.data_path_verified
            DataPathState.UNKNOWN -> R.string.data_path_unknown
        }), color = if (state == DataPathState.VERIFIED) app.slipnet.presentation.theme.ConnectionSuccessColor else MaterialTheme.colorScheme.onSurface)
        Text(stringResource(R.string.data_path_hint), style = MaterialTheme.typography.bodySmall)
        TextButton(enabled = state != DataPathState.CHECKING, onClick = retry) { Text(stringResource(R.string.data_path_retry)) }
    } }
}
