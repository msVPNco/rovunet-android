package app.slipnet.presentation

import android.content.res.Configuration
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.presentation.main.StartupPresentation
import app.slipnet.presentation.theme.SlipstreamTheme
import java.util.Locale
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class StartupCoverDeviceTest {
    @get:Rule val compose=createComposeRule()
    @Test fun brandCoverUsesAllThemesAndLanguagesAndReleasesContentWithoutAMinimumWait() {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        var language by mutableStateOf("en")
        var theme by mutableStateOf(DarkMode.LIGHT)
        var loading by mutableStateOf(true)
        var contentCompositions=0
        compose.setContent {
            val config=Configuration(base.resources.configuration).apply { setLocale(Locale.forLanguageTag(language)) }
            val resources=base.createConfigurationContext(config).resources
            CompositionLocalProvider(LocalResources provides resources,LocalConfiguration provides config,
                LocalLayoutDirection provides if(language=="fa")LayoutDirection.Rtl else LayoutDirection.Ltr) {
                SlipstreamTheme(darkMode=theme) {
                    StartupPresentation(loading) { SideEffect { contentCompositions++ };Text("Synthetic content ready") }
                }
            }
        }
        for(lang in listOf("en","fa","zh","ru")) for(mode in listOf(DarkMode.LIGHT,DarkMode.DARK,DarkMode.AMOLED)) {
            compose.runOnIdle { language=lang;theme=mode }
            compose.onNodeWithTag("startup-cover").assertIsDisplayed()
            compose.onNodeWithTag("startup-brand").assertTextEquals("Rovunet")
            compose.onNodeWithTag("startup-message").assertIsDisplayed()
            compose.onNodeWithText("Synthetic content ready").assertDoesNotExist()
        }
        compose.runOnIdle { assertEquals(0,contentCompositions) }
        compose.mainClock.autoAdvance=false
        compose.runOnIdle { loading=false }
        compose.mainClock.advanceTimeByFrame()
        compose.onNodeWithText("Synthetic content ready").assertExists()
        compose.mainClock.advanceTimeBy(180)
        compose.onNodeWithTag("startup-cover").assertDoesNotExist()
        compose.mainClock.autoAdvance=true
    }
}
