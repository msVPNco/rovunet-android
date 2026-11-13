package app.slipnet.data.subscription

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** Built-in identities are separate from presentation and historical download addresses. */
internal object PublicProviders {
    enum class Provider(val address: String) {
        ANONYMOUS("https://raw.githubusercontent.com/4n0nymou3/multi-proxy-config-fetcher/refs/heads/main/configs/proxy_configs.txt"),
        MAHSA("https://raw.githubusercontent.com/mahsanet/MahsaFreeConfig/main/app/sub.txt")
    }
    private val mahsaAliases = setOf(Provider.MAHSA.address,
        "https://raw.githubusercontent.com/mahsanet/MahsaFreeConfig/refs/heads/main/app/sub.txt",
        "https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa",
        "https://raw.githubusercontent.com/hiddify/hiddify-app/main/test.configs/mahsa")
    fun provider(address: String): Provider? = when (address.trim()) {
        in mahsaAliases -> Provider.MAHSA
        Provider.ANONYMOUS.address,
        "https://raw.githubusercontent.com/4n0nymou3/multi-proxy-config-fetcher/main/configs/proxy_configs.txt" -> Provider.ANONYMOUS
        else -> null
    }
    fun canonical(address: String): String = provider(address)?.address ?: address.trim()
    fun hidesAddress(sourceId: String?, address: String): Boolean = sourceId?.startsWith("public:") == true || provider(address) != null
    suspend fun download(address: String, fetch: suspend (String, Int) -> String): String {
        currentCoroutineContext().ensureActive()
        val target = canonical(address) // No request is ever made to a legacy alias.
        return if (provider(target) == Provider.MAHSA) MahsaFeed.decode(fetch(target, MahsaFeed.MAX_BYTES))
            else fetch(target, PublicFeed.MAX_BYTES)
    }
}
