package app.slipnet.presentation.theme

import android.app.Activity
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat
import app.slipnet.data.local.datastore.DarkMode

// Brand roles are explicit in both themes; cards must remain distinct from the background.
private val DarkColorScheme = darkColorScheme(
    primary = Color(0xFFC4B5FD), onPrimary = Color(0xFF2E1868),
    primaryContainer = Color(0xFF30264C), onPrimaryContainer = Color(0xFFF1EBFF),
    secondary = Color(0xFF72DCE6), onSecondary = Color(0xFF00363B),
    secondaryContainer = Color(0xFF16434D), onSecondaryContainer = Color(0xFFC2F5FA),
    tertiary = Color(0xFF9DC4FF), onTertiary = Color(0xFF002F60),
    background = Color(0xFF0B1020), onBackground = Color(0xFFF1F3FC),
    surface = Color(0xFF11182B), onSurface = Color(0xFFF1F3FC),
    surfaceContainerLowest = Color(0xFF0B1020), surfaceContainerLow = Color(0xFF172137),
    surfaceContainer = Color(0xFF1D2941), surfaceContainerHigh = Color(0xFF26334D),
    surfaceContainerHighest = Color(0xFF303E58), surfaceVariant = Color(0xFF303E58),
    onSurfaceVariant = Color(0xFFC1CADD), outline = Color(0xFF8190AD), outlineVariant = Color(0xFF45536D),
    error = Color(0xFFFFB4B0), onError = Color(0xFF690005),
    errorContainer = Color(0xFF71262D), onErrorContainer = Color(0xFFFFDAD6)
)

private val AmoledColorScheme = DarkColorScheme.copy(
    background = Color.Black, surface = Color(0xFF070B14), surfaceContainerLowest = Color.Black,
    surfaceContainerLow = Color(0xFF111827), surfaceContainer = Color(0xFF192235)
)

private val LightColorScheme = lightColorScheme(
    primary = Color(0xFF5C3DB8), onPrimary = Color.White,
    primaryContainer = Color(0xFFE9E0FF), onPrimaryContainer = Color(0xFF25104F),
    secondary = Color(0xFF006A75), onSecondary = Color.White,
    secondaryContainer = Color(0xFFBEEFF4), onSecondaryContainer = Color(0xFF002F35),
    tertiary = Color(0xFF315C96), onTertiary = Color.White,
    background = Color(0xFFF3F5FC), onBackground = Color(0xFF19233A),
    surface = Color(0xFFF9FAFF), onSurface = Color(0xFF19233A),
    surfaceContainerLowest = Color.White, surfaceContainerLow = Color(0xFFFAFBFF),
    surfaceContainer = Color.White, surfaceContainerHigh = Color(0xFFE8ECF7),
    surfaceContainerHighest = Color(0xFFDFE5F1), surfaceVariant = Color(0xFFE4E9F4),
    onSurfaceVariant = Color(0xFF46526A), outline = Color(0xFF6D7990), outlineVariant = Color(0xFFBCC6D9),
    error = Color(0xFFB3261E), onError = Color.White,
    errorContainer = Color(0xFFFFDAD6), onErrorContainer = Color(0xFF410002)
)

@Composable
fun SlipstreamTheme(
    darkMode: DarkMode = DarkMode.SYSTEM,
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val darkTheme = when (darkMode) {
        DarkMode.LIGHT -> false
        DarkMode.DARK -> true
        DarkMode.AMOLED -> true
        DarkMode.SYSTEM -> isSystemInDarkTheme()
    }

    val colorScheme = when {
        darkMode == DarkMode.AMOLED -> AmoledColorScheme
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }

    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as Activity).window
            // Control status bar and navigation bar icon colors based on theme
            val insetsController = WindowCompat.getInsetsController(window, view)
            insetsController.isAppearanceLightStatusBars = !darkTheme
            insetsController.isAppearanceLightNavigationBars = !darkTheme
        }
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}
