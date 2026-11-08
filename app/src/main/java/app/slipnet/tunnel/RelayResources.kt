package app.slipnet.tunnel

import java.io.IOException
import java.net.Socket
import java.util.concurrent.ExecutorService

/** Per-start owner: late workers cannot attach resources to a replacement connection. */
internal class RelayResources {
    private var closed = false
    private val sockets = mutableSetOf<Socket>()
    private val executors = mutableSetOf<ExecutorService>()
    @Synchronized fun own(socket: Socket): Socket {
        if (closed) { runCatching { socket.close() }; throw IOException("Relay stopped") }
        sockets.add(socket)
        return socket
    }
    @Synchronized fun own(executor: ExecutorService): ExecutorService {
        if (closed) { executor.shutdownNow(); throw IOException("Relay stopped") }
        executors.add(executor)
        return executor
    }
    @Synchronized fun close(socket: Socket) { sockets.remove(socket); runCatching { socket.close() } }
    @Synchronized fun close(executor: ExecutorService) { executors.remove(executor); executor.shutdownNow() }
    @Synchronized fun close() {
        closed = true
        sockets.forEach { runCatching { it.close() } }
        executors.forEach { it.shutdownNow() }
        sockets.clear()
        executors.clear()
    }
    @Synchronized fun remaining(): Int = sockets.size + executors.size
}
