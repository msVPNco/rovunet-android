using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow : Window
{
    readonly UserData data;
    readonly bool fixtureMode;
    readonly Dictionary<string, StatusSnapshot> statuses = [];
    readonly Dictionary<string, string> statusBodies = [];

    readonly System.Windows.Threading.DispatcherTimer statusExpiry = new();
    AgentClient? client; AgentReply state = new();
    StackPanel cards = null!; TextBlock connectionText = null!, detail = null!, errorText = null!; Button connect = null!;
    Border shield = null!; CheckBox proxy = null!; bool busy, closing, allowClose;
    AgentClient? lostClient;
    string lastError = ""; long lastConnectMs, lastDisconnectMs;
    public static string Version => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0-preview.20";
    static Brush Tone(string key) => (Brush)Application.Current.FindResource(key);
    static Brush Muted => Tone("Muted");
    static Brush Accent => Tone("Accent");
    static Brush Green => Tone("Green");
    static Brush Red => Tone("Red");
    static Brush Amber => Tone("Amber");
    public MainWindow(UserData data, bool uiSmoke = false)
    {
        
        this.data = data; fixtureMode=uiSmoke; Title = uiSmoke ? "Rovunet — UI fixture" : "Rovunet"; allowClose = uiSmoke;
        foreach(var p in data.Profiles)if(SshHostKey.Required(p)&&data.SshKeys.ContainsKey(p.Id)&&!data.SshKeyScopes.ContainsKey(p.Id))data.SshKeyScopes[p.Id]=SshHostKey.Scope(p);
        Style = (Style)Application.Current.FindResource(typeof(Window));
        
        var area = SystemParameters.WorkArea;
        
        Width = UiPolicy.WindowSize(data.WindowWidth > 0 ? data.WindowWidth : 1200, area.Width, 580);
        Height = UiPolicy.WindowSize(Math.Max(data.WindowHeight,900), area.Height, 880);
        MinWidth = Math.Min(580, Width); MinHeight = Math.Min(880, Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        foreach(var sub in data.Subscriptions){try{if(sub.Usage!=null){subscriptionUsage20[sub.Id]=sub.Usage;statuses[sub.Id]=sub.Usage.Snapshot(sub.Id);}else if(sub.StatusBody.Length>0)statuses[sub.Id]=new(){Status=AccountStatus.Read(sub.StatusBody),Error="ACCOUNT_STALE"};}catch{}}
        ApplyTheme();Build();
        SizeChanged += (_, args) => { if(args.WidthChanged&&!closing){if(NavigationDepth>0)ResizeInnerPage(args.NewSize.Width);else if((args.NewSize.Width<ContextThreshold)!=compactLayout)RebuildForResize();} };
        foreach (var (id, m) in data.Measurements) measurements[id] = new() { Verified = m.Verified, ElapsedMs = m.ElapsedMs, Error = m.Error, ProbeStage=m.Stage,ProbeFallbackUsed=m.ProbeFallbackUsed };
        RefreshCards();
        Loaded += async (_, _) =>
        {
            if (uiSmoke) return;
            try { tray = new TrayIcon(ShowExisting, ToggleTrayConnection, () => _ = ExitApplication()); }
            catch { data.CloseToTray = false; lastError = "TRAY_UNAVAILABLE"; }
            await Run(async () => await AgentClient.Recover());usageRefresh20.Tick+=async(_,_)=>await RefreshAllUsage20();usageRefresh20.Start();_ =RefreshAllUsage20();
        };
        Closing += (_, e) =>
        {
            if(!closing&&NavigationDepth>0){e.Cancel=true;pageStack[^1].Close();return;}
            if (allowClose) return;
            e.Cancel = true;
            if (data.CloseToTray && tray != null && !closing) { SaveWindowSize(); Hide(); tray.HiddenNotice(); }
            else _ = ExitApplication();
        };
        Closed += (_, _) => { usageRefresh20.Stop();usageLifetime20.Cancel();statusExpiry.Stop(); durationTimer.Stop(); CancelSleepTimer(); tray?.Dispose(); SystemEvents.PowerModeChanged -= OnPower;SystemEvents.UserPreferenceChanged-=OnPreferenceChanged; };
        if (!uiSmoke) {SystemEvents.PowerModeChanged += OnPower; SystemEvents.UserPreferenceChanged+=OnPreferenceChanged;}
        statusExpiry.Tick += (_, _) => { statusExpiry.Stop(); RefreshCards(); };
        sleepTicker.Tick += (_, _) => TickSleepTimer();
        durationTimer.Tick += async (_, _) => { RefreshDuration(); RefreshTrafficText(); await PollTelemetry(); };
        // The counter clock belongs to the connection, including browser/tray use.
        Activated += (_, _) => { if (state.State == "connected") { durationTimer.Start(); RefreshTrafficText(); } };
        SourceInitialized += (_, _) => UpdateTitleTheme();
        PreviewKeyDown += (_, e) =>
        {
            if(NavigationDepth>0)return;
            if(e.Key==Key.Escape&&currentCollection is {Running:true}){CancelCollectionTest();e.Handled=true;return;}
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { ShowSearch(); e.Handled=true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemComma) { SettingsDialog(); e.Handled = true; }
            if (e.Key == Key.F5) { ProbeDialog(); e.Handled = true; }
            if (e.Key == Key.F1) { Help(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.I or Key.N) { ImportDialog(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D) { RequestDisconnect(); e.Handled = true; }
        };
    }
    void OnPower(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Suspend) Dispatcher.Invoke(() => { _ = SuspendConnection(); }); }
    async Task SuspendConnection()
    {
        if (closing) return;
        timerDisconnectClient = client; // Intentional stop also covers OS suspend.
        RequestDisconnect();
        try
        {
            if (operationDone != null && !operationDone.Task.IsCompleted) await operationDone.Task.WaitAsync(TimeSpan.FromSeconds(35));
            if (!closing && (client != null || state.State != "disconnected")) await Run(Disconnect);
        }
        catch { lastError = "RECOVERY_REQUIRED"; SetState(new() { State = "error", Error = lastError }); }
    }
    TextBlock Text(string value, double size = 14, Brush? brush = null) => new() { Text = value, FontSize = Typography.Size(size),FontWeight=Typography.Body, Foreground = brush ?? (Brush)FindResource("Ink"), Margin = new(4, 3, 4, 3), TextWrapping = TextWrapping.Wrap };
    Button Button(string key, Action action, bool primary = false)
    {
        var b = new Button { Content = L.Text(key), HorizontalAlignment = HorizontalAlignment.Stretch };
        if (primary) { b.Background = Accent; b.Foreground = SystemParameters.HighContrast ? SystemColors.HighlightTextBrush : darkTheme ? Tone("Bg") : Brushes.White; b.FontWeight = FontWeights.SemiBold; }
        b.Click += (_, _) => { try { action(); } catch (Exception e) { lastError = e is SafeError se ? se.Code : "UNEXPECTED_ERROR"; if (errorText != null) SetState(state); } }; System.Windows.Automation.AutomationProperties.SetName(b, L.Text(key)); return b;
    }
    void Save() { if(!fixtureMode) PrivateStore.Save("profiles.dpapi", data); }
    Profile? Selected => data.Profiles.FirstOrDefault(p => p.Id == data.SelectedId);
    async Task Run(Func<Task> operation)
    {
        if (fixtureMode && !networkFixtureMode || busy || closing) return; busy = true; operationDone = new(TaskCreationOptions.RunContinuationsAsynchronously); lastError = ""; SetState(state);
        try { await operation(); }
        catch (Exception e) { if (connectCancelled && client == null && (e is OperationCanceledException || e is SafeError { Code: "AGENT_FAILED" })) { lastError = ""; SetState(new()); } else { lastError = connectCancelled ? "CANCELLED" : e is SafeError se ? se.Code : "UNEXPECTED_ERROR"; if (state.State == "connecting" || state.State == "connected" && client == null) SetState(new() { State = "error", Error = lastError }); } }
        finally { busy = false; connectCancelled = false; operationDone?.TrySetResult(); SetState(state); BuildCards(); if (lostClient != null && !closing) await Run(HandleLost); }
    }
    async Task Connect()
    {
        var p = Selected ?? throw new SafeError("SELECT_PATH"); string support = SupportPolicy.Reason(p, data.IncludeExperimental); if (support.Length > 0) throw new SafeError(support); ResetTelemetry();
        var resolverPlan = GlobalResolvers.ForConnection(p, data.GlobalResolverEnabled, data.GlobalResolverList);
        p = resolverPlan.Effective;
        if (resolverPlan.Notice.Length > 0) lastError = resolverPlan.Notice;
        p = await ApplyDnsPool(p);
        string slipPin = StoredSlipPin(p);
        if (TransportPlan.UsesSlip(p.Raw) && slipPin.Length == 0)
        {
            if (!ChooseSlipCertificate(p)) throw new SafeError("SLIP_CERT_REQUIRED");
            slipPin = StoredSlipPin(p);
        }
        // Synthetic UI fixtures must not contend with the owner's network lease or recovery journal.
        client = new AgentClient(probeOnly:fixtureMode&&networkFixtureMode); var current = client;
        client.Lost += () => Dispatcher.BeginInvoke(async () =>
        {
            if (current != client || closing) return;
            lostClient = current; if (!busy) await Run(HandleLost);
        });
        SetState(new() { State = "connecting" });
        try
        {
            await current.Start(); var result = await current.Send(new() { Action = "connect", Profile = p, SystemProxy = proxy.IsChecked == true, LocalHttpPort=fixtureMode?0:data.LocalHttpPort, LocalSocksPort=fixtureMode?0:data.LocalSocksPort, SlipCertificate = slipPin, SlipInterval = SlipInterval(p), SshTlsCa=StoredSshTlsCa(p), HostKey = StoredSshKey(p) });
            if (result.State == "trust")
            {
                if (!connectCancelled && !closing && Confirm(L.Text(result.Error) + "\n\n" + p.Name + "\n" + L.Text("SSH_TRUST_HINT"), result.Fingerprint))
                {
                    data.SshKeys[p.Id] = result.HostKey; data.SshKeyScopes[p.Id]=SshHostKey.Scope(p); Save();
                    result = await current.Send(new() { Action = "connect", Profile = p, SystemProxy = proxy.IsChecked == true, LocalHttpPort=fixtureMode?0:data.LocalHttpPort, LocalSocksPort=fixtureMode?0:data.LocalSocksPort, SlipCertificate = slipPin, SlipInterval = SlipInterval(p), SshTlsCa=StoredSshTlsCa(p), HostKey = result.HostKey });
                }
                else result = new() { State = "disconnected", Error = "CANCELLED" };
            }
            lastConnectMs = result.ElapsedMs; if (result.State == "connected") { activeProfile = p; connectedSince = DateTimeOffset.UtcNow; }
            SetState(result); if (result.State != "connected") { await current.DisposeAsync(); if (client == current) client = null; }
        }
        catch { await current.DisposeAsync(); if (client == current) client = null; throw; }
    }
    int? SlipInterval(Profile p) => data.FastSlipProfiles.Contains(p.Id) ? 400 : null;
    void SlipTimingDialog(Profile p)
    {
        var dialog = Dialog("SLIP_INTERVAL", 520, 340);
        var stack = new StackPanel { Margin = new(24) };
        stack.Children.Add(Text(L.Text("SLIP_TIMING_HINT"), 14, Muted));
        void Apply(bool fast)
        {
            bool previous = data.FastSlipProfiles.Contains(p.Id);
            if (fast) data.FastSlipProfiles.Add(p.Id); else data.FastSlipProfiles.Remove(p.Id);
            try { Save(); } catch { if (previous) data.FastSlipProfiles.Add(p.Id); else data.FastSlipProfiles.Remove(p.Id); throw; }
            dialog.DialogResult = true;
        }
        var imported = Button("IMPORT_TIMING", () => Apply(false), SlipInterval(p) == null);
        imported.Content = L.Text("IMPORT_TIMING") + " · " + Importer.Fields(p.Raw)[6] + " ms";
        stack.Children.Add(imported); stack.Children.Add(Button("FAST_TIMING", () => Apply(true), SlipInterval(p) != null));
        dialog.Content = stack; dialog.ShowDialog();
    }
    string StoredSlipPin(Profile p) => TransportPlan.UsesSlip(p.Raw) ? data.SlipCertificates.GetValueOrDefault(SlipCertificate.Scope(p), "") : "";
    bool ChooseSlipCertificate(Profile p)
    {
        var picker = new OpenFileDialog { Title = L.Text("SLIP_CERT_CHOOSE"), Filter = "PEM (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer" };
        if (picker.ShowDialog(this) != true) return false;
        if (new FileInfo(picker.FileName).Length > 32768) throw new SafeError("SLIP_CERT_INVALID");
        var pem = SlipCertificate.ReadPublic(File.ReadAllText(picker.FileName));
        string scope = SlipCertificate.Scope(p);
        using var certificate = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(pem);
        if (!Confirm(L.Text("SLIP_CERT_CONFIRM") + "\n" + Importer.Fields(p.Raw)[3], "SHA256 " + certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256))) return false;
        data.SlipCertificates[scope] = pem; Save(); return true;
    }
    async Task ResolveRecovery()
    {
        if (client != null) await Disconnect();
        try { await AgentClient.Recover(); }
        catch (SafeError e) when (e.Code == "PROXY_RECOVERY_CONFLICT")
        {
            if (!Confirm(L.Text("KEEP_CURRENT_CONFIRM"))) throw;
            await AgentClient.Recover(true);
        }
        SetState(new());
    }
    async Task HandleLost()
    {
        var lost = lostClient; lostClient = null;
        if (lost == null || lost != client) return;
        bool timerRequested = ReferenceEquals(timerDisconnectClient, lost);
        if (timerRequested) timerDisconnectClient = null;
        await lost.DisposeAsync(); client = null; RecordTraffic(new()); await recoverConnection();
        SetState(new() { State = timerRequested ? "disconnected" : "error", Error = timerRequested ? "" : "AGENT_EXITED" });
    }
    async Task Disconnect()
    {
        if(fixtureMode && !networkFixtureMode){SetState(new());return;}
        SetState(new() { State = "disconnecting" });
        string failure = "";
        if (client != null)
        {
            try { var reply = await client.Send(new() { Action = "disconnect" }); lastDisconnectMs = reply.ElapsedMs; failure = reply.Error; RecordTraffic(reply); }
            catch (SafeError e) { failure = ReferenceEquals(timerDisconnectClient, client) && e.Code == "AGENT_FAILED" ? "" : e.Code; }
            finally { if (ReferenceEquals(timerDisconnectClient, client)) timerDisconnectClient = null; await client.DisposeAsync(); client = null; }
        }
        await recoverConnection();
        SetState(new() { State = failure.Length == 0 ? "disconnected" : "error", Error = failure });
    }
    string AccountText(StatusView? view)
    {
        if (view == null || !view.Trustworthy || view.Status == null) return L.Text(view?.Status == null ? "ACCOUNT_UNKNOWN" : view.Status.Metering is "partial" or "unavailable" ? "ACCOUNT_INCOMPLETE" : "ACCOUNT_STALE");
        var s = view.Status;
        if (s.State == "expired") return L.Text("ACCOUNT_EXPIRED"); if (s.State == "limit_reached") return L.Text("ACCOUNT_LIMIT");
        string amount = s.RemainingBytes.HasValue ? "\u2066" + (s.RemainingBytes.Value / 1073741824d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " GiB\u2069 " + L.Text("REMAINING") : L.Text("UNLIMITED_TRAFFIC");
        string time = s.ExpiresAt.HasValue ? "\u2066" + Math.Max(0, (s.ExpiresAt.Value - view.ServerNow!.Value).TotalDays).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "\u2069 " + L.Text("DAYS_REMAINING") : L.Text("UNLIMITED_TIME");
        return amount + " · " + time;
    }
    void Delete(Profile p)
    {
        if (busy || state.State == "connected") return;
        if (!Confirm(L.Text("DELETE_CONFIRM"))) return;
        var previous = data.Profiles.ToList(); var selection = data.SelectedId; data.Profiles.Remove(p); if (data.SelectedId == p.Id) data.SelectedId = ""; try { Save(); } catch { data.Profiles = previous; data.SelectedId = selection; lastError = "PRIVATE_STORE_FAILED"; }
        SetState(state); BuildCards();
    }
    void SubscriptionDialog()=>SubscriptionDialog(null,null);
    void SubscriptionDialog(string? selectedSource,string? initialUrl)
    {
        if (busy) return;

        var d = Dialog("SUBSCRIPTIONS", 620, 500); var content = new StackPanel { Margin = new(24) };
        content.Children.Add(Text(L.Text("SUBSCRIPTION_HINT"), 14, Muted));
        var choose = new ComboBox { Margin = new(4, 12, 4, 8), MinHeight = 34, DisplayMemberPath = "Name", ItemsSource = data.Subscriptions };
        var subscriptionName=new TextBox{Text=selectedSource!=null?data.Subscriptions.FirstOrDefault(s=>s.Id==selectedSource)?.Name??"":"",Tag="SUBSCRIPTION_NAME"};content.Children.Add(OutlinedField("COLLECTION_NAME",subscriptionName)); var input = new TextBox { FlowDirection = FlowDirection.LeftToRight, MinHeight = 48 }; content.Children.Add(OutlinedField("SUBSCRIPTION_URL",input));

        var publicBox = new ComboBox { Margin = new(4, 8, 4, 4), MinHeight = 34, DisplayMemberPath = "Name", ItemsSource = PublicFeed.Presets, SelectedIndex = -1 };

        var publicHint = Text(L.Text("PUBLIC_CONSENT"), 12, Muted); publicHint.Visibility = Visibility.Collapsed; content.Children.Add(publicHint);
        var publicConsent=new CheckBox{Content=L.Text("PUBLIC_ACCEPT"),Visibility=Visibility.Collapsed};content.Children.Add(SettingsRow(publicConsent));
        input.TextChanged+=(_,_)=>{bool known=PublicFeed.IsKnown(input.Text.Trim());publicConsent.Visibility=known?Visibility.Visible:Visibility.Collapsed;publicConsent.IsChecked=false;};
        publicBox.SelectionChanged += (_, _) => { if (publicBox.SelectedItem is PublicFeed.Preset pr) { input.Text = pr.Url; publicHint.Visibility = Visibility.Visible; } };
        choose.SelectionChanged += (_, _) => { if (choose.SelectedItem is Subscription s) input.Text = s.Url; };
        if(selectedSource!=null)choose.SelectedItem=data.Subscriptions.FirstOrDefault(s=>s.Id==selectedSource);
        if(initialUrl!=null){input.Text=initialUrl;publicHint.Visibility=Visibility.Visible;}
        var previewSummary=Text("",13,Muted);previewSummary.Tag="IMPORT_PREVIEW";content.Children.Add(previewSummary);
        var error = Text("", 13, Muted); content.Children.Add(error); var actions = new WrapPanel(); content.Children.Add(actions);
        using var cancellation = new CancellationTokenSource();
        d.Closed += (_, _) => cancellation.Cancel();
        string fetchedUrl = ""; List<Profile>? preview = null; bool fetching = false;
        string FeedError(string code, string sourceId)
        {
            if (code == "STATUS_NOT_FOUND" && statuses.TryGetValue(sourceId, out var snap))
            {
                var view = snap.Evaluate();
                if (view.Status?.State == "expired") return L.Text("ACCOUNT_EXPIRED");
                if (view.Status?.State == "limit_reached") return L.Text("ACCOUNT_LIMIT");
            }
            return L.Text(code);
        }
        string SourceOf(string url)
        {
            try { return Importer.Fingerprint(SubscriptionUrls.Validate(url).AbsoluteUri); }
            catch { return ""; }
        }
        async Task Update()
        {
            if(PublicFeed.IsKnown(input.Text.Trim())&&publicConsent.IsChecked!=true)throw new SafeError("PUBLIC_APPROVAL_REQUIRED");
            var uri = SubscriptionUrls.Validate(input.Text);
            string source = Importer.Fingerprint(uri.AbsoluteUri);
            var sub = data.Subscriptions.FirstOrDefault(s => s.Id == source);
            bool publicFeed=PublicFeed.IsKnown(input.Text.Trim());if(!publicFeed)await RefreshStatus(uri, sub, cancellation.Token); BuildCards();
            int proxyPort=state.State=="connected"?state.HttpPort:0;
            var payload = publicFeed ? await PublicFeed.Fetch(input.Text.Trim(), cancellation.Token,proxyPort) : await SubscriptionClient.Fetch(uri,proxyPort,cancellation: cancellation.Token); if(!publicFeed)CaptureUsage20(source,payload); var batch = publicFeed?PublicFeed.Preview(payload.Body,data.IncludeExperimental):Importer.ReadCompatible(SubscriptionUsage20.ConfigBody(payload.Body), data.IncludeExperimental); previewSummary.Text=ImportSummary(batch);preview = batch.Profiles; if (preview.Count == 0) throw new SafeError("NO_SUPPORTED_IMPORT"); fetchedUrl = uri.AbsoluteUri;
            error.Text = L.Text("UPDATE_READY"); BuildCards();
        }
        var update = Button("UPDATE", () => { }); var import = Button("IMPORT", () => { }, true);
        update.Click += async (_, _) =>
        { if (fetching) return; fetching = true; input.IsEnabled = choose.IsEnabled = false; update.IsEnabled = import.IsEnabled = false; try { await Update(); } catch (Exception e) { preview = null; error.Text = FeedError(e is SafeError se ? se.Code : "FETCH_FAILED", SourceOf(input.Text)); } finally { fetching = false; input.IsEnabled = choose.IsEnabled = true; update.IsEnabled = import.IsEnabled = true; } };
        import.Click += async (_, _) =>
        {
            if (fetching) return; fetching = true; input.IsEnabled = choose.IsEnabled = false; update.IsEnabled = import.IsEnabled = false;
            try
            {
                var uri = SubscriptionUrls.Validate(input.Text); if (preview == null || fetchedUrl != uri.AbsoluteUri){await Update();error.Text+="\n"+L.Text("IMPORT_REVIEW_CONFIRM");return;}
                if (!d.IsVisible) return;
                CommitSubscription20(uri,preview!,subscriptionName.Text);
                BuildCards(); d.DialogResult = true;
            }
            catch (Exception e) { error.Text = FeedError(e is SafeError se ? se.Code : "FETCH_FAILED", SourceOf(input.Text)); }
            finally { fetching = false; input.IsEnabled = choose.IsEnabled = true; update.IsEnabled = import.IsEnabled = true; }
        };
        import.MinWidth=280;import.MinHeight=58;import.FontSize=18;import.Padding=new(24,10,24,10);actions.Children.Add(import);
        var summary = Text("", 13, Muted); content.Children.Insert(2, summary);
        choose.SelectionChanged += (_, _) =>
        {
            if (choose.SelectedItem is Subscription selected)
            {
                var count = data.Profiles.Count(p => p.SourceId == selected.Id);
                summary.Text = L.Text("PATHS") + " · " + count + "\n" + L.Text("LAST_UPDATED") + " · " + (data.SubscriptionUpdated.TryGetValue(selected.Id, out var time) ? time.ToLocalTime().ToString("g") : L.Text("UNKNOWN"));
            }
        };
        d.Content = content; d.ShowDialog();
    }
    async Task RefreshStatus(Uri uri, Subscription? subscription, CancellationToken cancellation = default)
    {
        var id = Importer.Fingerprint(uri.AbsoluteUri); var statusUri = SubscriptionUrls.Status(uri);
        if (statusUri == null) { statuses[id] = new() { Error = "INVALID_STATUS" }; return; }
        statuses.TryGetValue(id, out var prior);
        try
        {
            var payload = await SubscriptionClient.Fetch(statusUri, state.State=="connected"?state.HttpPort:0, 65536, cancellation); var account = AccountStatus.Read(payload.Body);
            var previous = prior?.Status ?? (subscription?.StatusBody.Length > 0 ? AccountStatus.Read(subscription.StatusBody) : null);
            if (previous != null && !account.Accepts(previous)) throw new SafeError("INVALID_STATUS");
            ownAccountSources20.Add(id);statuses[id] = new() { Status = account, ServerDate = payload.Date };
            statusBodies[id] = payload.Body; if (subscription != null) { subscription.StatusBody = payload.Body; Save(); }
        }
        catch (Exception e) { statuses[id] = new() { Status = prior?.Status, ServerDate = prior?.ServerDate, ReceivedAt = prior?.ReceivedAt ?? DateTimeOffset.UtcNow, ReceivedTick = prior?.ReceivedTick ?? Stopwatch.GetTimestamp(), Error = e is SafeError se ? se.Code : "INVALID_STATUS" }; }
    }
    void ProbeDialog()
    {
        if (busy || client == null || state.State != "connected") return;
        var d = Dialog("VERIFY_HTTPS", 570, 340); var c = new StackPanel { Margin = new(24) }; c.Children.Add(Text(L.Text("PROBE_HINT"), 14, Muted));
        var input = new TextBox { Text = data.LatencyUrl, FlowDirection = FlowDirection.LeftToRight }; c.Children.Add(input); var result = Text("", 14); c.Children.Add(result);
        var test = Button("VERIFY_HTTPS", () => { }, true);bool active=false,closePending=false,written=false,cancellationSent=false;CancellationTokenSource? manualStop=null;var connection=client;
        var cancel=Button("STOP_TEST",()=>{});cancel.Visibility=Visibility.Collapsed;cancel.Foreground=Red;d.SetPageAction(cancel,false);
        async Task SendCancellation(){if(!written||cancellationSent)return;cancellationSent=true;try{await connection.CancelProbe();}catch(SafeError e){result.Text=L.Text(e.Code);}}
        cancel.Click+=async(_,_)=>{if(!active)return;cancel.IsEnabled=false;cancel.Content=L.Text("CANCELLING");manualStop?.Cancel();await SendCancellation();};
        d.Closing+=(_,e)=>{if(active){e.Cancel=true;closePending=true;cancel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));}};
        test.Click += async (_, _) => {
            if(active)return;string url;try{url=SubscriptionUrls.Validate(input.Text).AbsoluteUri;}catch(SafeError e){result.Text=L.Text(e.Code);return;}
            active=true;written=cancellationSent=false;manualStop=new();test.IsEnabled=input.IsEnabled=false;cancel.Content=L.Text("STOP_TEST");cancel.Visibility=Visibility.Visible;cancel.IsEnabled=true;
            var profile=activeProfile??Selected;if(profile!=null){string key=CollectionKey(profile);data.CollectionTestStarted[key]=DateTimeOffset.UtcNow;collectionRuns.Remove(key);liveMeasurements.ExceptWith(data.Profiles.Where(p=>CollectionKey(p)==key).Select(p=>p.Id));}
            try{await Run(async()=>{AgentReply reply;try{reply=await connection.Send(new(){Action="probe",ProbeUrl=url,ProbeFallbacks=data.ProbeFallbacks},manualStop.Token,()=>{written=true;if(manualStop.IsCancellationRequested)_=SendCancellation();});}catch(OperationCanceledException)when(manualStop.IsCancellationRequested){result.Text=L.Text("CANCELLED");result.Foreground=Muted;return;}bool stopped=reply.Error=="CANCELLED";result.Text=(stopped?L.Text("CANCELLED"):ProbeResultText(reply))+" · "+reply.ElapsedMs+" ms";result.Foreground=stopped?Muted:reply.Verified?Green:HttpsProbePolicy.Unconfirmed(reply.Error)?Amber:Red;
                if(!stopped&&profile!=null){measurements[profile.Id]=reply;data.Measurements[profile.Id]=new(){Verified=reply.Verified,ProbeFallbackUsed=reply.ProbeFallbackUsed,ElapsedMs=reply.ElapsedMs,Error=reply.Error,Stage=reply.ProbeStage,Attempts=1,Passes=reply.Verified?1:0,SampledAt=DateTimeOffset.UtcNow};liveMeasurements.Add(profile.Id);Save();}
                if(stopped)reply.Error="";SetState(reply);
            });}finally{active=false;manualStop.Dispose();manualStop=null;test.IsEnabled=input.IsEnabled=true;cancel.Visibility=Visibility.Collapsed;if(closePending)d.Close();}
        };
        c.Children.Add(test); d.Content = c; d.ShowDialog();
    }
    void LanguageDialog()
    {
        var d = Dialog("LANGUAGE", 500, 360); var panel = new StackPanel { Margin = new(24) };
        foreach (var (code, title) in new[] { ("fa", "فارسی"), ("en", "English"), ("zh-CN", "简体中文"), ("ru", "Русский") })
        { var b = Button("LANGUAGE", () => { L.Language = code; data.Language = code; try { Save(); } catch { lastError = "PRIVATE_STORE_FAILED"; } d.DialogResult = true; Build(); }); b.Content = title; panel.Children.Add(b); }
        d.Content = panel; d.ShowDialog();
    }
    void Help() => Info("HELP", L.Text("HELP_BODY"));
    void About() => AboutDialog();
    static string SafeProtocol(string protocol) => new[] { "VLESS WS", "VLESS XHTTP", "VLESS REALITY", "VLESS TCP", "SSH", "NaiveProxy", "NaiveProxy + SSH", "Hysteria2", "WireGuard", "SOCKS5", "Soar", "Soar + SSH", "DNSTT", "DNSTT + SSH", "NoizDNS", "NoizDNS + SSH", "VayDNS", "VayDNS + SSH", "Slipstream", "Slipstream + SSH" }.Contains(protocol) ? protocol : "other";
    void ExportDiagnostics()
    {
        var d = new SaveFileDialog { FileName = "Rovunet-diagnostics.json", Filter = "JSON|*.json" }; if (d.ShowDialog(this) != true) return;
        try { File.WriteAllText(d.FileName, JsonSerializer.Serialize(new { report_version = 1, product_version = Version, utc = DateTimeOffset.UtcNow, windows_version = Environment.OSVersion.Version.ToString(), process_arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), connection_state = state.State, https_verified = state.Verified, last_error_code = state.Error.Length > 0 ? state.Error : lastError, last_connect_ms = lastConnectMs, last_disconnect_ms = lastDisconnectMs, recent_error_codes = safeEvents.ToArray(), path_tests = data.Profiles.Select((p, i) => new { row = i + 1, protocol = SafeProtocol(p.Protocol), result = measurements.TryGetValue(p.Id, out var m) ? new { verified = m.Verified, error_code = m.Error, stage = m.ProbeStage, elapsed_ms = m.ElapsedMs } : null }).ToArray(), tun_supported = false, signature = "unsigned-preview" }, new JsonSerializerOptions { WriteIndented = true })); }
        catch { lastError = "SAVE_FAILED"; SetState(state); }
    }
}



