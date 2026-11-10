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
    private fun vlessEncryption(value: String?): String {
        val encryption = value?.ifBlank { "none" } ?: "none"
        if (encryption == "none") return encryption
        // The pinned Xray accepts a raw URL-safe public X25519 or ML-KEM key.
        // Preserve this tested single-key dialect; never downgrade unknown modes.
        val match = Regex("^mlkem768x25519plus\\.(native|xorpub|random)\\.(0rtt|1rtt)\\.([A-Za-z0-9_-]+)$")
            .matchEntire(encryption) ?: error("UNSUPPORTED_VLESS_ENCRYPTION")
        val encoded = match.groupValues[3]
        val bytes = WireBase64.decode(encoded.replace('-', '+').replace('_', '/'))
        require(bytes.size in setOf(32, 1184) &&
            WireBase64.encode(bytes).trimEnd('=').replace('+', '-').replace('/', '_') == encoded) { "UNSUPPORTED_VLESS_ENCRYPTION" }
        return encryption
    }
    internal fun isUnencryptedVless(outbound: JSONObject): Boolean = outbound.optString("protocol") == "vless" &&
        outbound.optJSONObject("streamSettings")?.optString("security") == "none" &&
        outbound.optJSONObject("settings")?.optJSONArray("vnext")?.optJSONObject(0)
            ?.optJSONArray("users")?.optJSONObject(0)?.optString("encryption", "none") == "none"

    /** Convert the supported sharing URI to the same strict configuration parser used for files. */
    internal fun wireguardDocument(text: String): String {
        val uri = URI(text)
        require(uri.scheme.equals("wireguard", true))
        val host = uri.host?.removeSurrounding("[", "]") ?: error("Missing WireGuard endpoint")
        val port = if (uri.port < 0) 51820 else uri.port
        require(port in 1..65535 && host.isNotBlank() && uri.rawPath.orEmpty() in setOf("", "/"))
        val q = linkedMapOf<String, String>()
        for (part in uri.rawQuery.orEmpty().split('&').filter(String::isNotEmpty)) {
            val pair = part.split('=', limit = 2)
            val name = decode(pair[0]).lowercase()
            require(name !in q) { "Duplicate option" }
            q[name] = decode(pair.getOrElse(1) { "" })
        }
        require(q.keys.all { it in setOf("publickey", "address", "allowedips", "dns", "mtu", "presharedkey", "persistentkeepalive") || it in AmneziaConfig.labels }) { "UNSUPPORTED_OPTION" }
        val privateKey = decode(uri.rawUserInfo ?: error("Missing WireGuard private key"))
        require((q.values + privateKey).none { '\n' in it || '\r' in it }) { "UNSUPPORTED_OPTION" }
        return buildString {
            append("[Interface]\nPrivateKey = ").append(privateKey)
            append("\nAddress = ").append(q["address"] ?: error("Missing WireGuard address"))
            for ((name, label) in listOf("dns" to "DNS", "mtu" to "MTU")) q[name]?.let { append('\n').append(label).append(" = ").append(it) }
            for ((name, label) in AmneziaConfig.labels) q[name]?.let { append('\n').append(label).append(" = ").append(it) }
            append("\n[Peer]\nPublicKey = ").append(q["publickey"] ?: error("Missing peer key"))
            append("\nEndpoint = ").append(if (':' in host) "[$host]" else host).append(':').append(port)
            append("\nAllowedIPs = ").append(q["allowedips"] ?: error("Missing AllowedIPs"))
            for ((name, label) in listOf("presharedkey" to "PresharedKey", "persistentkeepalive" to "PersistentKeepalive"))
                q[name]?.let { append('\n').append(label).append(" = ").append(it) }
        }
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
        return params.keys.any { it in setOf("ed", "eh", "serviceName", "authority", "heartbeatPeriod", "cs", "fm") } ||
            params["encryption"].orEmpty() !in setOf("", "none") ||
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
        require(auth[0] in setOf("aes-128-gcm", "aes-256-gcm", "chacha20-ietf-poly1305", "xchacha20-ietf-poly1305", "2022-blake3-aes-128-gcm", "2022-blake3-aes-256-gcm", "2022-blake3-chacha20-poly1305")) { "Only authenticated AEAD Shadowsocks methods are supported" }
        if (auth[0].startsWith("2022-")) {
            val size = if (auth[0] == "2022-blake3-aes-128-gcm") 16 else 32
            require(auth[1].split(':').all { WireBase64.decode(it).size == size }) { "INVALID_FEED" }
        }
        return Parsed(name, host, obj("protocol" to "shadowsocks", "settings" to obj("servers" to arr(
            obj("address" to host, "port" to uri.port, "method" to auth[0], "password" to auth[1])))))
    }

    fun parse(raw: String): Parsed {
        val text = CoreSharing.normalize(raw.trim())
        require(text.length <= 65536) { "Core profile too large" }
        if (text.trimStart().startsWith("[Interface]", true)) return if (AmneziaConfig.handles(text)) AmneziaConfig.parse(text) else wireguard(text)
        if (text.startsWith("wireguard://", true)) return parse(wireguardDocument(text)).copy(
            name = URI(text).rawFragment?.let(::decode)?.ifBlank { null } ?: "WireGuard")
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
                only("type", "security", "encryption", "flow", "sni", "fp", "pbk", "sid", "spx", "path", "host", "mode", "alpn", "extra", "headerType", "serviceName", "authority", "ed", "eh", "heartbeatPeriod", "mtu", "tti", "cs", "fm", "allowInsecure", "insecure")
                val encryption = vlessEncryption(q["encryption"])
                require(auth.matches(Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")))
                val transport = when (q["type"] ?: "tcp") { "tcp", "raw" -> "raw"; "xhttp", "ws", "grpc", "httpupgrade" -> q.getValue("type"); "kcp", "mkcp" -> "kcp"; "websocket" -> "ws"; else -> error("UNSUPPORTED_TRANSPORT") }
                val security = q["security"] ?: "none"
                require(security in setOf("none", "tls", "reality"))
                val flow = q["flow"].orEmpty()
                require(flow.isEmpty() || (flow == "xtls-rprx-vision" && transport == "raw" && security != "none"))
                val stream = obj("network" to transport, "security" to security)
                require(transport != "kcp" || security != "reality") { "UNSUPPORTED_OPTION" }
                if (transport == "kcp") {
                    val settings = JSONObject()
                    q["mtu"]?.let { value -> val n = value.toInt(); require(n in 21..65535); settings.put("mtu", n) }
                    q["tti"]?.let { value -> val n = value.toInt(); require(n in 10..1000); settings.put("tti", n) }
                    stream.put("kcpSettings", settings)
                } else require(!q.containsKey("mtu") && !q.containsKey("tti")) { "UNSUPPORTED_OPTION" }
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
                obj("protocol" to "vless", "settings" to obj("vnext" to arr(obj("address" to host, "port" to port, "users" to arr(obj("id" to auth, "encryption" to encryption, "flow" to flow))))), "streamSettings" to stream)
            }
            "trojan", "vmess" -> {
                only("type", "security", "sni", "fp", "path", "host", "alpn", "headerType", "serviceName", "authority", "mode", "ed", "eh", "allowInsecure", "insecure", "cipher", "encryption", "cs", "fm")
                require(auth.isNotEmpty())
                val stream = stream(if (scheme == "trojan") "tls" else "none")
                if (scheme == "trojan") {
                    require(stream.getString("security") == "tls") { "TLS_REQUIRED" }
                    require(q["cipher"].isNullOrBlank()) { "UNSUPPORTED_OPTION" }
                    obj("protocol" to "trojan", "settings" to obj("servers" to arr(obj("address" to host, "port" to port, "password" to auth))), "streamSettings" to stream)
                } else {
                    require(auth.matches(Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")))
                    require(q["cipher"].isNullOrBlank() || q["encryption"].isNullOrBlank() || q["cipher"] == q["encryption"]) { "UNSUPPORTED_OPTION" }
                    val cipher = q["cipher"] ?: q["encryption"] ?: "auto"
                    require(cipher in setOf("auto", "aes-128-gcm", "chacha20-poly1305")) { "UNSUPPORTED_OPTION" }
                    obj("protocol" to "vmess", "settings" to obj("vnext" to arr(obj("address" to host, "port" to port, "users" to arr(obj("id" to auth, "security" to cipher))))), "streamSettings" to stream)
                }
            }
            "hysteria2", "hy2" -> {
                only("sni", "insecure", "allowInsecure", "obfs", "obfs-password", "alpn", "security", "fm", "mport", "hop-interval")
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
            else -> error("Unsupported core protocol")
        }
        outbound.optJSONObject("streamSettings")?.let { stream ->
            q["cs"]?.let { cs ->
                require(stream.optString("security") == "tls") { "UNSUPPORTED_OPTION" }
                stream.getJSONObject("tlsSettings").put("cipherSuites", CoreOptions.cipherSuites(cs))
            }
            q["fm"]?.let { fm ->
                val incoming = CoreOptions.finalMask(fm)
                val existing = stream.optJSONObject("finalmask")
                if (existing == null) stream.put("finalmask", incoming) else {
                    require(incoming.keys().asSequence().none(existing::has)) { "UNSUPPORTED_OPTION" }
                    incoming.keys().forEach { existing.put(it, incoming.get(it)) }
                }
            }
            q["mport"]?.let { ports ->
                require(scheme in setOf("hy2", "hysteria2") && ports.length <= 512 &&
                    ports.split(',').all { range -> range.split('-').let { pair -> pair.size in 1..2 && pair.all { it.toIntOrNull() in 1..65535 } && (pair.size == 1 || pair[0].toInt() <= pair[1].toInt()) } }) { "UNSUPPORTED_OPTION" }
                val seconds = q["hop-interval"]?.removeSuffix("s")?.toInt() ?: 30
                require(seconds in 5..3600)
                val mask = stream.optJSONObject("finalmask") ?: JSONObject().also { stream.put("finalmask", it) }
                val layers = mask.optJSONArray("udp") ?: JSONArray().also { mask.put("udp", it) }
                require((0 until layers.length()).none { layers.getJSONObject(it).optString("type") == "udphop" }) { "UNSUPPORTED_OPTION" }
                layers.put(obj("type" to "udphop", "settings" to obj("mode" to "intervalremote", "interval" to seconds, "remotePorts" to ports)))
            }
            require(q["hop-interval"].isNullOrBlank() || q.containsKey("mport")) { "UNSUPPORTED_OPTION" }
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
                q["heartbeatPeriod"]?.let { value ->
                    val seconds = value.toLong(); require(seconds in 0..4_294_967_295L)
                    settings.put("heartbeatPeriod", seconds)
                }
                stream.put("wsSettings", settings)
            }
            "grpc" -> {
                require(q["mode"].orEmpty() in setOf("", "gun", "multi")) { "UNSUPPORTED_OPTION" }
                stream.put("grpcSettings", obj("serviceName" to q["serviceName"].orEmpty(), "authority" to (q["authority"] ?: q["host"].orEmpty()), "multiMode" to (q["mode"] == "multi")))
            }
            "httpupgrade" -> stream.put("httpupgradeSettings", obj("host" to (q["host"] ?: host), "path" to (q["path"] ?: "/")))
        }
        if (transport != "ws") require(q["ed"].isNullOrBlank() && q["eh"].isNullOrBlank()) { "UNSUPPORTED_OPTION" }
        if (transport != "ws") require(!q.containsKey("heartbeatPeriod")) { "UNSUPPORTED_OPTION" }
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
