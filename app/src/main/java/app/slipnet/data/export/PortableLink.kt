package app.slipnet.data.export

import java.net.URI
import java.net.URLDecoder

/** Deliberately narrow password-based URI dialects. No silent option loss. */
data class PortableLink(val scheme: String, val host: String, val port: Int,
                        val username: String, val password: String, val name: String) {
    companion object {
        fun parse(text: String): PortableLink {
            require(text.length <= 65536)
            val uri = URI(text)
            val scheme = uri.scheme?.lowercase() ?: error("Missing scheme")
            val defaultPort = when (scheme) { "ssh" -> 22; "socks5" -> 1080; "naive+https" -> 443; else -> error("Unsupported scheme") }
            require(uri.rawQuery == null && (uri.rawPath.isNullOrEmpty() || uri.rawPath == "/"))
            val host = uri.host?.removePrefix("[")?.removeSuffix("]") ?: error("Missing host")
            require(host.isNotBlank())
            val port = if (uri.port == -1) defaultPort else uri.port
            require(port in 1..65535)
            fun decode(s: String) = URLDecoder.decode(s.replace("+", "%2B"), "UTF-8")
            val auth = uri.rawUserInfo.orEmpty().split(':', limit = 2)
            val user = decode(auth[0])
            val password = decode(auth.getOrElse(1) { "" })
            if (scheme != "socks5") require(user.isNotEmpty() && password.isNotEmpty())
            val name = uri.rawFragment?.let(::decode)?.takeIf { it.isNotBlank() } ?: scheme.uppercase()
            return PortableLink(scheme, host, port, user, password, name)
        }
    }
}
