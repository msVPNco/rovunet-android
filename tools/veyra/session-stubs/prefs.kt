package app.slipnet.data.local.datastore
import kotlinx.coroutines.flow.flowOf
class PreferencesDataStore {
 val autoConnectOnBoot=flowOf(false)
 val lastConnectedProfileId=kotlinx.coroutines.flow.MutableStateFlow<Long?>(null)
 suspend fun setLastConnectedProfileId(id: Long) { lastConnectedProfileId.value=id }
}
