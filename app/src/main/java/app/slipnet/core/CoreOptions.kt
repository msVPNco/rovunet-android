package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject

/** Bounded subset of the pinned core's schema. Unsupported effective settings are rejected. */
internal object CoreOptions {
    private fun keys(o: JSONObject, allowed: Set<String>) = require(o.keys().asSequence().all { it in allowed }) { "UNSUPPORTED_OPTION" }
    fun cipherSuites(raw: String): String {
        val names = raw.split(':').map(String::trim)
        val supported = setOf("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
            "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256", "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
            "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256", "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256",
            "TLS_AES_128_GCM_SHA256", "TLS_AES_256_GCM_SHA384", "TLS_CHACHA20_POLY1305_SHA256")
        require(names.isNotEmpty() && names.size <= 16 && names.all { it in supported }) { "UNSUPPORTED_OPTION" }
        return raw
    }
    private fun bounded(value: Any, depth: Int = 0) {
        require(depth <= 8) { "UNSUPPORTED_OPTION" }
        when (value) {
            is JSONObject -> { require(value.length() <= 32); value.keys().forEach { bounded(value.get(it), depth + 1) } }
            is JSONArray -> { require(value.length() <= 64); for (i in 0 until value.length()) bounded(value.get(i), depth + 1) }
            is String -> require(value.length <= 4096)
            is Number -> require(value.toDouble().isFinite() && value.toDouble() >= 0)
            is Boolean -> Unit
            else -> error("UNSUPPORTED_OPTION")
        }
    }
    fun finalMask(raw: String): JSONObject {
        require(raw.length <= 16_384) { "UNSUPPORTED_OPTION" }
        val result = JSONObject(raw); bounded(result)
        keys(result, setOf("tcp", "udp", "quicParams"))
        for (network in listOf("tcp", "udp")) result.optJSONArray(network)?.let { masks ->
            require(masks.length() <= 4)
            for (i in 0 until masks.length()) {
                val mask = masks.getJSONObject(i); keys(mask, setOf("type", "settings"))
                val type = mask.getString("type")
                require(type in if (network == "tcp") setOf("fragment") else setOf("salamander", "udphop", "mkcp-legacy", "noise")) { "UNSUPPORTED_OPTION" }
                val settings = mask.optJSONObject("settings") ?: JSONObject()
                val allowed = when (type) {
                    "fragment" -> setOf("packets", "length", "delay", "lengths", "delays", "maxSplit")
                    "salamander" -> setOf("password", "packetSize")
                    "udphop" -> setOf("mode", "interval", "remoteIPs", "remotePorts")
                    "mkcp-legacy" -> setOf("header", "value")
                    else -> setOf("reset", "noise") // Embedded Lua/scripts are not imported.
                }
                keys(settings, allowed)
                settings.optJSONArray("noise")?.let { a -> for (j in 0 until a.length())
                    keys(a.getJSONObject(j), setOf("rand", "randRange", "type", "packet", "delay")) }
            }
        }
        for (network in listOf("tcp", "udp")) require(!result.has(network) || result.get(network) is JSONArray)
        result.optJSONObject("quicParams")?.let { q ->
            keys(q, setOf("congestion", "bbrProfile", "brutalUp", "brutalDown", "brutalDisableLossCompensation",
                "initStreamReceiveWindow", "maxStreamReceiveWindow", "initConnectionReceiveWindow", "maxConnectionReceiveWindow",
                "maxIdleTimeout", "keepAlivePeriod", "disablePathMTUDiscovery", "disableChromeParrot", "disableGSO",
                "maxIncomingStreams", "disableStatelessReset"))
            require(!q.has("congestion") || q.getString("congestion") in setOf("reno", "bbr", "brutal", "force-brutal"))
        }
        require(!result.has("quicParams") || result.get("quicParams") is JSONObject)
        return result
    }
}
