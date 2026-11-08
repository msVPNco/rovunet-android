package app.slipnet.tunnel

import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.security.SecureRandom

/** Client framing used by VLESS/WS; Ping replies use the same serialized writer as data. */
internal object WebSocketFrames {
    private val maskingRandom = SecureRandom()
    fun write(out: OutputStream, payload: ByteArray, opcode: Int = 0x02) {
        val header = ByteArrayOutputStream()
        // FIN + opcode
        header.write(0x80 or opcode)
        // Mask bit set (client) + length
        val len = payload.size
        when {
            len <= 125 -> header.write(0x80 or len)
            len <= 65535 -> {
                header.write(0x80 or 126)
                header.write(len shr 8 and 0xFF)
                header.write(len and 0xFF)
            }
            else -> {
                header.write(0x80 or 127)
                for (i in 7 downTo 0) {
                    header.write((len.toLong() shr (i * 8) and 0xFF).toInt())
                }
            }
        }
        // Masking key
        val mask = ByteArray(4)
        maskingRandom.nextBytes(mask)
        header.write(mask)

        // Masked payload
        val masked = ByteArray(len)
        for (i in 0 until len) {
            masked[i] = (payload[i].toInt() xor mask[i % 4].toInt()).toByte()
        }

        out.write(header.toByteArray())
        out.write(masked)
        out.flush()
    }

    /**
     * Read a WebSocket frame. Returns payload bytes, or null on EOF/close.
     */
    fun read(input: InputStream, output: OutputStream): ByteArray? {
        val b0 = input.read()
        if (b0 < 0) return null
        val b1 = input.read()
        if (b1 < 0) return null

        val opcode = b0 and 0x0F
        val masked = (b1 and 0x80) != 0
        var payloadLen = (b1 and 0x7F).toLong()

        if (payloadLen == 126L) {
            val h = input.read()
            val l = input.read()
            if (h < 0 || l < 0) return null
            payloadLen = ((h shl 8) or l).toLong()
        } else if (payloadLen == 127L) {
            var len = 0L
            for (i in 0 until 8) {
                val b = input.read()
                if (b < 0) return null
                len = (len shl 8) or b.toLong()
            }
            payloadLen = len
        }

        var maskKey: ByteArray? = null
        if (masked) {
            maskKey = ByteArray(4)
            readFully(input, maskKey)
        }

        if (payloadLen < 0 || payloadLen > 16 * 1024 * 1024) return null
        if (opcode >= 0x08 && (payloadLen > 125 || b0 and 0x80 == 0)) return null

        val payload = ByteArray(payloadLen.toInt())
        if (payloadLen > 0) {
            readFully(input, payload)
            if (maskKey != null) {
                for (i in payload.indices) {
                    payload[i] = (payload[i].toInt() xor maskKey[i % 4].toInt()).toByte()
                }
            }
        }

        return when (opcode) {
            0x01, 0x02, 0x00 -> payload // text, binary, continuation
            0x08 -> null // close
            0x09 -> { // ping -> pong
                // RFC 6455 5.5.2: the Pong echoes the Ping payload and is client-masked.
                synchronized(output) { write(output, payload, opcode = 0x0A) }
                ByteArray(0)
            }
            0x0A -> ByteArray(0) // pong (ignore)
            else -> payload
        }
    }


    private fun readFully(input: InputStream, bytes: ByteArray) {
        var offset = 0
        while (offset < bytes.size) {
            val count = input.read(bytes, offset, bytes.size - offset)
            if (count < 0) throw java.io.EOFException()
            offset += count
        }
    }
}
