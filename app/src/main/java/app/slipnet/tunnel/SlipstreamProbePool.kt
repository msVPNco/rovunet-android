package app.slipnet.tunnel

import android.content.*
import android.os.*
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel
import app.slipnet.domain.model.ServerProfile
import java.net.ServerSocket

/** Slots are returned only after process death. A stalled worker cannot collide with its successor. */
object SlipstreamProbePool {
    private val services = listOf(SlipstreamProbe0::class.java, SlipstreamProbe1::class.java, SlipstreamProbe2::class.java, SlipstreamProbe3::class.java)
    private val slots = Channel<Int>(4).apply { repeat(4) { trySend(it) } }
    suspend fun <T> use(context: Context, action: suspend (Client) -> T): T {
        val slot = slots.receive()
        val connected = CompletableDeferred<IBinder>()
        val died = CompletableDeferred<Unit>()
        var remote: IBinder? = null
        val connection = object : ServiceConnection {
            override fun onServiceConnected(name: ComponentName, binder: IBinder) {
                remote = binder
                binder.linkToDeath({ died.complete(Unit) }, 0)
                connected.complete(binder)
            }
            override fun onServiceDisconnected(name: ComponentName) { died.complete(Unit) }
            override fun onNullBinding(name: ComponentName) { connected.completeExceptionally(IllegalStateException("Probe unavailable")) }
        }
        var bound = false
        try {
            bound = context.bindService(Intent(context, services[slot]), connection, Context.BIND_AUTO_CREATE)
            check(bound) { "Probe unavailable" }
            val binder = withTimeout(10000) { connected.await() }
            return action(Client(binder))
        } finally {
            withContext(NonCancellable + Dispatchers.IO) {
                val binder = remote
                if (binder != null && binder.isBinderAlive) runCatching { Client(binder).stop() }
                if (bound) runCatching { context.unbindService(connection) }
                // With no connected binder on a cancelled bind, unbind cancels the
                // connection; Service.onDestroy tears down any created process.
                val released = binder == null || !binder.isBinderAlive || withTimeoutOrNull(5000) { died.await(); true } == true
                if (released) slots.send(slot)
            }
        }
    }
    class Client(private val binder: IBinder) {
        private var port = 0
        private fun call(code: Int, args: Bundle? = null): Int {
            val input = Parcel.obtain(); val output = Parcel.obtain()
            try {
                input.writeInterfaceToken(SlipstreamProbeService.TOKEN)
                if (args != null) input.writeBundle(args)
                check(binder.transact(code, input, output, 0))
                output.readException()
                return if (code == SlipstreamProbeService.STOP) 0 else output.readInt()
            } finally { input.recycle(); output.recycle() }
        }
        fun start(profile: ServerProfile, host: String, resolverPort: Int, listenPort: Int): Result<Unit> = runCatching {
            port = call(SlipstreamProbeService.START, Bundle().apply {
                putString("domain", profile.domain); putString("host", host); putInt("port", resolverPort)
                putInt("listen", listenPort); putBoolean("authoritative", profile.authoritativeMode)
                putString("cc", profile.congestionControl.value); putInt("keepAlive", profile.keepAliveInterval)
                putString("certificate", profile.slipstreamCertificate)
            })
            check(port > 0) { "Slipstream native startup failed" }
        }
        fun isQuicReady() = call(SlipstreamProbeService.READY) == 1
        fun getClientPort() = port
        fun stop() { call(SlipstreamProbeService.STOP) }
    }
}
