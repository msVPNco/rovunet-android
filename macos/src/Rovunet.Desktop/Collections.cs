using Avalonia;
using Avalonia.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    MenuItem Item(string key,Action action){var item=new MenuItem{Header=L.Text(key),MinHeight=44,Tag=key};item.Click+=(_,e)=>{e.Handled=true;try{action();}catch(Exception error){Display(error);}};return item;}
    Button More(Func<ContextMenu> createMenu)
    {
        var button=IconButton("MORE","M12,3 V5 M12,11 V13 M12,19 V21",()=>{});
        void OpenMenu(){
            var menu=createMenu();
            button.ContextMenu=menu;
            menu.Closed+=(_,_)=>{if(ReferenceEquals(button.ContextMenu,menu))button.ContextMenu=null;};
            menu.Open(button);
        }
        button.Click+=(_,_)=>OpenMenu();
        button.ContextRequested+=(_,e)=>{e.Handled=true;OpenMenu();};
        return button;
    }
    void Heading(ContextMenu menu,string key){if(menu.Items.Count>0)menu.Items.Add(new Separator());menu.Items.Add(new MenuItem{Header=L.Text(key),IsEnabled=false});}
    string ProfileConnectionAction(Profile profile)
    {
        if(state.State=="connected"&&activeId==profile.Id)return "DISCONNECT";
        if(state.State is "connecting" or "engine-started"&&connectingId==profile.Id)return "CANCEL";
        return "CONNECT";
    }
    MenuItem ConnectionItem(ContextMenu menu,Func<Profile> target,Func<Profile,Task>? connect=null)
    {
        var item=Item("CONNECT",()=>_=Guard(async()=>{
            var profile=target();
            if(ProfileConnectionAction(profile) is "DISCONNECT" or "CANCEL")await Disconnect();
            else await (connect?.Invoke(profile)??ConnectProfile(profile));
        }));
        void Refresh()
        {
            string key=ProfileConnectionAction(target());
            item.Header=L.Text(key);
            item.Tag=key;
            item.IsEnabled=!busy||key is "DISCONNECT" or "CANCEL";
        }
        menu.Opened+=(_,_)=>Refresh();
        Refresh();
        return item;
    }
    ContextMenu PathMenu(Profile p)
    {
        var menu=new ContextMenu{FlowDirection=L.Direction};Heading(menu,"MENU_CONNECTION");menu.Items.Add(ConnectionItem(menu,()=>p));menu.Items.Add(AutomaticMenu(menu,()=>p));menu.Items.Add(Item("LAST_TEST",()=>ShowMeasurement(p)));menu.Items.Add(Item("QUICK_HTTPS",()=>_=TestCollection([p])));menu.Items.Add(Item("STABILITY_TEST",()=>_=TestCollection([p],3)));menu.Items.Add(Item("STOP_TEST",()=>collectionStop?.Cancel()));if(ProfileEditing.IsDns(p))menu.Items.Add(Item("FIND_DNS",()=>ShowScanner(p)));if(MacTlsPolicy.IsTls(p.Raw)&&!MacTlsPolicy.Supports(p.Raw))menu.Items.Add(Item("MAC_TLS_ENGINE_PIN_UNSUPPORTED",()=>{message=L.Text("MAC_TLS_ENGINE_PIN_HINT");UpdateStatus();}));if(MacTlsPolicy.Supports(p.Raw)){var tls=Item(MacTlsPolicy.Approved(data,p)?"MAC_TLS_REMOVE_EXCEPTION":"MAC_TLS_ADD_EXCEPTION",()=>_=ToggleTlsException(p));tls.IsEnabled=!busy&&collectionStop==null&&state.State!="connected";menu.Items.Add(tls);}Heading(menu,"MENU_MANAGE");menu.Items.Add(Item("EDIT",()=>ShowEditor(p)));menu.Items.Add(Item("PIN_TOP",()=>{Commit(d=>{if(!d.Pinned.Add(p.Id))d.Pinned.Remove(p.Id);});BuildCards();}));if(TransportPlan.UsesSlip(p.Raw)){menu.Items.Add(Item("MAC_SLIP_REVIEW",()=>_=ReviewSlipIdentity(p)));menu.Items.Add(Item("SLIP_INTERVAL",()=>ShowDnsEditor(p)));}menu.Items.Add(Item("MOVE_COLLECTION",()=>ShowMove([p])));Heading(menu,"SECTION_TRANSFER");menu.Items.Add(Item("EXPORT_PROFILE",()=>_=ExportProfiles([p])));menu.Items.Add(Item("SHARE_QR",()=>ShareQr([p])));menu.Items.Add(new Separator());menu.Items.Add(Item("DELETE",()=>_=DeleteProfiles([p])));foreach(var item in menu.Items.OfType<MenuItem>()){if(item.Tag is "QUICK_HTTPS" or "STABILITY_TEST" or "EDIT" or "MOVE_COLLECTION" or "DELETE")item.IsEnabled=collectionStop==null&&!busy;if(item.Tag is "STOP_TEST")item.IsEnabled=collectionStop!=null&&!collectionStop.IsCancellationRequested;}return menu;
    }
    ContextMenu CollectionMenu(List<Profile> group)
    {
        string key=group[0].SourceId.Length>0?"sub:"+group[0].SourceId:group[0].CollectionId;
        bool testing=collectionStop!=null&&collectionTestKey==key;
        var menu=new ContextMenu{FlowDirection=L.Direction};menu.Items.Add(ConnectionItem(menu,()=>group.FirstOrDefault(p=>p.Id==activeId||p.Id==connectingId)??group.FirstOrDefault(p=>p.Id==data.SelectedId)??group[0],ConnectCollection));menu.Items.Add(AutomaticMenu(menu,()=>group[0]));
        menu.Items.Add(Item(data.Expanded.GetValueOrDefault(key)?"HIDE_PATHS":"SHOW_PATHS",()=>{Commit(d=>d.Expanded[key]=!d.Expanded.GetValueOrDefault(key));BuildCards();}));
        var test=Item(testing?collectionStop!.IsCancellationRequested?"CANCELLING":"STOP_TEST":"TEST_COLLECTION",()=>{if(testing)StopCollectionTest();else _=TestCollection(group,phased:true);});test.IsEnabled=collectionStop==null&&!busy||testing&&!collectionStop!.IsCancellationRequested;menu.Items.Add(test);
        var stability=Item("STABILITY_TEST",()=>_=TestCollection(group,3,phased:true));stability.IsEnabled=collectionStop==null&&!busy;menu.Items.Add(stability);
        var sort=new MenuItem{Header=L.Text("SORT_RESULTS")};sort.Items.Add(Item("SORT_LATENCY",()=>Sort(group,false)));sort.Items.Add(Item("SORT_RELIABILITY",()=>Sort(group,true)));sort.Items.Add(Item("CLEAR_TESTS",()=>{Commit(d=>{foreach(var p in group)d.Measurements.Remove(p.Id);});BuildCards();}));menu.Items.Add(sort);var tcp=Item("TCP_CHECK",()=>_=TestCollection(group,tcp:true));tcp.IsEnabled=collectionStop==null&&!busy;menu.Items.Add(tcp);
        menu.Items.Add(new Separator());if(group[0].SourceId.Length>0){var update=Item(SubscriptionUpdateKey(group[0].SourceId),()=>_=UpdateSubscription(group[0].SourceId));update.IsEnabled=CanUpdateSubscription;menu.Items.Add(update);}menu.Items.Add(Item("RENAME",()=>ShowRename(group)));menu.Items.Add(Item("MOVE_COLLECTION",()=>ShowMove(group)));menu.Items.Add(new Separator());menu.Items.Add(Item("EXPORT_COLLECTION",()=>_=ExportProfiles(group)));menu.Items.Add(Item("SHARE_QR",()=>ShareQr(group)));menu.Items.Add(new Separator());menu.Items.Add(Item("DELETE_COLLECTION",()=>_=DeleteProfiles(group)));foreach(var item in menu.Items.OfType<MenuItem>().Where(i=>i.Tag is "RENAME" or "MOVE_COLLECTION" or "DELETE_COLLECTION"))item.IsEnabled=collectionStop==null&&!busy;return menu;
    }
    void StopCollectionTest(){collectionStop?.Cancel();BuildCards();UpdateStatus();}
    async Task TestCollection(List<Profile> profiles,int attempts=1,bool tcp=false,bool phased=false)
    {
        await Guard(async()=>{
            if(collectionStop!=null||busy||profiles.Count==0)return;liveSample?.Cancel();collectionStop=new();var cancellation=collectionStop.Token;
            collectionTestKey=profiles[0].SourceId.Length>0?"sub:"+profiles[0].SourceId:profiles[0].CollectionId;
            freshMeasurements.Clear();testingProfiles.Clear();int totalCompleted=0;
            void Phase(bool dns,int count){collectionTestPhase=dns?"MAC_GROUP_DNS_PHASE":"MAC_GROUP_FAST_PHASE";collectionTestCompleted=0;collectionTestTotal=count;message=L.Text(collectionTestPhase);BuildCards();UpdateStatus();}
            async Task<bool> TestPath(Profile p,CancellationToken token){
                testingProfiles.Add(p.Id);BuildCards();long elapsed=0;int passes=0,completed=0;string error="";
                for(int i=0;i<attempts;i++){
                    token.ThrowIfCancellationRequested();
                    if(collectionProbe!=null){var result=await collectionProbe(p,tcp,token);completed++;passes+=result.Passes;elapsed=result.ElapsedMs;continue;}
                    if(tcp){try{var ms=await MacProfileEditing.Tcp(p,token);if(ms.HasValue){passes++;elapsed=ms.Value;}}catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch{error="TCP_FAILED";}completed++;continue;}
                    using var test=new AgentClient();
                    try{
                        var started=await test.Send(new(){Action="connect",Profile=p,TlsCertificatePin=MacTlsPolicy.ApprovedPin(data,p),HostKey=data.SshKeys.GetValueOrDefault(SshHostKey.Scope(p),""),SlipCertificate=StoredSlipCertificate(p)},token);
                        if(started.State=="connected"){var result=await test.Send(new(){Action="probe",ProbeUrl=data.ProbeUrl,ProbeFallbacks=data.ProbeFallbacks},token);if(result.Verified){passes++;elapsed=result.ElapsedMs;}else error=result.Error;}else error=started.Error;
                    }catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch(SafeError e){error=e.Code;}
                    completed++;
                }
                token.ThrowIfCancellationRequested();Commit(d=>d.Measurements[p.Id]=new(!tcp&&passes>0,elapsed,DateTimeOffset.UtcNow,tcp?"tcp":"https",passes,completed));
                testingProfiles.Remove(p.Id);freshMeasurements.Add(p.Id);collectionTestCompleted++;totalCompleted++;
                message=p.Name+" · "+passes+" / "+completed+(error.Length>0?" · "+L.Text(error):"");messageTone=passes>0?"Green":"Red";BuildCards();UpdateStatus();return passes>0;
            }
            try{
                int passes=await CollectionTestPolicy.Run(profiles,phased,TestPath,async token=>{
                    message=L.Text("MAC_GROUP_DNS_OFFER");messageTone="Muted";BuildCards();UpdateStatus();
                    return await Confirm(message,token);
                },Phase,cancellation);
                if(profiles.Count>1){
                    var ranked=profiles.OrderBy(p=>freshMeasurements.Contains(p.Id)&&data.Measurements.TryGetValue(p.Id,out var m)&&m.Passes>0?m.ElapsedMs:long.MaxValue).ToArray();
                    var ids=ranked.Select(p=>p.Id).ToHashSet();
                    Commit(d=>{int i=0;d.Profiles=d.Profiles.Select(p=>ids.Contains(p.Id)?ranked[i++]:p).ToList();});
                }
                message=string.Format(L.Text("MAC_GROUP_TEST_FINISHED"),passes,totalCompleted);messageTone=passes>0?"Green":"Red";
            }finally{testingProfiles.Clear();collectionStop.Dispose();collectionStop=null;collectionTestKey="";BuildCards();UpdateStatus();}
        });
    }
    void ShowMeasurement(Profile p){var body=Page();if(data.Measurements.TryGetValue(p.Id,out var m)){bool passed=m.Verified||m.Passes>0;string tone=passed?freshMeasurements.Contains(p.Id)?"Green":"StaleMetric":"Red";body.Children.Add(Label(passed?m.Stage+" · "+m.ElapsedMs+" ms":L.Text("FAILED"),18,tone,true));body.Children.Add(Label(m.SampledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),13,"Muted",true));body.Children.Add(Label(m.Passes+" / "+m.Attempts,14,"Muted",true));body.Children.Add(Label(passed?m.Verified?L.Text("HTTPS_VERIFIED"):"TCP":L.Text("FAILED"),14,tone));}else body.Children.Add(Label(L.Text("MAC_UNKNOWN"),16));Navigate("LAST_TEST",body);}
    void ShowPathSelection(List<Profile> group){var body=Page();foreach(var p in group){var b=ActionButton("SELECT",()=>{Commit(d=>{d.SelectedId=p.Id;d.PreferredPaths[AutomaticPathPolicy.Collection(p)]=p.Id;});GoHome();});b.Content=p.Name;body.Children.Add(b);}Navigate("PATH_SELECTION",body);}
    void Sort(List<Profile> group,bool reliability)
    {
        var ids=group.Select(p=>p.Id).ToHashSet();bool Fresh(Measurement m)=>DateTimeOffset.UtcNow-m.SampledAt<TimeSpan.FromMinutes(5);
        var order=reliability?group.OrderByDescending(p=>data.Measurements.TryGetValue(p.Id,out var m)&&Fresh(m)&&m.Attempts>0?(double)m.Passes/m.Attempts:-1).ToArray():group.OrderBy(p=>data.Measurements.TryGetValue(p.Id,out var m)&&Fresh(m)&&m.Verified?m.ElapsedMs:long.MaxValue).ToArray();
        Commit(d=>{int i=0;d.Profiles=d.Profiles.Select(p=>ids.Contains(p.Id)?order[i++]:p).ToList();});BuildCards();
    }
    async Task DeleteProfiles(List<Profile> group)
    {
        await Guard(async()=>{RequireIdle();if(!await Confirm(L.Text("DELETE_CONFIRM")))return;var ids=group.Select(p=>p.Id).ToHashSet();Commit(d=>{d.Profiles.RemoveAll(p=>ids.Contains(p.Id));foreach(string id in ids){d.Measurements.Remove(id);d.OverrideSources.Remove(id);d.OverrideNames.Remove(id);d.Pinned.Remove(id);d.SlipCertificates.Remove(id);}if(ids.Contains(d.SelectedId))d.SelectedId="";d.Subscriptions.RemoveAll(s=>!d.Profiles.Any(p=>p.SourceId==s.Id));});BuildCards();});
    }
    void ShowRename(List<Profile> group)
    {
        var panel=Page();var name=Field(panel,"COLLECTION_NAME",group[0].CollectionName);panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();string text=(name.Text??"").Trim();if(text.Length is <1 or >120||text.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");var ids=group.Select(p=>p.Id).ToHashSet();Commit(d=>{foreach(var p in d.Profiles.Where(p=>ids.Contains(p.Id)))p.CollectionName=text;foreach(var s in d.Subscriptions.Where(s=>s.Id==group[0].SourceId))s.Name=text;});Back();},true));Navigate("RENAME",panel);
    }
    void ShowMove(List<Profile> group)
    {
        if(group.Any(p=>p.SourceId.Length>0))throw new SafeError("LOCAL_OVERRIDE_HINT");var panel=Page();var choices=data.Profiles.Where(p=>p.SourceId.Length==0).GroupBy(p=>p.CollectionId).Select(g=>g.First()).ToList();var combo=Choice(panel,"COLLECTION",choices.Select(p=>p.CollectionName.Length>0?p.CollectionName:p.Name).ToArray(),"");var name=Field(panel,"NEW_COLLECTION");panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();var target=combo.SelectedIndex>=0?choices[combo.SelectedIndex]:null;string title=(name.Text??"").Trim();if(title.Length==0&&target==null)throw new SafeError("CORE_NAME_INVALID");string id=title.Length>0?"manual:"+Guid.NewGuid().ToString("N"):target!.CollectionId;title=title.Length>0?title:target!.CollectionName;var ids=group.Select(p=>p.Id).ToHashSet();Commit(d=>{foreach(var p in d.Profiles.Where(p=>ids.Contains(p.Id))){p.CollectionId=id;p.CollectionName=title;}});Back();},true));Navigate("MOVE_COLLECTION",panel);
    }
    void ShowManagement()
    {
        var panel=Page();var groups=data.Profiles.GroupBy(p=>p.SourceId.Length>0?"sub:"+p.SourceId:p.CollectionId).Select(g=>g.ToList()).ToList();var choose=Choice(panel,"COLLECTION",groups.Select(g=>g[0].CollectionName.Length>0?g[0].CollectionName:g[0].Name).ToArray(),"");List<Profile> Members()=>choose.SelectedIndex>=0?groups[choose.SelectedIndex]:[];
        foreach(var (key,action) in new (string,Action)[]{("TEST_COLLECTION",()=>_=TestCollection(Members(),phased:true)),("STABILITY_TEST",()=>_=TestCollection(Members(),3,phased:true)),("TCP_CHECK",()=>_=TestCollection(Members(),tcp:true)),("SORT_LATENCY",()=>Sort(Members(),false)),("SORT_RELIABILITY",()=>Sort(Members(),true)),("CLEAR_TESTS",()=>{var ids=Members().Select(p=>p.Id);Commit(d=>{foreach(string id in ids)d.Measurements.Remove(id);});})})panel.Children.Add(Card(ActionButton(key,action)));
        panel.Children.Add(Card(ActionButton("REGROUP",()=>_=Regroup())));panel.Children.Add(Card(ActionButton("UNDO_GROUP",UndoGroups)));panel.Children.Add(Card(ActionButton("DUPLICATES",()=>_=RemoveDuplicates())));Navigate("MANAGE_COLLECTIONS",panel);
    }
    async Task Regroup(){await Guard(async()=>{RequireIdle();var groups=CollectionActions.SuggestedGroups(data.Profiles).ToArray();if(groups.Length==0)return;if(!await Confirm(L.Text("REGROUP_HINT")))return;Commit(d=>{d.GroupUndo.Clear();foreach(var group in groups){string id="manual:"+Guid.NewGuid().ToString("N");foreach(var p in d.Profiles.Where(p=>group.Any(x=>x.Id==p.Id))){d.GroupUndo[p.Id]=new(p.CollectionId,p.CollectionName,id);p.CollectionId=id;p.CollectionName=group[0].Name;}}});GoHome();});}
    void UndoGroups(){RequireIdle();Commit(d=>{foreach(var p in d.Profiles)if(d.GroupUndo.TryGetValue(p.Id,out var old)&&p.CollectionId==old.NewGroup){p.CollectionId=old.Id;p.CollectionName=old.Name;}d.GroupUndo.Clear();});GoHome();}
    async Task RemoveDuplicates(){var duplicates=data.Profiles.Where(p=>p.SourceId.Length==0).GroupBy(p=>(p.CollectionId,Key:CollectionActions.DuplicateKey(p))).SelectMany(g=>g.OrderByDescending(p=>p.Id==data.SelectedId||data.OverrideSources.ContainsKey(p.Id)).Skip(1)).Where(p=>p.Id!=data.SelectedId&&!data.OverrideSources.ContainsKey(p.Id)).ToList();if(duplicates.Count>0)await DeleteProfiles(duplicates);}
}
