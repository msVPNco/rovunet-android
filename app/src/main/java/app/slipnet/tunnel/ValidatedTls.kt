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
            var privateCertificate = false
            val tls = TransportTrust.tlsFactory(host, port) { privateCertificate = true }.createSocket(raw, host, port, true) as SSLSocket
            tls.sslParameters = tls.sslParameters.apply {
                if (!host.contains(':') && !host.matches(Regex("[0-9.]+"))) serverNames = listOf(SNIHostName(host))
                endpointIdentificationAlgorithm = "HTTPS"
            }
            tls.soTimeout = deadline
            try { tls.startHandshake() } catch (e: Exception) { runCatching { tls.close() }; throw e }
            try {
                check(matchesHostname(host, tls.session.peerCertificates.first() as java.security.cert.X509Certificate)) { "TLS hostname mismatch" }
                TransportTrust.rememberTlsCertificate(host, port, tls.session.peerCertificates
                    .map { it as java.security.cert.X509Certificate }.toTypedArray(), privateCertificate)
            } catch (e: Exception) { runCatching { tls.close() }; throw e }
            tls.soTimeout = 0
            return tls
        } catch (e: Exception) { runCatching { raw.close() }; throw e }
        finally { timer.cancel(false) }
    }

    private fun matchesHostname(host: String, certificate: java.security.cert.X509Certificate): Boolean = runCatching {
        val name = host.removeSurrounding("[", "]")
        val ip = name.contains(':') || name.matches(Regex("[0-9.]+"))
        val names = certificate.subjectAlternativeNames.orEmpty()
        if (ip) {
            val address = java.net.InetAddress.getByName(name).address
            names.any { san -> san.size >= 2 && san[0] == 7 && runCatching {
                val candidate = san[1]
                val bytes = if (candidate is ByteArray) candidate else {
                    val text = candidate as String
                    require(text.contains(':') || text.matches(Regex("[0-9.]+")))
                    java.net.InetAddress.getByName(text).address
                }
                address.contentEquals(bytes)
            }.getOrDefault(false) }
        } else {
            val ascii = java.net.IDN.toASCII(name, java.net.IDN.USE_STD3_ASCII_RULES).trimEnd('.').lowercase(java.util.Locale.ROOT)
            require(ascii.isNotEmpty())
            names.any { san ->
                if (san.size < 2 || san[0] != 2 || san[1] !is String) false
                else {
                    val pattern = (san[1] as String).trimEnd('.').lowercase(java.util.Locale.ROOT)
                    if (pattern.startsWith("*.") && pattern.count { it == '*' } == 1) {
                        val suffix = pattern.substring(1)
                        ascii.endsWith(suffix) && ascii.removeSuffix(suffix).let { it.isNotEmpty() && '.' !in it }
                    } else '*' !in pattern && ascii == pattern
                }
            }
        }
    }.getOrDefault(false)
}
