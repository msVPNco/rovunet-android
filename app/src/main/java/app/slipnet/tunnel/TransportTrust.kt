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

/** Private, endpoint-scoped pins. First use requires an explicit user decision. Changed pins fail closed. */
object TransportTrust {
    data class Request(val endpoint: String, val fingerprint: String, internal val answer: CompletableFuture<Boolean> = CompletableFuture())
    private lateinit var preferences: android.content.SharedPreferences
    private val _request = MutableStateFlow<Request?>(null)
    val request = _request.asStateFlow()
    @Volatile var allowPrompt = false
    fun initialize(context: Context) { preferences = context.getSharedPreferences("rovunet_server_trust", Context.MODE_PRIVATE) }
    fun answer(accepted: Boolean) { _request.value?.answer?.complete(accepted) }
    fun clear() { preferences.edit().clear().commit() }
    @Synchronized private fun verify(endpoint: String, encoded: ByteArray): Boolean {
        val fingerprint = "SHA256:" + android.util.Base64.encodeToString(MessageDigest.getInstance("SHA-256").digest(encoded), android.util.Base64.NO_WRAP or android.util.Base64.NO_PADDING)
        val key = MessageDigest.getInstance("SHA-256").digest(endpoint.toByteArray()).joinToString("") { "%02x".format(it) }
        val known = preferences.getString(key, null)
        if (known != null) {
            check(MessageDigest.isEqual(known.toByteArray(), fingerprint.toByteArray())) { "Server identity changed; connection refused" }
            return true
        }
        check(allowPrompt) { "Server identity approval required; connect manually first" }
        val pending = Request(endpoint, fingerprint)
        _request.value = pending
        return try {
            val accepted = pending.answer.get(60, TimeUnit.SECONDS)
            if (accepted) check(preferences.edit().putString(key, fingerprint).commit()) { "Could not save server identity" }
            accepted
        } finally { _request.value = null }
    }
    val sshKeys = object : HostKeyRepository {
        override fun check(host: String, key: ByteArray): Int = try {
            if (verify("SSH · $host", key)) HostKeyRepository.OK else HostKeyRepository.NOT_INCLUDED
        } catch (_: Exception) { HostKeyRepository.CHANGED }
        override fun add(key: HostKey, ui: UserInfo?) = Unit
        override fun remove(host: String, type: String?) = Unit
        override fun remove(host: String, type: String?, key: ByteArray?) = Unit
        override fun getKnownHostsRepositoryID() = "Rovunet private server pins"
        override fun getHostKey(): Array<HostKey> = emptyArray()
        override fun getHostKey(host: String?, type: String?): Array<HostKey> = emptyArray()
    }
    fun tlsFactory(host: String, port: Int): SSLSocketFactory {
        val system = (TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm()).apply { init(null as java.security.KeyStore?) }).trustManagers.filterIsInstance<X509TrustManager>().single()
        val manager = object : X509TrustManager {
            override fun checkClientTrusted(chain: Array<X509Certificate>, auth: String) = system.checkClientTrusted(chain, auth)
            override fun getAcceptedIssuers(): Array<X509Certificate> = system.acceptedIssuers
            override fun checkServerTrusted(chain: Array<X509Certificate>, auth: String) {
                try { system.checkServerTrusted(chain, auth) }
                catch (e: java.security.cert.CertificateException) {
                    require(chain.isNotEmpty())
                    chain.forEach { it.checkValidity() }
                    if (!verify("TLS · $host:$port", chain.first().encoded)) throw e
                }
            }
        }
        return SSLContext.getInstance("TLS").apply { init(null, arrayOf(manager), null) }.socketFactory
    }
}
