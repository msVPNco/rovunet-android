package app.slipnet.presentation

import androidx.compose.foundation.layout.height
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.material3.Text
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.dp
import app.slipnet.domain.model.ServerProfile
import app.slipnet.presentation.main.collectionPathItems
import app.slipnet.presentation.theme.SlipstreamTheme
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class Alpha17UiDeviceTest {
    @get:Rule val compose = createComposeRule()
    @Test fun languageWelcomeHasFourExplicitChoicesAndNoImportOrConnection() {
        assertFalse(languageReady(null, null))
        assertFalse(languageReady(null, false))
        assertFalse(languageReady("unknown", false))
        assertTrue(languageReady("fa", false))
        assertTrue(languageReady("system", true))
        assertTrue(languageReady(null, true))
        var choice = ""
        compose.setContent { SlipstreamTheme { LanguageWelcome { choice = it } } }
        compose.onNodeWithTag("language-welcome").assertExists()
        for (code in listOf("fa", "en", "ru", "zh")) compose.onNodeWithTag("language-$code").assertIsDisplayed()
        compose.runOnIdle { assertEquals("", choice) }
        compose.onNodeWithTag("language-fa").performClick()
        compose.runOnIdle { assertEquals("fa", choice) }
    }
    @Test fun expandedThreeThousandPathsComposeOnlyVisibleRowsAndScrollToLast() {
        val composed = mutableSetOf<Long>()
        val paths = (1L..3000L).map { ServerProfile(id = it, name = "Path $it", collectionId = "large-fixture") }
        compose.setContent { SlipstreamTheme {
            LazyColumn(Modifier.testTag("large-paths")) {
                collectionPathItems(paths) { profile ->
                    SideEffect { composed += profile.id }
                    Text(profile.name, Modifier.height(100.dp).testTag("path-${profile.id}"))
                }
            }
        } }
        compose.onNodeWithTag("path-1").assertIsDisplayed()
        compose.onNodeWithTag("path-3000").assertDoesNotExist()
        compose.runOnIdle { assertTrue("Expanded list eagerly composed ${composed.size} rows", composed.size < 50) }
        compose.onNodeWithTag("large-paths").performScrollToIndex(2999)
        compose.onNodeWithTag("path-3000").assertIsDisplayed()
        compose.runOnIdle { assertTrue(composed.size < 100) }
    }
}
