package app.slipnet.presentation.scanner

import android.content.res.Configuration
import android.graphics.Bitmap
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.domain.model.ResolverStatus
import app.slipnet.presentation.theme.SlipstreamTheme
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.io.File
import java.util.Locale

class ScannerLocalizationDeviceTest {
    @get:Rule val compose = createComposeRule()
    private val context get() = InstrumentationRegistry.getInstrumentation().targetContext
    private fun configuration(language: String) = Configuration(context.resources.configuration).apply {
        setLocale(Locale.forLanguageTag(language)); setLayoutDirection(Locale.forLanguageTag(language))
    }

    @Test fun persianScannerUsesTranslatedLabelsAcrossAllModes() {
        val config = configuration("fa")
        val resources = context.createConfigurationContext(config).resources
        var mode by mutableStateOf(ScanMode.SIMPLE)
        var starts = 0
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config,
                LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme(darkMode = DarkMode.DARK) { Surface {
                    Column(Modifier.width(360.dp).verticalScroll(rememberScrollState()).padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        HeroCard()
                        ScanModeToggle(mode, true, true, true) { mode = it }
                        ActionSection(true, false, 0, mode, { starts++ }, {})
                        ConfigurationSection("example.invalid", scanPort = "53", timeoutMs = "3000", concurrency = "50",
                            scanMode = mode, scanTransport = ScanTransportMode.TCP,
                            onTestDomainChange = {}, onScanPortChange = {}, onTimeoutChange = {}, onConcurrencyChange = {})
                    }
                } }
            }
        }
        for (id in listOf(R.string.scanner_hero_title, R.string.scanner_configuration,
                R.string.scanner_test_domain, R.string.scanner_workers)) {
            compose.onNodeWithText(resources.getString(id)).assertExists()
        }
        for (english in listOf("Find Working Resolvers", "Configuration", "Workers", "Shuffle IP list")) {
            compose.onAllNodesWithText(english).assertCountEquals(0)
        }
        compose.onNodeWithText(resources.getString(R.string.scanner_start_simple)).performScrollTo().performClick()
        compose.runOnIdle { assertEquals(1, starts) }
        compose.onNodeWithText(resources.getString(R.string.scanner_e2e)).performScrollTo().performClick()
        compose.onNodeWithText(resources.getString(R.string.scanner_start_e2e)).assertExists()
        compose.onNodeWithText("Prism").performScrollTo().performClick()
        compose.onNodeWithText(resources.getString(R.string.scanner_pass_threshold)).assertExists()
        compose.onNodeWithText(resources.getString(R.string.rv_advanced_9f088d)).performScrollTo().performClick()
        compose.onNodeWithText(resources.getString(R.string.scanner_nearby)).assertExists()
        compose.onNodeWithText(resources.getString(R.string.scanner_hero_title)).performScrollTo()
        File(context.filesDir, "scanner-fa-review02.png").outputStream().use {
            assertTrue(compose.onRoot().captureToImage().asAndroidBitmap().compress(Bitmap.CompressFormat.PNG, 100, it))
        }
    }

    @Test fun changingLanguageUpdatesScannerLabelsWithoutLosingTransportSelection() {
        var language by mutableStateOf("en")
        var selected by mutableStateOf(ScanTransportMode.TCP)
        compose.setContent {
            val config = configuration(language)
            val resources = context.createConfigurationContext(config).resources
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config,
                LocalLayoutDirection provides if (language == "fa") LayoutDirection.Rtl else LayoutDirection.Ltr) {
                SlipstreamTheme { Column(Modifier.verticalScroll(rememberScrollState())) {
                    HeroCard()
                    ConfigurationSection("example.invalid", scanPort = "53", timeoutMs = "3000", concurrency = "50",
                        scanTransport = selected, onScanTransportChange = { selected = it },
                        onTestDomainChange = {}, onScanPortChange = {}, onTimeoutChange = {}, onConcurrencyChange = {})
                } }
            }
        }
        for (next in listOf("fa", "zh", "ru", "en")) {
            compose.runOnIdle { language = next }
            val expected = context.createConfigurationContext(configuration(next)).getString(R.string.scanner_hero_title)
            compose.onNodeWithText(expected).assertExists()
            compose.runOnIdle { assertEquals(ScanTransportMode.TCP, selected) }
        }
    }

    @Test fun persianResultsAndValidationErrorsDoNotExposeEnglishBackendMessages() {
        val config = configuration("fa")
        val localized = context.createConfigurationContext(config)
        val resources = localized.resources
        assertEquals(resources.getString(R.string.scanner_error_no_resolvers), scannerErrorText(localized, "No resolvers to scan"))
        assertEquals(resources.getString(R.string.scanner_error_profile), scannerErrorText(localized, "No profile loaded"))
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config,
                LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme { Column {
                    ResultsStatusLabel(ResolverStatus.WORKING)
                    ResultsStatusLabel(ResolverStatus.CENSORED)
                    ResultsStatusLabel(ResolverStatus.TIMEOUT)
                    ResultsEmptyState(isSimpleMode = true)
                } }
            }
        }
        compose.onNodeWithText(resources.getString(R.string.scanner_censored)).assertExists()
        compose.onNodeWithText(resources.getString(R.string.scanner_none_passed)).assertExists()
        compose.onAllNodesWithText("Censored").assertCountEquals(0)
    }
}
