package app.slipnet.service

import androidx.test.ext.junit.runners.AndroidJUnit4
import app.slipnet.tunnel.SshTunnelInstance
import java.io.DataInputStream
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SshStartupCancellationDeviceTest {
    @Test fun stopClosesPendingSocksHandshakeAndNeverPublishesAListener() {
        repeat(3) {
            ServerSocket(0).use { upstream ->
                val pool = Executors.newFixedThreadPool(2)
                val greeted = CountDownLatch(1)
                val instance = SshTunnelInstance("device-synthetic-startup")
                val localPort = ServerSocket(0).use { it.localPort }
                try {
                    val remote = pool.submit<Boolean> {
                        upstream.accept().use { socket ->
                            socket.soTimeout = 3000
                            val input = DataInputStream(socket.getInputStream())
                            check(input.readUnsignedByte() == 5)
                            input.readFully(ByteArray(input.readUnsignedByte()))
                            greeted.countDown()
                            input.read() == -1 // Never return a SOCKS greeting.
                        }
                    }
                    val connecting = pool.submit<Result<Unit>> {
                        instance.startOverSocks5Proxy("127.0.0.1",22,"synthetic-user","synthetic-only",
                            "127.0.0.1",upstream.localPort,null,null,localPort)
                    }
                    assertTrue("SOCKS greeting not reached", greeted.await(5, TimeUnit.SECONDS))
                    val at = System.nanoTime()
                    instance.stop()
                    assertTrue(connecting.get(2, TimeUnit.SECONDS).isFailure)
                    assertTrue(remote.get(2, TimeUnit.SECONDS))
                    assertTrue(TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - at) < 2000)
                    assertFalse(instance.isRunning())
                    assertTrue(runCatching { Socket("127.0.0.1", localPort).use { } }.isFailure)
                    instance.stop()
                } finally { instance.stop(); pool.shutdownNow() }
            }
        }
    }
}
