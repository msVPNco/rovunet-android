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
}
