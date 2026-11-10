using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    string ProbeResultText(AgentReply reply)=>reply.Verified?L.Text(reply.ProbeFallbackUsed?"TEST_OK_FALLBACK":"TEST_OK"):reply.Error.Length==0?L.Text("PATH_READY_UNVERIFIED"):reply.Error=="SSH_TRUST_REQUIRED"?L.Text("TEST_IDENTITY_PENDING"):HttpsProbePolicy.Unconfirmed(reply.Error)?L.Text("TEST_UNCONFIRMED")+" · "+L.Text(reply.Error):L.Text(reply.Error.Length>0?reply.Error:"PATH_READY_UNVERIFIED");
    TrayIcon? tray;
    TaskCompletionSource? operationDone;
    bool connectCancelled, reportingError;
    Profile? activeProfile;
    DateTimeOffset connectedSince;
    readonly DispatcherTimer durationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    TextBlock globalStatus = null!, durationValue = null!, pathValue = null!, protocolValue = null!, lastTestValue = null!, notice = null!;
    TextBlock httpAddress = null!, socksAddress = null!;
    Button copyHttp = null!, copySocks = null!, verify = null!;
    ProgressBar connectingProgress = null!;
    ScrollViewer mainScroll = null!;
    TextBox search = null!;
    string cardsSignature = "";
    readonly List<RowView> rows = [];
    readonly List<GroupView> groups = [];
    readonly Queue<string> safeEvents = [];
    sealed class RowView
    {
        public required Profile Profile;
        public required Border Tile;
        public required RadioButton Select;
        public required MenuItem Connect;
        public required TextBlock Result;
        public required TextBlock Timing;
        public required TextBlock Protocol;
        public required MenuItem Edit;
        public required MenuItem Delete;
    }
    sealed class GroupView
    {
        public required string Key;
        public required List<Profile> Members;
        public required Border Card;
        public required StackPanel Paths;
        public required Button Toggle;
        public required Button Connect;
        public required Border Header;
        public required TextBlock Summary;
        public required TextBlock Account;
        public AccountQuotaTile? Quota;
        public required ContextMenu Menu;
        public required Button Test;
        public required Button Stop;
        public required Button More;
        public required ProgressBar Progress;
    }
    Border Card(UIElement child, Thickness? margin = null) => new ElevatedBorder() { Child = child, Background = GlassSurface("Card"), CornerRadius = new(18), Padding = new(12), Margin = margin ?? new(0, 0, 0, 14), BorderBrush = GlassEdge(Tone("Outline")), BorderThickness = new(1) };
    static TextBlock Technical(TextBlock text) { text.FlowDirection = FlowDirection.LeftToRight; text.FontFamily = new("Consolas, Segoe UI");text.FontWeight=FontWeights.SemiBold; return text; }
    System.Windows.Shapes.Path shieldOutline = null!, shieldMark = null!;
    TextBlock trafficUp = null!, trafficDown = null!, trafficTotal = null!, trafficRecorded = null!, latencyAge = null!;
    System.Windows.Shapes.Polyline trafficLine = null!;
    Border BuildShield(double size, bool connection = false)
    {
        var frame=new System.Windows.Shapes.Path{Data=(Geometry)Application.Current.FindResource("Shield_disconnected"),Fill=Accent};
        var mark=new System.Windows.Shapes.Path{Data=(Geometry)Application.Current.FindResource("ShieldCheck"),Stroke=Accent,StrokeThickness=2.1,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Visibility=Visibility.Collapsed};
        if(connection){shieldOutline=frame;shieldMark=mark;}
        var grid=new Grid{Width=24,Height=24};grid.Children.Add(frame);grid.Children.Add(mark);
        return new Border{Width=size,Height=size,CornerRadius=new(13),Background=Tone("Selected"),Padding=new(8),Child=new Viewbox{Child=grid},FlowDirection=FlowDirection.LeftToRight};
    }
    Button IconButton(string key, string geometry, Action action)
    {
        var b = Button(key, action); b.Template=(ControlTemplate)Application.Current.FindResource("IconButtonTemplate"); b.ToolTip = L.Text(key); b.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse(geometry), Stroke = Tone("Ink"), StrokeThickness = 1.6, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Width = 17, Height = 17, Stretch = Stretch.Uniform }; b.Padding = new(7); b.MinHeight = 44; return b;
    }
    bool darkTheme;
    bool compactLayout;
    bool IsAmoled => data.Theme == "amoled";
    bool EffectiveDark()
    {
        if(data.Theme!="system")return data.Theme is "dark" or "amoled";
        try{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is 0;}catch{return false;}
    }
    void SetTheme(string mode)
    {
        string old=data.Theme;data.Theme=mode;try{Save();}catch{data.Theme=old;throw;}ApplyTheme();Build();
    }
    void ToggleTheme()=>SetTheme(darkTheme?"light":"dark");
    void OnPreferenceChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs args)
    {
        if(data.Theme!="system" && args.Category!=Microsoft.Win32.UserPreferenceCategory.Accessibility)return;
        Dispatcher.BeginInvoke(()=>{if(!closing && IsLoaded){ApplyTheme();Build();}});
    }
    void SetState(AgentReply value)
    {
        state = value; RefreshLocalProxyPanels19(); if (connectionText == null) return; UpdateSleepTimer();
        if(addButton!=null)addButton.IsEnabled=!busy&&!closing;
        connectionText.Text = L.Text(value.State.ToUpperInvariant()); globalStatus.Text = connectionText.Text;
        Brush color = value.State == "connected" ? Green : value.State == "error" ? Red : value.State is "connecting" or "disconnecting" ? Accent : Muted;
        var display=value.State is "connected" or "connecting" or "disconnecting" or "error"?value.State:"disconnected";
        shieldOutline.Data=(Geometry)Application.Current.FindResource("Shield_"+display);shieldOutline.Fill=color;shieldMark.Stroke=color;shieldMark.Visibility=value.State=="connected"?Visibility.Visible:Visibility.Collapsed;
        detail.Text = L.Text(value.State switch { "connected" => value.Verified ? "HTTPS_VERIFIED" : "PATH_READY_UNVERIFIED", "connecting" => "CONNECTING_HINT", "disconnecting" => "DISCONNECTING_HINT", "error" => "ERROR_HINT", _ => "SELECT_PATH" });
        if (value.State == "connected") detail.Text = (activeProfile ?? Selected)?.Name + " · " + (activeProfile ?? Selected)?.Protocol;
        connectionText.Foreground = color;
        string key = value.State == "connected" ? "DISCONNECT" : value.State == "connecting" ? "CANCEL" : "CONNECT";
        if (connect != null) { AutomationProperties.SetName(connect, L.Text(key)); connect.IsEnabled = !closing && (value.State is "connecting" or "connected" || !busy); }
        if (verify != null) verify.IsEnabled = !busy && value.State == "connected";
        RefreshHero();
        proxy.IsEnabled = !busy && value.State is not ("connected" or "connecting" or "disconnecting");
        connectingProgress.Visibility = value.State is "connecting" or "disconnecting" ? Visibility.Visible : Visibility.Collapsed;
        httpAddress.Text = value.State == "connected" && value.HttpPort > 0 ? "127.0.0.1:" + value.HttpPort.ToString(CultureInfo.InvariantCulture) : "—";
        socksAddress.Text = value.State == "connected" && value.SocksPort > 0 ? "127.0.0.1:" + value.SocksPort.ToString(CultureInfo.InvariantCulture) : "—";
        copyHttp.IsEnabled = value.State == "connected" && value.HttpPort > 0; copySocks.IsEnabled = value.State == "connected" && value.SocksPort > 0;
        pathValue.Text = value.State == "connected" ? activeProfile?.Name ?? Selected?.Name ?? "—" : "—"; pathValue.ToolTip = pathValue.Text;
        protocolValue.Text = value.State == "connected" ? activeProfile?.Protocol ?? Selected?.Protocol ?? "—" : "—";
        lastTestValue.Text = value.State == "connected" && value.Verified ? value.ElapsedMs.ToString(CultureInfo.InvariantCulture) + " ms" : "—";
        if (value.State == "connected") { if (connectedSince == default) connectedSince = DateTimeOffset.UtcNow;BeginTrafficSampling(); durationTimer.Start(); } else durationTimer.Stop(); RefreshDuration();
        string code = value.Error.Length > 0 ? value.Error : lastError;
        errorText.Visibility = code.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        errorText.Foreground=HttpsProbePolicy.Unconfirmed(code)?Amber:Red;
        errorText.Text = code.Length > 0 ? (HttpsProbePolicy.Unconfirmed(code)?L.Text("TEST_UNCONFIRMED")+" · ":"")+L.Text(code) + "\n" + ErrorHelp(code) : "";
        if (code.Length > 0 && (safeEvents.Count == 0 || !safeEvents.Last().EndsWith(" " + code, StringComparison.Ordinal))) { safeEvents.Enqueue(DateTimeOffset.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + code); while (safeEvents.Count > 3) safeEvents.Dequeue(); }
        if(value.State!="connected")rates=new(null,null);RefreshTrafficText();
        tray?.Update(value.State=="connected"?"connected":value.State=="error"||value.State=="disconnected"&&code.Length>0&&code!="CANCELLED"?"error":value.State,CanConnectFromTray20(),busy);
        RefreshPowerButton(); RefreshCards();
    }
    void RefreshDuration() { if (durationValue != null) durationValue.Text = state.State == "connected" ? (DateTimeOffset.UtcNow - connectedSince).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "—"; }
    void RefreshCards()
    {
        RefreshHero(); 
        if (cards == null) return; string query = search?.Text.Trim() ?? "";
        foreach (var row in rows)
        {
            var p = row.Profile; bool selected = p.Id == data.SelectedId; row.Select.IsChecked = selected; row.Select.IsEnabled = !busy;
            bool activePath=state.State=="connected"&&activeProfile?.Id==p.Id;
            row.Tile.Background = GlassSurface(selected||activePath ? "Selected" : "Soft"); row.Tile.BorderBrush = GlassEdge(activePath?Green:Tone(selected?"Accent":"Outline"));
            row.Protocol.Text = p.Protocol + (activePath ? " · " + L.Text("CONNECTED") : ""); row.Protocol.Foreground=activePath?Green:Muted;
            bool connectedPath=state.State=="connected"&&activeProfile?.Id==p.Id;row.Connect.Header=L.Text(connectedPath?"DISCONNECT":"CONNECT");row.Connect.IsEnabled = !busy&&(connectedPath||p.CanConnect); row.Edit.IsEnabled = !busy; row.Delete.IsEnabled = !busy && state.State != "connected";
            bool visible = query.Length == 0 || p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || p.Protocol.Contains(query, StringComparison.OrdinalIgnoreCase) || p.CollectionName.Contains(query, StringComparison.CurrentCultureIgnoreCase); row.Tile.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            bool has = measurements.TryGetValue(p.Id, out var measured); row.Result.Visibility = row.Timing.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            if (has)
            {
                row.Result.Text = ProbeResultText(measured!);
                bool fresh = data.Measurements.TryGetValue(p.Id, out var saved) && liveMeasurements.Contains(p.Id)&&LatestRound(p,saved)&&UiPolicy.MeasurementFresh(saved.SampledAt, DateTimeOffset.UtcNow);row.Result.Foreground=fresh?measured!.Verified?Green:HttpsProbePolicy.Unconfirmed(measured.Error)?Amber:Red:Muted;row.Timing.Foreground=fresh&&measured!.Verified?Green:Muted;if(saved?.Attempts>1)row.Result.Text+=" · "+saved.Passes+"/"+saved.Attempts;if(!fresh)row.Result.Text+=" · "+L.Text("RESULT_STALE");row.Result.ToolTip=row.Result.Text; row.Timing.Text = measured!.Verified ? measured.ElapsedMs.ToString(CultureInfo.InvariantCulture) + " ms" : "—"; row.Timing.ToolTip = saved?.SampledAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            }
            RenderCollectionRow(row);
        }
        foreach (var group in groups)
        {
            bool open = data.ExpandedCollections.GetValueOrDefault(group.Key, false) || query.Length > 0; bool reveal=open&&group.Paths.Visibility!=Visibility.Visible;group.Paths.Visibility = open ? Visibility.Visible : Visibility.Collapsed;if(reveal)Reveal(group.Paths); group.Toggle.Content = new System.Windows.Shapes.Path {Data=Geometry.Parse(open?"M4,16 L12,8 L20,16":"M4,8 L12,16 L20,8"),Stroke=Tone("Ink"),StrokeThickness=1.7,Width=16,Height=10,Stretch=Stretch.Uniform}; group.Toggle.ToolTip=L.Text(open?"HIDE_PATHS":"SHOW_PATHS"); group.Connect.IsEnabled = group.Members.Any(p=>p.Id==data.SelectedId) && state.State is "connected" or "connecting" || !busy && group.Members.Any(p => p.CanConnect);
            bool selectedCollection=group.Members.Any(p=>p.Id==data.SelectedId);bool activeCollection=group.Members.Any(p=>p.Id==activeProfile?.Id);group.Header.BorderBrush=group.Card.BorderBrush=GlassEdge(activeCollection&&state.State=="connected"?Green:selectedCollection?Accent:Tone("Outline")); if(group.Connect.Content is System.Windows.Shapes.Path icon)icon.Stroke=activeCollection&&state.State=="connected"?Green:Tone("Ink"); string action=activeCollection&&state.State=="connecting"?"CANCEL":activeCollection&&state.State=="connected"?"DISCONNECT":"CONNECT";group.Connect.ToolTip=L.Text(action);AutomationProperties.SetName(group.Connect,L.Text(action));
            var results=group.Members.Select(p=>(p,m:data.Measurements.GetValueOrDefault(p.Id))).Where(x=>x.m!=null&&LatestRound(x.p,x.m)).Select(x=>x.m!).ToArray();int passed=results.Count(m=>m.Verified),unknown=results.Count(m=>!m.Verified&&HttpsProbePolicy.Unconfirmed(m.Error));group.Summary.Text=string.Format(CultureInfo.CurrentCulture,L.Text("COLLECTION_RESULT_COUNTS"),passed,results.Length-passed-unknown,group.Members.Count-results.Length)+(unknown>0?" · "+string.Format(L.Text("GROUP_UNCONFIRMED_COUNT"),unknown):"");group.Summary.Visibility=results.Length>0?Visibility.Visible:Visibility.Collapsed;
            RefreshCollectionMenu(group.Menu,group.Members);OrderPinnedPaths(group);
            UpdateAccountQuota(group);
            group.Card.Visibility = query.Length == 0 || rows.Any(r => group.Members.Contains(r.Profile) && r.Tile.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            RenderCollectionProgress(group);
        }
        statusExpiry.Stop(); var pending = statuses.Values.Select(s => 901 - Stopwatch.GetElapsedTime(s.ReceivedTick).TotalSeconds).Where(n => n > 0).ToList(); if (data.Measurements.Values.Any(m => UiPolicy.MeasurementFresh(m.SampledAt, DateTimeOffset.UtcNow))) pending.AddRange(data.Measurements.Values.Select(m => 901 - (DateTimeOffset.UtcNow - m.SampledAt).TotalSeconds).Where(n => n > 0)); if (pending.Count > 0) { statusExpiry.Interval = TimeSpan.FromSeconds(pending.Min()); statusExpiry.Start(); }
    }
    Button MoreButton(ContextMenu menu)
    {
        var button = Button("MORE", () => { }); var dots = new StackPanel { Orientation = Orientation.Vertical, Margin = new(5) };
        for (int i = 0; i < 3; i++) dots.Children.Add(new System.Windows.Shapes.Ellipse { Width = 3, Height = 3, Fill = Muted, Margin = new(1) });
        button.Template=(ControlTemplate)Application.Current.FindResource("IconButtonTemplate");button.Content = dots; button.ToolTip = L.Text("MORE"); button.ContextMenu = menu; button.Click += (_, _) => { menu.PlacementTarget = button; menu.IsOpen = true; }; return button;
    }
    void SwitchPath(Profile p)
    {
        if (busy || !SupportPolicy.Allowed(p, data.IncludeExperimental)) return;
        _ = Run(async () => { if (client != null) await Disconnect(); data.SelectedId = p.Id; Save(); await Connect(); });
    }
    void Notify(string text) { if (notice != null) { notice.Text = text; notice.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible; } }
    string ErrorHelp(string code) => L.Text(code switch { "SLIP_CERT_REQUIRED" or "SLIP_CERT_INVALID" => "ACTION_CERT", "SSH_TRUST_REQUIRED" or "SSH_KEY_CHANGED" => "SSH_TRUST_HINT", "PROXY_RECOVERY_CONFLICT" or "RECOVERY_REQUIRED" or "PROXY_RECOVERY_FAILED" => "ACTION_RECOVER", "INVALID_IMPORT" => "IMPORT_HINT", _ => "ACTION_CONNECTION" });
    void SaveWindowSize() { if (WindowState == WindowState.Normal) { data.WindowWidth = ActualWidth; data.WindowHeight = ActualHeight; } try { Save(); } catch { lastError = "SAVE_FAILED"; } }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    public void ShowExisting() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle); }
    void RequestDisconnect()
    {
        poolStop?.Cancel();
        if (busy) { timerDisconnectClient = client; connectCancelled = true; testStop?.Cancel(); testing?.Abort(); client?.Abort(); }
        else _ = Run(Disconnect);
    }
    public async Task ExitApplication()
    {
        if (closing) return; closing = true; ShowExisting(); SaveWindowSize(); poolStop?.Cancel(); testStop?.Cancel(); testing?.Abort();
        if(!await CloseNavigation()){closing=false;return;}
        if (busy) { connectCancelled = true; client?.Abort(); if (operationDone != null) { try { await operationDone.Task.WaitAsync(TimeSpan.FromSeconds(35)); } catch { closing = false; lastError = "RECOVERY_REQUIRED"; SetState(state); return; } } }
        SetState(new() { State = "disconnecting" });
        try { await Disconnect(); allowClose = true; Close(); }
        catch { closing = false; lastError = "RECOVERY_REQUIRED"; SetState(new() { State = "error", Error = lastError }); }
    }
    public async Task RecoverUiError()
    {
        if (reportingError) return; reportingError = true; testStop?.Cancel(); testing?.Abort(); client?.Abort();
        try { if (operationDone != null && !operationDone.Task.IsCompleted) await operationDone.Task.WaitAsync(TimeSpan.FromSeconds(35)); await Disconnect(); }
        catch { lastError = "RECOVERY_REQUIRED"; }
        finally { lastError = lastError.Length > 0 ? lastError : "UNEXPECTED_ERROR"; SetState(new() { State = "error", Error = lastError }); Info("ERROR", L.Text(lastError)); reportingError = false; }
    }
    void ApplyTheme()
    {
        if(!Application.Current.Resources.Contains("DesktopChromeLoaded")){
            var chrome=new ResourceDictionary{Source=new Uri("/Rovunet;component/DesktopStyles.xaml",UriKind.Relative)};
            foreach(System.Collections.DictionaryEntry entry in chrome)Application.Current.Resources[entry.Key]=entry.Value;
            Application.Current.Resources["DesktopChromeLoaded"]=true;
        }
        if(!Application.Current.Resources.Contains("Appearance18Loaded")){
            var skin=new ResourceDictionary{Source=new Uri("/Rovunet;component/Appearance18.xaml",UriKind.Relative)};
            foreach(System.Collections.DictionaryEntry entry in skin)Application.Current.Resources[entry.Key]=entry.Value;
            var raised=new Style(typeof(Button),(Style)Application.Current.FindResource(typeof(Button)));raised.Setters.Add(new Setter(Control.TemplateProperty,Application.Current.FindResource("RaisedButtonTemplate")));Application.Current.Resources[typeof(Button)]=raised;
            Application.Current.Resources["Appearance18Loaded"]=true;
        }
        glassSurfaces.Clear();
        bool dark = darkTheme = EffectiveDark();
        var palette = dark ? new Dictionary<string, string> { { "Bg", "#0B1020" }, { "Card", "#11182B" }, { "Soft", "#172137" }, { "Sidebar", "#11182B" }, { "Ink", "#F1F3FC" }, { "Muted", "#C1CADD" }, { "Accent", "#C4B5FD" }, { "Outline", "#45536D" }, { "Selected", "#30264C" }, { "Green", "#73D7AB" }, { "Red", "#FF9BAB" }, { "Amber", "#F3CF7D" } } : new Dictionary<string, string> { { "Bg", "#F3F5FC" }, { "Card", "#FFFFFF" }, { "Soft", "#FAFBFF" }, { "Sidebar", "#F9FAFF" }, { "Ink", "#19233A" }, { "Muted", "#46526A" }, { "Accent", "#5C3DB8" }, { "Outline", "#BCC6D9" }, { "Selected", "#E9E0FF" }, { "Green", "#087849" }, { "Red", "#AD2645" }, { "Amber", "#885706" } };
        palette["PowerSurface"] = dark ? "#43864A" : "#087849"; palette["Collection"] = dark ? "#49395D" : "#E9E0FF"; palette["StatusSurface"] = dark ? "#2B2D43" : "#E8E5F3";
        if(dark){palette["Bg"]="#1B263D";palette["Card"]="#172137";palette["Soft"]="#1D293E";palette["Sidebar"]="#172137";palette["Accent"]="#BFB1F0";palette["Selected"]="#332C4E";palette["Collection"]="#322B4A";palette["StatusSurface"]="#1B1E2B";palette["Outline"]="#52617C";}
        else{palette["Bg"]="#F3F4FC";palette["Soft"]="#F7F6FC";palette["Sidebar"]="#F0EFF8";palette["Selected"]="#E7DFF6";palette["Collection"]="#E7E0F5";palette["StatusSurface"]="#EAE7F2";palette["Accent"]="#6345AC";palette["Outline"]="#BAC2D5";}
        if (IsAmoled) { palette["Bg"]="#111927"; palette["Card"]="#0B0D14"; palette["Soft"]="#11141B"; palette["Sidebar"]="#0B0D14"; palette["Selected"]="#2A2340"; palette["Collection"]="#2A2340"; palette["StatusSurface"]="#15151B"; palette["Outline"]="#343D52"; }
        if(dark){palette["Card"]=IsAmoled?"#11192C":"#1B2740";palette["Soft"]=IsAmoled?"#26314C":"#33415F";palette["Sidebar"]=IsAmoled?"#0D1426":"#19243C";palette["Collection"]=IsAmoled?"#504366":"#62517C";palette["Selected"]=IsAmoled?"#655181":"#746191";palette["StatusSurface"]=IsAmoled?"#252D47":"#303B57";palette["Outline"]="#726B91";palette["Accent"]="#CEBBFF";palette["Muted"]="#CBD0E4";palette["Green"]="#7DE0B9";}
        palette["ScanCyan"]=dark?"#83D4DD":"#146A78";
        foreach (var (key, color) in palette) { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); Application.Current.Resources[key] = brush; }
        if (SystemParameters.HighContrast) foreach (var (key, brush) in new (string, Brush)[] { ("Bg", SystemColors.WindowBrush), ("Card", SystemColors.WindowBrush), ("Soft", SystemColors.WindowBrush), ("Sidebar", SystemColors.WindowBrush), ("Ink", SystemColors.WindowTextBrush), ("Muted", SystemColors.WindowTextBrush), ("Accent", SystemColors.HighlightBrush), ("Selected", SystemColors.WindowBrush), ("Outline", SystemColors.WindowTextBrush), ("Green", SystemColors.WindowTextBrush), ("Red", SystemColors.WindowTextBrush), ("Amber", SystemColors.WindowTextBrush) }) Application.Current.Resources[key] = brush;
        if(SystemParameters.HighContrast)Application.Current.Resources["ScanCyan"]=SystemColors.WindowTextBrush;
        Application.Current.Resources["PageBg"]=HomeBackdrop();
    }
}

