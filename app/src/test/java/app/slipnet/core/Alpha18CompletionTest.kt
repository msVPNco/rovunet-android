package app.slipnet.core

import app.slipnet.data.export.WireBase64
import app.slipnet.data.subscription.SubscriptionMetadata
import app.slipnet.data.subscription.AccountStatusView
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Alpha18CompletionTest {
    private val uuid = "00000000-0000-4000-8000-000000000001"
    private val key = WireBase64.encode(ByteArray(32) { (it + 1).toByte() })
    private val wg = "[Interface]\nPrivateKey=$key\nAddress=10.77.0.2/32\nDNS=1.1.1.1\n[Peer]\nPublicKey=$key\nEndpoint=example.test:51821\nAllowedIPs=0.0.0.0/0,::/0\nPersistentKeepalive=25"
    private fun rejected(block: () -> Unit) = assertTrue(runCatching(block).isFailure)

    @Test fun metadataUsesBothDirectionsAndSeparatesUnlimitedFromUnknown() {
        val at = 1_790_000_000_000L
        val payload = SubscriptionMetadata.usage("upload=12; download=18; total=100; expire=1790001000", at)
        val status = SubscriptionMetadata.readStatus(payload.toString())
        assertEquals(70L, status.remainingBytes)
        assertEquals(1_000_000L, AccountStatusView.evaluate(status, at, 1000, at, "", at, 1000).remainingMillis)
        assertTrue(status.providerReported)
        val unlimited = SubscriptionMetadata.readStatus(SubscriptionMetadata.usage("upload=0;download=100;total=0;expire=0", at).toString())
        assertTrue(unlimited.volumeKnown); assertNull(unlimited.limitBytes); assertEquals(100L, unlimited.usedBytes)
        val unknown = SubscriptionMetadata.readStatus(SubscriptionMetadata.usage("expire=0", at).toString())
        assertFalse(unknown.volumeKnown)
        rejected { SubscriptionMetadata.usage("total=100;expire=0", at) }
        rejected { SubscriptionMetadata.usage("upload=9223372036854775807;download=1;total=0", at) }
        rejected { SubscriptionMetadata.usage("upload=-1;download=0", at) }
        rejected { SubscriptionMetadata.usage("expire=1;expire=2", at) }
    }
    @Test fun optionalNamesDoNotExposeSubscriptionTokensOrOverwriteUserNames() {
        assertEquals("mine", SubscriptionMetadata.name("mine", "remote", "example.test"))
        assertEquals("saved", SubscriptionMetadata.name("", "remote", "example.test", "saved"))
        assertEquals("Remote", SubscriptionMetadata.name("", "base64:" + WireBase64.encode("Remote".toByteArray()), "example.test"))
        assertEquals("example.test", SubscriptionMetadata.name("", "https://example.test/private-token", "example.test"))
    }
    @Test fun tlsAndFinalMaskRoundtripWithoutWeakeningTrust() {
        val fm = "{\"tcp\":[{\"type\":\"fragment\",\"settings\":{\"packets\":\"tlshello\",\"length\":\"50-100\",\"delay\":\"1-2\"}}]}"
        val link = CoreSharing.uri("vless", uuid, "example.test", "443", mapOf("security" to "tls", "cs" to "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", "fm" to fm), "fixture")
        val stream = CoreLink.parse(link).outbound.getJSONObject("streamSettings")
        assertEquals("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", stream.getJSONObject("tlsSettings").getString("cipherSuites"))
        assertEquals(JSONObject(fm).toString(), stream.getJSONObject("finalmask").toString())
        val vmess = CoreSharing.uri("vmess", uuid, "example.test", "443", mapOf("security" to "tls", "encryption" to "aes-128-gcm", "cs" to "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", "fm" to fm), "fixture")
        val vmessAfterEdit = CoreLink.parse(CoreForm.decode(vmess).encode("edited")).outbound
        assertEquals("aes-128-gcm", vmessAfterEdit.getJSONObject("settings").getJSONArray("vnext").getJSONObject(0).getJSONArray("users").getJSONObject(0).getString("security"))
        assertTrue(vmessAfterEdit.getJSONObject("streamSettings").has("finalmask"))
        val edited = CoreLink.parse(CoreForm.decode(link).encode("renamed")).outbound
        assertEquals(stream.toString(), edited.getJSONObject("streamSettings").toString())
        rejected { CoreLink.parse(link.replace("security=tls", "security=none")) }
        assertTrue(runCatching { CoreOptions.finalMask("{\"tcp\":" + "[".repeat(1000)) }.exceptionOrNull() is IllegalArgumentException)
        rejected { CoreOptions.finalMask("{\"tcp\":[{\"type\":\"fragment\",\"settings\":{\"masterKeyLog\":\"/tmp/log\"}}]}") }
        rejected { CoreOptions.finalMask("{\"udp\":[{\"type\":\"noise\",\"settings\":{\"script\":\"anything\"}}]}") }
    }
    @Test fun shadowsocks2022ChecksKeysAndPreservesCipher() {
        for (method in listOf("2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305")) {
            val size = if (method.endsWith("128-gcm")) 16 else 32
            val auth = WireBase64.encode(ByteArray(size) { 7 })
            val link = "ss://" + WireBase64.encode("$method:$auth".toByteArray()) + "@example.test:443"
            val parsed = CoreLink.parse(CoreForm.decode(link).encode("fixture"))
            assertEquals(method, parsed.outbound.getJSONObject("settings").getJSONArray("servers").getJSONObject(0).getString("method"))
        }
        rejected { CoreLink.parse("ss://" + WireBase64.encode("2022-blake3-aes-128-gcm:bad".toByteArray()) + "@example.test:443") }
    }
    @Test fun hysteriaPortHoppingKeepsSalamanderAndRejectsUnusableRanges() {
        val stream = CoreLink.parse("hy2://secret@example.test:443?mport=443,8443-8445&hop-interval=15s&obfs=salamander&obfs-password=test").outbound.getJSONObject("streamSettings")
        val masks = stream.getJSONObject("finalmask").getJSONArray("udp")
        assertEquals("salamander", masks.getJSONObject(0).getString("type"))
        assertEquals("443,8443-8445", masks.getJSONObject(1).getJSONObject("settings").getString("remotePorts"))
        rejected { CoreLink.parse("hy2://secret@example.test:443?mport=0-99999") }
        rejected { CoreLink.parse("hy2://secret@example.test:443?hop-interval=1s") }
    }
    @Test fun amnezia31PreservesObfuscationAndEditorRoundtrip() {
        val text = wg.replace("[Peer]", "Jc=4\nJmin=40\nJmax=70\nS1=16\nS2=16\nS3=16\nS4=16\nH1=123-456\nH2=500-600\nH3=700-800\nH4=900-999\nHeaderProtectionKey=$key\nRandomTrailers=true\nDisableCookies=1\nContentPaddingAddition=8-16\n[Peer]")
        val parsed = CoreLink.parse(text)
        assertEquals("amneziawg", parsed.outbound.getString("protocol"))
        val settings = parsed.outbound.getJSONObject("settings")
        val options = settings.getJSONObject("amnezia-wg-option")
        assertEquals(3, options.getInt("version")); assertEquals("123-456", options.getString("h1"))
        assertTrue(options.getBoolean("random-trailers")); assertEquals(key, options.getString("header-protection-key"))
        assertEquals(settings.toString(), CoreLink.parse(CoreForm.decode(text).encode("fixture")).outbound.getJSONObject("settings").toString())
        assertEquals("wireguard", CoreLink.parse(wg).outbound.getString("protocol"))
        rejected { CoreLink.parse(text.replace("Jc=4", "Jc=4\nJc=5")) }
        rejected { CoreLink.parse(text + "\nPostUp=anything") }
        rejected { CoreLink.parse(text.replace("[Peer]", "J1=invalid\n[Peer]")) }
    }
}
