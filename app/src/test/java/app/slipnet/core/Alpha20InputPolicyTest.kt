package app.slipnet.core

import java.io.ByteArrayInputStream
import java.util.concurrent.CancellationException
import org.junit.Assert.*
import org.junit.Test

class Alpha20InputPolicyTest {
    @Test fun redirectsStayOnHttpsReleaseInfrastructure() {
        assertTrue(UpdateNetworkPolicy.allows(UpdateManifest.API_URL + "?page=1&per_page=30"))
        assertTrue(UpdateNetworkPolicy.allows(UpdateManifest.RELEASES_URL + "/download/v0.3.0-alpha20/Rovunet-Full.apk"))
        val cdn = "https://release-assets.githubusercontent.com/github-production-release-asset/test?sig=temporary"
        assertFalse(UpdateNetworkPolicy.allows(cdn))
        assertTrue(UpdateNetworkPolicy.allows(cdn, redirect = true))
        for (bad in listOf("http://github.com/file", "https://example.invalid/file", "https://github.com.evil.invalid/file",
            "https://api.github.com/repos/other/repo/releases", "https://user@release-assets.githubusercontent.com/file",
            "https://release-assets.githubusercontent.com:444/file", cdn + "#fragment")) {
            assertFalse(bad, UpdateNetworkPolicy.allows(bad, redirect = true))
        }
    }

    @Test fun partialResponsesMustMatchTheRequestedOffsetAndFullAssetSize() {
        assertTrue(UpdateNetworkPolicy.rangeStart("bytes 20-99/100", 20, 100))
        for (bad in listOf(null, "bytes 0-99/100", "bytes 20-98/100", "bytes 20-99/101", "bytes 20-99/*",
            "bytes 999999999999999999999-99/100")) assertFalse(UpdateNetworkPolicy.rangeStart(bad, 20, 100))
    }

    @Test fun fileLimitsApplyToBytesAndDoNotSilentlyRepairMalformedUtf8() {
        val bytes = "سلام".toByteArray(Charsets.UTF_8)
        assertEquals("سلام", BoundedTextInput.read(ByteArrayInputStream(bytes), bytes.size))
        assertThrows(IllegalArgumentException::class.java) { BoundedTextInput.read(ByteArrayInputStream(bytes), bytes.size - 1) }
        assertThrows(java.nio.charset.CharacterCodingException::class.java) {
            BoundedTextInput.read(ByteArrayInputStream(byteArrayOf(0xc3.toByte(), 0x28)), 16)
        }
        assertThrows(CancellationException::class.java) {
            BoundedTextInput.read(ByteArrayInputStream(bytes), 16) { throw CancellationException() }
        }
    }
}
