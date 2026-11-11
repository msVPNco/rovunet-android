package app.slipnet.domain.repository
import kotlinx.coroutines.flow.flowOf
import app.slipnet.domain.model.*
class ChainRepository
class ProfileRepository {
 val activated=mutableListOf<Long>(); val connected=mutableListOf<Long>()
 suspend fun setActiveProfile(id:Long) { activated.add(id) }
 suspend fun updateLastConnectedAt(id:Long) { connected.add(id) }
 suspend fun getProfileById(id:Long):ServerProfile?=null
 fun getActiveProfile()=flowOf<ServerProfile?>(null)
}
