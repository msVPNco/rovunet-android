package app.slipnet.presentation.main

import android.graphics.Bitmap
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createEmptyComposeRule
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
                ActivityScenario.launch(MainActivity::class.java).use {
                    compose.waitUntil(15000) {
                        compose.onAllNodesWithText("Review 1").fetchSemanticsNodes().isNotEmpty()
                    }
                    compose.onNodeWithTag("soft-pop-header").assertIsDisplayed()
                    val header = compose.onNodeWithTag("soft-pop-header").fetchSemanticsNode()
                    assertTrue("Header is shorter than 72 dp", header.boundsInRoot.height / context.resources.displayMetrics.density >= 72f)
                    compose.waitForIdle()
                    val bitmap = requireNotNull(inst.uiAutomation.takeScreenshot())
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
