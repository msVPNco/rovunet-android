package app.slipnet.core

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ProcessCompatibilityTest {
    private fun awaitExit(process: Process): Boolean {
        val deadline = System.nanoTime() + 2_000_000_000L
        while (ProcessCompatibility.isAlive(process) && System.nanoTime() < deadline) Thread.sleep(5)
        return !ProcessCompatibility.isAlive(process)
    }
    @Test fun liveChildAndTerminationUseTheLegacyCompatiblePath() {
        // Gradle uses mock SDK_INT=0; the portable host fixture uses API24. This exercises
        // the legacy destroy fallback on a real host child, not an Android device.
        val process = ProcessBuilder("sh", "-c", "printf ready; exec sleep 30").start()
        try {
            repeat(5) { process.inputStream.read() }
            assertTrue(ProcessCompatibility.isAlive(process))
            ProcessCompatibility.destroyForcibly(process)
            assertTrue(awaitExit(process))
            process.exitValue()
            assertFalse(ProcessCompatibility.isAlive(process))
        } finally {
            process.destroy()
        }
    }

    @Test fun anAlreadyExitedChildIsNotReportedAsRunning() {
        val process = ProcessBuilder("sh", "-c", "exit 0").start()
        assertTrue(awaitExit(process))
        assertFalse(ProcessCompatibility.isAlive(process))
    }
}
