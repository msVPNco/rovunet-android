package app.slipnet.tunnel

import java.net.InetAddress
import java.net.Inet6Address
import org.junit.Assert.*
import org.junit.Test

class ProxyAuthLimiterTest {
    private val first = InetAddress.getByAddress(byteArrayOf(10, 0, 0, 1))
    private val second = InetAddress.getByAddress(byteArrayOf(10, 0, 0, 2))

    @Test fun fiveFailuresBlockOnlyThatPeerAndTheWindowExpires() {
        var now = 1L
        val limiter = ProxyAuthLimiter { now }
        repeat(5) { assertFalse(limiter.check(first) { false }) }
        var called = false
        assertFalse(limiter.check(first) { called = true; true })
        assertFalse(called)
        assertTrue(limiter.check(second) { true })
        now += 60_000_000_001L
        assertTrue(limiter.check(first) { true })
    }

    @Test fun normalAuthenticatedTrafficAndLocalTunAreNotRateLimited() {
        val limiter = ProxyAuthLimiter()
        repeat(50) { assertTrue(limiter.check(first) { true }) }
        repeat(20) { assertFalse(limiter.check(InetAddress.getByAddress(byteArrayOf(127, 0, 0, 1))) { false }) }
        assertTrue(limiter.check(first) { true })
    }

    @Test fun simultaneousPendingAuthenticationCannotBypassTheLimit() {
        val limiter = ProxyAuthLimiter()
        var depth = 0
        fun nested(): Boolean {
            depth++
            return if (depth < 6) limiter.check(first) { nested() } else error("Sixth login was admitted")
        }
        assertFalse(limiter.check(first) { nested() })
        assertEquals(5, depth)
    }

    private fun mapped(address: InetAddress): Inet6Address {
        val bytes = ByteArray(16)
        bytes[10] = 0xff.toByte()
        bytes[11] = 0xff.toByte()
        address.address.copyInto(bytes, 12)
        // Unlike InetAddress.getByAddress, this factory preserves an explicit mapped IPv6 instance.
        return Inet6Address.getByAddress(null, bytes, 0)
    }

    @Test fun ipv4AndExplicitMappedIpv6ShareOneFailureBudget() {
        val limiter = ProxyAuthLimiter()
        repeat(3) { assertFalse(limiter.check(first) { false }) }
        repeat(2) { assertFalse(limiter.check(mapped(first)) { false }) }
        var called = false
        for (peer in listOf(first, mapped(first))) {
            assertFalse(limiter.check(peer) { called = true; true })
        }
        assertFalse(called)
        assertTrue(limiter.check(second) { true })
    }

    @Test fun explicitMappedLoopbackRemainsExempt() {
        val limiter = ProxyAuthLimiter()
        val local = mapped(InetAddress.getByAddress(byteArrayOf(127, 0, 0, 1)))
        repeat(20) { assertFalse(limiter.check(local) { false }) }
        assertTrue(limiter.check(local) { true })
    }

    @Test fun capacityRefusesNewPeersWithoutEvictionAndWarnsOncePerMinute() {
        var now = 1L
        var warnings = 0
        val limiter = ProxyAuthLimiter(onCapacityLimit = { warnings++ }, clockNanos = { now })
        repeat(5) { assertFalse(limiter.check(first) { false }) }
        repeat(511) { i ->
            val peer = InetAddress.getByAddress(byteArrayOf(10, 1, (i / 256).toByte(), i.toByte()))
            assertFalse(limiter.check(peer) { false })
        }
        var attempted = false
        var blocked = 0
        repeat(50) { assertFalse(limiter.check(second, { blocked++ }) { attempted = true; true }) }
        assertFalse(attempted)
        assertEquals(50, blocked)
        assertEquals(1, warnings)
        // Saturation must not make a previously blocked peer eligible again.
        assertFalse(limiter.check(first) { attempted = true; true })
        assertFalse(attempted)
        now += 60_000_000_001L
        assertTrue(limiter.check(second) { true })
        assertEquals(1, warnings)
    }

    @Test fun pendingPeersCannotBeEvictedAndRepeatedCapacityWarningsAreThrottled() {
        var now = 1L
        var warnings = 0
        val limiter = ProxyAuthLimiter(onCapacityLimit = { warnings++ }, clockNanos = { now })
        fun hold(index: Int): Boolean {
            if (index == 512) {
                repeat(20) { assertFalse(limiter.check(second) { error("Over-capacity login") }) }
                assertEquals(1, warnings)
                now += 60_000_000_001L
                repeat(20) { assertFalse(limiter.check(second) { error("Pending peer was evicted") }) }
                assertEquals(2, warnings)
                return true
            }
            val peer = InetAddress.getByAddress(byteArrayOf(10, 2, (index / 256).toByte(), index.toByte()))
            return limiter.check(peer) { hold(index + 1) }
        }
        assertTrue(hold(0))
        assertTrue(limiter.check(second) { true })
        assertEquals(2, warnings)
    }
}
