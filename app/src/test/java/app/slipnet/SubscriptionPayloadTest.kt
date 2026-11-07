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
    @Test fun compressedCollectionRoundTripRejectsFutureVersion() {
        val text = (1..30).joinToString("\n") { "rovunet://synthetic$it" }
        assertEquals(text, CollectionLink.decode(CollectionLink.encode(text)))
        assertTrue(runCatching { CollectionLink.decode("rovunet-collection://2.invalid") }.isFailure)
    }
}
