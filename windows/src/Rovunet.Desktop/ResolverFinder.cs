using System.Globalization;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Markup;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly SemaphoreSlim scannerTrustGate19=new(1,1);
    sealed class ScanEntry : System.ComponentModel.INotifyPropertyChanged
    {
        public required string Address {get;init;}
        internal required ResolverResult Result;
        internal string Detail = "";
        public bool Selectable {get;internal set;}
        internal bool Pending,Stale,Testing,DnsCached,TunnelCached;
        internal readonly Dictionary<string,ResolverResult> Probes=new(StringComparer.Ordinal);
        internal readonly HashSet<string> CachedDnsMethods=new(StringComparer.Ordinal);
        internal bool TunnelFinished,TunnelVerified,HttpsVerified;
        internal string VerifiedTransport="";
        internal string TunnelError="";
        internal string Phase="";
        internal IEnumerable<ResolverResult> Proofs=>Probes.Count>0?Probes.Values:[Result];
        public bool TunnelPassed=>TunnelFinished?TunnelVerified:VerifiedElapsedMs.HasValue&&Selectable;
        public int Score=>Result.Checks?.Length??0;
        public int PrismScore=>Result.Passed;
        public string AddressSortKey{get{try{var endpoint=DnsProbe.Endpoint(Address);return Convert.ToHexString(endpoint.Address.MapToIPv6().GetAddressBytes())+endpoint.Port.ToString("D5",CultureInfo.InvariantCulture);}catch{return Address;}}}
        public bool MatchesProbes(IEnumerable<string> checks)=>Proofs.Any(r=>checks.All(key=>(r.Checks??[]).Contains(key)));
        internal long? VerifiedElapsedMs;
        public int Rank=>Selectable?0:Pending?1:Stale?3:2;
        public int ScanIndex=>Result.Index;
        public long Latency=>TunnelPassed?VerifiedElapsedMs??long.MaxValue:long.MaxValue;
        public string Ping=>Selectable?Latency.ToString(CultureInfo.InvariantCulture)+" ms":Pending?"…":"—";
        public string TransportLabel=>string.Join(" + ",Proofs.Where(r=>r.Pass).Select(r=>DnsTransportLabel20(r.Transport)).DefaultIfEmpty(DnsTransportLabel20(Result.Transport)));
        public string DnsPing=>DnsLatency<long.MaxValue?DnsLatency.ToString(CultureInfo.InvariantCulture)+" ms":"—";
        public long DnsLatency=>Result.DnsElapsedMS>0?Result.DnsElapsedMS:(Result.Pass||Result.Dns)&&Result.ElapsedMS>0?Result.ElapsedMS:long.MaxValue;
        public Brush DnsBrush=>Stale||DnsCached?Muted:Result.Pass||Result.Dns?Green:Red;
        public string ChecksLabel=>Result.Completed>0?(Result.Checks?.Length??0)+"/"+Result.Completed:Result.Attempted>0?Result.Passed+"/"+Result.Attempted:"";
        public Brush ResultBrush=>Pending||Testing||Stale||TunnelCached||!TunnelFinished&&Detail==L.Text("CANCELLED")?Muted:TunnelFinished&&!TunnelVerified?Red:Selectable?Green:Red;
        public string Description=>Stale?L.Text("RESULT_STALE"):Testing?L.Text("SCAN_TESTING"):Pending?L.Text("TEST_QUEUED"):TunnelPassed?L.Text("SCAN_TUNNEL")+" "+VerifiedElapsedMs+" ms"+(VerifiedTransport.Length>0?" · "+VerifiedTransport.ToUpperInvariant():""):Detail.Length>0?Detail:Result.Error.Length>0?L.Text(Result.Error):Result.Dns?L.Text("DNS_REPLIED"):Selectable?L.Text("TEST_PASSED"):L.Text("DNS_FAILED");
        public string Display=>ToString();
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        internal void Refresh()=>PropertyChanged?.Invoke(this,new(null));
        public override string ToString()=>ToString(true);
        string ToString(bool address)
        {
            var r=Result;
            var parts=new List<string>();if(address)parts.Add("\u2066"+Address+"\u2069");parts.Add(r.Transport.ToUpperInvariant());
            if(Selectable)parts.Add(L.Text("TEST_PASSED"));
            if(r.Dns)parts.Add(L.Text("DNS_REPLIED"));
            if(r.Completed>0)parts.Add(string.Join(" / ",r.Checks??[])+(r.Edns>0?" · EDNS "+r.Edns:"")+" · "+(r.Checks?.Length??0)+"/"+r.Completed);
            if(r.Attempted>0)parts.Add("Prism "+r.Passed+"/"+r.Attempted);
            if(r.Handshake)parts.Add(L.Text("HANDSHAKE_OK"));
            if(r.Error.Length>0)parts.Add(L.Text(r.Error));
            if(Detail.Length>0)parts.Add(Detail);
            if(!Selectable&&!Pending&&!Stale&&r.Error.Length==0&&Detail.Length==0)parts.Add(L.Text("DNS_FAILED"));
            return string.Join("   ·   ",parts);
        }
    }

    void ScanMenu()
    {
        var profiles=data.Profiles.Where(p=>p.CanConnect && ProfileEditing.IsDns(p)).ToList();

        var d=Dialog("FIND_DNS",550,390);
        var c=new StackPanel{Margin=new(20)};
        c.Children.Add(Text(L.Text(profiles.Count==0?"SCAN_NEEDS_PROFILE":"SELECT_PATH"),14));
        var choose=new ComboBox{ItemsSource=profiles,DisplayMemberPath=nameof(Profile.Name),SelectedItem=profiles.FirstOrDefault(p=>p.Id==data.SelectedId)??profiles.FirstOrDefault()};
        c.Children.Add(choose);
        var withProfile=Button("FIND_DNS",()=>{if(choose.SelectedItem is Profile selected){d.DialogResult=true;ResolverFinder(selected);}},true);withProfile.IsEnabled=profiles.Count>0;c.Children.Add(withProfile);
        c.Children.Add(Button("SCAN_STANDALONE",()=>{d.DialogResult=true;ResolverFinder(null);}));
        c.Children.Add(Button("DNS_CHECK",DnsDialog));
        d.Content=c;d.ShowDialog();
    }

    static string[] CatalogLines(string resource)
    {
        return ResolverCatalog.Lines(resource);
    }

    void ResolverFinder(Profile? profile, Action<Profile>? apply=null)
    {
        if(profile!=null && (!profile.CanConnect || !ProfileEditing.IsDns(profile))) {Info("FIND_DNS",L.Text("SCAN_NEEDS_PROFILE"));return;}
        BuildResolverFinder(profile,apply).ShowDialog();
    }
    DialogWindow BuildResolverFinder(Profile? profile, Action<Profile>? apply=null)
    {
        ScannerSession19? session=null;
        var fields=profile!=null?TransportPlan.Normalize(Importer.Fields(profile.Raw)):new string[88];
        if(profile==null){Array.Fill(fields,"");fields[1]="dnstt";fields[3]="example.com";fields[22]="udp";}
        var scope=profile?.Id??"standalone";string scanFingerprint19=profile==null?"":Importer.Fingerprint(profile.Raw);
        string family=ResolverScan.Family(fields);
        bool e2eSupported=profile!=null&&!(family=="dnstt"&&fields[22]=="https");
        string[] transports=ResolverScan.TransportChoices(fields);
        var d=Dialog("DNS_SCANNER",500,800);d.UsePageChrome();
        var panel=new StackPanel{Margin=new(18,8,18,20)};var modeHost=new StackPanel();var wireHost=new StackPanel();var basicFields=new WrapPanel();
        panel.Children.Add(Text(profile==null?L.Text("SCAN_STANDALONE"):profile.Name+"  ·  "+profile.Protocol,17));
        var standaloneDomain=new TextBox{Text=fields[3],MinHeight=34,FlowDirection=FlowDirection.LeftToRight};
        if(profile==null){panel.Children.Add(Text(L.Text("TUNNEL_DOMAIN"),12,Muted));panel.Children.Add(standaloneDomain);}
        var selectors=new StackPanel();panel.Children.Add(selectors);
        ComboBox Select(string key, params (string Value,string Label)[] items)
        {
            var box=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Value",Tag=key};
            box.ItemsSource=items.Select(x=>new{ x.Value,x.Label }).ToList();box.SelectedIndex=0;
            if(key is "SCAN_MODE" or "TRANSPORT" or "SCAN_SOURCE") {box.Visibility=Visibility.Collapsed;var host=key=="SCAN_MODE"?modeHost:key=="TRANSPORT"?wireHost:selectors;host.Children.Add(box);host.Children.Add(ChoiceChips(box));}
            else selectors.Children.Add(OutlinedField(key,box));return box;
        }        var modes=new List<(string,string)>{("simple",L.Text("SCAN_SIMPLE")),("advanced",L.Text("SCAN_ADVANCED")),("e2e",L.Text("SCAN_E2E"))};
        if(profile==null)modes.RemoveAll(x=>x.Item1!="advanced");
        else if(family!="slipstream")modes.Add(("prism","Prism"));
        if(family=="dnstt"&&fields[22]=="https")modes.RemoveAll(x=>x.Item1 is "simple" or "e2e");
        var mode=Select("SCAN_MODE",modes.ToArray());
        var wire=Select("TRANSPORT",transports.Select(t=>(t,DnsTransportLabel20(t))).ToArray());
        var sources=new List<(string,string)>{("profile",L.Text("SCAN_SOURCE_PROFILE")),("input",L.Text("SCAN_SOURCE_INPUT"))};
        if(fields[22]!="https")sources.AddRange(new[]{("resolvers.txt",L.Text("SCAN_SOURCE_APP")),("ir_dns_lite.cidr",L.Text("SCAN_IR_LITE")),("ir_dns.cidr",L.Text("SCAN_IR_FULL")),("ir.cidr",L.Text("DNS_BANK_IR")),("ru.cidr",L.Text("DNS_BANK_RU")),("cn.cidr",L.Text("DNS_BANK_CN")),("range",L.Text("SCAN_RANGE"))});
        if(profile==null)sources.RemoveAll(x=>x.Item1=="profile");
        var source=Select("SCAN_SOURCE",sources.ToArray());if(profile==null)source.SelectedValue="resolvers.txt";
        var bankStatus=Text("",12,Muted);bankStatus.Tag="DNS_BANK_STATUS";
        var bankButton=Button("DNS_BANK_TITLE",()=>{var picker=BuildResolverBankPicker(source.SelectedValue?.ToString(),selected=>source.SelectedValue=selected);picker.Owner=d;picker.ShowDialog();});bankButton.IsEnabled=fields[22]!="https";
        string current=fields[22]=="https"?fields[21]:string.Join('\n',fields[4].Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>TransportPlan.SlipResolver(x).Address));
        var input=new TextBox{Tag="SCAN_INPUT",Text=current,AcceptsReturn=true,Height=65,FlowDirection=FlowDirection.LeftToRight,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        panel.Children.Add(input);
        var pick=Button("CHOOSE_FILE",()=>{
            var dialog=new OpenFileDialog{Filter="TXT / CIDR|*.txt;*.cidr|*.*|*.*"};
            if(dialog.ShowDialog(d.NativeOwner)!=true)return;
            if(new FileInfo(dialog.FileName).Length>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
            source.SelectedValue=dialog.FileName.EndsWith(".cidr",StringComparison.OrdinalIgnoreCase)?"range":"input";input.Text=File.ReadAllText(dialog.FileName);
        });
        var options=new Expander{Header=L.Text("SCAN_OPTIONS"),Margin=new(0,5,0,5)};
        var form=new WrapPanel();options.Content=form;panel.Children.Add(options);
        FrameworkElement? limitContainer=null;
        TextBox Number(string key,int value)
        {
            var cell=new StackPanel{Margin=new(0,0,8,0),Width=146};if(key=="SCAN_LIMIT")limitContainer=cell;
            var box=new TextBox{Tag=key,Text=value.ToString(CultureInfo.InvariantCulture),FlowDirection=FlowDirection.LeftToRight};cell.Children.Add(OutlinedField(key,box));if(key is "SCAN_PORT" or "SCAN_LIMIT" or "SCAN_WORKERS" or "SCAN_TIMEOUT")basicFields.Children.Add(cell);else form.Children.Add(cell);return box;
        }
        var prefs=data.ResolverOptions.GetValueOrDefault(scope)??new ScanOptions();
        int initialPort=prefs.Port>0?prefs.Port:53;if(prefs.Port==0&&profile!=null&&fields[22]!="https")try{initialPort=DnsProbe.Endpoint(TransportPlan.SlipResolver(fields[4].Split(',')[0]).Address).Port;}catch{}
        bool portOverride=prefs.Port>0,settingPort=false;var scanPort=Number("SCAN_PORT",initialPort);scanPort.TextChanged+=(_,_)=>{if(!settingPort)portOverride=true;};
        var shuffle=new CheckBox{Content=L.Text("SCAN_SHUFFLE"),IsChecked=prefs.Shuffle,Tag="SCAN_SHUFFLE"};
        var nearby=new CheckBox{Content=L.Text("SCAN_NEARBY"),IsChecked=prefs.Nearby,Tag="SCAN_NEARBY"};
        var wholeBank=new CheckBox{Content=L.Text("SCAN_WHOLE_BANK"),IsChecked=source.SelectedValue?.ToString()=="resolvers.txt",Tag="SCAN_WHOLE_BANK"};
        var e2eTimeout=Number("SCAN_E2E_TIMEOUT",prefs.E2eTimeout);var e2eConcurrency=Number("SCAN_E2E_CONCURRENCY",prefs.E2eConcurrency);
        var limit=Number("SCAN_LIMIT",prefs.Limit);var workers=Number("SCAN_WORKERS",prefs.Workers);var timeout=Number("SCAN_TIMEOUT",prefs.Timeout);
        var attempts=Number("SCAN_ATTEMPTS",prefs.Attempts);var threshold=Number("SCAN_THRESHOLD",prefs.Threshold);var bytes=Number("SCAN_BYTES",prefs.Size);
        var pre=new CheckBox{Content=L.Text("SCAN_PREFILTER"),IsChecked=prefs.Prefilter,Margin=new(4)};form.Children.Add(pre);
        var target=new TextBox{Text=data.LatencyUrl,FlowDirection=FlowDirection.LeftToRight};form.Children.Add(OutlinedField("LATENCY_URL",target));
        if(modes.Any(x=>x.Item1==prefs.Mode))mode.SelectedValue=prefs.Mode;
        if(transports.Contains(prefs.Transport))wire.SelectedValue=prefs.Transport;
        if(sources.Any(x=>x.Item1==prefs.Source) && prefs.Source is not ("input" or "range"))source.SelectedValue=prefs.Source;
        var hint=Text("",12,Muted);panel.Children.Add(hint);
        var actions=new WrapPanel();panel.Children.Add(actions);
        var start=Button("START_TEST",()=>{},true);var restartDns=Button("SCAN_RESTART_DNS",()=>start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)));
        var cancel=Button("STOP_TEST",()=>{});cancel.IsEnabled=false;
        actions.Children.Add(start);actions.Children.Add(cancel);actions.Children.Add(pick);
        cancel.Background=Red;cancel.Foreground=Tone("Bg");cancel.Content=L.Text("SCAN_STOP_DNS");
        var cancelTunnel=Button("SCAN_STOP_TUNNEL",()=>{});cancelTunnel.Background=Red;cancelTunnel.Foreground=Tone("Bg");cancelTunnel.IsEnabled=false;
        var progress=Text("",13,Muted);panel.Children.Add(progress);
        var results=new ObservableCollection<ScanEntry>();var list=ScanResultsList(results);var view=CollectionViewSource.GetDefaultView(results);
        var resultTools=new StackPanel();var selectedCount=Text("",12,Muted);selectedCount.Tag="SCAN_SELECTED";
        var sort=new ComboBox{Tag="SCAN_SORT",DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=new[]{("order","SCAN_SORT_ORDER"),("dns","SCAN_SORT_DNS"),("address","SCAN_SORT_ADDRESS"),("score","SCAN_SCORE"),("latency","SCAN_SORT_TUNNEL"),("prism","SCAN_PRISM_SCORE")}.Select(x=>new{Value=x.Item1,Label=L.Text(x.Item2)}).ToArray()};
        var filter=new TextBox{Tag="SCAN_RESULT_FILTER",MinHeight=44};
        ComboBox ResultChoice(string key,(string Value,string Label)[] items)=>new(){Tag=key,DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=items.Select(x=>new{x.Value,Label=L.Text(x.Label)}).ToArray(),SelectedIndex=0,ToolTip=L.Text(key)};
        var transportFilter=ResultChoice("SCAN_PROBE_FILTER",[("all","SCAN_ALL_PROBES"),("udp","SCAN_UDP_PROBES"),("tcp","SCAN_TCP_PROBES"),("tls","SCAN_TLS_PROBES"),("https","SCAN_DOH_PROBES")]);
        var statusFilter=ResultChoice("SCAN_RESULT_STATUS",[("passed","SCAN_ONLY_WORKING"),("tunnel","SCAN_ONLY_TUNNEL"),("all","SCAN_ALL_RESULTS"),("failed","SCAN_ONLY_FAILED")]);
        var savedView=data.ScannerViews.GetValueOrDefault(scope)??new ScannerViewOptions();
        var requiredProbes=savedView.Probes.Where(p=>new[]{"NS","TXT","A","LONG-TXT","EDNS","NXDOMAIN"}.Contains(p)).ToHashSet();int scoreMinimum=Math.Clamp(savedView.Score,0,6),prismMinimum=Math.Clamp(savedView.PrismMinimum,0,30);
        sort.SelectedValue=savedView.Sort;sort.SelectedIndex=sort.SelectedIndex<0?0:sort.SelectedIndex;transportFilter.SelectedValue=savedView.Transport;statusFilter.SelectedValue=savedView.Status;
        var chosenEntries=new HashSet<ScanEntry>();Button workingToggle=null!,searchAction=null!,moreAction=null!;
        var probeButton=Button("SCAN_ALL_PROBES",()=>{});probeButton.Tag="SCAN_DIAGNOSTIC_FILTER";var probeMenu=new ContextMenu();probeButton.ContextMenu=probeMenu;
        foreach(var (key,label) in new[]{("NS","NS"),("TXT","TXT"),("A","RND"),("LONG-TXT","DPI"),("EDNS","EDNS"),("NXDOMAIN","NXD")}){var item=new MenuItem{Header=label,IsCheckable=true,IsChecked=requiredProbes.Contains(key),StaysOpenOnClick=true};item.Click+=(_,_)=>{if(item.IsChecked)requiredProbes.Add(key);else requiredProbes.Remove(key);FilterResults();SaveView();};probeMenu.Items.Add(item);}
        probeButton.Click+=(_,_)=>{probeMenu.PlacementTarget=probeButton;probeMenu.IsOpen=true;};
        workingToggle=Button("SCAN_ONLY_WORKING",()=>statusFilter.SelectedValue=statusFilter.SelectedValue?.ToString()=="tunnel"?"passed":"tunnel");workingToggle.Tag="SCAN_WORKING_TOGGLE";
        var selectPassed=Button("SCAN_SELECT_PASSED",()=>{});var clearSelection=Button("SCAN_CLEAR_SELECTION",()=>{});var selectActions=new WrapPanel();selectActions.Children.Add(selectedCount);selectActions.Children.Add(clearSelection);resultTools.Children.Add(selectActions);
        var use=Button("USE_DNS",()=>{},true);use.IsEnabled=false;d.SetPageAction(use);use.Visibility=profile==null?Visibility.Collapsed:Visibility.Visible;
        var fullVerification=new CheckBox{Content=L.Text("SCAN_FULL_VERIFICATION"),IsChecked=prefs.FullVerification,Tag="SCAN_FULL_VERIFICATION",ToolTip=L.Text("SCAN_FULL_VERIFICATION_HINT")};form.Children.Add(fullVerification);
        var resumeDns=Button("SCAN_RESUME",()=>{});resumeDns.Tag="SCAN_RESUME_DNS";
        var testTunnel=Button("SCAN_START_TUNNEL",()=>{},true);testTunnel.Tag="SCAN_START_TUNNEL";
        var resumeTunnel=Button("SCAN_RESUME_TUNNEL",()=>{});resumeTunnel.Tag="SCAN_RESUME_TUNNEL";
        var retest=Button("SCAN_RETEST_TUNNEL",()=>{});retest.Tag="SCAN_RETEST_TUNNEL";
        foreach(var b in new[]{cancel,cancelTunnel}){b.Background=new SolidColorBrush(Color.FromRgb(199,45,64));b.Foreground=Brushes.White;}
        bool active=false,closePending=false,resultsShown=false,selectionSync=false,applying=false;string probeUrl=data.LatencyUrl;

        var configBody=new StackPanel();var settingsViewport=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var visualRoot=new Grid();visualRoot.Children.Add(settingsViewport);ScannerPresentation? presentation=null;
        var showResults=Button("SCAN_RESULTS",()=>ShowResults());showResults.IsEnabled=false;
        searchAction=IconButton("SEARCH",MenuIcon("SEARCH"),()=>{});searchAction.Background=Brushes.Transparent;searchAction.Visibility=Visibility.Collapsed;d.AddToolbarAction(searchAction);
        BindSearchAction30(filter,searchAction,FilterResults,"SCAN_SEARCH_ACTION");
        moreAction=IconButton("MORE","M12,3 V5 M12,11 V13 M12,19 V21",()=>{});moreAction.Background=Brushes.Transparent;moreAction.Visibility=Visibility.Collapsed;d.AddToolbarAction(moreAction);
        moreAction.Content=FilledMoreIcon28();moreAction.Tag="SCANNER_MORE";
        void ShowResults(bool show=true){resultsShown=show;settingsViewport.Visibility=show?Visibility.Collapsed:Visibility.Visible;if(presentation!=null)presentation.Root.Visibility=show?Visibility.Visible:Visibility.Collapsed;searchAction.Visibility=moreAction.Visibility=show?Visibility.Visible:Visibility.Collapsed;d.SetPageTitle(L.Text(show?"SCAN_RESULTS":"DNS_SCANNER"));}
        void SaveView(){var prior=data.ScannerViews.GetValueOrDefault(scope);data.ScannerViews[scope]=new(){Sort=sort.SelectedValue?.ToString()??"order",Score=scoreMinimum,PrismMinimum=prismMinimum,Probes=requiredProbes.ToArray(),Status=statusFilter.SelectedValue?.ToString()??"passed",Transport=transportFilter.SelectedValue?.ToString()??"all"};try{Save();}catch{if(prior==null)data.ScannerViews.Remove(scope);else data.ScannerViews[scope]=prior;progress.Text=L.Text("PRIVATE_STORE_FAILED");}}
        bool ScorePass(ScanEntry entry)=>session?.DirectTunnel==true||entry.Result.Completed==0||entry.Score>=scoreMinimum;
        void RefreshView(){selectionSync=true;try{view.Refresh();foreach(var entry in chosenEntries.Where(e=>e.Selectable&&!e.Stale&&list.Items.Contains(e)))if(!list.SelectedItems.Contains(entry))list.SelectedItems.Add(entry);}finally{selectionSync=false;}}
        void FilterResults(){string query=filter.Text.Trim(),wireChoice=transportFilter.SelectedValue?.ToString()??"all",statusChoice=statusFilter.SelectedValue?.ToString()??"passed";selectionSync=true;view.Filter=value=>value is ScanEntry entry&&(wireChoice=="all"||entry.Proofs.Any(r=>r.Transport==wireChoice))&&(statusChoice switch{"passed"=>entry.Selectable||entry.Pending||entry.Testing,"tunnel"=>entry.TunnelPassed,"failed"=>!entry.Pending&&!entry.Testing&&(entry.TunnelFinished?!entry.TunnelPassed:!entry.Selectable),_=>true})&&(statusChoice is "all" or "failed"||ScorePass(entry)&&entry.PrismScore>=prismMinimum&&entry.MatchesProbes(requiredProbes))&&(query.Length==0||entry.Address.Contains(query,StringComparison.OrdinalIgnoreCase)||entry.Description.Contains(query,StringComparison.CurrentCultureIgnoreCase));selectionSync=false;RefreshView();probeButton.Content=requiredProbes.Count==0?L.Text("SCAN_ALL_PROBES"):string.Join(" + ",requiredProbes.Select(ScannerProbeLabel));workingToggle.Content=L.Text(statusChoice=="tunnel"?"SCAN_ONLY_TUNNEL":"SCAN_ONLY_WORKING");}
        void RefreshPresentation(){if(presentation!=null){string key=session?.Source??source.SelectedValue?.ToString()??"";var b=ResolverCatalog.Banks.FirstOrDefault(x=>x.File==key);presentation.BankScope.Text=b==null?"":L.Text(b.LabelKey)+" · "+BankSummary(b)+" · "+string.Format(L.Text("SCAN_PLANNED_COUNT"),session?.Total??(b.Networks&&int.TryParse(limit.Text,out int n)?n:ResolverCatalog.Count(b)));}presentation?.Refresh(session?.Total??results.Count,session?.Scanned??results.Count,session?.Working??results.Count(e=>e.Selectable),session?.TunnelTotal??results.Count(e=>e.TunnelPassed||e.Pending||e.Testing),session?.TunnelCompleted??results.Count(e=>e.TunnelPassed),session?.TunnelPassed??results.Count(e=>e.TunnelPassed),(session?.Running??results.Where(e=>e.Testing)).Select(e=>e.Address+" · "+(e.Phase.Length>0?e.Phase:L.Text("SCAN_TESTING"))),profile!=null&&(session?.TunnelTotal>0||mode.SelectedValue?.ToString() is "simple" or "e2e"));}
        void Inputs(){
            active=session?.Busy==true;mode.IsEnabled=wire.IsEnabled=configBody.IsEnabled=!active;string selectedMode=mode.SelectedValue?.ToString()??"advanced";
            if(!portOverride){settingPort=true;scanPort.Text=(source.SelectedValue?.ToString()=="profile"?initialPort:wire.SelectedValue?.ToString()=="tls"?853:53).ToString(CultureInfo.InvariantCulture);settingPort=false;}
            hint.Text=L.Text(selectedMode switch{"simple"=>family=="soar"?"SCAN_HINT_SOAR_SIMPLE":"SCAN_HINT_SIMPLE","advanced"=>"SCAN_HINT_ADVANCED","prism"=>"SCAN_HINT_PRISM",_=>"SCAN_HINT_E2E"});
            input.IsReadOnly=active||source.SelectedValue is not("input" or "range");input.Visibility=source.SelectedValue is "input" or "range" or "profile"?Visibility.Visible:Visibility.Collapsed;if(source.SelectedValue?.ToString()=="profile")input.Text=current;
            var bank=ResolverCatalog.Banks.FirstOrDefault(b=>b.File==source.SelectedValue?.ToString());bankStatus.Text=fields[22]=="https"?L.Text("DNS_BANK_DOH"):bank!=null?L.Text(bank.LabelKey)+" · "+BankSummary(bank):string.Format(L.Text("DNS_BANK_AVAILABLE"),ResolverCatalog.Banks.Length);bankButton.IsEnabled=!active&&fields[22]!="https";if(bank!=null){int.TryParse(limit.Text,out int requested);int planned=bank.Networks?Math.Clamp(requested,0,ResolverScan.MaximumCandidates):ResolverCatalog.Count(bank);bankStatus.Text+="\n"+string.Format(L.Text("SCAN_PLANNED_COUNT"),planned);}
            attempts.IsEnabled=threshold.IsEnabled=bytes.IsEnabled=pre.IsEnabled=selectedMode=="prism";target.IsEnabled=profile!=null;fullVerification.IsEnabled=profile!=null;
            if(limitContainer!=null)limitContainer.Visibility=source.SelectedValue?.ToString()=="resolvers.txt"?Visibility.Collapsed:Visibility.Visible;wholeBank.IsEnabled=false;limit.IsEnabled=!active&&source.SelectedValue?.ToString()!="resolvers.txt";nearby.IsEnabled=source.SelectedValue is "input" or "range";scanPort.IsEnabled=fields[22]!="https";
            start.IsEnabled=!active;standaloneDomain.IsEnabled=selectors.IsEnabled=options.IsEnabled=pick.IsEnabled=!active;showResults.IsEnabled=results.Count>0||session!=null;
            cancel.Visibility=session?.DnsRunning==true||selectedMode is "simple" or "e2e"&&active?Visibility.Visible:Visibility.Collapsed;cancel.IsEnabled=active&&session?.DnsStopping!=true;cancel.Content=L.Text(session?.DnsStopping==true?"CANCELLING":"SCAN_STOP_DNS");
            bool independent=selectedMode is "advanced" or "prism";resumeDns.Visibility=session?.CanResumeDns==true&&(independent||!active)?Visibility.Visible:Visibility.Collapsed;resumeDns.IsEnabled=session?.DnsRunning!=true&&(independent||!active);
            restartDns.Visibility=session!=null&&session.DnsRunning==false?Visibility.Visible:Visibility.Collapsed;restartDns.IsEnabled=!active;
            bool running=session?.TunnelRunning==true;cancelTunnel.Visibility=running?Visibility.Visible:Visibility.Collapsed;cancelTunnel.IsEnabled=running&&session?.TunnelStopping!=true;cancelTunnel.Content=L.Text(session?.TunnelStopping==true?"CANCELLING":"SCAN_STOP_TUNNEL");
            testTunnel.Visibility=e2eSupported&&!running&&session!=null&&!session.TunnelStarted?Visibility.Visible:Visibility.Collapsed;testTunnel.IsEnabled=session?.CanRetest==true;
            resumeTunnel.Visibility=e2eSupported&&session?.CanResumeTunnel==true&&session.TunnelStarted?Visibility.Visible:Visibility.Collapsed;resumeTunnel.IsEnabled=!running;
            retest.Visibility=e2eSupported&&!running&&session?.TunnelStarted==true?Visibility.Visible:Visibility.Collapsed;retest.IsEnabled=!running;
            var chosen=chosenEntries.Where(e=>e.Selectable&&!e.Stale).ToArray();selectedCount.Text=string.Format(L.Text("SCAN_SELECTION_COUNT"),chosen.Length,8);resultTools.Visibility=chosen.Length>0?Visibility.Visible:Visibility.Collapsed;use.IsEnabled=!applying&&chosen.Length>0;clearSelection.IsEnabled=chosen.Length>0;
            if(presentation!=null)presentation.StageHint.Text=L.Text(session?.DirectTunnel==true?"SCAN_DIRECT_STAGE":profile==null?"SCAN_NEEDS_PROFILE":session?.CanRetest==true?"SCAN_TWO_STAGES":session?.DnsRunning==true?"SCAN_WAIT_DNS":"SCAN_NO_ELIGIBLE");
            RefreshPresentation();if(closePending&&!active)Dispatcher.BeginInvoke(()=>d.Close());
        }
        long checkpointRevision19=-1;DateTimeOffset checkpointTime19=DateTimeOffset.UtcNow;
        void SaveCheckpoint(){if(session==null||checkpointRevision19==session.Revision)return;SaveScannerCheckpoint19(scope,session,scanFingerprint19);checkpointRevision19=session.Revision;checkpointTime19=DateTimeOffset.UtcNow;}
        var refreshTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};refreshTimer.Tick+=(_,_)=>{refreshTimer.Stop();RefreshView();Inputs();if(session?.Busy==true&&DateTimeOffset.UtcNow-checkpointTime19>TimeSpan.FromSeconds(10)){try{SaveCheckpoint();}catch{progress.Text=L.Text("PRIVATE_STORE_FAILED");}}};
        void SessionChanged(){bool ended=active&&session?.Busy==false;active=session?.Busy==true;if(ended){try{SessionHistory();}catch{progress.Text=L.Text("PRIVATE_STORE_FAILED");}}RefreshPresentation();if(!refreshTimer.IsEnabled)refreshTimer.Start();}
        void SessionHistory(){if(session==null)return;SaveCheckpoint();var saved=results.Where(e=>e.Selectable&&!e.Stale).OrderByDescending(e=>e.TunnelPassed).ThenBy(e=>e.TunnelPassed?e.Latency:e.DnsLatency).Take(256).Select(e=>new SavedResolver{Address=e.Address,Transport=e.VerifiedTransport.Length>0?e.VerifiedTransport:e.Result.Transport}).ToList();data.ResolverHistory[scope]=new(){Fingerprint=scanFingerprint19,Time=DateTimeOffset.UtcNow,Entries=saved};Save();}
        async Task Observe(Task operation){try{Inputs();await operation;if(session?.Error.Length>0)progress.Text=L.Text(session.Error);SessionHistory();}catch(Exception error){progress.Text=L.Text(error is SafeError safe?safe.Code:"DNS_SCAN_INVALID");}finally{refreshTimer.Stop();RefreshView();Inputs();}}
        async Task RunTunnels(bool fresh){if(profile==null||session==null||session.TunnelRunning)return;if(TransportPlan.UsesSlip(profile.Raw)&&StoredSlipPin(profile).Length==0&&!ChooseSlipCertificate(profile))return;await Observe(session.StartTunnel(fresh));}
        testTunnel.Click+=async(_,_)=>await RunTunnels(false);resumeTunnel.Click+=async(_,_)=>await RunTunnels(false);retest.Click+=async(_,_)=>await RunTunnels(true);cancelTunnel.Click+=(_,_)=>{session?.StopTunnel();Inputs();};
        cancel.Click+=async(_,_)=>{if(session==null)return;if(mode.SelectedValue?.ToString() is "simple" or "e2e")await Observe(session.StopAll());else{session.StopDns();Inputs();}};
        resumeDns.Click+=async(_,_)=>{if(session!=null)await Observe(session.ResumeDns());};
        clearSelection.Click+=(_,_)=>{chosenEntries.Clear();selectionSync=true;list.SelectedItems.Clear();selectionSync=false;Inputs();};
        selectPassed.Click+=(_,_)=>{chosenEntries.Clear();selectionSync=true;list.SelectedItems.Clear();foreach(var entry in list.Items.Cast<ScanEntry>().Where(e=>e.Selectable&&!e.Stale).Take(8)){chosenEntries.Add(entry);list.SelectedItems.Add(entry);}selectionSync=false;Inputs();};
        list.SelectionChanged+=(_,e)=>{if(selectionSync)return;foreach(ScanEntry entry in e.RemovedItems)chosenEntries.Remove(entry);foreach(ScanEntry entry in e.AddedItems){if(entry.Selectable&&!entry.Stale&&chosenEntries.Count<8)chosenEntries.Add(entry);else{selectionSync=true;list.SelectedItems.Remove(entry);selectionSync=false;progress.Text=L.Text("SCAN_SELECTION_LIMIT");}}Inputs();};
        sort.SelectionChanged+=(_,_)=>{selectionSync=true;SortScanResults(view,sort.SelectedValue?.ToString()??"order");selectionSync=false;RefreshView();SaveView();};
        filter.TextChanged+=(_,_)=>FilterResults();transportFilter.SelectionChanged+=(_,_)=>{FilterResults();SaveView();};statusFilter.SelectionChanged+=(_,_)=>{FilterResults();SaveView();};
        mode.SelectionChanged+=(_,_)=>Inputs();source.SelectionChanged+=(_,_)=>{wholeBank.IsChecked=source.SelectedValue?.ToString()=="resolvers.txt";Inputs();};limit.TextChanged+=(_,_)=>Inputs();wholeBank.Checked+=(_,_)=>Inputs();wholeBank.Unchecked+=(_,_)=>Inputs();
        results.CollectionChanged+=(_,_)=>{if(results.Count>0&&!resultsShown)ShowResults();if(active){if(!refreshTimer.IsEnabled)refreshTimer.Start();}else Inputs();};
        use.Click+=async(_,_)=>{if(applying)return;try{var chosen=chosenEntries.Where(e=>e.Selectable&&!e.Stale).ToArray();if(chosen.Length==0)return;applying=true;Inputs();if(session!=null)await session.StopAll();if(profile==null){SaveScannedResolvers(chosen.Select(e=>e.Address).ToArray());return;}var transportChoices=chosen.Select(e=>e.VerifiedTransport.Length>0?new[]{e.VerifiedTransport}:e.Proofs.Where(r=>r.Pass).Select(r=>r.Transport).ToArray()).ToArray();var common=transportChoices.Aggregate((a,b)=>a.Intersect(b).ToArray());if(common.Length==0)throw new SafeError("SCAN_SAME_TRANSPORT");string method=common.Contains(fields[22])?fields[22]:common[0];var next=ResolverScan.Selected(profile,chosen.Select(e=>(e.Address,method)));if(apply!=null)apply(next);else{if(state.State=="connected"&&activeProfile?.Id==profile.Id){Info("HELP",L.Text("DISCONNECT_EDIT"));return;}SaveEdited(profile,next.Raw);}d.DialogResult=true;}catch(Exception error){progress.Text=L.Text(error is SafeError safe?safe.Code:"INVALID_IMPORT");}finally{applying=false;Inputs();}};
        moreAction.Click+=(_,_)=>{
            var menu=new ContextMenu();void Item(string key,Action action,bool enabled=true){var item=new MenuItem{Header=L.Text(key),IsEnabled=enabled};item.Click+=(_,_)=>action();menu.Items.Add(item);}
            Item("SCAN_SELECT_PASSED",()=>selectPassed.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)));
            Item("COPY",()=>ScannerOutput19(d,results,list,chosenEntries,"copy"),results.Any(e=>e.Selectable));Item("EXPORT_FILE",()=>ScannerOutput19(d,results,list,chosenEntries,"export"),results.Any(e=>e.Selectable));Item("DNS_POOL_SAVE",()=>ScannerOutput19(d,results,list,chosenEntries,"pool"),!active&&results.Any(e=>e.Selectable));
            var scores=new MenuItem{Header=L.Text("SCAN_SCORE")};foreach(int minimum in Enumerable.Range(0,7)){var item=new MenuItem{Header=minimum==0?L.Text("SCAN_ALL_RESULTS"):minimum+"+",IsCheckable=true,IsChecked=minimum==scoreMinimum};item.Click+=(_,_)=>{scoreMinimum=minimum;FilterResults();SaveView();};scores.Items.Add(item);}menu.Items.Add(scores);
            Item("SCAN_PRISM_SCORE",()=>{var page=Dialog("SCAN_PRISM_SCORE");var body=new StackPanel{Margin=new(18)};var input=new TextBox{Text=prismMinimum.ToString(CultureInfo.InvariantCulture)};body.Children.Add(OutlinedField("SCAN_THRESHOLD",input));body.Children.Add(Button("SAVE",()=>{if(int.TryParse(input.Text,out int n)&&n is >=0 and <=30){prismMinimum=n;FilterResults();SaveView();page.Close();}},true));page.Content=body;page.ShowDialog();});
            Item("SCAN_ALL_RESULTS",()=>statusFilter.SelectedValue="all");Item("SCAN_ONLY_FAILED",()=>statusFilter.SelectedValue="failed");
            var transfers=new MenuItem{Header=L.Text("SCAN_TRANSPORT_FILTER")};foreach(string method in new[]{"all","udp","tcp","tls","https"}){var item=new MenuItem{Header=method=="all"?L.Text("SCAN_ALL_PROBES"):DnsTransportLabel20(method),IsCheckable=true,IsChecked=transportFilter.SelectedValue?.ToString()==method};item.Click+=(_,_)=>transportFilter.SelectedValue=method;transfers.Items.Add(item);}menu.Items.Add(transfers);menu.PlacementTarget=moreAction;menu.IsOpen=true;
        };
        void AttachSession(ScannerPlan19 plan,bool tunnelStarted=false){bool refused=false;var settings=plan.Options;if(plan.Family=="soar")settings.FullVerification=settings.Mode=="e2e";checkpointRevision19=-1;session=new(plan,results,ResolverWorker.Run,async(entry,method,reportPhase,token)=>{if(profile==null)throw new SafeError("SCAN_NEEDS_PROFILE");var candidate=ResolverScan.Candidate(profile,entry.Address,method,true);return await VerifyCandidate(candidate,profile,plan.ProbeUrl.Length>0?plan.ProbeUrl:probeUrl,token,()=>refused,value=>refused=value,plan.Family=="soar"&&settings.Mode=="simple"?settings.Timeout:settings.E2eTimeout,settings.FullVerification,reportPhase);},tunnelStarted);session.Eligible=e=>e.Result.Pass&&ScorePass(e);session.Compare=(a,b)=>CompareScannerEntries19(a,b,sort.SelectedValue?.ToString()??"order");session.Changed=SessionChanged;}
        var restored=ReadScannerCheckpoint19(scope,scanFingerprint19);
        if(restored!=null){RestoreScannerRows19(restored,results);AttachSession(new(restored.Options,restored.Candidates,restored.Methods,restored.Family,restored.Zone,restored.PublicKey,restored.AutomaticTunnel,restored.ProbeUrl,restored.QuerySize),restored.TunnelStarted);resultsShown=false;showResults.Content=L.Text("SCAN_SAVED_RESULTS");standaloneDomain.Text=restored.Zone;mode.SelectedValue=restored.Options.Mode;progress.Text=string.Format(L.Text("SCAN_RESTORED"),restored.SavedAt.ToLocalTime().ToString("g"));}
        else{var history=data.ResolverHistory.GetValueOrDefault(scope);if(history!=null&&history.Fingerprint==scanFingerprint19){foreach(var row in history.Entries)results.Add(new(){Address=row.Address,Result=new(){Transport=row.Transport},Stale=true});}}
        d.Closing+=(_,e)=>{if(session?.Busy==true){e.Cancel=true;closePending=true;_ =session.StopAll();}};d.Closed+=(_,_)=>{refreshTimer.Stop();if(session!=null){try{SaveCheckpoint();}catch{lastError="PRIVATE_STORE_FAILED";}session.Changed=()=>{};_ =session.StopAll();}};
        start.Click+=async(_,_)=>{
            if(active)return;
            try{
                int Read(TextBox box,int min,int max){if(!int.TryParse(box.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n<min||n>max)throw new SafeError("DNS_SCAN_INVALID");return n;}
                string phase=mode.SelectedValue as string??throw new SafeError("DNS_SCAN_INVALID"),protocol=wire.SelectedValue as string??throw new SafeError("DNS_SCAN_INVALID"),sourceName=source.SelectedValue as string??throw new SafeError("DNS_SCAN_INVALID");int count=Read(attempts,1,30);bool entire=sourceName=="resolvers.txt";
                var settings=new ScanOptions{Mode=phase,Transport=protocol,Source=sourceName,Port=portOverride?Read(scanPort,1,65535):0,Shuffle=shuffle.IsChecked==true,WholeBank=entire,Nearby=nearby.IsChecked==true&&nearby.IsEnabled,E2eTimeout=Read(e2eTimeout,1000,180000),E2eConcurrency=Read(e2eConcurrency,1,10),FullVerification=fullVerification.IsChecked==true,Limit=entire?128:Read(limit,1,ResolverScan.MaximumCandidates),Workers=Read(workers,1,50),Timeout=Read(timeout,200,90000),Attempts=count,Threshold=Read(threshold,1,count),Size=Read(bytes,0,4096),Prefilter=pre.IsChecked==true};
                if(settings.Size is >0 and <200||phase=="prism"&&settings.Timeout/settings.Threshold<200)throw new SafeError("DNS_SCAN_INVALID");
                if(phase=="prism"&&!ScannerKeyValid19(family,fields[11]))throw new SafeError("DNS_SCAN_INVALID");
                if(settings.FullVerification)probeUrl=SubscriptionUrls.Validate(target.Text).AbsoluteUri;string baseMode=protocol=="both"?"udp":protocol;
                IReadOnlyList<string> candidates=sourceName switch{"profile"=>ResolverScan.FromText(current,baseMode,settings.Limit),"input"=>ResolverInputs.Input(input.Text,baseMode,settings.Limit,Read(scanPort,1,65535)),"range"=>ResolverInputs.Ranges(input.Text,baseMode,settings.Limit,Read(scanPort,1,65535)),_=>ResolverCatalog.TestCandidates(sourceName,baseMode,settings.Limit)};
                candidates=ResolverInputs.Prepare(candidates,baseMode,settings.Port,settings.Shuffle);string zone=standaloneDomain.Text.Trim().TrimEnd('.');if(Uri.CheckHostName(zone)!=UriHostNameType.Dns)throw new SafeError("DNS_SCAN_INVALID");
                if(profile!=null&&phase is "simple" or "e2e"&&TransportPlan.UsesSlip(profile.Raw)&&StoredSlipPin(profile).Length==0&&!ChooseSlipCertificate(profile))throw new SafeError("SLIP_CERT_REQUIRED");
                data.ResolverOptions[scope]=settings;Save();if(session!=null)await session.StopAll();chosenEntries.Clear();results.Clear();progress.Text="";
                var plan=new ScannerPlan19(settings,candidates.ToArray(),protocol=="both"?["udp","tcp"]:[protocol],family,zone,fields[11],profile!=null&&phase is "simple" or "e2e",probeUrl,int.TryParse(fields[39],out int querySize20)?querySize20:0);
                AttachSession(plan);SaveCheckpoint();ShowResults();await Observe(session!.ResumeDns());
            }catch(Exception error){progress.Text=L.Text(error is SafeError safe?safe.Code:"DNS_SCAN_INVALID");Inputs();}
        };
        panel.Children.Clear();actions.Children.Clear();panel.Children.Add(modeHost);panel.Children.Add(hint);panel.Children.Add(bankStatus);panel.Children.Add(start);
        standaloneDomain.IsReadOnly=profile!=null;configBody.Children.Add(OutlinedField("TUNNEL_DOMAIN",standaloneDomain));configBody.Children.Add(basicFields);configBody.Children.Add(wireHost);configBody.Children.Add(SettingsRow(shuffle));configBody.Children.Add(SettingsRow(nearby,"SCAN_NEARBY_HINT"));configBody.Children.Add(SettingsRow(wholeBank,"SCAN_WHOLE_BANK_HINT"));configBody.Children.Add(selectors);configBody.Children.Add(input);configBody.Children.Add(pick);configBody.Children.Add(options);panel.Children.Add(Card(configBody));panel.Children.Add(showResults);
        presentation=new(list,cancel,resumeDns,restartDns,cancelTunnel,testTunnel,resumeTunnel,retest,sort,probeButton,workingToggle,transportFilter,statusFilter,filter,resultTools,progress);visualRoot.Children.Add(presentation.Root);
        d.BackAction=async()=>{if(resultsShown){if(session!=null)await session.StopAll();ShowResults(false);Inputs();}else d.Close();};d.UseFixedBody();d.Content=visualRoot;FilterResults();SortScanResults(view,sort.SelectedValue?.ToString()??"order");ShowResults(resultsShown);wholeBank.IsChecked=source.SelectedValue?.ToString()=="resolvers.txt";Inputs();return d;
    }

    async Task<AgentReply> VerifyCandidate(Profile candidate,Profile profile,string url,CancellationToken stop,Func<bool> trustRefused,Action<bool> refuse,int timeoutMs=115000,bool fullVerification=true,Action<string>? reportPhase=null)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(stop);
        var limit=TimeSpan.FromMilliseconds(timeoutMs);
        budget.CancelAfter(limit);
        await using var probe=new AgentClient(probeOnly:true);
        using var abort=budget.Token.Register(probe.Abort);
        try{
            await probe.Start(budget.Token);
            var cmd=new AgentCommand{Action="connect",Profile=candidate,SlipCertificate=StoredSlipPin(profile),SlipInterval=SlipInterval(profile),SshTlsCa=StoredSshTlsCa(profile),HostKey=StoredSshKey(profile)};
            var reply=await probe.Send(cmd);
            if(reply.State=="trust" && !trustRefused() && !stop.IsCancellationRequested){
                budget.CancelAfter(Timeout.InfiniteTimeSpan);
                // Concurrent candidates may have presented this same key while this one was starting.
                await scannerTrustGate19.WaitAsync(stop);
                try{
                    if(trustRefused())return reply;
                    string stored=StoredSshKey(profile);
                    if(stored.Length>0&&stored!=reply.HostKey)return new(){Error="SSH_KEY_CHANGED"};
                    if(stored.Length==0){if(!Confirm(L.Text(reply.Error)+"\n"+profile.Name+"\n"+L.Text("SSH_TRUST_HINT"),reply.Fingerprint)){refuse(true);return reply;}data.SshKeys[profile.Id]=reply.HostKey;data.SshKeyScopes[profile.Id]=SshHostKey.Scope(profile);Save();}
                }finally{scannerTrustGate19.Release();}
                stop.ThrowIfCancellationRequested();cmd.HostKey=reply.HostKey;budget.CancelAfter(limit);reply=await probe.Send(cmd);
            }
            if(reply.State!="connected")return reply;
            if(!fullVerification)return reply.TunnelEstablished?reply:new(){Error="DNS_TUNNEL_FAILED"};
            reportPhase?.Invoke(L.Text("SCAN_PHASE_HTTPS"));
            var result=await probe.Send(new(){Action="probe",ProbeUrl=url,ProbeFallbacks=data.ProbeFallbacks});
            result.TunnelEstablished=reply.TunnelEstablished;result.SshEstablished=reply.SshEstablished;return result;
        }catch(Exception) when(budget.IsCancellationRequested){
            stop.ThrowIfCancellationRequested();return new(){Error="TEST_TIMEOUT"};
        }finally{probe.Abort();}
    }
}



