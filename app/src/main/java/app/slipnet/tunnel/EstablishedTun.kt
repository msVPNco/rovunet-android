package app.slipnet.tunnel

import android.net.VpnService
import android.os.ParcelFileDescriptor
import java.io.Closeable

/** The forwarder must use the MTU applied to this particular interface, including on reconnect. */
internal class EstablishedTun private constructor(val descriptor: ParcelFileDescriptor, val mtu: Int) : Closeable {
    override fun close() = descriptor.close()

    companion object {
        fun establish(builder: VpnService.Builder, mtu: Int): EstablishedTun? =
            builder.setMtu(mtu).establish()?.let { EstablishedTun(it, mtu) }
    }
}
