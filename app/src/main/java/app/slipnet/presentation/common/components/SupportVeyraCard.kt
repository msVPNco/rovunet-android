package app.slipnet.presentation.common.components

import app.slipnet.presentation.theme.rv
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalClipboardManager
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp

private const val SUPPORT_ADDRESS = "TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ"

@Composable
fun SupportRovunetCard() {
    val clipboard = LocalClipboardManager.current
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Support Rovunet development", style = MaterialTheme.typography.titleMedium)
            Text("Optional donations support the development of Rovunet.", style = MaterialTheme.typography.bodySmall)
            Text("USDT • Tron (TRC20)", style = MaterialTheme.typography.labelLarge)
            CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Ltr) {
                SelectionContainer {
                    Text(SUPPORT_ADDRESS, fontFamily = FontFamily.Monospace, style = MaterialTheme.typography.bodySmall)
                }
            }
            Text("Send only USDT on Tron (TRC20) to this address.", style = MaterialTheme.typography.bodySmall)
            TextButton(onClick = { clipboard.setText(AnnotatedString(SUPPORT_ADDRESS)) }) {
                Text(androidx.compose.ui.res.stringResource(app.slipnet.R.string.rv_copy_address_7c4e52))
            }
        }
    }
}
