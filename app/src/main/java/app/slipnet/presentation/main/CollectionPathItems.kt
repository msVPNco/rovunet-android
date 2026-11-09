package app.slipnet.presentation.main

import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.items
import androidx.compose.runtime.Composable
import app.slipnet.domain.model.ServerProfile

/** Individual lazy rows prevent expanded public feeds from composing thousands of cards. */
internal fun LazyListScope.collectionPathItems(profiles: List<ServerProfile>, content: @Composable (ServerProfile) -> Unit) {
    items(profiles, key = { "collection-path:${it.id}" }, contentType = "collection-path") { content(it) }
}
