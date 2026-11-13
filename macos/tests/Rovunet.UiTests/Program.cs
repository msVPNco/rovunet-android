using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Rovunet.Core;
using Rovunet.Desktop;
using Rovunet.Platform;

AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
string output=Path.GetFullPath(args.FirstOrDefault()??"artifacts/ui");Directory.CreateDirectory(output);
var results=new List<object>();int failures=0,checks=0;
void Check(bool value,string name){checks++;if(!value){failures++;Console.WriteLine("FAIL "+name);}results.Add(new{name,passed=value});}
object? Invoke(MainWindow window,string method,params object?[] parameters)=>typeof(MainWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,parameters);
void Settle(Window window){for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();window.UpdateLayout();}}
void Capture(Window window,string filename){Settle(window);using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height),new Vector(96,96));bitmap.Render(window);bitmap.Save(Path.Combine(output,filename+".png"),PngBitmapEncoderOptions.Default);}
var profile=Importer.Single("vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&path=%2Fdemo#VLESS%20WS");profile.CollectionId="fixture";profile.CollectionName="Rovunet UI fixture";
var second=Importer.Single(ManualProfiles.Native("ssh","SSH fixture","server.example.invalid",22,"fixture","fixture-password"));second.CollectionId="fixture";second.CollectionName=profile.CollectionName;
try {
    string AssemblyHash(Type type)=>Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(type.Assembly.Location)));
    var loadedAssemblies=new[]{typeof(MainWindow),typeof(ImportReview),typeof(Profile)}.Select(type=>new{type=type.Name,path=type.Assembly.Location,sha256=AssemblyHash(type)}).ToArray();
    File.WriteAllText(Path.Combine(output,"loaded-assemblies.json"),JsonSerializer.Serialize(loadedAssemblies,new JsonSerializerOptions{WriteIndented=true}));
    if(args.Length>1){
        var expected=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[1]))!;
        foreach(var assembly in loadedAssemblies)Check(expected[assembly.type]==assembly.sha256,"executed-assembly-matches-installed-"+assembly.type);
        if(loadedAssemblies.Any(assembly=>expected[assembly.type]!=assembly.sha256))throw new InvalidOperationException("ARTIFACT_HASH_MISMATCH");
    }
    {
        var liveWindow=new MainWindow(new AppState{Language="en",Profiles=[profile,second],SelectedId=profile.Id,LiveLatency=true},true);liveWindow.Show();Settle(liveWindow);
        void LiveField(string name,object? value)=>typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(liveWindow,value);
        var liveSource=(AgentClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AgentClient));
        LiveField("client",liveSource);LiveField("connectionRevision",41L);LiveField("activeId",profile.Id);LiveField("state",new AgentReply{State="connected",TunActive=true});
        var kind=typeof(MainWindow).GetNestedType("ProbeKind",BindingFlags.NonPublic)!;
        object Begin(string k)=>Invoke(liveWindow,"BeginProbe",Enum.Parse(kind,k))!;
        bool Finish(object request,bool pass)=>(bool)Invoke(liveWindow,"RecordProof",request,pass)!;
        bool Verified()=>(bool)Invoke(liveWindow,"ConnectionVerified")!;
        bool Manual(object request,bool pass)=> (bool)Invoke(liveWindow,"ApplyProbeResult",request,new AgentReply{State="connected",Verified=pass,Error=pass?"":"PROBE_TIMEOUT"})!;
        var first=Begin("Live");Check(Finish(first,true)&&Verified(),"live-success-for-active-route");
        LiveField("state",new AgentReply{State="connected",Verified=false,TunActive=true});Invoke(liveWindow,"UpdateStatus");
        Check(Verified()&&(string)Invoke(liveWindow,"ConnectionDetailKey")! == "HTTPS_VERIFIED","telemetry-without-new-evidence-preserves-current-proof");
        Check(Manual(Begin("Manual"),false)&&!Verified(),"manual-failure-supersedes-live-success-immediately");
        Check(Finish(Begin("Live"),false)&&Manual(Begin("Manual"),true)&&Verified(),"manual-success-supersedes-live-failure");
        var auto=Begin("Automatic");Check(Finish(auto,true)&&Verified(),"automatic-proof-updates-current-session");
        var oldAuto=Begin("Automatic");Check(Manual(Begin("Manual"),false)&&!Finish(oldAuto,true)&&!Verified(),"late-automatic-cannot-cover-manual-failure");
        var old=Begin("Live");var recent=Begin("Manual");Check(Manual(recent,false)&&!Finish(old,true)&&!Verified(),"late-live-completion-cannot-supersede-newer-manual-request");
        var lateManual=Begin("Manual");var newLive=Begin("Live");Check(Finish(newLive,true)&&!Manual(lateManual,false)&&Verified(),"late-manual-completion-cannot-rewrite-card");
        LiveField("activeId",second.Id);Check(!Verified()&&!Finish(newLive,true),"live-proof-bound-to-active-profile");LiveField("activeId",profile.Id);
        Check(!(bool)Invoke(liveWindow,"ApplyProbeResult",Begin("Manual"),new AgentReply{State="connected",Error="CANCELLED"})!&&Verified(),"cancelled-probe-does-not-publish-failure");
        LiveField("connectionRevision",42L);Check(!Verified(),"new-connection-revision-rejects-old-success");
        var monotonic=new EvidenceTestClock();LiveField("evidenceClock",monotonic);
        Check(Finish(Begin("Live"),true),"fresh-monotonic-sample");monotonic.Advance(21);Check(!Verified(),"live-success-expires-after-monotonic-window");
        Check(Manual(Begin("Manual"),true),"fresh-manual-monotonic-sample");monotonic.Advance(301);Check(!Verified(),"manual-success-expires-after-monotonic-window");
        var beforeNetwork=Begin("Live");Invoke(liveWindow,"OnNetworkChange",null,EventArgs.Empty);Settle(liveWindow);Check(!Finish(beforeNetwork,true)&&!Verified(),"network-change-invalidates-pending-completion");
        Check(Finish(Begin("Live"),true)&&Verified()&&typeof(MainWindow).GetField("currentMessage",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(liveWindow) as string == "","related-success-clears-network-change-notice");
        LiveField("currentMessage","Unrelated message");Finish(Begin("Live"),true);Check((string)typeof(MainWindow).GetField("currentMessage",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(liveWindow)! == "Unrelated message","unrelated-notice-preserved");
        var beforeChange=Begin("Live");Invoke(liveWindow,"Commit",new Action<AppState>(d=>d.ProbeUrl="https://other.example.invalid/"));Check(!Verified()&&!Finish(beforeChange,true),"changing-probe-target-invalidates-proof");
        var hidden=Begin("Live");liveWindow.Hide();Settle(liveWindow);Check(!Finish(hidden,true),"hiding-invalidates-in-flight-probe");liveWindow.Show();Settle(liveWindow);
        var mini=Begin("Live");liveWindow.WindowState=WindowState.Minimized;Settle(liveWindow);Check(!Finish(mini,true),"minimize-invalidates-in-flight-probe");liveWindow.WindowState=WindowState.Normal;
        Check(((AgentReply)typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(liveWindow)!).State=="connected","probe-failure-does-not-disconnect-session");
        LiveField("client",null);LiveField("exiting",true);liveWindow.Close();
    }
    {
        L.Language="en";var clock=DateTimeOffset.Parse("2026-11-08T10:00:00Z");
        var w=new MainWindow(new AppState{Language="en"},true);
        var receipt=new StatusReceipt(clock,clock,"test",1);
        AccountStatus ReadStatus(string name)=>AccountStatus.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/PanelStatus",name+".status.json")));
        void StatusCase(AccountStatus status,StatusReceipt? received,DateTimeOffset now,TimeSpan elapsed,bool trustworthy,string name,string? session=null){
            var view=MacPanelStatus.Evaluate(status,received,session??"test",now,elapsed);
            var text=(string)Invoke(w,"FormatPanelUsage",status,view)!;
            Check(view.Trustworthy==trustworthy&&text.Contains(L.Text("MAC_STATUS_STALE"))!=trustworthy,"status-model-and-text-"+name);
        }
        foreach(string name in new[]{"active","partial","expired","renewed","limit-reached"})
            StatusCase(ReadStatus(name),receipt,clock,TimeSpan.Zero,name!="partial",name);
        string active=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/PanelStatus/active.status.json"));
        StatusCase(AccountStatus.Read(active.Replace("09:58:00","09:00:00")),receipt,clock,TimeSpan.Zero,false,"old-sample");
        StatusCase(AccountStatus.Read(active.Replace("\"2026-11-08T09:58:00Z\"","null")),receipt,clock,TimeSpan.Zero,false,"missing-sample");
        StatusCase(AccountStatus.Read(active.Replace("09:58:00","10:01:00")),receipt,clock,TimeSpan.Zero,false,"future-sample");
        StatusCase(ReadStatus("active"),new(clock,null,"test",1),clock,TimeSpan.Zero,false,"missing-server-date");
        StatusCase(ReadStatus("active"),receipt,clock.AddHours(1),TimeSpan.FromMinutes(1),false,"clock-forward");
        StatusCase(ReadStatus("active"),receipt,clock.AddHours(-1),TimeSpan.FromMinutes(1),false,"clock-back");
        StatusCase(ReadStatus("active"),receipt,clock.AddMinutes(16),TimeSpan.FromMinutes(16),false,"stale");
        StatusCase(ReadStatus("active"),receipt,clock,TimeSpan.Zero,false,"restart","new-session");
        var partial=ReadStatus("partial");var partialText=(string)Invoke(w,"FormatPanelUsage",partial,MacPanelStatus.Evaluate(partial,receipt,"test",clock,TimeSpan.Zero))!;
        Check(partialText.Contains("≥")&&!partialText.Contains(L.Text("MAC_UNLIMITED")),"partial-never-unlimited-or-complete");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    foreach(string scenario in new[]{"invalid-body","bad-tls","redirect","account","older-revision","new-period","missing"}){
        var uri=new Uri("https://fixture.invalid/v2/sub/own.windows.json");
        var item=MacProtocols.Single(profile.Raw);item.ItemId="one";
        var seed=SubscriptionMerge.Apply(new AppState{Language="en"},uri,[item],"Private",null);
        var sub=seed.Subscriptions.Single();
        var fixtures=Path.Combine(AppContext.BaseDirectory,"Fixtures","PanelStatus");
        var good=File.ReadAllText(Path.Combine(fixtures,"active.status.json"));
        sub.StatusBody=good.Replace("\"revision\": 1","\"revision\": 2");
        seed.SelectedId=seed.Profiles.Single().Id;
        string rawNew=profile.Raw.Replace("#VLESS%20WS","#Rotated");
        string body=JsonSerializer.Serialize(new{version=1,items=new[]{new{id="one",link=rawNew}}});
        var calls=new List<Uri>();
        Task<SubscriptionClient.Payload> Fetch(Uri u,int port,CancellationToken token){
            calls.Add(u);if(u==uri)return Task.FromResult(new SubscriptionClient.Payload(body,null,""));
            string value=scenario switch{
                "invalid-body"=>"{}","bad-tls"=>throw new SafeError("FETCH_TLS_FAILED"),"redirect"=>throw new SafeError("FETCH_REDIRECT_FAILED"),
                "missing"=>throw new SafeError("STATUS_NOT_FOUND"),"account"=>good.Replace("abababababababababababababababab","aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                "new-period"=>File.ReadAllText(Path.Combine(fixtures,"renewed.status.json")),_=>good};
            return Task.FromResult(new SubscriptionClient.Payload(value,DateTimeOffset.UtcNow,""));
        }
        var temp=Directory.CreateTempSubdirectory();var store=new PrivateStore(temp.FullName,new UiTestVault());store.Load<AppState>();store.Save(seed);
        var previous=File.ReadAllBytes(Path.Combine(temp.FullName,"profiles.enc"));
        var win=new MainWindow(seed,true,subscriptionDownload:Fetch);typeof(MainWindow).GetField("store",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(win,store);
        win.Show();Settle(win);var job=(Task)Invoke(win,"UpdateSubscription",sub.Id)!;for(int attempt=0;attempt<200&&!job.IsCompleted;attempt++){Settle(win);Thread.Sleep(2);}job.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        var actual=store.Load<AppState>();var changed=!previous.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(temp.FullName,"profiles.enc")));
        Check(calls.SequenceEqual(new[]{uri,MacPanelStatus.StatusUri(uri)!}),"v2-status-only-on-exact-private-alias-"+scenario);
        bool success=scenario is "new-period" or "missing";
        Check(changed==success&&actual.Profiles.Single().Raw==(success?rawNew:profile.Raw)&&actual.SelectedId==seed.SelectedId,"v2-atomic-encrypted-refresh-restart-"+scenario);
        if(scenario=="missing")Check(actual.Subscriptions.Single().StatusBody==sub.StatusBody&&!actual.StatusReceipts.ContainsKey(sub.Id),"404-retains-stale-history");
        typeof(MainWindow).GetField("exiting",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(win,true);win.Close();temp.Delete(true);
    }
    foreach(bool add in new[]{false,true})foreach(bool accept in new[]{false,true}){
        const string source="https://subscription.example.invalid/sub/review.json";
        var uri=new Uri(source);var first=MacProtocols.Single(profile.Raw);first.ItemId="keep";
        var removed=MacProtocols.Single(profile.Raw.Replace("#VLESS%20WS","#Removed"));removed.ItemId="remove";
        var seed=SubscriptionMerge.Apply(new AppState{Language="en"},uri,new[]{first,removed},"Fixture",null);
        var chosen=seed.Profiles.Single(p=>p.ItemId=="keep");seed.SelectedId=chosen.Id;
        seed.OverrideSources[chosen.Id]=chosen.Raw;seed.OverrideNames[chosen.Id]=chosen.Name;
        seed.TlsExceptions[chosen.Id]=new(Importer.Fingerprint(chosen.Raw),new string('a',64));
        var root=Directory.CreateTempSubdirectory();var store=new PrivateStore(root.FullName,new UiTestVault());store.Load<AppState>();store.Save(seed);
        string smaller=JsonSerializer.Serialize(new{version=1,items=new[]{new{id="keep",link=chosen.Raw}}});
        var w=new MainWindow(seed,true,subscriptionDownload:(url,_,_)=>url.AbsolutePath.EndsWith(".status.json")?Task.FromException<SubscriptionClient.Payload>(new SafeError("STATUS_NOT_FOUND")):Task.FromResult(new SubscriptionClient.Payload(smaller,null,"")));
        typeof(MainWindow).GetField("store",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,store);w.Show();Settle(w);
        Task? updating=null;
        if(add){
            Invoke(w,"ShowSubscription",source,"Fixture");Settle(w);
            w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SubscriptionPreview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        }
        byte[] before=File.ReadAllBytes(Path.Combine(root.FullName,"profiles.enc"));string stateBefore=JsonSerializer.Serialize(store.Load<AppState>());
        if(add)w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ImportApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else updating=(Task)Invoke(w,"UpdateSubscription",seed.Subscriptions.Single().Id)!;
        Settle(w);Check(w.OwnedWindows.Count==1,"same-url-removals-need-review-"+add+"-"+accept);
        var dialog=w.OwnedWindows.Single();Settle(dialog);
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text(accept?"CONFIRM":"CANCEL")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for(int i=0;i<20;i++){Settle(w);Thread.Sleep(1);}
        updating?.GetAwaiter().GetResult();var saved=store.Load<AppState>();
        if(accept)Check(saved.Profiles.Count==1&&saved.Profiles.Single().Id==chosen.Id&&saved.Subscriptions.Single().Url==source&&saved.SelectedId==chosen.Id&&saved.TlsExceptions.ContainsKey(chosen.Id),"approved-removals-preserve-source-selection-trust-"+add);
        else Check(stateBefore==JsonSerializer.Serialize(saved)&&before.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root.FullName,"profiles.enc"))),"cancelled-removals-preserve-entire-state-and-encrypted-bytes-"+add);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();root.Delete(true);
    }

    {
        var seed=SubscriptionMerge.Apply(new AppState{Language="en"},new Uri("https://subscription.example.invalid/sub/state.json"),new[]{profile,second},"State fixture",null);
        var root=Directory.CreateTempSubdirectory();var store=new PrivateStore(root.FullName,new UiTestVault());store.Load<AppState>();store.Save(seed);
        var w=new MainWindow(seed,true);typeof(MainWindow).GetField("store",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,store);w.Show();Settle(w);
        var review=ImportReview.Read(profile.Raw);
        var plan=Invoke(w,"PrepareSubscription",new Uri(seed.Subscriptions.Single().Url),review,"State fixture",null,"",null)!;
        var applying=(Task<bool>)Invoke(w,"ApplySubscription",plan,CancellationToken.None)!;Settle(w);
        var dialog=w.OwnedWindows.Single();Settle(dialog);
        Invoke(w,"Commit",(Action<AppState>)(next=>next.SelectedId=next.Profiles.Last().Id));
        byte[] afterChange=File.ReadAllBytes(Path.Combine(root.FullName,"profiles.enc"));
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("CONFIRM")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        bool rejected=false;try{applying.GetAwaiter().GetResult();}catch(SafeError error){rejected=error.Code=="MAC_IMPORT_REVIEW_REQUIRED";}
        Check(rejected&&afterChange.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root.FullName,"profiles.enc")))&&store.Load<AppState>().Profiles.Count==2,"state-changed-during-removal-review-rejects-stale-plan");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();root.Delete(true);
    }
    {
        var dns=MacProtocols.Single(ManualProfiles.Dns("dnstt","DNS fixture","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"","22","",""));dns.CollectionId="fixture";
        var paths=new[]{dns,profile,second};var seen=new List<string>();int offered=0;
        Task<bool> Offer(CancellationToken ct){offered++;return Task.FromResult(true);}
        var passes=CollectionTestPolicy.Run(paths,true,(p,ct)=>{seen.Add(p.Id);return Task.FromResult(p.Id==profile.Id);},Offer,(_,_)=>{},CancellationToken.None).GetAwaiter().GetResult();
        Check(passes==1&&seen.SequenceEqual(new[]{profile.Id,second.Id})&&offered==0,"group-fast-success-skips-all-dns-and-no-fallback-prompt");
        seen.Clear();offered=0;passes=CollectionTestPolicy.Run(paths,true,(p,ct)=>{seen.Add(p.Id);return Task.FromResult(p.Id==dns.Id);},Offer,(_,_)=>{},CancellationToken.None).GetAwaiter().GetResult();
        Check(passes==1&&offered==1&&seen.SequenceEqual(new[]{profile.Id,second.Id,dns.Id}),"group-all-fast-fail-offers-dns-after-fast-phase");
        seen.Clear();CollectionTestPolicy.Run(paths,true,(p,ct)=>{seen.Add(p.Id);return Task.FromResult(false);},ct=>Task.FromResult(false),(_,_)=>{},CancellationToken.None).GetAwaiter().GetResult();
        Check(!seen.Contains(dns.Id),"declining-group-dns-fallback-does-not-test-dns");
        seen.Clear();CollectionTestPolicy.Run(new[]{dns},false,(p,ct)=>{seen.Add(p.Id);return Task.FromResult(true);},Offer,(_,_)=>{},CancellationToken.None).GetAwaiter().GetResult();
        Check(seen.SequenceEqual(new[]{dns.Id}),"explicit-single-dns-test-remains-available");
        using var cancelled=new CancellationTokenSource();offered=0;
        try{CollectionTestPolicy.Run(paths,true,(p,ct)=>{cancelled.Cancel();return Task.FromResult(false);},Offer,(_,_)=>{},cancelled.Token).GetAwaiter().GetResult();Check(false,"stop-group-test-cancels-before-dns");}
        catch(OperationCanceledException){Check(offered==0,"stop-group-test-cancels-before-dns");}
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        var nav=new MainWindow(new AppState{Language=language,Profiles=[profile,second]},true);nav.Show();Settle(nav);
        string Route()=>(string)typeof(MainWindow).GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(nav)!;
        Button ConfirmButton()=>nav.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PageConfirm");
        Button SearchButton()=>nav.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="HeaderSearch");
        void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(nav);}
        Invoke(nav,"ShowGeneral");Invoke(nav,"ShowSettings");Settle(nav);
        var settingsPage=nav.GetVisualDescendants().OfType<ScrollViewer>().Single(c=>c.Name=="PageScroll");
        var openApps=settingsPage.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("LOCAL_PROXY_APPS"));
        Check(openApps.Classes.Contains("framed-action")&&openApps.BorderThickness.Left>=1,"settings-action-has-visible-border-"+language);
        var back=nav.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="HeaderBack");
        Check(back.Classes.Contains("framed-action") && Avalonia.Automation.AutomationProperties.GetName(back)==L.Text("BACK"),"directional-header-back-named-"+language);
        Check(ConfirmButton().GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==L.Text("SAVE")),"commit-shows-visible-action-label-"+language);
        var ports=nav.GetVisualDescendants().OfType<TextBox>().ToArray();ports[0].Text="invalid";
        Click(ConfirmButton());Check(Route()=="SETTINGS","invalid-settings-stay-editable-"+language);
        ports[0].Text="18080";Click(ConfirmButton());
        Check(Route()=="SECTION_GENERAL"&&((AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(nav)!).LocalHttpPort==18080,"settings-confirm-saves-returns-previous-page-"+language);
        Invoke(nav,"ShowSettings");Invoke(nav,"ShowResolverPreferences",false);Settle(nav);Click(ConfirmButton());
        Check(Route()=="SETTINGS","nested-resolver-confirm-returns-settings-"+language);
        Click(SearchButton());Check(Route()=="SEARCH"&&Avalonia.Automation.AutomationProperties.GetName(SearchButton())==L.Text("CLOSE"),"empty-search-toolbar-closes-"+language);
        var input=nav.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="ProfileSearch");
        input.Text="SSH";Settle(nav);
        Check(Avalonia.Automation.AutomationProperties.GetName(SearchButton())==L.Text("MAC_SEARCH_RUN")&&nav.GetVisualDescendants().OfType<Border>().Count(b=>b.Name=="PathCard")==2,"typing-search-waits-for-run-"+language);
        Click(SearchButton());
        Check(Route()=="SEARCH"&&Avalonia.Automation.AutomationProperties.GetName(SearchButton())==L.Text("CLOSE")&&nav.GetVisualDescendants().OfType<Border>().Count(b=>b.Name=="PathCard")==1,"search-executes-then-becomes-close-"+language);
        input.Text="VLESS";Settle(nav);
        input.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});Settle(nav);
        Check(nav.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==profile.Name)&&!nav.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==second.Name),"enter-executes-current-query-"+language);
        Invoke(nav,"ShowEditor",profile,false);Settle(nav);
        var name=nav.GetVisualDescendants().OfType<TextBox>().First();name.Text="Changed VLESS";Click(ConfirmButton());
        Check(Route()=="SEARCH"&&nav.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Changed VLESS"),"edit-confirm-returns-refreshed-search-results-"+language);
        input=nav.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="ProfileSearch");input.Text=" ";Settle(nav);
        Check(Avalonia.Automation.AutomationProperties.GetName(SearchButton())==L.Text("CLOSE")&&nav.GetVisualDescendants().OfType<Border>().Count(b=>b.Name=="PathCard")==2,"clearing-search-restores-list-and-close-"+language);
        Capture(nav,"search-close-"+language);input.Text="SSH";Settle(nav);Capture(nav,"search-run-"+language);
        input.Text="";Settle(nav);Click(SearchButton());Check(Route()=="SETTINGS","search-close-restores-origin-once-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(nav,true);nav.Close();
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        void Finish(Task task,Window window){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&watch.ElapsedMilliseconds<2000){Settle(window);Thread.Sleep(1);}Check(task.IsCompleted,"async-ui-operation-finishes-"+language);if(task.IsCompleted)task.GetAwaiter().GetResult();}
        var gate=new TaskCompletionSource<Measurement>();
        var testingWindow=new MainWindow(new AppState{Language=language,Profiles=[profile,second],Expanded=new(){["fixture"]=true}},true,testProbe:(p,tcp,ct)=>gate.Task.WaitAsync(ct));testingWindow.Show();Settle(testingWindow);
        var running=(Task)Invoke(testingWindow,"TestCollection",new List<Profile>{profile,second},1,false,true)!;Settle(testingWindow);
        var stop=testingWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="CollectionTest");
        Check(stop.IsEnabled&&Avalonia.Automation.AutomationProperties.GetName(stop)==L.Text("STOP_TEST")&&testingWindow.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="CollectionTestProgress"&&t.Text!.Contains("0 / 2")),"group-test-shows-stop-button-and-phase-progress-"+language);
        var runningMenu=(ContextMenu)Invoke(testingWindow,"CollectionMenu",new List<Profile>{profile,second})!;
        Check(runningMenu.Items.OfType<MenuItem>().Any(i=>i.Tag as string=="STOP_TEST"&&i.IsEnabled)&&!runningMenu.Items.OfType<MenuItem>().Any(i=>i.Tag as string=="TEST_COLLECTION")&&!runningMenu.Items.OfType<MenuItem>().Single(i=>i.Tag as string=="STABILITY_TEST").IsEnabled,"group-menu-matches-stop-state-and-disables-conflicting-tests-"+language);
        stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Finish(running,testingWindow);Settle(testingWindow);
        Check(testingWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="CollectionTest").IsEnabled&&Avalonia.Automation.AutomationProperties.GetName(testingWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="CollectionTest"))==L.Text("TEST_COLLECTION"),"group-stop-restores-test-button-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(testingWindow,true);testingWindow.Close();
        foreach(bool success in new[]{true,false}){
            var uri=new Uri("https://fixture.example.invalid/sub/demo.json");var initial=SubscriptionMerge.Apply(new AppState{Language=language},uri,[Importer.Single(profile.Raw)],"Fixture subscription",null,"");string source=initial.Subscriptions.Single().Id;
            var download=new TaskCompletionSource<SubscriptionClient.Payload>();int requests=0;
            var updatingWindow=new MainWindow(initial,true,subscriptionDownload:(u,port,ct)=>{if(u.AbsolutePath.EndsWith(".status.json"))throw new SafeError("STATUS_NOT_FOUND");requests++;return download.Task.WaitAsync(ct);}){Width=480,Height=680};updatingWindow.Show();Settle(updatingWindow);
            var update=(Task)Invoke(updatingWindow,"UpdateSubscription",source)!;Settle(updatingWindow);
            var pending=updatingWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SubscriptionUpdate");
            Check(!pending.IsEnabled&&Avalonia.Automation.AutomationProperties.GetName(pending)==L.Text("SUBSCRIPTION_UPDATING"),"subscription-update-disabled-with-pending-text-"+language+"-"+success);
            var pendingMenu=(ContextMenu)Invoke(updatingWindow,"CollectionMenu",initial.Profiles)!;
            Check(pendingMenu.Items.OfType<MenuItem>().Any(i=>i.Tag as string=="SUBSCRIPTION_UPDATING"&&!i.IsEnabled),"subscription-menu-matches-pending-button-"+language+"-"+success);
            ((Task)Invoke(updatingWindow,"UpdateSubscription",source)!).GetAwaiter().GetResult();Check(requests==1,"subscription-update-reentry-does-not-download-twice-"+language+"-"+success);
            if(success)download.SetResult(new(profile.Raw,null,""));else download.SetException(new SafeError("FETCH_TIMEOUT"));Finish(update,updatingWindow);Settle(updatingWindow);
            var done=updatingWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SubscriptionUpdate");
            Check(done.IsEnabled&&Avalonia.Automation.AutomationProperties.GetName(done)==L.Text(success?"MAC_SUBSCRIPTION_UPDATE_OK":"MAC_SUBSCRIPTION_UPDATE_FAILED"),"subscription-update-result-is-visible-and-retry-enabled-"+language+"-"+success);
            var saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(updatingWindow)!;
            Check(saved.Profiles.Count==1&&saved.Profiles[0].Raw==profile.Raw,"subscription-update-keeps-profile-on-success-or-failure-"+language+"-"+success);
            var doneMenu=(ContextMenu)Invoke(updatingWindow,"CollectionMenu",saved.Profiles)!;
            Check(doneMenu.Items.OfType<MenuItem>().Any(i=>i.Tag as string==(success?"MAC_SUBSCRIPTION_UPDATE_OK":"MAC_SUBSCRIPTION_UPDATE_FAILED")&&i.IsEnabled),"subscription-menu-matches-result-button-"+language+"-"+success);
            var colors=(Palette)typeof(MainWindow).GetField("palette",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(updatingWindow)!;
            Check((done.Foreground as ISolidColorBrush)?.Color==Color.Parse(colors.ColorOf(success?"Green":"Red")),"subscription-result-color-matches-success-or-failure-"+language+"-"+success);
            Capture(updatingWindow,"subscription-update-"+language+"-"+(success?"success":"failed"));
            typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(updatingWindow,true);updatingWindow.Close();
        }
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        string unsafeLink=profile.Raw.Replace("#VLESS%20WS","&allowInsecure=true#Unsafe");
        var download=new TaskCompletionSource<SubscriptionClient.Payload>();int requests=0;
        var w=new MainWindow(new AppState{Language=language,Profiles=[second]},true,subscriptionDownload:(_,_,ct)=>{requests++;return download.Task.WaitAsync(ct);}){Width=480,Height=680};w.Show();Settle(w);
        Invoke(w,"ShowSubscription",PublicFeed.Presets.Single(p=>p.Key=="anonymous").Url,"");Settle(w);
        Button ButtonNamed(string name)=>w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name==name);
        Check(!ButtonNamed("PageConfirm").IsEnabled,"subscription-cannot-save-before-review-"+language);
        ButtonNamed("SubscriptionPreview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        Check(!ButtonNamed("SubscriptionPreview").IsEnabled&&ButtonNamed("SubscriptionPreview").Content as string==L.Text("MAC_IMPORT_LOADING"),"subscription-preview-shows-processing-"+language);
        download.SetResult(new(profile.Raw+"\n"+unsafeLink,null,""));
        var watch=System.Diagnostics.Stopwatch.StartNew();while((bool)typeof(MainWindow).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!&&watch.ElapsedMilliseconds<2000){Settle(w);Thread.Sleep(1);}Settle(w);
        var checkbox=w.GetVisualDescendants().OfType<CheckBox>().Single();
        Check(checkbox.IsVisible&&!ButtonNamed("ImportApply").IsEnabled&&!ButtonNamed("PageConfirm").IsEnabled,"partial-import-blocked-until-accepted-"+language);
        Check(ButtonNamed("SubscriptionPreview").Content as string==L.Text("MAC_IMPORT_REVIEW_AGAIN"),"preview-refresh-is-not-mislabelled-as-import-"+language);
        checkbox.IsChecked=true;Settle(w);
        Check(ButtonNamed("ImportApply").IsEnabled&&ButtonNamed("PageConfirm").IsEnabled&&requests==1,"partial-acceptance-enables-real-save-without-refetch-"+language);
        ButtonNamed("ImportApply").BringIntoView();Capture(w,"subscription-import-ready-"+language);
        ButtonNamed(language=="fa"?"PageConfirm":"ImportApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(saved.Subscriptions.Count==1&&saved.Profiles.Count==3&&saved.Profiles.All(p=>!p.Raw.Contains("allowInsecure=true")),"partial-subscription-stores-only-valid-and-preserves-existing-"+language);
        Check(typeof(MainWindow).GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w) as string=="HOME"&&w.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==L.Text("MAC_IMPORT_APPLIED")),"subscription-save-returns-home-with-success-message-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
        var plain=new MainWindow(new AppState{Language=language},true);plain.Show();Settle(plain);Invoke(plain,"ShowImport",profile.Raw+"\n"+unsafeLink);Settle(plain);
        plain.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ImportPreview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(plain);
        plain.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=true;Settle(plain);
        plain.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ImportApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(plain);
        Check(typeof(MainWindow).GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plain) as string=="HOME"&&((AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plain)!).Profiles.Count==2,"plain-partial-import-also-adds-and-returns-home-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plain,true);plain.Close();
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        var w=new MainWindow(new AppState{Language=language},true,subscriptionDownload:(u,_,_)=>u.AbsolutePath.EndsWith(".status.json")?Task.FromException<SubscriptionClient.Payload>(new SafeError("STATUS_NOT_FOUND")):Task.FromResult(new SubscriptionClient.Payload(profile.Raw.Replace("#VLESS%20WS","&allowInsecure=true#PrivateTLS"),null,""))){Width=480,Height=680};w.Show();Settle(w);
        const string privateUrl="https://subscriptions.example.invalid/private/token-fixture";
        Invoke(w,"ShowSubscription",privateUrl,"Personal fixture");Settle(w);
        w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="SubscriptionPreview").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var accept=w.GetVisualDescendants().OfType<CheckBox>().Single();
        Check(accept.IsVisible&&accept.Content as string==L.Text("MAC_TLS_IMPORT_CONSENT"),"personal-subscription-reviews-tls-normalization-"+language);
        accept.IsChecked=true;Settle(w);w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ImportApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;var imported=saved.Profiles.Single();
        Check(saved.Subscriptions.Single().Url==privateUrl&&typeof(MainWindow).GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w) as string=="HOME"&&!MacTlsPolicy.Approved(saved,imported),"personal-subscription-saves-home-without-unsafe-approval-"+language);
        Task Toggle()=>(Task)Invoke(w,"ToggleTlsException",imported)!;
        void Answer(bool yes){var dialog=w.OwnedWindows.Single();dialog.Width=320;Settle(dialog);
            var digest=dialog.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="CertificateDigest");
            Check(digest.TranslatePoint(default,dialog) is {} point&&point.Y>=0&&point.Y+digest.Bounds.Height<dialog.Bounds.Height,"digest-visible-without-scrolling-"+language);Settle(dialog);
            Check(digest.FlowDirection==FlowDirection.LeftToRight&&digest.Text!.Split(' ').Length==8&&digest.Bounds.Width<=dialog.Bounds.Width,"narrow-certificate-digest-"+language);
            Capture(dialog,"tls-certificate-warning-"+language);Check(dialog.GetVisualDescendants().OfType<ScrollViewer>().Any(),"tls-warning-can-scroll-"+language);dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text(yes?"CONFIRM":"CANCEL")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);}
        Task Begin(){var task=Toggle();Settle(w);w.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="TlsCertificatePin").Text=new string('a',64);Settle(w);Capture(w,"tls-certificate-settings-"+language);w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="TlsApproveCertificate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);return task;}
        var cancel=Begin();Settle(w);Answer(false);Check(cancel.IsCompleted&&!MacTlsPolicy.Approved(saved,imported),"cancelled-tls-warning-grants-no-exception-"+language);
        var approve=Begin();Settle(w);Answer(true);saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(approve.IsCompleted&&MacTlsPolicy.Approved(saved,imported)&&w.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text!.Contains(L.Text("MAC_TLS_EXCEPTION_ACTIVE"))),"explicit-tls-approval-is-stored-and-card-warns-"+language);
        var revoke=Toggle();Settle(w);saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(revoke.IsCompleted&&!MacTlsPolicy.Approved(saved,imported),"per-card-exception-can-be-revoked-"+language);
        var reapprove=Begin();Answer(true);reapprove.GetAwaiter().GetResult();
        var changed=MacProfileEditing.Replace(imported,imported.Raw.Replace("server.example.invalid","changed.example.invalid"));
        Invoke(w,"SaveEdited",imported,changed,false);Invoke(w,"SaveEdited",changed,imported,false);
        saved=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(!saved.TlsExceptions.ContainsKey(imported.Id),"reverting-config-cannot-resurrect-certificate-approval-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    {
        var other=Importer.Single("vless://00000000-0000-4000-8000-000000000002@other.example.invalid:443?security=tls#Other%20group");other.CollectionId="other";other.CollectionName="Other collection";
        var initial=new AppState{Language="en",Profiles=[other,profile,second],Pinned=[other.Id],Expanded=new(){["fixture"]=true,["other"]=true},LastConnected=new(){[second.Id]=DateTimeOffset.UtcNow}};
        var ordered=new MainWindow(initial,true);ordered.Show();Settle(ordered);
        string[] Titles()=>ordered.GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="PathCard").Select(b=>b.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text==profile.Name||t.Text==second.Name||t.Text==other.Name).Text!).ToArray();
        Check(Titles().SequenceEqual(new[]{second.Name,profile.Name,other.Name}),"recent-connection-promotes-its-collection-and-path-above-pinned-collection");
        typeof(MainWindow).GetField("activeId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,profile.Id);
        typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,new AgentReply{State="connected"});Invoke(ordered,"BuildCards");Settle(ordered);
        Check(Titles().SequenceEqual(new[]{profile.Name,second.Name,other.Name}),"active-path-precedes-more-recent-history-without-duplicating-cards");
        Button GroupButton(string key)=>ordered.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="CollectionConnect"&&b.Tag as string==key);
        Check(Avalonia.Automation.AutomationProperties.GetName(GroupButton("fixture"))!.StartsWith(L.Text("DISCONNECT"))&&
            Avalonia.Automation.AutomationProperties.GetName(GroupButton("other"))==L.Text("CONNECT")&&
            GroupButton("fixture").Background is ISolidColorBrush activeBrush&&activeBrush.Color==Color.Parse("#A6EAC4")&&GroupButton("other").Background==Brushes.Transparent,
            "only-active-childs-collection-is-green");
        typeof(MainWindow).GetField("activeId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,"");
        typeof(MainWindow).GetField("connectingId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,other.Id);
        typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,new AgentReply{State="connecting"});
        Invoke(ordered,"UpdateStatus");Settle(ordered);
        Check(Avalonia.Automation.AutomationProperties.GetName(GroupButton("other"))==L.Text("CANCEL")&&
            GroupButton("other").Background is ISolidColorBrush pendingBrush&&pendingBrush.Color==Color.Parse("#F7DA8A")&&
            GroupButton("fixture").Background==Brushes.Transparent,"only-connecting-childs-collection-is-yellow");
        typeof(MainWindow).GetField("failedId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,other.Id);
        typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,new AgentReply{State="error"});
        Invoke(ordered,"UpdateStatus");Settle(ordered);
        Check(Avalonia.Automation.AutomationProperties.GetName(GroupButton("other"))!.Contains(L.Text("FAILED"))&&
            Avalonia.Automation.AutomationProperties.GetName(GroupButton("fixture"))==L.Text("CONNECT"),"failed-collection-feedback-does-not-cross-groups");
        Capture(ordered,"collection-connect-error");

        typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,new AgentReply{State="error"});Invoke(ordered,"BuildCards");Settle(ordered);
        Check(Titles().First()==second.Name,"failed-attempt-does-not-promote-unconnected-path");
        Check(initial.Profiles.Select(p=>p.Id).SequenceEqual(new[]{other.Id,profile.Id,second.Id}),"display-promotion-preserves-imported-profile-order");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(ordered,true);ordered.Close();
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        var expandedWindow=new MainWindow(new AppState{Language=language,Profiles=[profile],Expanded=new(){["fixture"]=true}},true);expandedWindow.Show();Settle(expandedWindow);
        Button Toggle()=>expandedWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="CollectionToggle");
        string? Arrow()=>((Avalonia.Controls.Shapes.Path)Toggle().Content!).Data?.ToString();
        string up=Geometry.Parse("M4,16 L12,8 L20,16").ToString()!,down=Geometry.Parse("M4,8 L12,16 L20,8").ToString()!;
        Check(Arrow()==up&&Avalonia.Automation.AutomationProperties.GetName(Toggle())==L.Text("HIDE_PATHS"),"expanded-collection-arrow-up-and-collapse-label-"+language);
        Toggle().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(expandedWindow);
        Check(Arrow()==down&&Avalonia.Automation.AutomationProperties.GetName(Toggle())==L.Text("SHOW_PATHS")&&!expandedWindow.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="PathCard"),"collapsed-collection-arrow-down-and-hidden-paths-"+language);
        Toggle().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(expandedWindow);
        Check(Arrow()==up&&expandedWindow.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="PathCard"),"collection-arrow-tracks-reopen-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(expandedWindow,true);expandedWindow.Close();
    }
    string? DecodeQr(byte[] png){using var decoded=SkiaSharp.SKBitmap.Decode(png);using var pixels=decoded.Copy(SkiaSharp.SKColorType.Bgra8888);return new ZXing.BarcodeReaderGeneric{Options=new ZXing.Common.DecodingOptions{TryHarder=true}}.Decode(new ZXing.RGBLuminanceSource(pixels.Bytes,pixels.Width,pixels.Height,ZXing.RGBLuminanceSource.BitmapFormat.BGRA32))?.Text;}
    const string wallet="TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ";
    using(var qr=(WriteableBitmap)typeof(MainWindow).GetMethod("CreateSupportQr",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!){using var png=new MemoryStream();qr.Save(png,PngBitmapEncoderOptions.Default);Check(DecodeQr(png.ToArray())==wallet,"wallet-qr-png-decodes-exact-address");File.WriteAllBytes(Path.Combine(output,"wallet-qr.png"),png.ToArray());}
    string suppliedQr=Environment.GetEnvironmentVariable("ROVUNET_REFERENCE_QR")??"";
    if(File.Exists(suppliedQr)){string? decoded=DecodeQr(File.ReadAllBytes(suppliedQr));Check(decoded==wallet,"owner-provided-wallet-qr-matches-address");}
    else File.WriteAllText(Path.Combine(output,"owner-qr-reference-unavailable.txt"),"The temporary attachment is unavailable; generated QR was decoded against the owner's exact text address. No visual reference match claimed.");
    {
        var probeWindow=new MainWindow(new AppState{Language="en",Profiles=[profile],SelectedId=profile.Id,Expanded=new(){["fixture"]=true}},true);probeWindow.Show();Settle(probeWindow);
        var agent=(AgentClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AgentClient));
        var agentField=typeof(MainWindow).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var stateField=typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var active=typeof(MainWindow).GetField("activeId",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var revision=typeof(MainWindow).GetField("connectionRevision",BindingFlags.Instance|BindingFlags.NonPublic)!;
        agentField.SetValue(probeWindow,agent);active.SetValue(probeWindow,profile.Id);revision.SetValue(probeWindow,7L);stateField.SetValue(probeWindow,new AgentReply{State="connected"});
        var mode=typeof(MainWindow).GetNestedType("ProbeKind",BindingFlags.NonPublic)!;
        object StartProbe()=>Invoke(probeWindow,"BeginProbe",Enum.Parse(mode,"Manual"))!;
        var stale=StartProbe();
        Check((bool)Invoke(probeWindow,"ApplyProbeResult",StartProbe(),new AgentReply{State="connected",Verified=false,Error="FETCH_TIMEOUT"})!,"current-failed-probe-is-applied");Settle(probeWindow);
        var palette=(Palette)typeof(MainWindow).GetField("palette",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(probeWindow)!;
        Check(probeWindow.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==L.Text("FAILED")&&(t.Foreground as ISolidColorBrush)?.Color==Color.Parse(palette.ColorOf("Red"))),"failed-current-ping-text-red");
        Check(!(bool)Invoke(probeWindow,"ApplyProbeResult",stale,new AgentReply{State="connected",Verified=true,ElapsedMs=44})!,"late-probe-cannot-overwrite-new-attempt");
        stateField.SetValue(probeWindow,new AgentReply());Invoke(probeWindow,"UpdateStatus");
        var status=(TextBlock)typeof(MainWindow).GetField("statusText",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(probeWindow)!;
        Check((status.Foreground as ISolidColorBrush)?.Color==Color.Parse(palette.ColorOf("Red")),"disconnected-status-text-red");
        Check(!(bool)Invoke(probeWindow,"ApplyProbeResult",stale,new AgentReply{State="connected",Verified=true})!,"probe-result-cannot-reconnect-disconnected-window");
        agentField.SetValue(probeWindow,null);typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(probeWindow,true);probeWindow.Close();
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"}){
        var w=new MainWindow(new AppState{Language=language,Profiles=[profile,second]},true){Width=560,Height=680};w.Show();Settle(w);
        var menu=(ContextMenu)Invoke(w,"CollectionMenu",new List<Profile>{profile,second})!;
        var parent=menu.Items.OfType<MenuItem>().Single(item=>item.Tag as string=="MAC_SELECTION_MODE");
        Check(parent.Items.OfType<MenuItem>().Select(item=>item.Tag as string).SequenceEqual(new[]{"MAC_SELECTION_MANUAL","MAC_SELECTION_FAILOVER","MAC_SELECTION_SMART"}),"three-selection-modes-under-one-parent-"+language);
        Check(parent.Items.OfType<MenuItem>().Count(item=>item.Icon!=null)==1&&parent.Items.OfType<MenuItem>().First().Icon!=null,"selection-mode-default-manual-check-"+language);
        AppState Saved()=>(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        parent.Items.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Settle(w);
        Check(Saved().SelectionModes["fixture"]==PathSelectionMode.Smart,"smart-choice-persists-for-this-collection-"+language);
        var path=(ContextMenu)Invoke(w,"PathMenu",profile)!;
        Check(path.Items.OfType<MenuItem>().Single(item=>item.Tag as string=="MAC_SELECTION_MODE").Items.OfType<MenuItem>().Last().Icon!=null,"path-menu-reflects-its-collection-policy-"+language);
        parent.Items.OfType<MenuItem>().ElementAt(1).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Settle(w);
        Check(Saved().SelectionModes["fixture"]==PathSelectionMode.Failover,"failure-choice-persists-"+language);
        parent.Items.OfType<MenuItem>().First().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Settle(w);
        w.GetVisualDescendants().OfType<Button>().Single(button=>button.Content as string==second.Name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        Check(Saved().SelectionModes["fixture"]==PathSelectionMode.Manual&&Saved().PreferredPaths["fixture"]==second.Id&&Saved().SelectedId==second.Id,"manual-submenu-selects-and-remembers-specific-path-"+language);
        const string subscription="https://subscriptions.example.invalid:8443/sub/example?format=base64";
        Invoke(w,"ShowImport",subscription);Settle(w);w.GetVisualDescendants().OfType<Button>().Single(button=>button.Content as string==L.Text("REVIEW_IMPORT")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        Check(typeof(MainWindow).GetField("route",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w) as string=="ADD_SUBSCRIPTION"&&w.GetVisualDescendants().OfType<TextBox>().Any(box=>box.Text==subscription),"pasted-https-subscription-opens-subscription-preview-form-"+language);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    {
        var w=new MainWindow(new AppState{Language="en"},true);
        var flag=(Image)Invoke(w,"Flag","fa")!;
        IEnumerable<GeometryDrawing> Shapes(Drawing drawing)=>drawing is GeometryDrawing path?[path]:drawing is DrawingGroup group?group.Children.SelectMany(Shapes):[];
        Check(flag.Source is DrawingImage vector&&Shapes(vector.Drawing!).Count()==35&&Shapes(vector.Drawing!).Any(shape=>(shape.Brush as ISolidColorBrush)?.Color==Color.Parse("#FFB81C")),"persian-flag-uses-real-vector-lion-and-sun-artwork");
        var preview=new Window{Width=420,Height=240,Content=new Image{Source=flag.Source,Stretch=Stretch.Uniform}};preview.Show();Capture(preview,"iran-lion-sun");preview.Close();
        string Usage(string header)=>(string)Invoke(w,"UsageText",SubscriptionUsage20.Parse(header,"",DateTimeOffset.UtcNow))!;
        Check(Usage("upload=1024;download=2048;total=8192;expire=0").Contains("5.0 KB"),"subscription-card-shows-declared-remaining-volume");
        Check(Usage("upload=10;download=20").Contains(L.Text("MAC_UNKNOWN"))&&!Usage("upload=10;download=20").Contains(L.Text("MAC_UNLIMITED")),"subscription-card-never-labels-missing-quota-as-unlimited");
        Check(Usage("upload=10;download=20;total=20;expire=1").Contains(L.Text("MAC_USAGE_EXPIRED")),"subscription-card-shows-declared-time-expiry");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    // The native menu data must follow the app language without restarting.
    // Headless checks exercise the Avalonia menu model, not macOS rendering.
    var trayData=new AppState{Language="zh-CN",Profiles=[profile],SelectedId=profile.Id,Expanded=new(){["fixture"]=true},Measurements=new(){[profile.Id]=new(true,42,DateTimeOffset.UtcNow,"https",1,1)}};
    var trayWindow=new MainWindow(trayData,true);trayWindow.Show();Settle(trayWindow);Invoke(trayWindow,"InstallTray");
    var tray=(TrayIcon?)typeof(MainWindow).GetField("tray",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trayWindow);Check(tray!=null,"tray-created-with-colored-R-icon");
    var menuIdentity=tray!.Menu;var appMenuIdentity=NativeMenu.GetMenu(Application.Current!);var windowMenuIdentity=NativeMenu.GetMenu(trayWindow);var fileMenuIdentity=windowMenuIdentity!.Items.OfType<NativeMenuItem>().Single().Menu;
    foreach(string lang in new[]{"fa","en","ru","zh-CN","fa"}){
        trayData.Language=lang;Invoke(trayWindow,"GoHome");Settle(trayWindow);
        Check(tray?.Menu?.Items.OfType<NativeMenuItem>().Select(i=>i.Header?.ToString()).SequenceEqual(new[]{L.Text("MAC_SHOW_APP"),L.Text("CONNECT"),L.Text("SETTINGS"),L.Text("EXIT")})==true,"tray-language-refresh-"+lang);
        Check(tray?.Menu?.Items.OfType<NativeMenuItem>().All(i=>i.IsEnabled&&i.Command?.CanExecute(null)==true)==true,"tray-native-commands-enabled-"+lang);
        Check(ReferenceEquals(menuIdentity,tray!.Menu)&&ReferenceEquals(appMenuIdentity,NativeMenu.GetMenu(Application.Current!))&&ReferenceEquals(windowMenuIdentity,NativeMenu.GetMenu(trayWindow))&&ReferenceEquals(fileMenuIdentity,NativeMenu.GetMenu(trayWindow)!.Items.OfType<NativeMenuItem>().Single().Menu),"exported-native-menu-identities-survive-rebuild-"+lang);
    }
    typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,new AgentReply{State="connected"});Invoke(trayWindow,"UpdateStatus");
    Check(tray?.Menu?.Items.OfType<NativeMenuItem>().ElementAt(1).Header?.ToString()==L.Text("DISCONNECT"),"tray-connect-label-follows-state");
    Check(ReferenceEquals(menuIdentity,tray!.Menu),"connection-state-does-not-replace-exported-tray-menu");
    trayWindow.Hide();((INativeMenuItemExporterEventsImplBridge)tray.Menu!.Items.OfType<NativeMenuItem>().First()).RaiseClicked();Settle(trayWindow);Check(trayWindow.IsVisible,"native-tray-show-command-reveals-hidden-window");
    ((INativeMenuItemExporterEventsImplBridge)tray.Menu.Items.OfType<NativeMenuItem>().ElementAt(2)).RaiseClicked();Settle(trayWindow);Check(trayWindow.GetVisualDescendants().OfType<CheckBox>().Any(c=>c.Name=="SystemTunOption"),"native-tray-settings-command-opens-settings");Invoke(trayWindow,"GoHome");Settle(trayWindow);
    Check(!MacOSProperties.GetIsTemplateIcon(tray!),"mac-tray-preserves-status-colors");
    foreach(var (status,hex,action) in new[]{
        ("disconnected","#FFFFFF","CONNECT"),("connecting","#FFD60A","CANCEL"),
        ("engine-started","#FFD60A","CANCEL"),("connected","#30D158","DISCONNECT"),
        ("error","#FF453A","CONNECT"),("disconnected","#FFFFFF","CONNECT")}){
        byte[] png=MenuBarMark.Png(status);
        using var bitmap=SkiaSharp.SKBitmap.Decode(png);var expected=SkiaSharp.SKColor.Parse(hex);
        Check(bitmap.Width==64&&bitmap.Height==64&&bitmap.Pixels.Count(p=>p==expected)>300,"actual-png-status-color-"+status);
        Check(bitmap.GetPixel(0,0).Alpha==0&&bitmap.Pixels.Any(p=>p.Alpha==255&&p==SkiaSharp.SKColor.Parse("#182231")),"transparent-background-with-contrast-outline-"+status);
        File.WriteAllBytes(Path.Combine(output,"tray-"+status+".png"),png);
        typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,new AgentReply{State=status});Invoke(trayWindow,"UpdateStatus");
        Check(ReferenceEquals(tray!.Icon,MenuBarMark.Icon(status))&&ReferenceEquals(tray.Menu,menuIdentity),"state-updates-actual-tray-icon-with-stable-menu-"+status);
        Check(tray.Menu!.Items.OfType<NativeMenuItem>().ElementAt(1).Header?.ToString()==L.Text(action),"tray-action-matches-status-"+status);
    }
    Check(ReferenceEquals(MenuBarMark.Icon("connecting"),MenuBarMark.Icon("engine-started")),"bootstrap-and-connecting-reuse-yellow-icon");
    using(var cancellation=new CancellationTokenSource()){
        // Only object identity is used here; progress updates must not start an Agent process.
        var currentAgent=(AgentClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AgentClient));
        var olderAgent=(AgentClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AgentClient));
        var stateField=typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!;
        typeof(MainWindow).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,currentAgent);
        foreach(string terminal in new[]{"connected","error","disconnected"}){
            stateField.SetValue(trayWindow,new AgentReply{State=terminal});Invoke(trayWindow,"UpdateStatus");
            Invoke(trayWindow,"UpdateConnectionStage",currentAgent,cancellation.Token,new AgentReply{State="engine-started"});
            Check(((AgentReply)stateField.GetValue(trayWindow)!).State==terminal&&ReferenceEquals(tray!.Icon,MenuBarMark.Icon(terminal)),"late-progress-cannot-overwrite-terminal-state-"+terminal);
        }
        stateField.SetValue(trayWindow,new AgentReply{State="connecting"});
        Invoke(trayWindow,"UpdateConnectionStage",olderAgent,cancellation.Token,new AgentReply{State="engine-started"});
        Check(((AgentReply)stateField.GetValue(trayWindow)!).State=="connecting","progress-from-previous-client-is-ignored");
        Invoke(trayWindow,"UpdateConnectionStage",currentAgent,cancellation.Token,new AgentReply{State="engine-started"});
        Check(((AgentReply)stateField.GetValue(trayWindow)!).State=="engine-started"&&ReferenceEquals(tray!.Icon,MenuBarMark.Icon("connecting")),"current-bootstrap-updates-yellow-icon");
        cancellation.Cancel();stateField.SetValue(trayWindow,new AgentReply{State="connecting"});
        Invoke(trayWindow,"UpdateConnectionStage",currentAgent,cancellation.Token,new AgentReply{State="engine-started"});
        Check(((AgentReply)stateField.GetValue(trayWindow)!).State=="connecting","cancelled-attempt-cannot-apply-progress");
        typeof(MainWindow).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,null);
    }
    {
        var stateField=typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var activeField=typeof(MainWindow).GetField("activeId",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var pendingField=typeof(MainWindow).GetField("connectingId",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var busyField=typeof(MainWindow).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic)!;
        stateField.SetValue(trayWindow,new AgentReply());activeField.SetValue(trayWindow,"");
        var pathMenu=(ContextMenu)Invoke(trayWindow,"PathMenu",profile)!;
        MenuItem Connection(ContextMenu menu)=>menu.Items.OfType<MenuItem>().First(item=>item.Tag as string is "CONNECT" or "DISCONNECT" or "CANCEL");
        Check(Connection(pathMenu).Header as string==L.Text("CONNECT"),"disconnected-profile-menu-offers-connect");
        stateField.SetValue(trayWindow,new AgentReply{State="connected"});activeField.SetValue(trayWindow,profile.Id);
        pathMenu.Open(trayWindow);Settle(trayWindow);
        Check(Connection(pathMenu).Header as string==L.Text("DISCONNECT"),"existing-profile-menu-refreshes-to-disconnect-on-open");pathMenu.Close();
        var otherMenu=(ContextMenu)Invoke(trayWindow,"PathMenu",second)!;
        Check(Connection(otherMenu).Header as string==L.Text("CONNECT"),"another-profile-does-not-offer-to-disconnect-active-one");
        var groupMenu=(ContextMenu)Invoke(trayWindow,"CollectionMenu",new List<Profile>{second,profile})!;
        Check(Connection(groupMenu).Header as string==L.Text("DISCONNECT"),"collection-menu-targets-its-connected-profile");
        Connection(pathMenu).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Settle(trayWindow);
        Check(((AgentReply)stateField.GetValue(trayWindow)!).State=="disconnected","disconnect-menu-item-performs-disconnect");
        using var cancelling=new CancellationTokenSource();
        typeof(MainWindow).GetField("connectStop",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,cancelling);
        pendingField.SetValue(trayWindow,profile.Id);stateField.SetValue(trayWindow,new AgentReply{State="engine-started"});busyField.SetValue(trayWindow,true);
        pathMenu.Open(trayWindow);Settle(trayWindow);
        Check(Connection(pathMenu).Header as string==L.Text("CANCEL")&&Connection(pathMenu).IsEnabled,"pending-profile-menu-offers-enabled-cancel");pathMenu.Close();
        otherMenu.Open(trayWindow);Settle(trayWindow);Check(!Connection(otherMenu).IsEnabled,"other-profile-connect-disabled-during-pending-attempt");otherMenu.Close();
        Connection(pathMenu).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Settle(trayWindow);
        Check(cancelling.IsCancellationRequested&&((AgentReply)stateField.GetValue(trayWindow)!).State=="disconnected","cancel-menu-item-cancels-pending-connection");
        typeof(MainWindow).GetField("connectStop",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,null);busyField.SetValue(trayWindow,false);
        stateField.SetValue(trayWindow,new AgentReply{State="connected"});activeField.SetValue(trayWindow,profile.Id);Invoke(trayWindow,"UpdateStatus");
    }
    var fresh=(HashSet<string>)typeof(MainWindow).GetField("freshMeasurements",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trayWindow)!;fresh.Add(profile.Id);Invoke(trayWindow,"BuildCards");Settle(trayWindow);
    var activePalette=(Palette)typeof(MainWindow).GetField("palette",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trayWindow)!;
    TextBlock Metric()=>trayWindow.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text=="HTTPS 42 ms");
    Check((Metric().Foreground as ISolidColorBrush)?.Color==Color.Parse(activePalette.ColorOf("Green")),"fresh-ping-is-green");fresh.Clear();Invoke(trayWindow,"BuildCards");Settle(trayWindow);
    Check((Metric().Foreground as ISolidColorBrush)?.Color==Color.Parse(activePalette.ColorOf("StaleMetric")),"previous-ping-is-light-gray");
    typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(trayWindow,true);trayWindow.Close();
    var defaults=new MainWindow(new AppState(),true);Check(defaults.Width==560&&defaults.Height==680,"14-inch-shorter-compact-default");typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(defaults,true);defaults.Close();
    foreach(double height in new[]{760d,700d}){var resized=new MainWindow(new AppState{WindowHeight=height},true);Check(resized.Height==(height==760?680:700),"old-default-height-migrates-custom-height-preserved-"+height);typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(resized,true);resized.Close();}
    {
        var w=new MainWindow(new AppState{Language="fa",Theme="dark",Profiles=[profile],SelectedId=profile.Id,SystemProxy=true,SystemTun=true},true){Width=560,Height=680};w.Show();Invoke(w,"ShowAppProxy");Settle(w);foreach(var section in w.GetVisualDescendants().OfType<Expander>())section.IsExpanded=true;Settle(w);
        TextBox Field(string name)=>w.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name==name);
        var save=w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="AppProxySave");
        Field("AppProxyHttpPort").Text="10808";Field("AppProxySocksPort").Text="10808";save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        AppState Saved()=>(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(Saved().SystemProxy&&Saved().SystemTun&&Saved().LocalSocksPort==0,"invalid-app-proxy-ports-cannot-change-routing-preferences");
        Field("AppProxyHttpPort").Text="10809";save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        Check(!Saved().SystemProxy&&!Saved().SystemTun&&Saved().LocalHttpPort==10809&&Saved().LocalSocksPort==10808,"header-save-activates-local-app-mode-with-stable-ports");
        Check(typeof(MainWindow).GetMethod("TelegramProxyLink",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[10808]) as string=="tg://socks?server=127.0.0.1&port=10808","telegram-link-uses-loopback-socks-port");
        Invoke(w,"ShowAppProxy");Settle(w);typeof(MainWindow).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,new AgentReply{State="connected",SocksPort=30333,HttpPort=30444});Invoke(w,"UpdateStatus");Settle(w);
        foreach(var section in w.GetVisualDescendants().OfType<Expander>())section.IsExpanded=true;Settle(w);
        Check(Field("AppProxySocksPort").Text=="30333"&&w.GetVisualDescendants().OfType<TextBlock>().Any(c=>c.Name=="AppProxyEndpoint"&&c.Text=="127.0.0.1:30333"),"app-proxy-displays-real-active-listener");
        Check(w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="AppProxyOpenTelegram").IsEnabled&&!w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="AppProxySave").IsEnabled,"telegram-action-ready-while-ports-remain-locked-during-connection");Capture(w,"dev6-app-proxy-fa-dark");
        var apps=w.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="AppProxyApplication");apps.SelectedIndex=1;var protocols=w.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="AppProxyProtocol");protocols.SelectedIndex=1;Settle(w);
        Check(w.GetVisualDescendants().OfType<TextBlock>().Any(c=>c.Name=="AppProxyEndpoint"&&c.Text=="127.0.0.1:30444")&&!w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="AppProxyOpenTelegram").IsEffectivelyVisible,"other-app-http-address-kept-separate-from-telegram-socks");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    {
        string Certificate(){
            using var key=System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            var request=new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=t.example.invalid",key,System.Security.Cryptography.HashAlgorithmName.SHA256);
            using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddDays(1));return certificate.ExportCertificatePem();
        }
        string pem=Certificate(),offered=pem;int discoveries=0;
        var slip=MacProtocols.Single(ManualProfiles.Dns("slipstream","Slip trust fixture","t.example.invalid","127.0.0.1:53","udp","","","22","",""));
        var w=new MainWindow(new AppState{Language="en",Profiles=[slip],SelectedId=slip.Id},true,certificateDiscovery:(_,_)=>{discoveries++;return Task.FromResult(offered);});w.Show();Settle(w);
        AppState Saved()=>(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        void Until(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted&&clock.ElapsedMilliseconds<5000){Settle(w);Thread.Sleep(4);}if(!task.IsCompleted)throw new Exception("identity decision did not complete");}
        Task Start(CancellationToken token=default,bool review=false)=>(Task)Invoke(w,"EnsureSlipIdentity",slip,token,review)!;
        var first=Start();Settle(w);Check(Saved().SlipCertificates.Count==0&&w.OwnedWindows.Count==1,"retrieved-slip-certificate-is-not-saved-before-owner-approval");
        var dialog=w.OwnedWindows.Single();dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("CONFIRM")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(first);first.GetAwaiter().GetResult();
        string scope=SlipCertificate.Scope(slip);Check(Saved().SlipCertificates.GetValueOrDefault(scope)==pem&&discoveries==1,"accepting-peer-fingerprint-saves-exact-domain-scoped-certificate");
        var reused=Start();Until(reused);reused.GetAwaiter().GetResult();Check(discoveries==1&&w.OwnedWindows.Count==0,"known-slip-identity-reused-without-file-picker-or-second-prompt");
        offered=Certificate();var changed=Start(review:true);Settle(w);Check(w.OwnedWindows.Single().GetVisualDescendants().OfType<TextBlock>().Any(c=>c.Text?.Contains(L.Text("MAC_SLIP_IDENTITY_CHANGED"))==true),"changed-slip-certificate-requires-explicit-new-decision");
        w.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("CANCEL")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Until(changed);Check(changed.IsCanceled&&Saved().SlipCertificates[scope]==pem,"rejecting-new-slip-identity-preserves-stored-certificate");
        using var cancellation=new CancellationTokenSource();var cancelled=Start(cancellation.Token,true);Settle(w);cancellation.Cancel();Until(cancelled);Check(cancelled.IsCanceled&&Saved().SlipCertificates[scope]==pem&&w.OwnedWindows.Count==0,"cancelled-certificate-review-closes-dialog-and-preserves-pin");
        var replacement=MacDnsEditing.Apply(slip,new Dictionary<int,string>{{3,"other.example.invalid"}},"127.0.0.1:53","udp","");Invoke(w,"SaveEdited",slip,replacement,false);
        Check((string)Invoke(w,"StoredSlipCertificate",replacement)! == "","changed-tunnel-domain-cannot-reuse-another-domains-certificate");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    foreach(bool fromEditor in new[]{true,false}){
        var dnsOriginal=MacProtocols.Single(ManualProfiles.Dns("dnstt_ssh","Original DNS","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"ssh.example.invalid","2222","fixture-user","fixture-secret"));dnsOriginal.CollectionId="dns-fixture";dnsOriginal.SourceId="fixture-subscription";dnsOriginal.ItemId="fixture-item";
        string name=fromEditor?"Unsaved form name":"Original DNS";
        string expectedPassword=fromEditor?"new secret | unicode ✓":"fixture-secret";
        var expected=MacDnsEditing.Apply(dnsOriginal,new Dictionary<int,string>{{2,name}},"127.0.0.1:53","tcp","",new("fixture-user",expectedPassword,"",""));
        var savedScan=new ScanCheckpoint{ProfileId=dnsOriginal.Id,ProfileFingerprint=Importer.Fingerprint(expected.Raw),Zone="t.example.invalid",Candidates=["127.0.0.2:53","127.0.0.3:53"],Transports=["tcp"],Results=[new(){Index=0,Transport="tcp",Pass=true,Dns=true,TunnelFinished=true,TunnelEstablished=true,HttpsVerified=true,TestedAt=DateTimeOffset.UtcNow},new(){Index=1,Transport="tcp",Error="DNS_FAILED"}]};
        var dnsState=new AppState{Language="en",Profiles=[dnsOriginal,profile],SelectedId=dnsOriginal.Id,Scan=savedScan};var dnsFlow=new MainWindow(dnsState,true){Width=480,Height=600};dnsFlow.Show();
        if(fromEditor){Invoke(dnsFlow,"ShowDnsEditor",dnsOriginal);Settle(dnsFlow);foreach(var section in dnsFlow.GetVisualDescendants().OfType<Expander>())section.IsExpanded=true;Settle(dnsFlow);dnsFlow.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Text==dnsOriginal.Name).Text=name;dnsFlow.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Text=="fixture-secret").Text=expectedPassword;dnsFlow.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="DnsAddresses").Text="";dnsFlow.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("FIND_DNS")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
        else Invoke(dnsFlow,"ShowScanner",dnsOriginal);
        Settle(dnsFlow);var apply=dnsFlow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerApply");
        var cpText=dnsFlow.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="ScannerCandidates");Check(cpText.Text==string.Join('\n',savedScan.Candidates),"saved-plan-not-randomized-on-reopen-"+fromEditor);
        Check(dnsFlow.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="ScannerPlanCount"&&t.Text!.Contains("2")),"separate-test-address-count-"+fromEditor);
        Check(!apply.IsEnabled,"scanner-needs-explicit-checkbox-selection-"+fromEditor);
        dnsFlow.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("MAC_SCAN_VIEW_RESULTS")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(dnsFlow);
        var choices=dnsFlow.GetVisualDescendants().OfType<CheckBox>().Where(c=>c.Name=="ScannerResultChoice").ToArray();Check(choices.Length==2&&choices.Count(c=>!c.IsEnabled)==1,"failed-result-cannot-be-selected-"+fromEditor);
        Check(!dnsFlow.GetVisualDescendants().OfType<ListBox>().Any(),"scanner-has-no-green-listbox-focus-for-failed-row-"+fromEditor);
        var firstUsable=choices.Single(c=>c.IsEnabled);var at=firstUsable.TranslatePoint(default,dnsFlow);Check(at.HasValue&&at.Value.Y>0&&at.Value.Y+firstUsable.Bounds.Height<dnsFlow.Bounds.Height-65,"usable-result-visible-without-scrolling-on-14inch-"+fromEditor);
        foreach(var export in dnsFlow.GetVisualDescendants().OfType<Expander>())export.IsExpanded=true;Settle(dnsFlow);
        Check(!dnsFlow.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerExport").IsEnabled,"empty-export-disabled-"+fromEditor);
        choices.Single(c=>c.IsEnabled).IsChecked=true;Settle(dnsFlow);
        var origin=apply.TranslatePoint(default,dnsFlow)!.Value;Check(apply.IsEnabled&&origin.Y>=0&&origin.Y+apply.Bounds.Height<600,"apply-button-stays-visible-in-compact-scanner-"+fromEditor);
        Capture(dnsFlow,"dev4-dns-selected-"+fromEditor);apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(dnsFlow);
        var after=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dnsFlow)!;var updated=after.Profiles.Single(p=>p.Id==dnsOriginal.Id);var f=Importer.Fields(updated.Raw);
        Check(after.Profiles.Count==2&&f[4].StartsWith("127.0.0.2:53")&&f[22]=="tcp"&&updated.Name==name,"scan-applies-to-existing-profile-without-duplicate-"+fromEditor);
        Check(f[15]=="fixture-user"&&f[16]==expectedPassword&&f[17]=="2222"&&updated.SourceId==dnsOriginal.SourceId&&updated.ItemId==dnsOriginal.ItemId&&after.OverrideSources[dnsOriginal.Id]==dnsOriginal.Raw,"scan-preserves-credentials-source-identity-and-override-"+fromEditor);
        Check(after.Profiles.Single(p=>p.Id==profile.Id).Raw==profile.Raw,"scan-leaves-other-profile-unchanged-"+fromEditor);
        if(fromEditor)Check(dnsFlow.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="DnsAddresses").Text=="127.0.0.2:53","scan-returns-to-the-original-form");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dnsFlow,true);dnsFlow.Close();
    }
    foreach(var (wireMode,modeIndex) in new[]{("advanced",1),("prism",2),("e2e",3)}){
        var p=MacProtocols.Single(ManualProfiles.Dns("dnstt","Fixture","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"","22","",""));
        var checksFixture=new TwoStageFixture();var w=new MainWindow(new AppState{Language="fa",Profiles=[p],SelectedId=p.Id},true,checksFixture){Width=480,Height=600};w.Show();Invoke(w,"ShowScanner",p);Settle(w);
        var mode=w.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="ScannerMode");mode.SelectedIndex=modeIndex;Settle(w);
        var mark=w.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="ScannerTunnelToggle");
        var full=w.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="ScannerFullHttps");
        void Until(Func<bool> predicate){var watch=System.Diagnostics.Stopwatch.StartNew();do{Settle(w);if(predicate())return;Thread.Sleep(4);}while(watch.ElapsedMilliseconds<6000);throw new Exception("phase did not settle: "+wireMode);}
        AppState Saved()=>(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(mark.IsEffectivelyVisible&&!mark.IsEnabled&&mark.IsChecked!=true&&full.IsChecked!=true,"manual-and-https-initially-off-"+wireMode);
        if(wireMode=="advanced")checksFixture.HoldDns=true;
        if(wireMode=="e2e")full.IsChecked=true;
        w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerStart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(()=>mark.IsEnabled&&mark.IsChecked!=true);
        if(wireMode=="e2e")Check(checksFixture.First==1&&checksFixture.DnsRuns==0&&checksFixture.Https==1&&Saved().Scan!.Results.Single() is {ServerReached:true,HttpsVerified:true,TunnelFinished:true},"direct-e2e-with-https-on-first-connect-without-dns");
        else Check(checksFixture.DnsRuns==1&&checksFixture.First==0&&checksFixture.Second==0&&checksFixture.Https==0,"only-dns-before-manual-click-"+wireMode);
        if(wireMode=="advanced")Capture(w,"dev5-first-stage-and-tunnel-checkbox-fa");
        if(wireMode=="prism")full.IsChecked=true;
        checksFixture.Lower=true;mark.IsChecked=true;Until(()=>checksFixture.Second==1);
        Check(mark.IsChecked==true&&checksFixture.LastProfile==p.Id&&checksFixture.LastWire=="tcp"&&checksFixture.LastAddress=="127.0.0.1:53","same-profile-address-and-wire-"+wireMode);
        if(wireMode=="advanced"){
            Check(checksFixture.Https==0&&w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerStopDns").IsEnabled,"manual-handshake-overlaps-upper-dns");
            w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerStopDns").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Until(()=>!w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerStopDns").IsEnabled);
            Check(mark.IsChecked==true,"stopping-dns-leaves-tunnel-running");
        }
        if(wireMode=="prism"){
            mark.IsChecked=false;Until(()=>mark.IsEnabled&&mark.IsChecked!=true);
            Check(!Saved().Scan!.Results[0].TunnelFinished,"cancelled-stage-is-not-recorded-as-failure");
            mark.IsChecked=true;Until(()=>checksFixture.Second==2);
        }
        checksFixture.Release.TrySetResult();Until(()=>mark.IsChecked!=true);var result=Saved().Scan!.Results.Single();
        Check(result.TunnelFinished&&result.TunnelEstablished&&result.HttpsVerified==(wireMode!="advanced")&&result.TunnelElapsedMS==72&&(wireMode=="e2e"||result.DnsElapsedMS==28),"independent-measured-times-and-evidence-"+wireMode);
        if(wireMode=="advanced"){
            checksFixture.FailHttp=true;full.IsChecked=true;Until(()=>mark.IsEnabled);mark.IsChecked=true;Until(()=>checksFixture.Second==2&&mark.IsChecked!=true);
            result=Saved().Scan!.Results.Single();Check(result.TunnelEstablished&&result.Selectable&&result.HttpsAttempted&&!result.HttpsVerified&&result.HttpsElapsedMS==30&&result.DnsElapsedMS==28,"failed-optional-https-does-not-erase-server-success-or-dns-time");
        }
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    {
        var p=MacProtocols.Single(ManualProfiles.Dns("dnstt","Save during scan","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"","22","",""));
        var fake=new TwoStageFixture{HoldDns=true};var w=new MainWindow(new AppState{Language="en",Profiles=[p],SelectedId=p.Id},true,fake);w.Show();Invoke(w,"ShowScanner",p);Settle(w);
        w.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="ScannerMode").SelectedIndex=1;var source=w.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="ScannerSource");source.SelectedIndex=2;w.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="ScannerCandidates").Text="127.0.0.9:53";
        w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ScannerStart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var row=w.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="ScannerResultChoice");row.IsChecked=true;Settle(w);
        var pin=w.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="ScannerApply");Check(pin.IsEnabled,"fixed-confirm-available-on-qualified-selection-during-scan");pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var budget=System.Diagnostics.Stopwatch.StartNew();while(budget.ElapsedMilliseconds<5000&&w.GetVisualDescendants().OfType<Button>().Any(c=>c.Name=="ScannerApply")){Settle(w);Thread.Sleep(4);}
        var final=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        Check(Importer.Fields(final.Profiles.Single().Raw)[4].StartsWith("127.0.0.9:53")&&final.Profiles[0].Id==p.Id&&fake.Second==0,"single-header-click-stops-and-saves-selected-dns-to-original-profile");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    foreach(string family in new[]{"dnstt","soar"}){
        var p=MacProtocols.Single(ManualProfiles.Dns(family,"Simple fixture","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"","22","",""));
        var fake=new TwoStageFixture();var w=new MainWindow(new AppState{Language="en",Profiles=[p],SelectedId=p.Id},true,fake);w.Show();Invoke(w,"ShowScanner",p);Settle(w);
        w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerStart").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var timer=System.Diagnostics.Stopwatch.StartNew();AppState Saved()=>(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
        while(timer.ElapsedMilliseconds<5000&&Saved().Scan?.Results.Any(r=>r.TunnelFinished)!=true){Settle(w);Thread.Sleep(4);}
        Check(Saved().Scan?.Results.SingleOrDefault() is {TunnelFinished:true,TunnelEstablished:true,HttpsAttempted:false}&&fake.DnsRuns==(family=="soar"?0:1)&&fake.First==1&&fake.Second==0&&!w.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="ScannerTunnelToggle").IsEffectivelyVisible,"simple-automatic-one-session-correct-prefilter-"+family);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    foreach(bool pool in new[]{true,false}){
        var sample=MacProtocols.Single(ManualProfiles.Dns("dnstt","Saved profile","t.example.invalid","127.0.0.1:53","tcp",new string('a',64),"","22","",""));
        var cp=new ScanCheckpoint{Zone="t.example.invalid",ProfileId=sample.Id,ProfileFingerprint=Importer.Fingerprint(sample.Raw),Candidates=["127.0.0.7:53"],Results=[new(){Index=0,Transport="tcp",Pass=true,Dns=true}],Selected=["0:tcp"]};
        var w=new MainWindow(new AppState{Language="en",Profiles=[sample],SelectedId=sample.Id,Scan=cp},true);w.Show();Invoke(w,"ShowResolverPreferences",pool);Settle(w);
        w.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("FIND_DNS")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var applyList=w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ScannerApply");Check(applyList.IsEnabled&&applyList.Content as string==L.Text("MAC_APPLY_LIST"),"apply-target-is-originating-list-"+pool);
        applyList.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        Check(w.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="ResolverPreferenceList").Text=="127.0.0.7:53","selection-returns-to-list-"+pool);
        var stateAfter=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;Check(stateAfter.Profiles[0].Raw==sample.Raw&&stateAfter.GlobalResolverList==""&&stateAfter.DnsPoolList=="","list-apply-never-overwrites-profile-or-saves-before-confirm-"+pool);
        var confirm=w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PageConfirm");confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var stored=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;Check((pool?stored.DnsPoolList:stored.GlobalResolverList)=="127.0.0.7:53"&&stored.Profiles[0].Raw==sample.Raw,"fixed-confirm-saves-intended-list-"+pool);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    {
        var w=new MainWindow(new AppState{Language="en"},true);w.Show();Invoke(w,"ShowResolverBanks","resolvers.txt",new Action<string>(_=>{}));Settle(w);
        w.GetVisualDescendants().OfType<Button>().First(b=>b.Content as string==L.Text("MAC_BANK_BROWSE")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(w);
        var bankLabel=w.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="BankFound");var total=60370;Check(total==60370&&bankLabel.Text!.Contains(total.ToString("N0")),"offline-full-bank-count-not-sample-limit");
        var search=w.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="BankSearch");search.Text="invalid resolver search";Settle(w);Check(bankLabel.Text==string.Format(L.Text("MAC_BANK_MATCHES"),0,total,0,0),"offline-bank-search-empty-state");
        search.Text=".";Settle(w);Check(bankLabel.Text!=string.Format(L.Text("MAC_BANK_MATCHES"),0,total,0,0),"offline-search-actually-finds-address");
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,true);w.Close();
    }
    foreach(string family in new[]{"dnstt","sayedns","soar","vaydns","slipstream"}){
        var dnsProfile=MacProtocols.Single(ManualProfiles.Dns(family,"DNS form","t.example.invalid","127.0.0.1:53",family=="slipstream"?"udp":"tcp",new string('a',64),"","22","",""));
        var dnsWindow=new MainWindow(new AppState{Language="fa",Profiles=[dnsProfile],SelectedId=dnsProfile.Id},true);dnsWindow.Show();Invoke(dnsWindow,"ShowDnsEditor",dnsProfile);Settle(dnsWindow);foreach(var advancedDns in dnsWindow.GetVisualDescendants().OfType<Expander>())advancedDns.IsExpanded=true;Settle(dnsWindow);
        Check(dnsWindow.GetVisualDescendants().OfType<ComboBox>().Any(c=>c.Name=="DnsTransport"),"dns-family-transport-choice-"+family);
        Check(dnsWindow.GetVisualDescendants().OfType<Control>().Any(c=>c.Name=="DnsField43")==(family=="vaydns"),"vaydns-controls-only-in-vaydns-"+family);
        Check(dnsWindow.GetVisualDescendants().OfType<Control>().Any(c=>c.Name=="DnsField6")== (family=="slipstream"),"slip-controls-only-in-slipstream-"+family);
        var formScroll=dnsWindow.GetVisualDescendants().OfType<ScrollViewer>().Single(c=>c.Name=="PageScroll");var commitButton=dnsWindow.GetVisualDescendants().OfType<Button>().Single(c=>c.Name=="PageConfirm");
        var top=commitButton.TranslatePoint(default,dnsWindow);formScroll.Offset=new Vector(0,2000);Settle(dnsWindow);var scrolled=commitButton.TranslatePoint(default,dnsWindow);
        Check(formScroll.Offset.Y>0&&top.HasValue&&scrolled==top&&top.Value.Y<95&&!commitButton.GetVisualAncestors().OfType<ScrollViewer>().Any(),"confirm-stays-in-header-after-real-scroll-"+family);
        if(family=="vaydns"){
            var compat=dnsWindow.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="DnsField41");var idBox=dnsWindow.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="DnsField49");idBox.Text="4";compat.IsChecked=true;Settle(dnsWindow);
            Check(!idBox.IsEnabled&&idBox.Text=="8","vaydns-compatible-mode-shows-engines-fixed-eight-byte-id");
            compat.IsChecked=false;Settle(dnsWindow);Check(idBox.IsEnabled&&idBox.Text=="4","vaydns-editable-id-restored-when-compatibility-is-off");}
        if(family=="soar"){var discovery=dnsWindow.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="DnsField79");discovery.IsChecked=true;var source=dnsWindow.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="DnsField82");source.SelectedItem="custom";Settle(dnsWindow);Check(!dnsWindow.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="DnsField80").IsEffectivelyVisible&&dnsWindow.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="DnsField83").IsEffectivelyVisible,"soar-custom-list-distinct-from-country");}
        if(family is "dnstt" or "sayedns"){Check(dnsWindow.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Name=="DnsField27").Content as string==L.Text("AUTHORITATIVE")&&dnsWindow.GetVisualDescendants().OfType<CheckBox>().Any(c=>c.Name=="DnsField38")== (family=="sayedns"),"authoritative-is-not-noiz-only-control-"+family);}
        var wireChoice=dnsWindow.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="DnsTransport");
        if(family is "dnstt" or "sayedns" or "vaydns"){
            wireChoice.SelectedItem="https";Settle(dnsWindow);var ip=dnsWindow.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="DnsAddresses");var dohBox=dnsWindow.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="DnsDoh");
            Check(!ip.IsEffectivelyVisible&&dohBox.IsEffectivelyVisible,"doh-hides-unrelated-ip-field-"+family);dohBox.Text="https://resolver.example.invalid/dns-query";
            dnsWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("SAVE")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(dnsWindow);
            var after=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dnsWindow)!;
            Check(Importer.Fields(after.Profiles.Single().Raw)[22]=="https","doh-form-actually-saves-"+family);
        }
        Invoke(dnsWindow,"ShowScanner",dnsProfile);Settle(dnsWindow);
        dnsWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==L.Text("MAC_SCAN_VIEW_RESULTS")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(dnsWindow);
        foreach(var tools in dnsWindow.GetVisualDescendants().OfType<Expander>())tools.IsExpanded=true;Settle(dnsWindow);
        Check(dnsWindow.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="ScannerMode").ItemCount==(family=="slipstream"?3:4),"only-appropriate-scanner-modes-"+family);
        Check(new[]{"MAC_SCAN_TEST_TUNNEL","MAC_SCAN_EXPORT_REPORT","MAC_SCAN_SELECT_VISIBLE","DNS_POOL_SAVE"}.All(key=>dnsWindow.GetVisualDescendants().OfType<Button>().Any(b=>b.Content as string==L.Text(key))),"scanner-complete-result-tools-"+family);
        Capture(dnsWindow,"dns-scanner-"+family);Invoke(dnsWindow,"ShowResolverPreferences",false);Settle(dnsWindow);
        Check(dnsWindow.GetVisualDescendants().OfType<TextBox>().Any(b=>b.Name=="ResolverPreferenceList"),"global-resolver-real-editor-"+family);
        Invoke(dnsWindow,"ShowSettings");Settle(dnsWindow);var tunOption=dnsWindow.GetVisualDescendants().OfType<CheckBox>().Single(b=>b.Name=="SystemTunOption");tunOption.IsChecked=true;Settle(dnsWindow);
        var tunData=(AppState)typeof(MainWindow).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(dnsWindow)!;Check(tunData.SystemTun&&tunData.SystemProxy,"tun-choice-preserves-existing-proxy-preference-"+family);
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(dnsWindow,true);dnsWindow.Close();
    }
    foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"})foreach(int width in new[]{480,560,920,1200}){
        var initial=new AppState{Language=language,Theme=theme,Profiles=[profile,second],SelectedId=profile.Id,Expanded=new(){["fixture"]=true},Measurements=new(){[profile.Id]=new(true,42,DateTimeOffset.UtcNow,"https",1,1)}};
        Uri? launchedChannel=null;var window=new MainWindow(initial,true,externalLauncher:uri=>{launchedChannel=uri;return Task.FromResult(true);}){Width=width,Height=width==480?560:760};window.Show();Dispatcher.UIThread.RunJobs();window.UpdateLayout();
        var sidebar=(Control)typeof(MainWindow).GetField("sidebar",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        Check(sidebar.IsEffectivelyVisible==(width>=920),$"sidebar-{language}-{theme}-{width}");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="server.example.invalid"&&t.FlowDirection==FlowDirection.LeftToRight),$"technical-ltr-{language}-{theme}-{width}");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==L.Text("MAC_SYSTEM_PROXY_IDLE")),$"status-visible-{language}-{theme}-{width}");
        Check(window.GetVisualDescendants().OfType<TextBlock>().All(t=>t.Text?.Contains("MAC_PENDING")!=true),$"translated-{language}-{theme}-{width}");
        var pathEdit=window.GetVisualDescendants().OfType<Button>().First(b=>Avalonia.Automation.AutomationProperties.GetName(b)==L.Text("EDIT"));var editOrigin=pathEdit.TranslatePoint(default,window)!.Value;
        Check(editOrigin.X>=0&&editOrigin.X+pathEdit.Bounds.Width<=window.Bounds.Width,$"path-actions-fit-{language}-{theme}-{width}");
        Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="HTTPS 42 ms"&&t.IsEffectivelyVisible),$"measured-result-visible-{language}-{theme}-{width}");
        var pathTitle=window.GetVisualDescendants().OfType<OverflowMarquee>().First(t=>t.Text==profile.Name&&t.GetVisualAncestors().OfType<Border>().Any(b=>b.Name=="PathCard"));
        Check(pathTitle.Bounds.Width>30,$"path-text-has-width-{language}-{theme}-{width}");
        var collectionTitle=window.GetVisualDescendants().OfType<OverflowMarquee>().First(t=>t.Text==profile.CollectionName);
        Check(collectionTitle.Bounds.Width>30,$"collection-text-has-width-{language}-{theme}-{width}");
        Check(window.GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="PathCard").All(b=>b.Bounds.Height==52),$"fixed-path-height-{language}-{theme}-{width}");
        var add=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="AddButton");var addPoint=add.TranslatePoint(new Point(30,30),window)!.Value;
        window.MouseMove(addPoint,RawInputModifiers.None);Dispatcher.UIThread.RunJobs();window.UpdateLayout();var addPresenter=add.GetVisualDescendants().OfType<ContentPresenter>().First(p=>p.Name=="PART_ContentPresenter");
        Check(addPresenter.Background is ISolidColorBrush hover&&hover.Color==Color.Parse("#DCD8F1"),$"add-keeps-color-on-hover-{language}-{theme}-{width}");
        window.MouseDown(addPoint,MouseButton.Left,RawInputModifiers.None);Dispatcher.UIThread.RunJobs();window.UpdateLayout();Check(addPresenter.Background is ISolidColorBrush pressed&&pressed.Color==Color.Parse("#DCD8F1"),$"add-keeps-color-on-press-{language}-{theme}-{width}");window.MouseMove(default,RawInputModifiers.None);window.MouseUp(default,MouseButton.Left,RawInputModifiers.None);
        var footer=(TextBlock)typeof(MainWindow).GetField("statusText",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;var footerOrigin=footer.TranslatePoint(default,window)!.Value;Check(footerOrigin.Y>=0&&footerOrigin.Y+footer.Bounds.Height<=window.Bounds.Height,$"status-fits-height-{language}-{theme}-{width}");
        Capture(window,$"home-{language}-{theme}-{width}");
        if(width is 480 or 560){
            Invoke(window,"ShowAbout");Settle(window);
            var controls=window.GetVisualDescendants().OfType<Button>().ToArray();
            Check(controls.Any(b=>b.Name=="AboutOpenCommunity"&&b.Content as string==L.Text("ABOUT_OPEN_COMMUNITY")),"about-channel-action-"+language+"-"+theme+"-"+width);
            controls.Single(b=>b.Name=="AboutOpenCommunity").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(window);Check(launchedChannel?.AbsoluteUri=="https://t.me/RovunetVPN","channel-launches-exact-official-address-"+language+"-"+theme+"-"+width);
            controls.Single(b=>b.Name=="AboutCopySupportAddress").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(window);Check(window.Clipboard!.TryGetTextAsync().GetAwaiter().GetResult()==wallet,"wallet-copy-exact-"+language+"-"+theme+"-"+width);
            var attribution=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="AboutAttribution");
            Check(attribution.Text!.Contains("Based on Rovunet/SlipNet 2.5.6")&&attribution.FlowDirection==FlowDirection.LeftToRight&&attribution.FontSize==10,"small-english-attribution-"+language+"-"+theme+"-"+width);
            var update=controls.Single(b=>b.Name=="AboutCheckUpdate");Check(update.TranslatePoint(default,window)!.Value.Y<attribution.TranslatePoint(default,window)!.Value.Y,"update-above-attribution-"+language+"-"+theme+"-"+width);
            var expander=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="AboutSupportQrExpander");expander.IsExpanded=true;Settle(window);
            var image=window.GetVisualDescendants().OfType<Image>().Single(i=>i.Name=="AboutSupportQr");image.BringIntoView();Settle(window);
            Check(image.Bounds.Width==222&&image.Bounds.Height==222,"wallet-qr-square-small-window-"+language+"-"+theme+"-"+width);
            using(var rendered=new RenderTargetBitmap(new PixelSize(222,222),new Vector(96,96))){rendered.Render(image);using var stream=new MemoryStream();rendered.Save(stream,PngBitmapEncoderOptions.Default);Check(DecodeQr(stream.ToArray())==wallet,"rendered-wallet-qr-decodes-"+language+"-"+theme+"-"+width);}
            Check(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Name=="AboutSaveSupportQr"),"wallet-qr-export-action-"+language+"-"+theme+"-"+width);
            Capture(window,$"about-wallet-{language}-{theme}-{width}");
            update.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Settle(window);Check(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Name=="CheckMacUpdate"),"in-app-updater-opens-"+language+"-"+theme+"-"+width);
            Capture(window,$"update-{language}-{theme}-{width}");
            var bodyField=typeof(MainWindow).GetField("currentBody",BindingFlags.Instance|BindingFlags.NonPublic)!;var activeUpdate=bodyField.GetValue(window);
            using(var operation=new CancellationTokenSource()){var stopField=typeof(MainWindow).GetField("updateStop",BindingFlags.Instance|BindingFlags.NonPublic)!;stopField.SetValue(window,operation);Invoke(window,"GoHome");Invoke(window,"ShowUpdates");Settle(window);Check(ReferenceEquals(activeUpdate,bodyField.GetValue(window)),"ongoing-update-reopens-same-progress-view-"+language+"-"+theme+"-"+width);stopField.SetValue(window,null);}
            Invoke(window,"ShowPublic");Settle(window);
            Check(window.GetVisualDescendants().OfType<Button>().Count(b=>(b.Content as string)?.Contains(L.Text("REVIEW_IMPORT"))==true)==3,"three-prepared-public-sources-"+language+"-"+theme+"-"+width);
            foreach(string source in new[]{PublicFeed.MahsaUrl,PublicFeed.LegacyMahsaUrl,PublicFeed.Presets.Single(p=>p.Key=="anonymous").Url,PublicFeed.PatternihaUrl}){
                Invoke(window,"ShowSubscription",source,"");Settle(window);
                Check(!window.GetVisualDescendants().OfType<TextBox>().Any(t=>t.IsEffectivelyVisible&&t.Text==source),"public-source-url-not-visible-"+PublicFeed.NameFor(source)+"-"+language+"-"+theme+"-"+width);
                Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="PublicSourceIdentity"&&t.IsEffectivelyVisible&&t.Text==PublicFeed.DisplayName(source)),"public-source-identity-visible-"+PublicFeed.NameFor(source)+"-"+language+"-"+theme+"-"+width);
            }
            Invoke(window,"ShowSubscription","","");Settle(window);
            var pastedAddress=window.GetVisualDescendants().OfType<TextBox>().Last();pastedAddress.Text=PublicFeed.MahsaUrl;Settle(window);
            Check(!pastedAddress.IsEffectivelyVisible&&window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="PublicSourceIdentity"&&t.IsEffectivelyVisible),"pasted-public-source-hides-url-and-shows-identity-"+language+"-"+theme+"-"+width);
            Capture(window,"public-source-"+language+"-"+theme+"-"+width);
            Invoke(window,"GoHome");Settle(window);
        }

        if(width==480){
            var scroll=(ScrollViewer)typeof(MainWindow).GetField("homeScroll",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;for(int i=0;i<3;i++){scroll.Offset=new Vector(0,60);Dispatcher.UIThread.RunJobs();window.UpdateLayout();}
            var detail=(Control)typeof(MainWindow).GetField("heroDetailLine",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            var power=(Button)typeof(MainWindow).GetField("heroPower",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            Check(Math.Abs(power.Bounds.Width-power.Bounds.Height)<.1&&power.Bounds.Width<=64,$"collapsed-power-remains-circular-{language}-{theme}");
            Check(!detail.IsVisible,$"collapsed-hero-hides-whole-marquee-{language}-{theme}");scroll.Offset=default;for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();window.UpdateLayout();}
        }
        if(language is "fa" or "en"&&theme=="dark"){
            foreach(var (method,key) in new[]{("ShowSettings","settings"),("ShowConnectionDetails","connection"),("ShowManagement","collections"),("ShowBackup","backup"),("ShowManual","manual"),("ShowAddHub","add"),("ShowGeneral","general"),("ShowAbout","about"),("ShowHelp","help")}){
                Invoke(window,method);Dispatcher.UIThread.RunJobs();window.UpdateLayout();
                Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==L.Text("MAC_SYSTEM_PROXY_IDLE")),$"fixed-status-{key}-{language}-{width}");if(method=="ShowSettings")Check(window.GetVisualDescendants().OfType<CheckBox>().Any(b=>b.Content as string==L.Text("SYSTEM_PROXY")&&b.IsEnabled&&b.IsChecked==true),$"system-proxy-option-enabled-{language}-{width}");Capture(window,$"{key}-{language}-{theme}-{width}");Invoke(window,"GoHome");
            }
            Invoke(window,"ShowEditor",profile,false);Dispatcher.UIThread.RunJobs();Capture(window,$"vless-editor-{language}-{width}");
            Check(window.GetVisualDescendants().OfType<TextBox>().Any(b=>b.PasswordChar!='\0'),$"masked-auth-{language}-{width}");Invoke(window,"GoHome");
            if(width==560)foreach(string kind in new[]{"Shadowsocks 2022","Hysteria2 hopping","AmneziaWG","Tor","DoH","AnyTLS","Mieru","TUIC"}){
                Invoke(window,"NewProfile",kind);Settle(window);Check(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Content as string==L.Text("SAVE")),"extended-editor-opens-"+kind+"-"+language);
                Check(!window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.StartsWith("MAC_",StringComparison.Ordinal)==true),"extended-editor-localized-"+kind+"-"+language);
                Capture(window,$"extended-editor-{kind.Replace(' ','-')}-{language}");Invoke(window,"GoHome");
            }
            Invoke(window,"ShowScanner",profile);Dispatcher.UIThread.RunJobs();window.UpdateLayout();Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains(L.Text("SCAN_SOURCE_APP"))==true),$"offline-bank-visible-in-scanner-{language}-{width}");Capture(window,$"scanner-{language}-{width}");Invoke(window,"GoHome");
            Invoke(window,"ShowResolverBanks","resolvers.txt",new Action<string>(_=>{}));Settle(window);Check(window.GetVisualDescendants().OfType<Button>().Count(b=>b.Content as string==L.Text("DNS_BANK_USE"))==6,$"all-six-offline-banks-visible-{language}-{width}");var bankFooter=(TextBlock)typeof(MainWindow).GetField("statusText",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;var bankFooterPoint=bankFooter.TranslatePoint(default,window)!.Value;Check(bankFooter.IsEffectivelyVisible&&bankFooter.Bounds.Height>0&&bankFooterPoint.X>=0&&bankFooterPoint.X+bankFooter.Bounds.Width<=window.Bounds.Width&&bankFooterPoint.Y+bankFooter.Bounds.Height<=window.Bounds.Height,$"bank-picker-footer-fits-{language}-{width}");if(language=="fa"&&width==560)Console.WriteLine($"bank footer x={bankFooterPoint.X} y={bankFooterPoint.Y} bounds={bankFooter.Bounds}");Capture(window,$"resolver-banks-{language}-{width}");Invoke(window,"GoHome");
            SafeDiagnostics.Record("import-review","MAC_OPTION_NOT_READY","mport");Invoke(window,"ShowDiagnostics");Dispatcher.UIThread.RunJobs();window.UpdateLayout();Check(window.GetVisualDescendants().OfType<TextBox>().Any(t=>t.IsReadOnly&&t.Text?.Contains("mport")==true&&t.Text.Contains("server.example.invalid")==false),$"diagnostics-readable-and-redacted-{language}-{width}");Capture(window,$"diagnostics-{language}-{width}");Invoke(window,"GoHome");
            Invoke(window,"ShowEditor",profile,false);Dispatcher.UIThread.RunJobs();window.UpdateLayout();var unsaved=window.GetVisualDescendants().OfType<TextBox>().First(b=>b.Text==profile.Name);unsaved.Text="Unsaved UI fixture";
            Invoke(window,"ShowSettings");Dispatcher.UIThread.RunJobs();window.UpdateLayout();var light=window.GetVisualDescendants().OfType<Button>().First(b=>b.Content as string==L.Text("LIGHT_THEME"));light.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Invoke(window,"Back");Dispatcher.UIThread.RunJobs();window.UpdateLayout();
            Check(window.GetVisualDescendants().OfType<TextBox>().Any(b=>b.Text=="Unsaved UI fixture"),$"appearance-keeps-unsaved-editor-{language}-{width}");Invoke(window,"GoHome");
            Check(unsaved.Background is ISolidColorBrush updated&&updated.Color==Color.Parse(new Palette("light",false).ColorOf("Card")),$"saved-editor-receives-new-theme-colors-{language}-{width}");
        }
        typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,true);window.Close();
    }
    // Exercise the rendered control, including direction, hover and resize. This
    // catches a zero-width text viewport even when its TextBlock still exists.
    L.Language="fa";
    const string longText="long-host-name-for-a-compact-fourteen-inch-window.example.invalid";
    var latinLabel=new TextBlock{Text=longText,FontSize=14,FlowDirection=FlowDirection.LeftToRight};
    var latin=new OverflowMarquee(latinLabel,24);
    var persian=new OverflowMarquee(new TextBlock{Text="نام بسیار طولانی برای آزمایش حرکت قطاری نوشته در قاب کوچک مک بوک",FontFamily=L.Font,FontSize=14,FlowDirection=FlowDirection.RightToLeft},24);
    var shortLine=new OverflowMarquee(new TextBlock{Text="Short",FontSize=14,FlowDirection=FlowDirection.LeftToRight},24);
    var tickerWindow=new Window{Width=260,Height=180,Content=new StackPanel{Margin=new(20),Spacing=8,Children={latin,persian,shortLine}}};tickerWindow.Show();
    void Pump(){for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();tickerWindow.UpdateLayout();}}
    void Advance(OverflowMarquee control,double seconds)=>typeof(OverflowMarquee).GetMethod("Advance",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,[seconds]);
    void Reset(OverflowMarquee control)=>typeof(OverflowMarquee).GetMethod("Reflow",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,null);
    Pump();Reset(latin);Reset(persian);
    Check(latin.IsOverflowing&&persian.IsOverflowing,"marquee-long-values-overflow");
    Check(latin.ClipToBounds&&latin.Bounds.Height==24&&latinLabel.TextWrapping==TextWrapping.NoWrap,"marquee-single-line-clipped");
    Advance(latin,.5);Check(latin.MotionOffset==0,"marquee-reading-delay");Advance(latin,2);Advance(persian,2);
    Check(latin.MotionOffset<0&&persian.MotionOffset>0,"marquee-ltr-rtl-directions");
    var rail=(Canvas)latin.Child!;var duplicate=rail.Children.OfType<TextBlock>().Last();
    Check(Canvas.GetLeft(duplicate)>latinLabel.Bounds.Width&&duplicate.Text==longText,"marquee-continuous-second-copy");
    tickerWindow.MouseMove(latin.TranslatePoint(new Point(4,10),tickerWindow)!.Value,Avalonia.Input.RawInputModifiers.None);
    double paused=latin.MotionOffset;Advance(latin,3);Check(latin.IsPointerOver&&latin.MotionOffset==paused,"marquee-hover-pauses");
    tickerWindow.MouseMove(new Point(250,160),Avalonia.Input.RawInputModifiers.None);Advance(latin,1);Check(latin.MotionOffset!=paused,"marquee-leave-resumes");
    Advance(shortLine,10);Check(!shortLine.IsOverflowing&&shortLine.MotionOffset==0,"marquee-short-text-stationary");
    tickerWindow.Width=900;Pump();Check(!latin.IsOverflowing&&latin.MotionOffset==0,"marquee-resize-stops-when-text-fits");
    tickerWindow.Width=260;latinLabel.Text="Short";Pump();Check(!latin.IsOverflowing&&latin.MotionOffset==0,"marquee-content-update-remeasures");
    latinLabel.Text=longText;Pump();Check(latin.IsOverflowing&&latin.Text==longText,"marquee-original-text-preserved");
    Capture(tickerWindow,"marquee-test-fixture");
    var marqueeClock=(DispatcherTimer)typeof(OverflowMarquee).GetField("timer",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
    bool VisibleForAnimation(OverflowMarquee item)=>(bool)typeof(OverflowMarquee).GetMethod("InViewport",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(item,null)!;
    tickerWindow.Activate();Pump();
    Check(tickerWindow.IsActive&&VisibleForAnimation(latin)&&marqueeClock.IsEnabled,"marquee-focused-window-runs-shared-clock");
    double heldOffset=latin.MotionOffset;
    var focusWindow=new Window{Width=200,Height=100};focusWindow.Show();focusWindow.Activate();Pump();
    // Headless windows do not implement the OS-wide foreground switch. Raise
    // the same platform callback used by native deactivation explicitly.
    var tickerImpl=typeof(Window).GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).First(p=>p.Name=="PlatformImpl"&&p.PropertyType.Name=="IWindowImpl").GetValue(tickerWindow)!;
    ((Action)tickerImpl.GetType().GetProperty("Deactivated")!.GetValue(tickerImpl)!)();Pump();
    Check(!tickerWindow.IsActive&&!VisibleForAnimation(latin)&&!marqueeClock.IsEnabled,"marquee-unfocused-window-stops-animation-and-clock");
    Check(latin.MotionOffset==heldOffset,"marquee-lost-focus-preserves-reading-position");
    tickerWindow.Activate();Pump();
    Check(tickerWindow.IsActive&&VisibleForAnimation(latin)&&marqueeClock.IsEnabled&&latin.MotionOffset==heldOffset,"marquee-regained-focus-resumes-without-reset-or-jump");
    tickerWindow.WindowState=WindowState.Minimized;Pump();
    Check(!VisibleForAnimation(latin)&&!marqueeClock.IsEnabled,"marquee-minimized-window-stops-shared-clock");
    tickerWindow.WindowState=WindowState.Normal;tickerWindow.Activate();Pump();
    Check(VisibleForAnimation(latin)&&marqueeClock.IsEnabled,"marquee-restored-active-window-resumes-clock");
    focusWindow.Close();tickerWindow.Close();
    Check(!marqueeClock.IsEnabled,"marquee-detach-releases-window-and-stops-clock");
    var longProfile=Importer.Single("vless://00000000-0000-4000-8000-000000000001@"+longText+":443?type=ws&security=tls&path=%2Fdemo#"+Uri.EscapeDataString("مسیر نمونه با نام طولانی برای نمایش حرکت قطاری در پنجره کوچک مک"));longProfile.CollectionId="long-fixture";longProfile.CollectionName="مجموعه نمونه با نام طولانی برای آزمایش نمایش در مک بوک چهارده اینچی";
    var longWindow=new MainWindow(new AppState{Language="fa",Theme="dark",Profiles=[longProfile],SelectedId=longProfile.Id,Expanded=new(){["long-fixture"]=true},Measurements=new(){[longProfile.Id]=new(true,12345,DateTimeOffset.UtcNow,"https",1,1)}},true){Width=480,Height=560};longWindow.Show();
    for(int i=0;i<3;i++){Dispatcher.UIThread.RunJobs();longWindow.UpdateLayout();}
    foreach(string value in new[]{longProfile.Name,longProfile.CollectionName,longText})Check(longWindow.GetVisualDescendants().OfType<OverflowMarquee>().Any(t=>t.Text==value&&t.IsOverflowing),"integrated-long-value-overflows-"+value);
    Check(longWindow.GetVisualDescendants().OfType<Border>().Where(b=>b.Name=="PathCard").All(b=>b.Bounds.Height==52),"long-values-keep-card-height");
    Capture(longWindow,"home-fa-dark-480-long-values");foreach(var ticker in longWindow.GetVisualDescendants().OfType<OverflowMarquee>())Advance(ticker,3);Capture(longWindow,"home-fa-dark-480-long-values-moving");
    typeof(MainWindow).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(longWindow,true);longWindow.Close();
    Check(new Palette("amoled",false).ColorOf("Bg")=="#000000","owner-black-theme");
    // Native resources are inspected without claiming macOS runtime validation.
    using var font=Avalonia.Platform.AssetLoader.Open(new Uri("avares://Rovunet/Assets/Vazirmatn.ttf"));Check(font.Length>100000,"bundled-persian-font");
}catch(Exception e){failures++;Console.WriteLine("UI exception: "+e);}
File.WriteAllText(Path.Combine(output,"ui-tests.json"),JsonSerializer.Serialize(new{host=System.Runtime.InteropServices.RuntimeInformation.OSDescription+" / "+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture+" / Avalonia Headless + Skia",macRuntimeValidated=false,checks,failures,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"UI: {checks} checks, {failures} failures");return failures==0?0:1;

sealed class TwoStageFixture:IResolverChecks{
    public int DnsRuns,First,Second,Https;public string LastProfile="",LastWire="",LastAddress="";
    public volatile bool Lower,HoldDns,FailHttp;
    public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task Dns(ScanCheckpoint plan,Func<ValueTask> changed,CancellationToken ct){Interlocked.Increment(ref DnsRuns);plan.Results.Add(new(){Index=0,Transport="tcp",Dns=true,Pass=true,ElapsedMS=38,DnsElapsedMS=28});await changed();if(HoldDns)await Task.Delay(Timeout.Infinite,ct);}
    public async Task<AgentReply> Server(Profile p,string address,string wire,string url,TunnelScanTrust trust,int timeout,bool https,CancellationToken ct){
        LastProfile=p.Id;LastWire=wire;LastAddress=address;if(https)Interlocked.Increment(ref Https);
        if(Lower){Interlocked.Increment(ref Second);await Release.Task.WaitAsync(ct);}else Interlocked.Increment(ref First);
        return new(){State="connected",TunnelEstablished=true,Verified=https&&!FailHttp,HttpsAttempted=https,Error=https&&FailHttp?"HTTPS_TEST_FAILED":"",HandshakeMs=72,HttpsElapsedMs=https?30:0,ElapsedMs=https?102:72};
    }
}

sealed class UiTestVault:ISecretVault {readonly byte[] key=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);public byte[] GetOrCreateKey()=>key.ToArray();}

sealed class EvidenceTestClock:TimeProvider
{
    long tick;
    public override long TimestampFrequency=>1000;
    public override long GetTimestamp()=>tick;
    public void Advance(int seconds)=>tick+=seconds*TimestampFrequency;
}
