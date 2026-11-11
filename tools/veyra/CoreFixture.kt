import app.slipnet.core.CoreLink
import java.io.File
fun main(args: Array<String>) {
    File(args[1]).writeText(CoreLink.runtime(File(args[0]).readText(), args[2].toInt()))
}
