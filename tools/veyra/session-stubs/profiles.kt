package app.slipnet.domain.repository
import kotlinx.coroutines.flow.flowOf
import app.slipnet.domain.model.*
class ChainRepository
class ProfileRepository { suspend fun setActiveProfile(id:Long)=Unit; suspend fun updateLastConnectedAt(id:Long)=Unit; suspend fun getProfileById(id:Long):ServerProfile?=null; fun getActiveProfile()=flowOf<ServerProfile?>(null) }
