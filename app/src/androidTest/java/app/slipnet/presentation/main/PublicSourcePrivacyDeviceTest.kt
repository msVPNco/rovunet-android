package app.slipnet.presentation.main

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.subscription.*
import app.slipnet.di.DatabaseModule
import app.slipnet.presentation.theme.SlipstreamTheme
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.flow.first
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.util.Locale

class PublicSourcePrivacyDeviceTest {
    @get:Rule val compose=createComposeRule()
    @Test fun bothBuiltinDialogsHideUrlsInFourLanguagesAndThreeThemes() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val repository=EntryPointAccessors.fromApplication(context,SubscriptionWorkerEntryPoint::class.java).subscriptions()
        var language by mutableStateOf("en")
        var theme by mutableStateOf(DarkMode.LIGHT)
        var provider by mutableStateOf(PublicProviders.Provider.MAHSA)
        compose.setContent {
            val locale=Locale.forLanguageTag(language)
            val config=Configuration(context.resources.configuration).apply { setLocale(locale);setLayoutDirection(locale) }
            val resources=context.createConfigurationContext(config).resources
            CompositionLocalProvider(LocalResources provides resources,LocalConfiguration provides config,
                LocalLayoutDirection provides if(language=="fa") LayoutDirection.Rtl else LayoutDirection.Ltr) {
                SlipstreamTheme(darkMode=theme) {
                    key(language,theme,provider) {
                        PublicSourceDialog(repository,provider,prepare={ _,_,_ -> error("Test must not fetch") },onDismiss={},onImported={})
                    }
                }
            }
        }
        for(lang in listOf("en","fa","zh","ru")) for(mode in listOf(DarkMode.LIGHT,DarkMode.DARK,DarkMode.AMOLED)) for(item in PublicProviders.Provider.entries) {
            compose.runOnIdle { language=lang;theme=mode;provider=item }
            compose.waitForIdle()
            compose.onNodeWithTag("public-source-address-hidden").assertExists()
            compose.onAllNodesWithText("https://",substring=true,useUnmergedTree=true).assertCountEquals(0)
            compose.onAllNodesWithText("raw.githubusercontent.com",substring=true,useUnmergedTree=true).assertCountEquals(0)
            compose.onNodeWithTag("public-source-confirm").assertIsNotEnabled()
        }
    }
    @Test fun savedPublicSubscriptionCannotRevealOrCopyItsSourceWhilePrivateEntryRemainsEditable() = runBlocking<Unit> {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val repository=EntryPointAccessors.fromApplication(context,SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db=DatabaseModule.provideDatabase(context)
        val address="https://example.invalid/synthetic-public-private-address"
        val preview=repository.prepareDocument("Saved public fixture",address,null,false,
            "socks5://fixture:synthetic@192.0.2.11:10001#fixture",publicMode=true)
        repository.apply(preview)
        var saved by mutableStateOf(true)
        var open by mutableStateOf(true)
        try {
            compose.setContent { SlipstreamTheme {
                if(open) key(saved) { SubscriptionDialog(repository,initialUrl=null,initialId=if(saved)preview.source.id else null,
                    onDismiss={open=false}) }
            } }
            compose.waitUntil(8000) { compose.onAllNodesWithTag("subscription-source-address-hidden").fetchSemanticsNodes().isNotEmpty() }
            compose.onAllNodesWithText(address,substring=true,useUnmergedTree=true).assertCountEquals(0)
            compose.onAllNodesWithText("example.invalid",substring=true,useUnmergedTree=true).assertCountEquals(0)
            compose.onAllNodesWithText(context.getString(R.string.rv_show_0df6f1),useUnmergedTree=true).assertCountEquals(0)
            compose.runOnIdle { saved=false }
            compose.waitForIdle()
            compose.onAllNodesWithTag("subscription-source-address-hidden").assertCountEquals(0)
            compose.onNodeWithText(context.getString(R.string.subscription_url)).assertExists()
        } finally {
            compose.runOnIdle { open=false }
            repository.detach(preview.source.id)
            db.profileDao().getAllProfiles().first().filter { it.collectionId==preview.source.id }.forEach { db.profileDao().deleteProfile(it.id) }
            db.close()
        }
    }
}
