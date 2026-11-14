import app.slipnet.tunnel.TransportTrust
import app.slipnet.tunnel.TlsSocketFactory
import java.io.File
import java.net.ServerSocket
import java.security.KeyStore
import java.security.MessageDigest
import java.security.cert.X509Certificate
import java.util.concurrent.atomic.AtomicReference
import javax.net.ssl.KeyManagerFactory
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket
import kotlin.concurrent.thread

fun main() {
    val context = android.content.Context()
    TransportTrust.initialize(context)
    TransportTrust.clear()
    TransportTrust.allowPrompt = false
    val directory = java.nio.file.Files.createTempDirectory("rovunet-spki-").toFile()
    fun keytool(vararg options: String) {
        val process = ProcessBuilder(listOf(System.getProperty("java.home") + "/bin/keytool") + options).redirectErrorStream(true).start()
        val output = process.inputStream.bufferedReader().readText()
        check(process.waitFor() == 0) { "Synthetic certificate generation failed: $output" }
    }
    fun store(file: File): KeyStore = KeyStore.getInstance("PKCS12").apply {
        file.inputStream().use { load(it, "test-only".toCharArray()) }
    }
    fun tls(keys: KeyStore) = SSLContext.getInstance("TLS").apply {
        val manager = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm()).apply { init(keys, "test-only".toCharArray()) }
        init(manager.keyManagers, null, null)
    }
    fun sha(bytes: ByteArray) = MessageDigest.getInstance("SHA-256").digest(bytes)
    fun legacyKey(endpoint: String) = sha(endpoint.toByteArray()).joinToString("") { "%02x".format(it) }
    fun pin(cert: X509Certificate) = "SHA256:" + java.util.Base64.getEncoder().withoutPadding().encodeToString(sha(cert.encoded))
    try {
        val originalFile = File(System.getenv("TEST_TLS_STORE"))
        val original = store(originalFile)
        val oldLeaf = original.getCertificate("probe") as X509Certificate
        val renewedFile = File(directory, "renewed.p12").apply { originalFile.copyTo(this) }
        keytool("-selfcert", "-alias", "probe", "-keystore", renewedFile.path, "-storepass", "test-only",
            "-dname", "CN=localhost", "-ext", "SAN=dns:localhost", "-validity", "90")
        val renewed = store(renewedFile)
        val renewedLeaf = renewed.getCertificate("probe") as X509Certificate
        check(!oldLeaf.encoded.contentEquals(renewedLeaf.encoded))
        check(oldLeaf.publicKey.encoded.contentEquals(renewedLeaf.publicKey.encoded))
        val active = AtomicReference(tls(original))
        val server = ServerSocket(0)
        thread(isDaemon = true) {
            while (!server.isClosed) runCatching {
                val raw = server.accept()
                thread(isDaemon = true) { runCatching {
                    (active.get().socketFactory.createSocket(raw, "localhost", server.localPort, true) as SSLSocket).use {
                        it.useClientMode = false; it.soTimeout = 3_000; it.startHandshake()
                    }
                }; runCatching { raw.close() } }
            }
        }
        val endpoint = "TLS · localhost:${server.localPort}"
        val preferences = context.getSharedPreferences("rovunet_server_trust", android.content.Context.MODE_PRIVATE)
        preferences.edit().putString(legacyKey(endpoint), pin(oldLeaf)).commit()
        fun connect() = TlsSocketFactory("localhost", 2_000).createSocket("127.0.0.1", server.localPort).close()
        try {
            connect()
            check(!preferences.contains(legacyKey(endpoint)))
            check(TransportTrust.request.value == null)
            active.set(tls(renewed)); connect()
            check(TransportTrust.request.value == null)
            val changedFile = File(directory, "changed.p12")
            keytool("-genkeypair", "-alias", "probe", "-keyalg", "EC", "-groupname", "secp256r1", "-keystore", changedFile.path,
                "-storetype", "PKCS12", "-storepass", "test-only", "-dname", "CN=localhost", "-ext", "SAN=dns:localhost", "-validity", "90")
            active.set(tls(store(changedFile)))
            check(runCatching { connect() }.isFailure) { "A changed public key was accepted" }
            check(TransportTrust.request.value == null)
            TransportTrust.clear()
            preferences.edit().putString(legacyKey(endpoint), pin(oldLeaf)).commit()
            active.set(tls(renewed))
            check(runCatching { connect() }.isFailure)
            check(preferences.contains(legacyKey(endpoint)))
            TransportTrust.clear()
            val spkiKey = legacyKey("$endpoint · SPKI/v1")
            val certificateKey = "$spkiKey.certificate"
            val changedLeaf = store(changedFile).getCertificate("probe") as X509Certificate
            TransportTrust.rememberTlsCertificate("localhost", server.localPort, arrayOf(oldLeaf), privateCertificate = false)
            check(!preferences.contains(spkiKey))
            val cached = preferences.getString(certificateKey, null)
            check(runCatching { TransportTrust.rememberTlsCertificate("localhost", server.localPort, arrayOf(changedLeaf), privateCertificate = true) }.isFailure)
            check(preferences.getString(certificateKey, null) == cached)
            TransportTrust.rememberTlsCertificate("localhost", server.localPort, arrayOf(changedLeaf), privateCertificate = false)
            TransportTrust.rememberTlsCertificate("localhost", server.localPort, arrayOf(changedLeaf), privateCertificate = true)
            check(!preferences.contains(spkiKey))
            check(TransportTrust.request.value == null)
        } finally { server.close(); TransportTrust.clear() }
        println("PASS real TLS SPKI: legacy migration, renewed certificate with unchanged key, changed-key and legacy-mismatch refusal")
    } finally { directory.deleteRecursively() }
}
