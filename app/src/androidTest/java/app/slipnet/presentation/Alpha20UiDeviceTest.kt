package app.slipnet.presentation

import android.content.res.Configuration
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.domain.model.PingResult
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.common.components.LegalAttribution
import app.slipnet.presentation.common.components.ProfileListItem
import app.slipnet.presentation.theme.SlipstreamTheme
import java.util.Locale
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class Alpha20UiDeviceTest {
    @get:Rule val compose = createComposeRule()

    @Test fun failedTextIsRedAndLegalFooterStaysEnglishInFourLanguagesAndThreeThemes() {
        val base = InstrumentationRegistry.getInstrumentation().targetContext
        var language by mutableStateOf("en")
        var theme by mutableStateOf(DarkMode.LIGHT)
        var errorColor = Color.Unspecified
        val profile = ServerProfile(id = 9901, name = "Synthetic", domain = "example.invalid", tunnelType = TunnelType.SSH)
        compose.setContent {
            val locale = Locale.forLanguageTag(language)
            val config = Configuration(base.resources.configuration).apply { setLocale(locale); setLayoutDirection(locale) }
            val resources = base.createConfigurationContext(config).resources
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config,
                LocalLayoutDirection provides if (language == "fa") LayoutDirection.Rtl else LayoutDirection.Ltr) {
                SlipstreamTheme(darkMode = theme) {
                    val currentError = MaterialTheme.colorScheme.error
                    SideEffect { errorColor = currentError }
                    Column {
                        ProfileListItem(profile, isSelected = false, isConnected = true,
                            pingResult = PingResult.Error("Synthetic failure"), onClick = {}, onConnectClick = {},
                            onEditClick = {}, onDeleteClick = {}, onExportClick = {}, onShareQrClick = {}, onPinClick = {}, onPingClick = {})
                        LegalAttribution()
                    }
                }
            }
        }
        for (lang in listOf("en", "fa", "zh", "ru")) for (mode in listOf(DarkMode.LIGHT, DarkMode.DARK, DarkMode.AMOLED)) {
            compose.runOnIdle { language = lang; theme = mode }
            compose.waitForIdle()
            val results = mutableListOf<TextLayoutResult>()
            compose.onNodeWithTag("profile-test-result-9901", useUnmergedTree = true)
                .performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(results) }
            assertEquals(errorColor, results.single().layoutInput.style.color)
            assertEquals(1, results.single().lineCount)
            val footer = mutableListOf<TextLayoutResult>()
            compose.onNodeWithTag("legal-attribution", useUnmergedTree = true)
                .performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(footer) }
            val layout = footer.single()
            assertTrue(layout.layoutInput.text.text.contains("SlipNet 2.5.6 by Anonvector"))
            assertEquals(TextDirection.Ltr, layout.layoutInput.style.textDirection)
            assertEquals(TextAlign.Center, layout.layoutInput.style.textAlign)
            assertFalse(layout.didOverflowHeight)
        }
    }
}
