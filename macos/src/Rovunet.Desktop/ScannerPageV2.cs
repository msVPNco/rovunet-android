using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void ShowResolverBanks(string selected,Action<string> chosen)
    {
        var page=Page();page.Children.Add(Label(L.Text("DNS_BANK_HINT"),13,"Muted"));
        foreach(var bank in ResolverCatalog.Banks){
            var body=new StackPanel{Spacing=5};body.Children.Add(Label(L.Text(bank.LabelKey),17));body.Children.Add(Label(string.Format(L.Text(bank.Networks?"DNS_BANK_RANGES":"DNS_BANK_ADDRESSES"),ResolverCatalog.Count(bank)),12,"Muted"));
            body.Children.Add(Label(string.Join(" · ",ResolverCatalog.Lines(bank.File).Take(3)),11,"Muted",true));
            body.Children.Add(ActionButton("MAC_BANK_BROWSE",()=>BrowseBank(bank,null)));
            body.Children.Add(ActionButton("DNS_BANK_USE",()=>{chosen(bank.File);Back();},bank.File==selected));page.Children.Add(Card(body));
        }
        Navigate("DNS_BANK_TITLE",page);
    }
    void BrowseBank(ResolverBank bank,Action<string>? use){
        var page=Page();var term=Field(page,"SEARCH");term.Name="BankSearch";var counts=Label("",13,"Muted");counts.Name="BankFound";page.Children.Add(counts);
        var actions=new WrapPanel();page.Children.Add(actions);var items=new StackPanel{Spacing=3};page.Children.Add(items);
        int index=0;const int size=32;Action draw=()=>{};
        var prev=ActionButton("MAC_PREV",()=>{index--;draw();});var next=ActionButton("MAC_NEXT",()=>{index++;draw();});actions.Children.Add(prev);actions.Children.Add(next);
        draw=()=>{string needle=term.Text?.Trim()??"";var lines=ResolverCatalog.Lines(bank.File);var match=needle.Length==0?lines:lines.Where(x=>x.Contains(needle,StringComparison.OrdinalIgnoreCase)).ToArray();
            index=Math.Clamp(index,0,Math.Max(0,(match.Length-1)/size));prev.IsEnabled=index>0;next.IsEnabled=(index+1)*size<match.Length;
            counts.Text=string.Format(L.Text("MAC_BANK_MATCHES"),match.Length,lines.Length,match.Length==0?0:index*size+1,Math.Min((index+1)*size,match.Length));items.Children.Clear();
            foreach(var address in match.Skip(index*size).Take(size)){
                if(use==null||bank.Networks)items.Children.Add(Label(address,14,"Ink",true));
                else{var pick=ActionButton("DNS_BANK_USE",()=>{Back();use(address);});pick.Content=address;items.Children.Add(pick);}
            }
        };term.TextChanged+=(_,_)=>{index=0;draw();};draw();Navigate(bank.LabelKey,page);
    }
    void ShowScanner(Profile? original)=>BuildScanner(original,null,null);
    void ShowScannerForEditor(Profile original,Action<Profile> apply)=>BuildScanner(original,apply,null);
    void BuildScanner(Profile? original,Action<Profile>? returnToEditor,Action<string,string>? returnList)
    {
        bool Running()=>scanStop!=null||tunnelScanStop!=null;
        if(Running())throw new SafeError("MAC_SCAN_WAIT_STOP");
        var profiles=(returnToEditor!=null?new[]{original!}:data.Profiles.Where(ProfileEditing.IsDns)).ToArray();
        Profile? profile=original!=null&&ProfileEditing.IsDns(original)?original:profiles.FirstOrDefault();
        var fields=profile==null?null:TransportPlan.Normalize(Importer.Fields(profile.Raw));
        Action redraw=()=>{};Func<bool,bool,bool,Task> begin=(_,_,_)=>Task.CompletedTask;
        ScanCheckpoint? shown=data.FindScan(profile?.Id??"",profile==null?"":Importer.Fingerprint(profile.Raw));
        if(shown!=null){try{shown.Validate();}catch(SafeError){shown=null;}}
        if(shown!=null)shown.NormalizeSelection();
        if(shown!=null&&(shown.ProfileId!=(profile?.Id??"")||shown.ProfileFingerprint!=(profile==null?"":Importer.Fingerprint(profile.Raw))))shown=null;
        bool suppress=false,skipAutomaticTunnel=false,applying=false;
        Task dnsSettled=Task.CompletedTask,tunnelSettled=Task.CompletedTask;int runGeneration=0;
        var layout=new Grid{RowDefinitions=new("Auto,*"),FlowDirection=L.Direction,Margin=new(16,6,16,8)};
        var header=new StackPanel{Spacing=5};layout.Children.Add(header);
        var page=new StackPanel{Spacing=10,Margin=new(0,8,0,16)};
        var scroll=new ScrollViewer{Content=page,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
        var settingsView=new StackPanel{Spacing=8};var resultsView=new StackPanel{Spacing=8};page.Children.Add(settingsView);page.Children.Add(resultsView);
        StackPanel Section(string title){var box=new StackPanel{Spacing=6};box.Children.Add(Label(L.Text(title),15,"Accent"));settingsView.Children.Add(Card(box));return box;}
        var identity=Section("MAC_SCAN_STEP_PROFILE");
        var profileChoice=Choice(identity,"PROFILE_NAME",profiles.Select(p=>p.Name).ToArray(),profile?.Name??"");profileChoice.Name="ScannerProfile";
        profileChoice.SelectedIndex=Array.FindIndex(profiles,p=>p.Id==profile?.Id);profileChoice.IsVisible=returnToEditor==null&&profiles.Length>1;
        var profileSummary=Label(profile==null?L.Text("MAC_SCAN_WITHOUT_PROFILE"):profile.Name,14);identity.Children.Add(profileSummary);
        identity.Children.Add(Label(L.Text(profile==null?"MAC_SCAN_PHASE_HINT":"MAC_SCAN_PROFILE_HINT"),12,"Muted"));
        if(profile!=null&&(data.GlobalResolverEnabled||data.DnsPoolEnabled))identity.Children.Add(Label(L.Text("MAC_DNS_PRIORITY"),12,"Muted"));
        var source=Section("MAC_SCAN_STEP_SOURCE");
        string bank=ResolverCatalog.Banks.Any(b=>b.File==data.ResolverBank)?data.ResolverBank:"resolvers.txt";
        string[] sources=["bank","profile","input","network"],sourceLabels=["DNS_BANK_TITLE","MAC_SCAN_SOURCE_PROFILE","MAC_SCAN_SOURCE_INPUT","DNS_LOCAL_NETWORK"];
        var sourceChoice=Choice(source,"SCAN_SOURCE",sourceLabels.Select(L.Text).ToArray(),L.Text("DNS_BANK_TITLE"));sourceChoice.Name="ScannerSource";
        var sourceActions=new WrapPanel();source.Children.Add(sourceActions);
        var bankSummary=Label("",12,"Muted");source.Children.Add(bankSummary);
        var options=new Grid{ColumnDefinitions=new("*,*"),ColumnSpacing=10};source.Children.Add(options);var left=new StackPanel();var right=new StackPanel();options.Children.Add(left);Grid.SetColumn(right,1);options.Children.Add(right);
        var limit=Choice(left,"SCAN_LIMIT",["64","128","256","512","1024","2048"],data.ScanLimit.ToString());
        string[] modes=ScanCheckpoint.Modes(fields==null?"":ResolverScan.Family(fields),fields?[22]??"",profile!=null);
        string ModeLabel(string value)=>value=="prism"?"Prism":L.Text(value switch{"advanced"=>"SCAN_ADVANCED","e2e"=>"MAC_SCAN_E2E",_=>"SCAN_SIMPLE"});
        var mode=Choice(right,"SCAN_MODE",modes.Select(ModeLabel).ToArray(),ModeLabel(modes[0]));mode.SelectedIndex=Math.Max(0,Array.IndexOf(modes,shown?.Mode??modes[0]));mode.Name="ScannerMode";
        string ModeName()=>modes[Math.Clamp(mode.SelectedIndex,0,modes.Length-1)];
        var modeHint=Label("",12,"Muted");source.Children.Add(modeHint);
        string[] choices=fields==null?["tcp","udp","both","tls","https"]:ResolverScan.TransportChoices(fields);
        string initial=fields?[22]??shown?.Transports.FirstOrDefault()??"tcp";if(!choices.Contains(initial))initial=choices[0];
        var transport=Choice(source,"TRANSPORT",choices,initial);transport.Name="ScannerTransport";LabelDnsTransport(transport);
        var candidates=Field(source,"DNS_RESOLVERS","",true);candidates.Name="ScannerCandidates";candidates.MinHeight=80;candidates.MaxHeight=125;
        var advanced=new StackPanel{Spacing=6};var expander=new Expander{Header=L.Text("SCAN_SETTINGS"),Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch};settingsView.Children.Add(expander);
        var zone=Field(advanced,"TUNNEL_DOMAIN",fields?[3]??shown?.Zone??"example.com");zone.IsReadOnly=profile!=null;
        var key=Field(advanced,"SERVER_PUBLIC_KEY",fields?[11]??shown?.PublicKey??"");key.IsReadOnly=profile!=null;
        var workers=Field(advanced,"SCAN_WORKERS",(shown?.Workers??6).ToString());var timeout=Field(advanced,"SCAN_TIMEOUT",(shown?.TimeoutMS??3000).ToString());
        var count=Field(advanced,"SCAN_ATTEMPTS",(shown?.Count??3).ToString());var threshold=Field(advanced,"SCAN_THRESHOLD",(shown?.Threshold??2).ToString());
        var bytes=Field(advanced,"SCAN_BYTES",(shown?.ResponseSize??0).ToString());var minScore=Field(advanced,"SCAN_SCORE",(shown?.ScoreMinimum??0).ToString());
        var pre=new CheckBox{Content=L.Text("SCAN_PREFILTER"),IsChecked=shown?.Prefilter==true};advanced.Children.Add(pre);
        var tunnelWorkers=Field(advanced,"MAC_SCAN_TUNNEL_WORKERS",(shown?.TunnelWorkers??2).ToString());var tunnelTimeout=Field(advanced,"MAC_SCAN_TUNNEL_TIMEOUT",(shown?.TunnelTimeoutMS??115000).ToString());var url=Field(advanced,"LATENCY_URL",shown?.ProbeUrl??data.ProbeUrl);
        var complete=new CheckBox{Name="ScannerFullHttps",Content=L.Text("MAC_FULL_HTTPS"),IsChecked=shown?.FullVerification==true,MinHeight=36,Margin=new(3,0)};
        bool fullSync=false;
        Button bankButton=null!,sampleButton=null!,localButton=null!,browseButton=null!;
        var planCount=Label("",13,"Accent");planCount.Name="ScannerPlanCount";source.Children.Add(planCount);
        void CountPlan(){
            int actual=0;string issue="";try{string text=candidates.Text??"";if(!string.IsNullOrWhiteSpace(text))actual=ResolverScan.FromText(text,SelectedTransport()=="both"?"udp":SelectedTransport(),2048).Count;}catch(SafeError e){issue=L.Text(e.Code);}
            planCount.Text=string.Format(L.Text("MAC_SCAN_PLAN_COUNT"),actual)+(issue.Length>0?" · "+issue:"");
        }
        string SelectedTransport()=>transport.SelectedItem?.ToString()??"tcp";
        void Describe(){var item=ResolverCatalog.Banks.Single(b=>b.File==bank);bankSummary.Text=L.Text(item.LabelKey)+" · "+string.Format(L.Text(item.Networks?"DNS_BANK_RANGES":"DNS_BANK_ADDRESSES"),ResolverCatalog.Count(item))+"\n"+L.Text("MAC_SCAN_SAMPLE_HINT");}
        void Sample(){
            if(suppress||Running())return;Describe();string method=SelectedTransport(),selected=sources[Math.Max(0,sourceChoice.SelectedIndex)];bool https=method=="https";
            bankButton.IsEnabled=sampleButton.IsEnabled=localButton.IsEnabled=browseButton.IsEnabled=!https;
            candidates.IsReadOnly=selected!="input";
            if(selected!="bank")bankSummary.Text=selected=="profile"?profile?.Name??L.Text("MAC_SCAN_WITHOUT_PROFILE"):L.Text(sourceLabels[Math.Max(0,sourceChoice.SelectedIndex)]);
            if(selected=="input")return;
            if(https){candidates.Text=fields?.ElementAtOrDefault(21)??"";bankSummary.Text=L.Text("DNS_BANK_DOH");CountPlan();return;}
            int max=int.TryParse(limit.SelectedItem?.ToString(),out int n)?n:256;
            candidates.Text=selected switch{"profile"=>profile==null?"":MacDnsEditing.DisplayResolvers(fields![4]),"network"=>string.Join('\n',MacDnsEditing.NetworkResolvers()),_=>string.Join('\n',ResolverCatalog.Candidates(bank,method=="both"?"udp":method,max))};
            if(selected=="network"&&string.IsNullOrWhiteSpace(candidates.Text))throw new SafeError("DNS_LOCAL_UNAVAILABLE");
            CountPlan();
        }
        bankButton=ActionButton("DNS_BANK_TITLE",()=>ShowResolverBanks(bank,value=>{bank=value;sourceChoice.SelectedIndex=0;Sample();}));bankButton.Name="ScannerBank";sourceActions.Children.Add(bankButton);
        localButton=ActionButton("DNS_LOCAL_NETWORK",()=>{if(sourceChoice.SelectedIndex==3)Sample();else sourceChoice.SelectedIndex=3;});localButton.Name="ScannerLocal";sourceActions.Children.Add(localButton);
        sampleButton=ActionButton("MAC_SCAN_NEW_SAMPLE",Sample);sourceActions.Children.Add(sampleButton);
        browseButton=ActionButton("MAC_BANK_BROWSE",()=>BrowseBank(ResolverCatalog.Banks.Single(b=>b.File==bank),address=>{string prior=sourceChoice.SelectedIndex==2?candidates.Text??"":"";sourceChoice.SelectedIndex=2;candidates.Text=prior.Trim().Length==0?address:prior.Trim()+'\n'+address;CountPlan();}));sourceActions.Children.Add(browseButton);
        var importButton=ActionButton("IMPORT_FILE",()=>_=Guard(async()=>{
            int generation=runGeneration,navigation=navigationRevision;string? inputBefore=candidates.Text,profileBefore=profile?.Id;int sourceBefore=sourceChoice.SelectedIndex;string wireBefore=SelectedTransport();
            bool Valid()=>generation==runGeneration&&navigation==navigationRevision&&!Running()&&ReferenceEquals(currentBody,layout)&&!exiting&&inputBefore==candidates.Text&&profileBefore==profile?.Id&&sourceBefore==sourceChoice.SelectedIndex&&wireBefore==SelectedTransport();
            if(!Valid())return;var files=await StorageProvider.OpenFilePickerAsync(new(){Title=L.Text("DNS_RESOLVERS"),AllowMultiple=false});
            if(files.Count==0||!Valid())return;await using var stream=await files[0].OpenReadAsync();var text=await ReadText(stream);
            if(!Valid())return;sourceChoice.SelectedIndex=2;candidates.Text=text;CountPlan();
        }));importButton.Name="ScannerImport";sourceActions.Children.Add(importButton);
        if(profile!=null&&TransportPlan.UsesSlip(profile.Raw))sourceActions.Children.Add(ActionButton("MAC_SLIP_REVIEW",()=>_=Guard(async()=>{await ReviewSlipIdentity(profile);redraw();})));
        candidates.TextChanged+=(_,_)=>CountPlan();
        sourceChoice.SelectionChanged+=(_,_)=>{try{Sample();}catch(Exception e){Display(e);}};limit.SelectionChanged+=(_,_)=>{try{Sample();}catch(Exception e){Display(e);}};transport.SelectionChanged+=(_,_)=>{try{Sample();}catch(Exception e){Display(e);}};
        profileChoice.SelectionChanged+=(_,_)=>{if(suppress||Running()||profileChoice.SelectedIndex<0)return;profile=profiles[profileChoice.SelectedIndex];fields=TransportPlan.Normalize(Importer.Fields(profile.Raw));profileSummary.Text=profile.Name;shown=Matching(profile);modes=ScanCheckpoint.Modes(ResolverScan.Family(fields),fields[22],true);mode.ItemsSource=modes.Select(ModeLabel).ToArray();mode.SelectedIndex=0;zone.Text=fields[3];key.Text=fields[11];var valid=ResolverScan.TransportChoices(fields);transport.ItemsSource=valid;transport.SelectedItem=valid.Contains(fields[22])?fields[22]:valid[0];if(shown!=null)RestoreForm(shown);else{fullSync=true;try{complete.IsChecked=false;}finally{fullSync=false;}sourceChoice.SelectedIndex=1;Sample();}redraw();};
        ScanCheckpoint? Matching(Profile target){var check=data.FindScan(target.Id,Importer.Fingerprint(target.Raw));if(check==null)return null;try{check.Validate();check.NormalizeSelection();return check;}catch(SafeError){return null;}}
        void RestoreForm(ScanCheckpoint cp){
            suppress=true;fullSync=true;try{complete.IsChecked=cp.FullVerification;
                bank=ResolverCatalog.Banks.Any(b=>b.File==cp.Bank)?cp.Bank:"resolvers.txt";sourceChoice.SelectedIndex=Math.Max(0,Array.IndexOf(sources,cp.Source));
                mode.SelectedIndex=Math.Max(0,Array.IndexOf(modes,cp.Mode));string wire=cp.Transports.Length==2?"both":cp.Transports[0];transport.SelectedItem=wire;
                bytes.Text=cp.ResponseSize.ToString();minScore.Text=cp.ScoreMinimum.ToString();pre.IsChecked=cp.Prefilter;limit.SelectedItem=cp.SampleLimit.ToString();zone.Text=cp.Zone;key.Text=cp.PublicKey;workers.Text=cp.Workers.ToString();timeout.Text=cp.TimeoutMS.ToString();count.Text=cp.Count.ToString();threshold.Text=cp.Threshold.ToString();tunnelWorkers.Text=cp.TunnelWorkers.ToString();tunnelTimeout.Text=cp.TunnelTimeoutMS.ToString();url.Text=cp.ProbeUrl;
                candidates.Text=string.Join('\n',cp.Candidates);candidates.IsReadOnly=sourceChoice.SelectedIndex!=2;Describe();bankSummary.Text=L.Text("MAC_SCAN_RESTORED")+" · "+L.Text(sourceLabels[sourceChoice.SelectedIndex]);CountPlan();
            }finally{fullSync=false;suppress=false;}
        }
        if(shown!=null)RestoreForm(shown);else{sourceChoice.SelectedIndex=profile==null?0:1;Sample();}
        var progress=Label("",13,"Muted");progress.Name="ScannerProgress";header.Children.Add(progress);
        var applySummary=Label("",12,"Muted");applySummary.Name="ScannerSelectionCount";header.Children.Add(applySummary);
        var dnsBar=new ProgressBar{Minimum=0,Maximum=1,Height=4,Name="ScannerDnsProgressBar"};header.Children.Add(dnsBar);
        var apply=ActionButton("MAC_SCAN_APPLY",()=>_=Guard(async()=>{
            if(applying)return;RequireIdle();MatchProfile();var snapshot=shown;var chosen=Chosen();
            if(snapshot==null||chosen.Length==0)throw new SafeError("MAC_SCAN_SELECT_FIRST");
            applying=true;try{
            if(Running()){
                skipAutomaticTunnel=true;scanStop?.Cancel();tunnelScanStop?.Cancel();redraw();await Task.WhenAll(dnsSettled,tunnelSettled);
                if(exiting||!ReferenceEquals(currentBody,layout)||!ReferenceEquals(shown,snapshot))return;
                RequireIdle();MatchProfile();if(chosen.Any(r=>!snapshot.CanApply(r)||!snapshot.Selected.Contains(Id(r))))throw new SafeError("MAC_SCAN_SELECT_FIRST");
            }
            if(returnList!=null){returnList(string.Join('\n',chosen.Select(r=>snapshot.Candidates[r.Index]).Distinct()),chosen[0].Transport);Back();return;}if(profile==null)throw new SafeError("DNS_PROFILE_REQUIRED");
            var next=ResolverScan.Selected(profile,chosen.Select(r=>(snapshot.Candidates[r.Index],r.Transport)));
            if(ResolverScan.Family(TransportPlan.Normalize(Importer.Fields(profile.Raw)))=="soar"){
                string raw=ProfileEditing.ChangeNative(next,new Dictionary<int,string>{{22,chosen[0].Transport},{81,chosen[0].Transport=="tcp"?"only":"off"}});
                next=MacProfileEditing.Replace(profile,raw);
            }else next=MacProfileEditing.Replace(profile,next.Raw);
            if(returnToEditor!=null){returnToEditor(next);return;}
            var stored=data.Profiles.SingleOrDefault(p=>p.Id==profile.Id);if(stored==null||stored.Raw!=profile.Raw)throw new SafeError("MAC_SCAN_PROFILE_CHANGED");
            SaveEdited(profile,next);GoHome();message=L.Text("MAC_SCAN_APPLIED");UpdateStatus();
        }finally{applying=false;if(ReferenceEquals(currentBody,layout))redraw();}}),true);apply.Name="ScannerApply";apply.HorizontalAlignment=HorizontalAlignment.Stretch;PinCommit(layout,apply);
        apply.Resources["ButtonBackgroundDisabled"]=Tone("Card");apply.Resources["ButtonForegroundDisabled"]=Tone("Muted");apply.Resources["ButtonBorderBrushDisabled"]=Tone("Outline");
        var tabs=new WrapPanel();header.Children.Insert(0,tabs);
        Button settingsTab=null!,resultsTab=null!;
        void Switch(bool show){settingsView.IsVisible=!show;resultsView.IsVisible=show;scroll.Offset=default;
            ButtonSurface(settingsTab,Tone(show?"Card":"Accent"),Tone(show?"Ink":"OnAccent"));ButtonSurface(resultsTab,Tone(show?"Accent":"Card"),Tone(show?"OnAccent":"Ink"));redraw();}
        settingsTab=ActionButton("SCAN_SETTINGS",()=>Switch(false));resultsTab=ActionButton("MAC_SCAN_VIEW_RESULTS",()=>{Switch(true);redraw();});tabs.Children.Add(settingsTab);tabs.Children.Add(resultsTab);
        var actions=new WrapPanel();header.Children.Add(actions);
        var start=ActionButton("SCAN_START",()=>_=Guard(()=>begin(false,false,false)),true);start.Name="ScannerStart";actions.Children.Add(start);
        var stop=ActionButton("MAC_STOP_DNS",()=>{scanStop?.Cancel();if(shown?.Mode=="simple"){skipAutomaticTunnel=true;tunnelScanStop?.Cancel();}});stop.Name="ScannerStopDns";actions.Children.Add(stop);
        var resume=ActionButton("MAC_SCAN_RESUME",()=>_=Guard(()=>begin(true,false,false)));actions.Children.Add(resume);
        // This is a phase control. It is independent of the checkboxes that pick
        // addresses for Apply. Restoring old results must never start network work.
        bool refreshingToggle=false;
        var phaseToggle=new CheckBox{Name="ScannerTunnelToggle",Content=L.Text("MAC_SCAN_TUNNEL_SWITCH"),MinHeight=36,Margin=new(3,0)};
        actions.Children.Add(phaseToggle);
        actions.Children.Add(complete);
        ToolTip.SetTip(complete,L.Text("SCAN_FULL_VERIFICATION_HINT"));
        complete.IsCheckedChanged+=(_,_)=>{
            if(fullSync||tunnelScanStop!=null||shown==null)return;
            try{bool value=complete.IsChecked==true;if(value)shown.ProbeUrl=SubscriptionUrls.Validate(url.Text??"").AbsoluteUri;shown.FullVerification=value;shown.NormalizeSelection();Commit(d=>d.SaveScan(shown));}
            catch(Exception e){fullSync=true;try{complete.IsChecked=shown.FullVerification;}finally{fullSync=false;}Display(e);}redraw();};

        var phaseNote=Label("",12,"Muted");phaseNote.Name="ScannerPhaseNote";header.Children.Add(phaseNote);
        phaseToggle.IsCheckedChanged+=(_,_)=>{
            if(refreshingToggle)return;
            if(phaseToggle.IsChecked==true){if(phaseToggle.IsEnabled)_=Guard(()=>begin(true,true,shown?.ServerFirst==true&&!shown.HasTunnelWork(false)));}
            else{skipAutomaticTunnel=true;tunnelScanStop?.Cancel();redraw();}
        };
        var phaseStats=Label("",12,"Muted");phaseStats.Name="ScannerTunnelProgress";header.Children.Add(phaseStats);
        var tunnelBar=new ProgressBar{Minimum=0,Maximum=1,Height=4,Name="ScannerTunnelProgressBar"};header.Children.Add(tunnelBar);
        var liveTargets=Label("",11,"Muted",true);liveTargets.Name="ScannerTestingNow";liveTargets.MaxLines=2;liveTargets.TextTrimming=TextTrimming.CharacterEllipsis;header.Children.Add(liveTargets);
        var verificationActions=new WrapPanel();resultsView.Children.Add(verificationActions);
        var tunnel=ActionButton("MAC_SCAN_TEST_TUNNEL",()=>_=Guard(()=>begin(true,true,false)));verificationActions.Children.Add(tunnel);
        var retest=ActionButton("MAC_SCAN_RETEST_TUNNEL",()=>_=Guard(()=>begin(true,true,true)));verificationActions.Children.Add(retest);
        var stopTunnel=ActionButton("MAC_STOP_TUNNEL",()=>{skipAutomaticTunnel=true;tunnelScanStop?.Cancel();});stopTunnel.Name="ScannerStopTunnel";verificationActions.Children.Add(stopTunnel);
        var tunnelHint=Label("",12,"Muted");tunnelHint.Name="ScannerTunnelHint";resultsView.Children.Add(tunnelHint);
        resultsView.Children.Add(Label(L.Text("MAC_SCAN_RESULT_HINT"),12,"Muted"));
        var selectActions=new WrapPanel();resultsView.Children.Add(selectActions);
        var filterBox=new StackPanel{Spacing=5};resultsView.Children.Add(new Expander{Header=L.Text("MAC_SCAN_FILTER_TOOLS"),Content=filterBox,HorizontalAlignment=HorizontalAlignment.Stretch});
        var search=Field(filterBox,"SEARCH");string[] filters=["all","passed","tunnel","https","failed"],filterKeys=["MAC_SCAN_FILTER_ALL","MAC_SCAN_FILTER_PASSED","MAC_SCAN_FILTER_TUNNEL","MAC_SCAN_FILTER_HTTPS","MAC_SCAN_FILTER_FAILED"];
        var filter=Choice(filterBox,"SCAN_RESULTS",filterKeys.Select(L.Text).ToArray(),L.Text("MAC_SCAN_FILTER_ALL"));filter.Name="ScannerFilter";
        var sort=Choice(filterBox,"SCAN_SORT",[L.Text("MAC_SCAN_SORT_ORDER"),L.Text("MAC_SCAN_SORT_LATENCY"),L.Text("MAC_SCAN_SORT_ADDRESS")],L.Text("MAC_SCAN_SORT_ORDER"));
        var pulse=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void NotifyDns(){var old=pulse;pulse=new(TaskCreationOptions.RunContinuationsAsynchronously);old.TrySetResult();}
        int batchTotal=0,batchDone=0,batchPassed=0,batchHttps=0;
        var fresh=new HashSet<string>();var active=new Dictionary<string,string>();
        var results=new ItemsControl{HorizontalAlignment=HorizontalAlignment.Stretch,Name="ScannerResults"};resultsView.Children.Insert(0,results);
        int resultPage=0;const int pageSize=24;var pager=new WrapPanel();resultsView.Children.Insert(1,pager);
        var previous=ActionButton("MAC_PREV",()=>{resultPage--;redraw();scroll.Offset=default;});var nextPage=ActionButton("MAC_NEXT",()=>{resultPage++;redraw();scroll.Offset=default;});var pageInfo=Label("",12,"Muted");pager.Children.Add(previous);pager.Children.Add(pageInfo);pager.Children.Add(nextPage);
        static string Id(ScanResult row)=>row.Index+":"+row.Transport;
        results.ItemTemplate=new FuncDataTemplate<ScanResult>((row,_)=>{
            if(row==null||shown==null)return new TextBlock();string id=Id(row);var item=new StackPanel{Spacing=3};
            var check=new CheckBox{Content=shown.Candidates[row.Index]+" · "+DnsTransportLabel(row.Transport),IsChecked=shown.Selected.Contains(id),IsEnabled=shown.CanApply(row)&&!applying,FlowDirection=FlowDirection.LeftToRight,MinHeight=40,Name="ScannerResultChoice",Tag=id};
            var owner=shown;check.IsCheckedChanged+=(_,_)=>{try{if(!ReferenceEquals(owner,shown)||!ReferenceEquals(currentBody,layout))return;
                if(check.IsChecked==true){string why=owner.SelectionError(row);if(why.Length>0){check.IsChecked=false;throw new SafeError(why);}owner.Selected.Add(id);}else owner.Selected.Remove(id);
                Commit(d=>d.SaveScan(owner));redraw();}catch(Exception e){Display(e);}};item.Children.Add(check);
            string tone=row.LatestServerReached&&fresh.Contains(id)?"Green":shown.CanApply(row)?"Ink":"Red";
            item.Children.Add(Label(L.Text("MAC_SCAN_DNS")+": "+(row.Dns?"✓":"—")+" · "+L.Text("MAC_SCAN_TUNNEL")+": "+(row.TunnelEstablished||row.ServerReached?"✓":"—")+" · HTTPS: "+(row.HttpsVerified?"✓":row.HttpsAttempted?"✗":L.Text("MAC_NOT_TESTED")),12,tone,true));
            string Time(long ms)=>ms>0?ms+" ms":"—";
            string detail=active.ContainsKey(id)?L.Text("MAC_SCAN_TESTING"):row.TunnelFinished?(Time(row.TunnelElapsedMS)+" · "+L.Text(row.TunnelError.Length>0?row.TunnelError:row.HttpsVerified?"HTTPS_VERIFIED":row.TunnelEstablished?"MAC_SERVER_REACHED":"DNS_TUNNEL_FAILED")):Time(row.ElapsedMS)+" · "+L.Text(row.Pass?"PASS":row.Error.Length>0?row.Error:"DNS_FAILED");
            if(row.ServerChecked&&!row.TunnelFinished)detail=L.Text("MAC_SCAN_FIRST_SERVER")+": "+L.Text(row.ServerReached?"PASS":row.ServerError.Length>0?row.ServerError:"DNS_FAILED")+(row.ServerElapsedMS>0?" · "+row.ServerElapsedMS+" ms":"");
            if(row.ServerChecked&&row.TunnelFinished)detail=L.Text("MAC_SCAN_FIRST_SERVER")+" "+(row.ServerReached?"✓":"—")+" · "+detail;
            if(row.TunnelFinished&&row.TunnelEstablished){
                detail=L.Text("MAC_SERVER_REACHED")+" "+Time(row.TunnelElapsedMS)+(row.HttpsAttempted?" · HTTPS "+(row.HttpsVerified?"✓":"✗")+(row.HttpsElapsedMS>0?" "+row.HttpsElapsedMS+" ms":""):"");
                if(row.TunnelError.Length>0)detail+=" · "+L.Text(row.TunnelError);
            }
            if(!row.ServerChecked&&row.Dns&&row.TunnelFinished){long dnsMs=row.DnsElapsedMS>0?row.DnsElapsedMS:row.ElapsedMS;if(dnsMs>0)detail=DnsTransportLabel(row.Transport)+" "+dnsMs+" ms · "+detail;}
            if(!row.TunnelFinished&&!row.ServerChecked&&row.Pass&&!active.ContainsKey(id))detail=DnsTransportLabel(row.Transport)+" "+Time(row.DnsElapsedMS>0?row.DnsElapsedMS:row.ElapsedMS)+" · "+L.Text("MAC_DNS_ONLY_ROW");
            if(!fresh.Contains(id)&&!active.ContainsKey(id))detail=L.Text("PREVIOUS")+" · "+detail;
            item.Children.Add(Label(detail,12,active.ContainsKey(id)||fresh.Contains(id)&&shown.CanApply(row)?"Green":"StaleMetric"));
            if(shown.Mode=="prism"&&row.Attempted>0)item.Children.Add(Label("Prism "+row.Passed+"/"+row.Attempted+" · "+L.Text("SCAN_THRESHOLD")+": "+shown.Threshold,11,"Muted",true));
            if(row.CompletedChecks?.Length>0)item.Children.Add(Label(string.Join(" · ",row.CompletedChecks.Select(c=>c+(row.Checks?.Contains(c)==true?" ✓":" —")))+" · "+row.Passed+"/"+row.Attempted+" · EDNS "+row.Edns,11,"Muted",true));
            return new Border{Child=item,Background=Tone("Card"),BorderBrush=Tone(shown.Selected.Contains(id)?"Green":"Outline"),BorderThickness=new(1),CornerRadius=new(12),Padding=new(10),Margin=new(0,3),Name="ScannerResultCard"};
        });
        ScanResult[] Visible(){var current=shown;if(current==null)return [];string selected=filters[Math.Max(0,filter.SelectedIndex)],text=search.Text?.Trim()??"";var rows=current.Results.Where(r=>(text.Length==0||current.Candidates[r.Index].Contains(text,StringComparison.OrdinalIgnoreCase))&&(selected switch{"passed"=>current.CanApply(r),"tunnel"=>r.TunnelEstablished,"https"=>r.HttpsVerified,"failed"=>!current.CanApply(r),_=>true}));return (sort.SelectedIndex switch{1=>rows.OrderByDescending(r=>current.CanApply(r)).ThenBy(r=>r.TunnelFinished?r.TunnelElapsedMS:r.ElapsedMS),2=>rows.OrderBy(r=>current.Candidates[r.Index],StringComparer.Ordinal),_=>rows.OrderByDescending(r=>r.HttpsVerified).ThenByDescending(r=>current.CanApply(r)).ThenBy(r=>r.Index).ThenBy(r=>r.Transport)}).ToArray();}
        selectActions.Children.Add(ActionButton("MAC_SCAN_SELECT_VISIBLE",()=>{if(shown==null)return;var usable=Visible().Skip(resultPage*pageSize).Take(pageSize).Where(shown.CanApply).ToArray();string? wire=Chosen().FirstOrDefault()?.Transport??usable.FirstOrDefault()?.Transport;foreach(var row in usable.Where(r=>r.Transport==wire&&!shown.Selected.Contains(Id(r)))){if(shown.SelectionError(row).Length!=0)break;shown.Selected.Add(Id(row));}Commit(d=>d.SaveScan(shown));redraw();}));
        selectActions.Children.Add(ActionButton("MAC_SCAN_CLEAR_SELECTION",()=>{shown?.Selected.Clear();Commit(d=>d.SaveScan(shown));redraw();}));
        ScanResult[] Chosen()=>shown?.Results.Where(r=>shown.CanApply(r)&&shown.Selected.Contains(Id(r))).ToArray()??[];
        var exportBody=new StackPanel{Spacing=4};resultsView.Children.Add(new Expander{Header=L.Text("MAC_SCAN_EXPORT_TOOLS"),Content=exportBody,HorizontalAlignment=HorizontalAlignment.Stretch});var tools=new WrapPanel();exportBody.Children.Add(tools);
        string Output(){var rows=Chosen();if(shown==null||rows.Length==0)throw new SafeError("MAC_SCAN_SELECT_FIRST");return string.Join('\n',rows.Select(r=>shown.Candidates[r.Index]).Distinct());}
        var copy=ActionButton("COPY",()=>_=Guard(async()=>{if(Clipboard!=null)await Clipboard.SetTextAsync(Output());}));tools.Children.Add(copy);
        var save=ActionButton("EXPORT_FILE",()=>_=Guard(()=>SaveText(Output(),"Rovunet-resolvers.txt")));save.Name="ScannerExport";tools.Children.Add(save);
        var report=ActionButton("MAC_SCAN_EXPORT_REPORT",()=>_=Guard(()=>SaveText(JsonSerializer.Serialize(new{mode=shown?.Mode,results=Visible().Select(r=>new{address=shown!.Candidates[r.Index],r.Transport,r.Dns,r.Handshake,r.Pass,r.ServerChecked,r.ServerReached,r.ServerElapsedMS,r.ServerError,r.HttpsAttempted,r.HttpsElapsedMS,r.Checks,r.CompletedChecks,r.Passed,r.Attempted,r.Completed,r.Edns,r.TunnelEstablished,r.SshEstablished,r.HttpsVerified,r.Error,r.TunnelError,r.DnsElapsedMS,r.ElapsedMS,r.TunnelElapsedMS})},new JsonSerializerOptions{WriteIndented=true}),"Rovunet-resolver-results.json")));tools.Children.Add(report);
        var pool=ActionButton("DNS_POOL_SAVE",()=>{var selectedRows=Chosen();if(selectedRows.Any(r=>r.Transport is not("udp" or "tcp")))throw new SafeError("MAC_POOL_WIRE");
            string value=string.Join('\n',DnsPool.Parse(data.DnsPoolList+"\n"+Output()));Commit(d=>d.DnsPoolList=value);message=L.Text("SETTINGS_SAVED");UpdateStatus();});tools.Children.Add(pool);
        var inputs=new Control[]{sourceChoice,mode,transport,zone,key,candidates,workers,timeout,count,threshold,tunnelWorkers,tunnelTimeout,url,profileChoice,bankButton,sampleButton,localButton,limit,importButton,browseButton,pre,bytes,minScore};
        void Render(){
            int selected=Chosen().Length;bool running=Running();foreach(var input in inputs)input.IsEnabled=!running;
            if(!running){bool ips=SelectedTransport()!="https";bankButton.IsEnabled=browseButton.IsEnabled=sampleButton.IsEnabled=localButton.IsEnabled=ips;}
            apply.IsEnabled=(profile!=null||returnList!=null)&&selected>0&&!applying;
            apply.Content=returnList!=null?L.Text("MAC_APPLY_LIST"):profile==null?L.Text("MAC_SCAN_APPLY"):string.Format(L.Text("MAC_SCAN_APPLY_TO"),profile.Name);
            applySummary.Text=(string)apply.Content!+" · "+string.Format(L.Text("MAC_SCAN_SELECTED_COUNT"),selected);
            string? unavailable=profile==null?"DNS_PROFILE_REQUIRED":fields!=null&&ResolverScan.Family(fields)=="dnstt"&&fields[22]=="https"?"MAC_NO_DNSTT_DOH_TUNNEL":TransportPlan.UsesSlip(profile.Raw)&&StoredSlipCertificate(profile).Length==0?"SLIP_CERT_REQUIRED":SshHostKey.Required(profile)&&!data.SshKeys.ContainsKey(SshHostKey.Scope(profile))?"MAC_SCAN_TRUST_FIRST":null;
            string currentMode=ModeName();bool prism=currentMode=="prism",soar=fields!=null&&ResolverScan.Family(fields)=="soar";
            count.IsEnabled=threshold.IsEnabled=pre.IsEnabled=bytes.IsEnabled=!running&&prism;
            minScore.IsEnabled=!running&&currentMode is "advanced" or "simple";
            modeHint.Text=L.Text(currentMode switch{"advanced"=>"SCAN_HINT_ADVANCED","prism"=>"SCAN_HINT_PRISM","e2e"=>"MAC_E2E_STAGES",_=>soar?"SCAN_HINT_SOAR_SIMPLE":"MAC_SIMPLE_PHASES"});
            start.IsEnabled=!running;stop.IsEnabled=scanStop!=null;stopTunnel.IsEnabled=tunnelScanStop!=null;
            resume.IsEnabled=scanStop==null&&(tunnelScanStop==null||shown?.Mode is "advanced" or "prism")&&shown!=null&&(shown.HasDnsRemaining()||profile!=null&&(shown.Direct||shown.Mode=="simple")&&shown.HasTunnelWork(false));
            stop.IsVisible=stop.IsEnabled;stopTunnel.IsVisible=stopTunnel.IsEnabled;resume.IsVisible=resume.IsEnabled;start.IsVisible=!running;
            bool eligible=shown?.HasTunnelWork(false)==true,repeat=shown?.HasTunnelWork(true)==true;
            tunnel.IsEnabled=tunnelScanStop==null&&unavailable==null&&eligible;retest.IsEnabled=!running&&unavailable==null&&repeat;
            bool manual=(resultsView.IsVisible&&shown!=null?shown.Mode:modes[Math.Max(0,mode.SelectedIndex)])!="simple";
            string effective=resultsView.IsVisible&&shown!=null?shown.Mode:currentMode;
            bool useFull=soar?effective=="e2e":shown?.FullVerification??complete.IsChecked==true;
            if(soar||resultsView.IsVisible&&shown!=null){fullSync=true;try{complete.IsChecked=useFull;}finally{fullSync=false;}}
            complete.IsEnabled=profile!=null&&!soar&&tunnelScanStop==null&&!(scanStop!=null&&shown?.ServerFirst==true);phaseToggle.IsVisible=manual;phaseNote.IsVisible=manual;
            bool matchingPlan=resultsView.IsVisible||shown?.Mode==modes[Math.Max(0,mode.SelectedIndex)];
            phaseToggle.IsEnabled=tunnelScanStop!=null?!tunnelScanStop.IsCancellationRequested:matchingPlan&&(scanStop==null||shown?.Mode is "advanced" or "prism")&&unavailable==null&&(eligible||shown?.ServerFirst==true&&repeat);
            refreshingToggle=true;try{phaseToggle.IsChecked=tunnelScanStop is {IsCancellationRequested:false};}finally{refreshingToggle=false;}
            string instruction=tunnelScanStop is {IsCancellationRequested:true}?"MAC_SCAN_WAIT_STOP":tunnelScanStop!=null?"MAC_STAGE_TWO_RUNNING":scanStop!=null&&!phaseToggle.IsEnabled?"MAC_STAGE_ONE_RUNNING":!matchingPlan||shown==null?"MAC_STAGE_ONE_FIRST":unavailable??(eligible?"MAC_STAGE_TWO_READY":repeat?"MAC_STAGE_TWO_DONE":"MAC_STAGE_NO_PASS");
            phaseNote.Text=L.Text(shown?.ServerFirst==true&&!running&&repeat?"MAC_E2E_REPEAT":instruction);
            retest.IsVisible=retest.IsEnabled&&shown!.Results.Any(r=>r.TunnelFinished);
            tunnel.IsVisible=!manual&&tunnel.IsEnabled;
            tunnelHint.Text=L.Text(unavailable??(!repeat?"MAC_SCAN_NEEDS_DNS":"MAC_SCAN_RESULT_HINT"));
            copy.IsEnabled=save.IsEnabled=pool.IsEnabled=selected>0;report.IsEnabled=shown?.Results.Count>0;
            bool has=shown!=null;dnsBar.IsVisible=has;tunnelBar.IsVisible=phaseStats.IsVisible=has&&profile!=null;liveTargets.IsVisible=active.Count>0;
            if(!has){pager.IsVisible=false;progress.Text=L.Text("MAC_SCAN_READY");results.ItemsSource=Array.Empty<ScanResult>();return;}
            var cp=shown!;int total=cp.Candidates.Length,scanned=cp.Direct?cp.Results.Where(r=>r.TunnelFinished).Select(r=>r.Index).Distinct().Count():cp.ScannedAddresses();
            progress.Text=string.Format(L.Text("MAC_DNS_COUNTERS"),total,scanned,cp.WorkingAddresses());dnsBar.Value=total>0?(double)scanned/total:0;
            int eligibleCount=cp.Direct?total*cp.Transports.Length:cp.EligibleRows().Count();
            int finished=cp.Results.Count(r=>r.TunnelFinished);
            int serverPassed=cp.Results.Count(r=>r.TunnelEstablished),httpsPassed=cp.Results.Count(r=>r.HttpsVerified);
            if(tunnelScanStop!=null){eligibleCount=batchTotal;finished=batchDone;serverPassed=batchPassed;httpsPassed=batchHttps;}
            phaseStats.Text=string.Format(L.Text("MAC_SERVER_COUNTERS"),finished,eligibleCount,serverPassed,httpsPassed);tunnelBar.Value=eligibleCount>0?Math.Min(1,(double)finished/eligibleCount):0;
            liveTargets.Text=string.Join(" · ",active.Values);ToolTip.SetTip(liveTargets,liveTargets.Text);
            var filtered=Visible();resultPage=Math.Clamp(resultPage,0,Math.Max(0,(filtered.Length-1)/pageSize));pager.IsVisible=filtered.Length>pageSize;
            previous.IsEnabled=resultPage>0;nextPage.IsEnabled=(resultPage+1)*pageSize<filtered.Length;pageInfo.Text=(resultPage+1)+" / "+Math.Max(1,(filtered.Length+pageSize-1)/pageSize);
            results.ItemsSource=filtered.Skip(resultPage*pageSize).Take(pageSize).ToArray();
        }
        mode.SelectionChanged+=(_,_)=>redraw();search.TextChanged+=(_,_)=>{resultPage=0;redraw();};filter.SelectionChanged+=(_,_)=>{resultPage=0;redraw();};sort.SelectionChanged+=(_,_)=>{resultPage=0;redraw();};
        void MatchProfile(){if(shown?.ProfileId.Length>0&&(profile==null||shown.ProfileId!=profile.Id||shown.ProfileFingerprint!=Importer.Fingerprint(profile.Raw)))throw new SafeError("MAC_SCAN_PROFILE_CHANGED");}
        async Task TestTunnels(bool again,CancellationToken ct,bool firstServer=false){
            var current=shown??throw new SafeError("DNS_PROFILE_REQUIRED");var targetProfile=profile??throw new SafeError("DNS_PROFILE_REQUIRED");MatchProfile();
            if(TransportPlan.UsesSlip(targetProfile.Raw)&&StoredSlipCertificate(targetProfile).Length==0)throw new SafeError("SLIP_CERT_REQUIRED");
            if(SshHostKey.Required(targetProfile)&&!data.SshKeys.ContainsKey(SshHostKey.Scope(targetProfile)))throw new SafeError("MAC_SCAN_TRUST_FIRST");
            bool direct=current.Direct,manualBatch=!firstServer&&current.Mode!="simple";
            var selection=current.Selected.ToHashSet();var attempted=new HashSet<(int Index,string Transport)>();
            (int Index,string Transport)[] Pending(){
                var done=again?null:current.Results.Where(r=>firstServer?r.ServerChecked:current.TunnelDone(r)).Select(r=>(r.Index,r.Transport)).ToHashSet();
                var options=(direct||firstServer?Enumerable.Range(0,current.Candidates.Length).SelectMany(i=>current.Transports.Select(t=>(Index:i,Transport:t))):current.EligibleRows().Select(r=>(r.Index,r.Transport)));
                return options.Where(x=>!attempted.Contains(x)&&(!manualBatch||selection.Count==0||selection.Contains(x.Index+":"+x.Transport))&&(done==null||!done.Contains(x))).Take(manualBatch?Math.Max(0,16-attempted.Count):int.MaxValue).ToArray();
            }
            bool verifyHttps=current.Family=="soar"?current.Mode=="e2e":current.FullVerification;if(verifyHttps)_=SubscriptionUrls.Validate(current.ProbeUrl);
            var trust=new TunnelScanTrust(data.SshKeys.GetValueOrDefault(SshHostKey.Scope(targetProfile),""),StoredSlipCertificate(targetProfile));
            while(true){
            ct.ThrowIfCancellationRequested();var targets=Pending();
            if(targets.Length==0){
                if(firstServer||direct||!manualBatch&&current.Mode!="simple"||!ReferenceEquals(shown,current)||scanStop==null||scanStop.IsCancellationRequested||manualBatch&&(attempted.Count>=16||selection.Count>0&&attempted.Count>=selection.Count)||current.Mode is not("simple" or "advanced" or "prism"))break;
                await pulse.Task.WaitAsync(ct);continue;
            }
            foreach(var target in targets)attempted.Add(target);
            if(!firstServer){batchTotal+=targets.Length;redraw();}
            await Parallel.ForEachAsync(targets,new ParallelOptions{MaxDegreeOfParallelism=current.Family=="slipstream"?1:current.TunnelWorkers,CancellationToken=ct},async(target,token)=>{
                string id=target.Index+":"+target.Transport;await Dispatcher.UIThread.InvokeAsync(()=>{active[id]=current.Candidates[target.Index]+" · "+DnsTransportLabel(target.Transport);redraw();});
                try{
                    var proof=await resolverChecks.Server(targetProfile,current.Candidates[target.Index],target.Transport,current.ProbeUrl,trust,current.TunnelTimeoutMS,verifyHttps,token);token.ThrowIfCancellationRequested();
                    await Dispatcher.UIThread.InvokeAsync(()=>{token.ThrowIfCancellationRequested();var row=current.Results.SingleOrDefault(r=>r.Index==target.Index&&r.Transport==target.Transport);if(row==null){row=new(){Index=target.Index,Transport=target.Transport};current.Results.Add(row);}
                        if(firstServer){row.ServerChecked=true;row.ServerReached=proof.TunnelEstablished;row.ServerElapsedMS=proof.HandshakeMs>0?proof.HandshakeMs:proof.ElapsedMs;row.ServerError=proof.Error;}
                        {row.TunnelFinished=true;row.TunnelEstablished=proof.TunnelEstablished;row.SshEstablished=proof.SshEstablished;row.HttpsVerified=proof.Verified;row.HttpsAttempted=proof.HttpsAttempted||proof.Verified;row.HttpsElapsedMS=proof.HttpsElapsedMs;row.TunnelElapsedMS=proof.HandshakeMs>0?proof.HandshakeMs:proof.ElapsedMs;row.TunnelError=proof.Error;if(!firstServer){batchDone++;if(proof.TunnelEstablished)batchPassed++;if(proof.Verified)batchHttps++;}}row.TestedAt=DateTimeOffset.UtcNow;if(!current.CanApply(row))current.Selected.Remove(id);fresh.Add(id);Commit(d=>d.SaveScan(current));redraw();});
                }finally{await Dispatcher.UIThread.InvokeAsync(()=>{active.Remove(id);redraw();});}
            });
            }
        }
        async Task Run(bool continuing,bool tunnelOnly=false,bool again=false){
            if(!ReferenceEquals(currentBody,layout)||exiting)return;
            if(tunnelOnly){if(scanStop!=null&&shown?.Mode is not("advanced" or "prism")||tunnelScanStop!=null||shown==null||!shown.HasTunnelWork(again))return;await TunnelPhase(again);return;}
            if(scanStop!=null||tunnelScanStop!=null&&!(continuing&&shown?.Mode is "advanced" or "prism"))return;skipAutomaticTunnel=false;
            int Number(TextBox input){if(!int.TryParse(input.Text,out int n))throw new SafeError("DNS_SCAN_INVALID");return n;}
            if(!continuing){string chosen=SelectedTransport();var draft=new ScanCheckpoint{Mode=modes[Math.Max(0,mode.SelectedIndex)],FullVerification=fields!=null&&ResolverScan.Family(fields)=="soar"?ModeName()=="e2e":complete.IsChecked==true,ResponseSize=ModeName()=="prism"?Number(bytes):0,Prefilter=ModeName()=="prism"&&pre.IsChecked==true,ScoreMinimum=ModeName() is "simple" or "advanced"?Number(minScore):0,QuerySize=fields!=null&&int.TryParse(fields[39],out var qsize)?qsize:0,Source=sources[Math.Max(0,sourceChoice.SelectedIndex)],Bank=bank,SampleLimit=int.TryParse(limit.SelectedItem?.ToString(),out var size)?size:256,Family=profile==null?"dnstt":ResolverScan.Family(TransportPlan.Normalize(Importer.Fields(profile.Raw))),Zone=zone.Text?.Trim().TrimEnd('.')??"",PublicKey=key.Text??"",ProfileId=profile?.Id??"",ProfileFingerprint=profile==null?"":Importer.Fingerprint(profile.Raw),Candidates=ResolverScan.FromText(candidates.Text??"",chosen=="both"?"udp":chosen,2048).ToArray(),Transports=chosen=="both"?["udp","tcp"]:[chosen],Workers=Number(workers),TimeoutMS=Number(timeout),Count=Number(count),Threshold=Number(threshold),TunnelWorkers=Number(tunnelWorkers),TunnelTimeoutMS=Number(tunnelTimeout),ProbeUrl=complete.IsChecked==true?SubscriptionUrls.Validate(url.Text??"").AbsoluteUri:url.Text?.Trim()??""};
                draft.Validate();if(draft.ServerFirst&&draft.Candidates.Length>16)throw new SafeError("MAC_E2E_LIMIT");
                if((draft.ServerFirst||draft.Direct)&&profile==null)throw new SafeError("DNS_PROFILE_REQUIRED");shown=draft;fresh.Clear();
            }else if(shown!=null)RestoreForm(shown);
            var current=shown??throw new SafeError("DNS_SCAN_INVALID");current.Validate();MatchProfile();if(current.ServerFirst&&current.HasDnsRemaining()&&current.Candidates.Length>16)throw new SafeError("MAC_E2E_LIMIT");
            Commit(d=>{d.SaveScan(current);d.ResolverBank=current.Bank;d.ScanLimit=current.SampleLimit;});message="";UpdateStatus();Switch(true);runGeneration++;
            if(!current.Direct&&current.HasDnsRemaining()){
                using var cancellation=new CancellationTokenSource();var dnsEnd=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);dnsSettled=dnsEnd.Task;scanStop=cancellation;Task? automatic=null;redraw();try{
                    var prior=current.Results.Select(Id).ToHashSet();var lastRender=DateTimeOffset.MinValue;var lastSave=DateTimeOffset.UtcNow;
                    if(current.ServerFirst)await TestTunnels(false,cancellation.Token,true);
                    else await resolverChecks.Dns(current,()=>new ValueTask(Dispatcher.UIThread.InvokeAsync(()=>{NotifyDns();if(current.Mode=="simple"&&profile!=null&&automatic==null&&!skipAutomaticTunnel&&ReferenceEquals(currentBody,layout)&&current.HasTunnelWork(false))automatic=Guard(()=>TunnelPhase(false));
                        var now=DateTimeOffset.UtcNow;if(now-lastRender>TimeSpan.FromMilliseconds(200)){lastRender=now;fresh.UnionWith(current.Results.Select(Id).Where(id=>!prior.Contains(id)));redraw();}if(now-lastSave>TimeSpan.FromSeconds(3)){lastSave=now;Commit(d=>d.SaveScan(current));}}).GetTask()),cancellation.Token);
                    fresh.UnionWith(current.Results.Select(Id).Where(id=>!prior.Contains(id)));
                }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){return;}
                finally{try{scanStop=null;NotifyDns();if(automatic!=null)await automatic;if(!exiting)Commit(d=>d.SaveScan(current));redraw();}finally{dnsEnd.TrySetResult();}}
                if(cancellation.IsCancellationRequested)return;
            }
            if(!skipAutomaticTunnel&&ReferenceEquals(currentBody,layout)&&profile!=null&&(current.Direct||current.Mode=="simple")&&tunnelScanStop==null&&current.HasTunnelWork(false))await TunnelPhase(false);
        }
        async Task TunnelPhase(bool again){
            var current=shown!;batchTotal=batchDone=batchPassed=batchHttps=0;using var cancel=new CancellationTokenSource();var tunnelEnd=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);tunnelSettled=tunnelEnd.Task;tunnelScanStop=cancel;runGeneration++;Switch(true);redraw();
            try{await TestTunnels(again,cancel.Token);}
            catch(OperationCanceledException)when(cancel.IsCancellationRequested){}
            finally{try{tunnelScanStop=null;active.Clear();if(!exiting)Commit(d=>d.SaveScan(current));redraw();}finally{tunnelEnd.TrySetResult();}}
        }
        resultsView.Children.Remove(verificationActions);resultsView.Children.Insert(0,verificationActions);
        // When a phase is running, Stop remains reachable without scrolling past result rows.
        verificationActions.Children.Remove(stopTunnel);actions.Children.Add(stopTunnel);
        redraw=Render;begin=Run;Switch(shown?.Results.Count>0);redraw();NavigateFixed("DNS_SCANNER",layout);
    }
}
