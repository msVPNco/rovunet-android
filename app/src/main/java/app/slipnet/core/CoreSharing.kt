package app.slipnet.core

import app.slipnet.data.export.WireBase64
import org.json.JSONObject
import java.net.URLEncoder

internal object CoreSharing {
    fun enc(s: String): String = URLEncoder.encode(s, "UTF-8").replace("+", "%20")
    fun normalize(raw: String): String {
        val text = raw.trim()
        if (text.startsWith("[Interface]", true)) return text
        if (text.startsWith("vmess://", true) && '@' !in text.substringBefore('#')) {
            require(text.length <= 65536) { "INVALID_FEED" }
            val data = WireBase64.decode(text.substring(8).substringBefore('#').replace('-', '+').replace('_', '/'))
            return vmessUri(jsonPayload(String(data, Charsets.UTF_8)))
        }
        // URI rejects raw display-name spaces. Never modify endpoint or query semantics.
        if ('#' !in text) return text
        val fragment = text.substringAfter('#').replace("+", "%2B").replace(Regex("%(?![0-9a-fA-F]{2})"), "%25")
        return text.substringBefore('#') + "#" + enc(java.net.URLDecoder.decode(fragment, "UTF-8"))
    }
    private fun jsonPayload(text: String): JSONObject {
        require(text.length <= 65536)
        var depth = 0; var quoted = false; var escaped = false
        for (c in text) {
            if (quoted) { if (escaped) escaped = false else if (c == '\\') escaped = true else if (c == '\"') quoted = false }
            else when (c) { '\"' -> quoted = true; '{', '[' -> { depth++; require(depth <= 16) }; '}', ']' -> depth-- }
        }
        require(depth == 0 && !quoted)
        return JSONObject(text)
    }
    fun vmessUri(o: JSONObject): String {
        require(o.keys().asSequence().all { it in setOf("v", "ps", "name", "add", "port", "id", "aid", "scy", "net", "type", "host", "path", "tls", "sni", "alpn", "fp", "allowInsecure", "insecure", "ed", "eh", "pcs", "vcn") }) { "UNSUPPORTED_OPTION" }
        fun value(key: String, default: String = ""): String = if (o.isNull(key)) default else o.get(key).toString()
        // Common exporters attach a second display name and empty custom-trust hints.
        // Nonempty trust hints must not be discarded or replaced with system trust.
        require(value("pcs").isEmpty() && value("vcn").isEmpty()) { "UNSUPPORTED_OPTION" }
        require(value("v", "2") == "2" && value("aid", "0") == "0") { "UNSUPPORTED_OPTION" }
        val net = value("net", "tcp").ifBlank { "tcp" }
        val type = value("type", "none")
        require(type in (if (net == "grpc") setOf("", "none", "gun", "multi") else setOf("", "none"))) { "UNSUPPORTED_TRANSPORT" }
        val tls = value("tls").ifBlank { "none" }
        require(tls in setOf("tls", "none")) { "UNSUPPORTED_OPTION" }
        val q = linkedMapOf("type" to net, "security" to tls, "cipher" to value("scy", "auto").ifBlank { "auto" })
        for (k in listOf("host", "sni", "alpn", "fp", "allowInsecure", "insecure", "ed", "eh")) if (!o.isNull(k)) q[k] = value(k)
        if (net == "grpc") { q["serviceName"] = value("path"); q["mode"] = if (type == "multi") "multi" else "gun" } else if (!o.isNull("path")) q["path"] = value("path")
        return uri("vmess", o.getString("id"), o.getString("add"), o.get("port").toString(), q, value("ps").ifBlank { value("name", "VMess") })
    }
    fun uri(kind: String, auth: String, host: String, port: String, q: Map<String, String>, name: String): String {
        val h = host.removeSurrounding("[", "]")
        return "$kind://${enc(auth)}@${if (':' in h) "[$h]" else h}:$port?" + q.entries.joinToString("&") { enc(it.key) + "=" + enc(it.value) } + "#" + enc(name)
    }
}
