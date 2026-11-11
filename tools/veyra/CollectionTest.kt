import app.slipnet.domain.model.*
import app.slipnet.data.export.*

fun main() {
    val a = ServerProfile(id=1, name="Alice", domain="shared.cdn.example", collectionId="A", collectionName="سرور من — آلمان", tunnelType=TunnelType.SSH, sshUsername="alice", sshPassword="test-only")
    val b = a.copy(id=2, collectionId="B")
    val dns = a.copy(id=3, tunnelType=TunnelType.DNSTT, dnsttPublicKey="ab".repeat(32), resolvers=listOf(DnsResolver("192.0.2.53")))
    check(ProfileCollections.members(listOf(a,b,dns), a).map { it.id } == listOf(1L,3L))
    check(ProfileCollections.members(listOf(a,b), null).isEmpty())
    check(ProfileCollections.key(a.copy(collectionId="")) != ProfileCollections.key(b.copy(collectionId="")))
    check(ProfileCollections.ordered(listOf(dns,a), mapOf(3L to PingResult.Success(1,true))).last().id == 3L)
    val tor = a.copy(id=4, tunnelType=TunnelType.SNOWFLAKE)
    val core = a.copy(id=5, tunnelType=TunnelType.XRAY)
    check(ProfileCollections.ordered(listOf(tor,core,dns), emptyMap()).map { it.id } == listOf(5L,4L,3L))
    check(ProfileCollections.ordered(listOf(tor,core), mapOf(4L to PingResult.Success(1,true, measuredAt=0))).first().id == 5L)
    check(!ProfileCollections.isDns(a.copy(tunnelType=TunnelType.DOH)))
    check(!ProfileCollections.isDns(a.copy(tunnelType=TunnelType.SNOWFLAKE)))
    val exporter = ConfigExporter()
    val importer = ConfigImporter()
    val groupExport = importer.parseAndImport(exporter.exportCollection(listOf(a,b,dns),a)) as ImportResult.Success
    check(groupExport.profiles.size == 2 && groupExport.profiles.all { it.collectionId == "A" && it.collectionName == a.collectionName }) { "Group import: ${groupExport.profiles.size}; ${groupExport.warnings}" }
    check(runCatching { exporter.exportCollection(listOf(a,dns.copy(isLocked=true,allowSharing=false)),a) }.isFailure)
    // JNI encryption is unavailable here. Ensure a shareable locked member routes
    // into encryption instead of being exported as plaintext.
    check(runCatching { exporter.exportCollection(listOf(a,dns.copy(isLocked=true,allowSharing=true)),a) }.exceptionOrNull()?.message == "JNI locking unavailable in JVM tests")
    println("PASS selected-group export, metadata and sharing restrictions; JNI roundtrip needs device test")
    val encoded = exporter.exportSingleProfile(a)
    for (scheme in listOf("rovunet", "veyra", "slipnet")) {
        val parsed = importer.parseAndImport(encoded.replace("rovunet://", "$scheme://"))
        check(parsed is ImportResult.Success) { parsed.toString() }
        val result = parsed
        check(result.profiles.single().collectionId == "A")
        check(result.profiles.single().collectionName == a.collectionName)
        check(result.profiles.single().sshPassword == a.sshPassword)
    }
    for (suffix in listOf("", "-enc", "-bundle-enc")) {
        check(ConfigSchemes.accepts("rovunet$suffix://abc"))
        check(ConfigSchemes.canonical("rovunet$suffix://abc") == "slipnet$suffix://abc")
    }
    check(System.getenv("CI") != "true" || !System.getenv("ROVUNET_ANDROID_FIXTURE").isNullOrBlank()) { "CI requires real panel fixture" }
    System.getenv("ROVUNET_ANDROID_FIXTURE")?.let { file ->
        val result = importer.parseAndImport(java.io.File(file).readText())
        check(result is ImportResult.Success) { result.toString() }
        val p = result.profiles.single()
        check(p.tunnelType == TunnelType.SOAR)
        check(p.collectionName == "alice — 192.0.2.1")
        check(p.collectionId.startsWith("panel:"))
        println("PASS real Go panel v32 output imported by Android parser")
    }
    val panelText = System.getenv("ROVUNET_PANEL_TEXT_FIXTURE")
    check(System.getenv("CI") != "true" || !panelText.isNullOrBlank()) { "CI requires full panel text fixture" }
    panelText?.let { file ->
        val result = importer.parseAndImport(java.io.File(file).readText()) as ImportResult.Success
        check(result.profiles.size == 6)
        val groups = CollectionInference.plan(result.profiles)
        check(groups.size == 2 && groups.all { it.profiles.size == 3 }) { groups.map { it.name to it.profiles.size }.toString() }
        check(groups.map { it.profiles.map { p -> p.importAccountHint }.toSet() }.toSet() == setOf(setOf("alice"),setOf("bob")))
        check(groups.all { ProfileDuplicates.withinImport(it.profiles).size == 2 })
        println("PASS real panel import removes equivalent native/standard VLESS within each user's collection")
        println("PASS actual showUserConfigs full text: two users, each native Soar/VLESS + standard VLESS")
    }
    println("PASS collection identity/isolation, DNS-last policy, native v32 metadata and legacy aliases")
}
