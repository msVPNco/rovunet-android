package app.slipnet.tunnel

import app.slipnet.util.AppLog as Log
import com.jcraft.jsch.SocketFactory
import java.io.InputStream
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.Socket
import javax.net.ssl.SNIHostName
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLParameters
import javax.net.ssl.SSLSocket
import javax.net.ssl.SSLSocketFactory
import javax.net.ssl.TrustManager

/**
 * JSch [SocketFactory] that wraps TCP connections in TLS with a configurable SNI hostname.
 *
 * Used for SSH-over-TLS (stunnel-style): the client opens a TLS tunnel to the server,
 * then runs the SSH protocol inside the encrypted TLS channel. This disguises SSH traffic
 * as ordinary HTTPS and allows custom SNI for domain fronting or firewall bypass.
 *
 * @param sniHost The hostname to send in the TLS ClientHello SNI extension.
 * @param connectTimeoutMs TCP connect timeout in milliseconds.
 */
class TlsSocketFactory(
    private val sniHost: String,
    private val connectTimeoutMs: Int = 30_000
) : SocketFactory {

    private val TAG = "TlsSocketFactory"

    override fun createSocket(host: String, port: Int): Socket {
        Log.i(TAG, "Creating TLS socket to $host:$port (SNI: $sniHost)")

        // 1. Open a plain TCP socket
        val rawSocket = Socket()
        rawSocket.connect(InetSocketAddress(host, port), connectTimeoutMs)

        // 2. Wrap in TLS with custom SNI (validate the certificate, with explicit approval for self-signed servers)
        val sslFactory = TransportTrust.tlsFactory(sniHost, port)
        val sslSocket = sslFactory.createSocket(
            rawSocket, sniHost, port, true  // autoClose = true
        ) as SSLSocket

        val params = sslSocket.sslParameters
        params.serverNames = listOf(SNIHostName(sniHost))
        params.endpointIdentificationAlgorithm = "HTTPS"
        sslSocket.sslParameters = params

        // 3. Perform TLS handshake
        sslSocket.soTimeout = 15_000
            try { sslSocket.startHandshake() } catch (e: Exception) { sslSocket.close(); throw e }
            sslSocket.soTimeout = 0
        Log.i(TAG, "TLS handshake complete (protocol: ${sslSocket.session.protocol}, cipher: ${sslSocket.session.cipherSuite})")

        return sslSocket
    }



    override fun getInputStream(socket: Socket): InputStream = socket.getInputStream()

    override fun getOutputStream(socket: Socket): OutputStream = socket.getOutputStream()
}
