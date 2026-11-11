package app.slipnet.tunnel

import org.junit.Assert.*
import org.junit.Test

class DnsFallbackTest {
    private val request = byteArrayOf(0x12, 0x34, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0)
    private fun reply(code: Int) = request.copyOf().also { it[2] = 0x81.toByte(); it[3] = (0x80 or code).toByte() }

    @Test fun aWorkingPrimaryAndANegativeAnswerDoNotContactTheFallback() {
        for (rcode in listOf(0, 3)) {
            val calls = mutableListOf<String>()
            val expected = reply(rcode)
            val actual = DnsFallback.query(request, "192.0.2.1", "192.0.2.2") { host, _ -> calls += host; expected }
            assertArrayEquals(expected, actual)
            assertEquals(listOf("192.0.2.1"), calls)
        }
    }

    @Test fun connectionFailureServfailAndRefusedUseExactlyOneConfiguredFallback() {
        for (primaryReply in listOf(null, reply(2), reply(5))) {
            val calls = mutableListOf<String>()
            val expected = reply(0)
            val actual = DnsFallback.query(request, "192.0.2.1", "192.0.2.2") { host, query ->
                assertArrayEquals(request, query)
                calls += host
                if (calls.size == 1) primaryReply else expected
            }
            assertArrayEquals(expected, actual)
            assertEquals(listOf("192.0.2.1", "192.0.2.2"), calls)
        }
    }

    @Test fun mismatchedTruncatedAndNonResponsePacketsDoNotBecomeCachedAnswers() {
        for (invalid in listOf(ByteArray(2), reply(0).also { it[0] = 0 }, request)) {
            var count = 0
            val expected = reply(0)
            val result = DnsFallback.query(request, "primary", "fallback") { _, _ -> if (++count == 1) invalid else expected }
            assertArrayEquals(expected, result)
            assertEquals(2, count)
        }
        assertFalse(DnsFallback.cacheable(reply(2)))
        assertFalse(DnsFallback.cacheable(reply(5)))
        assertTrue(DnsFallback.cacheable(reply(0)))
        assertTrue(DnsFallback.cacheable(reply(3)))
    }

    @Test fun emptyOrIdenticalFallbacksDoNotMultiplyAttemptsAndFinalFailuresRemainVisible() {
        for (fallback in listOf(null, "", " 192.0.2.1 ")) {
            var count = 0
            val failure = reply(2)
            assertArrayEquals(failure, DnsFallback.query(request, "192.0.2.1", fallback) { _, _ -> count++; failure })
            assertEquals(1, count)
        }
        var count = 0
        assertNull(DnsFallback.query(request, "primary", "fallback") { _, _ -> count++; null })
        assertEquals(2, count)
    }
}
