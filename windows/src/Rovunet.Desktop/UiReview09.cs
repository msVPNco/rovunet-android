using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
sealed class Review09Http(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken stop)=>Task.FromResult(respond(request));
}
public sealed partial class MainWindow
{
 internal async Task<bool> RunReview09(string inputs,string report)
 {
  var checks=new Dictionary<string,bool>();var evidence=new List<object>();var requests=new List<string>();
  string store=Path.Combine(Path.GetDirectoryName(report)!,"store-review09");
  var clock=DateTimeOffset.Parse("2026-11-08T10:00:00Z");
  var urls=new[]{new Uri("https://panel.example.invalid/sub/alpha.json"),new Uri("https://panel.example.invalid/sub/beta.windows.json")};
  string feed=File.ReadAllText(Path.Combine(inputs,"feeds","old20.json"));
  string status=File.ReadAllText(Path.Combine(inputs,"panel-status","active.status.json"));
  string betaStatus=status.Replace("abababababababababababababababab","56565656565656565656565656565656");
  string JsonState()=>JsonSerializer.Serialize(new{data.Profiles,data.Subscriptions,data.LocalOverrideSources,data.LocalOverrideNames,data.SelectedId});
  try {
   storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
   SubscriptionClient.HttpFixture=_=>new Review09Http(request=>{
    string path=request.RequestUri!.AbsolutePath;requests.Add(path);
    var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(path.EndsWith(".status.json")?(path.Contains("beta.windows")?betaStatus:status):feed)};
    response.Headers.Date=clock;return response;
   });
   foreach(var uri in urls)CommitSubscription20(uri,Importer.ReadCompatible(feed,true).Profiles,"Review09");
   string a=Importer.Fingerprint(urls[0].AbsoluteUri),b=Importer.Fingerprint(urls[1].AbsoluteUri);
   var chosen=data.Profiles.First(p=>p.SourceId==a);
   data.LocalOverrideSources[chosen.Id]=chosen.Raw;data.LocalOverrideNames[chosen.Id]=chosen.Name;chosen.Name="Local review09";data.SelectedId=chosen.Id;Save();
   var other=JsonSerializer.Serialize(data.Profiles.Where(p=>p.SourceId==b));
   async Task Refresh(string source) {
    var menu=CollectionMenu(data.Profiles.Where(p=>p.SourceId==source).ToList());
    menu.Items.OfType<MenuItem>().Single(item=>Equals(item.Tag,"UPDATE")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    var until=DateTime.UtcNow.AddSeconds(8);
    do{await Task.Delay(20);await Idle18();}while(updatingSubscriptions20.Contains(source)&&DateTime.UtcNow<until);
    if(updatingSubscriptions20.Contains(source))throw new TimeoutException();
   }
   foreach(string phase in new[]{"active","partial","expired","renewed","limit-reached"}) {
    status=File.ReadAllText(Path.Combine(inputs,"panel-status",phase+".status.json"));
    feed=File.ReadAllText(Path.Combine(inputs,"feeds","unlimited.json"));
    await Refresh(a);await Refresh(b);
    var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
    var account=AccountStatus.Read(saved.Subscriptions.Single(sub=>sub.Id==a).StatusBody);
    var snapshot=statuses[a];var view=snapshot.Evaluate();
    var quota=groups.Single(group=>group.Members[0].SourceId==a).Quota!;
    quota.Update(view);await Idle18();
    checks[phase+"_status_persisted"]=account.Revision==AccountStatus.Read(status).Revision&&account.AccountId=="abababababababababababababababab";
        var betaAccount=AccountStatus.Read(saved.Subscriptions.Single(sub=>sub.Id==b).StatusBody);
    checks[phase+"_distinct_account_isolated"]=betaAccount.AccountId=="56565656565656565656565656565656"&&betaAccount.Revision==1&&betaAccount.State=="active"&&betaAccount.PeriodId=="cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd";
    checks[phase+"_source_scope"]=saved.Profiles.Count(p=>p.SourceId==a)==24&&saved.Profiles.Count(p=>p.SourceId==b)==24;
    checks[phase+"_selection_override"]=saved.SelectedId==chosen.Id&&saved.Profiles.Single(p=>p.Id==chosen.Id).Name=="Local review09";
    checks[phase+"_quota_visibility"]=phase!="partial"||!Visuals<ProgressBar>(quota.Root).Single().IsVisible;
    checks[phase+"_period"]=account.PeriodId==(phase is "renewed" or "limit-reached"?"efefefefefefefefefefefefefefefef":"cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd");
    evidence.Add(new{phase,clock,betaAccount=betaAccount.AccountId,betaRevision=betaAccount.Revision,betaStatusHash=Importer.Fingerprint(betaStatus),account.AccountId,account.PeriodId,account.Revision,view.Trustworthy,quota.Used.Text,profiles=JsonSerializer.Serialize(saved.Profiles),saved.LocalOverrideSources,saved.LocalOverrideNames,saved.SelectedId,hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(store,"profiles.dpapi"))))});
   }
   // The exact November 8 bytes are deliberately stale under a later response clock.
   var stale=new StatusSnapshot{Status=AccountStatus.Read(File.ReadAllText(Path.Combine(inputs,"panel-status","active.status.json"))),ServerDate=clock.AddDays(5)};
   checks["fixed_fixture_stale_under_later_clock"]=!stale.Evaluate().Trustworthy;
   checks["actual_status_aliases"]=requests.Contains("/sub/alpha.status.json")&&requests.Contains("/sub/beta.windows.status.json");
   checks["status_not_feed"]=requests.Count(path=>path.EndsWith(".status.json"))==10;
   // Name/raw equal paths must refuse an endpoint change before carrying local IDs.
   feed=File.ReadAllText(Path.Combine(inputs,"feeds","multi-before.json"));status=File.ReadAllText(Path.Combine(inputs,"panel-status","limit-reached.status.json"));
   // Use a separate source for the positional identity regression.
   var positional=new Uri("https://panel.example.invalid/sub/multi.json");string pos=Importer.Fingerprint(positional.AbsoluteUri);
   CommitSubscription20(positional,Importer.ReadCompatible(feed,true).Profiles,"Multi");
   var previous=data.Profiles.First(p=>p.SourceId==pos);
   data.SelectedId=previous.Id;data.LocalOverrideSources[previous.Id]=previous.Raw;data.LocalOverrideNames[previous.Id]=previous.Name;previous.Name="Positional local";
   Save();string before=JsonState();string positionalBefore=JsonSerializer.Serialize(data.Profiles.Where(p=>p.SourceId==pos));string beta=JsonSerializer.Serialize(data.Profiles.Where(p=>p.SourceId==b));
   feed=File.ReadAllText(Path.Combine(inputs,"feeds","multi-reordered.json"));await Refresh(pos);
   checks["positional_endpoint_change_keeps_profiles"]=data.Profiles.Where(p=>p.SourceId==pos).Select(p=>p.Raw).SequenceEqual(PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!.Profiles.Where(p=>p.SourceId==pos).Select(p=>p.Raw))&&data.Profiles.Single(p=>p.Id==previous.Id).Name=="Positional local";
   checks["positional_full_profiles_preserved"]=positionalBefore==JsonSerializer.Serialize(data.Profiles.Where(p=>p.SourceId==pos));
   evidence.Add(new{phase="endpoint-change",before,after=JsonState()});
   checks["positional_refresh_refused"]=notice.Text.Contains(L.Text("SUBSCRIPTION_IDENTITY_CONFLICT"))&&data.SelectedId==previous.Id;
   checks["second_source_not_reassigned"]=beta==JsonSerializer.Serialize(data.Profiles.Where(p=>p.SourceId==b));
   var original=Importer.Single("socks5://127.0.0.1:18111#Base");
   var remote=Importer.Single("socks5://127.0.0.2:18111#Remote");
   original.Name="Local";
   bool Rejects(Action action){try{action();return false;}catch(SafeError e){return e.Code=="SUBSCRIPTION_IDENTITY_CONFLICT";}}
   checks["name_only_endpoint_change_rejected"]=Rejects(()=>ProfileEditing.MergeSubscription(original.Raw,original,remote,"Base"));
   checks["raw_equal_endpoint_change_rejected"]=Rejects(()=>ProfileEditing.MergeSubscription(original.Raw,Importer.Single(original.Raw),remote));
   var stable=Importer.Single(original.Raw);
   checks["same_endpoint_keeps_name"]=ProfileEditing.MergeSubscription(original.Raw,original,stable,"Base").Name=="Local";
   var edited=Importer.Single("socks5://local:pass@127.0.0.1:18111#Local");
   checks["same_endpoint_keeps_valid_raw_override"]=ProfileEditing.MergeSubscription(original.Raw,edited,stable,"Base").Raw==edited.Raw;
   var chain=Importer.Single(ChainProfiles.Create("Original",new[]{Importer.Single("socks5://127.0.0.1:18111#A"),Importer.Single("socks5://127.0.0.2:18112#B")}));
   var renamed=Importer.Single(ChainProfiles.Create("Renamed",ChainProfiles.Read(chain.Raw).Layers));
   checks["chain_display_name_does_not_change_endpoint_binding"]=SubscriptionEndpoint.Binding(chain)==SubscriptionEndpoint.Binding(renamed);
   foreach(string length in new[]{"200","201"}) {
    bool accepted;try{_=Importer.ReadCompatible(File.ReadAllText(Path.Combine(inputs,"unicode-id-"+length+".json")),true);accepted=true;}catch(SafeError){accepted=false;}
    checks["unicode_"+length]=accepted==(length=="200");
   }
  }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
  finally{SubscriptionClient.HttpFixture=null;storeSaveFixture28=null;}
  bool passed=checks.Count>0&&checks.Values.All(value=>value);
  File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,requests,evidence,clock,syntheticHttp=true,realNetworkTlsExercised=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
 }
}
