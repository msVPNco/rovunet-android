package app.slipnet.presentation.main

import androidx.compose.runtime.*
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import dagger.hilt.android.EntryPointAccessors
import app.slipnet.data.subscription.*
import app.slipnet.presentation.theme.SlipstreamTheme
import app.slipnet.R
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger

class PublicProviderUiDeviceTest {
    @get:Rule val compose = createComposeRule()
    @Test fun providerRequiresConsentCreatesOneSourceAndReaddingReviewsExistingCard() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val repository = EntryPointAccessors.fromApplication(context,SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db = app.slipnet.di.DatabaseModule.provideDatabase(context)
        val provider=PublicProviders.Provider.ANONYMOUS
        assertNull(repository.findSource(provider.address))
        var open by mutableStateOf(true)
        val calls=AtomicInteger()
        var sourceId:String?=null
        val body="socks5://fixture:synthetic@192.0.2.18:10001#first\nvmess://unsupported\nsocks5://fixture:synthetic@192.0.2.18:10001#renamed\nsocks5://fixture:synthetic@192.0.2.18:10002#second"
        try {
            compose.setContent { SlipstreamTheme {
                if(open) PublicSourceDialog(repository,provider=provider,prepare={ name,address,update ->
                    calls.incrementAndGet()
                    assertEquals(provider.address,address);assertTrue(update)
                    val existing=repository.findSource(address)
                    repository.prepareDocument(existing?.name ?: name,address,existing?.id,false,body,publicMode=true)
                }, onDismiss={open=false},onImported={open=false})
            } }
            compose.onNodeWithTag("public-source-confirm").assertIsNotEnabled()
            compose.runOnIdle { assertEquals(0,calls.get()) }
            compose.onNodeWithTag("public-source-consent").performScrollTo().performClick()
            compose.onNodeWithTag("public-source-confirm").performClick()
            val summary=context.getString(R.string.alpha17_public_summary,2,1,1)
            compose.waitUntil(8000) { compose.onAllNodesWithText(summary).fetchSemanticsNodes().isNotEmpty() }
            compose.onNodeWithTag("public-source-confirm").performClick()
            compose.waitUntil(8000) { !open }
            val source=requireNotNull(repository.findSource(provider.address));sourceId=source.id
            assertTrue(source.id.startsWith("public:"));assertFalse(source.autoUpdate)
            val ids=db.profileDao().getAllProfiles().first().filter{it.collectionId==source.id}.map{it.id}.toSet()
            assertEquals(2,ids.size)
            compose.runOnIdle { open=true }
            compose.onNodeWithTag("public-source-consent").performScrollTo().performClick()
            compose.onNodeWithTag("public-source-confirm").performClick()
            compose.waitUntil(8000) { calls.get()==2 && compose.onAllNodesWithText(summary).fetchSemanticsNodes().isNotEmpty() }
            compose.onNodeWithTag("public-source-confirm").performClick()
            compose.waitUntil(8000) { !open }
            assertEquals(source.id,repository.findSource(provider.address)?.id)
            assertEquals(ids,db.profileDao().getAllProfiles().first().filter{it.collectionId==source.id}.map{it.id}.toSet())
        } finally {
            compose.runOnIdle { open=false }
            (sourceId ?: repository.findSource(provider.address)?.id)?.let { id ->
                repository.detach(id)
                db.profileDao().getAllProfiles().first().filter{it.collectionId==id}.forEach{db.profileDao().deleteProfile(it.id)}
            }
            db.close()
        }
    }
}
