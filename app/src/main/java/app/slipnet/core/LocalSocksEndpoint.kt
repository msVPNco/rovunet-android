package app.slipnet.core

import java.net.InetSocketAddress
import java.net.ServerSocket
import java.util.UUID

/** Session credentials are never persisted, logged or included in generated profile links. */
class LocalSocksEndpoint(val port: Int, val user: String?, val password: String?) {
    companion object {
        fun privateCore(): LocalSocksEndpoint {
            val port = ServerSocket().use {
                it.bind(InetSocketAddress("127.0.0.1", 0))
                it.localPort
            }
            return LocalSocksEndpoint(port, UUID.randomUUID().toString(), UUID.randomUUID().toString())
        }
    }
}
