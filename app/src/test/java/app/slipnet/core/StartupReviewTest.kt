package app.slipnet.core

import app.slipnet.domain.model.ProfileDeletionReview
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.ProfileDuplicates
import com.google.gson.Gson
import org.junit.Assert.*
import org.junit.Test

class StartupReviewTest {
    @Test fun listEqualityAndRenderingDoNotHashRowsWhileDeletionComputesBothDigestsOnlyOnce() {
        var calculations = 0
        val review = ProfileDeletionReview.deferred(listOf(7L, "frozen-row")) {
            calculations++; "stored-digest" to "canonical-content"
        }
        val same = ProfileDeletionReview.deferred(listOf(7L, "frozen-row")) { error("UI equality must not hash") }
        val different = ProfileDeletionReview.deferred(listOf(7L, "changed-row")) { error("UI equality must not hash") }
        val profile = ServerProfile(id=7, name="Synthetic", deletionReview=review)
        assertEquals(review,same);assertEquals(review.hashCode(),same.hashCode())
        assertNotEquals(review,different)
        assertEquals(profile,profile.copy(deletionReview=same))
        assertEquals(0,calculations)
        assertEquals("canonical-content",review.contentDigest)
        assertEquals("stored-digest",review.storedDigest)
        assertEquals("canonical-content",review.contentDigest)
        assertEquals(1,calculations)
        assertFalse(review.toString().contains("frozen-row"))
        assertFalse(Gson().toJson(profile).contains("deletionReview"))
        assertNull(ProfileDuplicates.key(profile).deletionReview)
    }
    @Test fun eagerReviewCompatibilityRetainsValueEqualityWithoutLeakingDigests() {
        val review=ProfileDeletionReview("synthetic-stored","synthetic-content")
        assertEquals(review,ProfileDeletionReview("synthetic-stored","synthetic-content"))
        assertNotEquals(review,ProfileDeletionReview("changed","synthetic-content"))
        assertEquals("synthetic-stored",review.storedDigest)
        assertFalse(review.toString().contains("synthetic-stored"))
    }
}
