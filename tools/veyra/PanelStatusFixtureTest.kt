package app.slipnet

import app.slipnet.data.subscription.*
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.time.Instant

class PanelStatusFixtureTest {
    private val clock = Instant.parse("2026-11-08T10:00:00Z").toEpochMilli()
    private fun read(name: String) = File("app/src/test/resources/int03-status/$name.status.json").readText()
    private fun view(status: AccountStatus, age: Long = 0) = AccountStatusView.evaluate(
        status, clock, 1000, clock, "", clock + age, 1000 + age)

    @Test fun panelFixturesKeepPeriodPartialAndFreshnessSemantics() {
        val active = AccountStatus.read(read("active"))
        val partial = AccountStatus.read(read("partial"))
        val expired = AccountStatus.read(read("expired"))
        val renewed = AccountStatus.read(read("renewed"))
        val reached = AccountStatus.read(read("limit-reached"))
        assertEquals(1134L, active.usedBytes)
        assertEquals(1073741824L, active.limitBytes)
        assertTrue(view(active).trustworthy)
        assertTrue(partial.accepts(active))
        assertEquals(1134L, partial.usedBytes)
        assertNull(partial.sampledAt)
        assertFalse(view(partial).trustworthy)
        assertEquals(AccountSeverity.UNKNOWN, view(partial).severity)
        assertEquals("expired", expired.state)
        assertNull(expired.expiresAt)
        assertEquals(AccountSeverity.CRITICAL, view(expired).severity)
        assertTrue(renewed.accepts(expired))
        assertEquals(active.accountId, renewed.accountId)
        assertNotEquals(active.periodId, renewed.periodId)
        assertEquals(0L, renewed.usedBytes)
        assertFalse(active.accepts(renewed))
        assertEquals(renewed.periodId, reached.periodId)
        assertEquals(0L, reached.remainingBytes)
        assertEquals(AccountSeverity.CRITICAL, view(reached).severity)
        assertFalse(view(active, 16 * 60_000L).trustworthy)
        for (name in listOf("active", "partial", "expired", "renewed", "limit-reached")) {
            assertTrue(runCatching { SubscriptionPayload.read(read(name)) }.isFailure)
        }
    }
}
