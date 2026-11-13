using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;
using Rovunet.Platform;

string output=Path.GetFullPath(args.FirstOrDefault()??"artifacts/updates");Directory.CreateDirectory(output);
int checks=0,failures=0;var results=new List<object>();
void Check(bool ok,string name){checks++;if(!ok){failures++;Console.WriteLine("FAIL "+name);}results.Add(new{name,passed=ok});}
async Task Reject(Func<Task> action,string name,string? code=null){try{await action();Check(false,name);}catch(SafeError e){Check(code==null||code==e.Code,name);}catch(OperationCanceledException){Check(code=="CANCELLED",name);}}
string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
var payload=Encoding.UTF8.GetBytes("Rovunet update fixture, not an installer");
const string v="0.1.8-beta.1",rid="osx-arm64";
string Name(string version,string arch)=>$"Rovunet-macOS-{version}-{(arch=="osx-arm64"?"AppleSilicon-arm64":"Intel-x64")}.pkg";
Uri Url(string version,string name)=>new($"https://github.com/msVPNco/rovunet-app/releases/download/macos-v{version}/{name}");
JsonObject Asset(string version,string name,byte[] bytes)=>new(){["name"]=name,["state"]="uploaded",["size"]=bytes.Length,["digest"]="sha256:"+Hash(bytes),["browser_download_url"]=Url(version,name).AbsoluteUri};
JsonObject Manifest(string version,string arch="osx-arm64",string minimum="15.0")=>new(){["schemaVersion"]=1,["publisher"]=MacUpdateClient.Publisher,["version"]=version,["artifacts"]=new JsonArray(new JsonObject{["rid"]=arch,["minimumMacOS"]=minimum,["fileName"]=Name(version,arch),["size"]=payload.Length,["sha256"]=Hash(payload)})};
(JsonObject Release,byte[] Manifest) Release(string version,string arch="osx-arm64",string minimum="15.0"){
    byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(Manifest(version,arch,minimum));
    return(new(){["tag_name"]="macos-v"+version,["draft"]=false,["prerelease"]=version.Contains('-'),["assets"]=new JsonArray(Asset(version,Name(version,arch),payload),Asset(version,"UPDATE-MANIFEST.json",manifest))},manifest);
}
HttpResponseMessage Response(byte[] bytes)=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
UpdateRoute route=new(true,10809);var seen=new List<(Uri Url,UpdateRoute Route)>();
var sample=Release(v);var releases=new JsonArray(sample.Release);byte[] manifestBytes=sample.Manifest;
Func<HttpRequestMessage,UpdateRoute,CancellationToken,Task<HttpResponseMessage>> respond=(request,snapshot,ct)=>{
    seen.Add((request.RequestUri!,snapshot));ct.ThrowIfCancellationRequested();
    string path=request.RequestUri!.AbsolutePath;
    return Task.FromResult(Response(request.RequestUri.Host=="api.github.com"?JsonSerializer.SerializeToUtf8Bytes(releases):path.EndsWith("UPDATE-MANIFEST.json")?manifestBytes:payload));
};
MacUpdateClient Client()=>new(()=>route,snapshot=>new FakeHandler((request,ct)=>respond(request,snapshot,ct)));
try{
    Check(MacUpdateClient.CompareVersions("0.1.10","0.1.9")>0,"numeric-version-order");
    Check(MacUpdateClient.CompareVersions("0.1.8","0.1.8-beta.20")>0,"stable-after-beta");
    Check(MacUpdateClient.CompareVersions("0.1.8-beta.10","0.1.8-beta.2")>0,"numeric-prerelease-order");
    Check(MacUpdateClient.CompareVersions("1.0.0-alpha","1.0.0-1")>0,"semver-numeric-before-alpha");
    Check(MacUpdateClient.CompareVersions(v,v)==0,"equal-version");
    await Reject(()=>Task.FromResult(MacUpdateClient.CompareVersions("../bad",v)),"invalid-version-rejected");
    foreach(string bad in new[]{"http://github.com/msVPNco/rovunet-app/releases/download/macos-v1.0.0/app.pkg","https://github.com.evil.invalid/msVPNco/rovunet-app/releases/download/macos-v1.0.0/app.pkg","https://github.com/other/repo/releases/download/macos-v1.0.0/app.pkg","https://user@github.com/msVPNco/rovunet-app/releases/download/macos-v1.0.0/app.pkg","https://release-assets.githubusercontent.com/github-production-release-asset/1/asset"})Check(!MacUpdateClient.AllowedUrl(new(bad)),"reject-untrusted-url-"+checks);
    var found=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true);Check(found?.Version==v&&found.Rid==rid,"select-arm64-beta-installer");
    Check(seen.All(x=>x.Route.Connected&&x.Route.HttpPort==10809),"metadata-and-manifest-use-tunnel");
    Check(await Client().Check(v,rid,new(15,0),true)==null,"same-version-not-update");
    Check(await Client().Check(BuildVersion.SemVer,rid,new(15,0),false)==null,"stable-channel-excludes-beta");
    var android=new JsonObject{["tag_name"]="v9.0.0",["draft"]=false,["prerelease"]=false,["assets"]=new JsonArray()};releases.Insert(0,android);
    Check((await Client().Check(BuildVersion.SemVer,rid,new(15,0),true))?.Version==v,"ignore-global-android-latest");releases.RemoveAt(0);
    sample.Release["draft"]=true;Check(await Client().Check(BuildVersion.SemVer,rid,new(15,0),true)==null,"ignore-draft");sample.Release["draft"]=false;
    var intel=Release(v,"osx-x64");releases=new(intel.Release);manifestBytes=intel.Manifest;
    Check((await Client().Check(BuildVersion.SemVer,"osx-x64",new(15,0),true))?.FileName.EndsWith("Intel-x64.pkg")==true,"select-intel-installer");
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"wrong-architecture-not-offered","MAC_UPDATE_OS_UNSUPPORTED");
    var newerOS=Release(v,rid,"16.0");releases=new(newerOS.Release);manifestBytes=newerOS.Manifest;
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"minimum-os-enforced","MAC_UPDATE_OS_UNSUPPORTED");
    sample=Release(v);releases=new(sample.Release);manifestBytes=sample.Manifest;
    var assets=sample.Release["assets"]!.AsArray();string originalDigest=assets[0]!["digest"]!.GetValue<string>();assets[0]!["digest"]=null;
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"missing-asset-digest-rejected");assets[0]!["digest"]=originalDigest;
    string originalUrl=assets[0]!["browser_download_url"]!.GetValue<string>();assets[0]!["browser_download_url"]="https://github.com/other/repo/releases/download/macos-v0.1.8-beta.1/evil.pkg";
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"foreign-repository-asset-rejected");assets[0]!["browser_download_url"]=originalUrl;
    assets.Add(assets[0]!.DeepClone());await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"duplicate-installer-rejected");assets.RemoveAt(2);
    manifestBytes=Encoding.UTF8.GetBytes("{}");await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"manifest-digest-tamper-rejected","MAC_UPDATE_HASH_MISMATCH");manifestBytes=sample.Manifest;
    var badManifest=Manifest(v);badManifest["publisher"]="other/repo";manifestBytes=JsonSerializer.SerializeToUtf8Bytes(badManifest);assets[1]=Asset(v,"UPDATE-MANIFEST.json",manifestBytes);
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"manifest-publisher-rejected");manifestBytes=sample.Manifest;assets[1]=Asset(v,"UPDATE-MANIFEST.json",manifestBytes);

    string goodManifest=Encoding.UTF8.GetString(sample.Manifest);
    manifestBytes=Encoding.UTF8.GetBytes(goodManifest.Insert(1,"\"schemaVersion\":1,"));assets[1]=Asset(v,"UPDATE-MANIFEST.json",manifestBytes);
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"duplicate-manifest-field-rejected","MAC_UPDATE_METADATA_INVALID");
    badManifest=Manifest(v);badManifest["unreviewedPolicy"]="insecure";manifestBytes=JsonSerializer.SerializeToUtf8Bytes(badManifest);assets[1]=Asset(v,"UPDATE-MANIFEST.json",manifestBytes);
    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"unknown-manifest-policy-rejected","MAC_UPDATE_METADATA_INVALID");
    manifestBytes=sample.Manifest;assets[1]=Asset(v,"UPDATE-MANIFEST.json",manifestBytes);

    await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,"unknown",new(15,0),true),"unknown-architecture-rejected","MAC_UPDATE_ARCH_UNSUPPORTED");
    route=new(true,0);await Reject(async()=>_=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true),"active-tunnel-without-proxy-never-direct","MAC_UPDATE_ROUTE_NOT_READY");
    route=new(false,0);seen.Clear();found=await Client().Check(BuildVersion.SemVer,rid,new(15,0),true);Check(found!=null&&seen.All(x=>!x.Route.Connected),"disconnected-and-directly-reachable-can-check");
    string destination=Path.Combine(output,"fixture.pkg");await Client().Download(found!,destination);Check(File.ReadAllBytes(destination).SequenceEqual(payload),"direct-download-verified");
    route=new(true,10809);seen.Clear();await Client().Download(found!,destination);Check(seen.All(x=>x.Route.Connected),"connected-download-through-tunnel");
    var normal=respond;
    respond=(request,snapshot,ct)=>{
        seen.Add((request.RequestUri!,snapshot));
        if(request.RequestUri!.Host=="github.com")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found){Headers={Location=new("https://release-assets.githubusercontent.com/github-production-release-asset/1445854036/fixture?signature=public-fixture")}});
        return Task.FromResult(Response(payload));
    };
    seen.Clear();await Client().Download(found!,destination);Check(seen.Count==2&&seen.All(x=>x.Route.Connected),"github-and-cdn-redirect-both-use-tunnel");
    respond=(request,snapshot,ct)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found){Headers={Location=new("https://evil.invalid/payload")}});
    await Reject(()=>Client().Download(found!,destination),"untrusted-redirect-rejected","MAC_UPDATE_URL_REJECTED");
    Check(File.ReadAllBytes(destination).SequenceEqual(payload),"existing-file-preserved-on-redirect-failure");
    respond=(request,snapshot,ct)=>Task.FromResult(Response("corrupt"u8.ToArray()));
    await Reject(()=>Client().Download(found!,destination),"truncated-download-rejected","MAC_UPDATE_HASH_MISMATCH");
    Check(File.ReadAllBytes(destination).SequenceEqual(payload),"existing-file-preserved-on-integrity-failure");
    respond=(request,snapshot,ct)=>Task.FromResult(Response(payload.Select(x=>(byte)(x^1)).ToArray()));
    await Reject(()=>Client().Download(found!,destination),"same-size-tampering-rejected","MAC_UPDATE_HASH_MISMATCH");
    respond=(request,snapshot,ct)=>throw new HttpRequestException("fixture proxy unavailable");seen.Clear();var attempted=new List<UpdateRoute>();
    var failing=new MacUpdateClient(()=>route,r=>{attempted.Add(r);return new FakeHandler((request,ct)=>respond(request,r,ct));});
    await Reject(()=>failing.Download(found!,destination),"active-proxy-failure-no-direct-fallback","MAC_UPDATE_NETWORK_FAILED");Check(attempted.All(x=>x.Connected),"failure-did-not-create-direct-handler");
    respond=normal;
    using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();await Reject(()=>Client().Download(found!,destination,cancellation:cancelled.Token),"cancel-preserves-existing-file","CANCELLED");}
    Check(!Directory.EnumerateFiles(output,"*.partial").Any(),"partial-files-cleaned");
    bool switched=false;route=new(true,10809);seen.Clear();
    var switchProgress=new InlineProgress(p=>{if(p.Stage=="downloading"&&!switched){switched=true;route=new(false,0);}});
    await Client().Download(found!,destination,switchProgress);Check(switched&&seen.Any(x=>x.Route.Connected)&&seen.Any(x=>!x.Route.Connected),"mid-download-tunnel-loss-restarts-direct-when-reachable");
    Check(File.ReadAllBytes(destination).SequenceEqual(payload),"route-restart-still-verifies-package");
    switched=false;seen.Clear();route=new(false,0);await Client().Download(found!,destination,new InlineProgress(p=>{if(p.Stage=="downloading"&&!switched){switched=true;route=new(true,10809);}}));
    Check(seen.Any(x=>!x.Route.Connected)&&seen.Any(x=>x.Route.Connected),"newly-connected-tunnel-takes-over-download");

    // Real production SocketsHttpHandler must CONNECT to the loopback proxy;
    // this test never dials a public server and does not weaken TLS validation.
    using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
    var receive=Task.Run(async()=>{using var socket=await listener.AcceptTcpClientAsync();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));var stream=socket.GetStream();byte[] buffer=new byte[4096];int n=await stream.ReadAsync(buffer,deadline.Token);await stream.WriteAsync("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),deadline.Token);return Encoding.ASCII.GetString(buffer,0,n);});
    var actualProxy=new MacUpdateClient(()=>new(true,port));
    await Reject(async()=>_=await actualProxy.Check(BuildVersion.SemVer,rid,new(15,0),true),"production-handler-reports-unusable-proxy","MAC_UPDATE_NETWORK_FAILED");
    string connect=await receive;Check(connect.StartsWith("CONNECT api.github.com:443 HTTP/1.1",StringComparison.Ordinal),"production-handler-sends-hostname-through-proxy");

    var vault=new MemoryVault();string privateRoot=Path.Combine(output,"private-store");Directory.CreateDirectory(privateRoot);
    var first=new PrivateStore(privateRoot,vault);first.Save(new Dictionary<string,string>{{"owner","first"}});
    var second=new PrivateStore(privateRoot,vault);_=second.Load<Dictionary<string,string>>();_=first.Load<Dictionary<string,string>>();first.Save(new Dictionary<string,string>{{"owner","new"}});
    await Reject(()=>{second.Save(new Dictionary<string,string>{{"owner","stale"}});return Task.CompletedTask;},"stale-process-cannot-overwrite-new-profile-store","MAC_STORE_CHANGED");
    Check(first.Load<Dictionary<string,string>>()["owner"]=="new","newer-store-preserved-after-stale-save");
    var unknown=new PrivateStore(privateRoot,vault);await Reject(()=>{unknown.Save(new Dictionary<string,string>());return Task.CompletedTask;},"unloaded-instance-cannot-replace-existing-data","MAC_STORE_CHANGED");
    byte[] encrypted=File.ReadAllBytes(Path.Combine(privateRoot,"profiles.enc"));encrypted[12]^=1;File.WriteAllBytes(Path.Combine(privateRoot,"profiles.enc"),encrypted);
    await Reject(()=>Task.FromResult(first.Load<Dictionary<string,string>>()),"corrupt-store-rejected","PRIVATE_STORE_FAILED");
    await Reject(()=>{first.Save(new Dictionary<string,string>());return Task.CompletedTask;},"failed-load-cannot-reset-store","PRIVATE_STORE_FAILED");
    Check(File.ReadAllBytes(Path.Combine(privateRoot,"profiles.enc")).SequenceEqual(encrypted),"corrupt-store-retained-for-recovery");

    string hugeRoot=Path.Combine(output,"oversize-store");Directory.CreateDirectory(hugeRoot);string hugePath=Path.Combine(hugeRoot,"profiles.enc");
    using(var huge=new FileStream(hugePath,FileMode.Create,FileAccess.Write))huge.SetLength(16*1024*1024+29);
    var bounded=new PrivateStore(hugeRoot,vault);await Reject(()=>Task.FromResult(bounded.Load<Dictionary<string,string>>()),"oversize-store-rejected-before-decryption","PRIVATE_STORE_FAILED");
    await Reject(()=>{bounded.Save(new Dictionary<string,string>());return Task.CompletedTask;},"oversize-store-cannot-be-reset","PRIVATE_STORE_FAILED");
    Check(new FileInfo(hugePath).Length==16*1024*1024+29,"oversize-original-retained");

}catch(Exception e){Check(false,"unexpected-"+e.GetType().Name);Console.WriteLine(e);}
File.WriteAllText(Path.Combine(output,"update-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+"",macRuntimeValidated=false,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Updates: {checks} checks, {failures} failures");return failures==0?0:1;

sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> handle):HttpMessageHandler
{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>handle(request,cancellationToken);}
sealed class InlineProgress(Action<MacUpdateProgress> action):IProgress<MacUpdateProgress>{public void Report(MacUpdateProgress value)=>action(value);}
sealed class MemoryVault:ISecretVault{readonly byte[] key=RandomNumberGenerator.GetBytes(32);public byte[] GetOrCreateKey()=>key.ToArray();}
