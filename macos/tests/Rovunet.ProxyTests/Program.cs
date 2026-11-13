using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

int checks=0,failures=0;var results=new List<object>();
void Check(bool good,string name){checks++;results.Add(new{name,passed=good});if(!good)Console.WriteLine("FAIL "+name);if(!good)failures++;}
void Reject(Action action,string name){try{action();Check(false,name);}catch(SafeError){Check(true,name);}}
const string first="00000000-0000-4000-8000-000000000001",second="00000000-0000-4000-8000-000000000002";
var order=new List<string>();var prefs=new MemoryPreferences(order);var journal=new MemoryJournal(order);
var old=new ProxyMap{["HTTPEnable"]=ProxyMap.Number(1),["HTTPProxy"]=ProxyMap.Text("corporate.example.invalid"),["HTTPPort"]=ProxyMap.Number(3128),["ProxyAutoConfigEnable"]=ProxyMap.Number(1),["ProxyAutoConfigURLString"]=ProxyMap.Text("https://pac.example.invalid/proxy.pac"),["VendorExtra"]="<array><string>keep</string><integer>7</integer></array>"};
prefs.Values[first]=old.Copy();prefs.Eligible.Add(first);var lease=new SystemProxyLease(prefs,journal);lease.Start(18080,10808);
Check(lease.Active&&lease.ServiceCount==1,"lease-active-after-commit");Check(order.IndexOf("save")<order.IndexOf("write"),"journal-durable-before-system-write");
var installed=prefs.Values[first]!;Check(installed["HTTPProxy"]==ProxyMap.Text("127.0.0.1")&&installed["HTTPPort"]==ProxyMap.Number(18080)&&installed["HTTPSPort"]==ProxyMap.Number(18080)&&installed["SOCKSPort"]==ProxyMap.Number(10808),"http-https-socks-loopback-ports");
Check(installed["ProxyAutoConfigEnable"]==ProxyMap.Number(0)&&installed["VendorExtra"]==old["VendorExtra"]&&installed["ProxyAutoConfigURLString"]==old["ProxyAutoConfigURLString"],"pac-disabled-without-losing-original-url-or-vendor-options");
prefs.Values[first]!["UnrelatedUserEdit"]=ProxyMap.Text("retain");lease.Stop();
Check(prefs.Values[first]!["HTTPProxy"]==old["HTTPProxy"]&&prefs.Values[first]!["ProxyAutoConfigEnable"]==old["ProxyAutoConfigEnable"]&&!prefs.Values[first]!.ContainsKey("SOCKSEnable"),"exact-original-and-absent-keys-restored");
Check(prefs.Values[first]!["UnrelatedUserEdit"]==ProxyMap.Text("retain"),"unrelated-user-change-preserved");Check(journal.Load().Services.Count==0,"journal-cleared-only-after-restoration");
lease.Start(18081,10809);prefs.Values[first]!["HTTPProxy"]=ProxyMap.Text("user.example.invalid");prefs.Values[first]!["HTTPPort"]=ProxyMap.Number(9000);lease.Stop();
Check(prefs.Values[first]!["HTTPProxy"]==ProxyMap.Text("user.example.invalid")&&prefs.Values[first]!["HTTPPort"]==ProxyMap.Number(9000),"user-changed-channel-not-overwritten");
Check(!prefs.Values[first]!.ContainsKey("HTTPSProxy")&&!prefs.Values[first]!.ContainsKey("SOCKSProxy"),"other-owned-channels-restored-after-user-http-change");
lease.Start(18082,10810);prefs.Eligible.Add(second);prefs.Values[second]=null;lease.Refresh();Check(lease.ServiceCount==2&&prefs.Values[second]!["HTTPPort"]==ProxyMap.Number(18082),"new-network-service-added-to-lease");
prefs.Values[first]!["HTTPPort"]=ProxyMap.Number(9999);lease.Refresh();Check(prefs.Values[first]!["HTTPPort"]==ProxyMap.Number(9999),"refresh-does-not-reassert-over-user-change");
new SystemProxyLease(prefs,journal).Recover();Check(prefs.Values[second]==null,"abandoned-lease-restores-original-absence");Check(journal.Load().Services.Count==0,"abandoned-lease-journal-cleared");
var parsed=ProxyMap.Parse("<?xml version=\"1.0\"?><!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"><plist version=\"1.0\"><dict><key>Array</key><array><string>A&amp;B</string><data>AQID</data></array><key>Enabled</key><true/></dict></plist>");
Check(ProxyMap.Parse(parsed.Xml())["Array"]==parsed["Array"]&&ProxyMap.Parse(parsed.Xml())["Enabled"]=="<true />","plist-types-roundtrip-without-external-dtd");
Reject(()=>new SystemProxyLease(prefs,journal).Start(0,10808),"invalid-ports-rejected-before-mutation");
var idlePrefs=new MemoryPreferences(order){Eligible=[Guid.NewGuid().ToString()]};var idleJournal=new MemoryJournal(order);var stable=new SystemProxyLease(idlePrefs,idleJournal);stable.Start(18085,10813);int locks=idlePrefs.Locks;stable.Refresh();stable.Refresh();Check(idlePrefs.Locks==locks,"unchanged-services-never-request-another-privileged-write-lock");stable.Stop();
int beforeCancel=order.Count(x=>x=="write");Reject(()=>new SystemProxyLease(prefs,journal,()=>false).Start(18084,10812),"cancelled-owner-cannot-apply-proxy-after-authorization");Check(order.Count(x=>x=="write")==beforeCancel,"cancel-before-apply-makes-no-system-write");
prefs.FailCommit=true;try{new SystemProxyLease(prefs,journal).Start(18083,10811);}catch(SafeError){}Check(journal.Load().Services.Count>0,"failed-apply-retains-recovery-journal");prefs.FailCommit=false;new SystemProxyLease(prefs,journal).Recover();Check(journal.Load().Services.Count==0,"failed-apply-recovery-clears-after-success");
string root=Path.Combine(Path.GetFullPath(args.FirstOrDefault()??"artifacts"),"proxy-private-"+Guid.NewGuid().ToString("N"));using(var ownership=new PrivateProxyJournal(root,new Vault())){Reject(()=>{using var duplicate=new PrivateProxyJournal(root,new Vault());},"second-instance-cannot-own-same-proxy-journal");}
SafeDiagnostics.Record("import-review","MAC_OPTION_NOT_READY","mport");SafeDiagnostics.Record("import-review","UNEXPECTED_ERROR","vless://private-secret@server.example.invalid");string report=SafeDiagnostics.Json("fixture","error",false,true,false);Check(report.Contains("mport")&&!report.Contains("private-secret")&&!report.Contains("server.example.invalid"),"diagnostics-redact-sensitive-argument");
SafeDiagnostics.Error("HOME","UNEXPECTED_ERROR",new InvalidOperationException("private-secret server.example.invalid"));report=SafeDiagnostics.Json("fixture","error",false,true,false);Check(report.Contains("InvalidOperationException")&&!report.Contains("private-secret")&&!report.Contains("server.example.invalid"),"exception-type-recorded-without-exception-message");
if(OperatingSystem.IsMacOS()){
    await NativePreferencesFixture.Run(Check);
    NativeTestApplication.Initialize();
    using var authorization=new MacProxyAuthorization();
    byte[] firstForm=authorization.Export();
    try{
        for(int cycle=0;cycle<3;cycle++){
            byte[] form=authorization.Export();
            try{
                Check(form.AsSpan().SequenceEqual(firstForm),"mac-authorization-session-reused-"+cycle);
                using var reader=new MacProxyPreferences(form);
                Check(reader.Services().All(id=>Guid.TryParse(id,out _)),"mac-authorized-preferences-read-only-import-"+cycle);
            }finally{CryptographicOperations.ZeroMemory(form);}
        }
        using var wire=new MemoryStream();
        await Wire.Write(wire,new AgentCommand{Action="connect",ProxyAuthorization=firstForm},CancellationToken.None);
        wire.Position=0;var received=await Wire.Read<AgentCommand>(wire,CancellationToken.None);
        Check(received.ProxyAuthorization.AsSpan().SequenceEqual(firstForm)&&received.ToString()=="AgentCommand(redacted)","authorization-pipe-roundtrip-redacted");
        CryptographicOperations.ZeroMemory(received.ProxyAuthorization);
        Reject(()=>new MacProxyPreferences(new byte[1]),"invalid-external-authorization-rejected");
    }finally{CryptographicOperations.ZeroMemory(firstForm);}
    authorization.Dispose();
    try{_=authorization.Export();Check(false,"closed-authorization-session-rejected");}catch(ObjectDisposedException){Check(true,"closed-authorization-session-rejected");}
}
Directory.CreateDirectory(Path.GetFullPath(args.FirstOrDefault()??"artifacts"));File.WriteAllText(Path.Combine(Path.GetFullPath(args.FirstOrDefault()??"artifacts"),"proxy-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+" / transactional mock, Darwin isolated SCPreferences file commits and CAS, read-only system authorization transfer; host networking unmodified",nativeMacValidated=false,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Proxy: {checks} checks, {failures} failures");return failures==0?0:1;
sealed class MemoryJournal(List<string> order):IProxyJournal{ProxyLeaseJournal journal=new();public ProxyLeaseJournal Load()=>JsonSerializer.Deserialize<ProxyLeaseJournal>(JsonSerializer.Serialize(journal))!;public void Save(ProxyLeaseJournal value){order.Add("save");journal=JsonSerializer.Deserialize<ProxyLeaseJournal>(JsonSerializer.Serialize(value))!;}}
sealed class MemoryPreferences(List<string> order):IProxyPreferences{public Dictionary<string,ProxyMap?> Values=[];public List<string> Eligible=[];public bool FailCommit;public int Locks;public IDisposable Begin(){Locks++;return new Noop();}public IReadOnlyList<string> Services()=>Eligible;public ProxyMap? Read(string id)=>Values.GetValueOrDefault(id)?.Copy();public void Write(string id,ProxyMap? value){order.Add("write");Values[id]=value?.Copy();}public void Commit(){order.Add("commit");if(FailCommit)throw new SafeError("MAC_PROXY_APPLY_FAILED");}public void Dispose(){}sealed class Noop:IDisposable{public void Dispose(){}}}
sealed class Vault:ISecretVault{readonly byte[] key=RandomNumberGenerator.GetBytes(32);public byte[] GetOrCreateKey()=>key.ToArray();}

static class NativeTestApplication {
    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/AppKit.framework/AppKit",EntryPoint="NSApplicationLoad")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I1)]
    public static extern bool Initialize();
}
