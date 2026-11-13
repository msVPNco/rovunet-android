using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

int checks=0,failures=0;var results=new List<object>();void Check(bool pass,string name){checks++;if(!pass){failures++;Console.WriteLine("FAIL "+name);}results.Add(new{name,passed=pass});}
string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);EnginePaths.Root=Path.GetFullPath(args[0]);
using var resolver=new FixtureDns();using var silent=new FixtureDns(true);using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(35));
try{
    var plan=new ScanCheckpoint{Mode="advanced",Family="dnstt",Zone="t.example.invalid",Candidates=[$"127.0.0.1:{resolver.Port}","127.0.0.1:9"],Transports=["udp","tcp"],Workers=2,TimeoutMS=800,Count=1,Threshold=1};
    int notifications=0;await ScanSession.Run(plan,()=>{notifications++;return ValueTask.CompletedTask;},limit.Token);
    Check(plan.Results.Count==4&&notifications>=4,"actual-native-json-produces-all-four-results");
    Check(plan.Results.Where(r=>r.Index==0).All(r=>r.Dns&&r.Pass),"actual-tcp-and-udp-dns-replies-recognized");
    Check(plan.Results.Where(r=>r.Index==1).All(r=>!r.Pass),"dead-resolver-cannot-borrow-another-address-proof");
    Check(plan.Results.All(r=>!r.TunnelEstablished&&!r.HttpsVerified),"dns-response-does-not-manufacture-tunnel-or-https");
    int before=plan.Results.Count;await ScanSession.Run(plan,()=>ValueTask.CompletedTask,limit.Token);Check(plan.Results.Count==before,"completed-scan-resume-does-not-repeat-work");
    var resumed=JsonSerializer.Deserialize<ScanCheckpoint>(JsonSerializer.Serialize(plan))!;resumed.Results.RemoveAll(r=>r.Index==0&&r.Transport=="tcp");
    var earlier=resumed.Results.Single(r=>r.Index==0&&r.Transport=="udp");earlier.TunnelFinished=earlier.TunnelEstablished=true;earlier.TunnelElapsedMS=937;
    Check(earlier.Selectable&&earlier.TunnelEstablished&&!earlier.HttpsVerified,"authenticated-handshake-does-not-require-an-https-target");
    resumed.FullVerification=true;Check(!resumed.TunnelDone(earlier),"enabling-https-reopens-handshake-only-result");earlier.HttpsAttempted=true;
    Check(resumed.TunnelDone(earlier)&&earlier.Selectable&&!earlier.HttpsVerified,"failed-https-preserves-authenticated-server-route");earlier.HttpsVerified=true;resumed.Selected.Add("0:udp");await ScanSession.Run(resumed,()=>ValueTask.CompletedTask,limit.Token);Check(resumed.Results.Count==4&&resumed.Results.Count(r=>r.Index==0&&r.Transport=="tcp")==1,"saved-partial-scan-resumes-missing-address-transport-only");
    Check(ReferenceEquals(earlier,resumed.Results.Single(r=>r.Index==0&&r.Transport=="udp"))&&earlier.TunnelElapsedMS==937&&resumed.Selected.Contains("0:udp"),"resuming-tcp-preserves-completed-udp-tunnel-proof-and-selection");
    var tcp=resumed.Results.Single(r=>r.Index==0&&r.Transport=="tcp");Check(resumed.SelectionError(tcp)=="SCAN_SAME_TRANSPORT","mixed-wire-rejected-before-apply");
    resumed.Selected.Add("0:tcp");resumed.NormalizeSelection();Check(resumed.Selected.Count==0,"incompatible-legacy-selection-cleared-with-evidence-intact");
    var e2e=new ScanCheckpoint{Mode="e2e",FullVerification=true};var route=new ScanResult{ServerChecked=true,ServerReached=true,TunnelFinished=true,TunnelEstablished=true,HttpsAttempted=true};e2e.Results.Add(route);
    Check(route.LatestServerReached&&!e2e.CanApply(route)&&e2e.WorkingAddresses()==0,"full-e2e-does-not-count-failed-https-as-full-success-but-retains-server-proof");e2e.FullVerification=false;Check(e2e.CanApply(route)&&e2e.WorkingAddresses()==1,"handshake-criterion-counts-actual-server-evidence");
    var choices=new ScanCheckpoint{Zone="t.example.invalid",Candidates=Enumerable.Range(1,9).Select(i=>"127.0.0."+i+":53").ToArray(),Results=Enumerable.Range(0,9).Select(i=>new ScanResult{Index=i,Pass=true,Transport="tcp"}).ToList()};
    foreach(var next in choices.Results.Take(8)){if(choices.SelectionError(next)!="")throw new Exception("early limit");choices.Selected.Add(next.Index+":tcp");}
    Check(choices.SelectionError(choices.Results[8])=="MAC_SCAN_SELECTION_LIMIT","allow-eight-compatible-choices-reject-ninth");
    var thresholded=new ScanCheckpoint{Mode="advanced",Transports=["udp","tcp"],ScoreMinimum=2,Results=[new(){Index=0,Transport="udp",Pass=true,Completed=2,Checks=["NS","TXT"]}]};
    Check(!thresholded.EligibleRows().Any(),"both-waits-for-observed-transport-results");thresholded.Results.Add(new(){Index=0,Transport="tcp",Pass=true,Completed=2,Checks=["TXT"]});
    Check(thresholded.EligibleRows().Select(r=>r.Transport).SequenceEqual(["udp"]),"never-borrow-other-wires-capability-score");
    var cancelled=new ScanCheckpoint{Zone="t.example.invalid",Candidates=[$"127.0.0.1:{silent.Port}"],Transports=["udp"],TimeoutMS=30000,Workers=1,Count=1,Threshold=1};using(var stop=new CancellationTokenSource(200)){bool stopped=false;try{await ScanSession.Run(cancelled,()=>ValueTask.CompletedTask,stop.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped&&cancelled.Results.Count==0,"stop-does-not-count-an-incomplete-attempt-as-failure");}
    var profile=MacProtocols.Single(ManualProfiles.Dns("dnstt","DNS fixture","t.example.invalid",$"127.0.0.1:{resolver.Port}","tcp",new string('a',64),"","22","",""));
    var proof=await TunnelScanner.Verify(profile,$"127.0.0.1:{resolver.Port}","tcp","https://example.invalid/",new("",""),5000,limit.Token);
    Check(!proof.Verified&&!proof.TunnelEstablished&&proof.Error.Length>0,"real-agent-rejects-dns-only-server-as-working-tunnel");
    bool remaining=System.Diagnostics.Process.GetProcesses().Any(process=>{try{return process.Id!=Environment.ProcessId&&process.MainModule?.FileName?.StartsWith(AppContext.BaseDirectory,StringComparison.OrdinalIgnoreCase)==true;}catch{return false;}finally{process.Dispose();}});Check(!remaining,"cancelled-tunnel-probe-leaves-no-agent-or-native-child");
    using(var key=new SessionSecretVault(()=>RandomNumberGenerator.GetBytes(32))){var store=new PrivateStore(Path.Combine(output,"checkpoint"),key);store.Save(plan);var restored=store.Load<ScanCheckpoint>();Check(restored.Results.Count==4&&restored.Results.Count(r=>r.Pass)==2,"real-native-results-roundtrip-in-encrypted-checkpoint");}
}catch(Exception e){failures++;Console.WriteLine(e.GetType().Name+" "+(e is SafeError error?error.Code:e.Message));}
File.WriteAllText(Path.Combine(output,"dns-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+", real pinned native scan engine and C# Agent",macRuntimeValidated=false,controlledLoopbackOnly=true,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"DNS: {checks} checks, {failures} failures");return failures==0?0:1;

sealed class FixtureDns : IDisposable
{
    readonly TcpListener tcp=new(IPAddress.Loopback,0);readonly UdpClient udp;readonly CancellationTokenSource stop=new();readonly bool silent;
    public int Port {get;}
    public FixtureDns(bool silent=false){
        this.silent=silent;
        // Windows may reserve UDP on the port selected by an ephemeral TCP bind.
        // Claim both or release the TCP reservation before trying a fresh one.
        for(int attempt=0;;attempt++){
            tcp.Start();Port=((IPEndPoint)tcp.LocalEndpoint).Port;
            try{udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,Port));break;}
            catch(SocketException e)when(attempt<15&&e.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied){tcp.Stop();}
        }
        _=Udp();_=Tcp();
    }
    byte[] Reply(byte[] query){int p=12;while(p<query.Length&&query[p]!=0){p+=query[p]+1;}p++;ushort kind=BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(p));p+=4;byte[] value=kind switch{2=>new byte[]{3,110,115,49,7,101,120,97,109,112,108,101,7,105,110,118,97,108,105,100,0},16=>new byte[]{4,116,101,115,116},_=>new byte[]{127,0,0,1}};var answer=new byte[p+12+value.Length];query.AsSpan(0,p).CopyTo(answer);BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(2),0x8180);BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(4),1);BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(6),1);answer[8]=answer[9]=answer[10]=answer[11]=0;answer[p]=0xc0;answer[p+1]=0x0c;BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(p+2),kind);BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(p+4),1);BinaryPrimitives.WriteUInt16BigEndian(answer.AsSpan(p+10),(ushort)value.Length);value.CopyTo(answer,p+12);return answer;}
    async Task Udp(){try{while(!stop.IsCancellationRequested){var packet=await udp.ReceiveAsync(stop.Token);if(!silent)await udp.SendAsync(Reply(packet.Buffer),packet.RemoteEndPoint,stop.Token);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}}
    async Task Tcp(){try{while(!stop.IsCancellationRequested){var socket=await tcp.AcceptTcpClientAsync(stop.Token);_=Serve(socket);}}catch(OperationCanceledException){}catch(SocketException){}}
    async Task Serve(TcpClient socket){using(socket){try{var stream=socket.GetStream();while(!stop.IsCancellationRequested){byte[] header=new byte[2];await stream.ReadExactlyAsync(header,stop.Token);int size=BinaryPrimitives.ReadUInt16BigEndian(header);if(size is <12 or >8192)return;byte[] query=new byte[size];await stream.ReadExactlyAsync(query,stop.Token);if(silent)continue;var answer=Reply(query);BinaryPrimitives.WriteUInt16BigEndian(header,(ushort)answer.Length);await stream.WriteAsync(header,stop.Token);await stream.WriteAsync(answer,stop.Token);}}catch{}}}
    public void Dispose(){stop.Cancel();tcp.Stop();udp.Dispose();stop.Dispose();}
}
