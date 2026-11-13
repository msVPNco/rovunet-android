package app.slipnet.util

import android.content.Context
import app.slipnet.core.ProxySocketFactory
import app.slipnet.core.UpdateNetworkPolicy
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.ConnectionState
import app.slipnet.domain.model.TunnelType
import app.slipnet.service.VpnConnectionManager
import app.slipnet.tunnel.XrayBridge
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow
import okhttp3.*
import java.io.IOException
import java.net.InetAddress
import java.net.Proxy
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

/** A request group pins one VPN session. It never retries using a different or direct route. */
@Singleton
class UpdateNetwork @Inject constructor(
    private val connection: VpnConnectionManager,
    private val preferences: PreferencesDataStore
) {
    @EntryPoint @InstallIn(SingletonComponent::class)
    interface Access { fun updates(): UpdateNetwork }

    companion object {
        fun from(context: Context): UpdateNetwork =
            EntryPointAccessors.fromApplication(context.applicationContext, Access::class.java).updates()
    }

    suspend fun route(): Route {
        val session = connection.currentSessionId
        val state = connection.connectionState.value
        val builder = OkHttpClient.Builder().proxy(Proxy.NO_PROXY)
            .followRedirects(false).followSslRedirects(false).retryOnConnectionFailure(false)
            .connectTimeout(20, TimeUnit.SECONDS).readTimeout(45, TimeUnit.SECONDS)
            .callTimeout(20, TimeUnit.MINUTES).connectionPool(ConnectionPool(0, 1, TimeUnit.SECONDS))
        if (state is ConnectionState.Connected) {
            // Chain final SOCKS ownership is not exposed yet. Never silently bypass it.
            check(state.chainId <= 0) { "Update route unavailable" }
            val endpoint = if (state.profile.tunnelType == TunnelType.XRAY)
                XrayBridge.activeEndpoint ?: error("Update route unavailable")
            else preferences.configuredSocksEndpoint()
            builder.socketFactory(ProxySocketFactory(endpoint))
                .dns(object : Dns {
                    override fun lookup(hostname: String): List<InetAddress> =
                        listOf(InetAddress.getByAddress(hostname, byteArrayOf(0, 0, 0, 1)))
                })
        } else check(state.isDisconnected) { "Update route unavailable" }
        return Route(builder.build(), connection.connectionState) {
            connection.currentSessionId == session && connection.connectionState.value == state
        }.also { it.ensureRoute() }
    }

    class Route internal constructor(private val client: OkHttpClient,
        private val changes: Flow<ConnectionState> = emptyFlow(), private val valid: () -> Boolean) {
        private sealed interface Step<out T> {
            data class Redirect(val address: String) : Step<Nothing>
            data class Done<T>(val value: T) : Step<T>
        }
        fun ensureRoute() { if (!valid()) throw IOException("Update route changed") }

        suspend fun <T> get(address: String, offset: Long = 0, read: suspend (Response) -> T): T {
            require(UpdateNetworkPolicy.allows(address)) { "Untrusted update URL" }
            var target = address
            repeat(6) { hop ->
                ensureRoute()
                require(UpdateNetworkPolicy.allows(target, redirect = hop > 0)) { "Untrusted update redirect" }
                val request = Request.Builder().url(target)
                    .header("User-Agent", "Rovunet-Android").header("Cache-Control", "no-cache")
                    .header("Accept-Encoding", "identity")
                    .apply { if (offset > 0) header("Range", "bytes=$offset-") }.build()
                val step = useResponse(request) { response ->
                    if (response.code in 300..399) {
                        check(hop < 5) { "Too many update redirects" }
                        Step.Redirect(response.request.url.resolve(response.header("Location") ?: error("Download unavailable"))
                            ?.toString() ?: error("Download unavailable"))
                    } else Step.Done(read(response))
                }
                when (step) {
                    is Step.Done -> return step.value
                    is Step.Redirect -> target = step.address
                }
            }
            error("Too many update redirects")
        }

        private suspend fun <T> useResponse(request: Request, read: suspend (Response) -> T): T = coroutineScope {
            val call = client.newCall(request)
            // Cancel blocked reads as well as connection setup, even when the body has begun streaming.
            val cancellation = launch(Dispatchers.Default, start = CoroutineStart.UNDISPATCHED) {
                try { awaitCancellation() } finally { call.cancel() }
            }
            val routeWatch = launch(Dispatchers.Default) {
                changes.collect { if (!valid()) call.cancel() }
            }
            try {
                val response = suspendCancellableCoroutine<Response> { continuation ->
                    continuation.invokeOnCancellation { call.cancel() }
                    call.enqueue(object : Callback {
                        override fun onFailure(call: Call, e: IOException) {
                            if (continuation.isActive) continuation.resumeWithException(e)
                        }
                        override fun onResponse(call: Call, response: Response) {
                            continuation.resume(response) { response.close() }
                        }
                    })
                }
                response.use { ensureRoute(); read(it).also { ensureRoute() } }
            } catch (error: IOException) {
                currentCoroutineContext().ensureActive()
                ensureRoute()
                throw error
            } finally { routeWatch.cancel(); cancellation.cancel() }
        }
    }
}
