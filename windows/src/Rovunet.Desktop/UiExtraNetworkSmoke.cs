using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunExtraNetworkSmoke(string report)
    {
        var checks=new Dictionary<string,bool>();var before=new WinInetProxy().Read();var samples=new Dictionary<string,object>();
        string engine=Path.Combine(AppContext.BaseDirectory,"engines","xray.exe");using var lifetime=new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try{
            checks["pinned_engine"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(engine))).Equals("d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e",StringComparison.OrdinalIgnoreCase);
            if(!checks["pinned_engine"])throw new SafeError("ENGINE_HASH_MISMATCH");
            foreach(var (kind,transport) in new[]{("vmess","tcp"),("vmess","ws"),("vmess","grpc"),("vmess","httpupgrade"),("trojan","tcp"),("trojan","ws"),("trojan","grpc"),("trojan","httpupgrade"),("ss","aes-128-gcm"),("ss","aes-256-gcm"),("ss","chacha20-ietf-poly1305")}){
                string label=kind+"_"+transport;using var job=new ProcessJob();Process? server=null;int localHttp=0;
                try{
                    var reservation=new TcpListener(IPAddress.Loopback,0);reservation.Start();int port=((IPEndPoint)reservation.LocalEndpoint).Port;reservation.Stop();
                    const string uuid="00000000-0000-4000-8000-000000000001";string raw;JsonObject settings;
                    var stream=new JsonObject{["network"]=kind=="ss"?"tcp":transport,["security"]="none"};
                    if(transport=="ws")stream["wsSettings"]=new JsonObject{["path"]="/fixture"};
                    if(transport=="grpc")stream["grpcSettings"]=new JsonObject{["serviceName"]="fixture"};
                    if(transport=="httpupgrade")stream["httpupgradeSettings"]=new JsonObject{["path"]="/fixture",["host"]="localhost"};
                    if(kind=="vmess"){
                        settings=new JsonObject{["clients"]=new JsonArray(new JsonObject{["id"]=uuid,["alterId"]=0})};
                        raw="vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{v="2",ps="VMess fixture",add="gateway.example.invalid",port="443",id=uuid,aid="0",scy="auto",net=transport,path=transport=="ws"||transport=="httpupgrade"?"/fixture":"",serviceName=transport=="grpc"?"fixture":"",host=transport=="httpupgrade"?"localhost":"",tls=""})));
                    }else if(kind=="trojan"){
                        settings=new JsonObject{["clients"]=new JsonArray(new JsonObject{["password"]="fixture-password"})};
                        raw="trojan://fixture-password@gateway.example.invalid:443?security=none&type="+transport+(transport is "ws" or "httpupgrade"?"&path=%2Ffixture&host=localhost":transport=="grpc"?"&serviceName=fixture":"");
                    }else{
                        settings=new JsonObject{["method"]=transport,["password"]="fixture-password",["network"]="tcp"};
                        raw="ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(transport+":fixture-password"))+"@gateway.example.invalid:443#SS";
                    }
                    var original=Importer.Single(raw);var edits=ExtraProfileEditing.Read(original)!.Fields.ToDictionary();edits["server"]="127.0.0.1";edits["port"]=port.ToString();var profile=ExtraProfileEditing.Apply(original,"Edited fixture",edits);
                    var config=new JsonObject{
                        ["log"]=new JsonObject{["loglevel"]="none"},
                        ["inbounds"]=new JsonArray(new JsonObject{["listen"]="127.0.0.1",["port"]=port,["protocol"]=kind=="ss"?"shadowsocks":kind,["settings"]=settings,["streamSettings"]=stream}),
                        ["outbounds"]=new JsonArray(new JsonObject{["tag"]="allowed",["protocol"]="freedom"},new JsonObject{["tag"]="blocked",["protocol"]="blackhole"}),
                        ["routing"]=new JsonObject{["domainStrategy"]="AsIs",["rules"]=new JsonArray(new JsonObject{["type"]="field",["domain"]=new JsonArray("full:example.com"),["outboundTag"]="allowed"},new JsonObject{["type"]="field",["network"]="tcp,udp",["outboundTag"]="blocked"})}
                    };
                    server=Process.Start(new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-c","stdin:"}})!;job.Assign(server);server.BeginOutputReadLine();server.BeginErrorReadLine();await server.StandardInput.WriteAsync(config.ToJsonString());server.StandardInput.Close();
                    using var ready=new CancellationTokenSource(TimeSpan.FromSeconds(8));while(true){using var socket=new TcpClient();try{await socket.ConnectAsync(IPAddress.Loopback,port,ready.Token);break;}catch(SocketException){if(server.HasExited)throw new SafeError("ENGINE_EXITED");await Task.Delay(30,ready.Token);}}
                    await using var agent=new AgentClient(probeOnly:true);await agent.Start(lifetime.Token);var connected=await agent.Send(new(){Action="connect",Profile=profile,SystemProxy=false});checks[label+"_edited_profile_connects"]=connected.State=="connected";
                    localHttp=connected.HttpPort;if(!checks[label+"_edited_profile_connects"])throw new SafeError(connected.Error.Length>0?connected.Error:"CONNECTION_FAILED");
                    var probe=await agent.Send(new(){Action="probe",ProbeUrl="https://example.com/",ProbeFallbacks=false});checks[label+"_public_https_validated"]=probe.Verified;
                    AgentReply traffic=new();var counterDeadline=Stopwatch.StartNew();do{await Task.Delay(150,lifetime.Token);traffic=await agent.Send(new(){Action="telemetry"});}while(counterDeadline.ElapsedMilliseconds<2000&&(!traffic.TrafficAvailable||traffic.DownloadedBytes==0||traffic.UploadedBytes==0));
                    checks[label+"_real_traffic_counted"]=traffic.TrafficAvailable&&traffic.DownloadedBytes>0&&traffic.UploadedBytes>0;
                    samples[label]=new{traffic.TrafficAvailable,traffic.UploadedBytes,traffic.DownloadedBytes,traffic.TelemetryError};
                    var stopped=await agent.Send(new(){Action="disconnect"});checks[label+"_disconnect"]=stopped.State=="disconnected";
                }catch(Exception error){checks[label+"_error_"+(error is SafeError safe?safe.Code:error.GetType().Name)]=false;}
                finally{if(server!=null){try{if(!server.HasExited)server.Kill(true);await server.WaitForExitAsync();}catch{}server.Dispose();}}
                if(localHttp>0){using var socket=new TcpClient();try{await socket.ConnectAsync(IPAddress.Loopback,localHttp).WaitAsync(TimeSpan.FromSeconds(1));checks[label+"_listener_closed"]=false;}catch{checks[label+"_listener_closed"]=true;}}
            }
            checks["owner_proxy_unchanged"]=before==new WinInetProxy().Read();
        }catch(Exception error){checks["failure_"+error.GetType().Name]=false;}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Edited VMess/Trojan/Shadowsocks through real Windows Xray/Agent",synthetic_loopback_servers=true,outer_transport_tls_tested=false,public_https_tls_successes=checks.Count(p=>p.Key.EndsWith("_public_https_validated")&&p.Value),samples,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
