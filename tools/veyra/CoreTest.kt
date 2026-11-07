import app.slipnet.core.CoreLink
import app.slipnet.data.export.*
import app.slipnet.domain.model.TunnelType
import java.io.File

fun main(args: Array<String>) {
    val key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
    val id = "00000000-0000-4000-8000-000000000001"
    val links = listOf(
        "vless://$id@example.org:443?security=reality&type=tcp&pbk=${key.trimEnd('=')}&sid=ab&flow=xtls-rprx-vision#REALITY",
        "vless://$id@example.org:443?security=tls&type=xhttp&path=%2Ftest&mode=packet-up#XHTTP",
        "hysteria2://p%40ss@example.org:443?sni=example.org&obfs=salamander&obfs-password=secret#HY2",
        "[Interface]\nPrivateKey = $key\nAddress = 10.66.0.2/32\nDNS = 1.1.1.1\n[Peer]\nPublicKey = $key\nEndpoint = example.org:51820\nAllowedIPs = 0.0.0.0/0, ::/0\nPersistentKeepalive = 25"
    )
    val importer = ConfigImporter(); val exporter = ConfigExporter()
    links.forEachIndexed { i, link ->
        check(ConfigSchemes.accepts(link))
        val parsed = CoreLink.parse(link)
        check(parsed.outbound.getString("protocol") in setOf("vless", "hysteria", "wireguard"))
        val result = importer.parseAndImport(link) as ImportResult.Success
        val profile = result.profiles.single()
        check(profile.tunnelType == TunnelType.XRAY && profile.coreLink == link)
        val native = exporter.exportSingleProfile(profile)
        check((importer.parseAndImport(native) as ImportResult.Success).profiles.single().coreLink == link)
        if (args.isNotEmpty()) File(args[0], "core-$i.json").writeText(CoreLink.runtime(link, 10880+i))
    }
    fun extraLink(key: String, value: String): String {
        val extra = java.net.URLEncoder.encode("""{"$key":$value}""", "UTF-8")
        return links[1].substringBefore('#') + "&extra=" + extra
    }
    check(CoreLink.parse(extraLink("scMaxEachPostBytes", "65536")).outbound.has("streamSettings"))
    for (keyName in listOf("downloadSettings", "DownloadSettings", "DOWNLOADSETTINGS", "extra", "unknownOption")) {
        val unsafe = extraLink(keyName, "{}")
        check(runCatching { CoreLink.runtime(unsafe, 10880) }.isFailure)
        check(importer.parseAndImport(unsafe) is ImportResult.Error)
    }
    val invalid = listOf(links[0].replace("sid=ab", "sid=a"),links[0].substringBefore('#')+"&insecure=1",links[1].substringBefore('#')+"&type=raw",links[2].substringBefore('#')+"&insecure=1",links[3]+"\nPostUp = echo nope",links[3]+"\n[Peer]\nPublicKey = $key")
    invalid.forEach { check(runCatching { CoreLink.parse(it) }.isFailure) }
    // Real panel output (rovunet core-install / core-plan), not hand-written
    // links: every protocol must import as one Xray profile and survive the
    // app's own native export unchanged.
    System.getenv("ROVUNET_CORE_FIXTURE")?.takeIf { it.isNotEmpty() }?.let { path ->
        val records = File(path).readText().split(Regex("\\n%%\\n")).map { it.trim() }.filter { it.isNotEmpty() }
        check(records.size == 4) { "panel core fixture: want 4 profiles, got ${records.size}" }
        val protocols = records.map { link ->
            check(ConfigSchemes.accepts(link)) { "app rejects panel core profile: ${link.lineSequence().first()}" }
            val parsed = CoreLink.parse(link)
            val profile = (importer.parseAndImport(link) as ImportResult.Success).profiles.single()
            check(profile.tunnelType == TunnelType.XRAY && profile.coreLink == link)
            val native = exporter.exportSingleProfile(profile)
            check((importer.parseAndImport(native) as ImportResult.Success).profiles.single().coreLink == link)
            CoreLink.runtime(link, 10900)
            parsed.outbound.getString("protocol")
        }
        check(protocols == listOf("vless", "vless", "hysteria", "wireguard")) { "panel core protocols: $protocols" }
        println("Core parser: real panel REALITY, XHTTP, Hysteria2 and WireGuard profiles imported")
    }
    // The same profiles as the panel's users configs prints them: native
    // "xray" rovunet:// profiles in one user's collection.
    System.getenv("ROVUNET_CORE_NATIVE_FIXTURE")?.takeIf { it.isNotEmpty() }?.let { path ->
        val lines = File(path).readLines().map { it.trim() }.filter { it.isNotEmpty() }
        check(lines.size == 4) { "panel native core fixture: want 4 profiles, got ${lines.size}" }
        val raw = File(System.getenv("ROVUNET_CORE_FIXTURE")).readText().split(Regex("\\n%%\\n")).map { it.trim() }.filter { it.isNotEmpty() }
        val profiles = lines.mapIndexed { i, line ->
            val p = (importer.parseAndImport(line) as ImportResult.Success).profiles.single()
            check(p.tunnelType == TunnelType.XRAY) { "native core profile ${i + 1} imported as ${p.tunnelType}" }
            check(p.coreLink == raw[i]) { "native core profile ${i + 1}: core link changed in transit" }
            check(p.name == "core${i + 1}") { "native core profile ${i + 1}: name ${p.name}" }
            CoreLink.runtime(p.coreLink, 10900)
            p
        }
        check(profiles.map { it.collectionId }.toSet().size == 1 && profiles[0].collectionId.isNotEmpty()) { "native core profiles are not in one collection" }
        check(profiles.all { it.collectionName.startsWith("alice") }) { "collection name: ${profiles.map { it.collectionName }}" }
        println("Core parser: panel's native xray profiles imported into the user's collection")
    }
    println("Core parser: four protocols, native persistence and invalid-option checks passed")
}
