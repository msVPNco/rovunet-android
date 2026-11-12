package app.slipnet.core

import java.io.ByteArrayOutputStream
import java.io.InputStream
import java.nio.ByteBuffer
import java.nio.charset.CodingErrorAction

/** Bound bytes before decoding or allocating strings; malformed UTF-8 is not repaired silently. */
object BoundedTextInput {
    fun read(input: InputStream, limit: Int, checkCancelled: () -> Unit = {}): String {
        require(limit in 1..2 * 1024 * 1024)
        val output = ByteArrayOutputStream()
        val buffer = ByteArray(8192)
        while (true) {
            checkCancelled()
            val count = input.read(buffer, 0, minOf(buffer.size, limit + 1 - output.size()))
            if (count < 0) break
            require(output.size() + count <= limit) { "Input too large" }
            output.write(buffer, 0, count)
        }
        checkCancelled()
        return Charsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
            .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(output.toByteArray())).toString()
    }
}
