import app.slipnet.core.CoreProcess
import java.io.File
import java.net.ServerSocket
import java.nio.file.Files

fun main() {
    val directory = Files.createTempDirectory("rovunet-core-readiness-").toFile()
    val fake = File(directory, "fake-core.py")
    fake.writeText("""#!/usr/bin/env python3
import json,sys,time,socket,signal
cfg=json.load(sys.stdin)
if cfg.get('ignoreTerm'):signal.signal(signal.SIGTERM,signal.SIG_IGN)
if cfg.get('neverListen'):time.sleep(60)
time.sleep(cfg.get('readyDelayMs',0)/1000)
s=socket.socket();s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
s.bind(('127.0.0.1',cfg['inbounds'][0]['port']));s.listen(4)
print('2026/01/01 00:00:00.000000 [Warning] core: Xray 26.10.31 started',flush=True)
while True:
 c,_=s.accept()
 with c:
  ver=c.recv(1);n=c.recv(1)
  if not n:continue
  c.recv(n[0]);c.sendall(bytes([5,0]))
  while c.recv(1024):pass
""")
    check(fake.setExecutable(true))
    val port = ServerSocket(0).use { it.localPort }
    fun config(extra: String) = """{"inbounds":[{"listen":"127.0.0.1","port":$port,"protocol":"socks"}],$extra}"""
    val core = CoreProcess()
    try {
        val start = System.nanoTime()
        core.start(fake, config("\"readyDelayMs\":500")).getOrThrow()
        check((System.nanoTime() - start) / 1_000_000 >= 450) { "Child liveness was mistaken for listener readiness" }
        core.stop()
        ServerSocket(port).close()
        check(core.start(fake, config("\"neverListen\":true"), startupTimeoutMs = 300).isFailure)
        check(!core.isRunning())
        // A different listener can offer even the exact requested SOCKS method.
        java.net.ServerSocket(port).use { imposter ->
            val result = core.start(fake, config("\"neverListen\":true"), startupTimeoutMs = 300)
            check(result.isFailure && !core.isRunning())
            imposter.soTimeout = 100
            check(runCatching { imposter.accept().close() }.isFailure) { "Sent a handshake to an unrelated listener" }
        }
        ServerSocket(port).close()
        core.start(fake, config("\"ignoreTerm\":true")).getOrThrow()
        core.stop()
        check(!core.isRunning())
        // A different listener can offer even the exact requested SOCKS method.
        java.net.ServerSocket(port).use { imposter ->
            val result = core.start(fake, config("\"neverListen\":true"), startupTimeoutMs = 300)
            check(result.isFailure && !core.isRunning())
            imposter.soTimeout = 100
            check(runCatching { imposter.accept().close() }.isFailure) { "Sent a handshake to an unrelated listener" }
        }
        ServerSocket(port).close()
        println("PASS delayed core listener readiness, bounded never-ready startup and forced-stop cleanup")
    } finally { core.stop();directory.deleteRecursively() }
}
