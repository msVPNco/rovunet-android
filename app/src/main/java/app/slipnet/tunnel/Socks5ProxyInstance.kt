package app.slipnet.tunnel

import app.slipnet.util.AppLog as Log
import java.io.BufferedOutputStream
import java.io.ByteArrayInputStream
import java.io.InputStream
import java.io.OutputStream
import java.io.SequenceInputStream
import java.net.InetSocketAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executors
import java.util.concurrent.ScheduledThreadPoolExecutor
import java.util.concurrent.ScheduledFuture
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/**
 * SOCKS5 bridge for remote SOCKS5 proxy tunnel type.
 *
 * Sits between hev-socks5-tunnel and a remote SOCKS5 proxy server:
 * - CONNECT (0x01): chains to remote SOCKS5 proxy (with optional username/password auth)
 * - FWD_UDP (0x05) DNS (port 53): DNS-over-TCP through remote SOCKS5 CONNECT to configurable DNS
 * - FWD_UDP (0x05) non-DNS: dropped silently (browser falls back to TCP CONNECT)
 *
 * DNS optimization:
 * - Concurrent: 8-thread pool for parallel DNS resolution
 * - Cached: 60s TTL cache avoids repeated queries for the same domain
 *
 * Traffic flow:
 * App -> TUN -> hev-socks5-tunnel -> Socks5ProxyInstance (listenPort)
 *   TCP: -> SOCKS5 CONNECT (optional auth) -> Remote SOCKS5 Proxy -> Internet
 *   DNS: -> FWD_UDP -> DNS-over-TCP via Remote SOCKS5 CONNECT -> DNS server
 */
class Socks5ProxyInstance(val instanceId: String = "default") {
    private val TAG = "Socks5Proxy[$instanceId]"
    var debugLogging = false
    var domainRouter: DomainRouter = DomainRouter.DISABLED
    var uploadLimiter: RateLimiter? = null
    var downloadLimiter: RateLimiter? = null
    private fun logd(msg: String) { if (debugLogging) Log.d(TAG, msg) }

    companion object {
        private const val BIND_MAX_RETRIES = 10
        private const val BIND_RETRY_DELAY_MS = 200L
        private const val BUFFER_SIZE = 32768
        private const val TCP_CONNECT_TIMEOUT_MS = 30000
        private const val DNS_TIMEOUT_MS = 15000
        private const val DNS_CACHE_TTL_MS = 60_000L
        private val dnsDeadlines = ScheduledThreadPoolExecutor(1) { task ->
            Thread(task, "socks-dns-deadline").apply { isDaemon = true }
        }.apply { removeOnCancelPolicy = true }
    }

    private var remoteHost: String = ""
    private var remotePort: Int = 0
    private var remoteUsername: String? = null
    private var remotePassword: String? = null
    private var dnsHost: String = "8.8.8.8"
    private var dnsFallback: String? = null
    private var localAuthUsername: String? = null
    private var localAuthPassword: String? = null
    private var serverSocket: ServerSocket? = null
    private var acceptorThread: Thread? = null
    private val running = AtomicBoolean(false)
    private val connectionThreads = CopyOnWriteArrayList<Thread>()
    private val activeSockets = ConcurrentHashMap.newKeySet<Socket>()
    private val runGeneration = AtomicLong(0)
    @Volatile private var diagnosticRun: Pair<Long, Long> = 0L to 0L
    private fun trace(event: app.slipnet.util.ConnectionDiagnostics.Event, runId: Long, code: Int? = null,
        failure: app.slipnet.util.ConnectionDiagnostics.Failure = app.slipnet.util.ConnectionDiagnostics.Failure.NONE) {
        val snapshot = diagnosticRun
        if (snapshot.first == runId && running.get() && runGeneration.get() == runId)
            app.slipnet.util.ConnectionDiagnostics.flow(event, snapshot.second, code, failure)
    }
    private fun track(socket: Socket, runId: Long): Boolean = synchronized(activeSockets) {
        if (!running.get() || runGeneration.get() != runId) { runCatching { socket.close() }; false }
        else { activeSockets.add(socket); true }
    }
    private var dnsExecutor = Executors.newFixedThreadPool(8)
    private val tunnelTxBytes = AtomicLong(0)  // client -> tunnel (upload), current session
    private val tunnelRxBytes = AtomicLong(0)  // tunnel -> client (download), current session
    private val carriedTxBytes = AtomicLong(0)  // accumulated from previous sessions
    private val carriedRxBytes = AtomicLong(0)

    // DNS cache: query key (hex of query minus transaction ID) -> (response, expiry)
    private data class DnsCacheEntry(val response: ByteArray, val expiresAt: Long)
    private val dnsCache = ConcurrentHashMap<String, DnsCacheEntry>()

    @Synchronized
    fun start(
        remoteHost: String,
        remotePort: Int,
        remoteUsername: String?,
        remotePassword: String?,
        listenPort: Int,
        listenHost: String = "127.0.0.1",
        dnsHost: String = "8.8.8.8",
        localAuthUsername: String? = null,
        localAuthPassword: String? = null,
        dnsFallback: String? = null
    ): Result<Unit> {
        Log.i(TAG, "========================================")
        Log.i(TAG, "Starting SOCKS5 proxy bridge")
        Log.i(TAG, "  Remote SOCKS5: $remoteHost:$remotePort")
        Log.i(TAG, "  Auth: ${if (remoteUsername != null) "username/password" else "none"}")
        Log.i(TAG, "  Listen: $listenHost:$listenPort")
        Log.i(TAG, "  Local auth: ${if (!localAuthUsername.isNullOrEmpty()) "enabled" else "disabled"}")
        Log.i(TAG, "  DNS: $dnsHost")
        Log.i(TAG, "========================================")

        stop()
        this.remoteHost = remoteHost
        this.remotePort = remotePort
        this.remoteUsername = remoteUsername
        this.remotePassword = remotePassword
        this.dnsHost = dnsHost
        this.dnsFallback = dnsFallback
        this.localAuthUsername = localAuthUsername
        this.localAuthPassword = localAuthPassword

        return try {
            val ss = bindServerSocket(listenHost, listenPort)
            serverSocket = ss
            val runId = runGeneration.incrementAndGet()
            diagnosticRun = runId to app.slipnet.util.ConnectionDiagnostics.currentSessionId()
            running.set(true)

            acceptorThread = Thread({
                logd("Acceptor thread started")
                while (running.get() && runGeneration.get() == runId && !Thread.currentThread().isInterrupted) {
                    try {
                        val clientSocket = ss.accept()
                        handleConnection(clientSocket, runId)
                    } catch (e: Exception) {
                        if (running.get()) {
                            Log.w(TAG, "Accept error: ${e.message}")
                        }
                    }
                }
                logd("Acceptor thread exited")
            }, "socks5-bridge-acceptor-$instanceId").also { it.isDaemon = true; it.start() }

            Log.i(TAG, "Bridge started on $listenHost:$listenPort")
            Result.success(Unit)
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start bridge", e)
            stop()
            Result.failure(e)
        }
    }

    @Synchronized
    fun stop() {
        runGeneration.incrementAndGet()
        if (!running.getAndSet(false) && serverSocket == null) {
            return
        }
        logd("Stopping bridge...")

        try { serverSocket?.close() } catch (_: Exception) {}
        serverSocket = null
        synchronized(activeSockets) {
            activeSockets.forEach { runCatching { it.close() } }
            activeSockets.clear()
        }

        // Wait for acceptor thread to fully exit so the port is released
        // before a new start() tries to bind the same port.
        val oldAcceptor = acceptorThread
        acceptorThread = null
        oldAcceptor?.interrupt()
        try { oldAcceptor?.join(2000) } catch (_: InterruptedException) {
            Thread.currentThread().interrupt()
        }

        for (thread in connectionThreads) {
            thread.interrupt()
        }
        connectionThreads.clear()

        dnsExecutor.shutdownNow()
        dnsExecutor = Executors.newFixedThreadPool(8)

        dnsCache.clear()

        carriedTxBytes.addAndGet(tunnelTxBytes.getAndSet(0))
        carriedRxBytes.addAndGet(tunnelRxBytes.getAndSet(0))

        logd("Bridge stopped")
    }

    fun isRunning(): Boolean = running.get()

    fun isClientHealthy(): Boolean {
        val ss = serverSocket ?: return false
        return running.get() && !ss.isClosed
    }

    fun getTunnelTxBytes(): Long = carriedTxBytes.get() + tunnelTxBytes.get()
    fun getTunnelRxBytes(): Long = carriedRxBytes.get() + tunnelRxBytes.get()

    fun resetTrafficStats() {
        tunnelTxBytes.set(0)
        tunnelRxBytes.set(0)
        carriedTxBytes.set(0)
        carriedRxBytes.set(0)
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

    private fun handleConnection(clientSocket: Socket, runId: Long) {
        if (!track(clientSocket, runId)) return
        val thread = Thread({
            try {
                clientSocket.use { socket ->
                    socket.soTimeout = 30000
                    socket.tcpNoDelay = true
                    val input = socket.getInputStream()
                    val output = socket.getOutputStream()

                    // SOCKS5 greeting
                    val version = input.read()
                    if (version == -1) {
                        // EOF — readiness probe or client closed immediately; not an error
                        return@Thread
                    }
                    if (version != 0x05) {
                        Log.w(TAG, "Invalid SOCKS5 version: $version")
                        return@Thread
                    }
                    val nMethods = input.read()
                    val methods = ByteArray(nMethods)
                    input.readFully(methods)

                    // Authenticate local client
                    if (!LocalProxyAuth.handleGreeting(methods, input, output, localAuthUsername, localAuthPassword, socket.inetAddress)) {
                        return@Thread
                    }

                    // SOCKS5 request
                    val ver = input.read()
                    val cmd = input.read()
                    input.read() // reserved

                    if (ver != 0x05 || (cmd != 0x01 && cmd != 0x05)) {
                        output.write(byteArrayOf(0x05, 0x07, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                        output.flush()
                        return@Thread
                    }

                    // Parse address
                    val addrType = input.read()
                    val destHost: String
                    val rawAddr: ByteArray

                    when (addrType) {
                        0x01 -> { // IPv4
                            val addr = ByteArray(4)
                            input.readFully(addr)
                            destHost = addr.joinToString(".") { (it.toInt() and 0xFF).toString() }
                            rawAddr = byteArrayOf(0x01) + addr
                        }
                        0x03 -> { // Domain name
                            val len = input.read()
                            val domain = ByteArray(len)
                            input.readFully(domain)
                            destHost = String(domain)
                            rawAddr = byteArrayOf(0x03, len.toByte()) + domain
                        }
                        0x04 -> { // IPv6
                            val addr = ByteArray(16)
                            input.readFully(addr)
                            destHost = formatIpv6(addr)
                            rawAddr = byteArrayOf(0x04) + addr
                        }
                        else -> {
                            output.write(byteArrayOf(0x05, 0x08, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                            output.flush()
                            return@Thread
                        }
                    }

                    val portHigh = input.read()
                    val portLow = input.read()
                    val destPort = (portHigh shl 8) or portLow
                    val portBytes = byteArrayOf(portHigh.toByte(), portLow.toByte())

                    // Handle FWD_UDP (cmd 0x05)
                    if (cmd == 0x05) {
                        socket.soTimeout = 0
                        handleFwdUdp(input, output, runId)
                        return@Thread
                    }

                    // Handle CONNECT (cmd 0x01) — chain through remote SOCKS5 proxy
                    handleConnect(destHost, destPort, rawAddr, portBytes, socket, input, output, runId)
                }
            } catch (e: Exception) {
                if (running.get()) {
                    logd("Connection handler error: ${e.message}")
                }
            } finally {
                activeSockets.remove(clientSocket)
            }
        }, "socks5-bridge-handler-$instanceId")
        thread.isDaemon = true
        connectionThreads.add(thread)
        thread.start()

        connectionThreads.removeAll { !it.isAlive }
    }

    /**
     * Handle SOCKS5 CONNECT by chaining through the remote SOCKS5 proxy.
     * With domain routing: sniffs TLS SNI / HTTP Host to decide bypass vs tunnel.
     */
    private fun handleConnect(
        destHost: String,
        destPort: Int,
        rawAddr: ByteArray,
        portBytes: ByteArray,
        clientSocket: Socket,
        clientInput: InputStream,
        clientOutput: OutputStream,
        runId: Long
    ) {
        if (!running.get() || runGeneration.get() != runId) return
        val router = domainRouter
        if (router.enabled) {
            handleConnectWithRouting(router, destHost, destPort, rawAddr, portBytes, clientSocket, clientInput, clientOutput, runId)
            return
        }

        // Original flow — no domain routing
        connectViaProxy(destHost, destPort, rawAddr, portBytes, clientSocket, clientInput, clientOutput, sendReply = true, runId = runId)
    }

    private fun handleConnectWithRouting(
        router: DomainRouter,
        destHost: String,
        destPort: Int,
        rawAddr: ByteArray,
        portBytes: ByteArray,
        clientSocket: Socket,
        clientInput: InputStream,
        clientOutput: OutputStream,
        runId: Long
    ) {
        var effectiveHost = destHost
        var sniffBuffer: ByteArray? = null
        var sniffLen = 0
        var wasEarlyReply = false

        if (DomainRouter.isIpAddress(destHost)) {
            // IP address — sniff to recover domain
            clientOutput.write(byteArrayOf(0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
            clientOutput.flush()
            clientSocket.soTimeout = 3000
            wasEarlyReply = true

            val result = ProtocolSniffer.sniff(clientInput)
            if (result.domain != null) {
                effectiveHost = result.domain
                logd("CONNECT: sniffed domain=$effectiveHost from IP=$destHost")
            }
            sniffBuffer = result.bufferedData
            sniffLen = result.bufferedLength
        }

        if (router.shouldBypass(effectiveHost)) {
            // Direct connection — bypass tunnel
            logd("CONNECT: bypassing tunnel for $effectiveHost:$destPort")
            try {
                val directSocket = router.createDirectConnection(destHost, destPort)
                if (!track(directSocket, runId)) return
                try {
                    if (!wasEarlyReply) {
                        clientOutput.write(byteArrayOf(0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                        clientOutput.flush()
                    }
                    clientSocket.soTimeout = 0
                    val effectiveInput = if (sniffLen > 0)
                        SequenceInputStream(ByteArrayInputStream(sniffBuffer!!, 0, sniffLen), clientInput)
                    else clientInput
                    bridgeDirect(effectiveInput, clientOutput, directSocket)
                } finally {
                    activeSockets.remove(directSocket)
                    runCatching { directSocket.close() }
                }
            } catch (e: Exception) {
                logd("CONNECT: direct connection failed for $effectiveHost:$destPort: ${e.message}")
                if (!wasEarlyReply) {
                    try {
                        clientOutput.write(byteArrayOf(0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                        clientOutput.flush()
                    } catch (_: Exception) {}
                }
            }
            return
        }

        // Tunnel path
        clientSocket.soTimeout = 0
        val effectiveInput = if (sniffLen > 0)
            SequenceInputStream(ByteArrayInputStream(sniffBuffer!!, 0, sniffLen), clientInput)
        else clientInput
        connectViaProxy(destHost, destPort, rawAddr, portBytes, clientSocket, effectiveInput, clientOutput, sendReply = !wasEarlyReply, runId = runId)
    }

    /**
     * Perform SOCKS5 handshake with the remote proxy, including optional
     * username/password authentication (RFC 1929).
     *
     * @return true if handshake succeeded, false otherwise
     */
    private fun performRemoteSocks5Handshake(
        remoteInput: InputStream,
        remoteOutput: OutputStream,
        sendClientReply: Boolean,
        clientOutput: OutputStream,
        runId: Long
    ): Boolean {
        val username = remoteUsername
        val password = remotePassword
        val hasAuth = username != null && password != null

        // SOCKS5 greeting — offer methods
        if (hasAuth) {
            // Offer NO_AUTH (0x00) and USERNAME/PASSWORD (0x02)
            remoteOutput.write(byteArrayOf(0x05, 0x02, 0x00, 0x02))
        } else {
            // Offer NO_AUTH only
            remoteOutput.write(byteArrayOf(0x05, 0x01, 0x00))
        }
        remoteOutput.flush()

        val greetResp = ByteArray(2)
        trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_GREETING_WAIT, runId)
        try {
            remoteInput.readFully(greetResp)
        } catch (e: Exception) {
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_GREETING_FAILED, runId,
                failure = app.slipnet.util.ConnectionDiagnostics.failure(e))
            throw e
        }
        if (greetResp[0] != 0x05.toByte()) {
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_GREETING, runId,
                failure = app.slipnet.util.ConnectionDiagnostics.Failure.OTHER)
            Log.w(TAG, "Remote proxy rejected greeting (ver=${greetResp[0]})")
            if (sendClientReply) {
                clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                clientOutput.flush()
            }
            return false
        }

        val selectedMethod = greetResp[1].toInt() and 0xFF
        trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_GREETING, runId, selectedMethod)

        when (selectedMethod) {
            0x00 -> {
                // No authentication required — proceed
                logd("Remote proxy selected NO_AUTH")
            }
            0x02 -> {
                // Username/password authentication (RFC 1929)
                if (!hasAuth) {
                    trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_AUTH, runId, 2,
                        app.slipnet.util.ConnectionDiagnostics.Failure.AUTH_REJECTED)
                    Log.w(TAG, "Remote proxy requires auth but no credentials provided")
                    if (sendClientReply) {
                        clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                        clientOutput.flush()
                    }
                    return false
                }

                val usernameBytes = username!!.toByteArray(Charsets.UTF_8)
                val passwordBytes = password!!.toByteArray(Charsets.UTF_8)

                // Subnegotiation: VER=0x01, ULEN, UNAME, PLEN, PASSWD
                val authReq = ByteArray(3 + usernameBytes.size + passwordBytes.size)
                authReq[0] = 0x01 // subnegotiation version
                authReq[1] = usernameBytes.size.toByte()
                System.arraycopy(usernameBytes, 0, authReq, 2, usernameBytes.size)
                authReq[2 + usernameBytes.size] = passwordBytes.size.toByte()
                System.arraycopy(passwordBytes, 0, authReq, 3 + usernameBytes.size, passwordBytes.size)

                remoteOutput.write(authReq)
                remoteOutput.flush()

                // Read auth response: VER=0x01, STATUS (0x00 = success)
                val authResp = ByteArray(2)
                remoteInput.readFully(authResp)

                if (authResp[0] != 0x01.toByte() || authResp[1] != 0x00.toByte()) {
                    trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_AUTH, runId, authResp[1].toInt() and 255, app.slipnet.util.ConnectionDiagnostics.Failure.AUTH_REJECTED)
                    Log.w(TAG, "Remote proxy authentication failed (status=${authResp[1]})")
                    if (sendClientReply) {
                        clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                        clientOutput.flush()
                    }
                    return false
                }
                trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_AUTH, runId, 0)
                logd("Remote proxy authentication succeeded")
            }
            0xFF -> {
                trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_AUTH, runId, 255,
                    app.slipnet.util.ConnectionDiagnostics.Failure.AUTH_REJECTED)
                Log.w(TAG, "Remote proxy rejected all auth methods")
                if (sendClientReply) {
                    clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                    clientOutput.flush()
                }
                return false
            }
            else -> {
                Log.w(TAG, "Remote proxy selected unsupported auth method: $selectedMethod")
                if (sendClientReply) {
                    clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                    clientOutput.flush()
                }
                return false
            }
        }

        return true
    }

    /**
     * Connect through remote SOCKS5 proxy and bridge bidirectionally.
     * If [sendReply] is true, sends SOCKS5 success/failure replies to client.
     * If false, success reply was already sent (early reply for sniffing).
     */
    private fun connectViaProxy(
        destHost: String,
        destPort: Int,
        rawAddr: ByteArray,
        portBytes: ByteArray,
        clientSocket: Socket,
        clientInput: InputStream,
        clientOutput: OutputStream,
        sendReply: Boolean,
        runId: Long
    ) {
        val remoteSocket = Socket()
        if (!track(remoteSocket, runId)) return
        try {
            remoteSocket.connect(InetSocketAddress(remoteHost, remotePort), TCP_CONNECT_TIMEOUT_MS)
            remoteSocket.soTimeout = TCP_CONNECT_TIMEOUT_MS
            remoteSocket.tcpNoDelay = true
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_TCP_READY, runId)
        } catch (e: Exception) {
            activeSockets.remove(remoteSocket)
            runCatching { remoteSocket.close() }
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_CONNECT, runId, failure = app.slipnet.util.ConnectionDiagnostics.failure(e))
            logd("CONNECT: failed to connect to remote SOCKS5 proxy: ${e.message}")
            if (sendReply) {
                clientOutput.write(byteArrayOf(0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                clientOutput.flush()
            }
            return
        }

        try {
            val remoteInput = remoteSocket.getInputStream()
            val remoteOutput = remoteSocket.getOutputStream()

            // SOCKS5 handshake with remote proxy (greeting + optional auth)
            if (!performRemoteSocks5Handshake(remoteInput, remoteOutput, sendReply, clientOutput, runId)) {
                remoteSocket.close()
                return
            }

            // SOCKS5 CONNECT request to remote proxy
            val connectReq = byteArrayOf(0x05, 0x01, 0x00) + rawAddr + portBytes
            remoteOutput.write(connectReq)
            remoteOutput.flush()

            // Read CONNECT response header
            val connRespHeader = ByteArray(4)
            remoteInput.readFully(connRespHeader)

            if (connRespHeader[0] != 0x05.toByte() || connRespHeader[2] != 0x00.toByte()) {
                trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_CONNECT, runId, 1)
                throw java.io.IOException("Invalid SOCKS5 CONNECT response")
            }
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_CONNECT, runId, connRespHeader[1].toInt() and 255)
            if (connRespHeader[1] != 0x00.toByte()) {
                logd("CONNECT: remote proxy rejected to $destHost:$destPort (rep=${connRespHeader[1]})")
                if (sendReply) {
                    clientOutput.write(byteArrayOf(0x05, connRespHeader[1], 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                    clientOutput.flush()
                }
                remoteSocket.close()
                return
            }

            // Read remaining response bytes based on address type
            when (connRespHeader[3].toInt() and 0xFF) {
                0x01 -> { val rest = ByteArray(6); remoteInput.readFully(rest) }
                0x03 -> { val len = remoteInput.read(); if (len !in 1..255) throw java.io.IOException("Invalid SOCKS5 bound address"); remoteInput.readFully(ByteArray(len + 2)) }
                0x04 -> { val rest = ByteArray(18); remoteInput.readFully(rest) }
            }

            logd("CONNECT: $destHost:$destPort OK (via remote proxy)")

            // Send success to hev-socks5-tunnel (if not already sent)
            if (sendReply) {
                clientOutput.write(byteArrayOf(0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                clientOutput.flush()
            }

            clientSocket.soTimeout = 0
            remoteSocket.soTimeout = 0

            // Bridge bidirectionally
            remoteSocket.use { remote ->
                val t1 = Thread({
                    try {
                        copyStream(clientInput, remoteOutput, tunnelTxBytes, uploadLimiter)
                    } catch (_: Exception) {
                    } finally {
                        try { remoteOutput.close() } catch (_: Exception) {}
                    }
                }, "socks5-bridge-c2s-$instanceId")
                t1.isDaemon = true
                t1.start()

                try {
                    copyStream(remoteInput, clientOutput, tunnelRxBytes, downloadLimiter)
                } catch (_: Exception) {
                } finally {
                    try { remote.close() } catch (_: Exception) {}
                    t1.interrupt()
                }
            }
        } catch (e: Exception) {
            trace(app.slipnet.util.ConnectionDiagnostics.Event.SOCKS_CONNECT, runId,
                failure = app.slipnet.util.ConnectionDiagnostics.failure(e))
            logd("CONNECT: chain error for $destHost:$destPort: ${e.message}")
            if (sendReply) {
                try {
                    clientOutput.write(byteArrayOf(0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
                    clientOutput.flush()
                } catch (_: Exception) {}
            }
            try { remoteSocket.close() } catch (_: Exception) {}
        } finally {
            activeSockets.remove(remoteSocket)
            runCatching { remoteSocket.close() }
        }
    }

    /**
     * Handle FWD_UDP (cmd 0x05) — same wire format as other bridges.
     * DNS (port 53): DNS-over-TCP through remote SOCKS5 CONNECT to configured DNS server.
     * Non-DNS UDP: dropped silently.
     *
     * DNS queries are dispatched to a thread pool for concurrent resolution.
     * Responses are cached for 60s to avoid repeated round-trips.
     */
    private fun handleFwdUdp(input: InputStream, output: OutputStream, runId: Long) {
        output.write(byteArrayOf(0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0))
        output.flush()

        logd("FWD_UDP session established")

        while (running.get() && runGeneration.get() == runId && !Thread.currentThread().isInterrupted) {
            val hdr = ByteArray(3)
            input.readFully(hdr)

            val datLen = ((hdr[0].toInt() and 0xFF) shl 8) or (hdr[1].toInt() and 0xFF)
            val hdrLen = hdr[2].toInt() and 0xFF
            val addrLen = hdrLen - 3

            if (addrLen <= 0 || datLen <= 0) {
                Log.w(TAG, "FWD_UDP: invalid header (datLen=$datLen, hdrLen=$hdrLen)")
                break
            }

            val addrBytes = ByteArray(addrLen)
            input.readFully(addrBytes)

            val payload = ByteArray(datLen)
            input.readFully(payload)

            val dest = parseSocksAddress(addrBytes)
            if (dest == null) {
                Log.w(TAG, "FWD_UDP: failed to parse address")
                continue
            }

            if (dest.second != 53) {
                // Non-DNS UDP: drop silently
                continue
            }

            // Dispatch DNS query to thread pool for concurrent resolution
            val addrCopy = addrBytes.copyOf()
            val payloadCopy = payload.copyOf()
            dnsExecutor.submit {
                if (!running.get() || runGeneration.get() != runId) return@submit
                try {
                    // Check cache first. Only answers fetched through the tunnel count as
                    // tunnel traffic; cache hits and dropped non-DNS UDP never cross it.
                    val cached = getCachedDns(payloadCopy)
                    val response = if (cached != null) {
                        logd("DNS: cache hit")
                        cached
                    } else {
                        val resolved = forwardDnsTcp(payloadCopy, runId)
                        if (resolved != null && resolved.isNotEmpty()) {
                            synchronized(activeSockets) {
                                if (!running.get() || runGeneration.get() != runId) return@submit
                                if (DnsFallback.cacheable(resolved)) cacheDnsResponse(payloadCopy, resolved)
                                tunnelTxBytes.addAndGet(payloadCopy.size.toLong())
                                tunnelRxBytes.addAndGet(resolved.size.toLong())
                            }
                        }
                        resolved
                    }

                    if (response != null && response.isNotEmpty()) {

                        val respHdr = ByteArray(3)
                        respHdr[0] = ((response.size shr 8) and 0xFF).toByte()
                        respHdr[1] = (response.size and 0xFF).toByte()
                        respHdr[2] = (3 + addrCopy.size).toByte()

                        synchronized(output) {
                            output.write(respHdr)
                            output.write(addrCopy)
                            output.write(response)
                            output.flush()
                        }
                    }
                } catch (e: Exception) {
                    logd("FWD_UDP: DNS forward failed: ${e.message}")
                }
            }
        }

        logd("FWD_UDP session ended")
    }

    // --- DNS cache ---

    /**
     * Cache key: hex of DNS query bytes after the 2-byte transaction ID.
     * This way the same query with different TXIDs hits the cache.
     */
    private fun dnsCacheKey(query: ByteArray): String {
        if (query.size <= 2) return ""
        val sb = StringBuilder((query.size - 2) * 2)
        for (i in 2 until query.size) {
            sb.append("%02x".format(query[i]))
        }
        return sb.toString()
    }

    private fun getCachedDns(query: ByteArray): ByteArray? {
        val key = dnsCacheKey(query)
        if (key.isEmpty()) return null
        val entry = dnsCache[key] ?: return null
        if (System.currentTimeMillis() > entry.expiresAt) {
            dnsCache.remove(key)
            return null
        }
        // Copy cached response and replace transaction ID with query's
        val response = entry.response.copyOf()
        if (response.size >= 2 && query.size >= 2) {
            response[0] = query[0]
            response[1] = query[1]
        }
        return response
    }

    private fun cacheDnsResponse(query: ByteArray, response: ByteArray) {
        val key = dnsCacheKey(query)
        if (key.isEmpty()) return
        dnsCache[key] = DnsCacheEntry(response.copyOf(), System.currentTimeMillis() + DNS_CACHE_TTL_MS)
        // Evict old entries periodically (simple: if cache gets large)
        if (dnsCache.size > 500) {
            val now = System.currentTimeMillis()
            dnsCache.entries.removeIf { now > it.value.expiresAt }
        }
    }

    // --- DNS-over-TCP via remote SOCKS5 proxy ---

    /**
     * Forward DNS query as DNS-over-TCP through the remote SOCKS5 proxy CONNECT to the configured DNS server.
     * Each call opens a new SOCKS5 connection (no persistent connections to keep it simple).
     */
    private fun forwardDnsTcp(payload: ByteArray, runId: Long): ByteArray? {
        var index = 0
        val response = DnsFallback.query(payload, dnsHost, dnsFallback) { host, request ->
            if (!running.get() || runGeneration.get() != runId) null else {
                trace(if (index++ == 0) app.slipnet.util.ConnectionDiagnostics.Event.DNS_PRIMARY else app.slipnet.util.ConnectionDiagnostics.Event.DNS_FALLBACK, runId)
                forwardDnsTcpAttempt(request, host, runId)
            }
        }
        if (response != null && response.size >= 12)
            trace(app.slipnet.util.ConnectionDiagnostics.Event.DNS_ANSWER, runId, response[3].toInt() and 15)
        return response
    }

    private fun dnsDestination(host: String): ByteArray {
        val value = host.removeSurrounding("[", "]")
        val parts = value.split('.')
        if (parts.size == 4 && parts.all { it.toIntOrNull() != null }) {
            require(parts.all { it.toInt() in 0..255 }) { "Invalid DNS IPv4 address" }
            return byteArrayOf(1) + parts.map { it.toInt().toByte() }.toByteArray()
        }
        if (':' in value) {
            val bytes = java.net.InetAddress.getByName(value).address // Numeric IPv6 only, no DNS lookup.
            return byteArrayOf(if (bytes.size == 4) 1 else 4) + bytes
        }
        val bytes = java.net.IDN.toASCII(value).toByteArray(Charsets.US_ASCII)
        require(bytes.size in 1..255) { "Invalid remote DNS host" }
        return byteArrayOf(3, bytes.size.toByte()) + bytes
    }

    private fun forwardDnsTcpAttempt(payload: ByteArray, destination: String, runId: Long): ByteArray? {
        var socket: Socket? = null
        var deadline: ScheduledFuture<*>? = null
        val deadlineExpired = AtomicBoolean(false)
        try {
            // Connect to remote SOCKS5 proxy
            val ownedSocket = Socket()
            socket = ownedSocket
            if (!track(ownedSocket, runId)) return null
            deadline = dnsDeadlines.schedule({
                deadlineExpired.set(true)
                runCatching { ownedSocket.close() }
            }, DNS_TIMEOUT_MS.toLong(), TimeUnit.MILLISECONDS)
            socket.connect(InetSocketAddress(remoteHost, remotePort), DNS_TIMEOUT_MS)
            socket.soTimeout = DNS_TIMEOUT_MS
            socket.tcpNoDelay = true
            trace(app.slipnet.util.ConnectionDiagnostics.Event.DNS_TCP_READY, runId)

            val input = socket.getInputStream()
            val output = socket.getOutputStream()

            // SOCKS5 handshake with remote proxy (greeting + optional auth)
            // Use a dummy OutputStream for client reply since DNS doesn't send back SOCKS errors
            if (!performRemoteSocks5Handshake(input, output, false, output, runId)) {
                logd("DNS: remote proxy handshake failed")
                return null
            }

            // Keep DNS resolution at the remote side; support numeric IPv4/IPv6 and DNS names.
            val connectReq = byteArrayOf(0x05, 0x01, 0x00) + dnsDestination(destination) + byteArrayOf(0, 53)
            output.write(connectReq)
            output.flush()

            // Read CONNECT response
            val connResp = ByteArray(4)
            input.readFully(connResp)
            if (connResp[0] != 0x05.toByte() || connResp[2] != 0x00.toByte()) return null
            trace(app.slipnet.util.ConnectionDiagnostics.Event.DNS_CONNECT, runId, connResp[1].toInt() and 255)
            if (connResp[1] != 0x00.toByte()) {
                logd("DNS: SOCKS5 CONNECT rejected (rep=${connResp[1]})")
                return null
            }

            // Skip remaining response bytes based on address type
            when (connResp[3].toInt() and 0xFF) {
                0x01 -> { val rest = ByteArray(6); input.readFully(rest) }
                0x03 -> { val len = input.read(); if (len !in 1..255) return null; input.readFully(ByteArray(len + 2)) }
                0x04 -> { val rest = ByteArray(18); input.readFully(rest) }
                else -> return null
            }

            // Send DNS query as TCP (2-byte length prefix per RFC 1035 section 4.2.2)
            output.write(byteArrayOf(
                ((payload.size shr 8) and 0xFF).toByte(),
                (payload.size and 0xFF).toByte()
            ))
            output.write(payload)
            output.flush()
            trace(app.slipnet.util.ConnectionDiagnostics.Event.DNS_QUERY_SENT, runId)

            // Read DNS response (2-byte length prefix)
            val respLenBuf = ByteArray(2)
            input.readFully(respLenBuf)
            val respLen = ((respLenBuf[0].toInt() and 0xFF) shl 8) or (respLenBuf[1].toInt() and 0xFF)

            if (respLen <= 0 || respLen > 65535) {
                logd("DNS: invalid response length: $respLen")
                return null
            }

            val response = ByteArray(respLen)
            input.readFully(response)

            logd("DNS: resolved via remote proxy (${response.size} bytes)")
            return response
        } catch (e: Exception) {
            trace(app.slipnet.util.ConnectionDiagnostics.Event.DNS_FAILED, runId,
                failure = if (deadlineExpired.get()) app.slipnet.util.ConnectionDiagnostics.Failure.TIMEOUT
                    else app.slipnet.util.ConnectionDiagnostics.failure(e))
            logd("DNS-over-TCP failed: ${e.message}")
            return null
        } finally {
            deadline?.cancel(false)
            if (socket != null) activeSockets.remove(socket)
            try { socket?.close() } catch (_: Exception) {}
        }
    }

    private fun parseSocksAddress(addrBytes: ByteArray): Pair<String, Int>? {
        if (addrBytes.isEmpty()) return null

        return when (addrBytes[0].toInt() and 0xFF) {
            0x01 -> {
                if (addrBytes.size < 7) return null
                val host = "${addrBytes[1].toInt() and 0xFF}.${addrBytes[2].toInt() and 0xFF}.${addrBytes[3].toInt() and 0xFF}.${addrBytes[4].toInt() and 0xFF}"
                val port = ((addrBytes[5].toInt() and 0xFF) shl 8) or (addrBytes[6].toInt() and 0xFF)
                Pair(host, port)
            }
            0x03 -> {
                val len = addrBytes[1].toInt() and 0xFF
                if (addrBytes.size < 2 + len + 2) return null
                val host = String(addrBytes, 2, len)
                val port = ((addrBytes[2 + len].toInt() and 0xFF) shl 8) or (addrBytes[3 + len].toInt() and 0xFF)
                Pair(host, port)
            }
            0x04 -> {
                if (addrBytes.size < 19) return null
                val parts = mutableListOf<String>()
                for (i in 0 until 16 step 2) {
                    val value = ((addrBytes[1 + i].toInt() and 0xFF) shl 8) or (addrBytes[2 + i].toInt() and 0xFF)
                    parts.add(String.format("%x", value))
                }
                val host = parts.joinToString(":")
                val port = ((addrBytes[17].toInt() and 0xFF) shl 8) or (addrBytes[18].toInt() and 0xFF)
                Pair(host, port)
            }
            else -> null
        }
    }

    private fun bridgeDirect(clientInput: InputStream, clientOutput: OutputStream, directSocket: Socket) {
        directSocket.use { remote ->
            remote.tcpNoDelay = true
            val remoteInput = remote.getInputStream()
            val remoteOutput = remote.getOutputStream()

            val t1 = Thread({
                try {
                    copyStream(clientInput, remoteOutput, limiter = uploadLimiter)
                } catch (_: Exception) {
                } finally {
                    try { remoteOutput.close() } catch (_: Exception) {}
                }
            }, "socks5-bridge-direct-c2s-$instanceId")
            t1.isDaemon = true
            t1.start()

            try {
                copyStream(remoteInput, clientOutput, limiter = downloadLimiter)
            } catch (_: Exception) {
            } finally {
                try { remote.close() } catch (_: Exception) {}
                t1.interrupt()
            }
        }
    }

    private fun copyStream(input: InputStream, output: OutputStream, counter: AtomicLong? = null, limiter: RateLimiter? = null) {
        val buffered = BufferedOutputStream(output, BUFFER_SIZE)
        val buffer = ByteArray(BUFFER_SIZE)
        val maxRead = if (limiter != null && limiter.bytesPerSecond > 0) {
            (limiter.bytesPerSecond / 4).toInt().coerceIn(1024, BUFFER_SIZE)
        } else BUFFER_SIZE
        while (!Thread.currentThread().isInterrupted) {
            val bytesRead = input.read(buffer, 0, maxRead)
            if (bytesRead == -1) break
            limiter?.acquire(bytesRead)
            buffered.write(buffer, 0, bytesRead)
            counter?.addAndGet(bytesRead.toLong())
            if (bytesRead < maxRead || input.available() == 0) {
                buffered.flush()
            }
        }
        buffered.flush()
    }

    private fun InputStream.readFully(buffer: ByteArray) {
        var offset = 0
        while (offset < buffer.size) {
            val bytesRead = this.read(buffer, offset, buffer.size - offset)
            if (bytesRead == -1) throw java.io.IOException("Unexpected end of stream")
            offset += bytesRead
        }
    }

    private fun formatIpv6(addr: ByteArray): String {
        val parts = mutableListOf<String>()
        for (i in 0 until 16 step 2) {
            val value = ((addr[i].toInt() and 0xFF) shl 8) or (addr[i + 1].toInt() and 0xFF)
            parts.add(String.format("%x", value))
        }
        return parts.joinToString(":")
    }
}
