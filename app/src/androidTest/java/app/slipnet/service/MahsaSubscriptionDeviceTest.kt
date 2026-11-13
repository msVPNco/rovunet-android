package app.slipnet.service

import androidx.test.platform.app.InstrumentationRegistry
import app.slipnet.data.export.ConfigExporter
import app.slipnet.data.export.ConfigImporter
import app.slipnet.data.local.DeviceSecrets
import app.slipnet.data.local.database.ChainEntity
import app.slipnet.data.mapper.ProfileMapper
import app.slipnet.data.subscription.*
import app.slipnet.di.DatabaseModule
import app.slipnet.domain.model.ServerProfile
import app.slipnet.domain.model.TunnelType
import com.google.gson.Gson
import dagger.hilt.android.EntryPointAccessors
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec

class MahsaSubscriptionDeviceTest {
    private fun link(port: Int) = "socks5://fixture:synthetic@192.0.2.18:$port#fixture-$port"
    private fun encrypted(vararg links: String): String {
        val json=JSONObject().put("mci",JSONArray().apply { links.forEach { put(JSONObject().put("config",it)) } })
            .put("mtn",JSONArray()).put("ads_url","https://example.invalid/never-download-this")
        val cipher=Cipher.getInstance("AES/CBC/PKCS5Padding")
        cipher.init(Cipher.ENCRYPT_MODE,SecretKeySpec("ca815ecfdb1f153a".toByteArray(Charsets.US_ASCII),"AES"),
            IvParameterSpec("lvcas56410c97lpb".toByteArray(Charsets.US_ASCII)))
        return android.util.Base64.encodeToString(cipher.doFinal(json.toString().toByteArray()),android.util.Base64.NO_WRAP)
    }
    @Test fun legacyMahsaRefreshIsDirectAtomicAndRemovesOnlyUnmodifiedRetiredFeedRows() = runBlocking {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val repo=EntryPointAccessors.fromApplication(context,SubscriptionWorkerEntryPoint::class.java).subscriptions()
        val db=DatabaseModule.provideDatabase(context)
        val mapper=ProfileMapper(Gson())
        val address=PublicProviders.Provider.MAHSA.address
        val old="https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa"
        val source=repo.prepareDocument("Mahsa fixture",address,null,false,(10001..10004).joinToString("\n") { link(it) },publicMode=true)
        val other=repo.prepareDocument("Unrelated fixture","https://example.invalid/private-${UUID.randomUUID()}",null,false,link(11001))
        var chainId:Long?=null
        var manualId:Long?=null
        try {
            repo.apply(source);repo.apply(other)
            val original=db.profileDao().getAllProfiles().first().filter { it.collectionId==source.source.id }.associateBy { it.name }
            val kept=original.getValue("fixture-10001")
            val removed=original.getValue("fixture-10002")
            val edited=original.getValue("fixture-10003")
            val chained=original.getValue("fixture-10004")
            db.profileDao().updateProfile(mapper.toEntity(mapper.toDomain(edited).copy(name="Local override")))
            val now=System.currentTimeMillis()
            chainId=db.chainDao().insertChecked(ChainEntity(name="Synthetic chain",profileIdsJson="[${chained.id}]",createdAt=now,updatedAt=now))
            manualId=db.profileDao().insertProfile(mapper.toEntity(ServerProfile(name="Manual fixture",domain="192.0.2.18",tunnelType=TunnelType.SSH,sshPassword="synthetic")))
            val manual=requireNotNull(db.profileDao().getProfileById(requireNotNull(manualId)))
            val unrelated=db.profileDao().getAllProfiles().first().filter { it.collectionId==other.source.id }
            val saved=requireNotNull(db.subscriptionDao().get(source.source.id))
            db.subscriptionDao().put(saved.copy(sourceEnvelope=DeviceSecrets.seal(old,"subscription:${saved.id}")))
            val legacy=requireNotNull(db.subscriptionDao().get(saved.id))
            assertEquals(saved.id,repo.findSource(address)?.id)
            assertEquals("",repo.host(legacy))
            val calls=mutableListOf<String>()
            val fresh=repo.prepareUsing(legacy.name,old,legacy.id,false) { target,_ ->
                calls+=target;SubscriptionRepository.Download(200,null,encrypted(link(10001),link(10005),link(10001)))
            }
            assertEquals(listOf(address),calls)
            repo.apply(fresh)
            val updated=requireNotNull(db.subscriptionDao().get(saved.id))
            assertEquals(address,repo.sourceUrl(updated))
            assertEquals(saved.id,updated.id)
            val members=db.profileDao().getAllProfiles().first().filter { it.collectionId==saved.id }
            assertEquals(setOf("fixture-10001","fixture-10005"),members.map { it.name }.toSet())
            assertEquals(kept.id,members.single { it.name=="fixture-10001" }.id)
            assertNull(db.profileDao().getProfileById(removed.id))
            assertEquals("Local override",db.profileDao().getProfileById(edited.id)?.name)
            assertEquals(saved.id+":retained",db.profileDao().getProfileById(edited.id)?.collectionId)
            assertNotNull(db.profileDao().getProfileById(chained.id))
            assertNotNull(db.chainDao().getChainById(requireNotNull(chainId)))
            assertEquals(manual,db.profileDao().getProfileById(manual.id))
            assertEquals(unrelated,db.profileDao().getAllProfiles().first().filter { it.collectionId==other.source.id })
            val rows=db.profileDao().getAllProfiles().first()
            for (bad in listOf("broken",encrypted(),encrypted("unknown://synthetic"),encrypted(link(10001),"unknown://synthetic"))) {
                assertTrue(runCatching {
                    val pending = repo.prepareUsing(updated.name,address,updated.id,false) { _,_ -> SubscriptionRepository.Download(200,null,bad) }
                    repo.apply(pending)
                }.isFailure)
                assertEquals(updated,db.subscriptionDao().get(updated.id));assertEquals(rows,db.profileDao().getAllProfiles().first())
            }
            val partial=repo.prepareUsing(updated.name,address,updated.id,false) { _,_ ->
                SubscriptionRepository.Download(200,null,encrypted(link(10001),"unknown://synthetic"))
            }
            assertEquals(1,partial.unsupported)
            assertTrue(runCatching { repo.apply(partial) }.isFailure)
            assertEquals(rows,db.profileDao().getAllProfiles().first())
            for (cancel in listOf(false,true)) {
                assertTrue(runCatching { repo.prepareUsing(updated.name,old,updated.id,false) { _,_ ->
                    if(cancel) throw CancellationException() else error("DOWNLOAD_FAILED")
                } }.isFailure)
                assertEquals(updated,db.subscriptionDao().get(updated.id));assertEquals(rows,db.profileDao().getAllProfiles().first())
            }
            repo.apply(partial.copy(reviewedUnsupported=true))
            assertEquals(listOf("fixture-10001"),db.profileDao().getAllProfiles().first().filter { it.collectionId==saved.id }.map { it.name })
        } finally {
            chainId?.let { db.chainDao().deleteChain(it) }
            for(id in listOf(source.source.id,other.source.id)) {
                repo.detach(id)
                db.profileDao().getAllProfiles().first().filter { it.collectionId==id || it.collectionId==id+":retained" }.forEach { db.profileDao().deleteProfile(it.id) }
            }
            manualId?.let { db.profileDao().deleteProfile(it) }
            db.close()
        }
    }
}
