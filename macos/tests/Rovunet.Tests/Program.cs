using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

int checks=0,failures=0;var results=new List<object>();
void Check(bool good,string name){checks++;results.Add(new{name,passed=good});if(!good){failures++;Console.WriteLine("FAIL "+name);}}
void Reject(Action action,string name){try{action();Check(false,name);}catch(SafeError){Check(true,name);}catch(ImportFailure){Check(true,name);}}
string vless="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&path=%2Foriginal&heartbeatPeriod=15&cs=TLS_AES_128_GCM_SHA256#name";
var p=MacImport.Read(vless).Single();
Check(p.CanConnect,"shared-import");
var endpoint=EndpointEditing.Apply(p,"new name","other.example.invalid","8443");
Check(endpoint.Contains("heartbeatPeriod=15")&&endpoint.Contains("cs=TLS_AES_128_GCM_SHA256"),"endpoint-edit-preserves-options");
Check(MacImport.Read("hy2://secret@server.example.invalid:443?mport=443,8443&hop-interval=10s").Single().CanConnect,"hy2-hopping-is-admitted");
Reject(()=>MacImport.Read("vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?unknown=1"),"unknown-option-never-dropped");
Reject(()=>MacImport.Read("rovunet-enc://fixture"),"locked-profile-not-flattened");
Reject(()=>MacImport.Read("trojan://secret@server.example.invalid:443?allowInsecure=1"),"tls-not-downgraded");
Reject(()=>MacImport.Read("ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("2022-blake3-aes-128-gcm:fixture"))+"@server.example.invalid:443"),"ss2022-malformed-key-rejected");
Reject(()=>MacImport.Read("vmess://"+Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{cs="TLS_AES_128_GCM_SHA256"}))),"vmess-missing-required-fields-rejected");
Reject(()=>MacImport.Read("[Interface]\nJc=3"),"awg-missing-required-fields-rejected");
Reject(()=>MacImport.Read("tuic://fixture@server.example.invalid:443"),"future-protocol-not-advertised");
var usage=SubscriptionUsage20.Parse("upload=10; download=20", "",null)!;
Check(usage.Total==null&&usage.Expire==null,"missing-usage-is-unknown");
Check(SubscriptionUsage20.Parse("total=0;expire=0","",null)!.Total==0,"explicit-unlimited-remains-distinct");
var old=new AppState{Profiles=[p],SelectedId=p.Id};var uri=new Uri("https://subscriptions.example.invalid/list");
var first=SubscriptionMerge.Apply(old,uri,[p],"Saved collection",null);var original=first.Profiles.Last();
original.ItemId="stable-item";first.SelectedId=original.Id;first.OverrideSources[original.Id]=original.Raw;first.OverrideNames[original.Id]=original.Name;
original.Raw=EndpointEditing.Apply(original,"local label","server.example.invalid","443");original.Name="local label";
var incoming=Importer.Single(vless);incoming.ItemId="stable-item";
var second=SubscriptionMerge.Apply(first,uri,[incoming],"",usage,"Provider name");
Check(second.Subscriptions.Single().Name=="Saved collection","subscription-keeps-name");
Check(second.Profiles.Last().Id==original.Id&&second.SelectedId==original.Id,"subscription-keeps-stable-selection");
Check(second.Profiles.Last().Name=="local label","subscription-keeps-permitted-local-name");
Check(first.Subscriptions.Single().Usage==null,"subscription-commit-is-transactional");
var dns=Importer.Single(ManualProfiles.Dns("dnstt","DNS","tunnel.example.invalid","127.0.0.1:53","tcp",new string('1',64),"","22","",""));
var candidate=ResolverScan.Candidate(dns,"127.0.0.2:53","tcp",true);Check(Importer.Fields(candidate.Raw)[22]=="tcp","explicit-tcp-preserved");
Reject(()=>ResolverScan.Candidate(dns,"127.0.0.2:53","udp",true),"explicit-tcp-cannot-become-udp");
var checkpoint=new ScanCheckpoint{Zone="tunnel.example.invalid",Candidates=["127.0.0.1:53"],Transports=["tcp"],Workers=50};checkpoint.Validate();Check(true,"scan-bounds-valid");checkpoint.Workers=51;Reject(checkpoint.Validate,"scan-concurrency-bounded");
string temp=Path.Combine(Path.GetFullPath(args.FirstOrDefault()??"artifacts"),"private-store-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);var vault=new MemoryVault();var store=new PrivateStore(temp,vault);store.Save(new AppState{Profiles=[p]});
byte[] encrypted=File.ReadAllBytes(Path.Combine(temp,"profiles.enc"));Check(!Encoding.UTF8.GetString(encrypted).Contains("server.example.invalid"),"disk-profile-is-encrypted");Check(store.Load<AppState>().Profiles.Single().Raw==p.Raw,"encrypted-store-roundtrip");encrypted[^1]^=1;File.WriteAllBytes(Path.Combine(temp,"profiles.enc"),encrypted);Reject(()=>store.Load<AppState>(),"tampered-store-rejected");
var bundle=BundleCodec.Seal([p],"test-only-password");Check(BundleCodec.Open(bundle,"test-only-password",true).Profiles.Single().Raw==p.Raw,"encrypted-bundle-preserves-options");Reject(()=>BundleCodec.Open(bundle,"wrong-password",true),"bundle-authentication-required");
var frame=new MemoryStream();await Wire.Write(frame,new AgentCommand{Action="connect",Profile=p},CancellationToken.None);frame.Position=0;var restored=await Wire.Read<AgentCommand>(frame,CancellationToken.None);Check(restored.Profile!.Raw==p.Raw,"framed-agent-ipc-roundtrip");
Check(new AgentCommand{Profile=p}.ToString()=="AgentCommand(redacted)"&&p.ToString()=="Profile(redacted)","credentials-not-in-diagnostics");

int physicalReads=0;byte[] sessionKey=RandomNumberGenerator.GetBytes(32);
using(var cache=new SessionSecretVault(()=>{Interlocked.Increment(ref physicalReads);return sessionKey.ToArray();})){
    Parallel.For(0,24,_=>{byte[] acquired=cache.GetOrCreateKey();CryptographicOperations.ZeroMemory(acquired);});
    var cachedStore=new PrivateStore(Path.Combine(temp,"session"),cache);cachedStore.Save(new AppState());cachedStore.Load<AppState>();cachedStore.Save(new AppState());
    using var child=new PipeSecretVault(cachedStore.AcquireSessionKey());var childStore=new PrivateStore(Path.Combine(temp,"session"),child);childStore.Load<AppState>();childStore.Save(new AppState());
    Check(physicalReads==1,"one-keychain-read-per-session-including-child-journal");
    Check(cache.GetOrCreateKey().SequenceEqual(sessionKey),"erasing-caller-copy-does-not-corrupt-cache");
}
int denials=0;using(var denied=new SessionSecretVault(()=>{denials++;throw new SafeError("KEYCHAIN_ACCESS_FAILED");})){
    for(int n=0;n<6;n++)Reject(()=>denied.GetOrCreateKey(),"denied-keychain-access-latched-"+n);
    Check(denials==1,"diagnostics-and-saves-cannot-repeat-denied-password-prompt");
}
foreach(string json in new[]{"{\"Index\":1,\"Transport\":\"tcp\",\"Pass\":true,\"ElapsedMS\":47}","{\"index\":1,\"transport\":\"tcp\",\"pass\":true,\"elapsedMs\":47}"}){
    var row=ScanSession.DecodeNativeResult(json);Check(row.Index==1&&row.Transport=="tcp"&&row.Pass&&row.ElapsedMS==47,"scanner-actual-and-camel-json-preserve-measurements");
}
Reject(()=>ScanSession.DecodeNativeResult("{}"),"scanner-missing-required-fields-rejected");
var dohProfile=MacProtocols.Single(ManualProfiles.Dns("dnstt","DoH","t.example.invalid","https://resolver.example.invalid/dns-query","https",new string('a',64),"","22","",""));
var editedDoh=MacDnsEditing.Apply(dohProfile,new Dictionary<int,string>{{2,"Renamed DoH"}},"","https","https://resolver.example.invalid/dns-query");
Check(editedDoh.Name=="Renamed DoH"&&Importer.Fields(editedDoh.Raw)[4]=="","doh-editor-saves-without-unrelated-ip-list");
var duplicate=MacProtocols.Single(ProfileEditing.ChangeNative(dns,new Dictionary<int,string>{{4,"127.0.0.1:53:0,127.0.0.1:53:1,[::1]:53:1"}}));
var duplicateFields=Importer.Fields(duplicate.Raw);var retained=MacDnsEditing.Apply(duplicate,new Dictionary<int,string>{{2,"Renamed duplicate"}},MacDnsEditing.DisplayResolvers(duplicateFields[4]),"tcp","");
Check(Importer.Fields(retained.Raw)[4]==duplicateFields[4],"duplicate-authoritative-and-ipv6-resolvers-survive-name-edit");
Check(MacDnsEditing.NormalizeResolvers("127.0.0.1:53\n[::1]:53","tcp",duplicateFields[4])=="127.0.0.1:53:1,[::1]:53:1","resolver-editor-preserves-authoritative-flags");
var sshDns=MacProtocols.Single(ManualProfiles.Dns("dnstt_ssh","SSH DNS","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"127.0.0.1","22","fixture","original"));
var authEdit=MacDnsEditing.Apply(sshDns,new Dictionary<int,string>{{19,"127.0.0.2"},{17,"2222"}},"127.0.0.1:53","tcp","",new("fixture","new secret | unicode ✓","",""));
var authFields=Importer.Fields(authEdit.Raw);Check(authFields[16]=="new secret | unicode ✓"&&authFields[19]=="127.0.0.2"&&authFields[17]=="2222"&&authFields[22]=="tcp","v33-credential-editor-uses-envelope-and-preserves-explicit-tcp");
foreach(var (family,options) in new (string,Dictionary<int,string>)[]{
    ("dnstt",new(){{27,"1"},{60,"roundrobin"},{61,"2"}}),("sayedns",new(){{27,"0"},{38,"1"}}),
    ("vaydns",new(){{41,"1"},{42,"cname"},{43,"110"},{44,"3.5"},{45,"55"},{46,"4"},{47,"750"},{48,"4"},{49,"4"}}),
    ("soar",new(){{79,"1"},{80,"DE"},{81,"off"},{82,"custom"},{83,"127.0.0.3:53"}}),
    ("slipstream",new(){{5,"1"},{6,"2500"},{7,"dcubic"}})}){
    var familyProfile=MacProtocols.Single(ManualProfiles.Dns(family,"Family fixture","t.example.invalid","127.0.0.1:53","udp",new string('a',64),"","22","",""));
    var edited=MacDnsEditing.Apply(familyProfile,options,"127.0.0.1:53","udp","");
    using var runtime=JsonDocument.Parse(TransportPlan.Config(edited,12345,""));var slots=runtime.RootElement.GetProperty("fields");
    Check(options.All(pair=>slots[pair.Key].GetString()==pair.Value),"family-fields-arrive-at-real-engine-config-"+family);
    if(family!="slipstream")Reject(()=>MacDnsEditing.Apply(familyProfile,new Dictionary<int,string>{{11,"not-a-server-key"}},"127.0.0.1:53","udp",""),"invalid-public-key-rejected-for-"+family);
    if(family=="soar")Check(MacDnsEditing.Apply(familyProfile,new Dictionary<int,string>{{11,Convert.ToBase64String(Enumerable.Repeat((byte)12,32).ToArray())}},"127.0.0.1:53","udp","").CanConnect,"valid-soar-base64-key-accepted");
    int wrong=family=="vaydns"?38:45;Reject(()=>MacDnsEditing.Apply(familyProfile,new Dictionary<int,string>{{wrong,"1"}},"127.0.0.1:53","udp",""),"reject-unrelated-family-slot-"+family);
}
Reject(()=>MacDnsEditing.Apply(dns,new Dictionary<int,string>{{34,"1"}},"127.0.0.1:53","tcp",""),"dns-editor-cannot-change-sharing-restrictions");
var tunConfig=JsonDocument.Parse(MacProtocols.Runtime(p,18080,10808,0,0,new string('a',64),new string('b',64)));
var tunSocks=tunConfig.RootElement.GetProperty("inbounds").EnumerateArray().Single(i=>i.GetProperty("protocol").GetString()=="socks").GetProperty("settings");
Check(tunSocks.GetProperty("udp").GetBoolean()&&tunSocks.GetProperty("auth").GetString()=="password"&&tunSocks.GetProperty("accounts")[0].GetProperty("user").GetString()==new string('a',64),"tun-socks-udp-requires-ephemeral-credentials");
Check(!new AppState().SystemTun,"tun-is-explicit-opt-in-with-existing-settings-preserved");
using(var dnsOnly=new Rovunet.Agent.Runtime(true)){Reject(()=>dnsOnly.Connect(MacProfileEditing.Native("doh","DNS only","https://resolver.example.invalid/dns-query"),false,CancellationToken.None,systemTun:true).GetAwaiter().GetResult(),"dns-only-direct-profile-cannot-be-presented-as-vpn");}
var rememberedPlans=new AppState();var aPlan=new ScanCheckpoint{ProfileId="AAA",ProfileFingerprint="original",Candidates=["127.0.0.1:53"]};
var bPlan=new ScanCheckpoint{ProfileId="BBB",ProfileFingerprint="other",Candidates=["127.0.0.2:53"]};rememberedPlans.SaveScan(aPlan);rememberedPlans.SaveScan(bPlan);
var restoredPlans=rememberedPlans.Copy();Check(restoredPlans.FindScan("AAA","original")?.Candidates[0]=="127.0.0.1:53"&&restoredPlans.FindScan("AAA","edited")==null,"profile-specific-resume-survives-a-second-profile-and-rejects-edits");
int silentReads=0,interactiveReads=0;byte[] originalKey=RandomNumberGenerator.GetBytes(32);
using(var locked=new SessionSecretVault(()=>{silentReads++;throw new SafeError("KEYCHAIN_ACCESS_FAILED");})){
    Reject(()=>locked.GetOrCreateKey(),"silent-open-locked");Reject(()=>locked.GetOrCreateKey(),"background-read-does-not-retry-denial");
    Reject(()=>locked.Unlock(()=>{interactiveReads++;throw new SafeError("KEYCHAIN_ACCESS_FAILED");}),"user-may-deny-unlock");
    Reject(()=>locked.GetOrCreateKey(),"deny-never-starts-another-prompt");var acquired=locked.Unlock(()=>{interactiveReads++;return originalKey.ToArray();});
    CryptographicOperations.ZeroMemory(acquired);var cached=locked.GetOrCreateKey();Check(cached.SequenceEqual(originalKey)&&silentReads==1&&interactiveReads==2,"explicit-retry-without-relaunch-and-session-cache");CryptographicOperations.ZeroMemory(cached);
}
string untouched=Path.Combine(temp,"key-guard");Check(PrivateStore.MayInitializeKey(untouched),"new-store-can-initialize");
Directory.CreateDirectory(Path.Combine(untouched,"system-proxy"));File.WriteAllBytes(Path.Combine(untouched,"system-proxy","profiles.enc"),[1]);Check(!PrivateStore.MayInitializeKey(untouched),"proxy-recovery-key-must-not-be-replaced");
File.WriteAllBytes(Path.Combine(untouched,"profiles.enc"),[2]);Check(!PrivateStore.MayInitializeKey(untouched),"existing-profile-key-must-not-be-replaced");
await AutomaticPathCases.Run(Check);
Check(SubscriptionInput.Address(" \uFEFFhttps://subscriptions.example.invalid:8443/sub/example?format=base64 ")=="https://subscriptions.example.invalid:8443/sub/example?format=base64","subscription-input-recognizes-https-port-path-query-and-bom");
Check(SubscriptionInput.Address(vless)==null&&SubscriptionInput.Address("https://one.example.invalid/\nhttps://two.example.invalid/")==null,"subscription-input-does-not-misclassify-profile-or-multiline-data");
Reject(()=>SubscriptionInput.Address("https://user:password@subscriptions.example.invalid/sub"),"subscription-input-does-not-accept-url-credentials");
Check(SubscriptionInput.ProviderName("base64:"+Convert.ToBase64String(Encoding.UTF8.GetBytes("Sample subscription")))=="Sample subscription"&&SubscriptionInput.ProviderName("https://subscriptions.example.invalid/private")=="","subscription-title-decodes-provider-name-without-showing-private-url");
var metered=SubscriptionUsage20.Parse("upload=1024;download=2048;total=8192;expire=1800000000","",DateTimeOffset.UtcNow)!;
var meteredState=SubscriptionMerge.Apply(new(),uri,[p],"Metered fixture",metered).Copy();
Check(meteredState.Subscriptions.Single().Usage is {Upload:1024,Download:2048,Total:8192,Expire:1800000000},"metered-and-timed-subscription-fields-survive-merge-and-persistence");
Check(SubscriptionUsage20.Parse("expire=1800000000","",null) is {Total:null,Upload:null,Download:null,Expire:1800000000},"time-only-subscription-does-not-invent-volume-fields");
Check(SubscriptionUsage20.Parse("total=1;total=2","",null)==null&&SubscriptionUsage20.Parse("upload=9223372036854775807;download=1","",null)==null,"invalid-or-overflowing-quota-never-becomes-unlimited");
Directory.CreateDirectory(Path.GetFullPath(args.FirstOrDefault()??"artifacts"));File.WriteAllText(Path.Combine(Path.GetFullPath(args.FirstOrDefault()??"artifacts"),"mac-domain-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+"",macRuntimeValidated=false,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Mac domain: {checks} checks, {failures} failures");return failures==0?0:1;

sealed class MemoryVault : ISecretVault {readonly byte[] key=RandomNumberGenerator.GetBytes(32);public byte[] GetOrCreateKey()=>key.ToArray();}
