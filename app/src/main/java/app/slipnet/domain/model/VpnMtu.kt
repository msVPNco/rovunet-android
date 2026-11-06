package app.slipnet.domain.model

object VpnMtu {
    const val DEFAULT = 1280
    const val MAX = 1500

    val preferenceRange = 512..MAX

    fun range(blockIpv6: Boolean): IntRange = if (blockIpv6) preferenceRange else DEFAULT..MAX

    fun effective(requested: Int, blockIpv6: Boolean): Int = requested.coerceIn(range(blockIpv6))
}
