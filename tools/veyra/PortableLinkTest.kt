import app.slipnet.data.export.PortableLink
import app.slipnet.data.export.ConfigSchemes
import app.slipnet.util.ConnectionDiagnostics

fun main() {
    val ssh = PortableLink.parse("ssh://a%2Bb:p%40%3Ass@[2001:db8::1]:2222#My%20SSH")
    check(ssh.host == "2001:db8::1" && ssh.port == 2222)
    check(ssh.username == "a+b" && ssh.password == "p@:ss" && ssh.name == "My SSH")
    check(PortableLink.parse("SOCKS5://example.org").port == 1080)
    check(PortableLink.parse("naive+https://u:p@example.org").port == 443)
    check(PortableLink.parse("ssh://a+b:p@example.org").username == "a+b")
    for (bad in listOf("ssh://example.org", "ssh://u:p@example.org:0", "socks5://example.org:65536", "socks5://example.org:abc", "ssh://u:p@example.org?key=hidden", "socks5://example.org/path", "https://example.org", "ssh://u:%zz@example.org")) {
        check(runCatching { PortableLink.parse(bad) }.isFailure) { "Accepted $bad" }
    }
    for (scheme in listOf("veyra", "veyra-enc", "veyra-bundle-enc")) {
        val uri = "${scheme.uppercase()}://AbC+/="
        check(ConfigSchemes.accepts(uri))
        check(ConfigSchemes.canonical(uri) == uri.replace(scheme.uppercase(), scheme.replace("veyra", "slipnet")))
    }
    check(!ConfigSchemes.accepts("https://example.org"))
    repeat(300) { ConnectionDiagnostics.record(ConnectionDiagnostics.Event.CONNECT_REQUEST) }
    check(ConnectionDiagnostics.report().lines().count { "CONNECT_REQUEST" in it } == 256)
    ConnectionDiagnostics.clear()
    check(!ConnectionDiagnostics.report().contains("CONNECT_REQUEST"))
    println("Portable URI, alias routing and bounded diagnostic checks passed")
}
