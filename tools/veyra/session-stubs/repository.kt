package app.slipnet.data.repository
import kotlinx.coroutines.flow.MutableStateFlow
import app.slipnet.domain.model.*
class VpnRepositoryImpl {
 val connectionState=MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
 val sessionState=MutableStateFlow(SessionConnectionState(0,ConnectionState.Disconnected))
 val trafficStats=MutableStateFlow(TrafficStats())
 val dnsPoolScanState=MutableStateFlow(app.slipnet.tunnel.DnsPoolScanState())
 var session=0L
 fun emit(id:Long,state:ConnectionState) { session=id; connectionState.value=state; sessionState.value=SessionConnectionState(id,state) }
 fun updateConnectionState(state:ConnectionState) { emit(session,state) }
 fun refreshTrafficStats()=Unit
}
