package app.slipnet.presentation.main

import android.graphics.Bitmap
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createEmptyComposeRule
import androidx.compose.ui.semantics.SemanticsActions
import androidx.test.core.app.ActivityScenario
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.MainActivity
import com.google.gson.Gson
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.io.File

/** Captures the real home screen with synthetic profiles; no connection is requested. */
class SoftPopUiDeviceTest {
    @get:Rule val compose = createEmptyComposeRule()

    @Test fun interruptionMessagesKeepTheirSpecificReasonInAllLanguages() {
        val context = androidx.test.platform.app.InstrumentationRegistry.getInstrumentation().targetContext
        for (language in listOf("en", "fa", "zh", "ru")) {
            val config = android.content.res.Configuration(context.resources.configuration)
            config.setLocale(java.util.Locale.forLanguageTag(language))
            val resources = context.createConfigurationContext(config).resources
            for ((code, id) in listOf("PROBE_BUSY" to app.slipnet.R.string.probe_busy,
                "PROBE_CLEANUP" to app.slipnet.R.string.probe_cleanup,
                "PROBE_CHANGED" to app.slipnet.R.string.probe_changed,
                "PROBE_NETWORK_CHANGED" to app.slipnet.R.string.review02_network_changed)) {
                org.junit.Assert.assertEquals(resources.getString(id), app.slipnet.presentation.theme.UiErrors.text(resources, code))
            }
        }
    }

    @Test fun realHomeKeepsComfortableHeaderAcrossRtlAndThemes() = runBlocking {
        val inst = InstrumentationRegistry.getInstrumentation()
        val context = inst.targetContext
        val prefs = PreferencesDataStore(context)
        val oldMode = prefs.darkMode.first()
        val ui = context.getSharedPreferences("rovunet_ui", android.content.Context.MODE_PRIVATE)
        val oldLanguage = ui.getString("language", null)
        val db = DatabaseModule.provideDatabase(context)
        val ids = (1..4).map { index ->
            db.profileDao().insertProfile(ProfileMapper(Gson()).toEntity(ServerProfile(
                name = "Synthetic path $index", domain = "example.invalid", tunnelType = TunnelType.SSH,
                collectionId = "soft-pop-$index", collectionName = "Review $index")))
        }
        try {
            for ((language, mode) in listOf("en" to DarkMode.DARK, "fa" to DarkMode.DARK,
                "fa" to DarkMode.AMOLED, "en" to DarkMode.LIGHT)) {
                prefs.setDarkMode(mode)
                assertTrue(ui.edit().putString("language", language).commit())
                val needsWelcome = !prefs.firstLaunchDone.first()
                ActivityScenario.launch(MainActivity::class.java).use { screen ->
                    var start = ""
                    screen.onActivity { start = it.getString(app.slipnet.R.string.rv_get_started_983f31) }
                    if (needsWelcome) {
                        compose.waitUntil(15000) { compose.onAllNodesWithText(start).fetchSemanticsNodes().isNotEmpty() }
                        // This is snapshot setup, not a gesture test. Invoke the
                        // real button action without tapping a moving dialog window.
                        compose.onNodeWithText(start).performSemanticsAction(SemanticsActions.OnClick) { action -> assertTrue(action()) }
                        kotlinx.coroutines.withTimeout(5000) { prefs.firstLaunchDone.first { it } }
                    }
                    compose.waitUntil(15000) {
                        compose.onAllNodesWithText("Review 1").fetchSemanticsNodes().isNotEmpty()
                    }
                    compose.onNodeWithTag("soft-pop-header").assertIsDisplayed()
                    val header = compose.onNodeWithTag("soft-pop-header").fetchSemanticsNode()
                    assertTrue("Header is shorter than 72 dp", header.boundsInRoot.height / context.resources.displayMetrics.density >= 72f)
                    compose.waitForIdle()
                    compose.onAllNodes(isDialog()).assertCountEquals(0)
                    // Semantics can still describe the preceding Activity while a new
                    // window is drawing. Require both the requested appearance and a
                    // painted collection, rather than saving a light/loading frame as
                    // the AMOLED evidence. These bounds are pixels in the device window.
                    val card = compose.onNodeWithTag("collection-soft-pop-1").fetchSemanticsNode().boundsInWindow
                    var captured: Bitmap? = null
                    compose.waitUntil(15000) {
                        val frame = requireNotNull(inst.uiAutomation.takeScreenshot())
                        val background = frame.getPixel(frame.width / 2, frame.height * 2 / 3)
                        val surface = frame.getPixel(card.center.x.toInt().coerceIn(0, frame.width - 1),
                            card.center.y.toInt().coerceIn(0, frame.height - 1))
                        val red = android.graphics.Color.red(background)
                        val green = android.graphics.Color.green(background)
                        val blue = android.graphics.Color.blue(background)
                        val matches = when (mode) {
                            DarkMode.AMOLED -> red <= 2 && green <= 2 && blue <= 2
                            DarkMode.LIGHT -> red >= 235 && green >= 235 && blue >= 235
                            else -> red < 80 && green < 80 && blue < 100 && blue > red
                        }
                        val painted = kotlin.math.abs(android.graphics.Color.red(surface) - red) +
                            kotlin.math.abs(android.graphics.Color.green(surface) - green) +
                            kotlin.math.abs(android.graphics.Color.blue(surface) - blue) > 30
                        if (matches && painted) { captured = frame; true }
                        else { frame.recycle(); false }
                    }
                    val bitmap = requireNotNull(captured)
                    File(context.filesDir, "home-alpha18-$language-${mode.name.lowercase()}.png").outputStream().use { output ->
                        assertTrue(bitmap.compress(Bitmap.CompressFormat.PNG, 100, output))
                    }
                    bitmap.recycle()
                }
            }
        } finally {
            prefs.setDarkMode(oldMode)
            val editor = ui.edit()
            if (oldLanguage == null) editor.remove("language") else editor.putString("language", oldLanguage)
            assertTrue(editor.commit())
            ids.forEach { db.profileDao().deleteProfile(it) }
            db.close()
        }
    }
}

