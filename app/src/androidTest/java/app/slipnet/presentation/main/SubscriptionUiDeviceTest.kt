package app.slipnet.presentation.main

import android.content.res.Configuration
import android.graphics.Bitmap
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.data.subscription.SubscriptionWorkerEntryPoint
import app.slipnet.presentation.theme.SlipstreamTheme
import dagger.hilt.android.EntryPointAccessors
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.io.File
import java.util.Locale
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.flow.first
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicBoolean

class SubscriptionUiDeviceTest {
    @get:Rule val compose = createComposeRule()
    @Test fun persianSubscriptionFormUsesCurrentResourcesAndRtlWithoutStartingADownload() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val configuration = Configuration(context.resources.configuration).apply { setLocale(Locale("fa")); setLayoutDirection(Locale("fa")) }
        val resources = context.createConfigurationContext(configuration).resources
        val repository = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides configuration, LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme { SubscriptionDialog(repository, "https://example.invalid/sub?token=synthetic-only", null) {} }
            }
        }
        for (id in listOf(R.string.subscriptions_title, R.string.subscription_name, R.string.subscription_url, R.string.subscription_automatic)) {
            compose.onNodeWithText(resources.getString(id)).assertExists()
        }
        compose.onAllNodesWithText("Subscriptions").assertCountEquals(0)
        compose.onNodeWithTag("subscription-update").assertIsNotEnabled()
        compose.onNodeWithTag("subscription-import").assertIsNotEnabled()
        File(context.filesDir, "subscription-fa-code29.png").outputStream().use {
            assertTrue(compose.onNode(isDialog()).captureToImage().asAndroidBitmap().compress(Bitmap.CompressFormat.PNG, 100, it))
        }
    }

    @Test fun oneImportOrUpdateClickClosesTheWholeDialogAndFailuresKeepItOpen() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val configuration = Configuration(context.resources.configuration).apply { setLocale(Locale.forLanguageTag("fa")); setLayoutDirection(Locale.forLanguageTag("fa")) }
        val resources = context.createConfigurationContext(configuration).resources
        val repository = EntryPointAccessors.fromApplication(context, SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = app.slipnet.di.DatabaseModule.provideDatabase(context)
        val remote = app.slipnet.domain.model.ServerProfile(name = "Subscription UI fixture", domain = "192.0.2.51",
            tunnelType = app.slipnet.domain.model.TunnelType.SSH, sshHost = "192.0.2.51", sshUsername = "fixture", sshPassword = "synthetic")
        val document = app.slipnet.data.export.ConfigExporter().exportSingleProfile(remote)
        val address = "https://example.invalid/subscription-ui-single-action"
        val open = mutableStateOf(true)
        val calls = AtomicInteger(); val dismissed = AtomicInteger(); val fail = AtomicBoolean(false)
        val gate = CompletableDeferred<Unit>()
        var sourceId: String? = null
        try {
            compose.setContent {
                CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides configuration, LocalLayoutDirection provides LayoutDirection.Rtl) {
                    SlipstreamTheme {
                        if (open.value) SubscriptionDialog(repository, address, null,
                            prepare = { name, url, id, auto ->
                                calls.incrementAndGet(); gate.await()
                                if (fail.get()) error("DOWNLOAD_FAILED")
                                repository.prepareDocument(name, url, id, auto, document)
                            }, onDismiss = { dismissed.incrementAndGet(); open.value = false })
                    }
                }
            }
            compose.onNode(hasSetTextAction() and hasText(resources.getString(R.string.subscription_name))).performTextReplacement("First source")
            compose.onNodeWithTag("subscription-import").performScrollTo().performClick()
            compose.waitUntil(5000) { calls.get() == 1 }
            compose.onNodeWithTag("subscription-import").assertIsNotEnabled()
            compose.onNodeWithTag("subscription-update").assertIsNotEnabled()
            gate.complete(Unit)
            compose.waitUntil(8000) { !open.value }
            compose.onAllNodes(isDialog()).assertCountEquals(0)
            assertEquals(1, calls.get()); assertEquals(1, dismissed.get())
            val source = requireNotNull(repository.findSource(address)); sourceId = source.id
            val stored = db.profileDao().getAllProfiles().first().single { it.collectionId == source.id }
            repository.setAutomatic(source.id, true)

            compose.runOnIdle { open.value = true }
            compose.onNode(hasSetTextAction() and hasText(resources.getString(R.string.subscription_name))).performTextReplacement("Updated source")
            compose.onNodeWithTag("subscription-update").performScrollTo().performClick()
            compose.waitUntil(8000) { !open.value }
            assertEquals(2, calls.get()); assertEquals(2, dismissed.get())
            assertEquals("Updated source", repository.findSource(address)?.name)
            assertTrue(requireNotNull(repository.findSource(address)).autoUpdate)
            assertEquals(stored.id, db.profileDao().getAllProfiles().first().single { it.collectionId == source.id }.id)

            fail.set(true)
            compose.runOnIdle { open.value = true }
            compose.onNode(hasSetTextAction() and hasText(resources.getString(R.string.subscription_name))).performTextReplacement("Failed update")
            compose.onNodeWithTag("subscription-update").performScrollTo().performClick()
            compose.waitUntil(8000) { compose.onAllNodesWithTag("subscription-error").fetchSemanticsNodes().isNotEmpty() }
            compose.onNodeWithTag("subscription-error").performScrollTo().assertTextEquals(resources.getString(R.string.subscription_failed))
            assertTrue(open.value); assertEquals(2, dismissed.get())
            assertEquals("Updated source", repository.findSource(address)?.name)
            assertNotNull(db.profileDao().getProfileById(stored.id))
        } finally {
            gate.complete(Unit)
            compose.runOnIdle { open.value = false }
            (sourceId ?: repository.findSource(address)?.id)?.let { id ->
                repository.detach(id)
                db.profileDao().getAllProfiles().first().filter { it.collectionId == id }.forEach { db.profileDao().deleteProfile(it.id) }
            }
            db.close()
        }
    }
}
