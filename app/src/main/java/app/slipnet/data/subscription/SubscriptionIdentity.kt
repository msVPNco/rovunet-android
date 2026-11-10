package app.slipnet.data.subscription

import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import org.json.JSONArray

/** Hash these values before storage. A WS Host shared by CDN paths is not an endpoint identity. */
internal object SubscriptionIdentity {
    fun legacy(profile: ServerProfile): String = with(profile) {
        "derived-v1:" + JSONArray(listOf(tunnelType.value, domain, sshHost, sshPort,
            socks5ServerPort, naivePort, sshUsername, socksUsername, naiveUsername, coreLink)).toString()
    }
    fun derived(profile: ServerProfile): String = if (profile.tunnelType != TunnelType.VLESS) legacy(profile) else with(profile) {
        "derived-v2:" + JSONArray(listOf(tunnelType.value, domain, cdnIp, cdnPort,
            vlessUuid, vlessTransport, vlessWsPath, vlessSecurity, vlessSni,
            sniFragmentEnabled, sniFragmentStrategy, sniFragmentDelayMs)).toString()
    }
}
