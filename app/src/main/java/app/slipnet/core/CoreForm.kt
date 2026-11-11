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
            return "[Interface]\n" + line("privatekey", "PrivateKey") + line("address", "Address") + line("dns", "DNS") + line("mtu", "MTU") + AmneziaConfig.labels.entries.joinToString("") { (key, label) -> line(key, label) } +
                "\n[Peer]\n" + line("publickey", "PublicKey") + line("presharedkey", "PresharedKey") + line("endpoint", "Endpoint") + line("allowedips", "AllowedIPs") + line("persistentkeepalive", "PersistentKeepalive")
        }
        val host = this["server"].trim().removeSurrounding("[", "]")
        if (kind == "ss") {
            val credentials = app.slipnet.data.export.WireBase64.encode(
                (this["method"] + ":" + this["auth"]).toByteArray(Charsets.UTF_8)
            ).replace('+', '-').replace('/', '_')
            val encoded = "ss://$credentials@${if (':' in host) "[$host]" else host}:${this["port"]}#${enc(name)}"
            CoreLink.parse(encoded)
            return encoded
        }

        if (kind == "vmess") {
            val o = org.json.JSONObject().put("v", "2").put("ps", name).put("add", host).put("port", this["port"]).put("id", this["auth"]).put("aid", "0")
                .put("scy", this["cipher"].ifBlank { "auto" }).put("net", this["type"].ifBlank { "tcp" }).put("type", if (this["type"] == "grpc") this["mode"].ifBlank { "gun" } else "none")
                .put("tls", this["security"].ifBlank { "none" })
            for (k in listOf("host", "sni", "alpn", "fp", "allowInsecure", "insecure", "ed", "eh", "cs", "fm")) if (this[k].isNotBlank()) o.put(k, this[k])
            if (this["type"] == "grpc" && this["authority"].isNotBlank()) o.put("host", this["authority"])
            o.put("path", if (this["type"] == "grpc") this["serviceName"] else if (this["type"] in setOf("ws", "httpupgrade")) this["path"].ifBlank { "/" } else this["path"])
            val link = "vmess://" + app.slipnet.data.export.WireBase64.encode(o.toString().toByteArray(Charsets.UTF_8))
            CoreLink.parse(link)
            return link
        }
        if (kind == "mierus") {
            val query = linkedMapOf("profile" to name, "port" to this["port"], "protocol" to this["protocol"].ifBlank { "TCP" })
            for (key in listOf("multiplexing", "handshake-mode", "traffic-pattern")) if (this[key].isNotBlank()) query[key] = this[key]
            val link = "mierus://${enc(this["username"])}:${enc(this["auth"])}@${if (':' in host) "[$host]" else host}?" + query.entries.joinToString("&") { enc(it.key) + "=" + enc(it.value) }
            CoreLink.parse(link)
            return link
        }
        val q = fields.filterKeys { it !in setOf("server", "port", "auth") }.toMutableMap()
        if (kind == "vless") {
            if (q["type"] != "tcp" && q["type"] != "raw") q.remove("flow")
            if (q["type"] != "xhttp") q.remove("extra")
            if (q["type"] in setOf("tcp", "raw")) listOf("mode", "host", "path").forEach(q::remove)
            if (q["security"] != "reality") listOf("pbk", "sid", "spx").forEach(q::remove)
            if (q["security"] == "none") listOf("sni", "fp", "alpn", "flow").forEach(q::remove)
        } else if (kind == "hysteria2" && q["obfs"].isNullOrBlank()) q.remove("obfs-password")
        if (kind in setOf("vless", "trojan")) {
            if (q["type"] != "grpc") listOf("serviceName", "authority").forEach(q::remove)
            if (q["type"] != "ws") listOf("ed", "eh").forEach(q::remove)
            if (q["type"] != "ws") q.remove("heartbeatPeriod")
            if (q["type"] !in setOf("kcp", "mkcp")) listOf("mtu", "tti").forEach(q::remove)
            if (q["type"] !in setOf("grpc", "xhttp")) q.remove("mode")
        }
        val query = q.filterValues { it.isNotBlank() }.entries.joinToString("&") { enc(it.key) + "=" + enc(it.value) }
        return "$kind://${enc(this["auth"])}@${if (':' in host) "[$host]" else host}:${this["port"]}?$query#${enc(name)}"
    }
    companion object {
        fun fresh(mode: String) = when (mode) {
            "anytls", "tuic" -> CoreForm(mode, mapOf("port" to "443"))
            "mierus" -> CoreForm(mode, mapOf("port" to "443", "protocol" to "TCP"))
            "wireguard" -> CoreForm(mode, mapOf("mtu" to "1420", "allowedips" to "0.0.0.0/0", "persistentkeepalive" to "25"))
            "hysteria2" -> CoreForm(mode, mapOf("port" to "443"))
            "trojan" -> CoreForm(mode, mapOf("port" to "443", "type" to "tcp", "security" to "tls"))
            "vmess" -> CoreForm(mode, mapOf("port" to "443", "type" to "tcp", "security" to "tls", "cipher" to "auto"))
            "ss" -> CoreForm(mode, mapOf("port" to "443", "method" to "aes-256-gcm"))
            else -> CoreForm("vless", mapOf("port" to "443", "encryption" to "none", "type" to if (mode == "xhttp") "xhttp" else "tcp", "security" to if (mode == "xhttp") "tls" else "reality", "fp" to "chrome", "path" to "/", "mode" to "auto", "flow" to if (mode == "xhttp") "" else "xtls-rprx-vision"))
        }
        fun decode(raw: String): CoreForm {
            val parsed = CoreLink.parse(raw) // Reject unsupported input before exposing editable fields.
            if (raw.trim().startsWith("ss://", true)) {
                val server = parsed.outbound.getJSONObject("settings").getJSONArray("servers").getJSONObject(0)
                return CoreForm("ss", mapOf("server" to parsed.host, "port" to server.getInt("port").toString(),
                    "method" to server.getString("method"), "auth" to server.getString("password")))
            }
            val text = if (raw.trim().startsWith("wireguard://", true)) CoreLink.wireguardDocument(CoreSharing.normalize(raw.trim())) else CoreSharing.normalize(raw.trim())
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
            if (uri.scheme.equals("mierus", ignoreCase = true)) {
                val credentials = uri.rawUserInfo.split(':', limit = 2).map(::dec)
                fields["username"] = credentials[0]; fields["auth"] = credentials[1]
                fields["port"] = parsed.outbound.getJSONObject("settings").getInt("port").toString()
                fields.remove("profile")
            }
            val kind = if (uri.scheme in setOf("hysteria2", "hy2")) "hysteria2" else uri.scheme.lowercase()
            if (kind == "vmess" && fields.containsKey("encryption")) {
                fields["cipher"] = fields.getValue("encryption"); fields.remove("encryption")
            }
            if (kind == "vless") {
                fields["type"] = when (fields["type"]) { null, "raw" -> "tcp"; "websocket" -> "ws"; "mkcp" -> "kcp"; else -> fields.getValue("type") }
                fields.putIfAbsent("security", "none")
            }
            return CoreForm(kind, fields)
        }
    }
}
