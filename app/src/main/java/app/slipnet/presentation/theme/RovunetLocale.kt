package app.slipnet.presentation.theme

import android.content.Context
import android.content.SharedPreferences
import android.content.res.Configuration
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.unit.LayoutDirection
import java.util.Locale

/** Change resources and layout in place; preserve Activity, navigation and VPN state. */
@Composable
fun RovunetLocale(content: @Composable () -> Unit) {
    val context = LocalContext.current
    val deviceConfiguration = LocalConfiguration.current
    val preferences = remember(context) { context.getSharedPreferences("rovunet_ui", Context.MODE_PRIVATE) }
    var language by remember(preferences) { mutableStateOf(preferences.getString("language", "system")) }
    DisposableEffect(preferences) {
        val listener = SharedPreferences.OnSharedPreferenceChangeListener { prefs, key ->
            if (key == "language") language = prefs.getString(key, "system")
        }
        preferences.registerOnSharedPreferenceChangeListener(listener)
        onDispose { preferences.unregisterOnSharedPreferenceChangeListener(listener) }
    }
    val localized = remember(context, language, deviceConfiguration) {
        val locale = when (language) {
            "en", "fa", "ru" -> Locale.forLanguageTag(language!!)
            "zh" -> Locale.forLanguageTag("zh-Hans")
            else -> context.applicationContext.resources.configuration.locales[0]
        }
        context.createConfigurationContext(Configuration(deviceConfiguration).apply {
            setLocale(locale)
            setLayoutDirection(locale)
        }).resources
    }
    CompositionLocalProvider(
        LocalResources provides localized,
        LocalConfiguration provides localized.configuration,
        LocalLayoutDirection provides if (localized.configuration.layoutDirection == 1) LayoutDirection.Rtl else LayoutDirection.Ltr,
        content = content
    )
}
