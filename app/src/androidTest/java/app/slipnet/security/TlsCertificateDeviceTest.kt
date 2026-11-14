package app.slipnet.security

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.tunnel.TransportTrust
import app.slipnet.tunnel.ValidatedTls
import java.net.ServerSocket
import java.net.Socket
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.util.concurrent.Executors
import javax.net.ssl.KeyManagerFactory
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLSocket
import org.junit.Assert.*
import org.junit.Test

class TlsCertificateDeviceTest {
    private fun identity(): SSLContext {
        val pair = KeyPairGenerator.getInstance("RSA").apply { initialize(2048) }.generateKeyPair()
        val algorithm = org.bouncycastle.asn1.x509.AlgorithmIdentifier(
            org.bouncycastle.asn1.pkcs.PKCSObjectIdentifiers.sha256WithRSAEncryption, org.bouncycastle.asn1.DERNull.INSTANCE)
        val extensions = org.bouncycastle.asn1.x509.ExtensionsGenerator().apply {
            addExtension(org.bouncycastle.asn1.x509.Extension.subjectAlternativeName, false,
                org.bouncycastle.asn1.x509.GeneralNames(org.bouncycastle.asn1.x509.GeneralName(
                    org.bouncycastle.asn1.x509.GeneralName.dNSName, "localhost")))
        }
        val generator = org.bouncycastle.asn1.x509.V3TBSCertificateGenerator().apply {
            setSerialNumber(org.bouncycastle.asn1.ASN1Integer(java.math.BigInteger.valueOf(System.nanoTime()).abs()))
            setSignature(algorithm)
            val name = org.bouncycastle.asn1.x500.X500Name("CN=Synthetic TLS enrollment")
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
            load(null, null); setKeyEntry("synthetic", pair.private, "test-only".toCharArray(), arrayOf(certificate))
        }
        val keys = KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm()).apply { init(store, "test-only".toCharArray()) }
        return SSLContext.getInstance("TLS").apply { init(keys.keyManagers, null, null) }
    }

    @Test fun certificateEnrollmentIsAutomaticPersistentAndRejectsChangedIdentityOrWrongName() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        TransportTrust.initialize(context)
        TransportTrust.clear()
        val preferences = context.getSharedPreferences("rovunet_server_trust", android.content.Context.MODE_PRIVATE)
        val original = identity()
        val active = java.util.concurrent.atomic.AtomicReference(original)
        val workers = Executors.newCachedThreadPool()
        val sockets = java.util.concurrent.ConcurrentHashMap.newKeySet<Socket>()
        ServerSocket(0).use { listener ->
            workers.submit {
                while (!listener.isClosed) {
                    val raw = try { listener.accept() } catch (_: Exception) { break }
                    sockets.add(raw)
                    workers.submit {
                        try {
                            (active.get().socketFactory.createSocket(raw, "localhost", listener.localPort, true) as SSLSocket).use { tls ->
                                tls.useClientMode = false; tls.soTimeout = 5000
                                tls.startHandshake(); tls.getOutputStream().write(73)
                            }
                        } catch (_: Exception) { raw.close() }
                        finally { sockets.remove(raw) }
                    }
                }
            }
            fun connect(host: String) = ValidatedTls.wrap(Socket("127.0.0.1", listener.localPort), host, listener.localPort).use {
                assertEquals(73, it.getInputStream().read())
            }
            try {
                // Invalid hostname must not enroll a certificate, even on first use.
                try { connect("wrong.example.invalid"); fail("Wrong hostname accepted") } catch (_: Exception) { }
                assertTrue(preferences.all.isEmpty())
                TransportTrust.allowPrompt = false
                connect("localhost")
                assertNull(TransportTrust.request.value)
                val stored = preferences.all.toMap()
                assertEquals(2, stored.size) // Public certificate and its SPKI fingerprint.
                TransportTrust.initialize(context)
                connect("localhost")
                assertEquals(stored, preferences.all)
                active.set(identity())
                try { connect("localhost"); fail("Changed identity accepted") } catch (_: Exception) { }
                assertEquals(stored, preferences.all)
                active.set(original)
                connect("localhost")
                assertNull(TransportTrust.request.value)
            } finally {
                listener.close()
                sockets.forEach { runCatching { it.close() } }
                workers.shutdownNow()
                workers.awaitTermination(5, java.util.concurrent.TimeUnit.SECONDS)
                TransportTrust.allowPrompt = false
                TransportTrust.clear()
            }
        }
    }
}
