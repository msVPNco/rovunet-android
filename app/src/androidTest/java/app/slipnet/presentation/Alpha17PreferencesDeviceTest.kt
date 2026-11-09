package app.slipnet.presentation

import androidx.datastore.preferences.core.PreferenceDataStoreFactory
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.local.datastore.PreferencesDataStore
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.util.UUID

class Alpha17PreferencesDeviceTest {
    @Test fun freshPreferencesFollowSystemAndPreserveExplicitTheme() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val file = File(context.cacheDir, "alpha17-${UUID.randomUUID()}.preferences_pb")
        val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        try {
            val store = PreferenceDataStoreFactory.create(scope = scope, produceFile = { file })
            val preferences = PreferencesDataStore(context, store)
            assertEquals(DarkMode.SYSTEM, preferences.darkMode.first())
            preferences.setDarkMode(DarkMode.DARK)
            assertEquals(DarkMode.DARK, preferences.darkMode.first())
            preferences.setDarkMode(DarkMode.LIGHT)
            assertEquals(DarkMode.LIGHT, preferences.darkMode.first())
        } finally { scope.coroutineContext[Job]?.cancelAndJoin(); file.delete() }
    }
}
