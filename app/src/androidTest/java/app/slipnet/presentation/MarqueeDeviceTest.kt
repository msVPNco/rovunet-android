package app.slipnet.presentation

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.width
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.graphics.asAndroidBitmap
import androidx.compose.ui.unit.dp
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.semantics.SemanticsActions
import app.slipnet.presentation.common.components.MarqueeText
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

class MarqueeDeviceTest {
    @get:Rule val compose = createComposeRule()
    @Test fun overflowingTextMovesInsideFixedSingleLineWhileShortTextStaysStill() {
        compose.mainClock.autoAdvance = false
        compose.setContent {
            Column(Modifier.width(140.dp)) {
                MarqueeText("A very long profile title that must move inside its own fixed card", Modifier.testTag("long"))
                MarqueeText("Short", Modifier.testTag("short"))
            }
        }
        compose.mainClock.advanceTimeBy(200)
        val long = compose.onNodeWithTag("long")
        val short = compose.onNodeWithTag("short")
        val bounds = long.getUnclippedBoundsInRoot()
        val layouts = mutableListOf<TextLayoutResult>()
        long.performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(layouts) }
        assertEquals(1, layouts.single().lineCount)
        assertFalse(layouts.single().isLineEllipsized(0))
        val beforeLong = long.captureToImage().asAndroidBitmap()
        val beforeShort = short.captureToImage().asAndroidBitmap()
        compose.mainClock.advanceTimeBy(3000)
        assertEquals(bounds, long.getUnclippedBoundsInRoot())
        assertFalse("Overflowing text must visibly move", beforeLong.sameAs(long.captureToImage().asAndroidBitmap()))
        assertTrue("Short labels must remain stationary", beforeShort.sameAs(short.captureToImage().asAndroidBitmap()))
    }
}
