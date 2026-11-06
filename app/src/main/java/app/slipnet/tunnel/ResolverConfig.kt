package app.slipnet.tunnel

/** Live native Slipstream configuration, independent of the removed Kotlin packet engine. */
data class ResolverConfig(val host: String, val port: Int, val authoritative: Boolean)
