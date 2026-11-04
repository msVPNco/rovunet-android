import app.slipnet.domain.model.*
fun main() {
 val p=ServerProfile(id=1,name="first",domain="example.org",collectionId="A",tunnelType=TunnelType.XRAY,coreLink="vless://user@example.org:443?security=reality#name")
 val kinds=listOf(p,p.copy(id=2,coreLink="vless://user@example.org:443?type=xhttp"),p.copy(id=3,coreLink="hysteria2://user@example.org:443"),p.copy(id=4,coreLink="wireguard://key@example.org:443"))
 check(ProfileDuplicates.plan(kinds).isEmpty())
 val duplicate=p.copy(id=5,name="renamed",coreLink=p.coreLink.substringBefore('#')+"#other",createdAt=0,sortOrder=8)
 check(ProfileDuplicates.plan(kinds+duplicate).single().remove.id==5L)
 check(ProfileDuplicates.plan(listOf(p,duplicate.copy(collectionId="B"))).isEmpty())
 check(ProfileDuplicates.plan(listOf(p,duplicate),setOf(1,5)).isEmpty())
 check(ProfileDuplicates.plan(listOf(p,duplicate.copy(isLocked=true))).isEmpty())
 val v=p.copy(tunnelType=TunnelType.VLESS,coreLink="",vlessUuid="one")
 for (other in listOf(v.copy(vlessUuid="two"),v.copy(vlessWsPath="/other"),v.copy(vlessSni="other.org"),v.copy(sshTlsSni="other"),v.copy(soarCountry="IR"))) {
  check(ProfileDuplicates.plan(listOf(v,other.copy(id=9))).isEmpty())
 }
 check(ProfileDuplicates.plan(listOf(v,v.copy(id=9,sshHost="192.0.2.2",resolvers=listOf(DnsResolver("192.0.2.53"))))).size==1)
 check(ProfileDuplicates.plan(listOf(p,p.copy(id=8,resolvers=listOf(DnsResolver("192.0.2.53"))))).isEmpty())
 check(ProfileDuplicates.withinImport(kinds+duplicate).size==4)
 println("PASS four cores on one host retained; full-field equality, collection isolation, protected records and import dedup")
}
