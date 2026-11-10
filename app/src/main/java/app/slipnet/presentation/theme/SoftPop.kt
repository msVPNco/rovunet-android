package app.slipnet.presentation.theme

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.unit.dp

/** Static gradients and press-only spring motion; no perpetual decorative animation. */
val softPopBackground: Brush
    @Composable get() = Brush.verticalGradient(if (MaterialTheme.colorScheme.background.luminance() > .5f)
        listOf(Color(0xFFE9E6F6), Color(0xFFF5F4FC), Color.White)
    else listOf(Color(0xFF303C5C), Color(0xFF1C2940), MaterialTheme.colorScheme.background))

@Composable
fun SoftPopGlass(modifier: Modifier = Modifier, header: Boolean = false, content: @Composable () -> Unit) {
    val colors = MaterialTheme.colorScheme
    Surface(modifier = modifier, shape = RoundedCornerShape(24.dp),
        color = colors.primaryContainer.copy(alpha = if (header) .20f else .86f),
        border = BorderStroke(1.dp, colors.primary.copy(alpha = .26f)), content = content)
}

@Composable
fun SoftPopIconButton(onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true,
    content: @Composable () -> Unit) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    val scale by animateFloatAsState(if (pressed) .94f else 1f, spring(dampingRatio = .7f), label = "softPopPress")
    val colors = MaterialTheme.colorScheme
    FilledIconButton(onClick, enabled = enabled, interactionSource = interaction,
        modifier = modifier.sizeIn(minWidth = 48.dp, minHeight = 48.dp).padding(3.dp)
            .graphicsLayer { scaleX = scale; scaleY = scale; shadowElevation = if (pressed) 1.dp.toPx() else 4.dp.toPx(); shape = CircleShape; clip = true }
            .background(Brush.verticalGradient(listOf(colors.primary.copy(alpha = .24f), colors.primaryContainer)), CircleShape),
        colors = IconButtonDefaults.filledIconButtonColors(containerColor = Color.Transparent,
            contentColor = colors.primary, disabledContainerColor = Color.Transparent), content = content)
}

@Composable
fun SoftPopFab(onClick: () -> Unit, containerColor: Color, modifier: Modifier = Modifier, content: @Composable () -> Unit) {
    val interaction = remember { MutableInteractionSource() }
    val pressed by interaction.collectIsPressedAsState()
    val scale by animateFloatAsState(if (pressed) .94f else 1f, spring(dampingRatio = .7f), label = "softPopFabPress")
    val shape = RoundedCornerShape(20.dp)
    FloatingActionButton(onClick = onClick, interactionSource = interaction, shape = shape,
        containerColor = containerColor, modifier = modifier.graphicsLayer { scaleX = scale; scaleY = scale },
        content = { Box(Modifier.clip(shape).background(Brush.verticalGradient(listOf(Color.White.copy(alpha = .22f), Color.Transparent))).padding(16.dp)) { content() } })
}
