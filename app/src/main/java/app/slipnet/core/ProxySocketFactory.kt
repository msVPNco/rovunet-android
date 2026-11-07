package app.slipnet.core

import java.io.DataInputStream
import java.io.IOException
import java.net.InetAddress
import java.net.InetSocketAddress
import java.net.Socket
import java.net.SocketAddress
import javax.net.SocketFactory

/** Per-client SOCKS authentication. Never changes the process Authenticator or connects directly. */
class ProxySocketFactory(private val endpoint: LocalSocksEndpoint) : SocketFactory() {
    init { require((endpoint.user == null) == (endpoint.password == null)) { "Incomplete proxy credentials" } }
    private inner class ProxySocket : Socket() {
        override fun connect(address: SocketAddress, timeout: Int) {
            val target = address as? InetSocketAddress ?: throw IOException("Invalid proxy target")
            val previousTimeout = soTimeout
            try {
                super.connect(InetSocketAddress("127.0.0.1", endpoint.port), timeout)
                soTimeout = if (timeout > 0) timeout else 15_000
                val input = DataInputStream(getInputStream())
                val out = getOutputStream()
                val auth = endpoint.user != null && endpoint.password != null
                out.write(byteArrayOf(5, 1, if (auth) 2 else 0)); out.flush()
                if (input.readUnsignedByte() != 5 || input.readUnsignedByte() != if (auth) 2 else 0) throw IOException("SOCKS_METHOD")
                if (auth) {
                    val user = endpoint.user.orEmpty().toByteArray(Charsets.UTF_8)
                    val password = endpoint.password.orEmpty().toByteArray(Charsets.UTF_8)
                    if (user.size !in 1..255 || password.size !in 1..255) throw IOException("SOCKS_AUTH_LENGTH")
                    out.write(byteArrayOf(1, user.size.toByte()) + user + byteArrayOf(password.size.toByte()) + password); out.flush()
                    if (input.readUnsignedByte() != 1 || input.readUnsignedByte() != 0) throw IOException("SOCKS_AUTH")
                }
                val host = target.hostString
                val encoded = if (host.contains(':') || host.matches(Regex("[0-9.]+"))) {
                    val bytes = InetAddress.getByName(host).address // Numeric literals only; no local DNS lookup.
                    byteArrayOf(if (bytes.size == 4) 1 else 4) + bytes
                } else {
                    val bytes = host.toByteArray(Charsets.UTF_8)
                    if (bytes.size !in 1..255) throw IOException("SOCKS_TARGET_LENGTH")
                    byteArrayOf(3, bytes.size.toByte()) + bytes
                }
                out.write(byteArrayOf(5, 1, 0) + encoded + byteArrayOf((target.port shr 8).toByte(), target.port.toByte())); out.flush()
                if (input.readUnsignedByte() != 5) throw IOException("SOCKS_VERSION")
                val reply = input.readUnsignedByte()
                if (reply != 0) throw IOException("SOCKS_REP_$reply")
                if (input.readUnsignedByte() != 0) throw IOException("SOCKS_RESERVED")
                val length = when (input.readUnsignedByte()) {
                    1 -> 4
                    3 -> input.readUnsignedByte()
                    4 -> 16
                    else -> throw IOException("SOCKS_ADDRESS")
                }
                input.readFully(ByteArray(length)); input.readUnsignedShort()
                soTimeout = previousTimeout
            } catch (e: Exception) {
                runCatching { close() }
                if (e is IOException) throw e
                throw IOException("SOCKS_FAILED", e)
            }
        }
    }
    override fun createSocket(): Socket = ProxySocket()
    override fun createSocket(host: String, port: Int): Socket = createSocket().apply { connect(InetSocketAddress.createUnresolved(host, port), 10_000) }
    override fun createSocket(host: InetAddress, port: Int): Socket = createSocket().apply { connect(InetSocketAddress(host, port), 10_000) }
    override fun createSocket(host: String, port: Int, local: InetAddress?, localPort: Int): Socket = createSocket().apply {
        bind(InetSocketAddress(local, localPort)); connect(InetSocketAddress.createUnresolved(host, port), 10_000)
    }
    override fun createSocket(host: InetAddress, port: Int, local: InetAddress?, localPort: Int): Socket = createSocket().apply {
        bind(InetSocketAddress(local, localPort)); connect(InetSocketAddress(host, port), 10_000)
    }
}
