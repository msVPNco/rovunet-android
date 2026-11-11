package app.slipnet.presentation.profiles

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.unit.dp
import app.slipnet.R

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun ProfileCredentialRecovery(
    state: EditProfileUiState, back: () -> Unit, retry: () -> Unit,
    restore: (String) -> Unit, snackbar: SnackbarHostState
) {
    // A pasted config contains credentials. Keep it only in the current composition.
    var link by remember(state.profileId) { mutableStateOf("") }
    var confirm by remember { mutableStateOf(false) }
    Scaffold(topBar = {
        TopAppBar(title = { Text(state.name) }, navigationIcon = {
            IconButton(onClick = back) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.recovery_back))
            }
        })
    }, snackbarHost = { SnackbarHost(snackbar) }) { padding ->
        Column(Modifier.padding(padding).verticalScroll(rememberScrollState()).imePadding().padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text(stringResource(R.string.profile_credentials_unavailable), style = MaterialTheme.typography.titleMedium)
            Text(stringResource(if (state.isLocked) R.string.profile_restore_locked else R.string.profile_restore_help))
            TextButton(onClick = retry, enabled = !state.isSaving) { Text(stringResource(R.string.profile_restore_retry)) }
            if (!state.isLocked) {
                OutlinedTextField(value = link, onValueChange = { link = it }, modifier = Modifier.fillMaxWidth(),
                    label = { Text(stringResource(R.string.profile_restore_link)) }, singleLine = true,
                    textStyle = LocalTextStyle.current.copy(textDirection = TextDirection.Ltr),
                    visualTransformation = PasswordVisualTransformation(), keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password))
                Button(onClick = { confirm = true }, enabled = !state.isSaving && link.isNotBlank()) {
                    Text(stringResource(R.string.profile_restore_action))
                }
            }
        }
    }
    if (confirm) AlertDialog(
        onDismissRequest = { confirm = false },
        text = { Text(stringResource(R.string.profile_restore_confirm)) },
        confirmButton = { TextButton(onClick = { confirm = false; restore(link) }) { Text(stringResource(R.string.profile_restore_action)) } },
        dismissButton = { TextButton(onClick = { confirm = false }) { Text(stringResource(android.R.string.cancel)) } }
    )
}
