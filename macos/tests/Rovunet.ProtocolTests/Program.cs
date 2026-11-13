using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Rovunet.Core;
using Rovunet.Platform;
int checks=0,failures=0;var results=new List<object>();void Check(bool good,string name){checks++;results.Add(new{name,passed=good});if(!good){failures++;Console.WriteLine("FAIL "+name);}}void Reject(Action action,string name){try{action();Check(false,name);}catch(SafeError){Check(true,name);}catch(ImportFailure){Check(true,name);}}
string key16=Convert.ToBase64String(Enumerable.Range(0,16).Select(n=>(byte)n).ToArray()),key32=Convert.ToBase64String(Enumerable.Range(0,32).Select(n=>(byte)(n+1)).ToArray());
foreach(var pair in new[]{("2022-blake3-aes-128-gcm",key16),("2022-blake3-aes-256-gcm",key32),("2022-blake3-chacha20-poly1305",key32)}){
 string raw="ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Item1+":"+pair.Item2))+"@server.example.invalid:443#SS2022";var p=MacImport.Read(raw).Single();var config=JsonNode.Parse(MacProtocols.Runtime(p,18080,10808))!;Check(p.Raw==raw&&config["outbounds"]![0]!["settings"]!["servers"]![0]!["method"]!.ToString()==pair.Item1,"ss2022-method-preserved-"+pair.Item1);
}
Reject(()=>MacImport.Read("ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("2022-blake3-aes-128-gcm:"+key32))+"@server.example.invalid:443"),"ss2022-wrong-key-length-rejected");
var hy=MacImport.Read("hy2://fixture@server.example.invalid:443?sni=server.example.invalid&mport=443,10000-10010&hop-interval=15s").Single();var hyOut=MacProtocols.Extended(hy.Raw)!.Outbound;Check(hyOut["streamSettings"]!["finalmask"]!["udp"]![0]!["settings"]!["interval"]!.GetValue<int>()==15,"hy2-port-hop-runtime-schema");Reject(()=>MacImport.Read("hy2://fixture@server.example.invalid:443?mport=300-200"),"hy2-reversed-port-range-rejected");Reject(()=>MacImport.Read("hy2://fixture@server.example.invalid:443?hop-interval=1s"),"hy2-hop-without-ports-rejected");
string cs="TLS_AES_128_GCM_SHA256",fm="{\"tcp\":[{\"type\":\"fragment\",\"settings\":{\"packets\":\"tlshello\",\"length\":\"1-3\",\"delay\":\"0-2\"}}]}";
var trojan=MacImport.Read("trojan://fixture@server.example.invalid:443?security=tls&cs="+cs+"&fm="+Uri.EscapeDataString(fm)).Single();Check(MacProtocols.Extended(trojan.Raw)!.Outbound["streamSettings"]!["tlsSettings"]!["cipherSuites"]!.ToString()==cs,"trojan-cipher-suites-preserved");
string vmess="vmess://"+Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{v="2",ps="fixture",add="server.example.invalid",port=443,id="00000000-0000-4000-8000-000000000001",aid=0,scy="auto",net="tcp",tls="tls",cs,fm}));var vm=MacImport.Read(vmess).Single();Check(MacProtocols.Extended(vm.Raw)!.Outbound["streamSettings"]!["finalmask"]!["tcp"]!.AsArray().Count==1,"vmess-json-cs-fm-runtime");
var vmuri=MacImport.Read("vmess://00000000-0000-4000-8000-000000000001@server.example.invalid:443?security=tls&type=grpc&serviceName=fixture&mode=multi&cipher=auto").Single();Check(MacProtocols.Extended(vmuri.Raw)!.Outbound["streamSettings"]!["grpcSettings"]!["multiMode"]!.GetValue<bool>(),"vmess-uri-dialect-grpc-multi");
Reject(()=>MacImport.Read("trojan://fixture@server.example.invalid:443?cs="+cs+"&allowInsecure=1"),"extended-parser-does-not-disable-tls-verification");Reject(()=>MacCoreOptions.FinalMask("{\"udp\":[{\"type\":\"lua\",\"settings\":{}}]}"),"arbitrary-finalmask-code-rejected");
string awg="[Interface]\nPrivateKey="+key32+"\nAddress=10.0.0.2/32\nJc=3\nJmin=10\nJmax=50\nH1=123\n[Peer]\nPublicKey="+key32+"\nEndpoint=server.example.invalid:51820\nAllowedIPs=0.0.0.0/0, ::/0";var ap=MacImport.Read(awg).Single();Check(ap.Protocol=="AmneziaWG"&&MacProtocols.Extended(awg)!.Outbound["settings"]!["amnezia-wg-option"]!["jc"]!.GetValue<int>()==3,"awg-single-peer-runtime-parameters");Reject(()=>MacImport.Read(awg+"\n[Peer]\nPublicKey="+key32),"awg-multiple-peers-rejected");Reject(()=>MacImport.Read(awg.Replace("Jmin=10","Jmin=100")),"awg-inconsistent-junk-range-rejected");
var runtime=JsonNode.Parse(MacAmnezia.Runtime(ap,10808))!;Check(runtime["outbounds"]!.AsArray().Count==2&&runtime["inbounds"]![0]!["listen"]!.ToString()=="127.0.0.1","awg-only-constrained-loopback-gateway");
Check(ImportReview.Read(awg).Profiles.Single().Raw==awg,"awg-multiline-import-review");
foreach(var editable in new[]{hy,trojan,vm,vmuri,ap}){
 var form=MacProfileEditing.Read(editable)!;var same=MacProfileEditing.Apply(editable,editable.Name,form.Fields);Check(same.Raw==editable.Raw,"extended-editor-noop-keeps-exact-text-"+form.Kind);
 var changed=MacProfileEditing.Apply(editable,"local label",form.Fields);Check(changed.Name=="local label"&&changed.Id==editable.Id&&MacProtocols.Single(changed.Raw).CanConnect,"extended-editor-rename-valid-"+form.Kind);
}
var ah=MacProfileEditing.Read(ap)!;var changes=ah.Fields.ToDictionary();changes["Jc"]="5";var editedAwg=MacProfileEditing.Apply(ap,ap.Name,changes);Check(MacProtocols.Extended(editedAwg.Raw)!.Outbound["settings"]!["amnezia-wg-option"]!["jc"]!.GetValue<int>()==5&&editedAwg.Raw.Contains("H1=123"),"awg-edit-retains-other-options");
var tor=MacProfileEditing.Native("snowflake","Tor","");var doh=MacProfileEditing.Native("doh","DoH","https://dns.example.invalid/dns-query");Check(tor.Protocol=="Tor"&&TorRuntime.Bridges(tor.Raw).Length==0,"tor-native-import-with-default-bridges");Check(JsonNode.Parse(MacProtocols.Runtime(doh,18080,10808))!["dns"]!["servers"]![0]!["skipFallback"]!.GetValue<bool>(),"doh-only-runtime-no-fallback-resolver");
var stored=new Profile{Raw=tor.Raw,Name="local name",Protocol="snowflake",BlockReason="ENGINE_NOT_READY",CollectionId="saved-group"};string storedId=stored.Id;Check(MacProtocols.RefreshCapabilities(stored)&&stored.CanConnect&&stored.Protocol=="Tor","stored-capability-refreshed-after-upgrade");Check(stored.Name=="local name"&&stored.Id==storedId&&stored.CollectionId=="saved-group"&&stored.Raw==tor.Raw,"capability-refresh-preserves-identity-name-and-options");
Reject(()=>MacProfileEditing.Native("snowflake","Tor","obfs4 127.0.0.1:1234 AAA\nClientTransportPlugin exec malicious"),"tor-rejects-config-directive-in-bridge-list");
var source=new Uri("https://subscriptions.example.invalid/extended");ap.ItemId="awg";var data=SubscriptionMerge.Apply(new(),source,[ap],"fixture",null);var prior=data.Profiles.Single();data.OverrideSources[prior.Id]=prior.Raw;data.OverrideNames[prior.Id]=prior.Name;data.Profiles[0]=MacProfileEditing.Apply(prior,"local label",ah.Fields);Check(SubscriptionMerge.Apply(data,source,[ap],"",null).Profiles.Single().Name=="local label","awg-subscription-update-preserves-local-label");
string json=JsonSerializer.Serialize(new{version=1,items=new[]{new{id="awg",link=awg},new{id="vmess",link=vmess},new{id="tor",link=tor.Raw}}});Check(ImportReview.Read(json).Profiles.Count==3,"export-json-reimports-all-extended-families");
string bundle=MacBundleCodec.Seal([vm,tor],"fixture-passphrase");Check(MacBundleCodec.Open(bundle,"fixture-passphrase").Profiles.Count==2,"extended-encrypted-bundle-roundtrip");Reject(()=>MacBundleCodec.Open(bundle,"wrong"),"extended-bundle-authentication");
string commonBundle=BundleCodec.Seal([Importer.Single("trojan://fixture@server.example.invalid:443")],"fixture-passphrase");Check(MacBundleCodec.Open(commonBundle,"fixture-passphrase").Profiles.Count==1,"windows-encrypted-bundle-compatible");
string external=JsonSerializer.Serialize(new{outbounds=new[]{MacProtocols.Extended(vm.Raw)!.Outbound}});var externalReview=ImportReview.Read(external);Check(externalReview.Profiles.Count==1&&externalReview.Notices.Contains("JSON_OUTBOUND_ONLY"),"external-vmess-json-preserves-extensions-and-reports-outbound-only");
string[] supplementalLinks=[
 "anytls://fixture%2Bsecret@server.example.invalid:8443?sni=server.example.invalid&alpn=h2,http%2F1.1&fp=chrome&min-idle-session=2&idle-session-timeout=30#AnyTLS",
 "tuic://00000000-0000-4000-8000-000000000001:fixture%3Asecret@server.example.invalid:443?peer=server.example.invalid&alpn=h3&congestion_control=bbr&udp_relay_mode=native&reduce_rtt=1&heartbeat=10000ms#TUIC",
 "mierus://fixture%3Auser:fixture%2Bsecret@server.example.invalid?profile=Mieru&port=8443&protocol=UDP&multiplexing=MULTIPLEXING_LOW&handshake-mode=HANDSHAKE_STANDARD#Mieru"];
var supplemental=supplementalLinks.Select(raw=>MacImport.Read(raw).Single()).ToArray();
foreach(var profile in supplemental){
 string kind=profile.Protocol;var original=MacProtocols.Extended(profile.Raw)!.Outbound;var form=MacProfileEditing.Read(profile)!;
 Check(profile.CanConnect&&profile.Raw==supplementalLinks[Array.IndexOf(supplemental,profile)],"supplemental-import-retains-exact-text-"+kind);
 Check(MacProfileEditing.Apply(profile,profile.Name,form.Fields).Raw==profile.Raw,"supplemental-editor-noop-retains-exact-text-"+kind);
 var fields=form.Fields.ToDictionary();fields["server"]="edited.example.invalid";fields["port"]="9443";
 var changed=MacProfileEditing.Apply(profile,"Edited profile",fields);var settings=MacProtocols.Extended(changed.Raw)!.Outbound["settings"]!;
 Check(changed.Id==profile.Id&&changed.Name=="Edited profile"&&settings["server"]!.ToString()=="edited.example.invalid"&&settings["port"]!.GetValue<int>()==9443,"supplemental-edit-applies-endpoint-and-preserves-identity-"+kind);
 Check(settings["password"]!.ToString()==original["settings"]!["password"]!.ToString(),"supplemental-edited-encoded-secret-kept-"+kind);
 var config=JsonNode.Parse(MacSupplemental.Runtime(profile,18999))!;
 Check(config["inbounds"]![0]!["port"]!.GetValue<int>()==18999&&JsonNode.DeepEquals(config["outbounds"]![0],original),"supplemental-runtime-uses-selected-profile-"+kind);
 var root=JsonNode.Parse(MacProtocols.Runtime(profile,18080,10808,18999))!;
 Check(root["outbounds"]![0]!["protocol"]!.ToString()=="socks"&&root["outbounds"]![0]!["settings"]!["servers"]![0]!["port"]!.GetValue<int>()==18999,"supplemental-proxy-and-tun-route-through-helper-"+kind);
 Reject(()=>MacImport.Read(profile.Raw.Replace("?","?unknown=1&")),"supplemental-rejects-unknown-option-"+kind);
}
Check(MacImport.Read(string.Join('\n',supplementalLinks)).Count==3,"supplemental-mixed-multiline-import");
Check(MacBundleCodec.Open(MacBundleCodec.Seal(supplemental,"fixture-passphrase"),"fixture-passphrase").Profiles.Count==3,"supplemental-encrypted-bundle-roundtrip");
Check(MacImport.Read("anytls://fixture@server.example.invalid").Single().CanConnect,"anytls-default-port-and-sni");
Check(MacProtocols.Extended(supplemental[2].Raw)!.Outbound["settings"]!["username"]!.ToString()=="fixture:user","mieru-encoded-username-colon-preserved");
foreach(string invalid in new[]{
 "anytls://fixture@server.example.invalid?insecure=1", "tuic://00000000-0000-4000-8000-000000000001:fixture@server.example.invalid?allowInsecure=true",
 "anytls://fixture@server.example.invalid?sni=one.example.invalid&peer=other.example.invalid", "anytls://fixture@server.example.invalid?alpn=%20",
 "anytls://fixture@server.example.invalid?min-idle-session=9", "anytls://fixture@server.example.invalid?fp=unknown", "anytls://fixture@server.example.invalid?sni=a&sni=b",
 "tuic://not-a-uuid:fixture@server.example.invalid", "tuic://00000000-0000-4000-8000-000000000001:%20@server.example.invalid", "tuic://00000000-0000-4000-8000-000000000001:fixture@server.example.invalid?heartbeat=999",
 "mierus://user:fixture@server.example.invalid?profile=M&port=443&protocol=HTTP", "mierus://user:fixture@server.example.invalid:443?profile=M&port=443&protocol=TCP",
 "mierus://user:fixture@server.example.invalid?port=443&protocol=TCP", "mierus://user:fixture@server.example.invalid?profile=M&port=443&protocol=TCP&port=8443",
 "mierus://user:fixture@server.example.invalid?profile=M&port=443&protocol=TCP&traffic-pattern=!!!"})Reject(()=>MacImport.Read(invalid),"supplemental-invalid-case-"+(checks+1));
{
 string raw="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?security=tls&allowInsecure=true#TLS";
 var strict=ImportReview.Read(raw);Check(strict.Profiles.Count==0&&strict.Issues.Single().Code=="TLS_UNVERIFIED","tls-strict-import-remains-strict");
 var review=ImportReview.Read(raw,reviewTlsRequests:true);var p=review.Profiles.Single();
 Check(review.TlsAdjustedCount==1&&review.RequiresConsent&&p.Raw.Contains("allowInsecure=false"),"tls-review-normalizes-request-to-verification-on");
 var state=new AppState{Profiles=[p]};Check(!MacTlsPolicy.Approved(state,p),"tls-import-does-not-authorize-exception");state.TlsExceptions[p.Id]=new(Importer.Fingerprint(p.Raw),new string('a',64));
 Check(MacTlsPolicy.Approved(state,p),"tls-exact-profile-approval-recognized");var copy=MacProtocols.Single(p.Raw);Check(!MacTlsPolicy.Approved(state,copy),"tls-approval-never-transfers-to-another-profile");
 string original=p.Raw;p.Raw=original.Replace(":443",":8443");Check(!MacTlsPolicy.Approved(state,p),"tls-changed-config-invalidates-prior-approval");p.Raw=original;
 Check(MacTlsPolicy.Approved(state.Copy(),p),"tls-approval-survives-encrypted-state-serialization");
 string secure=MacProtocols.Runtime(p,18080,10808),exception=MacProtocols.Runtime(p,18080,10808,tlsCertificatePin:new string('a',64));
 Check(!secure.Contains("pinnedPeerCertSha256")&&exception.Contains("pinnedPeerCertSha256"),"tls-runtime-exception-is-explicit-and-default-stays-verified");
 Check(!MacProtocols.Runtime(p,18080,10808).Contains("pinnedPeerCertSha256"),"tls-runtime-exception-does-not-leak-to-next-session");
 foreach(string link in new[]{"anytls://fixture@server.example.invalid:443?insecure=1","tuic://00000000-0000-4000-8000-000000000001:fixture@server.example.invalid:443?allowInsecure=true","hy2://fixture@server.example.invalid:443?insecure=1","trojan://fixture@server.example.invalid:443?allowInsecure=true"}){
  var normalized=ImportReview.Read(link,reviewTlsRequests:true);Check(normalized.Profiles.Count==1&&normalized.TlsAdjustedCount==1,"tls-normalization-supported-family-"+link.Split(':')[0]);
  if(link.StartsWith("anytls")||link.StartsWith("tuic"))Check(!MacTlsPolicy.Supports(normalized.Profiles.Single().Raw)&&!MacSupplemental.Runtime(normalized.Profiles.Single(),18999).Contains("\"skip-cert-verify\":true"),"supplemental-unsupported-pin-cannot-disable-validation-"+link.Split(':')[0]);
 }
 string tlsBundle=JsonSerializer.Serialize(new{version=1,items=new[]{new{id="tls",link=raw}}});Check(ImportReview.Read(tlsBundle,reviewTlsRequests:true).TlsAdjustedCount==1,"tls-export-json-review-normalizes-per-item");
 string tlsExternal="{\"outbounds\":[{\"protocol\":\"trojan\",\"settings\":{\"servers\":[{\"address\":\"server.example.invalid\",\"port\":443,\"password\":\"fixture\"}]},\"streamSettings\":{\"network\":\"tcp\",\"security\":\"tls\",\"tlsSettings\":{\"allowInsecure\":true}}}]}";
 Check(ImportReview.Read(tlsExternal,reviewTlsRequests:true).TlsAdjustedCount==1,"tls-external-json-review-normalizes-certificate-request");
}
if(args.Length>1){
 EnginePaths.Root=Path.GetFullPath(args[1]);
 async Task Validate(string exe,string text,string name,params string[] command){
  var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in command)start.ArgumentList.Add(a);using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();await process.StandardInput.WriteAsync(text);process.StandardInput.Close();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));await process.WaitForExitAsync(deadline.Token);Check(process.ExitCode==0,name);if(process.ExitCode!=0)Console.WriteLine((await output)+" "+(await errors));
 }
 foreach(var p in new[]{hy,trojan,vm,vmuri,doh,tor})await Validate(EnginePaths.Require("xray"),MacProtocols.Runtime(p,18080,10808,p.Protocol=="Tor"?10809:0),"real-xray-config-accepts-"+p.Protocol+"-"+p.Name,"run","-test","-c","stdin:");
 foreach(var p in supplemental)await Validate(EnginePaths.Require("rovunet-extra"),MacSupplemental.Runtime(p,18999),"real-mihomo-config-accepts-"+p.Protocol,"--check");
 string privateDir=Path.Combine(Path.GetFullPath(args[0]),"tor-config-check");Directory.CreateDirectory(privateDir);await Validate(EnginePaths.Require("tor"),TorRuntime.Config(tor.Raw,10809,privateDir),"real-tor-verifies-default-snowflake-config","--verify-config","-f","-");
}
string resultPath=Path.GetFullPath(args.FirstOrDefault()??"artifacts");Directory.CreateDirectory(resultPath);File.WriteAllText(Path.Combine(resultPath,"protocol-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+"; parser/configuration only",macRuntimeValidated=false,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Protocols: {checks} checks, {failures} failures");return failures==0?0:1;
