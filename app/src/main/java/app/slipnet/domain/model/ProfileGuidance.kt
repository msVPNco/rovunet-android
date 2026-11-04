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
    const val GUIDE = "Start with a profile that passes the HTTPS test on your current network. The protocol name alone cannot predict speed.\n\nWeb / TCP: REALITY, XHTTP, VLESS WebSocket, SSH and NaiveProxy are candidates when UDP is restricted. Their servers still need to be reachable. SOCKS5 depends on the proxy server.\n\nUDP: Hysteria2 and WireGuard require a working UDP path. If UDP is blocked, try a Web / TCP profile.\n\nDNS / alternative: DNSTT, NoizDNS, VayDNS, Slipstream and Soar can provide alternative paths when normal access fails; DNS tunnels are usually a fallback for light traffic. DoH and Tor behave differently and depend on their configuration.\n\nHTTPS test: three fresh HTTPS requests through the selected tunnel to www.gstatic.com. The displayed number includes SOCKS connection, TLS and the HTTP response; it is not ICMP ping or download speed. 3/3 means three requests succeeded in this short test, not proven long-term stability. Spread is max minus min. Results depend on the test destination and become stale when the network changes.\n\nTest all while disconnected: profiles connect one at a time, then disconnect and sort by measured HTTPS latency. The test uses a small amount of traffic and may take about a minute per unavailable profile. Unverified TCP port checks do not count as working VPN connections."
}
