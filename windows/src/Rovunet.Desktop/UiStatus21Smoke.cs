using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
sealed class Status21Http(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken stop)=>respond(request,stop);
}
public sealed partial class MainWindow
{
    internal async Task<bool> RunStatus21(string report)
    {
        var start=DateTimeOffset.UtcNow;var checks=new Dictionary<string,bool>();var captures=new List<object>();string dir=Path.GetDirectoryName(report)!;Directory.CreateDirectory(dir);string store=Path.Combine(dir,"private-fixture");
        string Body(long revision=1,long? confirmed=1000000000,long? pending=500000000,Action<JsonObject>? edit=null){var o=new JsonObject{
            ["version"]=2,["server_id"]=new string('a',32),["account_id"]=new string('b',32),["period_id"]=new string('c',32),["revision"]=revision,["account_status"]="active",
            ["limit_kind"]="finite",["limit_bytes"]=10000000000L,["expiry_kind"]="at",["expires_at"]=DateTimeOffset.UtcNow.AddDays(8).ToString("O",CultureInfo.InvariantCulture),
            ["confirmed_bytes"]=confirmed,["pending_bytes"]=pending,["coverage"]="partial",["measurement"]="accounted_lower_bound",["sampled_at"]=null,["generated_at"]=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture)};edit?.Invoke(o);return o.ToJsonString();}
        HttpResponseMessage Response(string body,HttpStatusCode code=HttpStatusCode.OK){var r=new HttpResponseMessage(code){Content=new StringContent(body)};r.Headers.Date=DateTimeOffset.UtcNow;r.Content.Headers.ContentType=new("application/json");return r;}
        try{
            data.Profiles=[];data.Subscriptions=[];statuses.Clear();statusBodies.Clear();ownAccountSources20.Clear();
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            string token=new('a',32);var uri=new Uri("https://status.example.invalid/v2/sub/"+token+".windows.json");
            string feed=JsonSerializer.Serialize(new{version=1,name="Status21",items=new[]{new{id="a",link="socks5://127.0.0.1:19101#A"},new{id="b",link="socks5://127.0.0.1:19102#B"}}});
            var requests=new List<string>();string body=Body();
            Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond=(q,t)=>Task.FromResult(Response(q.RequestUri!.AbsolutePath.EndsWith(".status.json",StringComparison.Ordinal)?body:feed));
            SubscriptionClient.HttpFixture=_=>new Status21Http((q,t)=>{requests.Add(q.RequestUri!.AbsolutePath);return respond(q,t);});
            CommitSubscription20(uri,Importer.ReadCompatible(feed,true).Profiles,"Status21");var sub=data.Subscriptions.Single();string id=sub.Id;
            await RefreshStatus(uri,sub);
            checks["feed1_and_status2_are_independent"]=sub.StatusSchemaVersion==2&&statuses[id].Status!.BodyVersion==2&&data.Profiles.Count==2;
            checks["exact_windows_status_endpoint"]=requests.Single()=="/v2/sub/"+token+".windows.status.json";
            var persisted=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
            checks["body_and_highwater_saved_together"]=PrivateStatusProtocol.ReadStored(persisted.Subscriptions.Single()).UsedBytes==1000000000&&persisted.Subscriptions.Single().Status2Authority!.Periods.Single().Value.Revision==1;
            string baseline=sub.StatusBody;var authority=sub.Status2Authority;
            CaptureUsage20(id,new("",DateTimeOffset.UtcNow,"upload=0;download=0;total=0;expire=0"));
            checks["provider_cannot_replace_private_highwater"]=sub.StatusBody==baseline&&ReferenceEquals(authority,sub.Status2Authority)&&statuses[id].Status!.LimitBytes==10000000000;
            body=Body(99,edit:o=>o["account_id"]=new string('d',32));await RefreshStatus(uri,sub);
            checks["higher_revision_different_binding_refused"]=sub.StatusBody==baseline&&statuses[id].Error=="ACCOUNT_BINDING_CHANGED";
            string profileBaseline=JsonSerializer.Serialize(data.Profiles);int requestsBeforeBinding=requests.Count;await UpdateCollection20(id);
            checks["binding_conflict_cannot_apply_another_account_feed"]=profileBaseline==JsonSerializer.Serialize(data.Profiles)&&requests.Count==requestsBeforeBinding+1;
            foreach(var code in new[]{HttpStatusCode.NotFound,HttpStatusCode.ServiceUnavailable,HttpStatusCode.Redirect}){
                respond=(q,t)=>Task.FromResult(Response("unavailable",code));int count=requests.Count;await RefreshStatus(uri,sub);
                checks["failure_"+(int)code+"_preserves_cache_no_fallback"]=sub.StatusBody==baseline&&ReferenceEquals(sub.Status2Authority,authority)&&requests.Count==count+1&&!statuses[id].Evaluate().Trustworthy&&data.Profiles.Count==2;
            }
            respond=(q,t)=>Task.FromResult(Response("{bad"));await RefreshStatus(uri,sub);
            checks["malformed_preserves_raw_and_profiles"]=sub.StatusBody==baseline&&data.Profiles.Count==2;
            body=Body(2,2000000000);respond=(q,t)=>Task.FromResult(Response(body));
            storeSaveFixture28=()=>throw new IOException("synthetic save failure");await RefreshStatus(uri,sub);
            checks["failed_save_never_publishes_body_or_highwater"]=sub.StatusBody==baseline&&ReferenceEquals(sub.Status2Authority,authority)&&statusBodies[id]==baseline&&statuses[id].Status!.Revision==1;
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            await RefreshStatus(uri,sub);checks["valid_refresh_commits_after_save"]=sub.StatusSchemaVersion==2&&statuses[id].Status!.Revision==2&&sub.Status2Authority!.Periods.Single().Value.ConfirmedBytes==2000000000;
            var gate=new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);var began=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            respond=async(q,t)=>{began.SetResult();return await gate.Task.WaitAsync(t);};var late=RefreshStatus(uri,sub);await began.Task;
            body=Body(4,4000000000);respond=(q,t)=>Task.FromResult(Response(body));await RefreshStatus(uri,sub);
            gate.SetResult(Response(Body(3,3000000000)));await late;
            checks["late_concurrent_fetch_cannot_overwrite_newer"]=statuses[id].Status!.Revision==4&&sub.Status2Authority!.Periods.Single().Value.Revision==4;
            var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;var restart=new MainWindow(saved,true);
            try{checks["restart_keeps_private_binding_highwater_and_stale_label"]=restart.statuses[id].Status!.Revision==4&&!restart.statuses[id].Evaluate().Fresh&&restart.ownAccountSources20.Contains(id);}finally{restart.Close();}
            // Response already in flight when URL rotates must not commit to the source.
            gate=new(TaskCreationOptions.RunContinuationsAsynchronously);began=new(TaskCreationOptions.RunContinuationsAsynchronously);
            respond=async(q,t)=>{began.SetResult();return await gate.Task.WaitAsync(t);};late=RefreshStatus(uri,sub);await began.Task;
            var rotated=new Uri("https://status.example.invalid/v2/sub/"+new string('e',32)+".windows.json");sub.Url=rotated.AbsoluteUri;gate.SetResult(Response(Body(5,5000000000)));await late;
            checks["rotated_url_rejects_late_old_response"]=sub.Status2Authority!.Periods.Single().Value.Revision==4;
            var chosen=data.Profiles.First(p=>p.ItemId=="a");data.SelectedId=chosen.Id;data.PinnedProfileIds.Add(chosen.Id);data.LocalOverrideSources[chosen.Id]=chosen.Raw;data.LocalOverrideNames[chosen.Id]=chosen.Name;chosen.Name="Local override";Save();
            var reordered=JsonSerializer.Serialize(new{version=1,name="Status21",items=new[]{new{id="b",link="socks5://127.0.0.1:19102#B"},new{id="a",link="socks5://127.0.0.1:19101#A"}}});
            CommitSubscription20(rotated,Importer.ReadCompatible(reordered,true).Profiles,"Status21",id);sub=data.Subscriptions.Single();
            body=Body(5,5000000000);respond=(q,t)=>Task.FromResult(Response(body));await RefreshStatus(rotated,sub);
            checks["rotation_and_reorder_preserve_source_item_pin_override_selection"]=sub.Id==id&&sub.Url==rotated.AbsoluteUri&&data.SelectedId==chosen.Id&&data.PinnedProfileIds.Contains(chosen.Id)&&data.Profiles.Single(p=>p.ItemId=="a").Name=="Local override"&&statuses[id].Status!.Revision==5&&!statuses.ContainsKey(Importer.Fingerprint(rotated.AbsoluteUri));
            var snapshot=JsonSerializer.Serialize(data.Profiles);bool guard=false;try{CommitSubscription20(rotated,Importer.ReadCompatible(reordered.Replace("19101","19109"),true).Profiles,"Status21",id);}catch(SafeError){guard=true;}
            checks["endpoint_guard_not_bypassed_by_reorder"]=guard&&snapshot==JsonSerializer.Serialize(data.Profiles);
            var removed=Importer.ReadCompatible(feed,true).Profiles;var onlyB=removed.Where(p=>p.ItemId=="b").ToList();
            var plan=SubscriptionMigrationPreview.Plan(new(id,new('b',32),new('c',32)),new(id,new('b',32),new('c',32)),data.Profiles,onlyB,new HashSet<string>{chosen.Id},data.PinnedProfileIds,data.SelectedId,new HashSet<string>{"a"});
            checks["membership_change_remains_explicit_preview_without_trust_transfer"]=plan.RequiresExplicitDecision&&plan.Items.Any(x=>x.Change=="removed")&&plan.Items.All(x=>!x.TrustTransferAllowed);
            var aliasUri=new Uri("https://status.example.invalid/v2/sub/"+new string('b',32)+".json");var alias=new Subscription{Id="status21-alias",Url=aliasUri.AbsoluteUri};data.Subscriptions.Add(alias);
            body=Body(4,4000000000);respond=(q,t)=>Task.FromResult(Response(body));await RefreshStatus(aliasUri,alias);
            checks["token_and_format_alias_cannot_reset_shared_highwater"]=alias.StatusBody.Length==0&&statuses[alias.Id].Error=="INVALID_STATUS";
            body=Body(6,6000000000);await RefreshStatus(aliasUri,alias);
            checks["newer_alias_marks_older_snapshot_stale"]=alias.StatusSchemaVersion==2&&statuses[id].Error=="ACCOUNT_STALE";
            body=Body(5,5000000000);await RefreshStatus(rotated,sub);
            checks["older_alias_cannot_roll_back_shared_revision"]=sub.Status2Authority!.Periods.Single().Value.Revision==5&&statuses[id].Error=="INVALID_STATUS";
            string currentBody=sub.StatusBody;
            respond=(q,t)=>Task.FromResult(Response(Body(6,6000000000,edit:o=>o["generated_at"]=DateTimeOffset.UtcNow.AddMinutes(5).ToString("O",CultureInfo.InvariantCulture))));await RefreshStatus(rotated,sub);
            checks["future_generated_time_cannot_poison_checkpoint"]=sub.StatusBody==currentBody&&sub.Status2Authority!.Periods.Single().Value.Revision==5;
            respond=(q,t)=>{var reply=Response(Body(6,6000000000));reply.Headers.Date=null;return Task.FromResult(reply);};await RefreshStatus(rotated,sub);
            checks["missing_transport_date_retains_known_cache"]=sub.StatusBody==currentBody&&!statuses[id].Evaluate().Fresh;
            var damaged=JsonSerializer.Deserialize<UserData>(JsonSerializer.Serialize(data))!;var damagedSub=damaged.Subscriptions.Single(s=>s.Id==id);damagedSub.Status2Authority=null;damagedSub.Usage=SubscriptionUsage20.Parse("upload=0;download=0;total=0;expire=0","",DateTimeOffset.UtcNow);
            var damagedWindow=new MainWindow(damaged,true);try{checks["missing_checkpoint_cannot_downgrade_to_unlimited_header"]=damagedWindow.statuses[id].Status==null&&damagedWindow.statuses[id].Error=="INVALID_STATUS"&&damagedWindow.ownAccountSources20.Contains(id);}finally{damagedWindow.Close();}
            var lostGlobal=JsonSerializer.Deserialize<UserData>(JsonSerializer.Serialize(data))!;lostGlobal.Status2Authorities.Clear();var lostWindow=new MainWindow(lostGlobal,true);
            try{
                checks["missing_shared_highwater_fails_closed_on_restart"]=lostWindow.statuses[id].Status==null&&lostWindow.statuses[id].Error=="INVALID_STATUS";
                var lostAliasUri=new Uri("https://status.example.invalid/v2/sub/"+new string('d',32)+".json");var lostAlias=new Subscription{Id="lost-alias",Url=lostAliasUri.AbsoluteUri};lostGlobal.Subscriptions.Add(lostAlias);lostWindow.storeSaveFixture28=()=>{};
                respond=(q,t)=>Task.FromResult(Response(Body(1)));await lostWindow.RefreshStatus(lostAliasUri,lostAlias);
                checks["new_alias_cannot_reset_missing_shared_highwater"]=lostWindow.statuses[lostAlias.Id].Error=="INVALID_STATUS"&&lostAlias.StatusBody.Length==0&&lostGlobal.Status2Authorities.Count==0;
            }finally{lostWindow.Close();}
            var emptyUri=new Uri("https://status.example.invalid/v2/sub/"+new string('f',32)+".json");var empty=new Subscription{Id="empty-private-fixture",Url=emptyUri.AbsoluteUri,Usage=SubscriptionUsage20.Parse("upload=0;download=0;total=0;expire=0","",DateTimeOffset.UtcNow)};data.Subscriptions.Add(empty);
            respond=(q,t)=>Task.FromResult(Response("not found",HttpStatusCode.NotFound));await RefreshStatus(emptyUri,empty);CaptureUsage20(empty.Id,new("",DateTimeOffset.UtcNow,"upload=0;download=0;total=0;expire=0"));
            checks["first_404_does_not_create_active_or_unlimited"]=statuses[empty.Id].Status==null&&!statuses[empty.Id].Evaluate().Trustworthy;
            var emptyWindow=new MainWindow(JsonSerializer.Deserialize<UserData>(JsonSerializer.Serialize(data))!,true);try{checks["first_404_restart_remains_private_unknown"]=emptyWindow.statuses[empty.Id].Status==null&&emptyWindow.statuses[empty.Id].Error=="ACCOUNT_UNKNOWN"&&emptyWindow.ownAccountSources20.Contains(empty.Id);}finally{emptyWindow.Close();}
            using(var positive=JsonDocument.Parse(typeof(MainWindow).Assembly.GetManifestResourceStream("Rovunet.Status2.PositiveExamples")!)){
                int index=0;foreach(var example in positive.RootElement.GetProperty("examples").EnumerateArray()){var dto=AccountStatusV2.Read(example.GetRawText());checks["panel_normative_example_"+index++]=dto.ConfirmedBytes==example.GetProperty("confirmed_bytes").GetInt64()&&dto.PendingBytes==example.GetProperty("pending_bytes").GetInt64();}
            }
            using var fixture=JsonDocument.Parse(typeof(MainWindow).Assembly.GetManifestResourceStream("Rovunet.Status2.GeneratedFixtures")!);
            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=data.Language=language;data.Theme=theme;ApplyTheme();Build();
                foreach(var item in fixture.RootElement.EnumerateObject()){
                    if(item.Value.ValueKind!=JsonValueKind.Object||!item.Value.TryGetProperty("body",out var raw))continue;
                    var dto=AccountStatusV2.Read(raw.GetRawText());var status=dto.ToStatus();var tile=new AccountQuotaTile();
                    tile.Update(new(status,DateTimeOffset.UtcNow,false,Severity.Unknown){Fresh=false});string key=language+"_"+theme+"_"+item.Name;
                    checks[key+"_null_usage_not_fabricated"]=status.UsedBytes==dto.ConfirmedBytes;
                    checks[key+"_known_limit_survives_unknown_usage"]=dto.LimitBytes is not >0||dto.ConfirmedBytes!=null||tile.Used.Text==string.Format(L.Text("QUOTA_TOTAL_USAGE_UNKNOWN"),AccountUsage.Gigabytes(dto.LimitBytes.Value).ToString("0.##",CultureInfo.CurrentCulture));
                    checks[key+"_known_expiry_remains_visible"]=dto.ExpiryKind!="at"||tile.Time.Text.Length>0&&tile.Time.Text!=L.Text("UNKNOWN");
                    checks[key+"_fixed_quota_height"]=tile.Root.Height==44;
                    captures.Add(new{language,theme,fixture=item.Name,text=tile.Used.Text,time=tile.Time.Text,note=tile.Note.Text,confirmed=status.UsedBytes,pending=status.PendingBytes});
                }
                var pending=AccountStatusV2.Read(Body(6,null,15000000000)).ToStatus();var tile2=new AccountQuotaTile();tile2.Update(new(pending,DateTimeOffset.UtcNow,false,Severity.Unknown){Fresh=true});
                checks[language+"_"+theme+"_pending_alone_not_limit_reached"]=pending.State=="active"&&pending.UsedBytes==null&&tile2.Time.Text!=L.Text("ACCOUNT_LIMIT")&&tile2.Note.Text.Contains(string.Format(L.Text("QUOTA_PENDING"),"15"));
            }
        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
        finally{SubscriptionClient.HttpFixture=null;storeSaveFixture28=null;}
        var loaded=new[]{typeof(MainWindow).Assembly,typeof(AccountStatusV2).Assembly,typeof(UserData).Assembly}.Select(a=>new{path=a.Location,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray();
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,captures,startUtc=start,endUtc=DateTimeOffset.UtcNow,pid=Environment.ProcessId,command=Environment.GetCommandLineArgs(),cwd=Environment.CurrentDirectory,loaded,synthetic=true,httpMock=true,liveTls=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
