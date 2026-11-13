using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
sealed class UpdateFixtureHandler28(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>send(request,cancellationToken);
}
public sealed partial class MainWindow
{
    public async Task<bool> RunUpdater28Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string error="";string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
        string root=Path.Combine(Path.GetTempPath(),"RovunetUpdate28-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        async Task Complete(Button button){button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var until=DateTime.UtcNow.AddSeconds(8);do{await Task.Delay(20);await Idle();}while(!button.IsEnabled&&DateTime.UtcNow<until);if(!button.IsEnabled)throw new TimeoutException();}
        async Task<bool> Reject(Func<Task> task,string code){try{await task();return false;}catch(SafeError e){return e.Code==code;}}
        byte[] bytes=Enumerable.Range(0,2048).Select(i=>(byte)(i%251)).ToArray();string sha=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string tag="windows-v0.2.0-beta",name="Rovunet-Windows-0.2.0-Beta-x64-Setup.exe";
        string metadata=JsonSerializer.Serialize(new[]{new{tag_name=tag,html_url="https://github.com/msVPNco/rovunet-app/releases/tag/"+tag,draft=false,prerelease=true,assets=new[]{new{name,size=bytes.Length,digest="sha256:"+sha,browser_download_url="https://github.com/msVPNco/rovunet-app/releases/download/"+tag+"/"+name}}}});
        AppUpdateClient28 Fake(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send,Action<int>? port=null)=>new(value=>{port?.Invoke(value);return new UpdateFixtureHandler28(send);});
        static HttpResponseMessage Json(string text)=>new(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")};
        var item=AppUpdates28.Select(metadata,"0.1.0")!;
        try{
            var ports=new List<int>();var client=Fake((request,stop)=>Task.FromResult(request.RequestUri!.Host=="api.github.com"?Json(metadata):new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}),port=>ports.Add(port));
            checks["disconnected_check_uses_explicit_direct_route"]=(await client.Check("0.1.0",0,default))?.Version=="0.2.0-beta"&&ports.SequenceEqual(new[]{0});
            string destination=Path.Combine(root,name);await client.Download(item,12456,destination,null,default);
            checks["active_route_used_for_download"]=ports.Last()==12456;
            checks["download_hash_and_size_verified"]=File.ReadAllBytes(destination).SequenceEqual(bytes);
            var acl=new FileInfo(destination).GetAccessControl();var sid=WindowsIdentity.GetCurrent().User!;checks["download_owner_only_acl"]=acl.AreAccessRulesProtected&&acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().All(r=>r.IdentityReference.Equals(sid));
            var corrupt=Fake((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[2048])}));
            checks["wrong_hash_rejected"]=await Reject(()=>corrupt.Download(item,0,destination,null,default),"APP_UPDATE_INTEGRITY_FAILED");
            checks["bad_download_preserves_existing_destination"]=File.ReadAllBytes(destination).SequenceEqual(bytes);
            var shortFile=Fake((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[1024])}));checks["truncated_download_rejected"]=await Reject(()=>shortFile.Download(item,0,destination,null,default),"APP_UPDATE_INTEGRITY_FAILED");
            int external=0;var redirect=Fake((request,_)=>{external++;var reply=new HttpResponseMessage(HttpStatusCode.Redirect);reply.Headers.Location=new("https://attacker.example.invalid/installer.exe");return Task.FromResult(reply);});
            checks["redirect_rejected_before_external_request"]=await Reject(()=>redirect.Download(item,0,destination,null,default),"APP_UPDATE_REDIRECT_REJECTED")&&external==1;
            var network=Fake((_,_)=>throw new HttpRequestException("synthetic"));checks["network_error_has_retryable_safe_code"]=await Reject(()=>network.Check("0.1.0",0,default),"APP_UPDATE_NETWORK_FAILED");
            var cancelled=Fake(async(_,stop)=>{await Task.Delay(Timeout.Infinite,stop);return Json(metadata);});using(var cts=new CancellationTokenSource(50))checks["cancel_is_bounded_and_safe"]=await Reject(()=>cancelled.Check("0.1.0",0,cts.Token),"CANCELLED");
            checks["failed_download_temp_files_cleaned"]=!Directory.EnumerateFiles(root,".Rovunet-download-*.tmp").Any();
            int hops=0;var legitimate=Fake((request,_)=>{hops++;if(hops==1){var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new("https://release-assets.githubusercontent.com/synthetic-fixture");return Task.FromResult(response);}return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});});await legitimate.Download(item,0,destination,null,default);checks["github_asset_redirect_preserves_verified_payload"]=hops==2&&File.ReadAllBytes(destination).SequenceEqual(bytes);

            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=data.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();Build();SetState(new());string id=language+"_"+theme;ports.Clear();
                var page=BuildAppUpdatePage28(client,_=>destination);page.Show();await Idle();
                Button Find(string key)=>Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"update:"+key));
                await Complete(Find("check"));
                checks[id+"_check_and_candidate_in_same_window"]=NavigationDepth==1&&Visuals<TextBlock>(page.PageVisual).Any(t=>Equals(t.Tag,"update:release")&&t.Text.Contains("0.2.0-beta"))&&ports.Last()==0;
                checks[id+"_version_runs_keep_english_reading_order"]=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"update:release")).FlowDirection==FlowDirection.LeftToRight&&Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"update:current")).Inlines.OfType<System.Windows.Documents.Run>().Any(r=>r.Text==Version.Split('+')[0]&&r.FlowDirection==FlowDirection.LeftToRight);
                checks[id+"_download_enabled_only_for_verified_metadata"]=Find("download").IsEnabled;
                SetState(new(){State="connected",HttpPort=12456,SocksPort=12457});await Complete(Find("download"));
                checks[id+"_active_tunnel_route_and_download_completed"]=ports.Last()==12456&&Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"update:status")).Text==L.Text("APP_UPDATE_DOWNLOADED")&&File.ReadAllBytes(destination).SequenceEqual(bytes);
                checks[id+"_all_update_text_localized"]=L.Text("APP_UPDATE_TITLE")!="APP_UPDATE_TITLE"&&Visuals<TextBlock>(page.PageVisual).Any(t=>Equals(t.Tag,"update:integrity-note")&&t.Text==L.Text("APP_UPDATE_INTEGRITY_NOTE"));
                if(language is "fa" or "en"){await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"updater-"+id+".png"));}page.Close();SetState(new());
            }
            bool fail=true;var retry=Fake((_,_)=>fail?throw new HttpRequestException("synthetic"):Task.FromResult(Json(metadata)));var retryPage=BuildAppUpdatePage28(retry);retryPage.Show();await Idle();var retryButton=Visuals<Button>(retryPage.PageVisual).Single(b=>Equals(b.Tag,"update:check"));await Complete(retryButton);
            checks["ui_error_is_red_and_retry_stays_available"]=Visuals<TextBlock>(retryPage.PageVisual).Single(t=>Equals(t.Tag,"update:status")).Foreground==Red&&retryButton.IsEnabled;fail=false;await Complete(retryButton);checks["retry_recovers_without_navigation_or_old_file_loss"]=Visuals<Button>(retryPage.PageVisual).Single(b=>Equals(b.Tag,"update:download")).IsEnabled&&File.ReadAllBytes(destination).SequenceEqual(bytes);retryPage.Close();
            foreach(string transition in new[]{"disconnect","port","profile","disconnect-aba","port-aba","profile-aba"}) {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var routed=new List<int>();
                var interrupted=new AppUpdateClient28(port=> {
                    routed.Add(port);
                    return new UpdateFixtureHandler28(async(request,stop)=> {
                        if(request.RequestUri!.Host=="api.github.com")return Json(metadata);
                        entered.TrySetResult();await Task.Delay(Timeout.Infinite,stop);
                        return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
                    });
                });
                activeProfile=Importer.Single("socks5://127.0.0.1:12345#SessionA");
                SetState(new(){State="connected",HttpPort=12456,SocksPort=12457});
                var page=BuildAppUpdatePage28(interrupted,_=>destination);page.Show();await Idle();
                var check=Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"update:check"));
                var download=Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"update:download"));
                await Complete(check);
                download.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var originalProfile=activeProfile;
                if(transition.StartsWith("disconnect"))SetState(new());
                else if(transition.StartsWith("port"))SetState(new(){State="connected",HttpPort=12458,SocksPort=12459});
                else activeProfile=Importer.Single("socks5://127.0.0.1:12346#SessionB");
                // Both transitions occur in one dispatcher turn, before a 250ms poll could see B.
                if(transition.EndsWith("-aba")){activeProfile=originalProfile;SetState(new(){State="connected",HttpPort=12456,SocksPort=12457});}
                var until=DateTime.UtcNow.AddSeconds(5);
                while(!check.IsEnabled&&DateTime.UtcNow<until){await Task.Delay(25);await Idle();}
                checks["session_"+transition+"_cancelled"]=check.IsEnabled&&Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"update:status")).Text==L.Text("APP_UPDATE_ROUTE_CHANGED");
                checks["session_"+transition+"_no_direct_fallback"]=routed.All(port=>port==12456)&&routed.Count==2;
                checks["session_"+transition+"_destination_preserved"]=File.ReadAllBytes(destination).SequenceEqual(bytes)&&!Directory.EnumerateFiles(root,".Rovunet-download-*.tmp").Any();
                page.Close();activeProfile=null;SetState(new());
            }
            var generation=new RouteGeneration();var lease=generation.Capture();var originalBytes=File.ReadAllBytes(destination);
            bool refused=await Reject(()=>client.Download(item,12456,destination,null,default,commit=>{generation.Advance();generation.Advance();generation.Commit(lease,commit);}),"APP_UPDATE_ROUTE_CHANGED");
            checks["generation_changed_immediately_before_file_commit_rejected"]=refused;
            checks["generation_reject_preserves_file_and_cleans_temp"]=File.ReadAllBytes(destination).SequenceEqual(originalBytes)&&!Directory.EnumerateFiles(root,".Rovunet-download-*.tmp").Any();
            var about=BuildAboutSupport26(_=>{},_=>throw new InvalidOperationException("No browser expected"),_=>{},()=>BuildAppUpdatePage28(client).Show());about.Show();Visuals<Button>(about.PageVisual).Single(b=>Equals(b.Tag,"about:updates")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();checks["about_update_button_opens_in_app_page_without_browser"]=NavigationDepth==2&&DialogWindow.OpenPages.Last().PageVisual!=about.PageVisual;DialogWindow.OpenPages.Last().Close();about.Close();
        }catch(Exception ex){checks["exception_"+ex.GetType().Name]=false;error=ex is SafeError safe?safe.Code:ex.GetType().Name;}
        finally{SetState(new());if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath())+"RovunetUpdate28-",StringComparison.OrdinalIgnoreCase))try{Directory.Delete(root,true);}catch{}}
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,error,syntheticHttpFixture=true,realInternetDownloadTested=false,ownerDataTouched=false,installerExecuted=false,authenticodePublisherValidated=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
