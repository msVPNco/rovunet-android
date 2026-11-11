package app.slipnet.data.export

import java.util.Base64
import org.junit.Assert.*
import org.junit.Test

class WireBase64Test {
    @Test fun standardVectorsRemainByteCompatible() {
        val vectors = listOf("" to "", "f" to "Zg==", "fo" to "Zm8=", "foo" to "Zm9v",
            "foob" to "Zm9vYg==", "fooba" to "Zm9vYmE=", "foobar" to "Zm9vYmFy")
        for ((plain, encoded) in vectors) {
            assertEquals(encoded, WireBase64.encode(plain.toByteArray()))
            assertArrayEquals(plain.toByteArray(), WireBase64.decode(encoded))
        }
    }

    @Test fun binaryAndUnicodeDataMatchTheIndependentJdkCodec() {
        for (size in listOf(0, 1, 2, 3, 7, 31, 32, 127, 255, 256, 257, 1024)) {
            val bytes = ByteArray(size) { it.toByte() }
            val encoded = Base64.getEncoder().encodeToString(bytes)
            assertEquals(encoded, WireBase64.encode(bytes))
            assertArrayEquals(bytes, WireBase64.decode(encoded))
        }
        val unicode = "سرور — 示例 — сервер | : 🔒".toByteArray(Charsets.UTF_8)
        assertArrayEquals(unicode, WireBase64.decode(Base64.getEncoder().encodeToString(unicode)))
    }

    @Test fun omittedPaddingAndAsciiWhitespaceRemainSupported() {
        for (value in listOf("Zg", " Z\tg\r\n== ", "Zg==")) {
            assertArrayEquals(byteArrayOf('f'.code.toByte()), WireBase64.decode(value))
        }
        assertArrayEquals("fo".toByteArray(), WireBase64.decode("Zm8"))
        assertArrayEquals(ByteArray(0), WireBase64.decode(" \t\r\n"))
    }

    @Test fun malformedPaddingAndAlphabetAreRejected() {
        for (value in listOf("Z", "Zg=", "Zg===", "=Zg=", "====", "Zm=v", "Zm9v====", "Zg!!", "Zg-_")) {
            assertThrows("Accepted malformed input: $value", IllegalArgumentException::class.java) {
                WireBase64.decode(value)
            }
        }
    }
}
