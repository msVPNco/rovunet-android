import app.slipnet.domain.model.*
import kotlinx.coroutines.*
fun main() = runBlocking {
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
 println("PASS bounded collection phase, first-success stop, timeout/external-cancel cleanup and TCP-before-UDP default")
}
