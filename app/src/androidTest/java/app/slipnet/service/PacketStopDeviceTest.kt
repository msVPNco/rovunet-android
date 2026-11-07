package app.slipnet.service

import android.os.ParcelFileDescriptor
import android.os.SystemClock
import app.slipnet.tunnel.HevSocks5Tunnel
import org.junit.Assert.*
import org.junit.Test

class PacketStopDeviceTest {
    @Test fun idlePacketEngineStopsAndCanRestartWithoutTraffic() {
        repeat(5) {
            val pair = ParcelFileDescriptor.createReliableSocketPair()
            try {
                HevSocks5Tunnel.start(pair[0], "127.0.0.1", 9).getOrThrow()
                Thread.sleep(150)
                val start = SystemClock.elapsedRealtime()
                HevSocks5Tunnel.stop()
                assertFalse(HevSocks5Tunnel.isRunning())
                assertTrue("Idle native shutdown exceeded 2 seconds", SystemClock.elapsedRealtime() - start < 2000)
            } finally { pair.forEach { it.close() }; HevSocks5Tunnel.stop() }
        }
    }
    @Test fun immediateStopCannotWaitForAnInitializerThatAlreadyExited() {
        repeat(10) {
            val pair = ParcelFileDescriptor.createReliableSocketPair()
            try {
                HevSocks5Tunnel.start(pair[0], "127.0.0.1", 9).getOrThrow()
                val start = SystemClock.elapsedRealtime()
                HevSocks5Tunnel.stop()
                assertTrue(SystemClock.elapsedRealtime() - start < 2000)
                assertFalse(HevSocks5Tunnel.isRunning())
            } finally { pair.forEach { it.close() }; HevSocks5Tunnel.stop() }
        }
    }
}
