package app.slipnet.presentation.common.components

import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import app.slipnet.R

private const val COMMUNITY_URL = "https://t.me/RovunetVPN"

/** Opens the user-provided community URL via Telegram or an installed browser. */
@Composable
fun TelegramCommunityLink() {
    val context = LocalContext.current
    val unavailable = stringResource(R.string.community_open_failed)
    TextButton(onClick = {
        try {
            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(COMMUNITY_URL)))
        } catch (_: ActivityNotFoundException) {
            Toast.makeText(context, unavailable, Toast.LENGTH_LONG).show()
        }
    }) {
        Column {
            Text(stringResource(R.string.community_telegram))
            Text("@RovunetVPN")
        }
    }
}
