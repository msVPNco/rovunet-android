using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Rovunet.Agent;
using Rovunet.Core;
using Rovunet.Platform;

int checks=0;var before=new WinInetProxy().Read();
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(75));
if(args is ["--tor-online"]){
    using var runtime=new Runtime(probeOnly:true);using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(150));bool connected=false,verified=false;string error="";var clock=Stopwatch.StartNew();
    try{var ready=await runtime.Connect(Importer.Single(AlternativeProfiles.Create("snowflake","Tor network check","")),false,budget.Token);connected=ready.State=="connected";if(connected){var probe=await runtime.Probe("https://example.com/",budget.Token);verified=probe.Verified;error=probe.Error;}}
    catch(OperationCanceledException){error="NETWORK_CHECK_CANCELLED_AT_150_SECONDS";}catch(SafeError e){error=e.Code;}
    finally{runtime.Stop();}
    Console.WriteLine(JsonSerializer.Serialize(new{suite="tor-external-network",connected,verified,error,elapsedMs=clock.ElapsedMilliseconds,systemProxyUnchanged=JsonSerializer.Serialize(before)==JsonSerializer.Serialize(new WinInetProxy().Read())}));return;
}
if(args is ["--ssh-chain"]){
    using var input=JsonDocument.Parse(await Console.In.ReadToEndAsync());var root=input.RootElement;var endpoint=IPEndPoint.Parse(root.GetProperty("address").GetString()!);string pin=root.GetProperty("hostKey").GetString()!;
    await using var outer=new FixtureServer(true);await using var target=new FixtureServer(false);using var runtime=new Runtime(probeOnly:true);
    var ssh=Importer.Single(ManualProfiles.Native("ssh","SSH fixture",endpoint.Address.ToString(),endpoint.Port,"fixture","private-fixture-only"));
    var chain=Importer.Single(ChainProfiles.Create("SOCKS SSH",new[]{Importer.Single("socks5://127.0.0.1:"+outer.Port+"#Outer"),ssh}));
    var trust=await runtime.Connect(chain,false,deadline.Token);Check(trust.State=="trust"&&trust.ChainLayerIndex==1&&trust.Error=="SSH_TRUST_REQUIRED","SSH identity approval names exact chain layer");Check(runtime.Status().State=="disconnected","untrusted chain tears down outer stage");
    var ready=await runtime.Connect(chain,false,deadline.Token,chainIdentities:new List<ChainIdentity>{new(),new(){HostKey=pin}});
    using var client=new HttpClient(new SocketsHttpHandler{UseProxy=true,Proxy=new WebProxy("http://127.0.0.1:"+ready.HttpPort)}){Timeout=TimeSpan.FromSeconds(8)};
    Check(await client.GetStringAsync("http://127.0.0.1:"+target.Port,deadline.Token)=="protocol25-real-payload","encrypted SSH carries real payload via selected outer layer");Check(outer.Count>=2,"outer layer carries SSH handshakes");runtime.Stop();
    byte[] changed=Convert.FromBase64String(pin);changed[^1]^=1;
    var rejected=await runtime.Connect(chain,false,deadline.Token,chainIdentities:new List<ChainIdentity>{new(),new(){HostKey=Convert.ToBase64String(changed)}});
    Check(rejected.State=="trust"&&rejected.Error=="SSH_KEY_CHANGED"&&rejected.ChainLayerIndex==1&&runtime.Status().State=="disconnected","changed SSH identity is refused inside chain");
}else if(args is ["--protocol"]){
    using var input=JsonDocument.Parse(await Console.In.ReadToEndAsync());var root=input.RootElement;
    string raw=root.GetProperty("raw").GetString()!,url=root.GetProperty("url").GetString()!;bool expect=root.GetProperty("expect").GetBoolean();
    var p=Importer.Single(raw);using var runtime=new Runtime(probeOnly:true);var ready=await runtime.Connect(p,false,deadline.Token);
    Check(ready.State=="connected"&&!ready.Verified,"listener readiness is not traffic verification");
    bool received=false;try{using var client=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+ready.HttpPort),UseProxy=true,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(4)}){Timeout=TimeSpan.FromSeconds(8)};received=await client.GetStringAsync(url,deadline.Token)=="protocol25-real-payload";}catch(HttpRequestException){}catch(TaskCanceledException){}
    Check(received==expect,expect?"Agent -> Xray -> authenticated helper -> encrypted peer payload":"untrusted TLS is refused by real helper");
    if(expect){var stats=await runtime.Telemetry(deadline.Token);Check(stats.TrafficAvailable&&stats.UploadedBytes>0&&stats.DownloadedBytes>0,"helper traffic counted");}
    runtime.Stop();foreach(int port in new[]{ready.HttpPort,ready.SocksPort}){var l=new TcpListener(IPAddress.Loopback,port);l.Start();l.Stop();checks++;}
}else{
    await using var sink=new FixtureServer(false);await using var proxy=new FixtureServer(true);
    using var runtime=new Runtime(probeOnly:true);
    foreach(int count in new[]{2,4}){
        var layers=Enumerable.Range(0,count).Select(n=>Importer.Single("socks5://127.0.0.1:"+proxy.Port+"#Layer"+n)).ToArray();
        var p=Importer.Single(ChainProfiles.Create("Synthetic chain",layers));int accepted=proxy.Count;
                int Reserve(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();int value=((IPEndPoint)l.LocalEndpoint).Port;l.Stop();return value;}
        int wantedHttp=count==2?Reserve():0,wantedSocks=count==2?Reserve():0;while(count==2&&wantedSocks==wantedHttp)wantedSocks=Reserve();
        var ready=await runtime.Connect(p,false,deadline.Token,localHttpPort:wantedHttp,localSocksPort:wantedSocks);Check(ready.State=="connected"&&!ready.Verified,"chain ready unverified");
        Check(count!=2||ready.HttpPort==wantedHttp&&ready.SocksPort==wantedSocks,"chain keeps requested final ports");
        using var client=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+ready.HttpPort),UseProxy=true,AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(8)};
        Check(await client.GetStringAsync("http://127.0.0.1:"+sink.Port,deadline.Token)=="protocol25-real-payload","chain reaches payload sink");
        Check(proxy.Count-accepted==count,"each selected SOCKS layer consumed exactly once");
        var stats=await runtime.Telemetry(deadline.Token);Check(stats.TrafficAvailable&&stats.UploadedBytes>0&&stats.DownloadedBytes>0,"chain counters count payload once");
        runtime.Stop();Check(runtime.Status().State=="disconnected","chain stops");foreach(int port in new[]{ready.HttpPort,ready.SocksPort}){var l=new TcpListener(IPAddress.Loopback,port);l.Start();l.Stop();checks++;}
                if(count==2){var busyPort=new TcpListener(IPAddress.Loopback,wantedSocks);busyPort.Start();int prior=proxy.Count;try{await runtime.Connect(p,false,deadline.Token,localHttpPort:wantedHttp,localSocksPort:wantedSocks);throw new Exception("occupied final chain port accepted");}catch(SafeError e)when(e.Code=="LOCAL_PROXY_PORT_BUSY"){Check(proxy.Count==prior,"occupied final port fails before upstream traffic");}finally{busyPort.Stop();}}
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{await runtime.Connect(p,false,cancelled.Token);throw new Exception("chain cancellation ignored");}catch(OperationCanceledException){checks++;}
    }
    var unavailable=new TcpListener(IPAddress.Loopback,0);unavailable.Start();int closed=((IPEndPoint)unavailable.LocalEndpoint).Port;unavailable.Stop();
    var broken=Importer.Single(ChainProfiles.Create("Broken chain",new[]{Importer.Single("socks5://127.0.0.1:"+closed+"#Closed"),Importer.Single("socks5://127.0.0.1:"+proxy.Port+"#Inner")}));
    var brokenReady=await runtime.Connect(broken,false,deadline.Token);int hits=sink.Count;bool failed=false;
    try{using var client=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+brokenReady.HttpPort),UseProxy=true}){Timeout=TimeSpan.FromSeconds(5)};await client.GetStringAsync("http://127.0.0.1:"+sink.Port,deadline.Token);}catch(HttpRequestException){failed=true;}catch(TaskCanceledException){failed=true;}
    Check(failed&&sink.Count==hits,"broken outer chain never falls back to direct");runtime.Stop();
    string torRoot=Runtime.VerifiedTorRoot25();Check(File.Exists(Path.Combine(torRoot,"tor","tor.exe")),"all Tor payload hashes checked");
    string folder=Path.Combine(AppContext.BaseDirectory,"tor-verification-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
    try{
        var tor=Importer.Single(AlternativeProfiles.Create("snowflake","Tor fixture",""));string config=Runtime.TorConfiguration25(tor,closed,torRoot,folder);
        var start=new ProcessStartInfo(Path.Combine(torRoot,"tor","tor.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in new[]{"--verify-config","-f","-"})start.ArgumentList.Add(arg);
        using var child=Process.Start(start)!;var output=child.StandardOutput.ReadToEndAsync();var errors=child.StandardError.ReadToEndAsync();await child.StandardInput.WriteAsync(config);child.StandardInput.Close();await child.WaitForExitAsync(deadline.Token);await Task.WhenAll(output,errors);Check(child.ExitCode==0,"real Tor validates generated configuration");
        using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(3));var watch=Stopwatch.StartNew();
        try{await runtime.Connect(tor,false,cancel.Token);runtime.Stop();}catch(OperationCanceledException){checks++;}catch(SafeError error)when(error.Code is "TOR_BOOTSTRAP_FAILED" or "TOR_BOOTSTRAP_TIMEOUT"){checks++;}
        Check(watch.Elapsed<TimeSpan.FromSeconds(10)&&runtime.Status().State=="disconnected","Tor bootstrap cancellation cleans processes promptly");
    }finally{Directory.Delete(folder,true);}
}
Check(JsonSerializer.Serialize(before)==JsonSerializer.Serialize(new WinInetProxy().Read()),"runtime fixtures leave system proxy unchanged");
Console.WriteLine(JsonSerializer.Serialize(new{suite="protocol25-runtime",passed=checks}));

sealed class FixtureServer: IAsyncDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);readonly CancellationTokenSource stop=new();readonly List<TcpClient> clients=[];readonly Task serving;int count;
    public int Port{get;}public int Count=>Volatile.Read(ref count);
    public FixtureServer(bool socks){listener.Start();Port=((IPEndPoint)listener.LocalEndpoint).Port;serving=Serve(socks);}
    async Task Serve(bool socks){try{while(!stop.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync(stop.Token);lock(clients)clients.Add(client);_=Handle(client,socks);}}catch(OperationCanceledException){}catch(SocketException){}}
    async Task Handle(TcpClient client,bool socks){try{
        using(client){var stream=client.GetStream();using var budget=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);budget.CancelAfter(TimeSpan.FromSeconds(15));var ct=budget.Token;
            if(socks){byte[] header=new byte[2];await stream.ReadExactlyAsync(header,ct);if(header[0]!=5)return;byte[] methods=new byte[header[1]];await stream.ReadExactlyAsync(methods,ct);if(!methods.Contains((byte)0))return;await stream.WriteAsync(new byte[]{5,0},ct);
                byte[] request=new byte[4];await stream.ReadExactlyAsync(request,ct);if(request[0]!=5||request[1]!=1)return;string host;
                if(request[3] is 1 or 4){byte[] ip=new byte[request[3]==1?4:16];await stream.ReadExactlyAsync(ip,ct);host=new IPAddress(ip).ToString();}else if(request[3]==3){byte[] size=new byte[1];await stream.ReadExactlyAsync(size,ct);byte[] name=new byte[size[0]];await stream.ReadExactlyAsync(name,ct);host=Encoding.ASCII.GetString(name);}else return;
                byte[] port=new byte[2];await stream.ReadExactlyAsync(port,ct);using var peer=new TcpClient();lock(clients)clients.Add(peer);await peer.ConnectAsync(host,(port[0]<<8)|port[1],ct);Interlocked.Increment(ref count);await stream.WriteAsync(new byte[]{5,0,0,1,127,0,0,1,0,0},ct);
                using var tunnel=peer.GetStream();await Task.WhenAny(stream.CopyToAsync(tunnel,ct),tunnel.CopyToAsync(stream,ct));
            }else{var buffer=new byte[4096];int read=await stream.ReadAsync(buffer,ct);if(read==0)return;Interlocked.Increment(ref count);const string body="protocol25-real-payload";byte[] reply=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n"+body);await stream.WriteAsync(reply,ct);}
        }
    }catch(Exception error)when(error is IOException or SocketException or OperationCanceledException or ObjectDisposedException){} }
    public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();lock(clients)foreach(var c in clients)c.Dispose();await serving;stop.Dispose();}
}
