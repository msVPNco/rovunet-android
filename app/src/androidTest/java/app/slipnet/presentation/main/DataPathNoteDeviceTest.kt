package app.slipnet.presentation.main

import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.R
import app.slipnet.domain.model.DataPathState
import app.slipnet.domain.model.dataPathNoteVisible
import app.slipnet.presentation.theme.SlipstreamTheme
import org.junit.Rule
import org.junit.Test

class DataPathNoteDeviceTest {
    @get:Rule val compose = createComposeRule()

    @Test fun reopeningHomeDoesNotRenewAnExpiredSuccess() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val shown = mutableStateOf(true)
        val now = mutableStateOf(1_000L)
        val deadline = 4_000L
        compose.setContent {
            SlipstreamTheme {
                if (shown.value && dataPathNoteVisible(DataPathState.VERIFIED, deadline, now.value)) {
                    DataPathCheckCard(DataPathState.VERIFIED)
                }
            }
        }
        compose.onNodeWithText(context.getString(R.string.data_path_toast)).assertExists()
        compose.runOnIdle { shown.value = false; now.value = 5_000L }
        compose.runOnIdle { shown.value = true }
        compose.onNodeWithText(context.getString(R.string.data_path_toast)).assertDoesNotExist()
    }

    @Test fun failedCheckLivesInDetailsAndDoesNotLeaveAHomeRow() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val details = mutableStateOf(false)
        compose.setContent {
            SlipstreamTheme {
                if (details.value) DataPathCheckDetails(DataPathState.UNKNOWN)
                else DataPathCheckCard(DataPathState.UNKNOWN)
            }
        }
        compose.onNodeWithText(context.getString(R.string.data_path_unknown)).assertDoesNotExist()
        compose.onNodeWithText(context.getString(R.string.data_path_hint)).assertDoesNotExist()
        compose.runOnIdle { details.value = true }
        compose.onNodeWithText(context.getString(R.string.data_path_unknown)).assertExists()
        compose.onNodeWithText(context.getString(R.string.data_path_hint)).assertExists()
    }
}
