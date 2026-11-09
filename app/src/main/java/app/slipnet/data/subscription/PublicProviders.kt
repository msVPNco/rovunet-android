package app.slipnet.data.subscription

import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

/** Built-in sources are explicit opt-ins; nested URLs are allowed only for this fixed Mahsa catalog. */
internal object PublicProviders {
    enum class Provider(val address: String) {
        ANONYMOUS("https://raw.githubusercontent.com/4n0nymou3/multi-proxy-config-fetcher/refs/heads/main/configs/proxy_configs.txt"),
        MAHSA("https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa")
    }
    private val mahsaChild = Regex("""https://raw\.githubusercontent\.com/mahsanet/MahsaFreeConfig/(?:refs/heads/main|main)/(?:mci|mtn)/sub_[1-4]\.txt""")
    fun mahsaChildren(document: String): List<String> {
        val urls = document.lineSequence().map(String::trim).filter { it.isNotEmpty() && !it.startsWith("#") }.toList()
        require(urls.size in 1..8 && urls.distinct().size == urls.size && urls.all(mahsaChild::matches)) { "INVALID_FEED" }
        return urls
    }
    suspend fun download(address: String, fetch: suspend (String, Int) -> String): String {
        if (address != Provider.MAHSA.address) return fetch(address, PublicFeed.MAX_BYTES)
        val urls = mahsaChildren(fetch(address, 65_536))
        val combined = StringBuilder()
        for (url in urls) {
            currentCoroutineContext().ensureActive()
            // A failed child aborts the whole update; never remove old profiles on partial downloads.
            val expanded = PublicFeed.expand(fetch(url, PublicFeed.MAX_BYTES / 8))
            combined.append(expanded).append('\n')
            require(combined.length <= PublicFeed.MAX_BYTES && combined.toString().toByteArray(Charsets.UTF_8).size <= PublicFeed.MAX_BYTES) { "PUBLIC_FEED_LIMIT" }
        }
        return combined.toString()
    }
}
