package app.slipnet.presentation.settings

import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import app.slipnet.R

@Composable
internal fun AppBypassHelpDialog(onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.review02_app_bypass_title)) },
        text = { Text(stringResource(R.string.review02_app_bypass_message)) },
        confirmButton = { TextButton(onClick = onConfirm, modifier = Modifier.testTag("configure-app-bypass")) {
            Text(stringResource(R.string.review02_app_bypass_choose))
        } },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(android.R.string.cancel)) } })
}
