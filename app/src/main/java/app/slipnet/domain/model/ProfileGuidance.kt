package app.slipnet.domain.model

object ProfileGuidance {
    const val WEB = "Web / TCP paths"
    const val UDP = "UDP paths"
    const val ALTERNATIVE = "DNS / alternative paths"
    fun category(p: ServerProfile): String = when (p.tunnelType) {
        TunnelType.XRAY -> if (p.coreLink.trimStart().startsWith("[Interface]", true) || p.coreLink.startsWith("hy", true)) UDP else WEB
        TunnelType.VLESS, TunnelType.SSH, TunnelType.NAIVE, TunnelType.NAIVE_SSH, TunnelType.SOCKS5 -> WEB
        else -> ALTERNATIVE
    }
}
