package app.slipnet.tunnel

import java.net.Socket
import java.io.InputStream

/** Shared direct-TCP relay for bridges that previously ignored domestic rules. */
internal object DirectRouting {
    fun relay(router: DomainRouter, host: String, port: Int, client: Socket,
              input: InputStream = client.getInputStream(), initialData: ByteArray = byteArrayOf(),
              socksReply: Boolean = true): Boolean {
        val name = if (initialData.isNotEmpty()) runCatching {
            ProtocolSniffer.sniff(java.io.ByteArrayInputStream(initialData)).domain
        }.getOrNull() else null
        if (!router.shouldBypassDestination(host, name)) return false
        try {
            router.createDirectConnection(host, port).use { remote ->
                if (socksReply) client.getOutputStream().apply {
                    write(byteArrayOf(5,0,0,1,0,0,0,0,0,0)); flush()
                }
                client.soTimeout = 0
                remote.soTimeout = 0
                remote.getOutputStream().apply { write(initialData); flush() }
                val upload = Thread({
                    try { input.copyTo(remote.getOutputStream()) }
                    catch (_: Exception) {} finally { runCatching { remote.close() }; runCatching { client.close() } }
                }, "rovunet-direct-upload").apply { isDaemon=true; start() }
                try { remote.getInputStream().copyTo(client.getOutputStream()) }
                finally { runCatching { remote.close() }; runCatching { client.close() }; upload.interrupt() }
            }
        } catch (_: Exception) {
            if (socksReply) runCatching { client.getOutputStream().apply { write(byteArrayOf(5,4,0,1,0,0,0,0,0,0)); flush() } }
        }
        return true
    }
}
