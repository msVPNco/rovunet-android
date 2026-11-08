package app.slipnet.tunnel

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import org.junit.Assert.*
import org.junit.Test

class WebSocketFramesTest {
    @Test fun serverPingReceivesMaskedPongWithTheSamePayload() {
        val payload = "fixture-ping".toByteArray()
        val output = ByteArrayOutputStream()
        val result = WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x89.toByte(), payload.size.toByte()) + payload), output)
        assertArrayEquals(ByteArray(0), result)
        val pong = output.toByteArray()
        assertEquals(0x8A, pong[0].toInt() and 255)
        assertTrue(pong[1].toInt() and 0x80 != 0)
        assertEquals(payload.size, pong[1].toInt() and 127)
        val mask = pong.copyOfRange(2, 6)
        val echoed = pong.copyOfRange(6, pong.size).mapIndexed { i, byte -> (byte.toInt() xor mask[i % 4].toInt()).toByte() }.toByteArray()
        assertArrayEquals(payload, echoed)
    }
    @Test fun dataAndUnsolicitedPongDoNotGenerateExtraTraffic() {
        val output = ByteArrayOutputStream()
        assertArrayEquals(byteArrayOf(1, 2), WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x82.toByte(), 2, 1, 2)), output))
        assertArrayEquals(ByteArray(0), WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x8A.toByte(), 0)), output))
        assertEquals(0, output.size())
    }
    @Test fun closeAndMalformedControlFramesNeverStartAPongLoop() {
        val output = ByteArrayOutputStream()
        assertNull(WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x88.toByte(), 0)), output))
        assertNull(WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x09, 0)), output))
        assertNull(WebSocketFrames.read(ByteArrayInputStream(byteArrayOf(0x89.toByte(), 126, 0, 126) + ByteArray(126)), output))
        assertEquals(0, output.size())
    }
}
