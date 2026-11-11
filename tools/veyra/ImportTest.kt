import app.slipnet.data.export.*
import app.slipnet.domain.model.*
import app.slipnet.util.BundleCrypto

fun main() {
 val importer = ConfigImporter()
 val exporter = ConfigExporter()
 val link = "vless://00000000-0000-4000-8000-000000000001@example.org:443?type=ws&security=tls&fragment=#Test"
 fun success(input:String): List<ServerProfile> {
  val result = importer.parseAndImport(input)
  check(result is ImportResult.Success) { result.toString() }
  return result.profiles
 }
 check(success(link).single().vlessSecurity == "tls")
 check(!success(link.replace("&fragment=", "")).single().sniFragmentEnabled)
 val invalidNative = exporter.exportSingleProfile(ServerProfile(name="Unsupported",tunnelType=TunnelType.VLESS,vlessSecurity="reality"))
 check(importer.parseAndImport(invalidNative) is ImportResult.Error)
 check(success(link.replace("vless://", "VlEsS://")).size == 1)
 for (bad in listOf(link.replace("security=tls", "security=reality"), link.replace(":443", ":invalid"), link.replace("type=ws", "type=xhttp"))) {
   if (bad != link) check(importer.parseAndImport(bad) is ImportResult.Error)
 }
 val original = ServerProfile(name="DNS profile",domain="dns.example.org",tunnelType=TunnelType.SLIPSTREAM,resolvers=listOf(DnsResolver("1.1.1.1")))
 val encoded = exporter.exportSingleProfile(original)
 check(encoded.startsWith("rovunet://"))
 check(success(encoded).single().domain == original.domain)
 check(success(encoded.replace("rovunet://", "slipnet://")).single().tunnelType == original.tunnelType)
 val mixed = success("$link\n$encoded\nssh://user:p%40ss@example.org:2222\nsocks5://example.org:1080\nnaive+https://u:p@example.org")
 check(mixed.map { it.tunnelType } == listOf(TunnelType.VLESS,TunnelType.SLIPSTREAM,TunnelType.SSH,TunnelType.SOCKS5,TunnelType.NAIVE))
 check(mixed[2].sshPassword == "p@ss" && mixed[2].sshPort == 2222)
 check(importer.parseAndImport("x".repeat(2*1024*1024+1)) is ImportResult.Error)
 check(importer.parseAndImport((1..1001).joinToString("\n") { link }) is ImportResult.Error)
 val encrypted = "veyra-bundle-enc://" + java.util.Base64.getEncoder().encodeToString(BundleCrypto.encrypt("$encoded\n$link", "test-password"))
 check(importer.parseAndImport(encrypted) is ImportResult.NeedsPassword)
 check((importer.parseAndImport(encrypted,bundlePassword="test-password") as ImportResult.Success).profiles.size == 2)
 check(importer.parseAndImport(encrypted,bundlePassword="wrong") is ImportResult.Error)
 println("Real importer/exporter: aliases, mixed profiles, strict VLESS, limits and encrypted bundle round-trip passed")
}
