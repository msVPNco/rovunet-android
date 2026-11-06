import app.slipnet.data.export.*
import app.slipnet.domain.model.*
import app.slipnet.core.LockHash
import app.slipnet.core.UpdateManifest
import org.json.JSONObject
import org.json.JSONArray

fun main() {
 val importer=ConfigImporter(); val exporter=ConfigExporter()
 for (secret in listOf("|", "p|a:ss@/#? +", "пароль|密碼|رمز")) {
  val profile=ServerProfile(name="test",domain="example.invalid",tunnelType=TunnelType.SSH,sshUsername="a|b",sshPassword=secret,socksUsername="s|u",socksPassword=secret)
  val result=importer.parseAndImport(exporter.exportSingleProfile(profile)) as ImportResult.Success
  val copy=result.profiles.single()
  check(copy.sshUsername==profile.sshUsername && copy.sshPassword==secret && copy.socksUsername==profile.socksUsername && copy.socksPassword==secret)
 }
 val hidden=ServerProfile(name="hidden",domain="example.invalid",tunnelType=TunnelType.SSH,sshUsername="u",sshPassword="p",resolversHidden=true,resolvers=listOf(DnsResolver("9.9.9.9")))
 val copy=(importer.parseAndImport(exporter.exportSingleProfile(hidden)) as ImportResult.Success).profiles.single()
 check(copy.resolversHidden && copy.resolvers==hidden.resolvers)
 val hash=LockHash.create("secret")
 check(LockHash.verify("secret",hash) && !LockHash.verify("wrong",hash))
 for (bad in listOf("", "xx:yy", "0".repeat(32)+":"+"z".repeat(64), ":", hash+"00", "f".repeat(100000))) check(!LockHash.verify("x",bad))
 val p=ServerProfile(name="ssh",domain="example.invalid",tunnelType=TunnelType.SSH,sshUsername="u",sshPassword="p")
 check(ChainValidation.validate(listOf(p,p.copy(expirationDate=1)))!=null)
 check(ProbePolicy.forProfile(p).httpsMs < ProbePolicy.forProfile(p.copy(tunnelType=TunnelType.DNSTT)).httpsMs)
 check(ProbePolicy.forProfile(p.copy(tunnelType=TunnelType.NAIVE)).setupMs > ProbePolicy.forProfile(p).setupMs)
 val original=p.copy(id=42)
 check(ProbeCache.unchanged(listOf(original),listOf(original.copy(name="renamed",isPinned=true,lastConnectedAt=123)))==setOf(42L))
 for (changed in listOf(original.copy(sshPassword="changed"),original.copy(domain="other.invalid"),original.copy(sshTlsSni="other.invalid"))) check(ProbeCache.unchanged(listOf(original),listOf(changed)).isEmpty())
 check(ProbeCache.unchanged(listOf(original),emptyList()).isEmpty())
 val prefix="https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha99/"
 val digest="a".repeat(64)
 val assets=JSONArray().put(JSONObject().put("name","Rovunet-full.apk").put("size",100).put("digest","sha256:$digest").put("browser_download_url",prefix+"Rovunet-full.apk"))
 val body=JSONObject().put("tag_name","v0.3.0-alpha99").put("assets",assets).toString()
 check(UpdateManifest.parse(body,"0.3.0-alpha08",false)?.digest==digest)
 check(UpdateManifest.parse(body,"0.3.0-alpha99",false)==null)
 check(!UpdateManifest.trustedAsset("https://github.com.evil.invalid/msVPNco/rovunet-android/releases/download/x/a.apk"))
 check(!UpdateManifest.trustedAsset(prefix+"../evil.apk"))
 check(UpdateManifest.checksum("$digest  Rovunet-full.apk",prefix+"Rovunet-full.apk")==digest)
 check(runCatching { UpdateManifest.parse(body.replace("Rovunet-full.apk","other.apk"),"0.3.0-alpha08",false) }.isFailure)
 val ipv6 = DnsResolver("2001:db8::53", 5353, true)
 val sshProfile = ServerProfile(name = "regression", domain = "ssh.invalid", tunnelType = TunnelType.SSH,
     sshUsername = "qa", sshPassword = "sample", resolvers = listOf(ipv6))
 fun native(p: ServerProfile) = ConfigImporter().parseAndImport(ConfigExporter().exportSingleProfile(p))
 check((native(sshProfile) as ImportResult.Success).profiles.single().resolvers.single() == ipv6)
 check(native(sshProfile.copy(sshPort = -1)) is ImportResult.Error)
 check(native(sshProfile.copy(dnsPayloadSize = Int.MAX_VALUE)) is ImportResult.Error)
 val releases = JSONArray("""[{"tag_name":"v0.3.0-alpha13","draft":true},{"tag_name":"cli-v10"},{"tag_name":"v0.3.0-alpha11"},{"tag_name":"v0.3.0-alpha12","prerelease":true}]""")
 check(UpdateManifest.newestPublished(releases)?.getString("tag_name") == "v0.3.0-alpha12")
 println("PASS lossless v33 secrets, hidden resolver policy, malformed hashes, expired chain and update asset checks")
}
