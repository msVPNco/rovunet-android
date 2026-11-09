package app.slipnet

import app.slipnet.data.subscription.*
import app.slipnet.data.export.ConfigImporter
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import java.util.Base64

class PublicProvidersTest {
    private val first="https://raw.githubusercontent.com/mahsanet/MahsaFreeConfig/refs/heads/main/mci/sub_1.txt"
    private val second=first.replace("mci","mtn")
    private fun body(port:Int) = Base64.getEncoder().encodeToString("socks5://synthetic:fixture@192.0.2.11:$port#fixture".toByteArray())
    @Test fun mahsaChildrenAreBoundedAndDecodedBeforePublicDeduplication() = runBlocking {
        val calls=mutableListOf<String>()
        val document=PublicProviders.download(PublicProviders.Provider.MAHSA.address) { url, limit ->
            calls += url
            if(url==PublicProviders.Provider.MAHSA.address) "#profile-title: example\n$first\n$second" else body(10001)
        }
        val parsed=PublicFeed.parse(document, ConfigImporter(), "synthetic")
        assertEquals(1,parsed.profiles.size);assertEquals(1,parsed.duplicates)
        assertEquals(listOf(PublicProviders.Provider.MAHSA.address,first,second),calls)
    }
    @Test fun malformedNestedManifestAndFailedChildNeverYieldPartialReplacement() = runBlocking {
        for (url in listOf("http://example.invalid/feed", "https://example.invalid/feed", first+"?redirect=1", first.replace("sub_1","sub_9"))) {
            assertTrue(runCatching { PublicProviders.mahsaChildren(url) }.isFailure)
        }
        val calls=mutableListOf<String>()
        val failure=runCatching {
            PublicProviders.download(PublicProviders.Provider.MAHSA.address) { url,_ ->
                calls+=url
                when(url) { PublicProviders.Provider.MAHSA.address -> "$first\n$second"; first -> body(10001); else -> error("DOWNLOAD_FAILED") }
            }
        }
        assertTrue(failure.isFailure);assertEquals(3,calls.size)
    }
    @Test fun ordinaryPublicSourceNeverFollowsEmbeddedHttpsUrls() = runBlocking {
        var calls=0
        assertEquals("https://example.invalid/nested",PublicProviders.download(PublicProviders.Provider.ANONYMOUS.address) { _,_ -> calls++;"https://example.invalid/nested" })
        assertEquals(1,calls)
    }
    @Test fun publishedEmptyChildIsNotADownloadFailureAndDoesNotDiscardOtherChildren() = runBlocking {
        val combined=PublicProviders.download(PublicProviders.Provider.MAHSA.address) { url,_ ->
            when(url) { PublicProviders.Provider.MAHSA.address -> "$first\n$second"; first -> ""; else -> body(10001) }
        }
        assertEquals(1,PublicFeed.parse(combined,ConfigImporter(),"synthetic").profiles.size)
        assertEquals("",PublicFeed.expand("  \n"))
    }

}
