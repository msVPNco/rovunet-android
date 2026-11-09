package app.slipnet.core

import org.json.JSONArray
import org.json.JSONObject

/** Extract connections, never execute another client's listeners, DNS, routing, files or plugins. */
internal object CoreDocument {
    fun looksLikeJson(text: String): Boolean = text.trimStart().let { it.startsWith("{") || Regex("^\\[\\s*[\\{\\]\"]").containsMatchIn(it) }
    data class Result(val links: List<String>, val rejected: Int)
    private fun keys(o: JSONObject, vararg allowed: String) {
        require(o.keys().asSequence().all { it in allowed }) { "UNSUPPORTED_OPTION" }
    }
    private fun single(a: JSONArray): JSONObject { require(a.length() == 1) { "UNSUPPORTED_OPTION" }; return a.getJSONObject(0) }
    fun read(text: String): Result {
        require(text.toByteArray(Charsets.UTF_8).size <= 2 * 1024 * 1024) { "FEED_TOO_LARGE" }
        var depth = 0; var quoted = false; var escaped = false
        for (c in text) {
            if (quoted) { if (escaped) escaped = false else if (c == '\\') escaped = true else if (c == '"') quoted = false }
            else when (c) { '"' -> quoted = true; '{', '[' -> { depth++; require(depth <= 24) { "INVALID_FEED" } }; '}', ']' -> depth-- }
        }
        require(depth == 0 && !quoted) { "INVALID_FEED" }
        val root = if (text.trimStart().startsWith("[")) JSONArray(text) else JSONObject(text)
        val nativeEnvelope = root is JSONObject && (root.has("version") || root.has("items"))
        if (nativeEnvelope) {
            val envelope = root as JSONObject
            keys(envelope, "version", "items", "name")
            require(envelope.get("version") is Number && envelope.get("version").toString() == "1") { "INVALID_FEED" }
        }
        val array = when (root) {
            is JSONArray -> root
            is JSONObject -> when {
                nativeEnvelope -> root.getJSONArray("items")
                root.has("outbounds") -> root.getJSONArray("outbounds")
                root.has("protocol") || root.has("type") -> JSONArray().put(root)
                else -> error("INVALID_FEED")
            }
            else -> error("INVALID_FEED")
        }
        require(array.length() in 1..1000) { "INVALID_FEED" }
        if (nativeEnvelope) {
            val ids = HashSet<String>()
            for (i in 0 until array.length()) {
                val item = array.getJSONObject(i); keys(item, "id", "link")
                require(item.opt("id") is String && item.opt("link") is String) { "INVALID_FEED" }
                val id = item.getString("id")
                require(id.isNotBlank() && id.length <= 200 && ids.add(id)) { "INVALID_FEED" }
            }
        }
        val links = ArrayList<String>(); var rejected = 0
        for (i in 0 until array.length()) {
            try {
                val o = array.getJSONObject(i)
                val protocol = o.optString("protocol", o.optString("type"))
                if (protocol in setOf("freedom", "blackhole", "direct", "block", "dns")) continue
                val link = if (o.has("link")) o.getString("link") else if (o.has("protocol")) xray(o) else singbox(o)
                // Don't coerce unsupported settings into a superficially valid connection.
                if (o.has("link")) {
                    require(link.contains("://") && app.slipnet.data.export.ConfigSchemes.accepts(link)) { "INVALID_FEED" }
                    links += link
                } else {
                    val parsed = CoreLink.parse(link)
                    links += if (link.startsWith("vmess://")) CoreForm.decode(link).encode(parsed.name) else link
                }
            } catch (_: Exception) { rejected++ }
        }
        return Result(links, rejected)
    }
    private fun xray(o: JSONObject): String {
        keys(o, "tag", "protocol", "settings", "streamSettings", "mux")
        o.optJSONObject("mux")?.let { keys(it, "enabled"); require(!it.optBoolean("enabled")) { "UNSUPPORTED_OPTION" } }
        val protocol = o.getString("protocol")
        val settings = o.getJSONObject("settings")
        val q = linkedMapOf<String, String>()
        val stream = o.optJSONObject("streamSettings") ?: JSONObject()
        keys(stream, "network", "security", "tlsSettings", "realitySettings", "wsSettings", "grpcSettings", "httpupgradeSettings", "xhttpSettings")
        q["type"] = stream.optString("network", "tcp")
        q["security"] = stream.optString("security", "none")
        val tls = stream.optJSONObject("tlsSettings")
        tls?.let {
            keys(it, "serverName", "fingerprint", "alpn", "allowInsecure")
            require(!it.optBoolean("allowInsecure")) { "TLS_UNVERIFIED" }
            q["sni"] = it.optString("serverName"); q["fp"] = it.optString("fingerprint", "chrome")
            it.optJSONArray("alpn")?.let { a -> q["alpn"] = (0 until a.length()).joinToString(",") { n -> a.getString(n) } }
        }
        stream.optJSONObject("realitySettings")?.let {
            keys(it, "serverName", "fingerprint", "publicKey", "shortId", "spiderX")
            for ((key, field) in mapOf("sni" to "serverName", "fp" to "fingerprint", "pbk" to "publicKey", "sid" to "shortId", "spx" to "spiderX")) if (it.has(field)) q[key] = it.getString(field)
        }
        stream.optJSONObject("wsSettings")?.let {
            keys(it, "path", "headers", "maxEarlyData", "earlyDataHeaderName")
            q["path"] = it.optString("path", "/")
            it.optJSONObject("headers")?.let { h -> require(h.keys().asSequence().all { k -> k.equals("host", true) }) { "UNSUPPORTED_OPTION" }; h.keys().asSequence().firstOrNull()?.let { k -> q["host"] = h.getString(k) } }
            if (it.has("maxEarlyData")) q["ed"] = it.get("maxEarlyData").toString()
            if (it.has("earlyDataHeaderName")) q["eh"] = it.getString("earlyDataHeaderName")
        }
        stream.optJSONObject("grpcSettings")?.let {
            keys(it, "serviceName", "authority", "multiMode")
            q["serviceName"] = it.optString("serviceName"); q["authority"] = it.optString("authority"); q["mode"] = if (it.optBoolean("multiMode")) "multi" else "gun"
        }
        for (transport in listOf("xhttp", "httpupgrade")) stream.optJSONObject(transport + "Settings")?.let {
            keys(it, "host", "path", "mode", "extra")
            for (key in listOf("host", "path", "mode", "extra")) if (it.has(key)) q[key] = it.get(key).toString()
        }
        val server: JSONObject; val auth: String; val kind: String
        when (protocol) {
            "vless", "vmess" -> {
                kind = protocol
                if (settings.has("vnext")) {
                    keys(settings, "vnext"); server = single(settings.getJSONArray("vnext")); keys(server, "address", "port", "users")
                    val user = single(server.getJSONArray("users")); keys(user, "id", "encryption", "flow", "security", "alterId", "level", "email")
                    require(user.optInt("alterId") == 0)
                    auth = user.getString("id")
                    if (protocol == "vless") { q["encryption"] = user.optString("encryption", "none"); q["flow"] = user.optString("flow") } else q["cipher"] = user.optString("security", "auto")
                } else {
                    keys(settings, "address", "port", "id", "encryption", "flow", "security", "level", "email")
                    server = settings; auth = settings.getString("id")
                    if (protocol == "vless") { q["encryption"] = settings.optString("encryption", "none"); q["flow"] = settings.optString("flow") } else q["cipher"] = settings.optString("security", "auto")
                }
            }
            "trojan", "shadowsocks" -> {
                kind = if (protocol == "shadowsocks") "ss" else "trojan"
                if (settings.has("servers")) { keys(settings, "servers"); server = single(settings.getJSONArray("servers")) } else server = settings
                keys(server, "address", "port", "password", "method", "level", "email")
                auth = if (kind == "ss") server.getString("method") + ":" + server.getString("password") else server.getString("password")
                if (kind == "ss") {
                    require(q["type"] in setOf("tcp", "raw") && q["security"] == "none" && stream.length() <= 2) { "UNSUPPORTED_OPTION" }; q.clear()
                }
            }
            else -> error("UNSUPPORTED_PROTOCOL")
        }
        return CoreSharing.uri(kind, auth, server.getString("address"), server.get("port").toString(), q, o.optString("tag", kind))
    }
    private fun singbox(o: JSONObject): String {
        keys(o, "tag", "type", "server", "server_port", "uuid", "password", "method", "security", "alter_id", "flow", "tls", "transport", "packet_encoding")
        val kind = when (val type = o.getString("type")) { "shadowsocks" -> "ss"; "vless", "trojan", "vmess" -> type; else -> error("UNSUPPORTED_PROTOCOL") }
        require(o.optInt("alter_id") == 0 && o.optString("packet_encoding") in setOf("", "xudp")) { "UNSUPPORTED_OPTION" }
        // Packet encoding affects UDP framing; only the pinned core's known default is accepted.
        require(!o.has("packet_encoding")) { "UNSUPPORTED_OPTION" }
        val q = linkedMapOf("type" to "tcp", "security" to "none")
        o.optJSONObject("tls")?.let { tls ->
            keys(tls, "enabled", "server_name", "insecure", "alpn", "utls", "reality")
            require(!tls.optBoolean("insecure")) { "TLS_UNVERIFIED" }
            q["security"] = if (tls.optBoolean("enabled")) "tls" else "none"; q["sni"] = tls.optString("server_name")
            tls.optJSONArray("alpn")?.let { a -> q["alpn"] = (0 until a.length()).joinToString(",") { a.getString(it) } }
            tls.optJSONObject("utls")?.let { u -> keys(u, "enabled", "fingerprint"); if (u.optBoolean("enabled")) q["fp"] = u.getString("fingerprint") }
            tls.optJSONObject("reality")?.let { a -> keys(a, "enabled", "public_key", "short_id"); if (a.optBoolean("enabled")) { require(q["security"] == "tls"); q["security"] = "reality"; q["pbk"] = a.getString("public_key"); q["sid"] = a.optString("short_id") } }
        }
        o.optJSONObject("transport")?.let { t ->
            keys(t, "type", "path", "headers", "service_name", "host", "max_early_data", "early_data_header_name")
            q["type"] = t.getString("type")
            for ((a,b) in mapOf("path" to "path", "service_name" to "serviceName", "max_early_data" to "ed", "early_data_header_name" to "eh")) if (t.has(a)) q[b] = t.get(a).toString()
            if (t.has("host")) q["host"] = t.getString("host")
            t.optJSONObject("headers")?.let { h -> require(h.keys().asSequence().all { it.equals("host", true) }) { "UNSUPPORTED_OPTION" }; h.keys().asSequence().firstOrNull()?.let { q["host"] = h.getString(it) } }
        }
        if (kind == "vless") { q["encryption"] = "none"; q["flow"] = o.optString("flow") }
        if (kind == "vmess") q["cipher"] = o.optString("security", "auto")
        val auth = when (kind) { "vless", "vmess" -> o.getString("uuid"); "ss" -> o.getString("method") + ":" + o.getString("password"); else -> o.getString("password") }
        if (kind == "ss") { require(q["security"] == "none" && q["type"] == "tcp" && !o.has("transport")) { "UNSUPPORTED_OPTION" }; q.clear() }
        return CoreSharing.uri(kind, auth, o.getString("server"), o.get("server_port").toString(), q, o.optString("tag", kind))
    }
}
