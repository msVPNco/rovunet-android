package app.slipnet.domain

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class ConnectionHistoryTest {
    private val now = 1_800_000_000_000L
    private val ssh = ServerProfile(id = 1, name = "SSH", domain = "server.invalid", tunnelType = TunnelType.SSH,
        sshUsername = "synthetic-user", sshPassword = "synthetic-secret", collectionId = "one")
    private val naive = ssh.copy(id = 2, name = "Naive", tunnelType = TunnelType.NAIVE, naivePassword = "another-secret")
    private val other = ssh.copy(id = 3, collectionId = "two")
    private val profiles = listOf(ssh, naive, other)
    private fun success(time: Long = now) = PingResult.Success(85, true, measuredAt = time, quick = true)

    @Test fun deletingReviewTokensMustNotInvalidateMeasurementsOrDuplicateEquality() {
        val before = ssh.copy(deletionReview = ProfileDeletionReview("ciphertext-before", "content-before"))
        val after = before.copy(isActive = true, lastConnectedAt = now, sortOrder = 20,
            deletionReview = ProfileDeletionReview("ciphertext-after", "content-after"))
        assertEquals(setOf(ssh.id), ProbeCache.unchanged(listOf(before), listOf(after)))
        assertEquals(ProfileDuplicates.key(before), ProfileDuplicates.key(after))
        assertEquals(1, ProfileDuplicates.plan(listOf(before, after.copy(id = 99))).size)
        assertTrue(ProbeCache.unchanged(listOf(before), listOf(after.copy(sshPassword = "rotated"))).isEmpty())
    }

    @Test fun everyResultSurvivesTheFinalPersistentSortAndActiveProfileRestoration() {
        val results = mapOf(1L to success(), 2L to success().copy(latencyMs = 300), 3L to PingResult.Error("Connection timed out", now))
        val sorted = ProfileRanking.sort(profiles, results).mapIndexed { index, p ->
            p.copy(sortOrder = index, isActive = p.id == 1L,
                deletionReview = ProfileDeletionReview("changed-$index", "updated-$index"))
        }
        val unchanged = ProbeCache.unchanged(profiles, sorted)
        assertEquals(results, results.filterKeys { it in unchanged })
    }

    @Test fun lastManualPathIsRememberedSeparatelyForEachCollectionAndSurvivesRestart() {
        val choices = mapOf("one" to ssh.id, "two" to other.id)
        val state = ConnectionHistory.capture(profiles, "one", choices, emptyMap(), false, false, now)
        val restored = ConnectionHistory.decode(ConnectionHistory.encode(state))
        val remembered = ConnectionHistory.choices(restored, profiles)
        assertEquals(ssh.id, restored.selectedAnchorId)
        assertEquals(ssh.id, ConnectionChoice.rememberedPath("one", remembered, profiles))
        assertEquals(other.id, ConnectionChoice.rememberedPath("two", remembered, profiles))
        val failed = mapOf(1L to PingResult.Error("old test failed", now))
        assertEquals(ssh.id, (ConnectionChoice.resolve(remembered["one"], profiles) as ConnectionChoice.Target.Direct).profile.id)
        assertEquals(naive.id, ConnectionChoice.initialPath(listOf(ssh, naive), failed, now)?.id)
        assertNull(ConnectionChoice.initialPath(listOf(ssh), failed, now))
    }

    @Test fun restartingRestoresAllMeasurementsAsHistoricalWithoutSavingSecretsOrReviewTokens() {
        val source = profiles.map { it.copy(deletionReview = ProfileDeletionReview("not-for-storage", "review")) }
        val measured = mapOf(1L to success(), 2L to PingResult.Error("Tunnel request timed out", now), 3L to PingResult.Testing("Connecting"))
        val saved = ConnectionHistory.capture(source, "one", mapOf("one" to 1L), measured, true, false, now)
        val json = ConnectionHistory.encode(saved)
        for (secret in listOf("synthetic-secret", "another-secret", "not-for-storage", "server.invalid", "synthetic-user")) {
            assertFalse("History leaked profile content: $secret", json.contains(secret))
        }
        val restored = ConnectionHistory.restoreResults(ConnectionHistory.decode(json), profiles, now)
        assertEquals(setOf(1L, 2L), restored.keys)
        assertEquals(85, (restored[1L] as PingResult.Success).latencyMs.toInt())
        assertFalse((restored[1L] as PingResult.Success).currentNetwork)
        assertFalse((restored[2L] as PingResult.Error).currentNetwork)
    }

    @Test fun editedOrDeletedProfilesCannotReuseOldEvidence() {
        val saved = ConnectionHistory.capture(profiles, "one", emptyMap(), mapOf(1L to success(), 2L to success()), false, false, now)
        val changed = listOf(ssh.copy(sshPassword = "changed"), other)
        assertTrue(ConnectionHistory.restoreResults(saved, changed, now).isEmpty())
        assertEquals(setOf(2L), ConnectionHistory.restoreResults(saved, listOf(ssh.copy(domain = "new.invalid"), naive), now).keys)
    }

    @Test fun oldEvidenceExpiresAndFutureTimestampsAreNotTrusted() {
        val saved = ConnectionHistory.capture(profiles, "one", emptyMap(), mapOf(1L to success()), false, false, now)
        assertTrue(ConnectionHistory.restoreResults(saved, profiles, now + 8 * 86_400_000L).isEmpty())
        assertTrue(ConnectionHistory.restoreResults(saved, profiles, now - 1).isEmpty())
        assertTrue(ConnectionHistory.decode("invalid-json").results.isEmpty())
    }

    @Test fun boundedHistoryAlwaysRetainsTheCurrentManualChoice() {
        val large = (1L..2_001L).map { ssh.copy(id = it, collectionId = "group-$it") }
        val choices = large.associate { it.collectionId to it.id }
        val state = ConnectionHistory.capture(large, "group-2001", choices, emptyMap(), false, false, now)
        assertEquals(2_000, state.manualIds.size)
        assertEquals(2_001L, state.selectedAnchorId)
        assertEquals(2_001L, ConnectionHistory.choices(state, large)["group-2001"])
    }

    @Test fun aNetworkChangeRetainsMeasurementsButCannotTreatThemAsCurrentEvidence() {
        val results = ProbeCache.historical(mapOf(1L to success(), 2L to PingResult.Error("failed", now)))
        assertEquals(2, results.size)
        assertFalse((results[1L] as PingResult.Success).currentNetwork)
        assertEquals(naive.id, ProfileCollections.ordered(listOf(naive, ssh), results).first().id)
    }
}
