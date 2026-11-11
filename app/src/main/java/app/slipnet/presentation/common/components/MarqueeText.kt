package app.slipnet.presentation.common.components

import androidx.compose.foundation.basicMarquee
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.boundsInWindow
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner

/** Scroll only overflowing text; lazy disposal, window bounds and lifecycle bound its cost. */
@Composable
fun MarqueeText(text: String, modifier: Modifier = Modifier, color: Color = Color.Unspecified,
                style: TextStyle = LocalTextStyle.current, fontWeight: FontWeight? = null) {
    val lifecycle = LocalLifecycleOwner.current.lifecycle
    val view = LocalView.current
    var resumed by remember(lifecycle) { mutableStateOf(lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) }
    var visible by remember { mutableStateOf(false) }
    DisposableEffect(lifecycle) {
        val observer = LifecycleEventObserver { _, _ -> resumed = lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED) }
        lifecycle.addObserver(observer)
        onDispose { lifecycle.removeObserver(observer) }
    }
    val bounds = modifier.clipToBounds().onGloballyPositioned {
        val rect = it.boundsInWindow()
        visible = rect.width > 0 && rect.height > 0 && rect.bottom > 0 && rect.right > 0 &&
            rect.top < view.height && rect.left < view.width
    }
    Text(text, modifier = if (resumed && visible) bounds.basicMarquee(
        iterations = Int.MAX_VALUE, initialDelayMillis = 1500, repeatDelayMillis = 1500, velocity = 24.dp
    ) else bounds, color = color, style = style, fontWeight = fontWeight,
        maxLines = 1, softWrap = false, overflow = TextOverflow.Clip)
}
