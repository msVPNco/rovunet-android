import app.slipnet.tunnel.Socks5ProxyInstance
import app.slipnet.util.ConnectionDiagnostics
import java.io.DataInputStream
import java.net.InetAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.concurrent.thread

private val dnsQuery = byteArrayOf(0x12,0x34,1,0,0,1,0,0,0,0,0,0,9) +
    "synthetic".toByteArray() + byteArrayOf(7) + "invalid".toByteArray() + byteArrayOf(0,0,1,0,1)

private class DnsUpstream(private val mode: String, private val primary: String) : AutoCloseable {
    val server = ServerSocket(0)
    val destinations = CopyOnWriteArrayList<String>()
    val failures = CopyOnWriteArrayList<Throwable>()
    val receivedQuery = CountDownLatch(1)
    private val sockets = CopyOnWriteArrayList<Socket>()
    private val pool = Executors.newCachedThreadPool()
    private val acceptor = thread(isDaemon = true) {
        while (!server.isClosed) try {
            val peer = server.accept(); sockets += peer
            pool.submit {
                try { peer.use { socket ->
                    socket.soTimeout = 20_000
                    val input = DataInputStream(socket.getInputStream())
                    val output = socket.getOutputStream()
                    check(input.readUnsignedByte() == 5)
                    val methods = ByteArray(input.readUnsignedByte()); input.readFully(methods)
                    check(methods.contains(0))
                    output.write(byteArrayOf(5,0)); output.flush()
                    check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1 && input.readUnsignedByte() == 0)
                    val host = when (input.readUnsignedByte()) {
                        1 -> ByteArray(4).also { input.readFully(it) }.let { InetAddress.getByAddress(it).hostAddress }
                        4 -> ByteArray(16).also { input.readFully(it) }.let { InetAddress.getByAddress(it).hostAddress }
                        3 -> ByteArray(input.readUnsignedByte()).also { input.readFully(it) }.toString(Charsets.US_ASCII)
                        else -> error("Unexpected DNS address type")
                    }
                    check(input.readUnsignedShort() == 53)
                    destinations += host
                    val first = host == primary
                    if (first && mode == "connect-refused") {
                        output.write(byteArrayOf(5,2,0,1,127,0,0,1,0,0)); output.flush(); return@submit
                    }
                    output.write(byteArrayOf(5,0,0,1,127,0,0,1,0,0)); output.flush()
                    val query = ByteArray(input.readUnsignedShort()); input.readFully(query)
                    check(query.size == dnsQuery.size)
                    receivedQuery.countDown()
                    if (first && mode == "silent") {
                        check(input.read() == -1); return@submit
                    }
                    val rcode = when {
                        mode == "both-servfail" -> 2
                        first && mode == "servfail" -> 2
                        first && mode == "dns-refused" -> 5
                        first && mode == "nxdomain" -> 3
                        else -> 0
                    }
                    val response = query.copyOf().also {
                        it[2] = 0x81.toByte(); it[3] = (0x80 or rcode).toByte()
                        if (first && mode == "bad-id") it[0] = (it[0] + 1).toByte()
                    }
                    val packet = byteArrayOf((response.size shr 8).toByte(), response.size.toByte()) + response
                    for (byte in packet) { output.write(byte.toInt()); output.flush() }
                } } catch (error: Throwable) { if (!server.isClosed) failures += error }
                finally { sockets.remove(peer) }
            }
        } catch (_: Exception) { if (!server.isClosed) throw IllegalStateException("Unexpected accept failure") }
    }
    override fun close() {
        server.close(); sockets.forEach { runCatching { it.close() } }
        pool.shutdownNow(); check(pool.awaitTermination(3,TimeUnit.SECONDS)); acceptor.join(2000)
    }
}

private fun dnsClient(port: Int): Socket = Socket("127.0.0.1", port).apply {
    soTimeout = 20_000
    val input = DataInputStream(getInputStream()); val output = getOutputStream()
    output.write(byteArrayOf(5,1,0)); output.flush()
    check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 0)
    output.write(byteArrayOf(5,5,0,1,8,8,8,8,0,53)); output.flush()
    input.readFully(ByteArray(10))
}

private fun query(client: Socket, id: Int): ByteArray {
    val packet = dnsQuery.copyOf().also { it[1] = id.toByte() }
    val address = byteArrayOf(1,8,8,8,8,0,53)
    client.getOutputStream().apply {
        write(byteArrayOf((packet.size shr 8).toByte(),packet.size.toByte(),(3+address.size).toByte()) + address + packet)
        flush()
    }
    val input = DataInputStream(client.getInputStream())
    val length = input.readUnsignedShort(); val headerLength = input.readUnsignedByte()
    input.readFully(ByteArray(headerLength - 3))
    return ByteArray(length).also { input.readFully(it); check(it[1] == id.toByte()) }
}

private fun oldDnsCannotRetryOrPopulateTheCacheOfAReplacement() {
    val bridge = Socks5ProxyInstance("synthetic-dns-replacement").apply { debugLogging = true }
    val caught = CountDownLatch(1)
    val release = CountDownLatch(1)
    val once = AtomicBoolean(true)
    val pool = Executors.newSingleThreadExecutor()
    app.slipnet.util.AppLog.debugHook = { tag, message ->
        if (tag == "Socks5Proxy[synthetic-dns-replacement]" && message.startsWith("DNS-over-TCP failed:") &&
            once.compareAndSet(true,false)) {
            caught.countDown()
            var done = false
            while (!done) {
                try { check(release.await(5,TimeUnit.SECONDS)); done = true } catch (_: InterruptedException) { }
            }
        }
    }
    try {
        DnsUpstream("silent", "192.0.2.1").use { previous -> DnsUpstream("success", "192.0.2.1").use { current ->
            val port = ServerSocket(0).use { it.localPort }
            bridge.start("127.0.0.1",previous.server.localPort,null,null,port,
                dnsHost="192.0.2.1",dnsFallback="192.0.2.2").getOrThrow()
            val oldExecutor = bridge.javaClass.getDeclaredField("dnsExecutor").apply { isAccessible = true }
                .get(bridge) as java.util.concurrent.ExecutorService
            dnsClient(port).use { oldClient ->
                val oldRead = pool.submit<Boolean> { runCatching { query(oldClient,0x34) }.isFailure }
                check(previous.receivedQuery.await(3,TimeUnit.SECONDS))
                bridge.stop()
                check(oldRead.get(2,TimeUnit.SECONDS))
                check(caught.await(2,TimeUnit.SECONDS))
                bridge.start("127.0.0.1",current.server.localPort,null,null,port,
                    dnsHost="192.0.2.1",dnsFallback="192.0.2.2").getOrThrow()
                release.countDown()
                check(oldExecutor.awaitTermination(2,TimeUnit.SECONDS))
                check(current.destinations.isEmpty()) { "Cancelled DNS retried through the replacement" }
                dnsClient(port).use { query(it,0x35) }
                check(current.destinations.toList() == listOf("192.0.2.1")) { "Old DNS poisoned the new cache" }
                check(bridge.isRunning())
            }
        } }
    } finally {
        release.countDown(); app.slipnet.util.AppLog.debugHook = null; bridge.stop(); pool.shutdownNow()
    }
    println("PASS stopped DNS run cannot retry through or populate the cache of a replacement")
}

fun main() {
    val fallback = "192.0.2.2"
    for (mode in listOf("success", "connect-refused", "servfail", "dns-refused", "bad-id", "nxdomain", "both-servfail", "silent", "ipv6", "hostname")) {
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(9001, "SOCKS5")
        val primary = when (mode) {
            "ipv6" -> "2001:db8::53"
            "hostname" -> "synthetic.invalid"
            else -> "192.0.2.1"
        }
        val normalized = if (mode == "ipv6") InetAddress.getByName(primary).hostAddress else primary
        DnsUpstream(mode, normalized).use { upstream ->
            val bridge = Socks5ProxyInstance("synthetic-dns-$mode")
            val port = ServerSocket(0).use { it.localPort }
            bridge.start("127.0.0.1",upstream.server.localPort,null,null,port,
                dnsHost=primary,dnsFallback=fallback).getOrThrow()
            try { dnsClient(port).use { client ->
                val at = System.nanoTime()
                val response = query(client, 0x34)
                val rcode = response[3].toInt() and 15
                check(rcode == when(mode) { "nxdomain" -> 3; "both-servfail" -> 2; else -> 0 })
                val failover = mode in setOf("connect-refused", "servfail", "dns-refused", "bad-id", "both-servfail", "silent")
                check(upstream.destinations.toList() == if(failover) listOf(normalized,fallback) else listOf(normalized)) {
                    "$mode destinations: ${upstream.destinations}"
                }
                query(client, 0x35)
                check(upstream.destinations.size == if(mode == "both-servfail") 4 else if(failover) 2 else 1) {
                    "$mode cache policy: ${upstream.destinations}"
                }
                check(bridge.isRunning()) // This never gates or tears down Connected.
                if(mode == "silent") {
                    val ms = TimeUnit.NANOSECONDS.toMillis(System.nanoTime()-at)
                    check(ms in 14_000..20_000) { "Shipped DNS deadline: $ms ms" }
                    check(ConnectionDiagnostics.report().contains("DNS_FAILED:TIMEOUT"))
                    check(ConnectionDiagnostics.report().contains("DNS_QUERY_SENT"))
                    println("PASS shipped DNS deadline and configured fallback through FWD_UDP: $ms ms")
                }
                check(upstream.failures.isEmpty()) { upstream.failures.toString() }
            } } finally { bridge.stop() }
        }
    }
    oldDnsCannotRetryOrPopulateTheCacheOfAReplacement()
    println("PASS real SOCKS FWD_UDP: primary/fallback, refused CONNECT, SERVFAIL/REFUSED, NXDOMAIN, cache IDs, IPv4/IPv6/domain DNS and fragmented replies")
}
