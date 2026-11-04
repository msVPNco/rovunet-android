package app.slipnet.tunnel

import android.content.Context
import app.slipnet.core.CoreLink
import app.slipnet.core.CoreProcess
import java.io.File

object XrayBridge {
    private val core = CoreProcess()
    fun start(context: Context, link: String, port: Int, username: String?, password: String?, routing: org.json.JSONObject? = null): Result<Unit> =
        runCatching {
            CoreLink.runtime(link, port, username, password, routing)
        }.fold(
            onSuccess = { core.start(File(context.applicationInfo.nativeLibraryDir, "libveyra_xray.so"), it, File(context.applicationInfo.nativeLibraryDir, "libveyra_core_launcher.so")) },
            onFailure = { Result.failure(it) })
    fun stop() = core.stop()
    fun isRunning() = core.isRunning()
}
