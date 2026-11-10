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
    @Composable get() = if (MaterialTheme.colorScheme.background.luminance() > 0.5f) {
        CollectionTones(Color(0xFFE4D8FC), Color(0xFFD2BAF5), Color(0xFF271544),
            Color(0xFFE8EAF4), Color(0xFFFCFBFF), Color(0xFFF0E9FC), Color(0xFFC4C6D8))
    } else {
        CollectionTones(Color(0xFF30274D), Color(0xFF40335F), Color(0xFFF7F1FF),
            Color(0xFF0D1425), Color(0xFF182238), Color(0xFF292440), Color(0xFF3B4860))
    }
