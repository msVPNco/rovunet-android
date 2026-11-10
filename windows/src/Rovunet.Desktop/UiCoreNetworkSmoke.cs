using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunCoreNetworkSmoke(string report)
    {
        var checks=new Dictionary<string,bool>();bool networkAttempted=false;var before=new WinInetProxy().Read();string engine=Path.Combine(AppContext.BaseDirectory,"engines","xray.exe");
        using var lifetime=new CancellationTokenSource(TimeSpan.FromMinutes(3));using var rsa=RSA.Create(2048);
        var request=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");request.CertificateExtensions.Add(san.Build());request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var generated=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));using var certificate=X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null,X509KeyStorageFlags.UserKeySet);
        string[] pem=certificate.ExportCertificatePem().Split('\n'),privatePem=rsa.ExportPkcs8PrivateKeyPem().Split('\n');var tls=Obj(("certificates",new[]{Obj(("certificate",pem),("key",privatePem))}),("alpn",new[]{"h2","http/1.1"}));
        using var decoy=new TcpListener(IPAddress.Loopback,0);decoy.Start();int decoyPort=((IPEndPoint)decoy.LocalEndpoint).Port;
        var decoyConnections=new List<Task>();var accept=Task.Run(async()=>{try{while(!lifetime.IsCancellationRequested){var socket=await decoy.AcceptTcpClientAsync(lifetime.Token);decoyConnections.Add(Task.Run(async()=>{using(socket)using(var stream=new SslStream(socket.GetStream()))try{await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls13,ApplicationProtocols=[SslApplicationProtocol.Http2,SslApplicationProtocol.Http11]},lifetime.Token);await stream.ReadExactlyAsync(new byte[1],lifetime.Token);}catch{}}));}}catch(OperationCanceledException){}catch(SocketException){}});
        try{
            using(var binary=File.OpenRead(engine))checks["pinned_xray_verified"]=Convert.ToHexString(await SHA256.HashDataAsync(binary)).Equals("d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e",StringComparison.OrdinalIgnoreCase);
            if(!checks["pinned_xray_verified"])throw new SafeError("ENGINE_HASH_MISMATCH");
            var reality=await Pair();var serverWg=await Pair();var clientWg=await Pair();string uuid="00000000-0000-4000-8000-000000000002";
            foreach(string kind in new[]{"ws","reality","xhttp","hysteria2","wireguard"}){
                Process? server=null,clientProcess=null;using var jobs=new ProcessJob();
                try{
                    int serverPort=Free(kind is "hysteria2" or "wireguard"),http=Free(false),socks=Free(false);
                    var stream=Obj(("network",kind=="ws"?"ws":kind=="xhttp"?"xhttp":"raw"),("security","none"));var settings=Obj(("clients",new[]{Obj(("id",uuid))}),("decryption","none"));
                    var edits=new Dictionary<string,string>{{"server","127.0.0.1"},{"port",serverPort.ToString()},{"auth",uuid},{"type",kind=="ws"?"ws":kind=="xhttp"?"xhttp":"tcp"},{"security","none"},{"host","localhost"},{"path","/edited"}};
                    string raw="vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=ws&security=tls&path=%2Fold#Original";
                    if(kind=="ws")stream["wsSettings"]=Obj(("path","/edited"));
                    if(kind=="reality"){
                        stream["security"]="reality";stream["realitySettings"]=Obj(("target","127.0.0.1:"+decoyPort),("serverNames",new[]{"localhost"}),("privateKey",reality.Private),("shortIds",new[]{"aabb"}));settings["clients"]=JsonSerializer.SerializeToNode(new[]{Obj(("id",uuid),("flow","xtls-rprx-vision"))});edits["security"]="reality";edits["pbk"]=reality.Public;edits["sid"]="aabb";edits["sni"]="localhost";edits["fp"]="chrome";edits["flow"]="xtls-rprx-vision";
                    }
                    if(kind=="xhttp"){stream["security"]="tls";stream["tlsSettings"]=tls.DeepClone();stream["xhttpSettings"]=Obj(("path","/edited"),("mode","auto"));edits["security"]="tls";edits["sni"]="localhost";edits["mode"]="packet-up";edits["extra"]="{\"noSSEHeader\":true}";}
                    var inbound=Obj(("listen","127.0.0.1"),("port",serverPort),("protocol","vless"),("settings",settings),("streamSettings",stream));
                    if(kind=="hysteria2"){
                        raw="hysteria2://original@gateway.example.invalid:443?sni=old.example.invalid#H2";edits=new(){{"server","127.0.0.1"},{"port",serverPort.ToString()},{"auth","synthetic:new-password"},{"sni","localhost"},{"obfs","salamander"},{"obfs-password","synthetic-new-obfs"}};
                        inbound["protocol"]="hysteria";inbound["settings"]=Obj(("version",2),("users",new[]{Obj(("auth",edits["auth"]))}));var hyTls=tls.DeepClone().AsObject();hyTls["alpn"]=JsonSerializer.SerializeToNode(new[]{"h3"});inbound["streamSettings"]=Obj(("network","hysteria"),("security","tls"),("tlsSettings",hyTls),("hysteriaSettings",Obj(("version",2))),("finalmask",Obj(("udp",new[]{Obj(("type","salamander"),("settings",Obj(("password",edits["obfs-password"]))))}))));
                    }
                    if(kind=="wireguard"){
                        string Std(string value)=>Convert.ToBase64String(Convert.FromBase64String(value.Replace('-','+').Replace('_','/').PadRight((value.Length+3)/4*4,'=')));string psk=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                        raw=$"[Interface]\nPrivateKey={Std(serverWg.Private)}\nAddress=10.66.0.9/32\n[Peer]\nPublicKey={Std(clientWg.Public)}\nEndpoint=gateway.example.invalid:51820\nAllowedIPs=0.0.0.0/0";
                        edits=new(){{"privatekey",Std(clientWg.Private)},{"publickey",Std(serverWg.Public)},{"endpoint","127.0.0.1:"+serverPort},{"address","10.66.0.2/32"},{"allowedips","0.0.0.0/0"},{"dns","1.1.1.1"},{"mtu","1420"},{"persistentkeepalive","25"},{"presharedkey",psk}};
                        inbound.Remove("streamSettings");inbound["protocol"]="wireguard";inbound["settings"]=Obj(("secretKey",Std(serverWg.Private)),("address",new[]{"10.66.0.1/32"}),("peers",new[]{Obj(("publicKey",Std(clientWg.Public)),("preSharedKey",psk),("allowedIPs",new[]{"10.66.0.2/32"}))}),("noKernelTun",true),("mtu",1420));
                    }
                    var serverConfig=Obj(("log",Obj(("loglevel","none"))),("inbounds",new[]{inbound}),("outbounds",new[]{Obj(("protocol","freedom"))}));server=await Start(serverConfig,jobs);
                    var original=Importer.Single(raw);var edited=CoreProfileEditing.Apply(original,"Edited network fixture",edits).Profile;var config=JsonNode.Parse(CoreLinks.Runtime(edited,http,socks))!.AsObject();checks[kind+"_original_unchanged"]=original.Raw==raw;
                    if(kind is "xhttp" or "hysteria2")config["outbounds"]![0]!["streamSettings"]!["tlsSettings"]!["certificates"]=JsonSerializer.SerializeToNode(new[]{Obj(("certificate",pem),("usage","verify"))});
                    checks[kind+"_no_direct_fallback"]=config["outbounds"]!.AsArray().Count==1;clientProcess=await Start(config,jobs);await Wait(http,clientProcess,lifetime.Token);
                    using var handler=new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+http),UseProxy=true,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(12)};using var web=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(18)};networkAttempted=true;using var response=await web.GetAsync("https://example.com/",HttpCompletionOption.ResponseHeadersRead,lifetime.Token);var bytes=await response.Content.ReadAsByteArrayAsync(lifetime.Token);checks[kind+"_edited_profile_public_https"]=response.IsSuccessStatusCode&&bytes.Length>0;
                }catch(Exception e){checks[kind+"_failure_"+e.GetType().Name+(e is SafeError safe?"_"+safe.Code:"")]=false;}
                finally{foreach(var owned in new[]{clientProcess,server})if(owned!=null){try{if(!owned.HasExited)owned.Kill(true);await owned.WaitForExitAsync();}finally{owned.Dispose();}}}
            }
            checks["proxy_unchanged"]=before==new WinInetProxy().Read();
        }catch(Exception e){checks["failure_"+e.GetType().Name]=false;}
        finally{lifetime.Cancel();decoy.Stop();await accept;await Task.WhenAll(decoyConnections);}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="Production editor -> pinned Xray local protocol server -> public HTTPS",external_network_tested=networkAttempted,target="https://example.com/",ephemeral_fixture_certificate_only=true,system_certificate_store_modified=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
        async Task<Process> Start(JsonObject config,ProcessJob job){var info=new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};info.ArgumentList.Add("run");info.ArgumentList.Add("-c");info.ArgumentList.Add("stdin:");var process=Process.Start(info)??throw new SafeError("CORE_START_FAILED");job.Assign(process);_=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);_=process.StandardError.BaseStream.CopyToAsync(Stream.Null);await process.StandardInput.WriteAsync(config.ToJsonString());process.StandardInput.Close();return process;}
        async Task<(string Private,string Public)> Pair(){using var process=Process.Start(new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"x25519"}})!;string text=await process.StandardOutput.ReadToEndAsync();_=await process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();var lines=text.Split('\n').Where(l=>l.Contains(':')).Select(l=>l.Split(':',2)).ToDictionary(l=>l[0].Trim(),l=>l[1].Trim());return(lines["PrivateKey"],lines.GetValueOrDefault("PublicKey",lines.GetValueOrDefault("Password (PublicKey)",lines.GetValueOrDefault("Password",""))));}
        static JsonObject Obj(params (string Key,object? Value)[] fields){var node=new JsonObject();foreach(var (key,value)in fields)node[key]=value is JsonNode j?j.DeepClone():JsonSerializer.SerializeToNode(value);return node;}
        static int Free(bool udp){if(udp){using var socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));return((IPEndPoint)socket.Client.LocalEndPoint!).Port;}using var tcp=new TcpListener(IPAddress.Loopback,0);tcp.Start();return((IPEndPoint)tcp.LocalEndpoint).Port;}
        static async Task Wait(int port,Process process,CancellationToken ct){var until=DateTime.UtcNow.AddSeconds(8);while(DateTime.UtcNow<until){if(process.HasExited)throw new SafeError("CORE_START_FAILED");using var socket=new TcpClient();try{await socket.ConnectAsync(IPAddress.Loopback,port,ct);return;}catch(SocketException){await Task.Delay(40,ct);}}throw new SafeError("CORE_START_FAILED");}
    }
}
