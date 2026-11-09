package app.slipnet.core

import app.slipnet.data.export.WireBase64
import org.json.JSONArray
import org.json.JSONObject
import java.net.URI
import java.net.URLDecoder

/** Converts explicitly supported sharing dialects to a single Xray outbound. */
object CoreLink {
    data class Parsed(val name: String, val host: String, val outbound: JSONObject)
    private fun obj(vararg fields: Pair<String, Any>): JSONObject = JSONObject().also { o -> fields.forEach { o.put(it.first, it.second) } }
    private fun arr(vararg values: Any) = JSONArray(values.toList())
    private fun decode(s: String) = URLDecoder.decode(s.replace("+", "%2B"), "UTF-8")
    private fun key(s: String): String {
        require(WireBase64.decode(s).size == 32) { "Expected a 32-byte key" }
        return s
    }
    fun handles(text: String): Boolean {
        if (text.trimStart().startsWith("[Interface]", true)) return true
        val scheme = text.substringBefore("://").lowercase()
        if (scheme in setOf("hysteria2", "hy2", "wireguard", "ss", "trojan", "vmess")) return true
        if (scheme != "vless") return false
        val params = URI(CoreSharing.normalize(text)).rawQuery.orEmpty().split('&').associate {
            val kv = it.split('=', limit = 2)
            decode(kv[0]) to decode(kv.getOrElse(1) { "" })
        }
        return params.keys.any { it in setOf("ed", "eh", "serviceName", "authority") } ||
            Regex("[?&]ed=").containsMatchIn(params["path"].orEmpty()) || params["security"] == "reality" || (params["type"] ?: "tcp") !in setOf("ws", "websocket")
    }

    private fun shadowsocks(text: String): Parsed {
        fun unpack(value: String): String {
            val bytes = WireBase64.decode(value.replace('-', '+').replace('_', '/'))
            return String(bytes, Charsets.UTF_8)
        }
        val body = text.substringBefore('#')
        val name = text.substringAfter('#', "").let(::decode).ifBlank { "Shadowsocks" }
        val uri: URI
        val credentials: String
        if ('@' in body) {
            uri = URI(body)
            val auth = decode(uri.rawUserInfo ?: error("Missing credentials"))
            credentials = if (':' in auth) auth else unpack(auth)
        } else {
            require('?' !in body) { "Unsupported Shadowsocks option" }
            val decoded = unpack(body.substring(5))
            val at = decoded.lastIndexOf('@')
            require(at > 0) { "Missing Shadowsocks endpoint" }
            credentials = decoded.substring(0, at)
            uri = URI("ss://" + decoded.substring(at + 1))
        }
        val host = uri.host?.removeSurrounding("[", "]") ?: error("Missing host")
        require(host.isNotBlank() && uri.port in 1..65535)
        require(uri.rawQuery.isNullOrEmpty() && (uri.rawPath.isNullOrEmpty() || uri.rawPath == "/")) { "Unsupported Shadowsocks plugin or option" }
        val auth = credentials.split(':', limit = 2)
        require(auth.size == 2 && auth[1].isNotEmpty()) { "Missing Shadowsocks credentials" }
        require(auth[0] in setOf("aes-128-gcm", "aes-256-gcm", "chacha20-ietf-poly1305", "xchacha20-ietf-poly1305")) { "Only authenticated AEAD Shadowsocks methods are supported" }
        return Parsed(name, host, obj("protocol" to "shadowsocks", "settings" to obj("servers" to arr(
            obj("address" to host, "port" to uri.port, "method" to auth[0], "password" to auth[1])))))
    }

    fun parse(raw: String): Parsed {
        val text = CoreSharing.normalize(raw.trim())
        require(text.length <= 65536) { "Core profile too large" }
        if (text.trimStart().startsWith("[Interface]", true)) return wireguard(text)
        if (text.startsWith("ss://", true)) return shadowsocks(text)
        val uri = URI(text)
        val scheme = uri.scheme.lowercase()
        val host = uri.host?.removePrefix("[")?.removeSuffix("]") ?: error("Missing host")
        val port = if (uri.port == -1) 443 else uri.port
        require(port in 1..65535 && host.isNotBlank())
        require(uri.rawPath.isNullOrEmpty() || uri.rawPath == "/")
        val q = linkedMapOf<String, String>()
        uri.rawQuery?.split('&')?.filter { it.isNotEmpty() }?.forEach {
            val kv = it.split('=', limit = 2); val k = decode(kv[0])
            require(k !in q) { "Duplicate option" }; q[k] = decode(kv.getOrElse(1) { "" })
        }
        fun only(vararg allowed: String) { require(q.keys.all { it in allowed }) { "Unsupported core link option" } }
        val name = uri.rawFragment?.let(::decode)?.ifBlank { null } ?: scheme.uppercase()
        val auth = decode(uri.rawUserInfo ?: error("Missing credentials"))
        for (flag in listOf("insecure", "allowInsecure")) require(q[flag].orEmpty().lowercase() in setOf("", "0", "false")) { "TLS_UNVERIFIED" }
        require(q["headerType"].orEmpty() in setOf("", "none")) { "UNSUPPORTED_OPTION" }
        fun stream(defaultSecurity: String): JSONObject {
            val transport = when (q["type"] ?: "tcp") { "tcp", "raw" -> "raw"; "ws", "websocket" -> "ws"; "grpc" -> "grpc"; "httpupgrade" -> "httpupgrade"; else -> error("UNSUPPORTED_TRANSPORT") }
            val security = q["security"] ?: defaultSecurity
            require(security in setOf("tls", "none")) { "UNSUPPORTED_OPTION" }
            val out = obj("network" to transport, "security" to security)
            if (security == "tls") out.put("tlsSettings", obj("serverName" to (q["sni"]?.ifBlank { host } ?: host), "fingerprint" to (q["fp"]?.ifBlank { "chrome" } ?: "chrome")).also {
                q["alpn"]?.takeIf(String::isNotBlank)?.let { a -> it.put("alpn", JSONArray(a.split(','))) }
            })
            addTransport(out, transport, q, host)
            return out
        }
        val outbound = when (scheme) {
            "vless" -> {
                only("type", "security", "encryption", "flow", "sni", "fp", "pbk", "sid", "spx", "path", "host", "mode", "alpn", "extra", "headerType", "serviceName", "authority", "ed", "eh", "allowInsecure", "insecure")
                require(q["encryption"].orEmpty() in setOf("", "none"))
                require(auth.matches(Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")))
                val transport = when (q["type"] ?: "tcp") { "tcp", "raw" -> "raw"; "xhttp", "ws", "grpc", "httpupgrade" -> q.getValue("type"); "websocket" -> "ws"; else -> error("UNSUPPORTED_TRANSPORT") }
                val security = q["security"] ?: "none"
                require(security in setOf("none", "tls", "reality"))
                val flow = q["flow"].orEmpty()
                require(flow.isEmpty() || (flow == "xtls-rprx-vision" && transport == "raw" && security != "none"))
                val stream = obj("network" to transport, "security" to security)
                val sni = q["sni"] ?: host
                if (security == "reality") {
                    val publicKey = q["pbk"] ?: error("REALITY public key required")
                    require(WireBase64.decode(publicKey.replace('-', '+').replace('_', '/')).size == 32)
                    val sid = q["sid"].orEmpty()
                    require(sid.length <= 16 && sid.length % 2 == 0 && sid.all { it in "0123456789abcdefABCDEF" })
                    stream.put("realitySettings", obj("serverName" to sni, "fingerprint" to (q["fp"] ?: "chrome"), "publicKey" to publicKey, "shortId" to sid, "spiderX" to (q["spx"] ?: "/")))
                } else if (security == "tls") {
                    val tls = obj("serverName" to sni, "fingerprint" to (q["fp"] ?: "chrome"))
                    q["alpn"]?.let { tls.put("alpn", JSONArray(it.split(','))) }
                    stream.put("tlsSettings", tls)
                }
                addTransport(stream, transport, q, host)
                if (transport == "xhttp") {
                    val mode = q["mode"] ?: "auto"
                    require(mode in setOf("auto", "packet-up", "stream-up", "stream-one"))
                    val settings = obj("host" to (q["host"] ?: host), "path" to (q["path"] ?: "/"), "mode" to mode)
                    q["extra"]?.let {
                        val extra = JSONObject(it)
                        require(extra.keys().asSequence().all { key -> key in xhttpExtraFields }) { "Unsupported XHTTP extra option" }
                        settings.put("extra", extra)
                    }
                    stream.put("xhttpSettings", settings)
                }
                obj("protocol" to "vless", "settings" to obj("vnext" to arr(obj("address" to host, "port" to port, "users" to arr(obj("id" to auth, "encryption" to "none", "flow" to flow))))), "streamSettings" to stream)
            }
            "trojan", "vmess" -> {
                only("type", "security", "sni", "fp", "path", "host", "alpn", "headerType", "serviceName", "authority", "mode", "ed", "eh", "allowInsecure", "insecure", "cipher")
                require(auth.isNotEmpty())
                val stream = stream(if (scheme == "trojan") "tls" else "none")
                if (scheme == "trojan") {
                    require(stream.getString("security") == "tls") { "TLS_REQUIRED" }
                    require(q["cipher"].isNullOrBlank()) { "UNSUPPORTED_OPTION" }
                    obj("protocol" to "trojan", "settings" to obj("servers" to arr(obj("address" to host, "port" to port, "password" to auth))), "streamSettings" to stream)
                } else {
                    require(auth.matches(Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")))
                    val cipher = q["cipher"] ?: "auto"
                    require(cipher in setOf("auto", "aes-128-gcm", "chacha20-poly1305")) { "UNSUPPORTED_OPTION" }
                    obj("protocol" to "vmess", "settings" to obj("vnext" to arr(obj("address" to host, "port" to port, "users" to arr(obj("id" to auth, "security" to cipher))))), "streamSettings" to stream)
                }
            }
            "hysteria2", "hy2" -> {
                only("sni", "insecure", "allowInsecure", "obfs", "obfs-password", "alpn", "security")
                require(q["security"].orEmpty() in setOf("", "tls")) { "UNSUPPORTED_OPTION" }
                require(q["insecure"].orEmpty() in setOf("", "0", "false")) { "Unverified TLS is not allowed" }
                require(auth.isNotEmpty())
                val stream = obj("network" to "hysteria", "security" to "tls",
                    "tlsSettings" to obj("serverName" to (q["sni"] ?: host), "alpn" to arr("h3")),
                    "hysteriaSettings" to obj("version" to 2, "auth" to auth))
                require(q["alpn"].orEmpty() in setOf("", "h3"))
                when (q["obfs"].orEmpty()) {
                    "" -> require(q["obfs-password"].isNullOrEmpty())
                    "salamander" -> {
                        val password = q["obfs-password"] ?: error("Missing obfuscation password")
                        require(password.isNotEmpty())
                        stream.put("finalmask", obj("udp" to arr(obj("type" to "salamander", "settings" to obj("password" to password)))))
                    }
                    else -> error("Unsupported obfuscation")
                }
                obj("protocol" to "hysteria", "settings" to obj("version" to 2, "address" to host, "port" to port), "streamSettings" to stream)
            }
            "wireguard" -> error("Import standard WireGuard [Interface]/[Peer] configuration")
            else -> error("Unsupported core protocol")
        }
        return Parsed(name, host, outbound)
    }

    private fun addTransport(stream: JSONObject, transport: String, q: Map<String, String>, host: String) {
        when (transport) {
            "ws" -> {
                var path = q["path"]?.ifBlank { "/" } ?: "/"
                require(path.startsWith("/")) { "UNSUPPORTED_OPTION" }
                require(q["eh"].orEmpty() in setOf("", "Sec-WebSocket-Protocol")) { "UNSUPPORTED_OPTION" }
                q["ed"]?.let {
                    val n = it.toInt(); require(n in 0..65535)
                    require(!Regex("[?&]ed=").containsMatchIn(path)) { "UNSUPPORTED_OPTION" }
                    // The pinned Xray reads early data from path?ed=N, not legacy JSON fields.
                    path += (if ('?' in path) "&" else "?") + "ed=" + n
                }
                require(q["eh"].isNullOrEmpty() || q.containsKey("ed")) { "UNSUPPORTED_OPTION" }
                val settings = obj("path" to path, "headers" to obj("Host" to (q["host"]?.ifBlank { host } ?: host)))
                stream.put("wsSettings", settings)
            }
            "grpc" -> {
                require(q["mode"].orEmpty() in setOf("", "gun", "multi")) { "UNSUPPORTED_OPTION" }
                stream.put("grpcSettings", obj("serviceName" to q["serviceName"].orEmpty(), "authority" to (q["authority"] ?: q["host"].orEmpty()), "multiMode" to (q["mode"] == "multi")))
            }
            "httpupgrade" -> stream.put("httpupgradeSettings", obj("host" to (q["host"] ?: host), "path" to (q["path"] ?: "/")))
        }
        if (transport != "ws") require(q["ed"].isNullOrBlank() && q["eh"].isNullOrBlank()) { "UNSUPPORTED_OPTION" }
        if (transport != "grpc") require(q["serviceName"].isNullOrBlank() && q["authority"].isNullOrBlank()) { "UNSUPPORTED_OPTION" }
    }

    private fun wireguard(text: String): Parsed {
        val iface = linkedMapOf<String, String>(); val peer = linkedMapOf<String, String>()
        var current: MutableMap<String, String>? = null
        for (raw in text.lines()) {
            val line = raw.substringBefore('#').trim()
            if (line.isEmpty()) continue
            when (line.lowercase()) {
                "[interface]" -> { require(current == null); current = iface }
                "[peer]" -> { require(current === iface); current = peer }
                else -> {
                    val kv = line.split('=', limit = 2); require(kv.size == 2)
                    val map = current ?: error("Missing section")
                    val k = kv[0].trim().lowercase(); require(k !in map)
                    map[k] = kv[1].trim()
                }
            }
        }
        require(iface.keys.all { it in setOf("privatekey", "address", "dns", "mtu") }) { "Unsupported Interface option (scripts are never executed)" }
        require(peer.keys.all { it in setOf("publickey", "presharedkey", "endpoint", "allowedips", "persistentkeepalive") })
        val ep = URI("udp://" + (peer["endpoint"] ?: error("Missing peer endpoint")))
        require(ep.host != null && ep.port in 1..65535 && ep.rawUserInfo == null && ep.rawQuery == null && ep.rawFragment == null && ep.rawPath.isNullOrEmpty())
        fun cidrs(value: String): List<String> = value.split(',').map { c ->
            val parts = c.trim().split('/'); require(parts.size == 2)
            val ip = parts[0]; require(ip.matches(Regex("[0-9a-fA-F:.]+")))
            val addr = java.net.InetAddress.getByName(ip)
            require(parts[1].toInt() in 0..(addr.address.size * 8)); c.trim()
        }
        val addresses = cidrs(iface["address"] ?: error("Missing interface address"))
        val allowed = cidrs(peer["allowedips"] ?: error("Missing AllowedIPs"))
        val p = obj("endpoint" to peer.getValue("endpoint"), "publicKey" to key(peer["publickey"] ?: error("Missing peer key")), "allowedIPs" to JSONArray(allowed))
        peer["presharedkey"]?.let { p.put("preSharedKey", key(it)) }
        peer["persistentkeepalive"]?.let { val n = it.toInt(); require(n in 0..65535); p.put("keepAlive", n) }
        val mtu = iface["mtu"]?.toInt() ?: 1280; require(mtu in 1280..1500)
        val settings = obj("secretKey" to key(iface["privatekey"] ?: error("Missing private key")), "address" to JSONArray(addresses), "peers" to arr(p), "noKernelTun" to true, "mtu" to mtu)
        iface["dns"]?.let { dns ->
            val ips = dns.split(',').map { it.trim() }
            require(ips.all { it.matches(Regex("[0-9a-fA-F:.]+")) && runCatching { java.net.InetAddress.getByName(it) }.isSuccess })
            settings.put("remoteDNS", JSONArray(ips))
        }
        return Parsed("WireGuard", ep.host, obj("protocol" to "wireguard", "settings" to settings))
    }

    // Only the single-endpoint fields understood by the pinned Xray are forwarded.
    // Xray's Go decoder accepts case variants, so a deny-list of literal spellings is unsafe.
    internal val xhttpExtraFields = setOf(
        "headers", "xPaddingBytes", "xPaddingObfsMode", "xPaddingKey", "xPaddingHeader",
        "xPaddingPlacement", "xPaddingMethod", "uplinkHTTPMethod", "sessionIDPlacement",
        "sessionIDKey", "sessionIDTable", "sessionIDLength", "seqPlacement", "seqKey",
        "uplinkDataPlacement", "uplinkDataKey", "uplinkChunkSize", "noGRPCHeader", "noSSEHeader",
        "scMaxEachPostBytes", "scMinPostsIntervalMs", "scMaxBufferedPosts", "scStreamUpServerSecs",
        "serverMaxHeaderBytes", "xmux", "h2Params"
    )

    fun runtime(text: String, port: Int, username: String? = null, password: String? = null, routing: JSONObject? = null): String {
        require(port in 1..65535)
        val socks = obj("auth" to "noauth", "udp" to true, "ip" to "127.0.0.1")
        if (!username.isNullOrEmpty()) { require(password != null); socks.put("auth", "password"); socks.put("accounts", arr(obj("user" to username, "pass" to password))) }
        val outbound = parse(text).outbound.put("tag", "proxy")
        val inbound = obj("listen" to "127.0.0.1", "port" to port, "protocol" to "socks", "settings" to socks)
        if (routing != null) inbound.put("sniffing", obj("enabled" to true, "destOverride" to arr("http", "tls", "quic"), "routeOnly" to true))
        val config = obj("log" to obj("loglevel" to "none"), "inbounds" to arr(inbound), "outbounds" to arr(outbound, obj("tag" to "direct", "protocol" to "freedom")))
        routing?.let { config.put("routing", it) }
        return config.toString()
    }
}
