package app.slipnet.core

import java.net.InetSocketAddress
import java.net.Socket
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext

/** TCP reachability only: opens and closes a socket without sending application data. */
object TcpPing {
    suspend fun measure(host: String, port: Int, timeoutMs: Int = 5000,
        socketFactory: () -> Socket = { Socket() }): Long = withContext(Dispatchers.IO) {
        require(host.isNotBlank() && port in 1..65535 && timeoutMs > 0)
        val socket = socketFactory()
        suspendCancellableCoroutine { continuation ->
            continuation.invokeOnCancellation { runCatching { socket.close() } }
            try {
                val started = System.nanoTime()
                socket.use { it.connect(InetSocketAddress(host, port), timeoutMs) }
                val elapsed = (System.nanoTime() - started) / 1_000_000
                if (continuation.isActive) continuation.resume(elapsed)
            } catch (error: Exception) {
                if (continuation.isActive) continuation.resumeWithException(error)
            }
        }
    }
}
