import app.slipnet.core.ReleaseVersion
fun main() {
    for ((remote,current) in listOf("v0.3.0-alpha07" to "0.3.0-alpha06", "0.3.0-alpha10" to "0.3.0-alpha9", "0.3.0" to "0.3.0-rc1", "0.3.0-beta1" to "0.3.0-alpha99", "0.3.0-rc1" to "0.3.0-beta9", "0.3.1" to "0.3.0-lite")) check(ReleaseVersion.isNewer(remote,current))
    for ((remote,current) in listOf("0.3.0-alpha06" to "0.3.0-alpha06", "0.3.0-alpha07" to "0.3.0", "0.2.9" to "0.3.0", "garbage" to "0.3.0", "0.4.0" to "unknown", "0.3.0" to "0.3.0-lite")) check(!ReleaseVersion.isNewer(remote,current))
    val revisions = listOf("0.3.0-alpha15", "0.3.0-alpha15.1", "0.3.0-alpha15.2", "0.3.0-alpha15.10", "0.3.0-alpha16")
    for ((old, next) in revisions.zipWithNext()) {
        check(ReleaseVersion.supports(next))
        check(ReleaseVersion.isNewer(next, old))
        check(!ReleaseVersion.isNewer(old, next))
        check(ReleaseVersion.isNewer(next + "-lite", old + "-lite"))
    }
    check(!ReleaseVersion.isNewer("0.3.0-alpha15.0", "0.3.0-alpha15"))
    for (bad in listOf("0.3.0-alpha15..1", "0.3.0-alpha15.", "0.3.0-alpha15.1-review01")) check(!ReleaseVersion.supports(bad))
    println("PASS release comparison, prerelease numbers, invalid versions and downgrade prevention")
}
