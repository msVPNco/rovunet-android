package app.slipnet.service

import java.io.Closeable
import java.io.DataInputStream
import java.io.IOException
import java.net.InetAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.Executors

/** Local SOCKS5 peer. Only the test destination can receive the payload. */
internal class MtuSocksFixture : Closeable {
    val payload = "rovunet-mtu-".repeat(400)
    private val listener = ServerSocket(0, 8, InetAddress.getByName("127.0.0.1"))
    private val worker = Executors.newSingleThreadExecutor()
    val port get() = listener.localPort

    init {
        worker.execute {
            while (!listener.isClosed) {
                val client = try { listener.accept() } catch (_: IOException) { break }
                client.use {
                    try { serve(it) } catch (_: IOException) { /* other VPN traffic can close early */ }
                }
            }
        }
    }

    private fun serve(socket: Socket) {
        socket.soTimeout = 5000
        val input = DataInputStream(socket.getInputStream())
        val output = socket.getOutputStream()
        if (input.readUnsignedByte() != 5) return
        val methods = ByteArray(input.readUnsignedByte()).also(input::readFully)
        if (0.toByte() !in methods) return
        output.write(byteArrayOf(5, 0))
        val header = ByteArray(4).also(input::readFully)
        if (header[0].toInt() != 5 || header[1].toInt() != 1) return
        val host = when (header[3].toInt()) {
            1 -> InetAddress.getByAddress(ByteArray(4).also(input::readFully)).hostAddress
            3 -> ByteArray(input.readUnsignedByte()).also(input::readFully).toString(Charsets.US_ASCII)
            else -> return
        }
        if (host != "198.18.0.13" || input.readUnsignedShort() != 80) return
        output.write(byteArrayOf(5, 0, 0, 1, 127, 0, 0, 1, 0, 80))
        val reader = input.bufferedReader(Charsets.US_ASCII)
        if (reader.readLine() != "GET /mtu HTTP/1.0") return
        while (!reader.readLine().isNullOrEmpty()) { /* consume request headers */ }
        output.write("HTTP/1.0 200 OK\r\nContent-Length: ${payload.length}\r\n\r\n$payload".toByteArray(Charsets.US_ASCII))
    }

    override fun close() {
        listener.close()
        worker.shutdown()
    }
}
