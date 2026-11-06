package app.slipnet.tunnel

import app.slipnet.util.AppLog as Log
import java.io.BufferedOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.io.IOException
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.atomic.AtomicBoolean

/**
 * HTTP proxy server that chains through an existing SOCKS5 proxy.
 * Supports HTTP CONNECT (for HTTPS) and plain HTTP forwarding.
 *
 * This works with ALL tunnel types automatically since it connects
 * upstream via the already-running SOCKS5 proxy using a manual SOCKS5 handshake.
 *
 * Traffic flow:
 * Other device -> HTTP proxy (listenPort) -> SOCKS5 proxy (socksPort) -> tunnel -> server
 */
object HttpProxyServer {
    private const val TAG = "HttpProxyServer"
    @Volatile var debugLogging = false
    @Volatile var uploadLimiter: RateLimiter? = null
    @Volatile var downloadLimiter: RateLimiter? = null
    private fun logd(msg: String) { if (debugLogging) Log.d(TAG, msg) }
    private const val BIND_MAX_RETRIES = 10
    private const val BIND_RETRY_DELAY_MS = 200L
    private const val BUFFER_SIZE = 32768
    private const val TCP_CONNECT_TIMEOUT_MS = 30000


    private var socksHost: String = "127.0.0.1"
    private var socksPort: Int = 1080
    private var clientUsername: String? = null
    private var clientPassword: String? = null
    private var socksAuthUsername: String? = null
    private var socksAuthPassword: String? = null
    private var serverSocket: ServerSocket? = null
    private var acceptorThread: Thread? = null
    private val running = AtomicBoolean(false)
    private val generation = java.util.concurrent.atomic.AtomicLong()
    private val activeSockets = java.util.concurrent.ConcurrentHashMap.newKeySet<Socket>()
    private val connectionThreads = CopyOnWriteArrayList<Thread>()

    @Synchronized
    fun start(
        socksHost: String,
        socksPort: Int,
        listenHost: String = "127.0.0.1",
        listenPort: Int = 8080,
        socksAuthUsername: String? = null,
        socksAuthPassword: String? = null,
        clientUsername: String? = socksAuthUsername,
        clientPassword: String? = socksAuthPassword
    ): Result<Unit> {
        Log.i(TAG, "========================================")
        Log.i(TAG, "Starting HTTP proxy")
        Log.i(TAG, "  Upstream SOCKS5: $socksHost:$socksPort")
        Log.i(TAG, "  Listen: $listenHost:$listenPort")
        Log.i(TAG, "  SOCKS5 auth: ${if (!socksAuthUsername.isNullOrEmpty()) "enabled" else "disabled"}")
        Log.i(TAG, "========================================")

        stop()
        this.socksHost = socksHost
        this.socksPort = socksPort
        this.socksAuthUsername = socksAuthUsername
        this.socksAuthPassword = socksAuthPassword
        this.clientUsername = clientUsername
        this.clientPassword = clientPassword

        return try {
            val ss = bindServerSocket(listenHost, listenPort)
            serverSocket = ss
            val epoch = generation.incrementAndGet()
            running.set(true)

            acceptorThread = Thread({
                logd("Acceptor thread started")
                while (running.get() && !Thread.currentThread().isInterrupted) {
                    try {
                        val clientSocket = ss.accept()
                        handleConnection(clientSocket, epoch)
                    } catch (e: Exception) {
                        if (running.get()) {
                            Log.w(TAG, "Accept error: ${e.message}")
                        }
                    }
                }
                logd("Acceptor thread exited")
            }, "http-proxy-acceptor").also { it.isDaemon = true; it.start() }

            Log.i(TAG, "HTTP proxy started on $listenHost:$listenPort")
            Result.success(Unit)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start HTTP proxy", e)
            stop()
            Result.failure(e)
        }
    }

    @Synchronized
    fun stop() {
        generation.incrementAndGet()
        if (!running.getAndSet(false) && serverSocket == null) {
            return
        }
        logd("Stopping HTTP proxy...")

        try { serverSocket?.close() } catch (_: Exception) {}
        serverSocket = null

        acceptorThread?.interrupt()
        acceptorThread = null

        activeSockets.forEach { runCatching { it.close() } }
        activeSockets.clear()
        for (thread in connectionThreads) {
            thread.interrupt()
        }
        connectionThreads.clear()

        logd("HTTP proxy stopped")
    }

    fun isRunning(): Boolean = running.get()

    fun isHealthy(): Boolean {
        val ss = serverSocket ?: return false
        return running.get() && !ss.isClosed
    }

    private fun bindServerSocket(host: String, port: Int): ServerSocket {
        val ss = ServerSocket()
        ss.reuseAddress = true
        var lastException: java.net.BindException? = null
        repeat(BIND_MAX_RETRIES) { attempt ->
            try {
                ss.bind(InetSocketAddress(host, port))
                if (attempt > 0) Log.i(TAG, "Port $port bound after ${attempt + 1} attempts")
                return ss
            } catch (e: java.net.BindException) {
                lastException = e
                if (attempt < BIND_MAX_RETRIES - 1) {
                    Log.w(TAG, "Port $port in use, retrying in ${BIND_RETRY_DELAY_MS}ms (attempt ${attempt + 1}/$BIND_MAX_RETRIES)")
                    Thread.sleep(BIND_RETRY_DELAY_MS)
                }
            }
        }
        ss.close()
        throw lastException ?: java.net.BindException("Failed to bind to port $port")
    }

    @Synchronized
    private fun register(socket: Socket, epoch: Long): Boolean {
        if (!running.get() || generation.get() != epoch || activeSockets.size >= 256) {
            socket.close()
            return false
        }
        activeSockets.add(socket)
        return true
    }

    private fun handleConnection(clientSocket: Socket, epoch: Long) {
        if (!register(clientSocket, epoch)) return
        val thread = Thread({
            try {
                clientSocket.use { socket ->
                    socket.soTimeout = 30000
                    socket.tcpNoDelay = true
                    val input = socket.getInputStream()

                    // Read the first line of the HTTP request
                    val requestLine = readLine(input) ?: return@Thread
                    logd("Request: $requestLine")

                    val parts = requestLine.split(" ", limit = 3)
                    if (parts.size < 3) {
                        sendError(socket, 400, "Bad Request")
                        return@Thread
                    }

                    val method = parts[0]
                    val target = parts[1]

                    if (method.equals("CONNECT", ignoreCase = true)) {
                        handleConnect(target, socket, input, epoch)
                    } else {
                        handlePlainHttp(requestLine, socket, input, epoch)
                    }
                }
            } catch (e: Exception) {
                if (running.get()) {
                    logd("Connection handler error: ${e.message}")
                }
            }
            finally { activeSockets.remove(clientSocket); activeSockets.removeAll { it.isClosed } }
        }, "http-proxy-handler")
        thread.isDaemon = true
        connectionThreads.add(thread)
        thread.start()

        connectionThreads.removeAll { !it.isAlive }
    }

    /**
     * Handle HTTP CONNECT method (used for HTTPS tunneling).
     * Format: CONNECT host:port HTTP/1.1
     */
    private fun handleConnect(target: String, clientSocket: Socket, clientInput: InputStream, epoch: Long) {
        // Parse host:port from CONNECT target
        val (host, port) = parseHostPort(target, 443) ?: run {
            sendError(clientSocket, 400, "Bad Request")
            return
        }

        var authorization: String? = null
        var headerCount = 0
        while (true) {
            val line = readLine(clientInput) ?: return
            if (line.isEmpty()) break
            check(++headerCount <= 100) { "Too many proxy headers" }
            if (line.startsWith("Proxy-Authorization:", true)) authorization = line.substringAfter(':').trim()
        }
        if (!authenticate(authorization, clientSocket)) return

        // Connect to target through SOCKS5 proxy using manual handshake.
        // Sends DOMAINNAME address type so the bridge resolves DNS remotely —
        // avoids DNS leaks to the local ISP.
        val remoteSocket: Socket
        try {
            remoteSocket = connectViaSocks5(host, port, epoch)
        } catch (e: Exception) {
            logd("CONNECT: failed to connect to $host:$port via SOCKS5: ${e.message}")
            sendError(clientSocket, 502, "Bad Gateway")
            return
        }

        try {
        logd("CONNECT: $host:$port OK")

        // Send 200 Connection Established
        val clientOutput = clientSocket.getOutputStream()
        clientOutput.write("HTTP/1.1 200 Connection Established\r\n\r\n".toByteArray())
        clientOutput.flush()

        // Disable read timeout for relay — matches SOCKS5 bridge behavior.
        // With soTimeout > 0, SocketTimeoutException kills long-lived tunnels
        // (e.g. Telegram MTProto) even when the connection is healthy but idle.
        clientSocket.soTimeout = 0
        clientSocket.keepAlive = true
        remoteSocket.keepAlive = true

        // Bridge bidirectionally
        remoteSocket.use { remote ->
            val remoteInput = remote.getInputStream()
            val remoteOutput = remote.getOutputStream()

            val t1 = Thread({
                try {
                    copyStream(clientInput, remoteOutput, uploadLimiter)
                } catch (_: Exception) {
                } finally {
                    try { remote.close() } catch (_: Exception) {}
                    try { clientSocket.close() } catch (_: Exception) {}
                }
            }, "http-proxy-c2s")
            t1.isDaemon = true
            t1.start()

            try {
                copyStream(remoteInput, clientOutput, downloadLimiter)
            } catch (_: Exception) {
            } finally {
                try { remote.close() } catch (_: Exception) {}
                try { clientSocket.close() } catch (_: Exception) {}
            }
        }
        } finally { runCatching { remoteSocket.close() } }
    }

    /**
     * Handle plain HTTP request (GET, POST, etc).
     * Parse Host header, connect via SOCKS5, rewrite absolute URI to relative, forward.
     */
    private fun handlePlainHttp(requestLine: String, clientSocket: Socket, clientInput: InputStream, epoch: Long) {
        val parts = requestLine.split(" ", limit = 3)
        val method = parts[0]
        val uri = parts[1]
        val httpVersion = parts[2]

        // Read all headers
        var authorization: String? = null
        var headerCount = 0
        val headers = mutableListOf<String>()
        var hostHeader: String? = null
        while (true) {
            val line = readLine(clientInput) ?: return
            if (line.isEmpty()) break
            check(++headerCount <= 100) { "Too many proxy headers" }
            if (line.startsWith("Proxy-Authorization:", true)) authorization = line.substringAfter(':').trim()
            headers.add(line)
            if (line.startsWith("Host:", ignoreCase = true)) {
                hostHeader = line.substringAfter(":").trim()
            }
        }
        if (!authenticate(authorization, clientSocket)) return


        // Determine target host and port from URI or Host header
        val (host, port) = if (uri.startsWith("http://", ignoreCase = true)) {
            // Absolute URI: http://host:port/path
            val urlPart = uri.removePrefix("http://").removePrefix("HTTP://")
            val hostPart = urlPart.substringBefore("/")
            parseHostPort(hostPart, 80) ?: run {
                sendError(clientSocket, 400, "Bad Request")
                return
            }
        } else if (hostHeader != null) {
            parseHostPort(hostHeader, 80) ?: run {
                sendError(clientSocket, 400, "Bad Request")
                return
            }
        } else {
            sendError(clientSocket, 400, "Bad Request")
            return
        }

        // Rewrite absolute URI to relative path
        val relativePath = if (uri.startsWith("http://", ignoreCase = true)) {
            val afterScheme = uri.removePrefix("http://").removePrefix("HTTP://")
            val slashIdx = afterScheme.indexOf('/')
            if (slashIdx >= 0) afterScheme.substring(slashIdx) else "/"
        } else {
            uri
        }

        // Connect through SOCKS5 using manual handshake — avoids DNS leaks
        val remoteSocket: Socket
        try {
            remoteSocket = connectViaSocks5(host, port, epoch)
        } catch (e: Exception) {
            logd("HTTP: failed to connect to $host:$port via SOCKS5: ${e.message}")
            sendError(clientSocket, 502, "Bad Gateway")
            return
        }

        logd("HTTP: $method $host:$port$relativePath")

        try {
            val remoteInput = remoteSocket.getInputStream()
            val remoteOutput = BufferedOutputStream(remoteSocket.getOutputStream(), BUFFER_SIZE)
            val clientOutput = clientSocket.getOutputStream()

            // Send rewritten request line
            remoteOutput.write("$method $relativePath $httpVersion\r\n".toByteArray())

            // Forward headers: strip proxy headers, force Connection: close so server
            // closes after the response (avoids blocking on keep-alive)
            var wroteConnection = false
            for (header in headers) {
                val headerName = header.substringBefore(":").trim().lowercase()
                when (headerName) {
                    "proxy-connection", "proxy-authorization" -> continue
                    "connection" -> {
                        remoteOutput.write("Connection: close\r\n".toByteArray())
                        wroteConnection = true
                    }
                    else -> remoteOutput.write("$header\r\n".toByteArray())
                }
            }
            if (!wroteConnection) {
                remoteOutput.write("Connection: close\r\n".toByteArray())
            }
            remoteOutput.write("\r\n".toByteArray())
            remoteOutput.flush()

            clientSocket.soTimeout = 0
            clientSocket.keepAlive = true
            remoteSocket.keepAlive = true

            // Bidirectional bridge: forwards request body (if any) and response.
            // Server will close after response due to Connection: close.
            val t1 = Thread({
                try {
                    copyStream(clientInput, remoteSocket.getOutputStream(), uploadLimiter)
                } catch (_: Exception) {
                } finally {
                    try { remoteSocket.close() } catch (_: Exception) {}
                    try { clientSocket.close() } catch (_: Exception) {}
                }
            }, "http-proxy-c2s")
            t1.isDaemon = true
            t1.start()

            try {
                copyStream(remoteInput, clientOutput, downloadLimiter)
            } catch (_: Exception) {
            } finally {
                try { remoteSocket.close() } catch (_: Exception) {}
                try { clientSocket.close() } catch (_: Exception) {}
            }
        } catch (e: Exception) {
            if (running.get()) {
                logd("HTTP: proxy error for $host:$port: ${e.message}")
            }
        } finally {
            try { remoteSocket.close() } catch (_: Exception) {}
        }
    }

    /**
     * Parse "host:port" string. Returns (host, port) or null on failure.
     * If no port specified, uses the default.
     */
    private fun parseHostPort(hostPort: String, defaultPort: Int): Pair<String, Int>? {
        // Handle IPv6 addresses like [::1]:port
        if (hostPort.startsWith("[")) {
            val closeBracket = hostPort.indexOf(']')
            if (closeBracket < 0) return null
            val host = hostPort.substring(1, closeBracket)
            val port = if (closeBracket + 1 < hostPort.length && hostPort[closeBracket + 1] == ':') {
                hostPort.substring(closeBracket + 2).toIntOrNull() ?: defaultPort
            } else {
                defaultPort
            }
            return host to port
        }

        val colonIdx = hostPort.lastIndexOf(':')
        return if (colonIdx > 0) {
            val host = hostPort.substring(0, colonIdx)
            val port = hostPort.substring(colonIdx + 1).toIntOrNull() ?: defaultPort
            host to port
        } else {
            hostPort to defaultPort
        }
    }

    /**
     * Read a line from the input stream (terminated by \r\n or \n).
     * Returns null on EOF.
     */
    private fun readLine(input: InputStream): String? {
        val sb = StringBuilder()
        while (true) {
            val b = input.read()
            if (b == -1) return if (sb.isEmpty()) null else sb.toString()
            if (b == '\n'.code) {
                // Remove trailing \r if present
                if (sb.isNotEmpty() && sb[sb.length - 1] == '\r') {
                    sb.deleteCharAt(sb.length - 1)
                }
                return sb.toString()
            }
            check(sb.length < 16 * 1024) { "Proxy header too large" }
            sb.append(b.toChar())
        }
    }

    private fun authenticate(header: String?, socket: Socket): Boolean {
        if (clientUsername == null && clientPassword == null) return true
        return ProxyAuthLimiter.shared.check(socket.inetAddress,
            blocked = { sendError(socket, 429, "Too many proxy authentication attempts") }) {
            authenticateUnchecked(header, socket)
        }
    }

    private fun authenticateUnchecked(header: String?, socket: Socket): Boolean {
        val user = clientUsername
        val pass = clientPassword
        if (user == null && pass == null) return true
        if (user.isNullOrEmpty() || pass.isNullOrEmpty()) { sendError(socket, 503, "Proxy authentication is not configured"); return false }
        val supplied = runCatching {
            if (header?.startsWith("Basic ", true) != true) return@runCatching byteArrayOf()
            android.util.Base64.decode(header.substringAfter(' '), android.util.Base64.NO_WRAP)
        }.getOrDefault(byteArrayOf())
        if (java.security.MessageDigest.isEqual(supplied, "$user:$pass".toByteArray(Charsets.UTF_8))) return true
        socket.getOutputStream().apply {
            write("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Rovunet\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".toByteArray(Charsets.US_ASCII))
            flush()
        }
        return false
    }

    private fun sendError(socket: Socket, code: Int, reason: String) {
        try {
            val body = "$code $reason\r\n"
            val response = "HTTP/1.1 $code $reason\r\nContent-Length: ${body.length}\r\nConnection: close\r\n\r\n$body"
            socket.getOutputStream().write(response.toByteArray())
            socket.getOutputStream().flush()
        } catch (_: Exception) {}
    }

    /**
     * Connect to a target through the SOCKS5 proxy using a manual handshake.
     * Uses DOMAINNAME (0x03) address type so the bridge resolves DNS remotely.
     *
     * This replaces java.net.Proxy(SOCKS) which wraps the socket in SocksSocketImpl
     * and can interfere with socket options, buffering, and relay behavior.
     */
    private fun connectViaSocks5(host: String, port: Int, epoch: Long): Socket {
        val socket = Socket()
        check(register(socket, epoch)) { "Proxy session stopped" }
        try {
        socket.connect(InetSocketAddress(socksHost, socksPort), TCP_CONNECT_TIMEOUT_MS)
        socket.soTimeout = TCP_CONNECT_TIMEOUT_MS // timeout for handshake only
        socket.tcpNoDelay = true

        val out = socket.getOutputStream()
        val inp = socket.getInputStream()

        val hasAuth = !socksAuthUsername.isNullOrEmpty() && !socksAuthPassword.isNullOrEmpty()
        if (hasAuth) {
            // SOCKS5 greeting: version 5, 2 auth methods (no auth + username/password)
            out.write(byteArrayOf(0x05, 0x02, 0x00, 0x02))
        } else {
            // SOCKS5 greeting: version 5, 1 auth method (no auth)
            out.write(byteArrayOf(0x05, 0x01, 0x00))
        }
        out.flush()

        // Server's auth method choice
        val authResp = ByteArray(2)
        readExactly(inp, authResp)
        if (authResp[0] != 0x05.toByte()) {
            socket.close()
            throw IOException("SOCKS5 auth negotiation failed (ver=${authResp[0]})")
        }
        when (authResp[1].toInt() and 0xFF) {
            0x00 -> { /* No auth — proceed */ }
            0x02 -> {
                // RFC 1929 username/password subnegotiation
                val user = socksAuthUsername!!.toByteArray(Charsets.UTF_8)
                val pass = socksAuthPassword!!.toByteArray(Charsets.UTF_8)
                require(user.size in 1..255 && pass.size in 1..255) { "Invalid SOCKS credentials" }
                val authReq = ByteArray(1 + 1 + user.size + 1 + pass.size)
                authReq[0] = 0x01 // subnegotiation version
                authReq[1] = user.size.toByte()
                System.arraycopy(user, 0, authReq, 2, user.size)
                authReq[2 + user.size] = pass.size.toByte()
                System.arraycopy(pass, 0, authReq, 3 + user.size, pass.size)
                out.write(authReq)
                out.flush()

                val authResult = ByteArray(2)
                readExactly(inp, authResult)
                if (authResult[1] != 0x00.toByte()) {
                    socket.close()
                    throw IOException("SOCKS5 username/password auth failed")
                }
            }
            0xFF -> {
                socket.close()
                throw IOException("SOCKS5 no acceptable auth methods")
            }
            else -> {
                socket.close()
                throw IOException("SOCKS5 unexpected auth method=${authResp[1]}")
            }
        }

        // CONNECT request with DOMAINNAME address type
        val domainBytes = host.toByteArray(Charsets.US_ASCII)
        require(domainBytes.size in 1..255 && port in 1..65535) { "Invalid SOCKS target" }
        val req = ByteArray(4 + 1 + domainBytes.size + 2)
        req[0] = 0x05 // version
        req[1] = 0x01 // CONNECT
        req[2] = 0x00 // reserved
        req[3] = 0x03 // DOMAINNAME
        req[4] = domainBytes.size.toByte()
        System.arraycopy(domainBytes, 0, req, 5, domainBytes.size)
        req[req.size - 2] = (port shr 8).toByte()
        req[req.size - 1] = (port and 0xFF).toByte()
        out.write(req)
        out.flush()

        // Read CONNECT response header (VER, REP, RSV, ATYP)
        val resp = ByteArray(4)
        readExactly(inp, resp)
        if (resp[0] != 0x05.toByte() || resp[1] != 0x00.toByte()) {
            socket.close()
            throw IOException("SOCKS5 CONNECT failed (rep=${resp[1]})")
        }

        // Read and discard bound address based on address type
        when (resp[3].toInt() and 0xFF) {
            0x01 -> readExactly(inp, ByteArray(4 + 2))  // IPv4 + port
            0x03 -> {
                val len = inp.read()
                if (len < 0) { socket.close(); throw IOException("SOCKS5: unexpected EOF") }
                readExactly(inp, ByteArray(len + 2))     // domain + port
            }
            0x04 -> readExactly(inp, ByteArray(16 + 2)) // IPv6 + port
            else -> {
                socket.close()
                throw IOException("SOCKS5: unknown address type ${resp[3]}")
            }
        }

        socket.soTimeout = 0 // clear handshake timeout before relay
        socket.keepAlive = true
        return socket
        } catch (e: Exception) {
            activeSockets.remove(socket)
            runCatching { socket.close() }
            throw e
        }
    }

    private fun readExactly(input: InputStream, buf: ByteArray) {
        var off = 0
        while (off < buf.size) {
            val n = input.read(buf, off, buf.size - off)
            if (n < 0) throw IOException("Unexpected end of stream")
            off += n
        }
    }

    private fun copyStream(input: InputStream, output: OutputStream, limiter: RateLimiter? = null) {
        val buffer = ByteArray(BUFFER_SIZE)
        val maxRead = if (limiter != null && limiter.bytesPerSecond > 0) {
            (limiter.bytesPerSecond / 4).toInt().coerceIn(1024, BUFFER_SIZE)
        } else BUFFER_SIZE
        while (!Thread.currentThread().isInterrupted) {
            val bytesRead = input.read(buffer, 0, maxRead)
            if (bytesRead == -1) break
            limiter?.acquire(bytesRead)
            output.write(buffer, 0, bytesRead)
            if (input.available() == 0) {
                output.flush()
            }
        }
        output.flush()
    }

}
