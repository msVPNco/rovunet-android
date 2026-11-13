package app.slipnet.core

import android.os.Build

/** Process APIs shared by bridges while keeping Android7/API24 support. */
internal object ProcessCompatibility {
    fun isAlive(process: Process): Boolean = try {
        process.exitValue()
        false
    } catch (_: IllegalThreadStateException) { true }

    fun destroyForcibly(process: Process) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) process.destroyForcibly()
        else process.destroy()
    }
}
