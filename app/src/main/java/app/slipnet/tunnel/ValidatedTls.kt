package app.slipnet.tunnel

import java.net.Socket
import java.util.Timer
import java.util.TimerTask
import javax.net.ssl.SNIHostName
import javax.net.ssl.SSLSocket

/** Bounded TLS setup shared by all SSH transports. Live streams have no idle read deadline. */
object ValidatedTls {
    fun wrap(raw: Socket, host: String, port: Int, timeoutMs: Int = 15_000): SSLSocket {
        val deadline = if (TransportTrust.allowPrompt) maxOf(timeoutMs, 60_000) else timeoutMs.coerceIn(1000, 15_000)
        val timer = Timer("rovunet-tls-deadline", true)
        timer.schedule(object : TimerTask() { override fun run() { runCatching { raw.close() } } }, deadline.toLong())
        try {
            val tls = TransportTrust.tlsFactory(host, port).createSocket(raw, host, port, true) as SSLSocket
            tls.sslParameters = tls.sslParameters.apply {
                if (!host.contains(':') && !host.matches(Regex("[0-9.]+"))) serverNames = listOf(SNIHostName(host))
                endpointIdentificationAlgorithm = "HTTPS"
            }
            tls.soTimeout = deadline
            try { tls.startHandshake() } catch (e: Exception) { runCatching { tls.close() }; throw e }
            tls.soTimeout = 0
            return tls
        } catch (e: Exception) { runCatching { raw.close() }; throw e }
        finally { timer.cancel() }
    }
}
