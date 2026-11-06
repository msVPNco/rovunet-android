package app.slipnet.core

import java.net.URI
import java.net.URLDecoder
import java.net.URLEncoder

/** Lossless editable representation of the supported sharing formats. */
data class CoreForm(val kind: String, val fields: Map<String, String>) {
    operator fun get(key: String): String = fields[key].orEmpty()
    fun with(key: String, value: String) = copy(fields = fields + (key to value))
    enum class FieldIssue { WIREGUARD_MULTILINE, SERVER_ADDRESS, SERVER_PORT, AUTH_REQUIRED }
    fun validationIssue(): FieldIssue? {
        if (kind == "wireguard") return if (fields.values.any { it.contains('\n') || it.contains('\r') }) FieldIssue.WIREGUARD_MULTILINE else null
        val host = this["server"].trim().removeSurrounding("[", "]")
        return when {
            host.isBlank() || host.any { it.isWhitespace() || it in "/?#@" } -> FieldIssue.SERVER_ADDRESS
            this["port"].toIntOrNull() !in 1..65535 -> FieldIssue.SERVER_PORT
            this["auth"].isBlank() -> FieldIssue.AUTH_REQUIRED
            else -> null
        }
    }

    fun encode(name: String): String {
        val issue = validationIssue()
        require(issue == null) { when(issue) {
            FieldIssue.WIREGUARD_MULTILINE -> "WireGuard fields must each fit on one line"
            FieldIssue.SERVER_ADDRESS -> "Enter a server hostname or IP address"
            FieldIssue.SERVER_PORT -> "Port must be 1–65535"
            FieldIssue.AUTH_REQUIRED -> "Enter the UUID or authentication password"
            null -> "Invalid core settings"
        } }
        fun enc(v: String) = URLEncoder.encode(v, "UTF-8").replace("+", "%20")
        if (kind == "wireguard") {
            fun line(key: String, label: String) = if (this[key].isBlank()) "" else "$label = ${this[key].trim()}\n"
            return "[Interface]\n" + line("privatekey", "PrivateKey") + line("address", "Address") + line("dns", "DNS") + line("mtu", "MTU") +
                "\n[Peer]\n" + line("publickey", "PublicKey") + line("presharedkey", "PresharedKey") + line("endpoint", "Endpoint") + line("allowedips", "AllowedIPs") + line("persistentkeepalive", "PersistentKeepalive")
        }
        val host = this["server"].trim().removeSurrounding("[", "]")
        val q = fields.filterKeys { it !in setOf("server", "port", "auth") }.toMutableMap()
        if (kind == "vless") {
            if (q["type"] == "xhttp") q.remove("flow") else listOf("mode", "host", "path", "extra").forEach(q::remove)
            if (q["security"] != "reality") listOf("pbk", "sid", "spx").forEach(q::remove)
            if (q["security"] == "none") listOf("sni", "fp", "alpn", "flow").forEach(q::remove)
        } else if (q["obfs"].isNullOrBlank()) q.remove("obfs-password")
        val query = q.filterValues { it.isNotBlank() }.entries.joinToString("&") { enc(it.key) + "=" + enc(it.value) }
        return "$kind://${enc(this["auth"])}@${if (':' in host) "[$host]" else host}:${this["port"]}?$query#${enc(name)}"
    }
    companion object {
        fun fresh(mode: String) = when (mode) {
            "wireguard" -> CoreForm(mode, mapOf("mtu" to "1420", "allowedips" to "0.0.0.0/0", "persistentkeepalive" to "25"))
            "hysteria2" -> CoreForm(mode, mapOf("port" to "443"))
            else -> CoreForm("vless", mapOf("port" to "443", "encryption" to "none", "type" to if (mode == "xhttp") "xhttp" else "tcp", "security" to if (mode == "xhttp") "tls" else "reality", "fp" to "chrome", "path" to "/", "mode" to "auto", "flow" to if (mode == "xhttp") "" else "xtls-rprx-vision"))
        }
        fun decode(raw: String): CoreForm {
            CoreLink.parse(raw) // Reject unsupported input before exposing editable fields.
            val text = raw.trim()
            if (text.startsWith("[Interface]", true)) {
                val fields = text.lines().map { it.substringBefore('#').trim() }.filter { '=' in it }.associate {
                    val pair = it.split('=', limit = 2); pair[0].trim().lowercase() to pair[1].trim()
                }
                return CoreForm("wireguard", fields)
            }
            fun dec(s: String) = URLDecoder.decode(s.replace("+", "%2B"), "UTF-8")
            val uri = URI(text)
            val fields = uri.rawQuery.orEmpty().split('&').filter { it.isNotEmpty() }.associate {
                val pair = it.split('=', limit = 2); dec(pair[0]) to dec(pair.getOrElse(1) { "" })
            }.toMutableMap()
            fields["server"] = uri.host.removeSurrounding("[", "]")
            fields["port"] = (if (uri.port < 0) 443 else uri.port).toString()
            fields["auth"] = dec(uri.rawUserInfo)
            val kind = if (uri.scheme == "vless") "vless" else "hysteria2"
            if (kind == "vless") {
                fields["type"] = if (fields["type"] == "xhttp") "xhttp" else "tcp"
                fields.putIfAbsent("security", "none")
            }
            return CoreForm(kind, fields)
        }
    }
}
