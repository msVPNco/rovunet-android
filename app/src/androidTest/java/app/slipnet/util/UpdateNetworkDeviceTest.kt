package app.slipnet.util

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.core.LocalSocksEndpoint
import app.slipnet.core.ProxySocketFactory
import app.slipnet.core.UpdateManifest
import app.slipnet.domain.model.ConnectionState
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import okhttp3.*
import java.io.DataInputStream
import java.net.InetAddress
import java.net.ServerSocket
import java.net.Socket
import java.security.KeyStore
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import javax.net.ssl.*
import org.junit.Assert.*
import org.junit.Test

/** TLS and authenticated SOCKS use loopback only; no request reaches GitHub or an operator. */
class UpdateNetworkDeviceTest {
    private class Fixture : AutoCloseable {
        val hosts = CopyOnWriteArrayList<String>()
        val requests = CopyOnWriteArrayList<String>()
        val errors = CopyOnWriteArrayList<Throwable>()
        val stalls = AtomicBoolean(false)
        val cutFirst = AtomicBoolean(false)
        val corrupt = AtomicBoolean(false)
        val wrongRange = AtomicBoolean(false)
        val closedReads = java.util.concurrent.CountDownLatch(1)
        private val sockets = CopyOnWriteArrayList<Socket>()
        private val workers = Executors.newCachedThreadPool()
        private val listener = ServerSocket(0, 16, InetAddress.getByName("127.0.0.1"))
        val ssl: SSLContext
        private var trustManager: X509TrustManager? = null
        init {
            // Fresh per-test key in memory; no private fixture key is tracked or shipped.
            val pair = java.security.KeyPairGenerator.getInstance("RSA").apply { initialize(2048) }.generateKeyPair()
            val algorithm = org.bouncycastle.asn1.x509.AlgorithmIdentifier(
                org.bouncycastle.asn1.pkcs.PKCSObjectIdentifiers.sha256WithRSAEncryption, org.bouncycastle.asn1.DERNull.INSTANCE)
            val extensions = org.bouncycastle.asn1.x509.ExtensionsGenerator().apply {
                addExtension(org.bouncycastle.asn1.x509.Extension.subjectAlternativeName, false,
                    org.bouncycastle.asn1.x509.GeneralNames(arrayOf("github.com", "api.github.com", "release-assets.githubusercontent.com").map {
                        org.bouncycastle.asn1.x509.GeneralName(org.bouncycastle.asn1.x509.GeneralName.dNSName, it)
                    }.toTypedArray()))
            }
            val generator = org.bouncycastle.asn1.x509.V3TBSCertificateGenerator().apply {
                setSerialNumber(org.bouncycastle.asn1.ASN1Integer(java.math.BigInteger.valueOf(System.nanoTime()).abs()))
                setSignature(algorithm)
                val name = org.bouncycastle.asn1.x500.X500Name("CN=Ephemeral updater test")
                setIssuer(name); setSubject(name)
                setStartDate(org.bouncycastle.asn1.x509.Time(java.util.Date(System.currentTimeMillis() - 60_000)))
                setEndDate(org.bouncycastle.asn1.x509.Time(java.util.Date(System.currentTimeMillis() + 86_400_000)))
                setSubjectPublicKeyInfo(org.bouncycastle.asn1.x509.SubjectPublicKeyInfo.getInstance(pair.public.encoded))
                setExtensions(extensions.generate())
            }
            val tbs = generator.generateTBSCertificate()
            val signer = java.security.Signature.getInstance("SHA256withRSA").apply { initSign(pair.private); update(tbs.encoded) }
            val encoded = org.bouncycastle.asn1.DERSequence(arrayOf<org.bouncycastle.asn1.ASN1Encodable>(
                tbs, algorithm, org.bouncycastle.asn1.DERBitString(signer.sign()))).encoded
            val certificate = java.security.cert.CertificateFactory.getInstance("X.509").generateCertificate(encoded.inputStream())
            val store = KeyStore.getInstance("PKCS12").apply {
                load(null, null)
                setKeyEntry("ephemeral-test", pair.private, "test-only".toCharArray(), arrayOf(certificate))
            }
            val keys = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm()).apply { init(store, "test-only".toCharArray()) }
            val trust = TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm()).apply { init(store) }
            ssl = SSLContext.getInstance("TLS").apply { init(keys.keyManagers, trust.trustManagers, null) }
            trustManager = trust.trustManagers.filterIsInstance<X509TrustManager>().single()
            workers.submit {
                while (!listener.isClosed) {
                    val socket = try { listener.accept() } catch (_: Exception) { break }
                    sockets += socket
                    workers.submit { serve(socket) }
                }
            }
        }
        fun client(trusted: Boolean = true): OkHttpClient = OkHttpClient.Builder()
            .proxy(java.net.Proxy.NO_PROXY)
            .socketFactory(ProxySocketFactory(LocalSocksEndpoint(listener.localPort, "test-user", "test-password")))
            .dns(object : Dns { override fun lookup(hostname: String) = listOf(InetAddress.getByAddress(hostname, byteArrayOf(0, 0, 0, 1))) })
            .apply { if (trusted) sslSocketFactory(ssl.socketFactory, requireNotNull(trustManager)) }
            .followRedirects(false).followSslRedirects(false).retryOnConnectionFailure(false)
            .connectTimeout(5, TimeUnit.SECONDS).readTimeout(5, TimeUnit.SECONDS)
            .connectionPool(ConnectionPool(0, 1, TimeUnit.SECONDS)).build()

        private fun serve(raw: Socket) {
            try {
                raw.soTimeout = 5000
                val input = DataInputStream(raw.getInputStream()); val out = raw.getOutputStream()
                check(input.readUnsignedByte() == 5)
                val methods = ByteArray(input.readUnsignedByte()).also(input::readFully)
                check(methods.contentEquals(byteArrayOf(2)))
                out.write(byteArrayOf(5, 2)); out.flush()
                check(input.readUnsignedByte() == 1)
                val user = ByteArray(input.readUnsignedByte()).also(input::readFully).toString(Charsets.UTF_8)
                val password = ByteArray(input.readUnsignedByte()).also(input::readFully).toString(Charsets.UTF_8)
                check(user == "test-user" && password == "test-password")
                out.write(byteArrayOf(1, 0)); out.flush()
                check(input.readUnsignedByte() == 5 && input.readUnsignedByte() == 1 && input.readUnsignedByte() == 0)
                check(input.readUnsignedByte() == 3) // hostname, not locally resolved IP
                hosts += ByteArray(input.readUnsignedByte()).also(input::readFully).toString(Charsets.UTF_8)
                check(input.readUnsignedShort() == 443)
                out.write(byteArrayOf(5, 0, 0, 1, 127, 0, 0, 1, 1, 1)); out.flush()
                val tls = ssl.socketFactory.createSocket(raw, "github.com", 443, true) as SSLSocket
                sockets += tls
                tls.useClientMode = false
                tls.use {
                    it.startHandshake()
                    val reader = it.inputStream.bufferedReader()
                    val headers = StringBuilder()
                    while (true) { val line = reader.readLine() ?: break; if (line.isEmpty()) break; headers.append(line).append('\n') }
                    requests += headers.toString()
                    val range = Regex("(?im)^range: bytes=([0-9]+)-").find(headers)?.groupValues?.get(1)?.toInt()
                    val body = when {
                        hosts.last() == "api.github.com" -> "HTTP/1.1 302 Found\r\nLocation: https://release-assets.githubusercontent.com/test\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        cutFirst.compareAndSet(true, false) -> "HTTP/1.1 200 OK\r\nContent-Length: 7\r\nConnection: close\r\n\r\npay"
                        range != null -> "HTTP/1.1 206 Partial Content\r\nContent-Range: bytes ${if (wrongRange.get()) 0 else range}-6/7\r\nContent-Length: ${7-range}\r\nConnection: close\r\n\r\n" + "payload".substring(range)
                        else -> "HTTP/1.1 200 OK\r\nContent-Length: ${if (stalls.get()) 100 else 7}\r\nConnection: close\r\n\r\n${if (corrupt.get()) "damaged" else "payload"}"
                    }
                    it.outputStream.write(body.toByteArray()); it.outputStream.flush()
                    if (stalls.get() && hosts.last() != "api.github.com") {
                        try { while (it.inputStream.read() >= 0) { } } finally { closedReads.countDown() }
                    }
                }
            } catch (error: Throwable) { if (!listener.isClosed && error !is SSLException) errors += error }
        }
        override fun close() {
            listener.close()
            sockets.forEach { runCatching { it.close() } }
            workers.shutdownNow()
            check(workers.awaitTermination(6, TimeUnit.SECONDS))
        }
    }

    @Test fun authenticatedTunnelCarriesHttpsAndCdnRedirectWithoutLocalDns() = runBlocking {
        Fixture().use { fixture ->
            val route = UpdateNetwork.Route(fixture.client()) { true }
            val text = route.get(UpdateManifest.API_URL) { it.body.string() }
            assertEquals("payload", text)
            assertEquals(listOf("api.github.com", "release-assets.githubusercontent.com"), fixture.hosts.toList())
            assertEquals("oad", route.get(UpdateManifest.RELEASES_URL + "/download/v0.3.0-alpha20/Rovunet-Full.apk", offset = 4) { it.body.string() })
            assertTrue(fixture.requests.last().contains("Range: bytes=4-", true))
            assertTrue(fixture.errors.toString(), fixture.errors.isEmpty())
        }
    }

    @Test fun cancellationClosesAStalledBodyAndSessionChangeNeverFallsBackToDirect() = runBlocking {
        for (switchSession in listOf(false, true)) Fixture().use { fixture ->
            fixture.stalls.set(true)
            val valid = AtomicBoolean(true)
            val state = MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
            val route = UpdateNetwork.Route(fixture.client(), state) { valid.get() }
            val download = async(Dispatchers.IO) { runCatching { route.get(UpdateManifest.API_URL) { it.body.string() } } }
            withTimeout(5000) { while (fixture.requests.size < 2) delay(10) }
            if (switchSession) { valid.set(false); state.value = ConnectionState.Connecting }
            else download.cancel()
            withTimeout(3000) { download.join() }
            if (switchSession) assertEquals("Update route changed", download.await().exceptionOrNull()?.message)
            assertTrue(fixture.closedReads.await(3, TimeUnit.SECONDS))
            assertEquals(2, fixture.hosts.size)
        }
    }

    @Test fun tunnelStillRejectsAnUntrustedTlsCertificate() = runBlocking {
        Fixture().use { fixture ->
            val route = UpdateNetwork.Route(fixture.client(trusted = false)) { true }
            val failure = runCatching { route.get(UpdateManifest.API_URL) { it.body.string() } }.exceptionOrNull()
            assertTrue(failure.toString(), failure is SSLException)
            assertEquals(1, fixture.hosts.size)
            assertTrue(fixture.requests.isEmpty())
        }
    }
    @Test fun partialDownloadResumesAndStillRejectsWrongRangeOrChecksum() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val digest = java.security.MessageDigest.getInstance("SHA-256").digest("payload".toByteArray())
            .joinToString("") { "%02x".format(it) }
        val update = AppUpdate("0.3.0-alpha20", UpdateManifest.RELEASES_URL + "/download/v0.3.0-alpha20/Rovunet-Full.apk", "", digest, 7)
        for (mode in listOf("resume", "bad-range", "bad-hash")) Fixture().use { fixture ->
            fixture.cutFirst.set(mode != "bad-hash")
            fixture.corrupt.set(mode == "bad-hash")
            fixture.wrongRange.set(mode == "bad-range")
            val file = java.io.File.createTempFile("update-synthetic-", ".part", context.cacheDir)
            try {
                val failure = runCatching { UpdateChecker.downloadBody(UpdateNetwork.Route(fixture.client()) { true }, update, file) {} }.exceptionOrNull()
                if (mode == "resume") {
                    assertNull(failure)
                    assertEquals("payload", file.readText())
                    assertTrue(fixture.requests.any { it.contains("Range: bytes=3-", true) })
                } else assertEquals(if (mode == "bad-range") "Unexpected APK size" else "APK checksum mismatch", failure?.message)
            } finally { file.delete() }
        }
    }

}
