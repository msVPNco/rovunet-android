package app.slipnet.data.export

/** Routing only; acceptance is decided by ConfigImporter and the installed engine. */
object ConfigSchemes {
    private val schemes = setOf("rovunet-collection", "rovunet", "rovunet-enc", "rovunet-bundle-enc", "veyra", "veyra-enc", "veyra-bundle-enc", "slipnet", "slipnet-enc", "slipnet-bundle-enc", "vless", "hysteria2", "hy2", "wireguard", "ssh", "socks5", "naive+https")
    fun accepts(input: String): Boolean = input.trimStart().startsWith("[Interface]", true) || input.substringBefore("://").lowercase() in schemes
    fun canonical(input: String): String {
        val scheme = input.substringBefore("://").lowercase()
        val legacy = when (scheme) {
            "rovunet", "veyra" -> "slipnet"
            "rovunet-enc", "veyra-enc" -> "slipnet-enc"
            "rovunet-bundle-enc", "veyra-bundle-enc" -> "slipnet-bundle-enc"
            else -> return input
        }
        return legacy + "://" + input.substringAfter("://")
    }
}
