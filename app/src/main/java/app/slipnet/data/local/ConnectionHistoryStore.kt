package app.slipnet.data.local

import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.domain.model.ConnectionHistory
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import javax.inject.Inject
import javax.inject.Singleton

/** Application-owned, ordered writes survive leaving the screen. One encryption per coalesced snapshot, off Main. */
@Singleton
class ConnectionHistoryStore @Inject constructor(private val preferences: PreferencesDataStore) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val writes = Channel<Pair<Long, ConnectionHistory.Input>>(Channel.CONFLATED)
    private val lock = Any()
    private var latest: ConnectionHistory.State? = null
    private var latestInput: ConnectionHistory.Input? = null
    private var revision = 0L
    private val completed = MutableStateFlow(0L)
    private val failed = MutableStateFlow(false)

    init {
        scope.launch {
            for ((sequence, input) in writes) {
                try {
                    val state = input.capture()
                    synchronized(lock) {
                        if (revision == sequence) { latest = state; latestInput = null }
                    }
                    preferences.saveConnectionHistory(ConnectionHistory.encode(state))
                    failed.value = false
                } catch (_: Exception) {
                    // History loss must not break access to actual profiles or their credentials.
                    failed.value = true
                } finally { completed.value = sequence }
            }
        }
    }

    suspend fun load(): ConnectionHistory.State = withContext(Dispatchers.IO) {
        val input = synchronized(lock) { latestInput }
        if (input != null) return@withContext input.capture()
        val saved = synchronized(lock) { latest } ?: runCatching {
            ConnectionHistory.decode(preferences.connectionHistory.first())
        }.getOrDefault(ConnectionHistory.State())
        val newer = synchronized(lock) { latest = saved; latestInput }
        newer?.capture() ?: saved
    }

    fun remember(input: ConnectionHistory.Input) = synchronized(lock) {
        latestInput = input
        revision++
        check(writes.trySend(revision to input).isSuccess)
    }

    /** Used by lifecycle tests and explicit persistence boundaries, never blocks the UI thread. */
    suspend fun flush(): Boolean {
        val expected = synchronized(lock) { revision }
        completed.first { it >= expected }
        return !failed.value
    }
}
