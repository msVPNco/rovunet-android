import app.slipnet.core.ReleaseVersion
fun main() {
    for ((remote,current) in listOf("v0.3.0-alpha07" to "0.3.0-alpha06", "0.3.0-alpha10" to "0.3.0-alpha9", "0.3.0" to "0.3.0-rc1", "0.3.0-beta1" to "0.3.0-alpha99", "0.3.0-rc1" to "0.3.0-beta9", "0.3.1" to "0.3.0-lite")) check(ReleaseVersion.isNewer(remote,current))
    for ((remote,current) in listOf("0.3.0-alpha06" to "0.3.0-alpha06", "0.3.0-alpha07" to "0.3.0", "0.2.9" to "0.3.0", "garbage" to "0.3.0", "0.4.0" to "unknown", "0.3.0" to "0.3.0-lite")) check(!ReleaseVersion.isNewer(remote,current))
    println("PASS release comparison, prerelease numbers, invalid versions and downgrade prevention")
}
