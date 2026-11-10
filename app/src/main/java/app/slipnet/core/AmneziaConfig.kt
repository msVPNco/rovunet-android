package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject

/** One userspace peer. No scripts, kernel configuration or arbitrary core configuration. */
internal object AmneziaConfig {
    val labels = linkedMapOf("jc" to "Jc", "jmin" to "Jmin", "jmax" to "Jmax", "s1" to "S1", "s2" to "S2", "s3" to "S3", "s4" to "S4",
        "h1" to "H1", "h2" to "H2", "h3" to "H3", "h4" to "H4", "i1" to "I1", "i2" to "I2", "i3" to "I3", "i4" to "I4", "i5" to "I5",
        "j1" to "J1", "j2" to "J2", "j3" to "J3", "itime" to "Itime", "version" to "Version",
        "headerprotectionkey" to "HeaderProtectionKey", "contentpaddingaddition" to "ContentPaddingAddition",
        "rekeyaftertime" to "RekeyAfterTime", "rekeytimeout" to "RekeyTimeout", "rejectaftertime" to "RejectAfterTime",
        "keepalivetimeout" to "KeepaliveTimeout", "maxhandshakeattempts" to "MaxHandshakeAttempts", "randomtrailers" to "RandomTrailers", "disablecookies" to "DisableCookies")
    private val mapped = mapOf("headerprotectionkey" to "header-protection-key", "contentpaddingaddition" to "content-padding-addition",
        "rekeyaftertime" to "rekey-after-time", "rekeytimeout" to "rekey-timeout", "rejectaftertime" to "reject-after-time",
        "keepalivetimeout" to "keepalive-timeout", "maxhandshakeattempts" to "max-handshake-attempts", "randomtrailers" to "random-trailers", "disablecookies" to "disable-cookies")
    fun handles(text: String) = text.lineSequence().any { it.substringBefore('=').trim().lowercase() in labels }
    fun parse(text: String): CoreLink.Parsed {
        require(text.length <= 65_536)
        val options = linkedMapOf<String, String>()
        var section = ""
        val plain = text.lines().filter { raw ->
            val line = raw.substringBefore('#').trim()
            if (line.startsWith('[')) section = line.lowercase()
            val name = line.substringBefore('=').trim().lowercase()
            if (name !in labels) true else {
                require(section == "[interface]" && '=' in line && name !in options) { "UNSUPPORTED_OPTION" }
                val value = line.substringAfter('=').trim()
                require(value.length <= 2048 && value.none(Char::isISOControl))
                options[name] = value; false
            }
        }.joinToString("\n")
        val base = CoreLink.parse(plain)
        val wg = base.outbound.getJSONObject("settings")
        val peer = wg.getJSONArray("peers").getJSONObject(0)
        val endpoint = java.net.URI("wg://" + peer.getString("endpoint"))
        val settings = JSONObject().put("name", "proxy").put("type", "wireguard").put("server", base.host).put("port", endpoint.port)
            .put("private-key", wg.getString("secretKey")).put("public-key", peer.getString("publicKey"))
            .put("allowed-ips", peer.getJSONArray("allowedIPs")).put("mtu", wg.getInt("mtu")).put("udp", true)
        peer.optString("preSharedKey").takeIf(String::isNotBlank)?.let { settings.put("pre-shared-key", it) }
        if (peer.has("keepAlive")) settings.put("persistent-keepalive", peer.getInt("keepAlive"))
        val addresses = wg.getJSONArray("address")
        for (i in 0 until addresses.length()) {
            val address = addresses.getString(i); val field = if (':' in address) "ipv6" else "ip"
            require(!settings.has(field)) { "UNSUPPORTED_OPTION" }; settings.put(field, address)
        }
        wg.optJSONArray("remoteDNS")?.let { settings.put("dns", it).put("remote-dns-resolve", true) }
        val awg = JSONObject()
        val v3 = options.containsKey("headerprotectionkey") || options["version"] in setOf("3", "3.0", "3.1")
        for ((key, value) in options) {
            val target = mapped[key] ?: key
            when {
                key == "version" -> require(value in setOf("1", "1.0", "1.5", "2", "2.0", "3", "3.0", "3.1"))
                key == "headerprotectionkey" -> { require(app.slipnet.data.export.WireBase64.decode(value).size == 32); awg.put(target, value) }
                key in setOf("randomtrailers", "disablecookies") -> { require(value.lowercase() in setOf("true", "false", "0", "1")); awg.put(target, value.lowercase() in setOf("true", "1")) }
                key in setOf("jc", "jmin", "jmax", "s1", "s2", "s3", "s4", "itime") -> {
                    val n = value.toLong(); require(n in 0..65535); awg.put(target, n)
                }
                key.startsWith('h') || key in mapped.keys -> {
                    require(value.matches(Regex("[0-9]+(?:-[0-9]+)?"))) { "UNSUPPORTED_OPTION" }
                    val range = value.split('-').map(String::toLong)
                    require(range.all { it in 0..0xffffffffL } && range.first() <= range.last())
                    awg.put(target, value)
                }
                else -> awg.put(target, value) // I1-I5/J1-J3 packet specifications are validated by the pinned AWG engine.
            }
        }
        if (v3) { require(options.keys.none { it in setOf("j1", "j2", "j3", "itime") }); awg.put("version", 3) }
        else require(options.keys.none { it in mapped.keys || it == "headerprotectionkey" }) { "UNSUPPORTED_OPTION" }
        require((options["jmin"]?.toInt() ?: 0) <= (options["jmax"]?.toInt() ?: 65535))
        settings.put("amnezia-wg-option", awg)
        return CoreLink.Parsed("AmneziaWG", base.host, JSONObject().put("protocol", "amneziawg").put("settings", settings))
    }
}
