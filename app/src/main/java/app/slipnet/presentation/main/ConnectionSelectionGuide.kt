package app.slipnet.presentation.main

import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import app.slipnet.R

@Composable
internal fun ConnectionSelectionGuide() {
    Text(stringResource(R.string.connection_selection_guide) + "\n\n" + stringResource(R.string.probe_204_requirement),
        modifier = Modifier.verticalScroll(rememberScrollState()))
}
