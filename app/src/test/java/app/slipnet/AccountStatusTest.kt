package app.slipnet

import app.slipnet.data.subscription.*
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class AccountStatusTest {
    private val time = java.time.Instant.parse("2026-11-08T10:00:00Z").toEpochMilli()
    private val gib = 1L shl 30
    private fun status() = AccountStatus("a".repeat(32), "b".repeat(32), "c".repeat(32), "active",
        time + 30 * 86_400_000L, 10 * gib, gib, "complete", time, time, 7)
    private fun view(s: AccountStatus, age: Long = 0, error: String = "") = AccountStatusView.evaluate(s,
        time, 1000, time, error, time + age, 1000 + age)

    @Test fun unlimitedIsNotZeroAndNeedsNoRunningCollectionTimer() {
        val s = status().copy(limitBytes = null, usedBytes = null, expiresAt = null,
            metering = "not_metered", sampledAt = null, generatedAt = time - 86_400_000)
        val parsed = AccountStatus.read(s.json())
        assertNull(parsed.remainingBytes)
        assertEquals(AccountSeverity.NORMAL, view(parsed).severity)
    }
    @Test fun quotaAndTimeThresholdsUseTheEarlierLimit() {
        assertEquals(AccountSeverity.NORMAL, view(status()).severity)
        assertEquals(AccountSeverity.WARNING, view(status().copy(usedBytes = 8 * gib)).severity)
        assertEquals(AccountSeverity.CRITICAL, view(status().copy(usedBytes = 10 * gib)).severity)
        assertEquals(AccountSeverity.WARNING, view(status().copy(expiresAt = time + 3 * 86_400_000L)).severity)
        assertEquals(AccountSeverity.CRITICAL, view(status().copy(expiresAt = time + 86_400_000L)).severity)
        assertEquals(0L, status().copy(usedBytes = 11 * gib).remainingBytes)
    }
    @Test fun timeOnlyAccountKeepsItsDeadlineWithoutInventingTraffic() {
        val v = view(status().copy(limitBytes = null, usedBytes = null, metering = "not_metered",
            sampledAt = null, expiresAt = time + 86_400_000L))
        assertEquals(AccountSeverity.CRITICAL, v.severity)
        assertNull(v.status?.remainingBytes)
        assertEquals(86_400_000L, v.remainingMillis)
    }
    @Test fun missingOrPartialMeteringCannotLookLikeFullAllowance() {
        for (metering in listOf("partial", "unavailable")) {
            val v = view(status().copy(metering = metering, usedBytes = 0))
            assertFalse(v.trustworthy)
            assertEquals(AccountSeverity.UNKNOWN, v.severity)
        }
    }
    @Test fun cachedExpiryIsNotReassertedAfterAnUnavailableResponse() {
        assertEquals(AccountSeverity.UNKNOWN, view(status().copy(state = "expired"), error = "UNAVAILABLE").severity)
        assertEquals(AccountSeverity.CRITICAL, view(status().copy(state = "expired", sampledAt = null,
            generatedAt = time - 86_400_000, metering = "unavailable")).severity)
    }
    @Test fun staleSamplesAndClockChangesLoseAuthority() {
        assertEquals(AccountSeverity.UNKNOWN, view(status(), age = 16 * 60_000).severity)
        assertEquals(AccountSeverity.UNKNOWN, view(status().copy(sampledAt = time - 16 * 60_000)).severity)
        assertEquals(AccountSeverity.UNKNOWN, AccountStatusView.evaluate(status(), time, 1000, time,
            "", time + 86_400_000, 2000).severity)
    }
    @Test fun wrongPhoneClockDoesNotChangeServerCountdown() {
        val phone = time + 4 * 86_400_000L
        val v = AccountStatusView.evaluate(status(), phone, 1000, time, "", phone + 120_000, 121_000)
        assertEquals(AccountSeverity.NORMAL, v.severity)
        assertEquals(30 * 86_400_000L - 120_000, v.remainingMillis)
    }
    @Test fun newPeriodReplacesOldUsageAndRevisionScope() {
        val previous = status()
        val renewed = previous.copy(periodId = "d".repeat(32), usedBytes = 0, revision = 8)
        assertTrue(renewed.accepts(previous))
        assertEquals(10 * gib, renewed.remainingBytes)
        assertFalse(previous.copy(revision = 6).accepts(previous))
        assertFalse(previous.accepts(renewed))
        assertTrue(previous.copy(generatedAt = time + 300_000, sampledAt = time + 300_000).accepts(previous))
        assertFalse(previous.copy(usedBytes = 0).accepts(previous))
    }
    @Test fun parserRejectsMalformedCountsAndVersions() {
        for ((key, bad) in listOf("used_bytes" to -1, "limit_bytes" to 0, "revision" to 1.5,
            "used_bytes" to "1", "version" to 2, "account_status" to "deleted", "limit_bytes" to JSONObject.NULL)) {
            try { AccountStatus.read(JSONObject(status().json()).put(key, bad).toString()); fail(key) }
            catch (_: IllegalArgumentException) { }
        }
        assertEquals(status(), AccountStatus.read(status().json()))
    }
    @Test fun unknownFieldsAreNotRetainedInTheCache() {
        val payload = JSONObject(status().json()).put("future_metadata", "discard-me").toString()
        assertFalse(AccountStatus.read(payload).json().contains("discard-me"))
    }
}
