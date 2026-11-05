package app.slipnet.domain.model
enum class TunnelType { SSH, DNSTT, SNOWFLAKE }
data class ServerProfile(val id: Long, val tunnelType: TunnelType = TunnelType.SSH, val isExpired: Boolean=false, val boundDeviceId: String="", val resolvers: List<DnsResolver> = emptyList())
data class DnsResolver(val host: String, val port: Int=53, val authoritative: Boolean=false)
data class ProfileChain(val id: Long)
class TrafficStats
