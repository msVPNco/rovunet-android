package app.slipnet.tunnel

import android.content.Context
import com.jcraft.jsch.HostKey
import com.jcraft.jsch.HostKeyRepository
import com.jcraft.jsch.UserInfo
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.security.MessageDigest
import java.security.cert.X509Certificate
import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit
import javax.net.ssl.*

/** Private endpoint pins. TLS certificates enroll automatically; SSH host keys retain approval. */
object TransportTrust {
    data class Request(val endpoint: String, val fingerprint: String, internal val answer: CompletableFuture<Boolean> = CompletableFuture())
    private lateinit var preferences: android.content.SharedPreferences
    private val _request = MutableStateFlow<Request?>(null)
    val request = _request.asStateFlow()
    @Volatile var allowPrompt = false
    fun initialize(context: Context) { preferences = context.getSharedPreferences("rovunet_server_trust", Context.MODE_PRIVATE) }
    private val pending = linkedMapOf<String, Request>()
    private var decisionStarted = 0L
    private var completedDecisionNanos = 0L
    val decisionWaitNanos: Long get() = synchronized(pending) {
        completedDecisionNanos + if (pending.isEmpty()) 0L else System.nanoTime() - decisionStarted
    }

    private fun updateRequest() {
        if (pending.isEmpty() && decisionStarted != 0L) {
            completedDecisionNanos += System.nanoTime() - decisionStarted
            decisionStarted = 0L
        }
        _request.value = pending.values.firstOrNull()
    }

    fun answer(request: Request, accepted: Boolean) = synchronized(pending) {
        if (_request.value === request) request.answer.complete(accepted)
    }

    fun cancelPending() = synchronized(pending) {
        pending.values.forEach { it.answer.complete(false) }
        pending.clear()
        updateRequest()
    }

    fun clear() = synchronized(pending) {
        cancelPending()
        check(preferences.edit().clear().commit()) { "Could not reset server identities" }
    }

    private class ApprovalRequired : IllegalStateException("Server identity approval required; connect manually first")
    private fun storageKey(endpoint: String): String =
        MessageDigest.getInstance("SHA-256").digest(endpoint.toByteArray()).joinToString("") { "%02x".format(it) }

    private fun fingerprint(encoded: ByteArray): String = "SHA256:" + android.util.Base64.encodeToString(
        MessageDigest.getInstance("SHA-256").digest(encoded), android.util.Base64.NO_WRAP or android.util.Base64.NO_PADDING)

    private fun verify(endpoint: String, encoded: ByteArray, prompt: Boolean = allowPrompt,
                       storedEndpoint: String = endpoint): Boolean {
        val fingerprint = fingerprint(encoded)
        val key = storageKey(storedEndpoint)
        val request = synchronized(pending) {
            preferences.getString(key, null)?.let { known ->
                check(known == fingerprint) { "Server identity changed; connection refused" }
                return true
            }
            if (!prompt) throw ApprovalRequired()
            pending[endpoint]?.let {
                check(it.fingerprint == fingerprint) { "Server identity changed; connection refused" }
                it
            } ?: Request(endpoint, fingerprint).also {
                if (pending.isEmpty()) decisionStarted = System.nanoTime()
                pending[endpoint] = it
                updateRequest()
            }
        }
        try {
            val accepted = try { request.answer.get(60, TimeUnit.SECONDS) }
                catch (_: java.util.concurrent.TimeoutException) { false }
                catch (_: InterruptedException) { Thread.currentThread().interrupt(); false }
            if (!accepted) return false
            synchronized(pending) {
                val known = preferences.getString(key, null)
                if (known != null) return known == fingerprint
                if (pending[endpoint] !== request) return false
                check(preferences.edit().putString(key, fingerprint).commit()) { "Could not save server identity" }
                return true
            }
        } finally {
            synchronized(pending) {
                if (pending[endpoint] === request) {
                    pending.remove(endpoint)
                    updateRequest()
                }
            }
        }
    }
    val sshKeys = sshRepository(manual = true)
    val scannerSshKeys = sshRepository(manual = false)

    private fun sshRepository(manual: Boolean): HostKeyRepository = object : HostKeyRepository {
        override fun check(host: String, key: ByteArray): Int = try {
            if (verify("SSH · $host", key, manual && allowPrompt)) HostKeyRepository.OK else HostKeyRepository.NOT_INCLUDED
        } catch (_: ApprovalRequired) { HostKeyRepository.NOT_INCLUDED }
        catch (_: Exception) { HostKeyRepository.CHANGED }
        override fun add(key: HostKey, ui: UserInfo?) = Unit
        override fun remove(host: String, type: String?) = Unit
        override fun remove(host: String, type: String?, key: ByteArray?) = Unit
        override fun getKnownHostsRepositoryID() = "Rovunet private server pins"
        override fun getHostKey(): Array<HostKey> = emptyArray()
        override fun getHostKey(host: String?, type: String?): Array<HostKey> = emptyArray()
    }
    /** Called only after the TLS handshake and hostname verification have succeeded. */
    fun rememberTlsCertificate(host: String, port: Int, chain: Array<X509Certificate>, privateCertificate: Boolean) = synchronized(pending) {
        require(chain.isNotEmpty())
        chain.first().checkValidity()
        val endpoint = "TLS · $host:$port"
        val legacyKey = storageKey(endpoint)
        val spkiKey = storageKey("$endpoint · SPKI/v1")
        preferences.getString(legacyKey, null)?.let {
            check(it == fingerprint(chain.first().encoded)) { "Server identity changed; connection refused" }
        }
        val pin = fingerprint(chain.first().publicKey.encoded)
        preferences.getString(spkiKey, null)?.let {
            check(it == pin) { "Server identity changed; connection refused" }
        }
        val certificateKey = "$spkiKey.certificate"
        val certificate = android.util.Base64.encodeToString(chain.first().encoded, android.util.Base64.NO_WRAP)
        val pinIdentity = privateCertificate || preferences.contains(spkiKey) || preferences.contains(legacyKey)
        if ((pinIdentity && preferences.getString(spkiKey, null) != pin) || preferences.getString(certificateKey, null) != certificate || preferences.contains(legacyKey)) {
            val edit = preferences.edit().putString(certificateKey, certificate).remove(legacyKey)
            if (pinIdentity) edit.putString(spkiKey, pin)
            check(edit.commit()) { "Could not save server certificate" }
        }
    }

    fun tlsFactory(host: String, port: Int, onPrivateCertificate: () -> Unit): SSLSocketFactory {
        val system = (TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm()).apply { init(null as java.security.KeyStore?) }).trustManagers.filterIsInstance<X509TrustManager>().single()
        val manager = object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<X509Certificate>, auth: String) = system.checkClientTrusted(chain, auth)
            override fun getAcceptedIssuers(): Array<X509Certificate> = system.acceptedIssuers
            override fun checkServerTrusted(chain: Array<X509Certificate>, auth: String) {
                val endpoint = "TLS · $host:$port"
                val spkiEndpoint = "$endpoint · SPKI/v1"
                require(chain.isNotEmpty())
                synchronized(pending) {
                    val legacyKey = storageKey(endpoint)
                    val spkiKey = storageKey(spkiEndpoint)
                    val legacy = preferences.getString(legacyKey, null)
                    if (legacy != null && !preferences.contains(spkiKey)) {
                        // Do not infer an old pin's public key from a different certificate.
                        chain.forEach { it.checkValidity() }
                        check(legacy == fingerprint(chain.first().encoded)) { "Server identity changed; connection refused" }
                        onPrivateCertificate()
                    }
                    if (preferences.contains(spkiKey)) {
                        chain.forEach { it.checkValidity() }
                        if (!verify(endpoint, chain.first().publicKey.encoded, false, spkiEndpoint)) throw java.security.cert.CertificateException("Server identity changed")
                        onPrivateCertificate()
                        return
                    }
                }
                try { system.checkServerTrusted(chain, auth) }
                catch (_: java.security.cert.CertificateException) {
                    // First-use private certificates are enrolled after proof of possession and
                    // hostname verification in ValidatedTls, never during this callback.
                    chain.forEach { it.checkValidity() }
                    onPrivateCertificate()
                }
            }
        }
        return SSLContext.getInstance("TLS").apply { init(null, arrayOf(manager), null) }.socketFactory
    }
}
