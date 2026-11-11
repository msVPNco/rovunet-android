using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;
using Runtime=Rovunet.Agent.Runtime;

EnginePaths.Root=Path.GetFullPath(args[0]);string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
int checks=0,failures=0;var rows=new List<object>();
void Check(bool passed,string name){checks++;if(!passed){failures++;Console.WriteLine("FAIL "+name);}rows.Add(new{name,passed});}
int Port(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();int p=((IPEndPoint)l.LocalEndpoint).Port;l.Stop();return p;}
async Task Respond(TcpListener listener,CancellationToken ct,bool tls=false)
{
    using var rsa=RSA.Create(2048);var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);using var cert=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
    try{while(!ct.IsCancellationRequested){var socket=await listener.AcceptTcpClientAsync(ct);_=Task.Run(async()=>{using(socket){try{Stream stream=socket.GetStream();if(tls){var ssl=new SslStream(stream,false);await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=cert},ct);stream=ssl;}byte[] buffer=new byte[2048];_=await stream.ReadAsync(buffer,ct);byte[] body="Rovunet fixture data"u8.ToArray();byte[] headers=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");await stream.WriteAsync(headers,ct);await stream.WriteAsync(body,ct);await stream.FlushAsync(ct);}catch{}}},ct);}}catch(OperationCanceledException){}
}
using var lifetime=new CancellationTokenSource();var httpServer=new TcpListener(IPAddress.Loopback,0);httpServer.Start();int httpPort=((IPEndPoint)httpServer.LocalEndpoint).Port;_=Respond(httpServer,lifetime.Token);
var tlsServer=new TcpListener(IPAddress.Loopback,0);tlsServer.Start();int tlsPort=((IPEndPoint)tlsServer.LocalEndpoint).Port;_=Respond(tlsServer,lifetime.Token,true);
int inbound=Port();string id="00000000-0000-4000-8000-000000000001";
using var server=Process.Start(new ProcessStartInfo(EnginePaths.Require("xray")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-c","stdin:"}})!;
var serverLog=new System.Collections.Concurrent.ConcurrentQueue<string>();
async Task Observe(StreamReader reader){while(await reader.ReadLineAsync() is {} line)serverLog.Enqueue(line.Replace(id,"[fixture-id]"));}
_=Observe(server.StandardOutput);_=Observe(server.StandardError);
await server.StandardInput.WriteAsync(JsonSerializer.Serialize(new{log=new{loglevel="warning"},inbounds=new[]{new{listen="127.0.0.1",port=inbound,protocol="vless",settings=new{clients=new[]{new{id}},decryption="none"}}},outbounds=new[]{new{protocol="freedom",settings=new{finalRules=new[]{new{action="allow",ip=new[]{"127.0.0.1/32","::1/128"}}}}}}}));server.StandardInput.Close();
var profile=Importer.Single($"vless://{id}@127.0.0.1:{inbound}?type=tcp&security=none#Loopback");
using var runtime=new Runtime(true);var stages=new List<string>();runtime.StageChanged+=s=>stages.Add(s.State);
try {
    await Task.Delay(200);
    using(var direct=new HttpClient(new SocketsHttpHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(3)})Check(await direct.GetStringAsync("http://127.0.0.1:"+httpPort+"/")=="Rovunet fixture data","controlled-http-fixture-ready");
    var started=await runtime.Connect(profile,false,CancellationToken.None);Check(started.State=="connected"&&!started.Verified,"manual-connect-without-probe");Check(stages.Contains("engine-started"),"engine-start-distinct-from-ready");
    using var client=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+started.HttpPort),UseProxy=true,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8)};
    Check(await client.GetStringAsync("http://127.0.0.1:"+httpPort+"/")=="Rovunet fixture data","http-body-through-real-vless");
    using var socks=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("socks5://127.0.0.1:"+started.SocksPort),UseProxy=true,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8)};
    Check(await socks.GetStringAsync("http://127.0.0.1:"+httpPort+"/")=="Rovunet fixture data","socks-body-through-real-vless");
    var rejected=await runtime.Probe("https://127.0.0.1:"+tlsPort+"/",CancellationToken.None);Check(!rejected.Verified&&rejected.Error=="PROBE_TLS_FAILED","invalid-tls-never-accepted");Check(runtime.Status().State=="connected","failed-probe-keeps-manual-connection");
    var traffic=await runtime.Telemetry(CancellationToken.None);Check(traffic.TrafficAvailable&&traffic.UploadedBytes>0&&traffic.DownloadedBytes>0,"actual-xray-traffic-counters");
    if(Socket.OSSupportsIPv6){var ipv6=new TcpListener(IPAddress.IPv6Loopback,0);ipv6.Start();_=Respond(ipv6,lifetime.Token);int port6=((IPEndPoint)ipv6.LocalEndpoint).Port;Check(await socks.GetStringAsync("http://[::1]:"+port6+"/")=="Rovunet fixture data","ipv6-http-through-vless");ipv6.Stop();}
    using(var control=new TcpClient()){
        await control.ConnectAsync(IPAddress.Loopback,started.SocksPort);var stream=control.GetStream();await stream.WriteAsync(new byte[]{5,1,0});byte[] hello=new byte[2];await stream.ReadExactlyAsync(hello);await stream.WriteAsync(new byte[]{5,3,0,1,127,0,0,1,0,0});byte[] head=new byte[4];await stream.ReadExactlyAsync(head);Check(head[1]!=0,"udp-associate-explicitly-unavailable-like-windows");
    }
    int oldPort=started.HttpPort;runtime.Stop();Check(runtime.Status().State=="disconnected","clean-disconnect");using(var check=new TcpClient()){bool closed=false;try{await check.ConnectAsync(IPAddress.Loopback,oldPort);}catch(SocketException){closed=true;}Check(closed,"listeners-close-after-disconnect");}
    started=await runtime.Connect(profile,false,CancellationToken.None);Check(started.State=="connected","reconnect-after-disconnect");
    var core=(Process)typeof(Runtime).GetField("core",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(runtime)!;core.Kill(true);await core.WaitForExitAsync();Check(runtime.Status().Error=="CORE_EXITED","unexpected-engine-exit-is-error");
    try{await runtime.Connect(profile,true,CancellationToken.None);Check(false,"native-mac-proxy-not-run-by-windows-fixture");}catch(SafeError e){Check(e.Code=="MACOS_REQUIRED","native-mac-proxy-not-run-by-windows-fixture");}
    runtime.Stop();
    foreach(string method in new[]{"2022-blake3-aes-128-gcm","2022-blake3-aes-256-gcm","2022-blake3-chacha20-poly1305"}){
        int ssPort=Port();string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(method.Contains("128")?16:32));
        using var ssServer=Process.Start(new ProcessStartInfo(EnginePaths.Require("xray")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-c","stdin:"}})!;
        _=Observe(ssServer.StandardOutput);_=Observe(ssServer.StandardError);
        try{
            await ssServer.StandardInput.WriteAsync(JsonSerializer.Serialize(new{log=new{loglevel="warning"},inbounds=new[]{new{listen="127.0.0.1",port=ssPort,protocol="shadowsocks",settings=new{method,password,network="tcp"}}},outbounds=new[]{new{protocol="freedom",settings=new{finalRules=new[]{new{action="allow",ip=new[]{"127.0.0.1/32"}}}}}}}));ssServer.StandardInput.Close();
            using var waiting=new CancellationTokenSource(TimeSpan.FromSeconds(5));while(true){using var socket=new TcpClient();try{await socket.ConnectAsync(IPAddress.Loopback,ssPort,waiting.Token);break;}catch(SocketException){await Task.Delay(30,waiting.Token);}}
            string raw="ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(method+":"+password))+"@127.0.0.1:"+ssPort+"#Loopback";var ssProfile=MacProtocols.Single(raw);var ssReady=await runtime.Connect(ssProfile,false,lifetime.Token);
            using var ssClient=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+ssReady.HttpPort),UseProxy=true}){Timeout=TimeSpan.FromSeconds(8)};
            Check(await ssClient.GetStringAsync("http://127.0.0.1:"+httpPort+"/")=="Rovunet fixture data","encrypted-ss2022-http-data-"+method);
            using var ssSocks=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("socks5://127.0.0.1:"+ssReady.SocksPort),UseProxy=true}){Timeout=TimeSpan.FromSeconds(8)};
            Check(await ssSocks.GetStringAsync("http://127.0.0.1:"+httpPort+"/")=="Rovunet fixture data","encrypted-ss2022-socks-data-"+method);
        }finally{runtime.Stop();EngineProcess.Terminate(ssServer);}
    }
}catch(Exception e){failures++;Console.WriteLine("Network fixture: "+e.GetType().Name+" "+(e is SafeError se?se.Code:e.Message));foreach(string line in serverLog.TakeLast(12))Console.WriteLine(line);}
finally{runtime.Stop();lifetime.Cancel();httpServer.Stop();tlsServer.Stop();EngineProcess.Terminate(server);}
File.WriteAllText(Path.Combine(output,"network-tests.json"),JsonSerializer.Serialize(new{host="Windows x64 / macOS C# source with Windows fixture engines",darwinRuntimeValidated=false,controlledLoopbackOnly=true,checks,failures,results=rows},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Network: {checks} checks, {failures} failures");return failures==0?0:1;
