import app.slipnet.core.CoreProcess
import java.io.File
import java.net.ServerSocket
import java.net.Socket

fun main() {
    val binary = System.getenv("XRAY")?.let(::File) ?: return
    val core = CoreProcess()
    val launcher = System.getenv("CORE_LAUNCHER")?.let(::File)
    val port = ServerSocket(0).use { it.localPort }
    val config = """{"log":{"loglevel":"none"},"inbounds":[{"listen":"127.0.0.1","port":$port,"protocol":"socks","settings":{"udp":true}}],"outbounds":[{"protocol":"freedom"}]}"""
    repeat(30) {
        core.start(binary,config,launcher).getOrThrow()
        check(core.isRunning())
        Socket("127.0.0.1",port).use { s -> s.soTimeout=2000;s.getOutputStream().write(byteArrayOf(5,1,0));check(s.getInputStream().read()==5 && s.getInputStream().read()==0) }
        core.stop();check(!core.isRunning())
        ServerSocket(port).close()
    }
    ServerSocket(port).use {
        check(core.start(binary,config,launcher).isFailure)
        check(!core.isRunning())
    }
    check(core.start(binary,"invalid-json",launcher).isFailure)
    check(!core.isRunning())
    Thread.sleep(100)
    check(Thread.getAllStackTraces().keys.none { it.isAlive && it.name == "veyra-core-output" })
    println("Core lifecycle: 30 start/stop cycles, occupied port and invalid-config cleanup passed")
}
