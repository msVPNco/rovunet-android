using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Rovunet.Agent;
using Rovunet.Core;
using Rovunet.Platform;

// Only synthetic fixture input on stdin. Never uses the owner's store or agent.
string raw=await Console.In.ReadToEndAsync();
var before=new WinInetProxy().Read();int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
using var runtime=new Runtime(probeOnly:true);
var profile=Importer.Single(raw);Check(profile.Protocol=="AmneziaWG","AWG parser");
var connected=await runtime.Connect(profile,false,deadline.Token);
Check(connected.State=="connected"&&!connected.Verified,"local ready distinct from HTTPS verification");
using(var client=new HttpClient(new SocketsHttpHandler{Proxy=new WebProxy("http://127.0.0.1:"+connected.HttpPort),UseProxy=true,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(10)})){
 string body=await client.GetStringAsync("http://10.77.0.1:8080",deadline.Token);
 Check(body=="awg-real-payload","Windows agent -> Xray -> authenticated AWG -> encrypted peer -> TCP server");
}
var stats=await runtime.Telemetry(deadline.Token);Check(stats.TrafficAvailable&&stats.UploadedBytes>0&&stats.DownloadedBytes>0,"real traffic counted");
var stopped=runtime.Stop();Check(stopped.State=="disconnected"&&runtime.Status().State=="disconnected","stop disconnects");
foreach(int port in new[]{connected.HttpPort,connected.SocksPort}){var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();listener.Stop();checks++;}
using var cancelled=new CancellationTokenSource();cancelled.Cancel();
try{await runtime.Connect(profile,false,cancelled.Token);throw new Exception("cancel ignored");}catch(OperationCanceledException){checks++;}
Check(runtime.Status().State=="disconnected","cancel cleans partial startup");
Check(JsonSerializer.Serialize(before)==JsonSerializer.Serialize(new WinInetProxy().Read()),"fixture leaves system proxy unchanged");
Console.WriteLine(JsonSerializer.Serialize(new{suite="windows-amnezia-agent-fixture",passed=checks}));
