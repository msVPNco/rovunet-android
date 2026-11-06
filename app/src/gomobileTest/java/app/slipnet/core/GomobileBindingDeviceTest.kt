package app.slipnet.core

import mobile.DnsttClient
import mobile.Mobile
import org.junit.Assert.*
import org.junit.Test
import soar.SoarClient
import snowflake.SnowflakeClient
import vaydns.VaydnsClient

/** Also run against the minified private Release APK, with the same test signer. */
class GomobileBindingDeviceTest {
    @Test fun nativeConstructorsAndReferenceCallbacksSurviveMinification() {
        // No remote server or configuration is used. Every wrapper crosses JNI,
        // including native-created (int refnum) constructors and Go reference tracking.
        repeat(8) {
            val dns = DnsttClient()
            dns.setNoizMode(true)
            dns.setResolverMode("fanout")
            dns.setRRSpreadCount(1)
            assertFalse(dns.isRunning)
            dns.stop()
            val vay = VaydnsClient()
            assertFalse(vay.isRunning)
            vay.stop()
            val soar = SoarClient()
            assertFalse(soar.isRunning)
            soar.stop()
            val snowflake = SnowflakeClient()
            assertFalse(snowflake.isRunning)
            snowflake.stop()
        }
    }

    @Test fun nativeErrorConstructorSurvivesMinification() {
        try {
            Mobile.newClient("127.0.0.1:9", "fixture.invalid", "not-a-key", "127.0.0.1:0")
            fail("The native constructor must reject the synthetic invalid key")
        } catch (failure: Exception) {
            // The generated go.Universe.proxyerror is constructed by native code.
            assertNotNull(failure.message)
            assertTrue(failure.message.orEmpty().isNotEmpty())
        }
    }
}
