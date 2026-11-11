import app.slipnet.tunnel.*
import app.slipnet.data.local.datastore.DomainRoutingMode
import com.jcraft.jsch.HostKeyRepository
import java.net.*
import java.io.*
import java.security.KeyStore
import java.util.concurrent.*
import javax.net.ssl.*
import kotlin.concurrent.thread

fun main() {
 TransportTrust.initialize(android.content.Context())
 val keys=TransportTrust.sshKeys;val key=byteArrayOf(1,2,3)
 TransportTrust.allowPrompt=false
 check(keys.check("server-a",key)==HostKeyRepository.NOT_INCLUDED)
 fun approve(waitMs: Long = 0, action:()->Unit) {
  val task=FutureTask { action() };thread(isDaemon=true){task.run()}
  val until=System.nanoTime()+TimeUnit.SECONDS.toNanos(2)
  while(TransportTrust.request.value==null && !task.isDone && System.nanoTime()<until) Thread.yield()
  check(TransportTrust.request.value!=null) { "No identity confirmation" }
  val pausedBefore = TransportTrust.decisionWaitNanos
  if (waitMs > 0) { Thread.sleep(waitMs); check(TransportTrust.decisionWaitNanos - pausedBefore >= TimeUnit.MILLISECONDS.toNanos(waitMs - 20)) }
  TransportTrust.answer(requireNotNull(TransportTrust.request.value), true);task.get(5,TimeUnit.SECONDS)
 }
 TransportTrust.allowPrompt=true
 approve { check(keys.check("server-a",key)==HostKeyRepository.OK) }
 TransportTrust.allowPrompt=false
 check(keys.check("server-a",key)==HostKeyRepository.OK)
 check(keys.check("server-a",byteArrayOf(9))==HostKeyRepository.CHANGED)
 check(keys.check("server-b",key)==HostKeyRepository.NOT_INCLUDED)
 val store=KeyStore.getInstance("PKCS12").apply{FileInputStream(System.getenv("TEST_TLS_STORE")).use{load(it,"test-only".toCharArray())}}
 val km=KeyManagerFactory.getInstance(KeyManagerFactory.getDefaultAlgorithm()).apply{init(store,"test-only".toCharArray())}
 val ctx=SSLContext.getInstance("TLS").apply{init(km.keyManagers,null,null)}
 val ssl=ctx.serverSocketFactory.createServerSocket(0) as SSLServerSocket
 thread(isDaemon=true){while(!ssl.isClosed)runCatching { val s=ssl.accept() as SSLSocket;thread(isDaemon=true){runCatching{s.use{it.startHandshake()}}} }}
 check(runCatching { TlsSocketFactory("localhost",1000).createSocket("127.0.0.1",ssl.localPort) }.isFailure)
 TransportTrust.allowPrompt=true
 approve(waitMs = 1200) { TlsSocketFactory("localhost",1000).createSocket("127.0.0.1",ssl.localPort).close() }
 approve { check(runCatching { TlsSocketFactory("wrong.invalid",1000).createSocket("127.0.0.1",ssl.localPort) }.isFailure) }
 TransportTrust.allowPrompt=false
 ssl.close()
 val hung=ServerSocket(0);val raw=CompletableFuture<Socket>()
 thread(isDaemon=true){raw.complete(hung.accept())}
 val start=System.nanoTime()
 check(runCatching{TlsSocketFactory("localhost",100).createSocket("127.0.0.1",hung.localPort)}.isFailure)
 check(TimeUnit.NANOSECONDS.toMillis(System.nanoTime()-start)<2500)
 raw.get(1,TimeUnit.SECONDS).close();hung.close()
 println("PASS real transport trust: explicit approval, saved keys, changed-key rejection, certificate/hostname checks and TLS deadline")
 val socks=ServerSocket(0)
 thread(isDaemon=true){socks.accept().use{s->
  val input=DataInputStream(s.inputStream);val out=s.outputStream
  check(input.readUnsignedByte()==5);input.readFully(ByteArray(input.readUnsignedByte()));out.write(byteArrayOf(5,2));out.flush()
  check(input.readUnsignedByte()==1);val u=ByteArray(input.readUnsignedByte());input.readFully(u);val p=ByteArray(input.readUnsignedByte());input.readFully(p)
  check(String(u)=="u"&&String(p)=="p");out.write(byteArrayOf(1,0));out.flush()
  check(input.readUnsignedByte()==5);input.readUnsignedByte();input.readUnsignedByte();check(input.readUnsignedByte()==3);input.readFully(ByteArray(input.readUnsignedByte()));input.readUnsignedShort()
  out.write(byteArrayOf(5,0,0,1,127,0,0,1,0,0));out.flush()
  while(true){val b=input.read();if(b<0)break;out.write(b);out.flush()}
 }}
 val port=ServerSocket(0).use{it.localPort}
 HttpProxyServer.start("127.0.0.1",socks.localPort,"127.0.0.1",port,"u","p").getOrThrow()
 fun connect(auth:String?):Socket=Socket("127.0.0.1",port).apply { soTimeout=2000;outputStream.write(("CONNECT example.org:443 HTTP/1.1\r\nHost: example.org\r\n"+(auth?.let{"Proxy-Authorization: Basic $it\r\n"}?:"")+"\r\n").toByteArray());outputStream.flush() }
 for (auth in listOf(null,java.util.Base64.getEncoder().encodeToString("bad:bad".toByteArray()))) connect(auth).use{check(it.inputStream.bufferedReader().readLine().contains("407"))}
 connect(java.util.Base64.getEncoder().encodeToString("u:p".toByteArray())).use{c->
  val reader=c.inputStream.bufferedReader();check(reader.readLine().contains("200"));while(!reader.readLine().isNullOrEmpty()){}
  c.outputStream.write(65);c.outputStream.flush();check(c.inputStream.read()==65)
  val handlers=Thread.getAllStackTraces().keys.filter{it.name=="http-proxy-handler"}
  HttpProxyServer.stop();handlers.forEach{it.join(2000)}
  check(handlers.none{it.isAlive}) { "Idle proxy handler leaked after stop" }
  check(c.inputStream.read()==-1)
 };socks.close()
 println("PASS real HTTP proxy: unauthenticated/wrong password rejected, authenticated relay, idle sockets/threads closed on stop")
 val range=DomainRouter.parseCidr("127.0.0.0/8")!!
 val router=DomainRouter(true,DomainRoutingMode.BYPASS,setOf("example.ir"),true,GeoBypassData(longArrayOf(range.first),longArrayOf(range.second),setOf(".ir")))
 check(router.shouldBypassDestination("127.0.0.1",null))
 check(router.shouldBypassDestination("203.0.113.1","bank.ir"))
 check(!router.shouldBypassDestination("203.0.113.1","example.com"))
 check(router.coreRules().toString().contains("127.0.0.0/8"))
 val echo=ServerSocket(0);thread(isDaemon=true){echo.accept().use { s->s.outputStream.write(s.inputStream.read());s.outputStream.flush() }}
 val local=ServerSocket(0)
 val relay=thread(isDaemon=true){local.accept().use{client->check(DirectRouting.relay(router,"127.0.0.1",echo.localPort,client))}}
 Socket("127.0.0.1",local.localPort).use{c->c.soTimeout=2000;val input=DataInputStream(c.inputStream);val reply=ByteArray(10);input.readFully(reply);check(reply[1].toInt()==0);c.outputStream.write(66);c.outputStream.flush();check(input.read()==66)}
 relay.join(2000);check(!relay.isAlive);local.close();echo.close()
 println("PASS domestic CIDR/domain decisions, generated core routing and shared direct relay")
 System.getenv("XRAY")?.let { xray ->
  val proxyPort=ServerSocket(0).use{it.localPort}
  val cfg=java.nio.file.Files.createTempFile("rovunet-routing-", ".json").toFile()
  val rules=router.coreRules();check(rules.toString().contains("domain:ir") && !rules.toString().contains("domain:.ir"))
  cfg.writeText(app.slipnet.core.CoreLink.runtime("vless://00000000-0000-4000-8000-000000000001@127.0.0.1:9?security=none&type=tcp",proxyPort,routing=rules))
  val core=ProcessBuilder(xray,"run","-c",cfg.absolutePath).redirectErrorStream(true).start()
  try {
   val until=System.nanoTime()+TimeUnit.SECONDS.toNanos(5)
   while(runCatching{Socket("127.0.0.1",proxyPort).close()}.isFailure) { check(core.isAlive && System.nanoTime()<until);Thread.yield() }
   val server=ServerSocket(0);thread(isDaemon=true){server.accept().use{s->s.outputStream.write(s.inputStream.read());s.outputStream.flush()}}
   Socket("127.0.0.1",proxyPort).use{c->c.soTimeout=3000;val input=DataInputStream(c.inputStream);val out=c.outputStream
    out.write(byteArrayOf(5,1,0));out.flush();check(input.readUnsignedShort()==0x0500)
    out.write(byteArrayOf(5,1,0,1,127,0,0,1,(server.localPort shr 8).toByte(),server.localPort.toByte()));out.flush()
    val response=ByteArray(10);input.readFully(response);check(response[1].toInt()==0)
    out.write(67);out.flush();check(input.read()==67)
   };server.close()
  } finally { core.destroy();if(!core.waitFor(3,TimeUnit.SECONDS))core.destroyForcibly();cfg.delete() }
  println("PASS generated routing on real Xray: loopback CIDR uses direct outbound while proxy endpoint is unreachable")
 }
}
