package app.slipnet

import app.slipnet.data.subscription.*
import app.slipnet.data.export.ConfigImporter
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test
import java.util.Base64

class PublicProvidersTest {
    private val old = "https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa"
    private fun encoded(json: String): String {
        val cipher = Cipher.getInstance("AES/CBC/PKCS5Padding")
        cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec("ca815ecfdb1f153a".toByteArray(Charsets.US_ASCII), "AES"),
            IvParameterSpec("lvcas56410c97lpb".toByteArray(Charsets.US_ASCII)))
        return Base64.getEncoder().encodeToString(cipher.doFinal(json.toByteArray()))
    }
    @Test fun mahsaDirectDecryptsDeduplicatesAndNeverFetchesHiddifyOrAds() = runBlocking {
        val link="socks5://synthetic:fixture@192.0.2.11:10001#fixture"
        val json=org.json.JSONObject().put("ads_url","https://example.invalid/advert")
            .put("mci",org.json.JSONArray().put(org.json.JSONObject().put("config",link)))
            .put("mtn",org.json.JSONArray().put(org.json.JSONObject().put("config",link)))
        for (address in listOf(old, PublicProviders.Provider.MAHSA.address)) {
            val calls=mutableListOf<String>()
            val result=PublicProviders.download(address) { target, limit ->
                calls += target; assertEquals(2*1024*1024,limit); encoded(json.toString())
            }
            assertEquals(listOf(PublicProviders.Provider.MAHSA.address),calls)
            assertEquals(link,result)
            assertEquals(1, PublicFeed.parse(result,ConfigImporter(),"synthetic").profiles.size)
        }
    }
    @Test fun malformedEncryptedOrJsonDataNeverYieldsPartialReplacement() = runBlocking {
        for (body in listOf("not-base64", encoded("{}"),encoded("{\"mci\":[],\"mtn\":[]}"),
            encoded("{\"mci\":[{\"config\":123}],\"mtn\":[]}"),
            encoded("{\"mci\":[{\"config\":\"socks5://first\\nsecond\"}],\"mtn\":[]}"),
            encoded("{\"mci\":[{\"config\":\"socks5://first\"},{}],\"mtn\":[]}"))) {
            assertTrue(runCatching { MahsaFeed.decode(body) }.isFailure)
        }
        val oversized="A".repeat(MahsaFeed.MAX_BYTES+1)
        assertTrue(runCatching { MahsaFeed.decode(oversized) }.isFailure)
    }
    @Test fun ordinaryPublicSourceNeverFollowsEmbeddedHttpsUrls() = runBlocking {
        var calls=0
        assertEquals("https://example.invalid/nested",PublicProviders.download(PublicProviders.Provider.ANONYMOUS.address) { _,_ -> calls++;"https://example.invalid/nested" })
        assertEquals(1,calls)
    }
    @Test fun aliasesResolveToOneSourceAndAllSavedFreeAddressesStayHidden() {
        assertEquals(PublicProviders.Provider.MAHSA.address,PublicProviders.canonical(old))
        assertTrue(PublicProviders.hidesAddress("legacy-id",old))
        assertTrue(PublicProviders.hidesAddress("public:custom","https://example.invalid/custom"))
        assertTrue(PublicProviders.hidesAddress(null,PublicProviders.Provider.ANONYMOUS.address))
        assertFalse(PublicProviders.hidesAddress("private-id","https://example.invalid/private"))
        assertEquals("https://example.invalid/private",PublicProviders.canonical("https://example.invalid/private"))
    }
    @Test fun failedOrCancelledDownloadPropagatesWithoutFollowingAnyOtherSource() = runBlocking {
        assertThrows(IllegalStateException::class.java) { runBlocking { PublicProviders.download(old) { _,_ -> error("DOWNLOAD_FAILED") } } }
        val child=launch(start=CoroutineStart.LAZY) { PublicProviders.download(old) { _,_ -> fail("Cancelled fetch started"); "" } }
        child.cancel();child.join();assertTrue(child.isCancelled)
    }
}
