import app.slipnet.tunnel.Socks5ProxyInstance
import app.slipnet.util.ConnectionDiagnostics
import java.io.DataInputStream
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit
import kotlin.concurrent.thread

private fun localClient(port: Int): Socket = Socket("127.0.0.1", port).apply {
    soTimeout = 35_000
    val input = DataInputStream(getInputStream())
    getOutputStream().write(byteArrayOf(5,1,0)); getOutputStream().flush()
    check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 0)
    val host = "target.invalid".toByteArray()
    getOutputStream().write(byteArrayOf(5,1,0,3,host.size.toByte()) + host + byteArrayOf(1,187.toByte()))
    getOutputStream().flush()
}

private fun serveOnce(server: ServerSocket, operation: (Socket) -> Unit): CompletableFuture<Unit> {
    val finished = CompletableFuture<Unit>()
    thread(isDaemon = true) {
        try { server.accept().use(operation); finished.complete(Unit) }
        catch (error: Throwable) { finished.completeExceptionally(error) }
    }
    return finished
}

private fun greeting(peer: Socket) {
    val input = DataInputStream(peer.getInputStream())
    check(input.readUnsignedByte() == 5)
    val methods = ByteArray(input.readUnsignedByte()); input.readFully(methods)
    check(methods.contains(2))
}

fun main() {
    // A refused remote CONNECT must reach the client and safe diagnostics unchanged.
    for ((rep, meaning) in mapOf(2 to "POLICY_DENIED", 3 to "NETWORK_UNREACHABLE",
        4 to "HOST_UNREACHABLE", 5 to "CONNECTION_REFUSED")) ServerSocket(0).use { upstream ->
        val session = 7000L + rep
        ConnectionDiagnostics.clear()
        ConnectionDiagnostics.begin(session, "SOCKS5")
        val finished = serveOnce(upstream) { remote ->
            val input = DataInputStream(remote.getInputStream()); val output = remote.getOutputStream()
            check(input.readUnsignedByte() == 5)
            input.readFully(ByteArray(input.readUnsignedByte()))
            output.write(byteArrayOf(5,0)); output.flush()
            check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1)
            check(input.readUnsignedByte() == 0 && input.readUnsignedByte() == 3)
            val host = ByteArray(input.readUnsignedByte()); input.readFully(host)
            check(String(host) == "target.invalid" && input.readUnsignedShort() == 443)
            output.write(byteArrayOf(5,rep.toByte(),0,1,0,0,0,0,0,0)); output.flush()
        }
        val bridge = Socks5ProxyInstance("reply-regression")
        val port = ServerSocket(0).use { it.localPort }
        bridge.start("127.0.0.1", upstream.localPort, null, null, port).getOrThrow()
        try {
            localClient(port).use { client ->
                val input = DataInputStream(client.getInputStream())
                check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == rep)
                input.readFully(ByteArray(8))
            }
            finished.get(3, TimeUnit.SECONDS)
            val report = ConnectionDiagnostics.report()
            check(report.contains("FLOW SOCKS_CONNECT:$rep:$meaning count=1")) { report }
            check(!report.contains("target.invalid") && !report.contains("127.0.0.1"))
            if (rep != 2) check(!report.contains("POLICY_DENIED"))
        } finally { bridge.stop() }
    }
    for (accept in listOf(true, false)) ServerSocket(0).use { upstream ->
        val finished = serveOnce(upstream) { remote ->
            val input = DataInputStream(remote.getInputStream()); val output = remote.getOutputStream()
            greeting(remote); output.write(byteArrayOf(5,2)); output.flush()
            check(input.readUnsignedByte() == 1)
            val user = ByteArray(input.readUnsignedByte()); input.readFully(user)
            val password = ByteArray(input.readUnsignedByte()); input.readFully(password)
            check(String(user) == "device-test" && String(password) == "synthetic-only")
            output.write(byteArrayOf(1,if (accept) 0 else 1)); output.flush()
            if (accept) {
                check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1)
                check(input.readUnsignedByte() == 0 && input.readUnsignedByte() == 3)
                val host = ByteArray(input.readUnsignedByte()); input.readFully(host)
                check(String(host) == "target.invalid" && input.readUnsignedShort() == 443)
                output.write(byteArrayOf(5,0,0,1,127,0,0,1,0,0)); output.flush()
                val payload = ByteArray(4); input.readFully(payload); check(String(payload) == "ping")
                output.write("pong".toByteArray()); output.flush()
            }
        }
        val bridge = Socks5ProxyInstance("auth-regression")
        val port = ServerSocket(0).use { it.localPort }
        bridge.start("127.0.0.1", upstream.localPort, "device-test", "synthetic-only", port).getOrThrow()
        try {
            localClient(port).use { client ->
                val input = DataInputStream(client.getInputStream())
                check(input.readUnsignedByte() == 5)
                val reply = input.readUnsignedByte()
                if (accept) {
                    check(reply == 0); input.readFully(ByteArray(8))
                    client.getOutputStream().write("ping".toByteArray());client.getOutputStream().flush()
                    val payload = ByteArray(4); input.readFully(payload); check(String(payload) == "pong")
                } else check(reply != 0)
            }
            finished.get(3, TimeUnit.SECONDS)
        } finally { bridge.stop() }
    }
    // A silent peer cannot leave its handshake socket stranded after Stop.
    ServerSocket(0).use { upstream ->
        val received = CompletableFuture<Unit>()
        val finished = serveOnce(upstream) { peer -> greeting(peer); received.complete(Unit); check(peer.getInputStream().read() == -1) }
        val bridge = Socks5ProxyInstance("cancel-regression")
        val port = ServerSocket(0).use { it.localPort }
        bridge.start("127.0.0.1",upstream.localPort,"device-test","synthetic-only",port).getOrThrow()
        localClient(port).use {
            received.get(3,TimeUnit.SECONDS)
            bridge.stop()
            finished.get(3,TimeUnit.SECONDS)
        }
    }
    // The default deadline is exercised as shipped, even without pressing Stop.
    ServerSocket(0).use { upstream ->
        val finished = serveOnce(upstream) { peer -> greeting(peer); check(peer.getInputStream().read() == -1) }
        val bridge = Socks5ProxyInstance("deadline-regression")
        val port = ServerSocket(0).use { it.localPort }
        bridge.start("127.0.0.1",upstream.localPort,"device-test","synthetic-only",port).getOrThrow()
        try { localClient(port).use {
            val input = DataInputStream(it.getInputStream())
            check(input.readUnsignedByte() == 5 && input.readUnsignedByte() != 0)
            finished.get(3,TimeUnit.SECONDS)
        } } finally { bridge.stop() }
    }
    println("PASS actual SOCKS bridge: REP 2/3/4/5 and safe diagnostics, authenticated relay, rejected credentials, Stop closes upstream handshake and shipped silent-peer deadline")
}
