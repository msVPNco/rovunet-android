package app.slipnet.presentation.main

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.fadeOut
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.slipnet.R
import app.slipnet.presentation.theme.softPopBackground

/** No timer or minimum display duration: content is available as soon as local profiles are ready. */
@Composable
internal fun StartupPresentation(loading: Boolean, content: @Composable () -> Unit) {
    Box(Modifier.fillMaxSize()) {
        if (!loading) content()
        AnimatedVisibility(visible = loading, enter = EnterTransition.None, exit = fadeOut(tween(140))) {
            StartupCover()
        }
    }
}

@Composable
internal fun StartupCover() {
    Box(Modifier.fillMaxSize().background(softPopBackground).testTag("startup-cover")
        .pointerInput(Unit) {
            awaitPointerEventScope {
                while (true) awaitPointerEvent().changes.forEach { it.consume() }
            }
        }, contentAlignment = Alignment.Center) {
        Column(Modifier.safeDrawingPadding().verticalScroll(rememberScrollState()).padding(horizontal = 28.dp, vertical = 32.dp),
            horizontalAlignment = Alignment.CenterHorizontally) {
            Surface(shape = RoundedCornerShape(34.dp), color = MaterialTheme.colorScheme.surface,
                shadowElevation = 12.dp, tonalElevation = 2.dp) {
                StartupSymbol(Modifier.padding(12.dp).size(104.dp))
            }
            Spacer(Modifier.height(28.dp))
            Text(stringResource(R.string.app_name), style = MaterialTheme.typography.headlineLarge.copy(
                fontSize = 32.sp, fontWeight = FontWeight.SemiBold, letterSpacing = 1.sp,
                textDirection = TextDirection.Ltr), color = MaterialTheme.colorScheme.onBackground,
                maxLines = 1, modifier = Modifier.testTag("startup-brand"))
            Spacer(Modifier.height(40.dp))
            CircularProgressIndicator(Modifier.size(22.dp), strokeWidth = 2.dp,
                color = MaterialTheme.colorScheme.primary, trackColor = MaterialTheme.colorScheme.primary.copy(alpha = .12f))
            Spacer(Modifier.height(16.dp))
            Text(stringResource(R.string.profiles_preparing), style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite }.testTag("startup-message"))
        }
    }
}

/** Same geometry/colors as ic_rovunet, drawn together to avoid vector layer redraw gaps on theme changes. */
@Composable
private fun StartupSymbol(modifier: Modifier) {
    val letter = remember {
        Path().apply {
            moveTo(28f,24f);lineTo(57f,24f);cubicTo(74f,24f,84f,33f,84f,47f)
            cubicTo(84f,58f,78f,66f,68f,69f);lineTo(85f,86f);lineTo(64f,86f)
            lineTo(45f,65f);lineTo(44f,65f);lineTo(44f,86f);lineTo(28f,86f);close()
            moveTo(44f,39f);lineTo(44f,52f);lineTo(57f,52f)
            cubicTo(63f,52f,67f,50f,67f,46f);cubicTo(67f,41f,63f,39f,57f,39f);close()
        }
    }
    Canvas(modifier.clip(RoundedCornerShape(25.dp)).testTag("startup-symbol")) {
        drawRect(Color(0xFFF5F8FA))
        scale(size.width / 108f, size.height / 108f, pivot = Offset.Zero) {
            drawPath(letter,Color(0xFF087F8C))
            drawRect(Color(0xFF172C42),topLeft=Offset(24f,89f),size=Size(60f,5f))
        }
    }
}
