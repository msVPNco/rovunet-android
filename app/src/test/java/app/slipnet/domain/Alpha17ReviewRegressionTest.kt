package app.slipnet.domain

import app.slipnet.domain.model.*
import org.junit.Assert.*
import org.junit.Test

class Alpha17ReviewRegressionTest {
    @Test fun secondaryNetworkEventsAndDuplicateCallbacksKeepFreshMeasurements() {
        val routes = PhysicalNetworkTracker("wifi-primary:addresses")
        assertFalse(routes.update("wifi-primary:addresses")) // cellular became available, selected route unchanged
        assertFalse(routes.update("wifi-primary:addresses")) // secondary cellular disappeared
        assertTrue(routes.update("cellular-primary:addresses"))
        assertFalse(routes.update("cellular-primary:addresses"))
        assertTrue(routes.update(null))
        assertFalse(routes.update(null))
        assertTrue(routes.update("wifi-new:addresses"))
        val initial = PhysicalNetworkTracker(null)
        assertFalse(initial.update("wifi-first:addresses"))
    }
    @Test fun freshSuccessRanksBeforeFastHistoricalValueAndWrongCollectionCannotResolve() {
        val profiles = (1L..3L).map { ServerProfile(id=it, name="Synthetic", collectionId=if (it==1L) "collection-a" else "collection-b") }
        val results = mapOf(1L to PingResult.Success(1, verified=true, currentNetwork=false),
            2L to PingResult.Success(1500, verified=true), 3L to PingResult.Success(700, verified=true))
        assertEquals(listOf(3L,2L,1L), ProfileRanking.sort(profiles,results).map { it.id })
        assertTrue(ConnectionChoice.resolve(1L, profiles.filter { it.collectionId == "collection-b" }) is ConnectionChoice.Target.Missing)
        assertEquals(profiles[2], ConnectionChoice.initialPath(profiles.drop(1), results))
        assertFalse(ProbeCache.fresh(PingResult.Success(1,true,measuredAt=1), now=600001))
    }
    @Test fun groupTestContinuesAfterSuccessAndIndividualFailure() = kotlinx.coroutines.runBlocking {
        val profiles = (1L..6L).map { ServerProfile(id=it,name="Synthetic external",collectionId="external-collection") }
        val seen = mutableListOf<Long>()
        assertTrue(CollectionPhase.run(profiles,false,5000, { _,_ -> }, { p,_ -> seen += p.id }) {
            if (it.id==2L) PingResult.Error("Synthetic failure") else PingResult.Success(100,true)
        })
        assertEquals(profiles.map { it.id },seen)
        val detailed=ProbePolicy.testAllBudgetMs(profiles,true)
        assertTrue(detailed >= profiles.sumOf { ProbePolicy.forProfile(it).let { p -> p.setupMs + p.httpsMs*3 + 25000L } })
    }
}
