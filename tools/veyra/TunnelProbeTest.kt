import kotlinx.coroutines.launch
import app.slipnet.core.TunnelProbe
import app.slipnet.core.ProbeSummary
import java.io.DataInputStream
import java.io.FileInputStream
import java.net.ServerSocket
import java.net.Socket
import java.security.KeyStore
import javax.net.ssl.*
import kotlin.concurrent.thread

fun main() {
    val path = System.getenv("TEST_TLS_STORE") ?: error("Set TEST_TLS_STORE to the local test PKCS12 keystore")
    val store = KeyStore.getInstance("PKCS12").apply { FileInputStream(path).use { load(it, "test-only".toCharArray()) } }
    val km = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm()).apply { init(store, "test-only".toCharArray()) }
    val tm = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm()).apply { init(store) }
    val ctx = SSLContext.getInstance("TLS").apply { init(km.keyManagers, tm.trustManagers, null) }
    val tlsServer = ctx.serverSocketFactory.createServerSocket(0) as SSLServerSocket
    thread(isDaemon = true) {
        while (!tlsServer.isClosed) runCatching {
            val s = tlsServer.accept() as SSLSocket
            thread(isDaemon = true) { runCatching { s.use {
                val reader = it.inputStream.bufferedReader()
                val request = reader.readLine()
                while (!reader.readLine().isNullOrEmpty()) { }
                it.outputStream.write((if (request.contains("/bad")) "HTTP/1.1 302 Found\r\n\r\n" else "HTTP/1.1 204 No Content\r\n\r\n").toByteArray())
                it.outputStream.flush()
            } } }
        }
    }
    fun proxy(auth: Boolean): ServerSocket {
        val server = ServerSocket(0)
        thread(isDaemon = true) {
            while (!server.isClosed) runCatching {
                val client = server.accept()
                thread(isDaemon = true) { runCatching { client.use { c ->
                    val input = DataInputStream(c.inputStream); val out = c.outputStream
                    check(input.readUnsignedByte() == 5)
                    input.readFully(ByteArray(input.readUnsignedByte()))
                    out.write(byteArrayOf(5, if (auth) 2 else 0));out.flush()
                    if (auth) {
                        check(input.readUnsignedByte() == 1)
                        val u = ByteArray(input.readUnsignedByte());input.readFully(u)
                        val p = ByteArray(input.readUnsignedByte());input.readFully(p)
                        val ok = String(u) == "u" && String(p) == "p"
                        out.write(byteArrayOf(1, if (ok) 0 else 1));out.flush()
                        if (!ok) return@use
                    }
                    check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1)
                    input.readUnsignedByte();check(input.readUnsignedByte() == 3)
                    input.readFully(ByteArray(input.readUnsignedByte()));input.readUnsignedShort()
                    Socket("127.0.0.1", tlsServer.localPort).use { remote ->
                        out.write(byteArrayOf(5,0,0,1,127,0,0,1,0,0));out.flush()
                        thread(isDaemon = true) { runCatching { c.inputStream.copyTo(remote.outputStream) } }
                        remote.inputStream.copyTo(c.outputStream)
                    }
                } } }
            }
        }
        return server
    }
    proxy(false).use { p ->
        check(TunnelProbe.measure(p.localPort, host = "localhost", remotePort = tlsServer.localPort, factory = ctx.socketFactory) >= 0)
        check(runCatching { TunnelProbe.measure(p.localPort, host = "localhost", path = "/bad", factory = ctx.socketFactory) }.isFailure)
        check(runCatching { TunnelProbe.measure(p.localPort, host = "wrong.invalid", factory = ctx.socketFactory) }.isFailure)
        check(runCatching { TunnelProbe.measure(p.localPort, host = "localhost") }.isFailure) // untrusted test certificate
    }
    proxy(true).use { p ->
        check(TunnelProbe.measure(p.localPort, "u", "p", host = "localhost", factory = ctx.socketFactory) >= 0)
        check(runCatching { TunnelProbe.measure(p.localPort, "u", "wrong", host = "localhost", factory = ctx.socketFactory) }.isFailure)
    }
    val dead = ServerSocket(0).use { it.localPort }
    check(runCatching { TunnelProbe.measure(dead, host = "localhost", factory = ctx.socketFactory) }.isFailure)
    check(ProbeSummary.of(listOf(120, null, 80)) == ProbeSummary(100,40,2,3))
    check(ProbeSummary.of(listOf(null,null,null)) == null)
    val profiles = (1L..4L).map { app.slipnet.domain.model.ServerProfile(id = it, name = "P$it") }
    val results = mapOf(1L to app.slipnet.domain.model.PingResult.Success(1),
        2L to app.slipnet.domain.model.PingResult.Success(100, true, 1, 3),
        3L to app.slipnet.domain.model.PingResult.Success(200, true, 3, 3))
    check(app.slipnet.domain.model.ProfileRanking.sort(profiles, results).map { it.id } == listOf(2L,3L,1L,4L))
    check(app.slipnet.domain.model.ProfileRanking.sort(profiles, results, true).map { it.id } == listOf(3L,2L,1L,4L))
    val mixed = results + (1L to app.slipnet.domain.model.PingResult.Success(5, true, quick = true))
    check(app.slipnet.domain.model.ProfileRanking.sort(profiles, mixed).first().id == 1L)
    check(app.slipnet.domain.model.ProfileRanking.sort(profiles, mixed, true).map { it.id } == listOf(3L,2L,1L,4L))
    kotlinx.coroutines.runBlocking {
        var calls = 0
        check(TunnelProbe.medianVerified({ true }) { host ->
            calls++
            if (host == TunnelProbe.HOST) error("primary blocked")
            80L
        } == 80L)
        check(calls == 4)
        calls = 0
        check(TunnelProbe.medianVerified({ false }) { calls++; 80L } == null && calls == 0)
        calls = 0
        check(TunnelProbe.medianVerified({ true }) { calls++; error("both blocked") } == null && calls == 2)
        val hosts = mutableListOf<String>()
        val blocked = TunnelProbe.sample({}) { host -> hosts.add(host); error("endpoint blocked") }
        check(blocked.summary == null && blocked.attempts == 2)
        check(hosts == listOf(TunnelProbe.HOST, TunnelProbe.FALLBACK_HOST))
        hosts.clear()
        val alternate = TunnelProbe.sample({}) { host ->
            hosts.add(host)
            if (host == TunnelProbe.HOST) throw java.net.SocketTimeoutException()
            83L
        }
        check(hosts == listOf(TunnelProbe.HOST, TunnelProbe.FALLBACK_HOST, TunnelProbe.FALLBACK_HOST))
        check(alternate.summary == ProbeSummary(83,0,2,3))
        hosts.clear()
        val quick = TunnelProbe.sample({}, successTarget = 1) { host -> hosts.add(host); 37L }
        check(hosts == listOf(TunnelProbe.HOST) && quick.summary == ProbeSummary(37,0,1,1))
        hosts.clear()
        val quickAlternate = TunnelProbe.sample({}, successTarget = 1) { host ->
            hosts.add(host)
            if (host == TunnelProbe.HOST) throw java.net.SocketTimeoutException()
            49L
        }
        check(hosts == listOf(TunnelProbe.HOST,TunnelProbe.FALLBACK_HOST))
        check(quickAlternate.summary == ProbeSummary(49,0,1,2))
        check(runCatching { TunnelProbe.sample({}, successTarget = 0) { 0L } }.isFailure)
        // Cancel while the proxy is silent. It must see EOF without waiting
        // for the configured 20-second socket deadline.
        ServerSocket(0).use { silent ->
            val greeting = kotlinx.coroutines.CompletableDeferred<Unit>()
            val closed = kotlinx.coroutines.CompletableDeferred<Boolean>()
            thread(isDaemon = true) {
                silent.accept().use { peer ->
                    DataInputStream(peer.getInputStream()).readFully(ByteArray(3))
                    greeting.complete(Unit)
                    closed.complete(peer.getInputStream().read() == -1)
                }
            }
            val job = launch { TunnelProbe.measureCancellable(silent.localPort, null, null, TunnelProbe.HOST, 20000) }
            kotlinx.coroutines.withTimeout(3000) { greeting.await(); job.cancel(); job.join(); check(closed.await()) }
        }
    }
    tlsServer.close()
    println("HTTPS probe: SOCKS-only success, authentication, bad credentials, redirect rejection, hostname and certificate verification, unavailable proxy and sample statistics passed")
}
