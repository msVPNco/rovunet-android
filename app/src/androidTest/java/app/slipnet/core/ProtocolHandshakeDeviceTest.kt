package app.slipnet.core

import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import kotlinx.coroutines.*
import org.junit.Assert.*
import org.junit.Test

class ProtocolHandshakeDeviceTest {
    @Test fun packagedUdpProbesRequireRealHandshakesAndCancelWithoutTouchingTheLiveCore() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val encoded = requireNotNull(InstrumentationRegistry.getArguments().getString("udpProtocolFixture"))
        val fixture = org.json.JSONObject(String(android.util.Base64.decode(encoded, android.util.Base64.DEFAULT), Charsets.UTF_8))
        val native = File(context.applicationInfo.nativeLibraryDir)
        val certificates = File(context.filesDir, "native-certificates-device-test")
        val before = app.slipnet.tunnel.XrayBridge.activeEndpoint
        suspend fun measure(link: String) = ProtocolHandshake.measure(link, File(native, "librovunet_awg.so"),
            File(native, "libveyra_core_launcher.so"), certificates)
        val links = fixture.getJSONArray("links")
        try {
            repeat(3) { index ->
                val link = links.getString(index)
                assertTrue(ProtocolHandshake.supports(link))
                assertTrue(withTimeout(15000) { measure(link) } >= 0)
                assertEquals(before, app.slipnet.tunnel.XrayBridge.activeEndpoint)
            }
            val stored = certificates.listFiles().orEmpty().filter { it.extension == "pem" }.associate { it.name to it.readText() }
            assertEquals(2, stored.size)
            repeat(2) { offset -> assertTrue(withTimeout(15000) { measure(links.getString(offset + 1)) } >= 0) }
            assertEquals(stored, certificates.listFiles().orEmpty().filter { it.extension == "pem" }.associate { it.name to it.readText() })
            val failed = runCatching { withTimeout(15000) { measure(fixture.getString("wrongWireguard")) } }
            assertTrue("Wrong WireGuard key was accepted", failed.isFailure)
            val pending = async(Dispatchers.IO) { measure(fixture.getString("wrongWireguard")) }
            delay(200)
            withTimeout(4000) { pending.cancelAndJoin() }
            assertEquals(before, app.slipnet.tunnel.XrayBridge.activeEndpoint)
            assertNull(app.slipnet.tunnel.TransportTrust.request.value)
        } finally { certificates.deleteRecursively() }
    }
}
