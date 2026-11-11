package app.slipnet.util

import java.net.InetSocketAddress
import java.nio.ByteBuffer
import java.nio.channels.ClosedSelectorException
import java.nio.channels.DatagramChannel
import java.nio.channels.SelectionKey
import java.nio.channels.Selector
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import kotlin.coroutines.resume

/** Event-driven UDP wait: cancellation wakes the selector; IO owns socket cleanup. */
internal object DnsDatagram {
    suspend fun exchange(host: String, port: Int, query: ByteArray, timeoutMs: Long,
        bufferSize: Int): Pair<ByteArray, Int>? = withContext(Dispatchers.IO) {
        ensureActive()
        suspendCancellableCoroutine { continuation ->
            var channel: DatagramChannel? = null
            var selector: Selector? = null
            try {
                val ready = Selector.open()
                selector = ready
                continuation.invokeOnCancellation {
                    try { ready.wakeup() } catch (_: ClosedSelectorException) { }
                }
                if (!continuation.isActive) return@suspendCancellableCoroutine
                val socket = DatagramChannel.open()
                channel = socket
                socket.configureBlocking(false)
                socket.connect(InetSocketAddress(host, port))
                socket.register(ready, SelectionKey.OP_READ)
                val deadline = System.nanoTime() + timeoutMs.coerceIn(100, 30_000) * 1_000_000
                if (socket.write(ByteBuffer.wrap(query)) != query.size) throw java.io.IOException("DNS query not sent")
                val buffer = ByteBuffer.allocate(bufferSize)
                var response: Pair<ByteArray, Int>? = null
                while (continuation.isActive) {
                    val left = deadline - System.nanoTime()
                    if (left <= 0) break
                    ready.select((left / 1_000_000).coerceAtLeast(1))
                    ready.selectedKeys().clear()
                    if (!continuation.isActive) break
                    val length = socket.read(buffer)
                    if (length > 0) { response = buffer.array() to length; break }
                }
                if (continuation.isActive) continuation.resume(response)
            } catch (_: Exception) {
                if (continuation.isActive) continuation.resume(null)
            } finally {
                try { channel?.close() } catch (_: Exception) { }
                try { selector?.close() } catch (_: Exception) { }
            }
        }
    }
}
