package app.slipnet.tunnel

import android.content.Intent
import android.net.VpnService
import android.os.*

/** One native singleton per dedicated probe process, never the live VPN process. */
open class SlipstreamProbeService : VpnService() {
    override fun onBind(intent: Intent?): IBinder = object : Binder() {
        override fun onTransact(code: Int, data: Parcel, reply: Parcel?, flags: Int): Boolean {
            if (code !in 1..3) return super.onTransact(code, data, reply, flags)
            check(Binder.getCallingUid() == Process.myUid())
            data.enforceInterface(TOKEN)
            when (code) {
                START -> {
                    val args = requireNotNull(data.readBundle(javaClass.classLoader))
                    val result = runCatching {
                        SlipstreamBridge.setVpnService(this@SlipstreamProbeService)
                        val connectivity = getSystemService(android.net.ConnectivityManager::class.java)
                        SlipstreamBridge.proxyOnlyMode = connectivity.allNetworks.none {
                            connectivity.getNetworkCapabilities(it)?.hasTransport(android.net.NetworkCapabilities.TRANSPORT_VPN) == true
                        }
                        SlipstreamBridge.startClient(domain = args.getString("domain").orEmpty(),
                            resolvers = listOf(ResolverConfig(args.getString("host").orEmpty(), args.getInt("port"), args.getBoolean("authoritative"))),
                            tcpListenPort = args.getInt("listen"), congestionControl = args.getString("cc").orEmpty(),
                            keepAliveInterval = args.getInt("keepAlive"),
                            certificatePath = SlipstreamCertificate.materialize(this@SlipstreamProbeService, args.getString("certificate").orEmpty())).getOrThrow()
                        SlipstreamBridge.getClientPort()
                    }
                    reply?.writeNoException()
                    reply?.writeInt(result.getOrDefault(-1))
                }
                READY -> { reply?.writeNoException(); reply?.writeInt(if (SlipstreamBridge.isQuicReady()) 1 else 0) }
                STOP -> {
                    reply?.writeNoException()
                    // This process owns no live connection. OS process teardown closes
                    // all native sockets/tasks even when an upstream thread is draining.
                    Handler(Looper.getMainLooper()).post { Process.killProcess(Process.myPid()) }
                }
            }
            return true
        }
    }
    override fun onDestroy() {
        super.onDestroy()
        Process.killProcess(Process.myPid())
    }
    companion object {
        const val TOKEN = "app.slipnet.SlipstreamProbe"
        const val START = 1
        const val READY = 2
        const val STOP = 3
    }
}
class SlipstreamProbe0 : SlipstreamProbeService()
class SlipstreamProbe1 : SlipstreamProbeService()
class SlipstreamProbe2 : SlipstreamProbeService()
class SlipstreamProbe3 : SlipstreamProbeService()
