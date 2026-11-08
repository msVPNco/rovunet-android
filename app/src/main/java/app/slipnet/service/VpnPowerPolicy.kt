package app.slipnet.service

import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType

/** Keep DNS/cover-traffic timing intact; let an idle TCP connection sleep with the screen off. */
internal object VpnPowerPolicy {
    fun continuous(profile: ServerProfile): Boolean = profile.wsPaddingEnabled || profile.tunnelType in setOf(
        TunnelType.SLIPSTREAM, TunnelType.SLIPSTREAM_SSH, TunnelType.DNSTT, TunnelType.DNSTT_SSH,
        TunnelType.NOIZDNS, TunnelType.NOIZDNS_SSH, TunnelType.VAYDNS, TunnelType.VAYDNS_SSH,
        TunnelType.SOAR, TunnelType.SOAR_SSH, TunnelType.SNOWFLAKE)
    fun holdCpu(continuous: Boolean, screenOn: Boolean, idleMs: Long): Boolean =
        continuous || screenOn || idleMs < 60_000
    fun sampleInterval(screenOn: Boolean, idleTicks: Int): Long = when {
        !screenOn && idleTicks >= 6 -> 60_000
        !screenOn && idleTicks >= 3 -> 30_000
        !screenOn -> 10_000
        idleTicks >= 3 -> 5_000
        else -> 1_000
    }
}
