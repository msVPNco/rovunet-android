package app.slipnet.presentation

import android.content.res.Configuration
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalResources
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.unit.LayoutDirection
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.core.CoreForm
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import app.slipnet.presentation.common.components.ProfileListItem
import app.slipnet.presentation.main.CollectionCard
import app.slipnet.presentation.profiles.CoreProfileEditor
import app.slipnet.presentation.profiles.EditProfileUiState
import app.slipnet.presentation.profiles.SoarTcpModeSelector
import app.slipnet.domain.model.SoarConfig
import app.slipnet.domain.model.PingResult
import androidx.compose.foundation.layout.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.dp
import app.slipnet.presentation.theme.SlipstreamTheme
import java.util.Locale
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class CardInteractionDeviceTest {
    @get:Rule val compose = createComposeRule()
    private val profile = ServerProfile(id = 701, name = "Synthetic SSH", domain = "example.invalid", tunnelType = TunnelType.SSH, collectionId = "ui-synthetic")

    @Test fun cardButtonAndBothMenuTestsCallTheirOwnAction() {
        var quick = 0
        var detailed = 0
        var connections = 0
        compose.setContent { SlipstreamTheme(darkMode = DarkMode.DARK) {
            CollectionCard(listOf(profile), emptyMap(), selected = false, expanded = false,
                connectedProfileId = null, manualProfileId = null, connecting = false,
                busy = false, canConnect = true, onConnect = { connections++ }, onDetails = {}, onRename = {},
                onMove = {}, onTest = { quick++ }, onStabilityTest = { detailed++ }, onExport = {}, onDelete = {}, canDelete = true)
        } }
        compose.onNodeWithTag("collection-quick-test-ui-synthetic").performClick()
        val menu = InstrumentationRegistry.getInstrumentation().targetContext.getString(R.string.rv_collection_actions_06a9e8)
        compose.onNodeWithContentDescription(menu).performClick()
        compose.onNodeWithTag("collection-menu-quick-test").performClick()
        compose.onNodeWithContentDescription(menu).performClick()
        compose.onNodeWithTag("collection-menu-detailed-test").performClick()
        compose.runOnIdle { assertEquals(2, quick); assertEquals(1, detailed); assertEquals(0, connections) }
    }

    @Test fun tcpResultsAreGreenThenGrayAndFailuresRemainRedAcrossThemes() {
        var mode by mutableStateOf(DarkMode.DARK)
        var result by mutableStateOf<PingResult>(PingResult.Success(22, quick = true, measuredAt = 1_000_000))
        var expectedGreen = androidx.compose.ui.graphics.Color.Unspecified
        var expectedGray = androidx.compose.ui.graphics.Color.Unspecified
        var expectedRed = androidx.compose.ui.graphics.Color.Unspecified
        compose.setContent { SlipstreamTheme(darkMode = mode) {
            val green = app.slipnet.presentation.theme.ConnectionSuccessColor
            val gray = app.slipnet.presentation.theme.PreviousResultColor
            val red = androidx.compose.material3.MaterialTheme.colorScheme.error
            SideEffect { expectedGreen = green; expectedGray = gray; expectedRed = red }
            ProfileListItem(profile, isSelected = false, isConnected = false, pingResult = result, measurementNowMillis = 1_000_000,
                onClick = {}, onConnectClick = {}, onEditClick = {}, onDeleteClick = {}, onExportClick = {},
                onShareQrClick = {}, onPinClick = {}, onPingClick = {})
        } }
        fun color(tag: String): androidx.compose.ui.graphics.Color {
            val layouts = mutableListOf<TextLayoutResult>()
            compose.onNodeWithTag(tag, useUnmergedTree = true).performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(layouts) }
            return layouts.single().layoutInput.style.color
        }
        for (theme in listOf(DarkMode.LIGHT, DarkMode.DARK, DarkMode.AMOLED)) {
            compose.runOnIdle { mode = theme; result = PingResult.Success(22, quick = true, measuredAt = 1_000_000) }
            assertEquals(expectedGreen, color("profile-latency-701"))
            compose.onNodeWithTag("profile-latency-701", useUnmergedTree = true).assertTextContains("TCP")
            compose.runOnIdle { result = PingResult.Success(22, quick = true, measuredAt = 699_999) }
            assertEquals(expectedGray, color("profile-latency-701"))
            compose.runOnIdle { result = PingResult.Success(22, quick = true, measuredAt = 1_000_000, currentNetwork = false) }
            assertEquals(expectedGray, color("profile-latency-701"))
            compose.runOnIdle { result = PingResult.Error("Synthetic refusal") }
            assertEquals(expectedRed, color("profile-test-result-701"))
        }
    }

    @Test fun lockedPathDoesNotRevealTheRestoredDestinationLine() {
        compose.setContent { SlipstreamTheme {
            ProfileListItem(profile.copy(isLocked = true), isSelected = false, isConnected = false,
                onClick = {}, onConnectClick = {}, onEditClick = {}, onDeleteClick = {}, onExportClick = {},
                onShareQrClick = {}, onPinClick = {}, onPingClick = {})
        } }
        compose.onAllNodesWithText("example.invalid", substring = true).assertCountEquals(0)
    }

    @Test fun persianCollectionCountUsesCurrentResourcesWhileActivityContextStaysAlive() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val config = Configuration(context.resources.configuration).apply {
            setLocale(Locale.forLanguageTag("fa")); setLayoutDirection(Locale.forLanguageTag("fa"))
        }
        val resources = context.createConfigurationContext(config).resources
        val expected = resources.getQuantityString(R.plurals.connection_paths, 1, 1)
        assertTrue(expected.contains("مسیر"))
        compose.setContent {
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config, LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme(darkMode = DarkMode.DARK) {
                    CollectionCard(listOf(profile), emptyMap(), selected = true, expanded = false,
                        connectedProfileId = null, manualProfileId = profile.id, connecting = false,
                        busy = false, canConnect = true, onConnect = {}, onDetails = {}, onRename = {},
                        onMove = {}, onTest = {}, onStabilityTest = {}, onExport = {}, onDelete = {}, canDelete = true)
                }
            }
        }
        compose.onNodeWithText(expected, substring = true).assertExists()
    }

    @Test fun collectionBrowsingNeverConnectsAndItsLocalActionWorksWhenCollapsed() {
        var expanded by mutableStateOf(false)
        var connections = 0
        compose.setContent { SlipstreamTheme(darkMode = DarkMode.DARK) {
            CollectionCard(listOf(profile), emptyMap(), selected = true, expanded = expanded,
                connectedProfileId = null, manualProfileId = profile.id, connecting = false,
                busy = false, canConnect = true, onConnect = { connections++ }, onDetails = { expanded = !expanded },
                onRename = {}, onMove = {}, onTest = {}, onStabilityTest = {}, onExport = {}, onDelete = {}, canDelete = true)
        } }
        repeat(4) { index ->
            compose.onNodeWithTag("collection-ui-synthetic").performClick()
            compose.runOnIdle { assertEquals(index % 2 == 0, expanded); assertEquals(0, connections) }
        }
        compose.onNodeWithTag("collection-connect-ui-synthetic").performClick()
        compose.runOnIdle { assertEquals(1, connections); assertFalse(expanded) }
    }

    @Test fun firstTapSelectsSecondConnectsAndNestedActionsDoNotConnect() {
        var selected by mutableStateOf(false)
        var connected = 0
        var edited = 0
        var deleted = 0
        compose.setContent { SlipstreamTheme {
            ProfileListItem(profile, isSelected = selected, isConnected = false, onClick = { selected = true },
                onConnectClick = { connected++ }, onEditClick = { edited++ }, onDeleteClick = { deleted++ }, onExportClick = {},
                onShareQrClick = {}, onPinClick = {}, onPingClick = {})
        } }
        compose.onNodeWithTag("profile-701").performClick()
        compose.runOnIdle { assertTrue(selected); assertEquals(0, connected) }
        compose.onNodeWithTag("profile-edit-701").performClick()
        compose.onNodeWithTag("profile-delete-701").performClick()
        compose.runOnIdle { assertEquals(1, edited); assertEquals(1, deleted); assertEquals(0, connected) }
        compose.onNodeWithTag("profile-701").performClick()
        compose.runOnIdle { assertEquals(1, connected) }
    }

    @Test fun localCancelRemainsAvailableWhileConnectionIsBusy() {
        var cancelled = 0
        compose.setContent { SlipstreamTheme {
            ProfileListItem(profile, isSelected = true, isConnected = false, isConnecting = true, connectionBusy = true,
                onClick = {}, onConnectClick = { cancelled++ }, onEditClick = {}, onDeleteClick = {},
                onExportClick = {}, onShareQrClick = {}, onPinClick = {}, onPingClick = {})
        } }
        compose.onNodeWithContentDescription(InstrumentationRegistry.getInstrumentation().targetContext.getString(R.string.ui_more_actions)).performClick()
        compose.onNodeWithTag("profile-connect-701").assertIsEnabled().performClick()
        compose.runOnIdle { assertEquals(1, cancelled) }
    }

    @Test fun soarModesHaveDistinctLabelsFitRtlAndSelectTheActualWireValues() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val config = Configuration(context.resources.configuration).apply { setLocale(Locale("fa")); setLayoutDirection(Locale("fa")) }
        val resources = context.createConfigurationContext(config).resources
        var selected by mutableStateOf(SoarConfig.TCP_AUTO)
        compose.setContent {
            val density = LocalDensity.current
            CompositionLocalProvider(LocalResources provides resources, LocalConfiguration provides config,
                LocalLayoutDirection provides LayoutDirection.Rtl, LocalDensity provides Density(density.density, 1.4f)) {
                SlipstreamTheme { Box(Modifier.width(280.dp)) { SoarTcpModeSelector(selected) { selected = it } } }
            }
        }
        val labels = listOf(R.string.editor_tcp_auto, R.string.editor_soar_tcp_parallel, R.string.editor_soar_tcp_only, R.string.editor_soar_udp_only)
        labels.forEach { compose.onAllNodesWithText(resources.getString(it)).assertCountEquals(1) }
        val bounds = compose.onNodeWithTag("soar-tcp-modes").fetchSemanticsNode().boundsInRoot
        SoarConfig.TCP_MODES.forEach { mode ->
            val chip = compose.onNodeWithTag("soar-tcp-$mode").assertIsDisplayed()
            val box = chip.fetchSemanticsNode().boundsInRoot
            assertTrue(box.left >= bounds.left && box.right <= bounds.right)
            chip.performClick().assertIsSelected()
            compose.runOnIdle { assertEquals(mode, selected) }
        }
    }

    @Test fun measuredHttpsLatencyRemainsVisibleWithoutConnecting() {
        var connected = 0
        compose.setContent { SlipstreamTheme(darkMode = DarkMode.DARK) {
            ProfileListItem(profile, isSelected = false, isConnected = false,
                pingResult = PingResult.Success(184, verified = true), onClick = {}, onConnectClick = { connected++ },
                onEditClick = {}, onDeleteClick = {}, onExportClick = {}, onShareQrClick = {}, onPinClick = {}, onPingClick = {})
        } }
        compose.onNodeWithTag("profile-latency-701", useUnmergedTree = true).assertIsDisplayed().assertTextEquals(InstrumentationRegistry.getInstrumentation().targetContext.getString(R.string.alpha17_ping, 184L))
        compose.runOnIdle { assertEquals(0, connected) }
    }

    @Test fun persianCoreEditorUsesTranslatedLabelsAndLtrTechnicalValues() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val config = Configuration(context.resources.configuration).apply { setLocale(Locale("fa")); setLayoutDirection(Locale("fa")) }
        val localized = context.createConfigurationContext(config)
        val form = CoreForm.fresh("wireguard").with("address", "10.0.0.2/32")
        compose.setContent {
            // Match RovunetLocale: resources/direction change while the Activity context stays alive.
            CompositionLocalProvider(LocalResources provides localized.resources, LocalConfiguration provides config, LocalLayoutDirection provides LayoutDirection.Rtl) {
                SlipstreamTheme(darkMode = DarkMode.DARK) {
                    CoreProfileEditor(EditProfileUiState(name = "آزمون", coreForm = form), { _, _ -> }, {}, {}, {}, remember { SnackbarHostState() })
                }
            }
        }
        val keyLabel = localized.getString(R.string.rv_private_key_477bf9)
        assertNotEquals("Private key", keyLabel)
        compose.onNodeWithText(keyLabel).assertExists()
        compose.onAllNodesWithText("Private key").assertCountEquals(0)
        val layouts = mutableListOf<TextLayoutResult>()
        compose.onNodeWithText("10.0.0.2/32").performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(layouts) }
        assertTrue(layouts.isNotEmpty())
        assertEquals(TextDirection.Ltr, layouts.first().layoutInput.style.textDirection)
    }

    @Test fun collectionMenuBrowsingAndExplicitConnectionRemainSeparate() {
        var browsed = 0
        var connected = 0
        compose.setContent { SlipstreamTheme {
            CollectionCard(listOf(profile), emptyMap(), selected = true, expanded = false,
                connectedProfileId = null, manualProfileId = profile.id, connecting = false,
                busy = false, canConnect = true, onConnect = { connected++ }, onDetails = { browsed++ },
                onRename = {}, onMove = {}, onTest = {}, onStabilityTest = {}, onExport = {}, onDelete = {}, canDelete = false)
        } }
        val menu = InstrumentationRegistry.getInstrumentation().targetContext.getString(R.string.rv_collection_actions_06a9e8)
        compose.onNodeWithContentDescription(menu).performClick()
        compose.onNodeWithTag("collection-menu-paths").performClick()
        compose.runOnIdle { assertEquals(1, browsed); assertEquals(0, connected) }
        compose.onNodeWithContentDescription(menu).performClick()
        compose.onNodeWithTag("collection-menu-connect").performClick()
        compose.runOnIdle { assertEquals(1, browsed); assertEquals(1, connected) }
    }
}
