package app.slipnet.presentation.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance

/** Separate surfaces make a collection's hierarchy readable in both appearances. */
data class CollectionTones(
    val header: Color, val raisedHeader: Color, val headerText: Color,
    val tray: Color, val child: Color, val selectedChild: Color, val childOutline: Color
)

val collectionTones: CollectionTones
    @Composable get() {
        val colors = MaterialTheme.colorScheme
        return when {
            colors.background == Color.Black -> CollectionTones(
                Color(0xFF191E29), Color(0xFF272F40), Color(0xFFEBEDF4),
                Color.Transparent, Color(0xFF161C25), Color(0xFF343C50), Color(0xFF333C49))
            colors.background.luminance() > 0.5f -> CollectionTones(
                Color(0xFFEAECF4), Color(0xFFDDE3EF), Color(0xFF1F2C42),
                Color.Transparent, Color.White, Color(0xFFE4EAF4), Color(0xFFD8DFEA))
            else -> CollectionTones(
                Color(0xFF30364A), Color(0xFF3B435A), Color(0xFFF1F2F8),
                Color.Transparent, Color(0xFF2C3D54), Color(0xFF4B536D), Color(0xFF53627B))
        }
    }
