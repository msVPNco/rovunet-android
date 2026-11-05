package app.slipnet.util
object DeviceIdUtil {fun getScrambledDeviceId(context:android.content.Context)="test"}
object ConnectionDiagnostics {enum class Event {CONNECT_REQUEST,RECONNECT_REQUEST,DISCONNECT_REQUEST,VPN_ESTABLISHED,VPN_DISCONNECTED,VPN_ERROR, STARTUP_TIMEOUT, SERVICE_STOP_TIMEOUT}; fun record(event:Event)=Unit}
