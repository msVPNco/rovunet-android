package app.slipnet.presentation

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.slipnet.R

/** No navigation, import, network work or permission dialog is created before a choice. */
@Composable
internal fun LanguageWelcome(onChoose: (String) -> Unit) {
    Column(Modifier.fillMaxSize().padding(28.dp).testTag("language-welcome"),
        verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) {
        Text(stringResource(R.string.app_name), style = MaterialTheme.typography.headlineLarge)
        Spacer(Modifier.height(20.dp))
        Text(stringResource(R.string.alpha17_language_title), style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(24.dp))
        for ((code, label) in listOf("fa" to "فارسی", "en" to "English", "ru" to "Русский", "zh" to "简体中文")) {
            OutlinedButton(onClick = { onChoose(code) }, modifier = Modifier.fillMaxWidth().testTag("language-$code")) { Text(label) }
            Spacer(Modifier.height(8.dp))
        }
    }
}
