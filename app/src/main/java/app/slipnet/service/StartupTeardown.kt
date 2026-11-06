package app.slipnet.service

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.withContext

/** Close only the retiring attempt's pending I/O before waiting for its workers. */
internal object StartupTeardown {
    suspend fun await(abortStartup: () -> Unit, vararg jobs: Job?) {
        withContext(Dispatchers.IO) { abortStartup() }
        jobs.forEach { it?.join() }
    }
}
