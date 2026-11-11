using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Rovunet.Core;
using Rovunet.Platform;
using ShapePath=Avalonia.Controls.Shapes.Path;

namespace Rovunet.Desktop;

public sealed partial class MainWindow : Window
{
    AppState data;
    readonly PrivateStore? store;
    readonly KeychainVault? vault;
    readonly bool fixture;
    readonly IResolverChecks resolverChecks;
    readonly Func<Profile,CancellationToken,Task<string>> discoverSlipCertificate;
    Palette palette=null!;
    AgentClient? client;
    AgentReply state=new();
    string activeId="",connectingId="",message="",route="HOME";
    bool busy,polling,exiting;
    bool storeHealthy=true,servicesStarted,recoveryPending;
    string storageFailure="";
    Button? recoveryAction;
    int navigationRevision;
    CancellationTokenSource? connectStop,scanStop,tunnelScanStop,collectionStop,subscriptionPreviewStop;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly TrafficSampler sampler=new();
    long connectedTick,lastProbeTick;
    DateTimeOffset lastTick=DateTimeOffset.UtcNow;
    TextBlock statusText=null!,statusDetail=null!,statusTraffic=null!,notice=null!,heroTitle=null!,heroDetail=null!;
    Border hero=null!,sidebar=null!;
    Border heroSpacer=null!;
    Button heroPower=null!;
    ColumnDefinition heroPowerColumn=null!;
    Border powerRing=null!;
    OverflowMarquee heroDetailLine=null!;
    Grid shell=null!;
    ContentControl host=null!;
    ContentControl headerIdentity=null!;
    StackPanel cards=null!;
    ScrollViewer homeScroll=null!;
    Control? currentBody;
    readonly Stack<(string Route,Control? Body,Vector Offset)> pages=[];
    readonly HashSet<string> freshMeasurements=[],testingProfiles=[];
    TrayIcon? tray;
    NativeMenu? appNativeMenu,windowNativeMenu,fileNativeMenu,trayNativeMenu;
    readonly Dictionary<string,NativeMenuItem> appNativeItems=[],fileNativeItems=[],trayNativeItems=[];
    NativeMenuItem? fileNativeHeading;
    string trayLanguage="",trayState="";
    Button? addAnchor;
    Flyout? addFlyout;
    bool Compact=>(Bounds.Width>0?Bounds.Width:Width)<920;
    Profile? Selected=>data.Profiles.FirstOrDefault(p=>p.Id==data.SelectedId);
    IBrush Tone(string key)=>palette[key];
    public MainWindow(AppState? initial=null,bool uiFixture=false,IResolverChecks? checks=null,Func<Profile,CancellationToken,Task<string>>? certificateDiscovery=null)
    {
        resolverChecks=checks??new MacResolverChecks();discoverSlipCertificate=certificateDiscovery??SlipCertificateDiscovery.Fetch;fixture=uiFixture||!OperatingSystem.IsMacOS();data=initial??new();
        if(!fixture){vault=new KeychainVault(()=>PrivateStore.MayInitializeKey(PrivateStore.Root));store=new PrivateStore(PrivateStore.Root,vault);LoadStorage(false);}
        PrepareState();
        Build();SizeChanged+=(_,_)=>UpdateWidth();
        KeyDown+=OnKey;
        Opened+=(_,_)=>{StartServices();ClampToScreen();};
        Closing+=(_,e)=>{if(exiting)return;e.Cancel=true;if(data.CloseToTray&&tray!=null){SaveWindowSize();Hide();}else _=Exit();};
        Closed+=(_,_)=>{timer.Stop();automaticStop?.Cancel();connectStop?.Cancel();scanStop?.Cancel();tunnelScanStop?.Cancel();collectionStop?.Cancel();client?.Dispose();tray?.Dispose();vault?.Dispose();NetworkChange.NetworkAddressChanged-=OnNetworkChange;};
    }
    void LoadStorage(bool interactive)
    {
        try{
            if(interactive)vault!.Unlock();
            else CryptographicOperations.ZeroMemory(vault!.GetOrCreateKey());
            var saved=store!.Load<AppState>();data=saved;storeHealthy=true;storageFailure="";
        }catch(Exception e){
            storeHealthy=false;storageFailure=e is SafeError error?error.Code:"PRIVATE_STORE_FAILED";
            SafeDiagnostics.Error("storage",storageFailure,e);
        }
    }
    void PrepareState(){
        foreach(var profile in data.Profiles)MacProtocols.RefreshCapabilities(profile);
        if(!fixture&&storeHealthy)SafeDiagnostics.Restore(data.RecentDiagnostics.Concat(SafeDiagnostics.Events()));
        if(data.LayoutVersion<4&&data.WindowHeight==760)data.WindowHeight=680;data.LayoutVersion=4;
        L.Language=data.Language;Title="Rovunet";Width=double.IsFinite(data.WindowWidth)&&data.WindowWidth>=480?data.WindowWidth:560;Height=double.IsFinite(data.WindowHeight)&&data.WindowHeight>=560?data.WindowHeight:680;MinWidth=480;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterScreen;
    }
    void StartServices(){
        if(fixture||!storeHealthy||servicesStarted)return;servicesStarted=true;
        InstallTray();timer.Tick+=async(_,_)=>await Poll();timer.Start();NetworkChange.NetworkAddressChanged+=OnNetworkChange;
        // A crashed system-proxy session may need an admin prompt to restore. Show
        // an actionable notice; opening the app itself must not request admin rights.
        try{recoveryPending=new PrivateStore(Path.Combine(PrivateStore.Root,"system-proxy"),vault!).Load<ProxyLeaseJournal>().Services.Count>0;}
        catch(Exception e){recoveryPending=true;SafeDiagnostics.Error("proxy-recovery","MAC_PROXY_RESTORE_PENDING",e);}
        if(recoveryAction!=null)recoveryAction.IsVisible=recoveryPending;
    }
    void ShowLockedStorage(){
        var body=new StackPanel{Spacing=12,Margin=new(28),MaxWidth=500,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,FlowDirection=L.Direction};
        body.Children.Add(Label("Rovunet",28));body.Children.Add(Label(L.Text("MAC_UNLOCK_TITLE"),20));
        body.Children.Add(Label(L.Text(storageFailure=="KEYCHAIN_ACCESS_FAILED"?"MAC_UNLOCK_HINT":storageFailure.Length>0?storageFailure:"MAC_UNLOCK_HINT"),14,"Muted"));
        var unlock=ActionButton("MAC_UNLOCK",()=>{LoadStorage(true);if(storeHealthy){PrepareState();Build();StartServices();ClampToScreen();}else ShowLockedStorage();},true);unlock.Name="UnlockStorage";body.Children.Add(unlock);
        body.Children.Add(ActionButton("EXIT",()=>_=Exit()));Content=body;
    }
    void ClampToScreen(){if(Screens.ScreenFromWindow(this) is {} screen){double scale=screen.Scaling,availableWidth=Math.Max(240,screen.WorkingArea.Width/scale-32),availableHeight=Math.Max(320,screen.WorkingArea.Height/scale-32);MinWidth=Math.Min(480,availableWidth);MinHeight=Math.Min(560,availableHeight);Width=Math.Clamp(Width,MinWidth,availableWidth);Height=Math.Clamp(Height,MinHeight,availableHeight);}}
    void SaveWindowSize(){if(fixture||!storeHealthy||WindowState!=Avalonia.Controls.WindowState.Normal)return;try{Commit(d=>{d.WindowWidth=Bounds.Width;d.WindowHeight=Bounds.Height;});}catch(Exception error){Display(error);}}
    void OnNetworkChange(object? sender,EventArgs e)=>Dispatcher.UIThread.Post(()=>{if(state.State=="connected"){state.Verified=false;message=L.Text("MAC_NETWORK_CHANGED");UpdateStatus();}});
    void Build()
    {
        addFlyout?.Hide();
        if(host!=null){host.Content=null;host.Presenter?.UpdateChild();}
        Content=null;L.Language=data.Language;FontFamily=L.Font;
        bool systemDark=Application.Current?.ActualThemeVariant==ThemeVariant.Dark;
        if(palette==null)palette=new(data.Theme,systemDark);else palette.Update(data.Theme,systemDark);RequestedThemeVariant=data.Theme=="system"?ThemeVariant.Default:palette.Dark?ThemeVariant.Dark:ThemeVariant.Light;
        Background=palette.Backdrop;Foreground=Tone("Ink");
        if(!storeHealthy){ShowLockedStorage();return;}
        shell=new Grid{ColumnDefinitions=new("260,*"),FlowDirection=L.Direction,Background=palette.Backdrop};
        sidebar=BuildSidebar();shell.Children.Add(sidebar);
        var main=new Grid{RowDefinitions=new("Auto,Auto,*,Auto"),FlowDirection=FlowDirection.LeftToRight};Grid.SetColumn(main,1);shell.Children.Add(main);
        main.Children.Add(BuildHeader());
        hero=BuildHero();hero.IsVisible=currentBody==null;Grid.SetRow(hero,1);main.Children.Add(hero);
        host=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,ClipToBounds=true};Grid.SetRow(host,2);main.Children.Add(host);
        cards=new StackPanel{Margin=new(24,10,24,100),Spacing=0,FlowDirection=L.Direction};homeScroll=new ScrollViewer{Content=cards,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        heroSpacer=new Border{Height=0,IsHitTestVisible=false};
        homeScroll.ScrollChanged+=(_,_)=>{if(currentBody==null){double collapse=Math.Clamp(homeScroll.Offset.Y,0,60),fraction=collapse/60,size=112-60*fraction;hero.Height=148-collapse;heroSpacer.Height=collapse;heroPower.MinWidth=heroPower.MinHeight=heroPower.Width=heroPower.Height=size;heroPower.CornerRadius=new(size/2);powerRing.Width=powerRing.Height=size+12;powerRing.CornerRadius=new((size+12)/2);if(heroPower.Content is ShapePath powerIcon){powerIcon.Width=powerIcon.Height=40-18*fraction;}heroPowerColumn.Width=new(140-60*fraction);heroTitle.FontSize=24-4*fraction;heroDetailLine.IsVisible=fraction<.9;}};
        var home=new Grid{FlowDirection=FlowDirection.LeftToRight};home.Children.Add(homeScroll);
        var add=IconButton("ADD","M12,2 V22 M2,12 H22",ShowAddHub,64);add.Name="AddButton";ButtonSurface(add,Tone("AddSurface"),Brush.Parse("#252C46"));add.CornerRadius=new(22);add.HorizontalAlignment=HorizontalAlignment.Right;add.VerticalAlignment=VerticalAlignment.Bottom;add.Margin=new(0,0,26,18);home.Children.Add(add);
        addAnchor=add;
        host.Content=currentBody??home;
        var status=BuildStatus();status.ZIndex=10;Grid.SetRow(status,3);main.Children.Add(status);Content=shell;
        BuildCards();UpdateStatus();UpdateWidth();InstallNativeMenu();
    }
    void UpdateWidth(){if(shell==null)return;shell.ColumnDefinitions[0].Width=new(Compact?0:260);sidebar.IsVisible=!Compact;if(headerIdentity!=null&&currentBody==null)headerIdentity.Content=Compact?Brand():Label(L.Text("HOME"),24);}
    Border BuildSidebar()
    {
        var grid=new Grid{RowDefinitions=new("Auto,*,Auto"),Background=palette.Glass("Sidebar")};
        var brand=Brand();brand.Margin=new(18,16,12,12);grid.Children.Add(brand);
        var list=new StackPanel{Margin=new(8,0,8,0),Spacing=2};
        list.Children.Add(NavButton("HOME",GoHome));list.Children.Add(Label(L.Text("SECTION_GENERAL"),14,"Muted"));
        foreach(var (key,action) in new (string,Action)[]{("SETTINGS",ShowSettings),("CONNECTION_DETAILS",ShowConnectionDetails),("MANAGE_COLLECTIONS",ShowManagement),("BACKUP_EXPORT",ShowBackup),("DIAGNOSTICS",ShowDiagnostics)})list.Children.Add(NavButton(key,action));
        list.Children.Add(Label(L.Text("NAV_TOOLS"),14,"Muted"));list.Children.Add(NavButton("DNS_SCANNER",()=>ShowScanner(Selected)));
        var scroll=new ScrollViewer{Content=list,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);grid.Children.Add(scroll);
        var footer=new StackPanel{Margin=new(8,0,8,12)};footer.Children.Add(NavButton("HELP",ShowHelp));footer.Children.Add(NavButton("ABOUT",ShowAbout));footer.Children.Add(Label("Rovunet "+BuildVersion.Text,10,"Muted",true));Grid.SetRow(footer,2);grid.Children.Add(footer);
        return new Border{Child=grid,BorderBrush=Tone("Outline"),BorderThickness=new(0,0,1,0)};
    }
    Control BuildHeader()
    {
        var grid=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(18,8,12,6),FlowDirection=FlowDirection.LeftToRight};
        headerIdentity=new ContentControl{Content=currentBody==null?(Compact?Brand():Label(L.Text("HOME"),24)):new OverflowMarquee(Label(L.Text(route),23),36),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};grid.Children.Add(headerIdentity);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=2};
        if(currentBody!=null){
            actions.Children.Add(IconButton("BACK","M15,4 L7,12 L15,20",Back));
            if(currentBody.Tag is PageCommit commit){
                var button=commit.Button;if(button.Parent is Panel previous)previous.Children.Remove(button);
                actions.Children.Add(button);
            }
        }
        else foreach(string code in new[]{"fa","en","ru","zh-CN"}){var b=ActionButton(code,()=>{Commit(d=>d.Language=code);GoHome();});b.Content=Flag(code);b.Padding=new(2);b.MinWidth=36;b.Width=38;b.MinHeight=40;ToolTip.SetTip(b,code);actions.Children.Add(b);}
        actions.Children.Add(IconButton("SEARCH",Icons.PathFor("SEARCH"),ShowSearch));actions.Children.Add(IconButton("SETTINGS",Icons.PathFor("SETTINGS"),ShowSettings));actions.Children.Add(IconButton("SECTION_GENERAL","M12,3 V5 M12,11 V13 M12,19 V21",ShowGeneral));
        Grid.SetColumn(actions,1);grid.Children.Add(actions);return grid;
    }
    Control Brand()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,FlowDirection=FlowDirection.LeftToRight,Spacing=2,VerticalAlignment=VerticalAlignment.Center};
        row.Children.Add(new ShapePath{Data=Geometry.Parse("F0 M4,0 H329 C416,0 466,57 466,148 C466,238 416,296 334,296 L476,482 H317 L178,296 H129 V482 H2 Z M127,101 H333 V201 H127 Z M0,516 H476 V534 H0 Z"),Width=36,Height=40,Stretch=Stretch.Uniform,Fill=Brush.Parse(palette.Dark?"#23B2BA":"#0A8490")});
        var name=Label(L.Text("BRAND_WORDMARK"),28);name.Margin=new(0);name.FontWeight=FontWeight.Bold;row.Children.Add(name);return row;
    }
    Border BuildHero()
    {
        var grid=new Grid{ColumnDefinitions=new("140,*"),FlowDirection=L.Direction};
        heroPowerColumn=grid.ColumnDefinitions[0];
        var power=IconButton("CONNECT","M12,2 V11 M6,5 A9,9 0 1 0 18,5",()=>_=ToggleConnect(),112);power.Name="HeroPower";power.CornerRadius=new(60);power.Background=state.State=="connected"?Tone("PowerSurface"):Tone("Accent");power.Foreground=palette.Dark?Brush.Parse("#1B263D"):Brushes.White;power.HorizontalAlignment=HorizontalAlignment.Center;power.VerticalAlignment=VerticalAlignment.Center;
        heroPower=power;
        ButtonSurface(power,power.Background!,power.Foreground!);
        powerRing=new Border{Child=power,Width=124,Height=124,CornerRadius=new(70),BorderBrush=Tone("Accent"),BorderThickness=new(3),Padding=new(3),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(powerRing);var texts=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Spacing=8};heroTitle=Label("",24);heroTitle.FontWeight=FontWeight.Bold;heroDetail=Label("",14,"Muted");heroDetailLine=new OverflowMarquee(heroDetail,24);texts.Children.Add(new OverflowMarquee(heroTitle,36));texts.Children.Add(heroDetailLine);Grid.SetColumn(texts,1);grid.Children.Add(texts);
        var card=Card(grid,26,new(24,10,24,8),148);card.Background=new LinearGradientBrush{StartPoint=new(0,0,RelativeUnit.Relative),EndPoint=new(.8,1,RelativeUnit.Relative),GradientStops=[new(Color.Parse(palette.Dark?"#111B31":"#FDFBFF"),0),new(Color.Parse(palette.Dark?"#080E1C":"#E9E5FA"),1)]};return card;
    }
    Border BuildStatus()
    {
        var body=new StackPanel{Spacing=3,FlowDirection=L.Direction};var row=new Grid{ColumnDefinitions=new("Auto,*,Auto")};
        row.Children.Add(VectorIcon("M12,1 L22,5 V12 C22,18 17,22 12,24 C7,22 2,18 2,12 V5 Z M8,8 H13 Q18,8 16,12 L12,13 L17,18 M8,18 V8",34,state.State=="connected"?"Green":"Muted"));
        var texts=new StackPanel();statusText=Label("",16);statusText.FontWeight=FontWeight.SemiBold;statusDetail=Label("",11,"Muted");texts.Children.Add(new OverflowMarquee(statusText,26));texts.Children.Add(new OverflowMarquee(statusDetail,20));Grid.SetColumn(texts,1);row.Children.Add(texts);
        var details=IconButton("CONNECTION_DETAILS",Icons.PathFor("CONNECTION_DETAILS"),ShowConnectionDetails);Grid.SetColumn(details,2);row.Children.Add(details);body.Children.Add(row);
        statusTraffic=Label("",11,"Muted",true);body.Children.Add(statusTraffic);notice=Label(message,12,"Red");notice.MaxHeight=60;body.Children.Add(notice);
        recoveryAction=ActionButton("MAC_PROXY_RECOVER_NOW",()=>_=Guard(RecoverSystemProxy));recoveryAction.IsVisible=recoveryPending;body.Children.Add(recoveryAction);
        return new Border{Child=body,Background=palette.Glass("StatusSurface"),CornerRadius=new(18,18,0,0),Padding=new(12,10,12,8)};
    }
    void BuildCards()
    {
        if(cards==null)return;cards.Children.Clear();if(heroSpacer!=null)cards.Children.Add(heroSpacer);
        if(data.Profiles.Count==0){var empty=new StackPanel{Spacing=8};empty.Children.Add(Label(L.Text("EMPTY_TITLE"),19));empty.Children.Add(Label(L.Text("ADD_HUB_HINT"),14,"Muted"));cards.Children.Add(Card(empty));return;}
        foreach(var group in data.Profiles.OrderByDescending(p=>data.Pinned.Contains(p.Id)).GroupBy(p=>p.SourceId.Length>0?"sub:"+p.SourceId:p.CollectionId)){
            var members=group.ToList();var first=members[0];string key=group.Key;var body=new StackPanel{Spacing=0};
            var header=new Grid{ColumnDefinitions=new("*,Auto"),MinHeight=44,Margin=new(14,1,8,1)};
            var names=new StackPanel();names.Children.Add(new OverflowMarquee(Label(first.CollectionName.Length>0?first.CollectionName:first.Name,17),24));names.Children.Add(new OverflowMarquee(Label(L.Text(first.SourceId.Length>0?"COLLECTION_SUBSCRIPTION":"COLLECTION_MANUAL")+" · "+string.Format(L.Text("PATH_COUNT"),members.Count),12,"Muted"),18));
            var identity=new Grid{ColumnDefinitions=new("Auto,*")};var folder=VectorIcon(Icons.PathFor(first.SourceId.Length>0?"MANAGE_COLLECTIONS":"FOLDER"),26);folder.Margin=new(0,0,10,0);identity.Children.Add(folder);Grid.SetColumn(names,1);identity.Children.Add(names);
            var open=ActionButton("SHOW_PATHS",()=>{Commit(d=>d.Expanded[key]=!d.Expanded.GetValueOrDefault(key));BuildCards();});open.Content=identity;open.HorizontalAlignment=HorizontalAlignment.Stretch;open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.Padding=new(6,0);header.Children.Add(open);
            var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};actions.Children.Add(IconButton("SHOW_PATHS","M4,8 L12,16 L20,8",()=>{Commit(d=>d.Expanded[key]=!d.Expanded.GetValueOrDefault(key));BuildCards();}));actions.Children.Add(IconButton("TEST_COLLECTION",Icons.PathFor("TEST_COLLECTION"),()=>_=TestCollection(members)));actions.Children.Add(IconButton("CONNECT","M12,2 V11 M6,5 A9,9 0 1 0 18,5",()=>_=ConnectCollection(members[0])));
            actions.Children.Add(More(CollectionMenu(members)));Grid.SetColumn(actions,1);header.Children.Add(actions);body.Children.Add(header);
            int verified=members.Count(p=>data.Measurements.TryGetValue(p.Id,out var m)&&m.Verified),failed=members.Count(p=>data.Measurements.TryGetValue(p.Id,out var m)&&m.Attempts>0&&m.Passes==0);var summary=new OverflowMarquee(Label(string.Format(L.Text("MAC_RESULTS_SUMMARY"),verified,failed,Math.Max(0,members.Count-verified-failed)),11,"Muted"),20);summary.Margin=new(18,0,14,4);body.Children.Add(summary);
            if(first.SourceId.Length>0){var quota=new StackPanel{Margin=new(14,0,14,4)};var sub=data.Subscriptions.FirstOrDefault(s=>s.Id==first.SourceId);quota.Children.Add(Label(L.Text("MAC_PROVIDER_USAGE"),11,"Muted"));quota.Children.Add(Label(UsageText(sub?.Usage),12,"Muted",true));var update=ActionButton("UPDATE",()=>_=UpdateSubscription(first.SourceId));update.IsEnabled=!busy;quota.Children.Add(update);body.Children.Add(quota);}
            if(data.Expanded.GetValueOrDefault(key,false)){
                var paths=new StackPanel{Margin=new(16,0,16,12),Spacing=0};foreach(var p in members)paths.Children.Add(BuildPathCard(p));body.Children.Add(paths);
            }
            var card=Card(body,22,new(0,0,0,16));card.Padding=new(0);card.Background=palette.Glass("Collection");cards.Children.Add(card);
        }
    }
    Control BuildPathCard(Profile profile)
    {
        var row=new Grid{ColumnDefinitions=new("*,120,132"),MinHeight=52};
        var texts=new StackPanel{Spacing=0};var title=Label(profile.Name,17);title.FontWeight=FontWeight.SemiBold;title.LineHeight=20;texts.Children.Add(new OverflowMarquee(title,20));
        string address=ProfileEditing.IsNative(profile.Raw)?Importer.Fields(profile.Raw)[3]:Uri.TryCreate(profile.Raw,UriKind.Absolute,out var uri)?uri.Host:"";
        var hostLabel=Label(address,13,"Muted",true);hostLabel.LineHeight=16;texts.Children.Add(new OverflowMarquee(hostLabel,16));
        var protocol=Label(profile.Protocol+(activeId==profile.Id&&state.State=="connected"?" · "+L.Text("CONNECTED"):""),12,activeId==profile.Id?"Green":"Muted",true);protocol.LineHeight=16;texts.Children.Add(new OverflowMarquee(protocol,16));
        var labels=new Grid{ColumnDefinitions=new("Auto,*")};var link=VectorIcon("M10,15 L7,18 A4,4 0 0 1 1,12 L6,7 A4,4 0 0 1 12,7 M14,9 L17,6 A4,4 0 0 1 23,12 L18,17 A4,4 0 0 1 12,17 M8,16 L16,8",24);link.Margin=new(2,0,10,0);labels.Children.Add(link);Grid.SetColumn(texts,1);labels.Children.Add(texts);
        var select=ActionButton("SELECT",()=>{if(data.SelectedId==profile.Id)_=ConnectProfile(profile);else _=Guard(()=>SelectPath(profile));});select.Content=labels;select.Padding=new(2,0);select.Margin=new(0);select.BorderThickness=new(0);select.HorizontalAlignment=HorizontalAlignment.Stretch;select.HorizontalContentAlignment=HorizontalAlignment.Stretch;select.MinHeight=52;row.Children.Add(select);
        bool running=testingProfiles.Contains(profile.Id),hasMeasurement=data.Measurements.TryGetValue(profile.Id,out var m)||running;var measured=new StackPanel{VerticalAlignment=VerticalAlignment.Center,FlowDirection=FlowDirection.LeftToRight,IsVisible=hasMeasurement};if(running)measured.Children.Add(new OverflowMarquee(Label(L.Text("TESTING"),12,"Green"),20));if(m!=null){string tone=freshMeasurements.Contains(profile.Id)?m.Verified||m.Passes>0?"Green":"Red":"StaleMetric";if(!running)measured.Children.Add(new OverflowMarquee(Label(m.Verified||m.Passes>0?(m.Stage=="tcp"?"TCP ":"HTTPS ")+m.ElapsedMs+" ms":L.Text("FAILED"),12,tone,true),20));measured.Children.Add(new OverflowMarquee(Label(m.SampledAt.ToLocalTime().ToString("HH:mm:ss",CultureInfo.InvariantCulture),11,"StaleMetric",true),18));}Grid.SetColumn(measured,1);row.Children.Add(measured);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};actions.Children.Add(IconButton("EDIT",Icons.PathFor("EDIT"),()=>ShowEditor(profile)));actions.Children.Add(More(PathMenu(profile)));actions.Children.Add(IconButton("DELETE",Icons.PathFor("CLEAR_TESTS"),()=>_=DeleteProfiles([profile])));Grid.SetColumn(actions,2);row.Children.Add(actions);
        var card=Card(row,16,new(0,8,0,0),60);card.Name="PathCard";card.Padding=new(8,2,4,2);card.Background=palette.Glass(data.SelectedId==profile.Id?"Selected":"Soft");card.BorderBrush=Tone(activeId==profile.Id&&state.State=="connected"?"Green":data.SelectedId==profile.Id?"Accent":"Outline");card.BorderThickness=new(activeId==profile.Id?2:1);
        void Adapt(double width){row.ColumnDefinitions[1].Width=new(hasMeasurement?Math.Clamp(width*.23,88,120):0);}
        row.SizeChanged+=(_,e)=>Adapt(e.NewSize.Width);Adapt(Math.Max(240,(Compact?Width:Width-260)-92));return card;
    }
    string UsageText(SubscriptionUsage20? u)
    {
        if(u==null)return L.Text("MAC_UNKNOWN");
        long? consumed=u.Upload.HasValue&&u.Download.HasValue?u.Upload.Value+u.Download.Value:null;
        string used=consumed.HasValue?Bytes(consumed.Value):L.Text("MAC_UNKNOWN");
        string total=u.Total==0?L.Text("MAC_UNLIMITED"):u.Total.HasValue?Bytes(u.Total.Value):L.Text("MAC_UNKNOWN");
        string remaining=u.Total==0?L.Text("MAC_UNLIMITED"):u.Total.HasValue&&consumed.HasValue?Bytes(Math.Max(0,u.Total.Value-consumed.Value)):L.Text("MAC_UNKNOWN");
        string expiry=u.Expire==0?L.Text("MAC_UNLIMITED"):u.Expire.HasValue?DateTimeOffset.FromUnixTimeSeconds(u.Expire.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture):L.Text("MAC_UNKNOWN");
        DateTimeOffset estimate=u.ServerDate is {} server?server+(DateTimeOffset.UtcNow-u.ReceivedAt):DateTimeOffset.UtcNow;
        string warning=u.Expire is >0&&DateTimeOffset.FromUnixTimeSeconds(u.Expire.Value)<=estimate?L.Text("MAC_USAGE_EXPIRED"):u.Total is >0&&consumed>=u.Total?L.Text("MAC_USAGE_EXHAUSTED"):"";
        return $"{used} / {total} · {L.Text("MAC_USAGE_REMAINING")} {remaining}\n{expiry} · {u.ReceivedAt.ToLocalTime():yyyy-MM-dd HH:mm}"+(warning.Length>0?"\n"+warning:"");
    }
    void UpdateStatus()
    {
        if(statusText==null)return;
        statusText.Text=L.Text(state.State=="engine-started"?"MAC_ENGINE_STARTED":state.State.ToUpperInvariant());statusText.Foreground=Tone(state.State=="connected"?"Green":"Ink");
        statusDetail.Text=state.State=="connected"?(state.TunActive?L.Text("MAC_TUN_ACTIVE")+" · ":data.SystemProxy&&!data.SystemTun?L.Text("MAC_SYSTEM_PROXY_ACTIVE")+" · ":"")+L.Text(state.Verified?"HTTPS_VERIFIED":"PATH_READY_UNVERIFIED"):L.Text(data.SystemTun?"MAC_TUN_IDLE":data.SystemProxy?"MAC_SYSTEM_PROXY_IDLE":"MAC_LOCAL_PROXY_ONLY");notice.Text=message;notice.IsVisible=message.Length>0;
        heroTitle.Text=statusText.Text;heroDetail.Text=state.State=="connected"?statusDetail.Text:Selected?.Name??L.Text("HOME_CONNECT_HINT");
        heroPower.Background=state.State=="connected"?Tone("PowerSurface"):Tone("Accent");heroPower.Foreground=state.State=="connected"||palette.Dark?Brush.Parse("#1B263D"):Brushes.White;ToolTip.SetTip(heroPower,L.Text(state.State=="connected"?"DISCONNECT":state.State is "connecting" or "engine-started"?"CANCEL":"CONNECT"));
        ButtonSurface(heroPower,heroPower.Background!,heroPower.Foreground!);
        if(!state.TrafficAvailable)statusTraffic.Text="↑ — B/s   ↓ — B/s · "+L.Text("MAC_NO_FAKE_METRICS");
        RefreshTray();
        RefreshAppProxyPages();
    }
    async Task Poll()
    {
        if(polling||busy||automaticMeasuring||client==null||state.State!="connected")return;polling=true;
        var source=client;long revision=connectionRevision;
        try{
            if(DateTimeOffset.UtcNow-lastTick>TimeSpan.FromSeconds(15)){state.Verified=false;message=L.Text("MAC_NETWORK_CHANGED");}lastTick=DateTimeOffset.UtcNow;
            var update=await source.Send(new(){Action="telemetry"});
            if(revision!=connectionRevision||!ReferenceEquals(source,client))return;
            state=update;
            if(state.State=="error")throw new SafeError(state.Error.Length>0?state.Error:"CORE_EXITED");
            if(state.TrafficAvailable){var rates=sampler.Sample(new(state.UploadedBytes,state.DownloadedBytes),Stopwatch.GetTimestamp());statusTraffic.Text=$"↑ {(rates.UploadPerSecond is {} up?Bytes((long)up):"—")}/s   ↓ {(rates.DownloadPerSecond is {} down?Bytes((long)down):"—")}/s    ↑ {Bytes(state.UploadedBytes)}  ↓ {Bytes(state.DownloadedBytes)}";}
            if(data.SleepTimerMinutes>0&&connectedTick>0&&Stopwatch.GetElapsedTime(connectedTick).TotalMinutes>=data.SleepTimerMinutes){await Disconnect();return;}
            if(automaticStop==null&&data.LiveLatency&&IsVisible&&Stopwatch.GetElapsedTime(lastProbeTick).TotalSeconds>30){lastProbeTick=Stopwatch.GetTimestamp();await Probe();}
            UpdateStatus();
        }catch(Exception e){if(revision==connectionRevision&&ReferenceEquals(source,client)){state=new(){State="error"};Display(e);client?.Dispose();client=null;activeId="";UpdateStatus();}}finally{polling=false;}
    }
    async Task ToggleConnect(){if(state.State is "connected" or "connecting" or "engine-started"){await Disconnect();return;}if(Selected==null){var choices=data.Profiles.Where(p=>p.CanConnect).ToArray();if(choices.Length==0){ShowAddHub();return;}var choice=choices[Random.Shared.Next(choices.Length)];Commit(d=>d.SelectedId=choice.Id);}await ConnectCollection(Selected!);}
    async Task ConnectProfileCore(Profile profile,bool automatic,CancellationToken cancellation)
    {
        if(busy)return;if(state.State=="connected"&&activeId==profile.Id){await DisconnectCore();return;}
        await DisconnectCore();connectionRevision++;busy=true;connectingId=profile.Id;state=new(){State="connecting"};message="";UpdateStatus();connectStop=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        try{
            client=new();
            var attempt=client;
            var attemptToken=connectStop.Token;
            client.StageChanged+=stage=>Dispatcher.UIThread.Post(()=>UpdateConnectionStage(attempt,attemptToken,stage));
            var reply=await SendConnect(profile,connectStop.Token);
            if(reply.State=="trust"){
                if(automatic)throw new SafeError("MAC_SCAN_TRUST_FIRST");
                string fingerprint=SshHostKey.Fingerprint(reply.HostKey);
                if(!await Confirm(L.Text("MAC_TRUST_REVIEW")+"\n\n"+SshHostKey.Scope(profile)+"\n"+fingerprint,attemptToken)){client.Dispose();client=null;state=new();return;}
                Commit(d=>d.SshKeys[SshHostKey.Scope(profile)]=SshHostKey.Normalize(reply.HostKey));
                reply=await SendConnect(profile,connectStop.Token);
            }
            state=reply;if(reply.State=="error")throw new SafeError(reply.Error);
            if(reply.State=="connected"){activeId=profile.Id;connectedTick=Stopwatch.GetTimestamp();Commit(d=>{d.SelectedId=profile.Id;d.LastConnected[profile.Id]=DateTimeOffset.UtcNow;if(!automatic)d.PreferredPaths[AutomaticPathPolicy.Collection(profile)]=profile.Id;});}
        }catch(OperationCanceledException){client?.Dispose();client=null;activeId="";state=new();}catch(Exception e){Display(e);client?.Dispose();client=null;state=new(){State="error"};activeId="";}
        finally{connectStop.Dispose();connectStop=null;connectingId="";busy=false;BuildCards();UpdateStatus();}
    }
    void UpdateConnectionStage(AgentClient source,CancellationToken cancellation,AgentReply stage)
    {
        if(exiting||cancellation.IsCancellationRequested||!ReferenceEquals(client,source)||
            state.State is not("connecting" or "engine-started"))return;
        state=stage;
        UpdateStatus();
    }
    async Task<AgentReply> SendConnect(Profile profile,CancellationToken ct)
    {
        await EnsureSlipIdentity(profile,ct);
        var effective=GlobalResolvers.ForConnection(profile,data.GlobalResolverEnabled,data.GlobalResolverList);if(effective.Notice.Length>0){message=L.Text(effective.Notice);SafeDiagnostics.Record("resolver-override",effective.Notice);}var selected=await ApplyDnsPool(effective.Effective,ct);
        byte[] key=data.SystemProxy&&!data.SystemTun?store?.AcquireSessionKey()??[]:[];
        try{return await client!.Send(new(){Action="connect",Profile=selected,SystemProxy=data.SystemProxy&&!data.SystemTun,SystemTun=data.SystemTun,ProxyStoreKey=key,HostKey=data.SshKeys.GetValueOrDefault(SshHostKey.Scope(profile),""),SlipCertificate=StoredSlipCertificate(profile),LocalHttpPort=data.LocalHttpPort,LocalSocksPort=data.LocalSocksPort},ct);}
        finally{CryptographicOperations.ZeroMemory(key);}
    }
    async Task DisconnectCore()
    {
        connectStop?.Cancel();
        if(client!=null){try{if(!busy&&!ReferenceEquals(client,automaticProbeInFlight)){var stopped=await client.Send(new(){Action="stop"});if(stopped.Error.Length>0)message=L.Text(stopped.Error);}}catch{message=L.Text("MAC_PROXY_RESTORE_PENDING");}client.Dispose();client=null;}
        state=new();activeId=connectingId="";connectedTick=0;UpdateStatus();BuildCards();
    }
    async Task Probe()
    {
        if(client==null||state.State!="connected")throw new SafeError("NOT_CONNECTED");
        string measuredId=activeId;freshMeasurements.Clear();testingProfiles.Add(measuredId);BuildCards();AgentReply reply;try{reply=await client.Send(new(){Action="probe",ProbeUrl=data.ProbeUrl,ProbeFallbacks=data.ProbeFallbacks});}finally{testingProfiles.Remove(measuredId);BuildCards();}state=reply;
        if(reply.Verified){Commit(d=>d.Measurements[activeId]=new(true,reply.ElapsedMs,DateTimeOffset.UtcNow,"https"));freshMeasurements.Add(activeId);message=L.Text("HTTPS_VERIFIED")+" · "+reply.ElapsedMs+" ms";}else{SafeDiagnostics.Record("probe",reply.Error);message=L.Text(reply.Error);}
        BuildCards();UpdateStatus();
    }
    void OnLeavingPage(){navigationRevision++;if(route=="DNS_SCANNER"){scanStop?.Cancel();tunnelScanStop?.Cancel();}if(route=="ADD_SUBSCRIPTION")subscriptionPreviewStop?.Cancel();}
    sealed record PageCommit(Button Button);
    void PinCommit(Control page,Button button){
        if(button.Parent is Panel old)old.Children.Remove(button);
        button.Name??="PageConfirm";button.Width=button.Height=button.MinWidth=button.MinHeight=44;button.Padding=new(6);button.Margin=new(2,0);
        // Keep the original command, validation, disabled state and live target label.
        // Only its visual content is a check mark. The title remains in VoiceOver and the tooltip.
        button.ContentTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<string>((_,_)=>{
            var icon=VectorIcon("M3,12 L9,18 L21,5",23);icon.Bind(ShapePath.StrokeProperty,new Avalonia.Data.Binding("IsEnabled"){Source=button,Converter=new Avalonia.Data.Converters.FuncValueConverter<bool,IBrush>(enabled=>Tone(enabled?"OnAccent":"Muted"))});return icon;});
        button.Bind(AutomationProperties.NameProperty,new Avalonia.Data.Binding("Content"){Source=button,StringFormat=L.Text("MAC_CONFIRM_APPLY")+" · {0}"});
        button.Bind(ToolTip.TipProperty,new Avalonia.Data.Binding("Content"){Source=button,StringFormat=L.Text("MAC_CONFIRM_APPLY")+" · {0}"});
        button.HotKey=new Avalonia.Input.KeyGesture(Avalonia.Input.Key.S,Avalonia.Input.KeyModifiers.Meta);
        page.Tag=new PageCommit(button);
    }
    public void Navigate(string key,Control body)
    {
        OnLeavingPage();pages.Push((route,currentBody,homeScroll?.Offset??default));route=key;var scroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Name="PageScroll"};currentBody=scroll;
        if(body is Panel form&&form.Children.OfType<Button>().SingleOrDefault(b=>b.Classes.Contains("page-confirm")) is {} save)PinCommit(scroll,save);
        Build();
    }
    void NavigateFixed(string key,Control body){OnLeavingPage();pages.Push((route,currentBody,homeScroll?.Offset??default));route=key;currentBody=body;Build();}
    void Back(){OnLeavingPage();if(pages.Count==0){GoHome();return;}var prior=pages.Pop();route=prior.Route;currentBody=prior.Body;Build();if(currentBody==null)homeScroll.Offset=prior.Offset;}
    void GoHome(){OnLeavingPage();pages.Clear();currentBody=null;route="HOME";Build();}
    void Commit(Action<AppState> change)
    {
        if(!storeHealthy)throw new SafeError("PRIVATE_STORE_FAILED");
        var next=data.Copy();change(next);store?.Save(next);data=next;
    }
    void Install(AppState next){if(!storeHealthy)throw new SafeError("PRIVATE_STORE_FAILED");store?.Save(next);data=next;}
    void Display(Exception e){string code=e switch{ImportFailure f=>f.Code,SafeError s=>s.Code,OperationCanceledException=>"CANCELLED",_=>"UNEXPECTED_ERROR"};SafeDiagnostics.Error(route,code,e,e is ImportFailure failure?failure.Option:"");RememberDiagnostics();message=L.Text(code)+(e is ImportFailure f2&&f2.Option.Length>0?" · "+f2.Option:code=="UNEXPECTED_ERROR"?" · "+e.GetType().Name:"");if(notice!=null){notice.Text=message;notice.IsVisible=true;}}
    void RememberDiagnostics(){if(fixture||!storeHealthy)return;try{Commit(d=>d.RecentDiagnostics=SafeDiagnostics.Events().ToList());}catch{}}
    async Task RecoverSystemProxy(){if(fixture||!File.Exists(Path.Combine(PrivateStore.Root,"system-proxy","profiles.enc")))return;RequireIdle();byte[] key=store!.AcquireSessionKey();try{using var recovery=new AgentClient();var reply=await recovery.Send(new(){Action="proxy-recover",ProxyStoreKey=key});if(reply.Error.Length>0)throw new SafeError(reply.Error);recoveryPending=false;if(recoveryAction!=null)recoveryAction.IsVisible=false;message=L.Text("MAC_PROXY_RECOVERED");SafeDiagnostics.Record("proxy-recovery","OK");UpdateStatus();}finally{CryptographicOperations.ZeroMemory(key);}}
    Button ActionButton(string key,Action action,bool primary=false)
    {
        var b=new Button{Content=L.Text(key),MinHeight=44,Padding=new(12,8),Margin=new(2),CornerRadius=new(10),Foreground=primary?Tone("OnAccent"):Tone("Ink"),Background=primary?Tone("Accent"):Brushes.Transparent};if(primary)ButtonSurface(b,b.Background!,b.Foreground!);if(primary&&key is ("SAVE" or "CORE_SAVE_PROFILE"))b.Classes.Add("page-confirm");AutomationProperties.SetName(b,L.Text(key));b.Click+=(_,_)=>{try{action();}catch(Exception e){Display(e);}};return b;
    }
    void ButtonSurface(Button button,IBrush background,IBrush foreground){button.Background=background;button.Foreground=foreground;foreach(string state in new[]{"PointerOver","Pressed","Disabled"}){button.Resources["ButtonBackground"+state]=background;button.Resources["ButtonForeground"+state]=foreground;button.Resources["ButtonBorderBrush"+state]=background;}}
    Button NavButton(string key,Action action){var b=ActionButton(key,action);b.MinHeight=52;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.Background=route==key?palette.Glass("Selected"):Brushes.Transparent;var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};row.Children.Add(VectorIcon(Icons.PathFor(key),24));row.Children.Add(Label(L.Text(key),17));b.Content=row;AutomationProperties.SetAutomationId(b,"nav:"+key);return b;}
    Button IconButton(string key,string path,Action action,double size=44){var b=ActionButton(key,action);b.Width=b.Height=size;b.MinHeight=size;b.Padding=new(10);b.Margin=new(0);if(path=="M12,3 V5 M12,11 V13 M12,19 V21")path="M12,3 A1,1 0 1 0 12,5 A1,1 0 1 0 12,3 M12,11 A1,1 0 1 0 12,13 A1,1 0 1 0 12,11 M12,19 A1,1 0 1 0 12,21 A1,1 0 1 0 12,19";var icon=VectorIcon(path,size>64?40:24);icon.Bind(ShapePath.StrokeProperty,new Avalonia.Data.Binding("Foreground"){Source=b});b.Content=icon;return b;}
    ShapePath VectorIcon(string geometry,double size=24,string tone="Ink",bool fill=false)=>new(){Data=Geometry.Parse(geometry),Width=size,Height=size,Stretch=Stretch.Uniform,Stroke=fill?null:Tone(tone),Fill=fill?Tone(tone):null,StrokeThickness=1.8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    TextBlock Label(string text,double size=14,string tone="Ink",bool technical=false)=>new(){Text=text,FontSize=size,Foreground=Tone(tone),Margin=new(2),TextWrapping=TextWrapping.Wrap,FlowDirection=technical?FlowDirection.LeftToRight:L.Direction,FontFamily=technical?new FontFamily("Menlo, Consolas, monospace"):L.Font};
    Border Card(Control child,double radius=18,Thickness? margin=null,double height=double.NaN)=>new(){Child=child,CornerRadius=new(radius),Padding=new(12),Margin=margin??new(0,6),Background=palette.Glass("Card"),BorderBrush=Tone("Outline"),BorderThickness=new(1),Height=height,BoxShadow=BoxShadows.Parse(palette.Dark?"0 6 20 0 #50020812":"0 6 20 0 #30485880")};
    static string Bytes(long n)=>n>=1073741824?(n/1073741824d).ToString("0.0",CultureInfo.InvariantCulture)+" GB":n>=1048576?(n/1048576d).ToString("0.0",CultureInfo.InvariantCulture)+" MB":n>=1024?(n/1024d).ToString("0.0",CultureInfo.InvariantCulture)+" KB":n+" B";
    void OnKey(object? sender,KeyEventArgs e){if(e.Key==Key.Escape){Back();e.Handled=true;}if(e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS()?KeyModifiers.Meta:KeyModifiers.Control)){if(e.Key==Key.O){_=OpenFile();e.Handled=true;}if(e.Key==Key.OemComma){ShowSettings();e.Handled=true;}if(e.Key==Key.Q){_=Exit();e.Handled=true;}if(e.Key==Key.N){ShowAddHub();e.Handled=true;}}}
    void InstallNativeMenu()
    {
        // Avalonia.Native 12.1.6 binds each exporter to one NativeMenu identity.
        // Replacing an exported root/submenu throws in IAvnMenu.Update. Keep the
        // complete graph alive and update its properties, including language.
        void Add(NativeMenu menu,Dictionary<string,NativeMenuItem> items,string key,Func<Task> action,string? gesture=null){var item=new NativeMenuItem{Header=L.Text(key),Command=new NativeAction(action,Display)};if(gesture!=null)item.Gesture=KeyGesture.Parse(gesture);items.Add(key,item);menu.Items.Add(item);}
        Task Run(Action action){action();return Task.CompletedTask;}
        if(appNativeMenu==null&&Application.Current is {} application){
            appNativeMenu=NativeMenu.GetMenu(application);
            if(appNativeMenu==null){appNativeMenu=new();NativeMenu.SetMenu(application,appNativeMenu);}else appNativeMenu.Items.Clear();
            Add(appNativeMenu,appNativeItems,"ABOUT",()=>Run(ShowAbout));Add(appNativeMenu,appNativeItems,"SETTINGS",()=>Run(ShowSettings),"Meta+OemComma");Add(appNativeMenu,appNativeItems,"MAC_HIDE",()=>Run(Hide),"Meta+H");Add(appNativeMenu,appNativeItems,"EXIT",Exit,"Meta+Q");
        }
        if(windowNativeMenu==null){
            windowNativeMenu=new();fileNativeMenu=new();fileNativeHeading=new(){Menu=fileNativeMenu};windowNativeMenu.Items.Add(fileNativeHeading);
            Add(fileNativeMenu,fileNativeItems,"IMPORT",OpenFile,"Meta+O");Add(fileNativeMenu,fileNativeItems,"ADD_MANUAL",()=>Run(ShowManual),"Meta+N");Add(fileNativeMenu,fileNativeItems,"BACKUP_EXPORT",()=>Run(ShowBackup));NativeMenu.SetMenu(this,windowNativeMenu);
        }
        foreach(var (key,item) in appNativeItems.Concat(fileNativeItems))item.Header=L.Text(key);
        fileNativeHeading!.Header=L.Text("ADD_LINK_FILE");
    }
    void InstallTray()
    {
        if(tray!=null)return;
        try{
            tray=new TrayIcon{Menu=TrayMenu(),Icon=MenuBarMark.Icon(state.State),ToolTipText="Rovunet"};
            MacOSProperties.SetIsTemplateIcon(tray,false);
            tray.IsVisible=true;
            TrayIcon.SetIcons(Application.Current!,new TrayIcons{tray});
            trayLanguage=trayState="";
            RefreshTray();
        }catch(Exception e){tray?.Dispose();tray=null;SafeDiagnostics.Error("tray","MAC_TRAY_FAILED",e);}
    }
    void RefreshTray()
    {
        if(tray==null||trayLanguage==data.Language&&trayState==state.State)return;
        if(trayState!=state.State)tray.Icon=MenuBarMark.Icon(state.State);
        trayLanguage=data.Language;
        trayState=state.State;
        string statusKey=state.State=="engine-started"?"MAC_ENGINE_STARTED":state.State.ToUpperInvariant();
        tray.ToolTipText="Rovunet · "+L.Text(statusKey);
        _=TrayMenu();
    }
    void Reveal(){if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Show();Activate();}
    NativeMenu TrayMenu(){
        if(trayNativeMenu==null){trayNativeMenu=new();void Add(string key,Func<Task> action){var item=new NativeMenuItem{Command=new NativeAction(action,Display)};trayNativeItems.Add(key,item);trayNativeMenu.Items.Add(item);}Add("MAC_SHOW_APP",()=>{Reveal();return Task.CompletedTask;});Add("CONNECT",ToggleConnect);Add("SETTINGS",()=>{Reveal();ShowSettings();return Task.CompletedTask;});Add("EXIT",Exit);}
        foreach(var (key,item) in trayNativeItems)item.Header=L.Text(key=="CONNECT"?(state.State=="connected"?"DISCONNECT":state.State is "connecting" or "engine-started"?"CANCEL":"CONNECT"):key);
        return trayNativeMenu;
    }
    async Task Exit(){if(exiting)return;SaveWindowSize();exiting=true;scanStop?.Cancel();tunnelScanStop?.Cancel();collectionStop?.Cancel();if(storeHealthy)await Disconnect();timer.Stop();tray?.Dispose();Close();(Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();}
    async Task<bool> Confirm(string text,CancellationToken cancellation=default)
    {
        var dialog=new Window{Title="Rovunet",Width=480,Height=300,MinHeight=200,Background=Tone("Card"),Foreground=Tone("Ink"),FontFamily=L.Font,WindowStartupLocation=WindowStartupLocation.CenterOwner};var body=new StackPanel{Margin=new(22),Spacing=16};body.Children.Add(Label(text,14));var actions=new StackPanel{Orientation=Orientation.Horizontal};var yes=ActionButton("CONFIRM",()=>dialog.Close(true),true);var no=ActionButton("CANCEL",()=>dialog.Close(false));actions.Children.Add(yes);actions.Children.Add(no);body.Children.Add(actions);dialog.Content=body;cancellation.ThrowIfCancellationRequested();
        var answer=dialog.ShowDialog<bool>(this);
        using var registration=cancellation.Register(()=>Dispatcher.UIThread.Post(()=>dialog.Close(false)));
        bool accepted=await answer;cancellation.ThrowIfCancellationRequested();return accepted;
    }
}
