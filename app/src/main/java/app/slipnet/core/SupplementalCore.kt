package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject
import java.net.URI
import java.net.URLDecoder
import java.util.UUID

/** Explicit URI subsets only. Never pass arbitrary provider YAML to the auxiliary process. */
internal object SupplementalCore {
    val schemes = setOf("anytls", "tuic", "mierus")
    fun handles(text: String) = text.substringBefore("://").lowercase() in schemes
    fun parse(text: String): CoreLink.Parsed {
        val uri = URI(text)
        val scheme = uri.scheme.lowercase()
        require(scheme in schemes && text.length <= 65536)
        val host = uri.host?.removeSurrounding("[", "]") ?: error("Missing host")
        require(host.isNotBlank() && uri.rawPath.orEmpty() in setOf("", "/"))
        fun decode(s: String) = URLDecoder.decode(s.replace("+", "%2B"), "UTF-8")
        val q = linkedMapOf<String, String>()
        for (part in uri.rawQuery.orEmpty().split('&').filter(String::isNotBlank)) {
            val pair = part.split('=', limit = 2)
            val key = decode(pair[0])
            require(key !in q) { "UNSUPPORTED_OPTION" }
            q[key] = decode(pair.getOrElse(1) { "" })
        }
        fun only(vararg names: String) = require(q.keys.all { it in names }) { "UNSUPPORTED_OPTION" }
        fun boolean(key: String, target: String, out: JSONObject) {
            q[key]?.let { require(it.lowercase() in setOf("true", "false", "0", "1")) { "UNSUPPORTED_OPTION" }; out.put(target, it.lowercase() in setOf("true", "1")) }
        }
        val auth = decode(uri.rawUserInfo ?: error("Missing credentials"))
        require(auth.isNotBlank() && auth.none(Char::isISOControl))
        val port = if (scheme == "mierus") {
            require(uri.port == -1 && q["port"] != null && q["profile"] != null) { "UNSUPPORTED_OPTION" }
            q.getValue("port").toInt()
        } else if (uri.port < 0) 443 else uri.port
        require(port in 1..65535)
        val type = if (scheme == "mierus") "mieru" else scheme
        val out = JSONObject().put("name", "proxy").put("type", type).put("server", host).put("port", port).put("udp", true)
        if (scheme != "mierus") {
            for (flag in listOf("insecure", "allowInsecure", "allow_insecure")) require(q[flag].orEmpty().lowercase() in setOf("", "false", "0")) { "TLS_UNVERIFIED" }
            out.put("skip-cert-verify", false)
            require(q["sni"].isNullOrBlank() || q["peer"].isNullOrBlank() || q["sni"] == q["peer"]) { "UNSUPPORTED_OPTION" }
            out.put("sni", q["sni"]?.ifBlank { null } ?: q["peer"]?.ifBlank { null } ?: host)
            q["alpn"]?.let {
                val values = it.split(','); require(values.size in 1..8 && values.all { value -> value.isNotBlank() && value.length <= 255 && value.none(Char::isISOControl) })
                out.put("alpn", JSONArray(values))
            }
        }
        when (scheme) {
            "anytls" -> {
                only("sni", "peer", "alpn", "fp", "insecure", "allowInsecure", "allow_insecure", "idle-session-check-interval", "idle-session-timeout", "min-idle-session")
                out.put("password", auth)
                q["fp"]?.let { require(it in setOf("chrome", "firefox", "safari", "ios", "android", "edge", "random", "randomized")) { "UNSUPPORTED_OPTION" }; out.put("client-fingerprint", it) }
                for (key in listOf("idle-session-check-interval", "idle-session-timeout", "min-idle-session")) q[key]?.let {
                    val n = it.toInt(); require(n in 0..(if (key == "min-idle-session") 8 else 600)); out.put(key, n)
                }
            }
            "tuic" -> {
                only("sni", "peer", "alpn", "insecure", "allowInsecure", "allow_insecure", "congestion_control", "udp_relay_mode", "reduce_rtt", "heartbeat")
                val credentials = auth.split(':', limit = 2)
                require(credentials.size == 2 && credentials[1].isNotBlank())
                require(UUID.fromString(credentials[0]).toString().equals(credentials[0], true))
                out.put("uuid", credentials[0]).put("password", credentials[1])
                q["congestion_control"]?.let { require(it in setOf("cubic", "new_reno", "bbr")); out.put("congestion-controller", it) }
                q["udp_relay_mode"]?.let { require(it in setOf("native", "quic")); out.put("udp-relay-mode", it) }
                boolean("reduce_rtt", "reduce-rtt", out)
                q["heartbeat"]?.let { val n = it.removeSuffix("ms").toInt(); require(n in 1000..600000); out.put("heartbeat-interval", n) }
            }
            "mierus" -> {
                // The official simple format may contain multiple bindings. Reject, never discard them.
                only("profile", "port", "protocol", "multiplexing", "handshake-mode", "traffic-pattern")
                val credentials = auth.split(':', limit = 2)
                require(credentials.size == 2 && credentials.all(String::isNotBlank))
                out.put("username", credentials[0]).put("password", credentials[1])
                val transport = q["protocol"] ?: error("Missing transport")
                require(transport in setOf("TCP", "UDP")) { "UNSUPPORTED_TRANSPORT" }; out.put("transport", transport)
                q["multiplexing"]?.let { require(it in setOf("MULTIPLEXING_OFF", "MULTIPLEXING_LOW", "MULTIPLEXING_MIDDLE", "MULTIPLEXING_HIGH")); out.put("multiplexing", it) }
                q["handshake-mode"]?.let { require(it in setOf("HANDSHAKE_DEFAULT", "HANDSHAKE_STANDARD", "HANDSHAKE_NO_WAIT")); out.put("handshake-mode", it) }
                q["traffic-pattern"]?.let { require(it.length <= 4096); app.slipnet.data.export.WireBase64.decode(it); out.put("traffic-pattern", it) }
            }
        }
        val name = uri.rawFragment?.let(::decode)?.ifBlank { null } ?: q["profile"]?.ifBlank { null } ?: type.uppercase()
        return CoreLink.Parsed(name, host, JSONObject().put("protocol", type).put("settings", out))
    }
}
