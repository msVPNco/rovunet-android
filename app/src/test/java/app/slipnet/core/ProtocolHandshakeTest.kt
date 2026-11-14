package app.slipnet.core

import org.junit.Assert.*
import org.junit.Test

class ProtocolHandshakeTest {
    @Test fun quickProbeNeverReplacesUnsupportedUdpMasksWithPlainQuic() {
        assertTrue(ProtocolHandshake.supports("hy2://synthetic@example.invalid:443?sni=example.invalid"))
        assertTrue(ProtocolHandshake.supports("tuic://00000000-0000-4000-8000-000000000001:synthetic@example.invalid:443?sni=example.invalid"))
        assertFalse(ProtocolHandshake.supports("hy2://synthetic@example.invalid:443?mport=443,8443&hop-interval=30"))
        assertFalse(ProtocolHandshake.supports("vless://00000000-0000-4000-8000-000000000001@example.invalid:443?type=kcp&security=none"))
        assertFalse(ProtocolHandshake.supports("mierus://synthetic:secret@example.invalid?profile=fixture&port=8443&protocol=UDP"))
    }
}
