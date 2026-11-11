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
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.LayoutDirection
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
                val density = LocalDensity.current
                CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Rtl,
                    LocalDensity provides Density(density.density, 1.5f)) {
                    MarqueeText("نام طولانی پروفایل آزمایشی با متن فارسی و English که باید در کادر خودش حرکت کند", Modifier.testTag("rtl"))
                }
            }
        }
        compose.mainClock.advanceTimeBy(200)
        val long = compose.onNodeWithTag("long")
        val short = compose.onNodeWithTag("short")
        val rtl = compose.onNodeWithTag("rtl")
        val bounds = long.getUnclippedBoundsInRoot()
        val rtlBounds = rtl.getUnclippedBoundsInRoot()
        val layouts = mutableListOf<TextLayoutResult>()
        long.performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(layouts) }
        assertEquals(1, layouts.single().lineCount)
        assertFalse(layouts.single().isLineEllipsized(0))
        val beforeLong = long.captureToImage().asAndroidBitmap()
        val beforeShort = short.captureToImage().asAndroidBitmap()
        val beforeRtl = rtl.captureToImage().asAndroidBitmap()
        compose.mainClock.advanceTimeBy(3000)
        assertEquals(bounds, long.getUnclippedBoundsInRoot())
        assertFalse("Overflowing text must visibly move", beforeLong.sameAs(long.captureToImage().asAndroidBitmap()))
        assertTrue("Short labels must remain stationary", beforeShort.sameAs(short.captureToImage().asAndroidBitmap()))
        assertEquals(rtlBounds, rtl.getUnclippedBoundsInRoot())
        assertFalse("Large RTL/mixed labels must visibly move", beforeRtl.sameAs(rtl.captureToImage().asAndroidBitmap()))
    }
}
