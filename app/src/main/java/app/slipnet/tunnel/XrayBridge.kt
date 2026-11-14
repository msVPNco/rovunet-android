package app.slipnet.tunnel

import android.content.Context
import app.slipnet.core.CoreLink
import app.slipnet.core.LocalSocksEndpoint
import app.slipnet.core.CoreProcess
import java.io.File

object XrayBridge {
    private val core = CoreProcess()
    @Volatile var activeEndpoint: LocalSocksEndpoint? = null
        private set

    fun start(context: Context, link: String, endpoint: LocalSocksEndpoint, routing: org.json.JSONObject?): Result<Unit> =
        runCatching {
            require(!endpoint.user.isNullOrEmpty() && !endpoint.password.isNullOrEmpty()) { "Xray requires local SOCKS authentication" }
            val config = CoreLink.runtime(link, endpoint.port, endpoint.user, endpoint.password, routing)
            val nativeDir = context.applicationInfo.nativeLibraryDir
            core.start(File(nativeDir, "libveyra_xray.so"), config, File(nativeDir, "libveyra_core_launcher.so"), workDirectory = context.cacheDir,
                certificateDirectory = File(context.filesDir, "native-certificates")).getOrThrow()
            activeEndpoint = endpoint
        }

    fun stop() {
        core.stop()
        activeEndpoint = null
    }
    fun isRunning() = core.isRunning()
}
