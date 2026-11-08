package app.slipnet.tunnel

import android.os.SystemClock
import app.slipnet.domain.model.*
import com.jcraft.jsch.JSch
import com.jcraft.jsch.ProxySOCKS5
import kotlinx.coroutines.*
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import java.net.ServerSocket

/** Each resolver owns an independent native client; never touches the live VPN's SoarBridge. */
internal object SoarResolverProbe {
    // One cap for actual native clients, including both UDP/TCP and all scan phases.
    private val nativeSlots = Semaphore(8)
    suspend fun test(host: String, port: Int, profile: ServerProfile, transport: DnsTransport,
                     timeoutMs: Long, testUrl: String, fullVerification: Boolean,
                     onPhase: (String) -> Unit = {}): E2eTestResult = withContext(Dispatchers.IO) {
        val started = SystemClock.elapsedRealtime()
        if (profile.needsCredentials || profile.isExpired) return@withContext E2eTestResult(errorMessage = "Profile unavailable")
        var client: soar.SoarClient? = null
        try {
            val result = withTimeoutOrNull(timeoutMs) { nativeSlots.withPermit { try {
                val listenPort = if (fullVerification) ServerSocket(0, 1, java.net.InetAddress.getLoopbackAddress()).use { it.localPort } else 0
                val address = if (host.contains(':')) "[$host]:$port" else "$host:$port"
                val instance = soar.Soar.newClient(address, profile.domain, profile.dnsttPublicKey, "127.0.0.1:$listenPort")
                client = instance
                app.slipnet.util.ScanDiagnostics.nativeStarted()
                app.slipnet.util.ScanDiagnostics.probe()
                // Test precisely this resolver and this transport. Discovery/fallback could certify a different path.
                instance.setDiscovery(false, "")
                instance.setTCPMode(if (transport == DnsTransport.TCP) SoarConfig.TCP_ONLY else SoarConfig.TCP_OFF)
                onPhase("Tunnel handshake...")
                instance.start()
                while (!instance.isEstablished || instance.verifiedPathCount() < 1L) { ensureActive(); delay(50) }
                val setup = SystemClock.elapsedRealtime() - started
                if (!fullVerification) return@withTimeoutOrNull E2eTestResult(success = true,
                    tunnelSetupMs = setup, totalMs = setup, phase = E2eTestPhase.COMPLETED)
                val remaining = (timeoutMs - setup).coerceAtLeast(1)
                val checked = if (profile.tunnelType == TunnelType.SOAR_SSH) {
                    onPhase("SSH connect...")
                    ssh(listenPort, profile, remaining)
                } else {
                    onPhase("HTTP request...")
                    http(listenPort, testUrl, remaining)
                }
                checked.copy(tunnelSetupMs = setup, totalMs = SystemClock.elapsedRealtime() - started)
            } finally {
                if (client != null) {
                    try { client?.stop() } finally {
                        client = null
                        app.slipnet.util.ScanDiagnostics.nativeFinished()
                    }
                }
            } } }
            result ?: E2eTestResult(totalMs = SystemClock.elapsedRealtime() - started,
                errorMessage = "Soar handshake or verification timed out", phase = E2eTestPhase.TUNNEL_SETUP)
        } catch (e: CancellationException) { throw e
        } catch (_: Exception) {
            E2eTestResult(totalMs = SystemClock.elapsedRealtime() - started,
                errorMessage = "Soar resolver verification failed", phase = E2eTestPhase.TUNNEL_SETUP)
        } finally {
            // Stop closes this client's listener, DNS paths and pending streams, including cancellation.
            client?.stop()
        }
    }

    /** Repeated real Soar handshakes; DNSTT Prism's HMAC packets are not a Soar protocol. */
    suspend fun verify(host: String, port: Int, profile: ServerProfile, transport: DnsTransport,
                       timeoutMs: Long, probeCount: Int, threshold: Int): Int =
        verifyDetailed(host, port, profile, transport, timeoutMs, probeCount, threshold).passed

    suspend fun verifyDetailed(host: String, port: Int, profile: ServerProfile, transport: DnsTransport,
                       timeoutMs: Long, probeCount: Int, threshold: Int): ProbeVerification {
        val started = SystemClock.elapsedRealtime()
        var passed = 0
        var attempted = 0
        for (index in 0 until probeCount) {
            currentCoroutineContext().ensureActive()
            val left = timeoutMs - (SystemClock.elapsedRealtime() - started)
            if (passed >= threshold || left <= 0 || passed + probeCount - index < threshold) break
            val allowance = minOf(left, (timeoutMs / threshold.coerceAtLeast(1)).coerceAtLeast(200L))
            attempted++
            if (test(host, port, profile, transport, allowance, "", false).success) passed++
        }
        return ProbeVerification(passed, attempted)
    }

    private suspend fun http(port: Int, url: String, timeoutMs: Long): E2eTestResult {
        val elapsed = app.slipnet.core.TunnelProbe.measureUrlCancellable(port, url, timeoutMs.coerceIn(100, 30_000).toInt())
        return E2eTestResult(success = true, httpStatusCode = 204, httpLatencyMs = elapsed, phase = E2eTestPhase.COMPLETED)
    }

    private suspend fun ssh(port: Int, profile: ServerProfile, timeoutMs: Long): E2eTestResult = coroutineScope {
        val startup = SshStartup()
        val started = SystemClock.elapsedRealtime()
        try {
            val work = async(Dispatchers.IO) {
                ensureActive()
                val jsch = JSch()
                if (profile.sshAuthType == SshAuthType.KEY) jsch.addIdentity("scanner-key", profile.sshPrivateKey.toByteArray(), null,
                    profile.sshKeyPassphrase.takeIf { it.isNotEmpty() }?.toByteArray())
                val session = jsch.getSession(profile.sshUsername, profile.sshHost, profile.sshPort)
                startup.ownSession(session)
                session.setSocketFactory(startup.socketFactory(timeoutMs.toInt().coerceAtLeast(1)))
                session.setProxy(startup.ownProxy(ProxySOCKS5("127.0.0.1", port)))
                session.setHostKeyAlias(profile.sshTrustIdentity)
                session.setHostKeyRepository(TransportTrust.scannerSshKeys)
                session.setConfig("StrictHostKeyChecking", "yes")
                if (profile.sshAuthType == SshAuthType.KEY) session.setConfig("PreferredAuthentications", "publickey")
                else session.setPassword(profile.sshPassword)
                startup.requireActive()
                session.connect(timeoutMs.toInt().coerceAtLeast(1))
                E2eTestResult(success = session.isConnected, httpLatencyMs = SystemClock.elapsedRealtime() - started,
                    phase = E2eTestPhase.COMPLETED)
            }
            work.await()
        } finally { startup.cancel() }
    }
}
