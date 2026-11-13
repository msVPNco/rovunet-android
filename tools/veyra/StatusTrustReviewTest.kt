package app.slipnet

import app.slipnet.data.subscription.*
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

/** Pure policy/DTO tests. These do not substitute for Android Room execution. */
class StatusTrustReviewTest {
    private val time = java.time.Instant.parse("2026-11-08T10:00:00Z").toEpochMilli()
    private val address = "https://subscription.example.invalid/sub/" + "a".repeat(32) + ".json"
    private fun status() = AccountStatus("b".repeat(32), "c".repeat(32), "d".repeat(32), "active",
        time + 86400000, 1073741824, 1134, "complete", time, time, 10)
    private fun binding() = PrivateStatusBinding("source-A", status().serverId, status().accountId, 10)

    @Test fun greaterRevisionCannotTransferServerOrAccount() {
        val old = status()
        for (bad in listOf(old.copy(serverId = "e".repeat(32), revision = 11), old.copy(accountId = "e".repeat(32), revision = 11))) {
            assertFalse(bad.accepts(old))
            assertFalse(PrivateStatusPolicy.accepts(binding(), "source-A", bad, old))
        }
    }
    @Test fun renewalAdvancesSameIdentityAndIndependentHighWater() {
        val renewed = status().copy(periodId = "e".repeat(32), usedBytes = 0, revision = 11)
        assertTrue(PrivateStatusPolicy.accepts(binding(), "source-A", renewed, status()))
        assertFalse(PrivateStatusPolicy.accepts(binding(), "source-A", renewed.copy(revision = 10), status()))
        assertFalse(PrivateStatusPolicy.accepts(binding(), "source-A", status().copy(revision = 9), status()))
        assertFalse(PrivateStatusPolicy.accepts(binding().copy(highWater = 12), "source-A", renewed, status()))
    }
    @Test fun newUrlCannotResetTrustOfAnExistingSource() {
        assertFalse(PrivateStatusPolicy.accepts(binding(), "source-B", status().copy(revision = 11), status()))
        assertFalse(PrivateStatusPolicy.accepts(null, "source-B", status(), null, hasPriorSourceBinding = true))
        assertTrue(PrivateStatusPolicy.accepts(null, "new-explicit-import", status(), null))
    }
    @Test fun latestCommittedRevisionWinsInsteadOfAnOldHttpSnapshot() {
        val committed = status().copy(revision = 12, usedBytes = 2000)
        val latestBinding = binding().copy(highWater = 12)
        assertFalse(PrivateStatusPolicy.accepts(latestBinding, "source-A", status().copy(revision = 11), committed))
        assertTrue(PrivateStatusPolicy.accepts(latestBinding, "source-A", committed.copy(revision = 13), committed))
        assertFalse(PrivateStatusPolicy.accepts(latestBinding, "source-A", committed.copy(usedBytes = 0), committed))
    }
    @Test fun endpointFamiliesAreExactAndV2StatusRemainsProposed() {
        val legacy = address.removeSuffix(".json") + ".status.json"
        assertEquals(legacy, PrivateStatusPolicy.endpoint(address))
        assertEquals(legacy, PrivateStatusPolicy.endpoint(address.removeSuffix(".json") + ".windows.json"))
        val v2 = address.replace("/sub/", "/v2/sub/")
        assertNull(PrivateStatusPolicy.endpoint(v2))
        val proposed = requireNotNull(PrivateStatusPolicy.endpoint(v2, proposedV2 = true))
        assertFalse(PrivateStatusPolicy.redirectAllowed(proposed, proposed))
        assertTrue(PrivateStatusPolicy.redirectAllowed(proposed, proposed, proposedV2 = true))
        assertEquals(v2.removeSuffix(".json") + ".status.json", PrivateStatusPolicy.endpoint(v2, proposedV2 = true))
        assertEquals(v2.removeSuffix(".json") + ".windows.status.json", PrivateStatusPolicy.endpoint(v2.removeSuffix(".json") + ".windows.json", proposedV2 = true))
        for (bad in listOf(address.replace("/sub/", "/any/sub/"), address + "?redirect=1", address.replace("https:", "http:"), address.replace("/sub/", "/%73ub/"), address + "#fragment")) assertNull(PrivateStatusPolicy.endpoint(bad))
    }
    @Test fun privateRedirectCannotChangeAuthorityPortTokenOrPath() {
        val target = requireNotNull(PrivateStatusPolicy.endpoint(address))
        assertTrue(PrivateStatusPolicy.redirectAllowed(target, target))
        assertTrue(PrivateStatusPolicy.redirectAllowed(target, target.replace(".invalid/", ".invalid:443/")))
        for (bad in listOf(target.replace("subscription.", "other."), target.replace(".invalid/", ".invalid:8443/"), target.replace("/sub/", "/other/"), target.replace("a".repeat(32), "e".repeat(32)), target + "?next=1", target + "#x", target.replace("https:", "http:"), target.replace("https://", "https://user@")))
            assertFalse(bad, PrivateStatusPolicy.redirectAllowed(target, bad))
    }
    @Test fun finiteTotalAndExpiryDoNotRequireDirectionalUsage() {
        for (header in listOf("total=1073741824", "total=1073741824;expire=1893456000")) {
            val dto = SubscriptionMetadata.parse(header, time)
            assertEquals(ProviderLimit.FINITE, dto.limit); assertEquals(1073741824L, dto.limitBytes)
            assertEquals(ProviderUsage.UNKNOWN, dto.usage); assertNull(dto.usedBytes)
            val display = SubscriptionMetadata.readStatus(dto.json().toString())
            assertTrue(display.volumeKnown); assertNull(display.remainingBytes)
            val view = AccountStatusView.evaluate(display, time, 1000, time, "", time, 1000)
            assertTrue(view.trustworthy)
            if ("expire" in header) assertEquals(1893456000000L, display.expiresAt) else assertFalse(display.expiryKnown)
        }
    }
    @Test fun oneDirectionIsOnlyALowerBoundAndNeverACompleteBalance() {
        for (direction in listOf("upload", "download")) {
            val dto = SubscriptionMetadata.parse("$direction=12;total=100", time)
            assertEquals(ProviderUsage.PARTIAL, dto.usage); assertEquals(12L, dto.lowerBound)
            assertNull(dto.usedBytes); assertNull(dto.display().remainingBytes)
            assertEquals(12L, SubscriptionMetadata.readStatus(dto.json().toString()).usageLowerBound)
        }
    }
    @Test fun zeroMissingTimeOnlyVolumeOnlyAndMixedHaveDistinctTypes() {
        assertEquals(ProviderLimit.UNLIMITED, SubscriptionMetadata.parse("total=0", time).limit)
        assertEquals(ProviderLimit.UNKNOWN, SubscriptionMetadata.parse("upload=0;download=0", time).limit)
        assertEquals(0L, SubscriptionMetadata.parse("upload=0;download=0", time).usedBytes)
        assertEquals(ProviderExpiry.NONE, SubscriptionMetadata.parse("expire=0", time).expiry)
        assertEquals(ProviderExpiry.AT, SubscriptionMetadata.parse("expire=1893456000", time).expiry)
        assertEquals(ProviderLimit.UNKNOWN, SubscriptionMetadata.parse("expire=1893456000", time).limit)
        assertEquals(ProviderExpiry.UNKNOWN, SubscriptionMetadata.parse("total=100", time).expiry)
        assertEquals(70L, SubscriptionMetadata.parse("total=100;upload=10;download=20;expire=1893456000", time).display().remainingBytes)
    }
    @Test fun invalidHeadersCannotBecomeGoodMetadata() {
        for (bad in listOf("", "total=-1", "upload=9223372036854775808", "upload=9223372036854775807;download=1", "expire=9223372036854775807", "total=1;total=1", "total=1;total=2", "total=1.5", "total=one", "other=1", "expire=-1"))
            assertTrue(bad, runCatching { SubscriptionMetadata.parse(bad, time) }.isFailure)
    }
    @Test fun publicDtoDoesNotRelaxPrivateSchemaOrOverrideItsIdentity() {
        val private = status()
        val provider = SubscriptionMetadata.usage("total=1073741824;expire=1893456000", time).toString()
        assertTrue(runCatching { AccountStatus.read(provider) }.isFailure)
        assertFalse(PrivateStatusPolicy.accepts(binding(), "source-A", SubscriptionMetadata.readStatus(provider), private))
        assertTrue(runCatching { AccountStatus.read(JSONObject(private.json()).put("used_bytes", JSONObject.NULL).toString()) }.isFailure)
        assertEquals(1134L, AccountStatus.read(private.json()).usedBytes)
        assertNull(private.copy(metering = "partial").remainingBytes)
        assertNull(private.copy(metering = "unavailable").remainingBytes)
    }
    @Test fun legacyProviderCacheIsStillReadAsAdvisory() {
        val json = JSONObject().put("source", "subscription-userinfo").put("used", 10).put("limit", 100)
            .put("volume_known", true).put("expiry", JSONObject.NULL).put("expiry_known", false).put("received", time)
        val display = SubscriptionMetadata.readStatus(json.toString())
        assertTrue(display.providerReported); assertEquals(90L, display.remainingBytes); assertFalse(display.expiryKnown)
    }
}
