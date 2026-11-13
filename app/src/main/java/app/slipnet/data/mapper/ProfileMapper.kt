package app.slipnet.data.mapper

import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import app.slipnet.data.local.database.ProfileEntity
import app.slipnet.data.local.database.ProfileSecrets
import app.slipnet.data.local.database.ProfileSnapshots
import app.slipnet.domain.model.ProfileDeletionReview
import app.slipnet.domain.model.CongestionControl
import app.slipnet.domain.model.DnsResolver
import app.slipnet.domain.model.DnsTransport
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.ResolverMode
import app.slipnet.domain.model.SshAuthType
import app.slipnet.domain.model.TunnelType
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ProfileMapper @Inject constructor(
    private val gson: Gson
) {
    fun toDomain(stored: ProfileEntity): ServerProfile {
        val opened = ProfileSecrets.open(stored)
        val entity = opened.getOrElse { ProfileSecrets.withoutSecrets(stored) }
        val resolversType = object : TypeToken<List<DnsResolver>>() {}.type
        val resolvers: List<DnsResolver> = try {
            gson.fromJson(entity.resolversJson, resolversType) ?: emptyList()
        } catch (e: Exception) {
            emptyList()
        }
        val defaultResolvers: List<DnsResolver> = try {
            gson.fromJson(entity.defaultResolversJson, resolversType) ?: emptyList()
        } catch (e: Exception) {
            emptyList()
        }

        val profile = ServerProfile(
            id = entity.id,
            unreadableEnvelopeToken = if (opened.isFailure) ProfileSecrets.token(stored) else null,
            name = entity.name,
            domain = entity.domain,
            resolvers = resolvers,
            authoritativeMode = entity.authoritativeMode,
            keepAliveInterval = entity.keepAliveInterval,
            congestionControl = CongestionControl.fromValue(entity.congestionControl),
            gsoEnabled = entity.gsoEnabled,
            tcpListenPort = entity.tcpListenPort,
            tcpListenHost = entity.tcpListenHost,
            socksUsername = entity.socksUsername.ifBlank { null },
            socksPassword = entity.socksPassword.ifBlank { null },
            isActive = entity.isActive,
            createdAt = entity.createdAt,
            updatedAt = entity.updatedAt,
            tunnelType = TunnelType.fromValue(entity.tunnelType),
            dnsttPublicKey = entity.dnsttPublicKey,
            slipstreamCertificate = entity.slipstreamCertificate,
            sshUsername = entity.sshUsername,
            sshPassword = entity.sshPassword,
            sshPort = entity.sshPort,
            sshHost = entity.sshHost,
            dohUrl = entity.dohUrl,
            lastConnectedAt = entity.lastConnectedAt,
            dnsTransport = DnsTransport.fromValue(entity.dnsTransport),
            sshAuthType = SshAuthType.fromValue(entity.sshAuthType),
            sshPrivateKey = entity.sshPrivateKey,
            sshKeyPassphrase = entity.sshKeyPassphrase,
            torBridgeLines = entity.torBridgeLines,
            sortOrder = entity.sortOrder,
            dnsttAuthoritative = entity.dnsttAuthoritative,
            naivePort = entity.naivePort,
            naiveUsername = entity.naiveUsername,
            naivePassword = entity.naivePassword,
            isLocked = entity.isLocked,
            lockPasswordHash = entity.lockPasswordHash,
            expirationDate = entity.expirationDate,
            allowSharing = entity.allowSharing,
            boundDeviceId = entity.boundDeviceId,
            noizdnsStealth = entity.noizdnsStealth,
            dnsPayloadSize = entity.dnsPayloadSize,
            resolversHidden = entity.resolversHidden,
            defaultResolvers = defaultResolvers,
            socks5ServerPort = entity.socks5ServerPort,
            vaydnsDnsttCompat = entity.vaydnsDnsttCompat,
            vaydnsRecordType = entity.vaydnsRecordType,
            vaydnsMaxQnameLen = entity.vaydnsMaxQnameLen,
            vaydnsRps = entity.vaydnsRps,
            vaydnsIdleTimeout = entity.vaydnsIdleTimeout,
            vaydnsKeepalive = entity.vaydnsKeepalive,
            vaydnsUdpTimeout = entity.vaydnsUdpTimeout,
            vaydnsMaxNumLabels = entity.vaydnsMaxNumLabels,
            vaydnsClientIdSize = entity.vaydnsClientIdSize,
            isPinned = entity.isPinned,
            sshTlsEnabled = entity.sshTlsEnabled,
            sshTlsSni = entity.sshTlsSni,
            sshHttpProxyHost = entity.sshHttpProxyHost,
            sshHttpProxyPort = entity.sshHttpProxyPort,
            sshHttpProxyCustomHost = entity.sshHttpProxyCustomHost,
            sshWsEnabled = entity.sshWsEnabled,
            sshWsPath = entity.sshWsPath,
            sshWsUseTls = entity.sshWsUseTls,
            sshWsCustomHost = entity.sshWsCustomHost,
            sshPayload = entity.sshPayload,
            resolverMode = ResolverMode.fromValue(entity.resolverMode),
            rrSpreadCount = entity.rrSpreadCount,
            coreLink = entity.coreLink,
            collectionId = entity.collectionId,
            collectionName = entity.collectionName,
            vlessUuid = entity.vlessUuid,
            vlessSecurity = entity.vlessSecurity,
            vlessTransport = entity.vlessTransport,
            vlessWsPath = entity.vlessWsPath,
            cdnIp = entity.cdnIp,
            cdnPort = entity.cdnPort,
            sniFragmentEnabled = entity.sniFragmentEnabled,
            sniFragmentStrategy = entity.sniFragmentStrategy,
            sniFragmentDelayMs = entity.sniFragmentDelayMs,
            sniSpoofTtl = entity.sniSpoofTtl,
            fakeDecoyHost = entity.fakeDecoyHost,
            tcpMaxSeg = entity.tcpMaxSeg,
            vlessSni = entity.vlessSni,
            chPaddingEnabled = entity.chPaddingEnabled,
            wsHeaderObfuscation = entity.wsHeaderObfuscation,
            wsPaddingEnabled = entity.wsPaddingEnabled,
            dnsAutoTune = entity.dnsAutoTune,
            soarDiscovery = entity.soarDiscovery,
            soarCountry = entity.soarCountry,
            soarDiscoveryList = entity.soarDiscoveryList,
            soarDiscoveryCustom = entity.soarDiscoveryCustom,
            soarTcpMode = entity.soarTcpMode
        )
        // Freeze the same canonical snapshot as before; keep certificate validation and
        // concurrency protection, but do not serialize/hash two complete rows just to draw a card.
        val snapshot = toSnapshot(profile)
        return profile.copy(deletionReview = ProfileDeletionReview.deferred(stored) {
            ProfileSnapshots.digest(stored) to ProfileSnapshots.digest(snapshot)
        })
    }

    fun toEntity(profile: ServerProfile): ProfileEntity {
        require(!profile.needsCredentials) { "Saved credentials unavailable" }
        return ProfileSecrets.seal(toSnapshot(profile))
    }

    /** Plain in-memory comparison/write input. The DAO seals it before persistence. */
    internal fun toSnapshot(profile: ServerProfile): ProfileEntity {
        app.slipnet.domain.model.SlipstreamIdentity.certificate(profile.slipstreamCertificate)
        val resolversJson = gson.toJson(profile.resolvers)
        val defaultResolversJson = gson.toJson(profile.defaultResolvers)

        return ProfileEntity(
            id = profile.id,
            name = profile.name,
            domain = profile.domain,
            resolversJson = resolversJson,
            authoritativeMode = profile.authoritativeMode,
            keepAliveInterval = profile.keepAliveInterval,
            congestionControl = profile.congestionControl.value,
            gsoEnabled = profile.gsoEnabled,
            tcpListenPort = profile.tcpListenPort,
            tcpListenHost = profile.tcpListenHost,
            socksUsername = profile.socksUsername ?: "",
            socksPassword = profile.socksPassword ?: "",
            isActive = profile.isActive,
            createdAt = profile.createdAt,
            updatedAt = profile.updatedAt,
            tunnelType = profile.tunnelType.value,
            dnsttPublicKey = profile.dnsttPublicKey,
            slipstreamCertificate = profile.slipstreamCertificate,
            sshUsername = profile.sshUsername,
            sshPassword = profile.sshPassword,
            sshPort = profile.sshPort,
            sshHost = profile.sshHost,
            dohUrl = profile.dohUrl,
            lastConnectedAt = profile.lastConnectedAt,
            dnsTransport = profile.dnsTransport.value,
            sshAuthType = profile.sshAuthType.value,
            sshPrivateKey = profile.sshPrivateKey,
            sshKeyPassphrase = profile.sshKeyPassphrase,
            torBridgeLines = profile.torBridgeLines,
            sortOrder = profile.sortOrder,
            dnsttAuthoritative = profile.dnsttAuthoritative,
            naivePort = profile.naivePort,
            naiveUsername = profile.naiveUsername,
            naivePassword = profile.naivePassword,
            isLocked = profile.isLocked,
            lockPasswordHash = profile.lockPasswordHash,
            expirationDate = profile.expirationDate,
            allowSharing = profile.allowSharing,
            boundDeviceId = profile.boundDeviceId,
            noizdnsStealth = profile.noizdnsStealth,
            dnsPayloadSize = profile.dnsPayloadSize,
            resolversHidden = profile.resolversHidden,
            defaultResolversJson = defaultResolversJson,
            socks5ServerPort = profile.socks5ServerPort,
            vaydnsDnsttCompat = profile.vaydnsDnsttCompat,
            vaydnsRecordType = profile.vaydnsRecordType,
            vaydnsMaxQnameLen = profile.vaydnsMaxQnameLen,
            vaydnsRps = profile.vaydnsRps,
            vaydnsIdleTimeout = profile.vaydnsIdleTimeout,
            vaydnsKeepalive = profile.vaydnsKeepalive,
            vaydnsUdpTimeout = profile.vaydnsUdpTimeout,
            vaydnsMaxNumLabels = profile.vaydnsMaxNumLabels,
            vaydnsClientIdSize = profile.vaydnsClientIdSize,
            isPinned = profile.isPinned,
            sshTlsEnabled = profile.sshTlsEnabled,
            sshTlsSni = profile.sshTlsSni,
            sshHttpProxyHost = profile.sshHttpProxyHost,
            sshHttpProxyPort = profile.sshHttpProxyPort,
            sshHttpProxyCustomHost = profile.sshHttpProxyCustomHost,
            sshWsEnabled = profile.sshWsEnabled,
            sshWsPath = profile.sshWsPath,
            sshWsUseTls = profile.sshWsUseTls,
            sshWsCustomHost = profile.sshWsCustomHost,
            sshPayload = profile.sshPayload,
            resolverMode = profile.resolverMode.value,
            rrSpreadCount = profile.rrSpreadCount,
            coreLink = profile.coreLink,
            collectionId = profile.collectionId,
            collectionName = profile.collectionName,
            vlessUuid = profile.vlessUuid,
            vlessSecurity = profile.vlessSecurity,
            vlessTransport = profile.vlessTransport,
            vlessWsPath = profile.vlessWsPath,
            cdnIp = profile.cdnIp,
            cdnPort = profile.cdnPort,
            sniFragmentEnabled = profile.sniFragmentEnabled,
            sniFragmentStrategy = profile.sniFragmentStrategy,
            sniFragmentDelayMs = profile.sniFragmentDelayMs,
            sniSpoofTtl = profile.sniSpoofTtl,
            fakeDecoyHost = profile.fakeDecoyHost,
            tcpMaxSeg = profile.tcpMaxSeg,
            vlessSni = profile.vlessSni,
            chPaddingEnabled = profile.chPaddingEnabled,
            wsHeaderObfuscation = profile.wsHeaderObfuscation,
            wsPaddingEnabled = profile.wsPaddingEnabled,
            dnsAutoTune = profile.dnsAutoTune,
            soarDiscovery = profile.soarDiscovery,
            soarCountry = profile.soarCountry,
            soarDiscoveryList = profile.soarDiscoveryList,
            soarDiscoveryCustom = profile.soarDiscoveryCustom,
            soarTcpMode = profile.soarTcpMode
        )
    }

    fun toDomainList(entities: List<ProfileEntity>): List<ServerProfile> {
        return entities.map { toDomain(it) }
    }
}
