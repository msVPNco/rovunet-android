import app.slipnet.service.VpnConnectionManager
import app.slipnet.domain.model.*
import app.slipnet.domain.repository.*
import app.slipnet.data.repository.VpnRepositoryImpl
import app.slipnet.data.local.datastore.PreferencesDataStore
import kotlinx.coroutines.*

fun main() = runBlocking {
 val context=android.content.Context(); val repo=VpnRepositoryImpl()
 val manager=VpnConnectionManager(context,repo,ProfileRepository(),ChainRepository(),PreferencesDataStore())
 val profile=ServerProfile(2)
 val first=manager.connect(profile)
 repo.emit(first,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 manager.disconnect(keepServiceAlive=true,expectedSessionId=first)
 manager.onVpnDisconnected(first)
 val second=manager.connect(profile)
 check(second>first && manager.connectionState.value is ConnectionState.Connecting)
 manager.onVpnDisconnected(first)
 manager.onVpnError("old setup error",first)
 repo.emit(first,ConnectionState.Disconnected)
 check(manager.connectionState.value is ConnectionState.Connecting) { "Stale teardown overwrote a newer attempt" }
 repo.emit(second,ConnectionState.Connected(profile))
 manager.onServiceDestroyed(first)
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
 manager.onServiceDestroyed(second)
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
 manager.onServiceDestroyed(third)
 check(context.started.count { it.action=="CONNECT" }==count) { "Canceled queued request started later" }
 val fifth=manager.connect(profile)
 check(fifth>fourth && context.started.count { it.action=="CONNECT" }==count+1)
 repo.emit(fifth,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 println("PASS real manager: stale disconnect/error/onDestroy isolation, scoped stop, delayed-stop delivery, queued connect barrier, queued cancellation and stale teardown isolation")
}
