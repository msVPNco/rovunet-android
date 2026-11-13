using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;

if(args.Length>0&&args[0] is "--boot-arm" or "--boot-disarm")return BootPolicy.Command(args);
if(IntPtr.Size!=8||!OperatingSystem.IsWindowsVersionAtLeast(10,0,19045))return 10;
if(args is ["--service"]){try{return NetworkService30.Run();}catch{return 20;}}
if(args is ["--self-test"]){
    var checks=new Dictionary<string,bool>{["x64_native_filter_layout"]=IntPtr.Size==8&&Marshal.SizeOf<WfpFilter29>()==200&&Marshal.SizeOf<WfpSublayer29>()==72&&Marshal.SizeOf<WfpCondition29>()==40,["route_layout"]=Marshal.SizeOf<RouteRow29>()==104,["key_recovery_deterministic"]=PersistentWfp29.Key(Guid.Parse("0bc3b2b7-4519-4568-84b0-d9c4a8aabc38"),"block/0")==PersistentWfp29.Key(Guid.Parse("0bc3b2b7-4519-4568-84b0-d9c4a8aabc38"),"block/0")};
    using(var sdk=JsonDocument.Parse(typeof(PersistentWfp29).Assembly.GetManifestResourceStream("Rovunet.WfpSdkIds30")!)){var expected=sdk.RootElement.GetProperty("identifiers");checks["all_wfp_identifiers_match_pinned_microsoft_sdk"]=PersistentWfp29.SdkIdentifiers().All(pair=>expected.GetProperty(pair.Key).GetGuid()==pair.Value)&&expected.EnumerateObject().Count()==13;}
    var peers=new[]{System.Net.IPAddress.Parse("192.0.2.1"),System.Net.IPAddress.Parse("2001:db8::1")};var labels=PersistentWfp29.Labels(1,peers,true);checks["bounded_unique_persistent_keys"]=labels.Length<1024&&labels.Distinct().Count()==labels.Length;checks["protects_ipv4_ipv6_both_directions"]=Enumerable.Range(0,4).All(i=>labels.Contains("block/"+i)&&labels.Contains("loopback/"+i));checks["tun_permits_are_separate"]=labels.Count(l=>l.StartsWith("tun/"))==4;
    try{var routes=NativeRoutes29.Read();checks["actual_windows_readonly_route_layout"]=routes.Length>0&&routes.All(r=>r.Prefix.Family is 2 or 23&&r.PrefixLength<=(r.Prefix.Family==2?32:128));}catch{checks["actual_windows_readonly_route_layout"]=false;}
    var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();try{int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;checks["actual_listener_pid_table"]=LocalListener29.Owners(port).SequenceEqual(new[]{Environment.ProcessId});try{LocalListener29.Require(port,AppContext.BaseDirectory,WindowsIdentity.GetCurrent().User!.Value);checks["foreign_listener_cannot_be_privileged_upstream"]=false;}catch(SafeError){checks["foreign_listener_cannot_be_privileged_upstream"]=true;}}finally{listener.Stop();}
    foreach(string bad in new[]{"0.0.0.0","::","127.0.0.1","224.0.0.1","::ffff:192.0.2.1","fe80::1"}){try{TunPlan29.Validate(new(){Action="start",Session=Guid.NewGuid(),SocksPort=10808,Peers=[bad],ExperimentalAcknowledged=true});checks["reject_"+bad]=false;}catch(SafeError){checks["reject_"+bad]=true;}}
    Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Values.All(v=>v),checks,administratorToken=TrustedInstallation29.IsAdmin,systemNetworkChanged=false,realTunTested=false,realPersistentFilterTested=false}));return checks.Values.All(v=>v)?0:1;
}
if(args is ["--config-test"]){
    try{string engine=Path.Combine(AppContext.BaseDirectory,"engines/xray.exe");if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine))).Equals(TrustedInstallation29.XrayHash,StringComparison.OrdinalIgnoreCase))throw new SafeError("ENGINE_HASH_MISMATCH");using var p=Process.Start(new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-test","-c","stdin:"}})!;await p.StandardInput.WriteAsync(TunPlan29.Config(10808));p.StandardInput.Close();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(10000);await p.WaitForExitAsync(timeout.Token);await Task.WhenAll(output,error);Console.WriteLine(JsonSerializer.Serialize(new{passed=p.ExitCode==0,configAccepted=p.ExitCode==0,serverStarted=false,systemNetworkChanged=false}));return p.ExitCode==0?0:1;}catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,error=e.Code}));return 1;}
}
try{
    if(args.Length!=4||args[0]!="--pipe"||args[2]!="--parent"||!Regex.IsMatch(args[1],@"\Arovunet-network-[0-9A-F]{48}\z")||!int.TryParse(args[3],out int parentId))return 12;
    TrustedInstallation29.RequireAdmin();string root=TrustedInstallation29.Root();var hashes=TrustedInstallation29.Manifest(root);string owner=TrustedInstallation29.Parent(parentId,root,hashes);
    using var instance=new Mutex(false,@"Global\Rovunet.ExperimentalNetworkBroker");bool acquired;try{acquired=instance.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)return 11;
    try{
        using var parent=Process.GetProcessById(parentId);using var lifetime=new CancellationTokenSource();parent.EnableRaisingEvents=true;parent.Exited+=(_,_)=>lifetime.Cancel();if(parent.HasExited)return 14;
        var sid=new SecurityIdentifier(owner);var security=new PipeSecurity();security.SetOwner(sid);security.SetAccessRuleProtection(true,false);security.AddAccessRule(new(sid,PipeAccessRights.FullControl,AccessControlType.Allow));
        using var pipe=NamedPipeServerStreamAcl.Create(args[1],PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
        using var wait=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);wait.CancelAfter(TimeSpan.FromSeconds(15));pipe.WaitForConnectionAsync(wait.Token).GetAwaiter().GetResult();PipePeer28.RequireClient(pipe.SafePipeHandle,parentId);
        using var runtime=new NetworkRuntime29(root,owner);runtime.UnexpectedExit+=()=>lifetime.Cancel();
        try{while(!lifetime.IsCancellationRequested){
            var request=Wire.Read<TunRequest29>(pipe,lifetime.Token,4096).GetAwaiter().GetResult();TunPlan29.Validate(request);using var commandTime=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);commandTime.CancelAfter(TimeSpan.FromSeconds(45));TunReply29 reply;
            try{reply=request.Action switch{"status"=>runtime.Status(),"start"=>runtime.Start(request,commandTime.Token).GetAwaiter().GetResult(),"stop" or "recover"=>runtime.Recover(request.Session),_=>throw new SafeError("BROKER_REQUEST_REJECTED")};}
            catch(Exception e){try{reply=runtime.Status();}catch{reply=new(){State="recovery-required"};}reply.Error=e is SafeError safe?safe.Code:e is OperationCanceledException?"CANCELLED":"BROKER_OPERATION_FAILED";}
            Wire.Write(pipe,reply,lifetime.Token).GetAwaiter().GetResult();if(request.Action is "stop" or "recover"&&reply.Error.Length==0)break;
        }}finally{runtime.LostController();}
    }finally{instance.ReleaseMutex();}return 0;
}catch(SafeError){return 20;}catch{return 20;} // No raw profile/endpoint/exception text.
