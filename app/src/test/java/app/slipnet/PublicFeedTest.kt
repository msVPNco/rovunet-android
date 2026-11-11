package app.slipnet

import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.export.ImportResult
import app.slipnet.data.subscription.PublicFeed
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test

class PublicFeedTest {
    private fun link(index: Int) = "socks5://fixture:synthetic@192.0.2.1:${10000 + index}#fixture-$index"
    @Test fun threeThousandPublicProfilesRemainDistinctWithoutRelaxingOrdinaryImport() = runBlocking {
        val body = (0 until 3000).joinToString("\n", transform = ::link)
        val result = PublicFeed.parse(body, ConfigImporter(), "fixture-device")
        assertEquals(3000, result.profiles.size)
        assertEquals(0, result.unsupported)
        assertTrue(ConfigImporter().parseAndImport(body) is ImportResult.Error)
    }
    @Test fun publicOnlySkipsUnsupportedAndExactDuplicatesWithAnExplicitCount() = runBlocking {
        val body = "# descriptive comment\n${link(1)}\nvmess://unsupported\n${link(1)}\n${link(2)}"
        val result = PublicFeed.parse(body, ConfigImporter(), "fixture-device")
        assertEquals(2, result.profiles.size)
        assertEquals(1, result.unsupported)
        assertEquals(1, result.duplicates)
    }
    @Test fun excessiveSourcesFailInsteadOfSilentlyTruncating() = runBlocking {
        try { PublicFeed.parse((0..10000).joinToString("\n") { link(1) }, ConfigImporter(), "fixture-device"); fail() }
        catch (e: IllegalArgumentException) { assertEquals("PUBLIC_FEED_LIMIT", e.message) }
    }
}
