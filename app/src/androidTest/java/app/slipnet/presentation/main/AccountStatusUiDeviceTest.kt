package app.slipnet.presentation.main

import android.content.res.Configuration
import android.graphics.Bitmap
import androidx.compose.foundation.layout.*
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.subscription.*
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.theme.SlipstreamTheme
import java.io.File
import java.util.Locale
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class AccountStatusUiDeviceTest {
    @get:Rule val compose = createComposeRule()

    @Test fun persianAccountSummaryKeepsTheCollectionCardAndDistinguishesUnknownFromExhausted() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val configuration = Configuration(context.resources.configuration).apply {
            setLocale(Locale.forLanguageTag("fa")); setLayoutDirection(Locale.forLanguageTag("fa"))
        }
        val resources = context.createConfigurationContext(configuration).resources
        val now = System.currentTimeMillis()
        val gib = 1L shl 30
        val status = AccountStatus("a".repeat(32), "b".repeat(32), "c".repeat(32), "active",
            now + 2 * 86_400_000L, 10 * gib, 9 * gib, "complete", now, now, 7)
        val view = mutableStateOf(AccountStatusView.evaluate(status, now, 1000, now, "", now, 1000))
        var connections = 0
        val profile = ServerProfile(id = 760, name = "Synthetic path", domain = "example.invalid",
            tunnelType = TunnelType.VLESS, collectionId = "status-ui", collectionName = "مجموعهٔ آزمایشی")
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides configuration,
                LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme(darkMode = DarkMode.DARK) {
                    Box(Modifier.width(340.dp).padding(8.dp)) {
                        CollectionCard(listOf(profile), emptyMap(), selected = true, expanded = false,
                            connectedProfileId = null, manualProfileId = profile.id, connecting = false,
                            busy = false, canConnect = true, onConnect = { connections++ }, onDetails = {}, onRename = {},
                            onMove = {}, onTest = {}, onStabilityTest = {}, onExport = {}, onDelete = {}, canDelete = true,
                            accountStatus = view.value)
                    }
                }
            }
        }
        compose.onNodeWithText("GiB", substring = true).assertExists()
        val progress = SemanticsMatcher.keyIsDefined(SemanticsProperties.ProgressBarRangeInfo)
        compose.onNode(progress).assertExists()
        File(context.filesDir, "account-status-fa-code32.png").outputStream().use {
            assertTrue(compose.onRoot().captureToImage().asAndroidBitmap().compress(Bitmap.CompressFormat.PNG, 100, it))
        }
        compose.runOnIdle { view.value = view.value.copy(trustworthy = false, severity = AccountSeverity.UNKNOWN, error = "UNAVAILABLE") }
        compose.onNodeWithText(resources.getString(R.string.account_status_unavailable)).assertExists()
        compose.onAllNodes(progress).assertCountEquals(0)
        compose.onAllNodesWithText(resources.getString(R.string.account_status_limit_reached)).assertCountEquals(0)
        compose.onNodeWithTag("collection-connect-status-ui").performClick()
        compose.runOnIdle { assertEquals("Missing advisory metadata must not block manual connection", 1, connections) }
    }
}
