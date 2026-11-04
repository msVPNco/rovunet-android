package app.slipnet.domain.usecase

import app.slipnet.domain.repository.ProfileRepository
import app.slipnet.domain.repository.VpnRepository
import javax.inject.Inject
import kotlinx.coroutines.flow.first

class DeleteProfileUseCase @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val vpnRepository: VpnRepository,
    private val chainRepository: app.slipnet.domain.repository.ChainRepository
) {
    suspend operator fun invoke(id: Long): Result<Unit> {
        // Check if this profile is currently connected
        val connectedProfile = vpnRepository.getConnectedProfile()
        if (connectedProfile?.id == id) {
            return Result.failure(IllegalStateException("Cannot delete profile while connected"))
        }

        val chains = chainRepository.getAllChains().first()
        if (chains.any { id in it.profileIds }) return Result.failure(IllegalStateException("Remove this profile from its chain first"))
        return try {
            profileRepository.deleteProfile(id)
            Result.success(Unit)
        } catch (e: kotlinx.coroutines.CancellationException) { throw e }
        catch (e: Exception) { Result.failure(e) }
    }
}
