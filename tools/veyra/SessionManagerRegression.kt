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
 manager.onVpnDisconnected(second)
 val starts=context.started.count { it.action=="CONNECT" }
 val third=manager.connect(profile)
 check(context.started.count { it.action=="CONNECT" }==starts) { "New service started before previous destruction" }
 manager.onServiceDestroyed(second)
 check(context.started.count { it.action=="CONNECT" }==starts+1)
 check(manager.connectionState.value is ConnectionState.Connecting)
 repo.emit(second,ConnectionState.Error("late"))
 check(manager.connectionState.value is ConnectionState.Connecting)
 repo.emit(third,ConnectionState.Connected(profile))
 check(manager.connectionState.value is ConnectionState.Connected)
 println("PASS real manager: stale disconnect/error/onDestroy isolation, scoped stop and teardown barrier")
}
