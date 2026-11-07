package app.slipnet.presentation.main

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R
import app.slipnet.domain.model.DataPathState

/**
 * Home-screen success only. The probe result owns its deadline in the ViewModel, so opening this
 * composable again cannot renew it. Other states and retry belong in connection details.
 */
@Composable
internal fun DataPathCheckCard(state: DataPathState) {
    if (state == DataPathState.VERIFIED) {
        Text(
            stringResource(R.string.data_path_toast),
            style = MaterialTheme.typography.bodyMedium,
            color = app.slipnet.presentation.theme.ConnectionSuccessColor,
            modifier = Modifier.fillMaxWidth().padding(horizontal = 4.dp)
        )
    }
}

@Composable
internal fun DataPathCheckDetails(state: DataPathState) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(stringResource(when (state) {
            DataPathState.UNTESTED -> R.string.data_path_untested
            DataPathState.CHECKING -> R.string.data_path_checking
            DataPathState.VERIFIED -> R.string.data_path_verified
            DataPathState.UNKNOWN -> R.string.data_path_unknown
        }))
        Text(stringResource(R.string.data_path_hint), style = MaterialTheme.typography.bodySmall)
    }
}
