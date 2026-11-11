package app.slipnet.tunnel

import java.net.Socket
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import javax.net.ssl.SNIHostName
import javax.net.ssl.SSLSocket

/** Bounded TLS setup shared by all SSH transports. Live streams have no idle read deadline. */
object ValidatedTls {
    private val deadlines = Executors.newSingleThreadScheduledExecutor { task ->
        Thread(task, "rovunet-tls-deadlines").apply { isDaemon = true }
    }
    fun wrap(raw: Socket, host: String, port: Int, timeoutMs: Int = 15_000): SSLSocket {
        val deadline = timeoutMs.coerceIn(1000, 15_000)
        val started = System.nanoTime()
        val previouslyPaused = TransportTrust.decisionWaitNanos
        val timer = deadlines.scheduleAtFixedRate({
            val elapsed = System.nanoTime() - started - (TransportTrust.decisionWaitNanos - previouslyPaused)
            if (elapsed >= TimeUnit.MILLISECONDS.toNanos(deadline.toLong())) runCatching { raw.close() }
        }, 100, 100, TimeUnit.MILLISECONDS)
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
        finally { timer.cancel(false) }
    }
}
