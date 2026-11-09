package app.slipnet.service

import android.content.Context
import app.slipnet.core.*
import app.slipnet.domain.model.*
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.io.File
import javax.inject.Inject
import javax.inject.Singleton
import dagger.hilt.android.qualifiers.ApplicationContext

/** Owns a separate core and private SOCKS endpoint; never touches XrayBridge or the live TUN. */
@Singleton
class StandaloneCoreProbe @Inject constructor(@ApplicationContext private val context: Context) {
    private val slot = Mutex()
    fun supports(profile: ServerProfile) = profile.tunnelType == TunnelType.XRAY && profile.coreLink.isNotBlank()
    suspend fun measure(profile: ServerProfile, valid: () -> Boolean): Long? = slot.withLock {
        if (!supports(profile) || !valid()) return@withLock null
        val core = CoreProcess(diagnostics = false)
        val endpoint = LocalSocksEndpoint.privateCore()
        try {
            val config = CoreLink.runtime(profile.coreLink, endpoint.port, endpoint.user, endpoint.password, null)
            val dir = context.applicationInfo.nativeLibraryDir
            runInterruptible(Dispatchers.IO) {
                check(valid())
                core.start(File(dir, "libveyra_xray.so"), config, File(dir, "libveyra_core_launcher.so"), 6_000).getOrThrow()
            }
            TunnelProbe.medianVerified(valid) { host ->
                TunnelProbe.measureCancellable(endpoint.port, endpoint.user, endpoint.password, host, 8_000)
            }
        } catch (e: CancellationException) { throw e
        } catch (_: Exception) { null
        } finally { withContext(NonCancellable + Dispatchers.IO) { core.stop() } }
    }
}
