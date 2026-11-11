import app.slipnet.domain.model.*
import app.slipnet.data.export.*
fun main() {
 val base = ServerProfile(name="test", domain="t.example.org", tunnelType=TunnelType.DNSTT, sshHost="192.0.2.1", socksUsername="alice", socksPassword="test-only")
 val paths = listOf(base,base.copy(domain="s.example.org"),base.copy(domain="example.org",tunnelType=TunnelType.SSH,sshUsername="alice"),base.copy(domain="rs.example.org",sshHost="127.0.0.1"),base.copy(domain="r.example.org",socksUsername=""),ServerProfile(name="VLESS",domain="example.org",tunnelType=TunnelType.VLESS))
 check(CollectionInference.plan(paths).single().profiles.size == 6)
 check(CollectionInference.plan(listOf(base,base.copy(socksUsername="bob"))).size == 2)
 check(CollectionInference.plan(listOf(base,base.copy(sshHost="192.0.2.2"))).size == 2)
 check(CollectionInference.plan(listOf(base.copy(collectionId="A"),base.copy(collectionId="B"))).size == 2)
 val ambiguous=CollectionInference.plan(listOf(base,base.copy(socksUsername="bob"),paths.last()))
 check(ambiguous.size == 3)
 val imported=ConfigImporter().parseAndImport("[dnstt] user=alice\n"+ConfigExporter().exportSingleProfile(base)+"\n[vless] user=alice\n"+ConfigExporter().exportSingleProfile(paths.last())) as ImportResult.Success
 check(imported.profiles.all { it.importAccountHint == "alice" })
 check(CollectionInference.plan(imported.profiles).size == 1)
 check(paths.first().socksPassword == "test-only")
 val labelImport=ConfigImporter().parseAndImport("[vless] Rovunet\n"+ConfigExporter().exportSingleProfile(paths.last())) as ImportResult.Success
 check(labelImport.profiles.single().importAccountHint.isEmpty())
 check(CollectionInference.plan(listOf(base.copy(sshHost="2001:db8::1"),base.copy(sshHost="2001:0db8:0:0:0:0:0:1"))).size == 1)
 check(CollectionInference.plan(listOf(paths.last(),paths.last().copy(vlessUuid="different-user"))).size == 2)
 val before = listOf(base.copy(id=1,collectionId="old",collectionName="Custom"))
 val after = before.map { it.copy(collectionId="new",collectionName="Merged") }
 val snapshot = RegroupHistory.encode(before,after)
 check(!snapshot.contains("test-only"))
 check(RegroupHistory.restore(snapshot,after.map { it.copy(socksPassword="updated") }).single().let { it.collectionName=="Custom" && it.socksPassword=="updated" })
 check(runCatching { RegroupHistory.restore(snapshot,after.map { it.copy(collectionName="Later edit") }) }.isFailure)
 check(RegroupHistory.restore(snapshot,emptyList()).isEmpty())
 println("PASS persistent undo, deleted profiles, later edits and credential preservation")
 println("PASS inferred account/server groups, ambiguous isolation, metadata and heading hints")
}
