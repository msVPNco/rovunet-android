package app.slipnet

import app.slipnet.data.subscription.SubscriptionPayload
import app.slipnet.data.export.CollectionLink
import org.junit.Assert.*
import org.junit.Test

class SubscriptionPayloadTest {
    @Test fun supportsPlainAndBase64AndVersionedStableIds() {
        val link = "vless://00000000-0000-0000-0000-000000000001@example.invalid:443?security=tls&type=tcp#test"
        assertEquals(link, SubscriptionPayload.read(link).single().link)
        val encoded = java.util.Base64.getEncoder().encodeToString(link.toByteArray())
        assertEquals(link, SubscriptionPayload.read(encoded).single().link)
        assertEquals("stable", SubscriptionPayload.read("""{"version":1,"items":[{"id":"stable","link":"$link"}]}""").single().key)
    }
    @Test fun rejectsErrorDocumentsEmptyUnknownVersionsAndSecurityFields() {
        for (text in listOf("", "<html>error</html>", """{"version":2,"items":[]}""",
            """{"version":1.5,"items":[]}""", """{"version":"1","items":[]}""",
            """{"version":1,"items":[{"id":"a","link":"vless://x","newSecurityPolicy":true}]}""")) {
            assertTrue(text, runCatching { SubscriptionPayload.read(text) }.isFailure)
        }
    }
    @Test fun panelV1ValidatesOptionalNameAndUnicodeIdLength() {
        fun feed(id: String, name: String) =
            """{"version":1,"name":$name,"items":[{"id":"$id","link":"vless://fixture"}]}"""
        val boundary = "🌐".repeat(200)
        assertEquals(boundary, SubscriptionPayload.read(feed(boundary, "\"panel\"")).single().key)
        assertTrue(runCatching { SubscriptionPayload.read(feed(boundary + "a", "\"panel\"")) }.isFailure)
        assertTrue(runCatching { SubscriptionPayload.read(feed("stable", "null")) }.isFailure)
    }

    @Test fun unknownTransportNeverDowngradesKnownOrProtectedInput() {
        fun eligible(link: String) = SubscriptionPayload.unknownTransport(SubscriptionPayload.Item("stable", link))
        assertTrue(eligible("exampletransport://user@node.example.invalid:443"))
        for (link in listOf("vless://invalid", "wireguard://invalid", "rovunet-enc://invalid",
            "slipnet-bundle-enc://invalid", "https://node.example.invalid:443", "future://",
            "future://node.example.invalid:70000", "future://node.example.invalid:443\nrovunet-enc://x")) {
            assertFalse(link, eligible(link))
        }
        assertFalse(SubscriptionPayload.unknownTransport(SubscriptionPayload.Item(null, "future://node.example.invalid:443")))
    }

    @Test fun compressedCollectionRoundTripRejectsFutureVersion() {
        val text = (1..30).joinToString("\n") { "rovunet://synthetic$it" }
        assertEquals(text, CollectionLink.decode(CollectionLink.encode(text)))
        assertTrue(runCatching { CollectionLink.decode("rovunet-collection://2.invalid") }.isFailure)
    }
}
