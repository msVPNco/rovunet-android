package app.slipnet.presentation.main

import android.content.res.Configuration
import android.graphics.Bitmap
import androidx.compose.runtime.CompositionLocalProvider
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
        compose.onNodeWithText(resources.getString(R.string.subscription_preview)).assertIsNotEnabled()
        File(context.filesDir, "subscription-fa-code29.png").outputStream().use {
            assertTrue(compose.onNode(isDialog()).captureToImage().asAndroidBitmap().compress(Bitmap.CompressFormat.PNG, 100, it))
        }
    }
}
