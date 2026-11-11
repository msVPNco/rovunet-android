import app.slipnet.service.VpnConnectionManager
import app.slipnet.domain.model.*
import app.slipnet.domain.repository.*
import app.slipnet.data.repository.VpnRepositoryImpl
import app.slipnet.data.local.datastore.PreferencesDataStore
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.first

fun main() = runBlocking {
 val context=android.content.Context(); val repo=VpnRepositoryImpl()
 val manager=VpnConnectionManager(context,repo,ProfileRepository(),ChainRepository(),PreferencesDataStore())
 val profile=ServerProfile(2)
 val memoryProfiles=ProfileRepository(); val memoryPrefs=PreferencesDataStore()
 val memoryManager=VpnConnectionManager(android.content.Context(),VpnRepositoryImpl(),memoryProfiles,ChainRepository(),memoryPrefs)
 memoryPrefs.setLastConnectedProfileId(99)
 val temporary=memoryManager.connect(profile,testMode=true)
 memoryManager.onVpnEstablished(temporary)
 check(memoryProfiles.activated.isEmpty() && memoryProfiles.connected.isEmpty())
 check(memoryPrefs.lastConnectedProfileId.value==99L) { "A probe changed the last user connection" }
 memoryManager.disconnect(expectedSessionId=temporary)
 memoryManager.onVpnDisconnected(temporary); memoryManager.onServiceReleased(temporary)
 val userConnection=memoryManager.connect(profile)
 memoryManager.onVpnEstablished(userConnection)
 check(memoryProfiles.activated==listOf(2L) && memoryProfiles.connected==listOf(2L))
 check(memoryPrefs.lastConnectedProfileId.value==2L)
 memoryManager.disconnect(expectedSessionId=userConnection)
 memoryManager.onVpnDisconnected(userConnection); memoryManager.onServiceReleased(userConnection)
 println("PASS probes preserve active/last-connected path; real user connections still persist them")
 check(manager.connect(profile.copy(needsCredentials=true))==0L)
 check(context.started.isEmpty() && manager.connectionState.value is ConnectionState.Error)
 val first=manager.connect(profile)
 check(context.started.last().extras["rovunet_allow_trust_prompt"] == true)
 repo.emit(first,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 // An explicit on-screen test can ask for identity approval while automatic tests stay noninteractive.
 val identityContext=android.content.Context(); val identityRepo=VpnRepositoryImpl()
 val identityManager=VpnConnectionManager(identityContext,identityRepo,ProfileRepository(),ChainRepository(),PreferencesDataStore())
 val automaticIdentity=identityManager.connect(profile,testMode=true)
 check(identityContext.started.last().extras["rovunet_test_mode"] == true)
 check(identityContext.started.last().extras["rovunet_allow_trust_prompt"] == false)
 identityManager.onServiceReleased(automaticIdentity)
 identityManager.connect(profile,testMode=true,allowTrustPrompt=true)
 check(identityContext.started.last().extras["rovunet_test_mode"] == true)
 check(identityContext.started.last().extras["rovunet_allow_trust_prompt"] == true)
 manager.disconnect(keepServiceAlive=true,expectedSessionId=first)
 manager.onVpnDisconnected(first)
 val second=manager.connect(profile)
 check(second>first && manager.connectionState.value is ConnectionState.Connecting)
 manager.onVpnDisconnected(first)
 manager.onVpnError("old setup error",first)
 repo.emit(first,ConnectionState.Disconnected)
 check(manager.connectionState.value is ConnectionState.Connecting) { "Stale teardown overwrote a newer attempt" }
 repo.emit(second,ConnectionState.Connected(profile))
 manager.onServiceReleased(first)
 check(manager.connectionState.value is ConnectionState.Connected) { "Old onDestroy erased an active connection" }
 manager.disconnect(expectedSessionId=first)
 check(manager.connectionState.value is ConnectionState.Connected)
 manager.disconnect(expectedSessionId=second)
 check(manager.connect(profile)==0L)
 manager.onVpnDisconnected(second)
 val starts=context.started.count { it.action=="CONNECT" }
 val third=manager.connect(profile)
 check(context.started.count { it.action=="CONNECT" }==starts) { "New service started before previous destruction" }
 // The old service must finish its stop even AFTER a newer request is queued.
 check(!manager.ownsSession(second))
 check(ServiceCommandPolicy.acceptsStop(second,second)) { "Queued connect incorrectly invalidated the delivered stop" }
 manager.onVpnDisconnected(second)
 check(ServiceCommandPolicy.mayStopService(second,second))
 manager.onServiceReleased(second)
 check(context.started.count { it.action=="CONNECT" }==starts+1)
 check(manager.connectionState.value is ConnectionState.Connecting)
 repo.emit(second,ConnectionState.Error("late"))
 check(manager.connectionState.value is ConnectionState.Connecting)
 // Once CONNECT for the new attempt is delivered, an old stop cannot touch it.
 check(!ServiceCommandPolicy.acceptsStop(second,third))
 check(!ServiceCommandPolicy.mayStopService(second,third))
 check(ServiceCommandPolicy.mayStopService(0,0))
 repo.emit(third,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 manager.disconnect(expectedSessionId=third)
 check(manager.connect(profile)==0L) { "Connect accepted before teardown acknowledgement" }
 manager.onVpnDisconnected(third)
 val count=context.started.count { it.action=="CONNECT" }
 val fourth=manager.connect(profile)
 check(context.started.count { it.action=="CONNECT" }==count)
 manager.disconnect(expectedSessionId=fourth)
 check(manager.connectionState.value is ConnectionState.Disconnected)
 check(context.started.none { it.action=="DISCONNECT" && it.extras["session"]==fourth }) { "Undelivered request sent a stop to another instance" }
 manager.onServiceReleased(third)
 check(context.started.count { it.action=="CONNECT" }==count) { "Canceled queued request started later" }
 val fifth=manager.connect(profile)
 check(fifth>fourth && context.started.count { it.action=="CONNECT" }==count+1)
 repo.emit(fifth,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 // The service's protocol-ready callback itself completes the UI state.
 val callbackContext=android.content.Context(); val callbackRepo=VpnRepositoryImpl()
 val callbackManager=VpnConnectionManager(callbackContext,callbackRepo,ProfileRepository(),ChainRepository(),PreferencesDataStore())
 val callbackId=callbackManager.connect(profile)
 callbackManager.onVpnEstablished(callbackId)
 check(callbackManager.connectionState.value is ConnectionState.Connected)
 // Reproduce an old service whose destruction never arrives. Queue wait must
 // terminate with Error and a late destruction must not dispatch the timed-out start.
 callbackManager.disconnect(expectedSessionId=callbackId)
 callbackManager.onVpnDisconnected(callbackId)
 val beforeTimeout=callbackContext.started.count { it.action=="CONNECT" }
 val timedOut=callbackManager.connect(profile)
 withTimeout(12_000) { callbackManager.connectionState.first { it is ConnectionState.Error } }
 check(callbackContext.started.count { it.action=="CONNECT" }==beforeTimeout)
 check(!callbackManager.acceptsStart(timedOut))
 callbackManager.onServiceReleased(callbackId)
 check(callbackContext.started.count { it.action=="CONNECT" }==beforeTimeout)
 callbackRepo.emit(timedOut,ConnectionState.Connected(profile))
 callbackManager.onVpnEstablished(timedOut)
 check(callbackManager.connectionState.value is ConnectionState.Error)
 val recovered=callbackManager.connect(profile)
 callbackManager.onVpnEstablished(recovered)
 check(callbackManager.connectionState.value is ConnectionState.Connected)
 // A repeated stop announcement after destruction must not create a new,
 // unfulfillable barrier for a service that no longer exists.
 callbackManager.expectServiceStop(callbackId)
 callbackManager.disconnect(keepServiceAlive=true,expectedSessionId=recovered)
 callbackManager.onVpnDisconnected(recovered)
 val retryCount=callbackContext.started.count { it.action=="CONNECT" }
 val afterLateStop=callbackManager.connect(profile)
 check(afterLateStop>recovered && callbackContext.started.count { it.action=="CONNECT" }==retryCount+1)
 callbackManager.onVpnEstablished(afterLateStop)
 check(callbackManager.connectionState.value is ConnectionState.Connected)
 // A failed probe asks to disconnect after the service has already begun
 // retiring. Android may keep the instance bound and never call onDestroy here.
 val staleContext = android.content.Context()
 val stale = VpnConnectionManager(staleContext,VpnRepositoryImpl(),ProfileRepository(),ChainRepository(),PreferencesDataStore())
 val failed = stale.connect(profile, testMode = true)
 stale.onVpnError("Server identity approval required", failed)
 stale.expectServiceStop(failed)
 stale.disconnect(expectedSessionId = failed)
 check(staleContext.started.none { it.action == "DISCONNECT" }) { "Extra STOP can resurrect a retiring service" }
 val next = stale.connect(profile)
 check(staleContext.started.count { it.action == "CONNECT" } == 1)
 stale.onServiceReleased(failed) // native cleanup complete, instance still bound
 check(staleContext.started.count { it.action == "CONNECT" } == 2)
 stale.onVpnEstablished(next)
 stale.finishProbe(failed)
 stale.onServiceReleased(failed) // actual later destruction must be harmless
 check(stale.connectionState.value is ConnectionState.Connected)
 stale.disconnect(expectedSessionId = next)
 stale.disconnect(expectedSessionId = next)
 check(staleContext.started.count { it.action == "DISCONNECT" } == 1)
 stale.onServiceReleased(next)
 stale.awaitSessionDisconnected(next)
 check(stale.connectionState.value.isDisconnected)
 val review = stale.connect(profile, testMode = true)
 stale.onVpnError("reject HostKey", review)
 stale.finishProbe(review)
 check(stale.connectionState.value is ConnectionState.Error)
 stale.onServiceReleased(review)
 check(stale.connectionState.value is ConnectionState.Error)
 stale.finishProbe(review)
 check(stale.connectionState.value.isDisconnected)
 println("PASS failed probe -> release with no onDestroy -> manual connect; duplicate STOP suppressed")
 println("PASS real manager: protocol readiness updates UI, stuck cleanup becomes Error, timed-out starts never run later and retry recovers")
 println("PASS real manager: stale disconnect/error/onDestroy isolation, scoped stop, delayed-stop delivery, queued connect barrier, queued cancellation and stale teardown isolation")
}
