package app.slipnet.presentation.main

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.fadeOut
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Image
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
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
                Image(painterResource(R.drawable.ic_rovunet), contentDescription = null,
                    modifier = Modifier.padding(12.dp).size(104.dp).clip(RoundedCornerShape(25.dp)))
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
