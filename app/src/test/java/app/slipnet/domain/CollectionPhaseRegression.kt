package app.slipnet.domain

import app.slipnet.domain.model.*
import kotlinx.coroutines.*
fun collectionPhaseRegression() = runBlocking {
 val profiles=(1L..4L).map { ServerProfile(id=it,name="test") }
 var calls=0
 check(CollectionPhase.run(profiles,true,1000,{_,_->},{_,_->}) {calls++;PingResult.Success(1,true)})
 check(calls==1)
 var cleaned=false
 check(!CollectionPhase.run(profiles,true,30,{_,_->},{_,_->}) {
  try { awaitCancellation() } finally {cleaned=true}
 })
 check(cleaned)
 val started=CompletableDeferred<Unit>();cleaned=false
 val job=launch {CollectionPhase.run(profiles,false,90_000,{_,_->},{_,_->}) {try {started.complete(Unit);awaitCancellation()} finally {cleaned=true}}}
 started.await();job.cancelAndJoin();check(cleaned)
 val tcp=profiles[0].copy(tunnelType=TunnelType.XRAY,coreLink="vless://a@host:443?type=xhttp")
 val udp=profiles[1].copy(tunnelType=TunnelType.XRAY,coreLink="hysteria2://a@host:443")
 check(ProfileCollections.ordered(listOf(udp,tcp),emptyMap()).first().id==tcp.id)
 // A human identity decision is bounded separately and does not consume the network budget.
 val pausedStarted = System.nanoTime()
 var deciding = true
 var spentPaused = 0L
 val pausedNanos = { if (deciding) System.nanoTime() - pausedStarted else spentPaused }
 val approval = launch { delay(160); spentPaused = System.nanoTime() - pausedStarted; deciding = false }
 check(CollectionPhase.run(profiles.take(1), false, 100, {_,_->}, {_,_->}, pausedNanos) {
  while (deciding) delay(5)
  delay(20)
  PingResult.Success(1,true)
 })
 approval.join()
 // Cancellation still stops a test while a prompt is open; it cannot strand cleanup.
 val pausingAt = System.nanoTime()
 val waiting = CompletableDeferred<Unit>()
 cleaned=false
 val cancelled=launch { CollectionPhase.run(profiles,false,30,{_,_->},{_,_->}, {System.nanoTime()-pausingAt}) {
  try { waiting.complete(Unit); awaitCancellation() } finally {cleaned=true}
 } }
 waiting.await();delay(70);check(cancelled.isActive);cancelled.cancelAndJoin();check(cleaned)
 println("PASS bounded collection phase, approval-paused network budget, timeout/external-cancel cleanup and TCP-before-UDP default")
}
