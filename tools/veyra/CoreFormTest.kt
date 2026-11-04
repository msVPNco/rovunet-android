import app.slipnet.core.CoreForm
import app.slipnet.core.CoreLink

fun main() {
    val key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="
    val id = "00000000-0000-4000-8000-000000000001"
    val links = listOf(
        "vless://$id@example.org:443?security=reality&type=tcp&pbk=${key.trimEnd('=')}&sid=ab&spx=%2Ffoo&flow=xtls-rprx-vision#REALITY",
        "vless://$id@[2001:db8::1]:443?security=tls&type=xhttp&host=example.org&sni=example.org&path=%2Fa%3Fb%3D1&mode=packet-up&extra=%7B%22scMaxEachPostBytes%22%3A100000%7D&alpn=h2#XHTTP",
        "hysteria2://p%40ss%2B%3A%23@example.org:443?sni=example.org&obfs=salamander&obfs-password=a%2Bb#HY2",
        "[Interface]\nPrivateKey = $key\nAddress = 10.66.0.2/32\nDNS = 1.1.1.1\nMTU = 1420\n[Peer]\nPublicKey = $key\nPresharedKey = $key\nEndpoint = example.org:51820\nAllowedIPs = 0.0.0.0/0, ::/0\nPersistentKeepalive = 25"
    )
    links.forEach { raw ->
        val original = CoreLink.parse(raw)
        val edited = CoreLink.parse(CoreForm.decode(raw).encode("نام تست"))
        check(original.outbound.similar(edited.outbound)) { "Roundtrip changed runtime settings" }
    }
    val reality = CoreForm.fresh("reality").with("server", "example.org").with("auth", id).with("pbk", key.trimEnd('='))
    check(CoreLink.parse(reality.encode("New")).outbound.getString("protocol") == "vless")
    val switched = reality.with("type", "xhttp").with("path", "/new")
    check(CoreLink.parse(switched.encode("XHTTP")).outbound.getJSONObject("streamSettings").getString("network") == "xhttp")
    val wg = CoreForm.decode(links[3]).with("endpoint", "other.example:51821")
    check(CoreLink.parse(wg.encode("WG")).host == "other.example")
    check(runCatching { wg.with("endpoint", "x:123\nPostUp = command").encode("bad") }.isFailure)
    check(runCatching { CoreLink.parse(reality.with("sid", "z").encode("bad")) }.isFailure)
    check(runCatching { reality.with("port", "70000").encode("bad") }.isFailure)
    check(runCatching { CoreForm.decode(links[1]).with("extra", "{\"downloadSettings\":{}}").encode("bad").let(CoreLink::parse) }.isFailure)
    println("Core editor: four protocol roundtrips, IPv6, secret escaping, XHTTP options, transport switch and invalid fields passed")
}
