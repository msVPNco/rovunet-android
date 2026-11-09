package app.slipnet.core

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.net.ServerSocket
import java.net.Socket

/** Requires an attached Android device/emulator. This is NOT an internet test. */
class CoreDeviceSmokeTest {
    @Test fun packagedCoreStartsStopsAndAcceptsSocksHandshake() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val core = CoreProcess()
        val binary = File(context.applicationInfo.nativeLibraryDir, "libveyra_xray.so")
        val launcher = File(context.applicationInfo.nativeLibraryDir, "libveyra_core_launcher.so")
        assertTrue(binary.canExecute()); assertTrue(launcher.canExecute())
        val port = ServerSocket(0).use { it.localPort }
        val link = "hysteria2://test@example.invalid:443?sni=example.invalid"
        try {
            repeat(5) {
                core.start(binary, CoreLink.runtime(link, port), launcher).getOrThrow()
                assertTrue(core.isRunning())
                Socket("127.0.0.1", port).use { socket ->
                    socket.soTimeout = 2000
                    socket.getOutputStream().write(byteArrayOf(5, 1, 0))
                    assertEquals(5, socket.getInputStream().read())
                    assertEquals(0, socket.getInputStream().read())
                }
                core.stop(); assertFalse(core.isRunning())
            }
        } finally { core.stop() }
    }
    @Test fun stoppingIndependentCandidateLeavesLiveCoreAndPrivateEndpointWorking() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val binary = File(context.applicationInfo.nativeLibraryDir, "libveyra_xray.so")
        val launcher = File(context.applicationInfo.nativeLibraryDir, "libveyra_core_launcher.so")
        val live = CoreProcess(diagnostics = false)
        val candidate = CoreProcess(diagnostics = false)
        val first = LocalSocksEndpoint.privateCore()
        val second = LocalSocksEndpoint.privateCore()
        assertNotEquals(first.port, second.port)
        fun greeting(endpoint: LocalSocksEndpoint) {
            Socket("127.0.0.1", endpoint.port).use { socket ->
                socket.soTimeout = 2000
                socket.outputStream.write(byteArrayOf(5, 1, 2))
                assertEquals(5, socket.inputStream.read())
                assertEquals(2, socket.inputStream.read())
            }
        }
        try {
            val link = "vless://00000000-0000-4000-8000-000000000001@example.invalid:443?security=tls&type=tcp"
            live.start(binary, CoreLink.runtime(link, first.port, first.user, first.password), launcher).getOrThrow()
            val aead = android.util.Base64.encodeToString("aes-256-gcm:synthetic-password".toByteArray(), android.util.Base64.NO_WRAP)
            candidate.start(binary, CoreLink.runtime("ss://$aead@example.invalid:8388#Synthetic", second.port, second.user, second.password), launcher).getOrThrow()
            greeting(first); greeting(second)
            candidate.stop()
            assertFalse(candidate.isRunning())
            assertTrue(live.isRunning())
            greeting(first)
        } finally { candidate.stop(); live.stop() }
    }

    @Test fun trojanVmessAndGrpcUseThePackagedCore() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val binary = File(context.applicationInfo.nativeLibraryDir, "libveyra_xray.so")
        val launcher = File(context.applicationInfo.nativeLibraryDir, "libveyra_core_launcher.so")
        val id = "00000000-0000-4000-8000-000000000001"
        val vmess = org.json.JSONObject().put("v", "2").put("ps", "Synthetic VMess").put("add", "example.invalid").put("port", "443").put("id", id).put("aid", "0").put("net", "ws").put("tls", "tls")
        val links = listOf("trojan://synthetic@example.invalid:443?security=tls&type=tcp#Display name",
            "vmess://" + app.slipnet.data.export.WireBase64.encode(vmess.toString().toByteArray()),
            "vless://$id@example.invalid:443?security=tls&type=grpc&serviceName=fixture#Display name")
        val process = CoreProcess(diagnostics = false)
        try {
            for (link in links) {
                val parsed = CoreLink.parse(link)
                assertEquals(parsed.outbound.toString(), CoreLink.parse(CoreForm.decode(link).encode(parsed.name)).outbound.toString())
                val endpoint = LocalSocksEndpoint.privateCore()
                process.start(binary, CoreLink.runtime(link, endpoint.port, endpoint.user, endpoint.password), launcher).getOrThrow()
                Socket("127.0.0.1", endpoint.port).use { socket ->
                    socket.soTimeout = 2000
                    socket.outputStream.write(byteArrayOf(5, 1, 2))
                    assertEquals(5, socket.inputStream.read()); assertEquals(2, socket.inputStream.read())
                }
                process.stop(); assertFalse(process.isRunning())
            }
        } finally { process.stop() }
    }

}
