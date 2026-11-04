package app.slipnet.domain.model
data class ServerProfile(val id: Long, val isExpired: Boolean=false, val boundDeviceId: String="", val resolvers: List<DnsResolver> = emptyList())
data class DnsResolver(val host: String, val port: Int=53, val authoritative: Boolean=false)
data class ProfileChain(val id: Long)
class TrafficStats
