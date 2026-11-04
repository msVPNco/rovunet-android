package app.slipnet.core

import java.io.DataInputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.util.Timer
import java.util.TimerTask
import javax.net.ssl.SSLSocket
import javax.net.ssl.SSLSocketFactory

/** HTTPS through an explicit SOCKS endpoint. Never falls back to a direct connection. */
object TunnelProbe {
    const val HOST = "www.gstatic.com"
    const val PATH = "/generate_204"
    fun measure(port: Int, username: String? = null, password: String? = null,
                host: String = HOST, remotePort: Int = 443, path: String = PATH,
                factory: SSLSocketFactory = SSLSocketFactory.getDefault() as SSLSocketFactory): Long {
        val start = System.nanoTime()
        val socket = Socket()
        val timer = Timer("veyra-probe-deadline", true)
        timer.schedule(object : TimerTask() { override fun run() { runCatching { socket.close() } } }, 8000)
        try {
            socket.connect(InetSocketAddress("127.0.0.1", port), 2000)
            socket.soTimeout = 6000
            val input = DataInputStream(socket.getInputStream())
            val out = socket.getOutputStream()
            val auth = !username.isNullOrEmpty()
            out.write(if (auth) byteArrayOf(5, 2, 0, 2) else byteArrayOf(5, 1, 0)); out.flush()
            check(input.readUnsignedByte() == 5) { "Invalid SOCKS response" }
            when (input.readUnsignedByte()) {
                0 -> Unit
                2 -> {
                    check(auth) { "SOCKS authentication required" }
                    val user = username!!.toByteArray(); val pass = password.orEmpty().toByteArray()
                    check(user.size in 1..255 && pass.size in 1..255) { "Invalid SOCKS credentials" }
                    out.write(byteArrayOf(1, user.size.toByte()) + user + byteArrayOf(pass.size.toByte()) + pass); out.flush()
                    check(input.readUnsignedByte() == 1 && input.readUnsignedByte() == 0) { "SOCKS authentication failed" }
                }
                else -> error("SOCKS authentication rejected")
            }
            val domain = host.toByteArray(Charsets.US_ASCII)
            require(domain.size in 1..255)
            out.write(byteArrayOf(5, 1, 0, 3, domain.size.toByte()) + domain + byteArrayOf((remotePort shr 8).toByte(), remotePort.toByte())); out.flush()
            check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 0) { "Tunnel CONNECT failed" }
            check(input.readUnsignedByte() == 0)
            val size = when (input.readUnsignedByte()) { 1 -> 4; 4 -> 16; 3 -> input.readUnsignedByte(); else -> error("Invalid SOCKS address") }
            input.readFully(ByteArray(size + 2))
            (factory.createSocket(socket, host, remotePort, true) as SSLSocket).use { tls ->
                tls.sslParameters = tls.sslParameters.apply { endpointIdentificationAlgorithm = "HTTPS" }
                tls.soTimeout = 6000
                tls.startHandshake()
                tls.outputStream.write("GET $path HTTP/1.1\r\nHost: $host\r\nConnection: close\r\nCache-Control: no-cache\r\n\r\n".toByteArray(Charsets.US_ASCII))
                tls.outputStream.flush()
                val line = StringBuilder()
                while (line.length < 1024) {
                    val b = tls.inputStream.read(); check(b >= 0) { "Empty HTTPS response" }
                    if (b == 10) break
                    line.append(b.toChar())
                }
                check(Regex("HTTP/1\\.[01] 204(?: .*)?\\r?").matches(line.toString())) { Regex("HTTP/1\\.[01] ([0-9]{3})").find(line.toString())?.groupValues?.get(1)?.let { "Unexpected HTTPS status: $it" } ?: "Unexpected HTTPS response" }
                return (System.nanoTime() - start) / 1_000_000
            }
        } finally { timer.cancel(); runCatching { socket.close() } }
    }
}

data class ProbeSummary(val medianMs: Long, val jitterMs: Long, val successes: Int, val attempts: Int) {
    companion object {
        fun of(samples: List<Long?>): ProbeSummary? {
            val good = samples.filterNotNull().sorted()
            if (good.isEmpty()) return null
            val median = if (good.size % 2 == 1) good[good.size / 2] else good[good.size / 2 - 1] + (good[good.size / 2] - good[good.size / 2 - 1]) / 2
            return ProbeSummary(median, good.last() - good.first(), good.size, samples.size)
        }
    }
}
