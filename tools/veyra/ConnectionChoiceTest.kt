import app.slipnet.domain.model.*
fun main() {
    val ssh = ServerProfile(id=1, name="SSH", domain="example.org", tunnelType=TunnelType.SSH)
    val vless = ssh.copy(id=2, name="VLESS", isActive=true)
    val profiles = listOf(ssh,vless)
    check((ConnectionChoice.resolve(1,profiles) as ConnectionChoice.Target.Direct).profile.id == 1L)
    check(ConnectionChoice.resolve(null,profiles) == ConnectionChoice.Target.Automatic)
    check(ConnectionChoice.resolve(3,profiles) == ConnectionChoice.Target.Missing)
    val results = mapOf(2L to PingResult.Success(100,verified=true))
    check(ConnectionChoice.summaryProfileId(profiles,results,1,null) == 1L)
    check(ConnectionChoice.summaryProfileId(profiles,results,null,1) == 1L)
    check(ConnectionChoice.summaryProfileId(profiles,results,null,null) == 2L)
    check(ConnectionChoice.summaryProfileId(profiles,results,3,null) == null)
    check(ConnectionChoice.summaryProfileId(profiles,mapOf(1L to PingResult.Success(1)),null,null) == null)
    println("PASS manual selection, deleted target, and active-path measurement isolation")
}
