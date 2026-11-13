using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;
using Rovunet.Desktop;
int checks=0,failures=0;var results=new List<object>();void Check(bool good,string name){checks++;results.Add(new{name,passed=good});if(!good){failures++;Console.WriteLine("FAIL "+name);}}
string valid="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&path=%2Fdemo#fixture";

string EncryptMahsa(object value){using var aes=Aes.Create();aes.Key=Encoding.ASCII.GetBytes("ca815ecfdb1f153a");return Convert.ToBase64String(aes.EncryptCbc(JsonSerializer.SerializeToUtf8Bytes(value),Encoding.ASCII.GetBytes("lvcas56410c97lpb"),PaddingMode.PKCS7));}
void RejectMahsa(string body,string name){try{_=PublicFeed.DecodeMahsa(body);Check(false,name);}catch(SafeError){Check(true,name);}}
string fresh=valid.Replace("000000000001","000000000002").Replace("#fixture","#fresh");
string removed=valid.Replace("000000000001","000000000003").Replace("#fixture","#removed");
string encryptedMahsa=EncryptMahsa(new{mci=new[]{new{config=valid,ads_url="https://ads.example.invalid/never"},new{config=fresh,ads_url="https://ads.example.invalid/never"}},mtn=new[]{new{config=valid,ads_url="https://ads.example.invalid/never"}},ads_url="https://ads.example.invalid/root"});
string decodedMahsa=PublicFeed.DecodeMahsa(encryptedMahsa);
Check(decodedMahsa.Split('\n').Length==2&&!decodedMahsa.Contains("ads.example.invalid"),"mahsa-extract-config-only-and-deduplicate");
Check(PublicFeed.Presets.Length==3&&PublicFeed.Presets.Select(p=>p.Key).ToHashSet().SetEquals(new[]{"mahsa","anonymous","patterniha"}),"three-supported-public-sources-remain-prepared");
Check(PublicFeed.IsKnown(PublicFeed.LegacyMahsaUrl)&&PublicFeed.IsKnown(PublicFeed.MahsaUrl),"legacy-and-direct-mahsa-recognized");
Check(!PublicFeed.IsKnown("https://example.invalid/mahsa")&&!PublicFeed.IsKnown("https://raw.githubusercontent.com/other/project/main/app/sub.txt"),"ordinary-subscriptions-not-reclassified");
var requests=new List<string>();
Task<SubscriptionClient.Payload> FetchFixture(Uri uri,int proxy,CancellationToken ct){ct.ThrowIfCancellationRequested();requests.Add(uri.AbsoluteUri);Check(proxy==18089,"mahsa-fetch-keeps-active-proxy");return Task.FromResult(new SubscriptionClient.Payload(encryptedMahsa,null,""));}
var received=await PublicFeed.Fetch(PublicFeed.LegacyMahsaUrl,proxyPort:18089,download:FetchFixture);
Check(received.Body==decodedMahsa&&requests.SequenceEqual(new[]{PublicFeed.MahsaUrl}),"legacy-mahsa-mapped-before-single-direct-request-no-hiddify-or-ads");
var legacyIdentity=new Uri(PublicFeed.LegacyMahsaUrl);var baseline=SubscriptionMerge.Apply(new AppState(),legacyIdentity,[MacProtocols.Single(valid),MacProtocols.Single(removed)],"Mahsa",null);
var other=MacProtocols.Single(valid.Replace("#fixture","#other-source"));var otherIdentity=new Uri("https://other.example.invalid/sub");baseline=SubscriptionMerge.Apply(baseline,otherIdentity,[other],"Other",null);
var manual=MacProtocols.Single(valid.Replace("#fixture","#manual"));baseline.Profiles.Add(manual);string before=JsonSerializer.Serialize(baseline);
var updated=PublicFeed.ApplyMahsa(baseline,legacyIdentity,received.Body,"Mahsa");string source=Importer.Fingerprint(legacyIdentity.AbsoluteUri);
Check(updated.Profiles.Count(p=>p.SourceId==source)==2&&updated.Profiles.All(p=>p.Raw!=removed),"mahsa-success-replaces-and-removes-only-obsolete-own-items");
Check(updated.Subscriptions.Single(p=>p.Id==source).Url==PublicFeed.LegacyMahsaUrl,"legacy-subscription-identity-preserved-while-request-is-direct");
Check(updated.Profiles.Any(p=>p.Id==manual.Id&&p.SourceId.Length==0)&&updated.Subscriptions.Any(p=>p.Url==otherIdentity.AbsoluteUri)&&updated.Profiles.Any(p=>p.SourceId==Importer.Fingerprint(otherIdentity.AbsoluteUri)),"other-subscriptions-and-manual-profiles-preserved");
Check(JsonSerializer.Serialize(baseline)==before,"successful-merge-does-not-mutate-previous-state");
foreach(var entry in new[]{("not base64","invalid-base64"),(Convert.ToBase64String(new byte[17]),"invalid-ciphertext-block"),(EncryptMahsa(new{mci=Array.Empty<object>(),mtn=Array.Empty<object>()}),"empty"),(EncryptMahsa(new{mci=new[]{new{config=valid}},mtn=new[]{new{config="unknown://bad"}}}),"one-invalid-profile-rejects-full-result"),(EncryptMahsa(new{mci=new[]{new{config=valid+"\n"+fresh}},mtn=Array.Empty<object>()}),"multiline-config"),(EncryptMahsa(new{mci="wrong-shape",mtn=Array.Empty<object>()}),"invalid-array-shape")}){
 RejectMahsa(entry.Item1,"mahsa-"+entry.Item2);Check(JsonSerializer.Serialize(baseline)==before,"old-data-intact-after-"+entry.Item2);
}
byte[] corrupted=Convert.FromBase64String(encryptedMahsa);corrupted[^1]^=1;RejectMahsa(Convert.ToBase64String(corrupted),"mahsa-padding-tamper-rejected");
try{_=PublicFeed.ApplyMahsa(baseline,legacyIdentity,valid+"\nunknown://bad","Mahsa");Check(false,"partial-validation-cannot-delete-old-items");}catch(SafeError){Check(JsonSerializer.Serialize(baseline)==before,"partial-validation-cannot-delete-old-items");}
try{_=await PublicFeed.Fetch(PublicFeed.LegacyMahsaUrl,proxyPort:18089,download:(_,_,_)=>throw new SafeError("FETCH_TIMEOUT"));Check(false,"mahsa-network-failure-preserves-old");}catch(SafeError){Check(JsonSerializer.Serialize(baseline)==before,"mahsa-network-failure-preserves-old");}
using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{_=await PublicFeed.Fetch(PublicFeed.LegacyMahsaUrl,cancelled.Token,18089,FetchFixture);Check(false,"mahsa-cancellation-preserves-old");}catch(OperationCanceledException){Check(JsonSerializer.Serialize(baseline)==before,"mahsa-cancellation-preserves-old");}}
int patternRequests=0;var patternPayload=await PublicFeed.Fetch(PublicFeed.PatternihaUrl,download:(uri,_,_)=>{patternRequests++;Check(uri.AbsoluteUri==PublicFeed.PatternihaUrl,"patterniha-fetch-uses-reviewed-source-url");return Task.FromResult(new SubscriptionClient.Payload(valid,null,""));});Check(patternRequests==1&&patternPayload.Body==valid&&PublicFeed.IsKnown(PublicFeed.PatternihaUrl),"patterniha-update-is-available-again");
// Mixed TLS-policy rejections must reach explicit partial review without
// making unverified profiles connectable or relaxing strict decoder callers.
string unsafeTls=valid.Replace("#fixture","&allowInsecure=true#unverified");
string mixedTls=EncryptMahsa(new{mci=new[]{new{config=valid},new{config=unsafeTls}},mtn=Array.Empty<object>()});
RejectMahsa(mixedTls,"mixed-tls-strict-decoder-still-atomic");
var tlsPayload=await PublicFeed.Fetch(PublicFeed.MahsaUrl,download:(_,_,_)=>Task.FromResult(new SubscriptionClient.Payload(mixedTls,null,"")));
var tlsReview=ImportReview.Read(tlsPayload.Body);
Check(tlsReview.Profiles.Count==1&&tlsReview.Rejected==1&&tlsReview.Issues.Single().Code=="TLS_UNVERIFIED","mixed-tls-fetch-offers-safe-item-and-explicit-rejection");
Check(tlsReview.Profiles.All(p=>!p.Raw.Contains("allowInsecure=true")),"tls-review-never-admits-unverified-config");
Check(JsonSerializer.Serialize(baseline)==before,"tls-review-fetch-never-mutates-stored-profiles");
string allTls=EncryptMahsa(new{mci=new[]{new{config=unsafeTls}},mtn=Array.Empty<object>()});
var allTlsReview=ImportReview.Read(PublicFeed.DecodeMahsa(allTls,reviewTlsRejections:true));
Check(allTlsReview.Profiles.Count==0&&allTlsReview.Rejected==1,"all-unverified-feed-has-no-connectable-profiles");
try{_=PublicFeed.DecodeMahsa(EncryptMahsa(new{mci=new[]{new{config=valid},new{config="unknown://bad"}},mtn=Array.Empty<object>()}),reviewTlsRejections:true);Check(false,"tls-review-still-rejects-structural-or-other-profile-errors");}catch(SafeError){Check(true,"tls-review-still-rejects-structural-or-other-profile-errors");}

string? suppliedMahsa=Environment.GetEnvironmentVariable("ROVUNET_MAHSA_SAMPLE");
if(suppliedMahsa!=null&&File.Exists(suppliedMahsa)){
 string sampleInput=await File.ReadAllTextAsync(suppliedMahsa);
 using var sampleAes=Aes.Create();sampleAes.Key=Encoding.ASCII.GetBytes("ca815ecfdb1f153a");
 byte[] sampleClear=sampleAes.DecryptCbc(Convert.FromBase64String(sampleInput),Encoding.ASCII.GetBytes("lvcas56410c97lpb"),PaddingMode.PKCS7);
 using var sampleJson=JsonDocument.Parse(sampleClear);var safeLinks=new List<string>();int unsafeCount=0;
 foreach(string network in new[]{"mci","mtn"})foreach(var row in sampleJson.RootElement.GetProperty(network).EnumerateArray()){
  string raw=row.GetProperty("config").GetString()??"";var review=ImportReview.Read(raw);
  if(review.Rejected==0&&review.Profiles.Count==1)safeLinks.Add(raw);
  else{unsafeCount++;Check(review.Issues.All(i=>i.Code=="TLS_UNVERIFIED"),"supplied-feed-rejection-is-explicit-tls-policy");}
 }
 Check(safeLinks.Count>0&&unsafeCount>0,"supplied-feed-decrypts-but-contains-unverified-tls");
 RejectMahsa(sampleInput,"supplied-mixed-trust-feed-preserves-atomic-validation");
 Check(JsonSerializer.Serialize(baseline)==before,"supplied-mixed-trust-feed-preserves-previous-state");
 string safeFixture=EncryptMahsa(new{mci=safeLinks.Select(config=>new{config}).ToArray(),mtn=Array.Empty<object>()});
 Check(ImportReview.Read(PublicFeed.DecodeMahsa(safeFixture)).Rejected==0,"supplied-valid-protocols-survive-strict-decoder");
 CryptographicOperations.ZeroMemory(sampleClear);
}
string? negativeMahsa=Environment.GetEnvironmentVariable("ROVUNET_MAHSA_NEGATIVE_SAMPLE");if(negativeMahsa!=null&&File.Exists(negativeMahsa)){RejectMahsa(await File.ReadAllTextAsync(negativeMahsa),"supplied-other-ciphertext-rejected-without-reset");Check(JsonSerializer.Serialize(baseline)==before,"supplied-invalid-response-preserves-previous-state");}

var mixed=ImportReview.Read(valid+"\nanytls://fixture@server.example.invalid:443\n"+valid.Replace("#fixture","&unknown=1#fixture")+"\nmieru://fixture@server.example.invalid:443\n");
Check(mixed.Profiles.Count==2&&mixed.Profiles.Any(p=>p.Protocol=="AnyTLS")&&mixed.Rejected==2,"mixed-feed-retains-newly-supported-and-reports-rejections");
Check(mixed.Issues.Any(i=>i.Option=="mieru"),"unsupported-full-mieru-format-name-recorded");
var base64=ImportReview.Read(Convert.ToBase64String(Encoding.UTF8.GetBytes(valid+"\n# comment\n")));Check(base64.Profiles.Count==1&&base64.Rejected==0,"base64-subscription-decoded-once");
var empty=ImportReview.Read("mieru://fixture@server.example.invalid:443");Check(empty.Profiles.Count==0&&empty.Rejected==1,"all-unsupported-retains-reason-without-replacement");
bool protectedRejected=false;try{ImportReview.Read(valid+"\nrovunet-enc://fixture");}catch{protectedRejected=true;}Check(protectedRejected,"protected-item-keeps-atomic-rejection");
foreach(var bank in ResolverCatalog.Banks){var sample=ResolverCatalog.Candidates(bank.File,"tcp",64);Check(ResolverCatalog.Count(bank)>0&&sample.Count is >0 and <=64&&sample.All(v=>v.EndsWith(":53",StringComparison.Ordinal)),"offline-bank-"+bank.File);}
var live=new List<object>();if(args.Contains("--live"))foreach(var preset in PublicFeed.Presets){try{var payload=await PublicFeed.Fetch(preset.Url);var review=ImportReview.Read(payload.Body);var record=new{name=preset.Name,fetchedAt=DateTimeOffset.UtcNow,sha256=Importer.Fingerprint(payload.Body),bytes=Encoding.UTF8.GetByteCount(payload.Body),accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0,"live-public-feed-"+preset.Key);}catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name=preset.Name,error=code});Console.WriteLine(preset.Name+": "+code);Check(false,"live-public-feed-"+preset.Key);}}
int privateIndex=Array.IndexOf(args,"--private-body-dir");
if(privateIndex>=0)foreach(string file in Directory.GetFiles(args[privateIndex+1],"subscription-repro-*.data")){
    string name=Path.GetFileNameWithoutExtension(file);
    try{
        var review=ImportReview.Read(await File.ReadAllTextAsync(file));
        var record=new{name,accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};
        live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0,"owner-body-accepted-"+name);
    }catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name,error=code});Console.WriteLine(JsonSerializer.Serialize(new{name,error=code}));Check(false,"owner-body-accepted-"+name);}
}
int sourcesIndex=Array.IndexOf(args,"--sources-file");
if(sourcesIndex>=0){
    var addresses=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[sourcesIndex+1]))!;
    for(int i=0;i<addresses.Length;i++){
        string name="owner-subscription-"+(i+1);
        try{
            var payload=await SubscriptionClient.Fetch(SubscriptionUrls.Validate(addresses[i]));var review=ImportReview.Read(payload.Body);
            var record=new{name,downloaded=true,accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};
            live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0&&review.Rejected==0,"live-owner-subscription-accepted-"+(i+1));
        }catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name,error=code});Console.WriteLine(JsonSerializer.Serialize(new{name,error=code}));Check(false,"live-owner-subscription-accepted-"+(i+1));}
    }
}
foreach(string prefix in new[]{"/sub/","/v2/sub/"})foreach(string suffix in new[]{".json",".windows.json"}){
    var url=new Uri("https://panel.invalid:9443"+prefix+"private_token-12"+suffix+"?k=one");
    Check(MacPanelStatus.StatusUri(url)?.AbsoluteUri==url.AbsoluteUri.Replace(".json?",".status.json?"),"private-status-exact-alias-"+prefix+suffix);
}
foreach(string path in new[]{"a/b.json","a%2Fb.json","a.other.json","a.status.json",".json",".windows.json","a.json/"})
    Check(MacPanelStatus.StatusUri(new Uri("https://panel.invalid/v2/sub/"+path))==null,"unrecognized-private-alias-"+path);
var privateUri=new Uri("https://panel.invalid/v2/sub/test.json");
Check(!MacPanelStatus.SameAuthority(privateUri,new Uri("https://other.invalid/list"))&&!MacPanelStatus.SameAuthority(privateUri,new Uri("https://panel.invalid:444/list"))&&!MacPanelStatus.SameAuthority(privateUri,new Uri("http://panel.invalid/list")),"private-redirect-rejects-authority-port-downgrade");
bool rejectedShape=false;try{MacPanelStatus.ReadFeed(privateUri,valid);}catch(SafeError){rejectedShape=true;}Check(rejectedShape,"v2-refuses-freeform-feed");
var official=JsonSerializer.Serialize(new{version=1,items=new[]{new{id="stable-1",link=valid}}});
Check(MacPanelStatus.ReadFeed(privateUri,official).Profiles.Single().ItemId=="stable-1","v2-strict-v1-body");
Profile Item(string raw){var item=MacProtocols.Single(raw);item.ItemId="same";return item;}
var origin=SubscriptionMerge.Apply(new(),privateUri,[Item(valid)],"A",null);var savedId=origin.Profiles.Single().Id;origin.SelectedId=savedId;origin.Pinned.Add(savedId);
origin.TlsExceptions[savedId]=new(Importer.Fingerprint(valid),new string('a',64));
var otherAddress=new Uri("https://other.invalid/v2/sub/test.windows.json");
var withClone=SubscriptionMerge.Apply(origin,otherAddress,[origin.Profiles.Single()],"B",null);
Check(withClone.Profiles.Select(p=>p.Id).Distinct().Count()==2,"same-source-item-namespace-clone-has-separate-id");
foreach(var changed in new[]{fresh,valid.Replace("/demo","/v2"),valid.Replace("%2Fdemo","%2Fv2"),valid.Replace("#fixture","#new-name")}){
    var next=SubscriptionMerge.Apply(withClone,privateUri,[Item(changed)],"A",null);
    Check(next.SelectedId==savedId&&next.Pinned.Contains(savedId)&&next.Profiles.Single(p=>p.Id==savedId).Raw==changed,"same-endpoint-refresh-preserves-row-selection-"+Importer.Fingerprint(changed)[..8]);
}
var moved=SubscriptionMerge.Apply(withClone,privateUri,[Item(valid.Replace("server.example.invalid","different.invalid"))],"A",null);
Check(moved.Profiles.All(p=>p.Id!=savedId)&&moved.SelectedId==""&&!moved.TlsExceptions.ContainsKey(savedId)&&!moved.Pinned.Contains(savedId)&&moved.Profiles.Any(p=>p.SourceId==Importer.Fingerprint(otherAddress.AbsoluteUri)),"replaced-endpoint-removes-old-trust-and-selection-and-preserves-other-source");
var again=SubscriptionMerge.Apply(moved,privateUri,[Item(valid)],"A",null);
Check(again.Profiles.All(p=>p.Id!=savedId)&&again.SelectedId=="","reappearance-needs-fresh-local-identity");
origin.OverrideSources[savedId]=valid;origin.OverrideNames[savedId]="fixture";var originalData=JsonSerializer.Serialize(origin);
try{_=SubscriptionMerge.Apply(origin,privateUri,[Item(valid.Replace("server.example.invalid","changed.invalid"))],"A",null);Check(false,"local-override-endpoint-conflict");}catch(SafeError e){Check(e.Code=="OVERRIDE_CONFLICT"&&originalData==JsonSerializer.Serialize(origin),"local-override-endpoint-conflict");}
string output=args.FirstOrDefault(a=>!a.StartsWith("--"))??"artifacts";Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"feed-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+"",macRuntimeValidated=false,checks,failures,results,live},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Feeds/banks: {checks} checks, {failures} failures");return failures==0?0:1;
