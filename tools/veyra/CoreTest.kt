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
    val invalid = listOf(links[0].replace("sid=ab", "sid=a"),links[0].substringBefore('#')+"&insecure=1",links[1].substringBefore('#')+"&type=raw",links[2].substringBefore('#')+"&insecure=1",links[3]+"\nPostUp = echo nope",links[3]+"\n[Peer]\nPublicKey = $key")
    invalid.forEach { check(runCatching { CoreLink.parse(it) }.isFailure) }
    println("Core parser: four protocols, native persistence and invalid-option checks passed")
}
