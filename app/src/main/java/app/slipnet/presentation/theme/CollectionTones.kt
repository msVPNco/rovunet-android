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
                Color(0xFF302344), Color(0xFF4B3962), Color(0xFFFFF1FF),
                Color.Transparent, Color(0xFF111F35), Color(0xFF20375A), Color(0xFF435A79))
            colors.background.luminance() > 0.5f -> CollectionTones(
                Color(0xFFE3CEFF), Color(0xFFD7BAFA), Color(0xFF302044),
                Color.Transparent, Color(0xFFE3EEFF), Color(0xFFCADFFF), Color(0xFFA3B9DE))
            else -> CollectionTones(
                Color(0xFF49395F), Color(0xFF5C4878), Color(0xFFFFF1FF),
                Color.Transparent, Color(0xFF223E61), Color(0xFF315580), Color(0xFF597BA8))
        }
    }
