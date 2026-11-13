using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;

public partial class App : Application
{
    Mutex? instance; EventWaitHandle? activation; RegisteredWaitHandle? activationWait;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += async (_, args) =>
        {
            args.Handled = true;
            if(MainWindow is MainWindow ending&&ending.SystemSessionEnding30){Shutdown();return;}
            if(e.Args.Length==2 && e.Args[0].StartsWith("--ui-",StringComparison.Ordinal))
            {
                if(Path.IsPathFullyQualified(e.Args[1])&&!File.Exists(e.Args[1]))File.WriteAllText(e.Args[1],System.Text.Json.JsonSerializer.Serialize(new{passed=false,error=args.Exception.GetType().Name}));Shutdown(1);return;
            }
            if (MainWindow is MainWindow main) await main.RecoverUiError();
            else { Notice("UNEXPECTED_ERROR"); Shutdown(1); }
        };
        if (e.Args is ["--ui-review09",var inputRoot,var outputPath]) {
            if(!Path.IsPathFullyQualified(inputRoot)||!Path.IsPathFullyQualified(outputPath)||File.Exists(outputPath)){Shutdown(12);return;}
            var reviewWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);
            MainWindow=reviewWindow;reviewWindow.Show();bool ok=await reviewWindow.RunReview09(inputRoot,outputPath);reviewWindow.Close();Shutdown(ok?0:1);return;
        }
        if(e.Args is ["--ui-accordion26",var accordionReport]) {
            if(!Path.IsPathFullyQualified(accordionReport)||File.Exists(accordionReport)){Shutdown(12);return;}
            var accordionWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=accordionWindow;accordionWindow.Show();bool passed=await accordionWindow.RunAccordion26(accordionReport);accordionWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args is ["--ui-selection17",var selectionReport]) {
            if(!Path.IsPathFullyQualified(selectionReport)||File.Exists(selectionReport)){Shutdown(12);return;}
            var selectionWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=selectionWindow;selectionWindow.Show();bool passed=await selectionWindow.RunScroll17(selectionReport,true);selectionWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args is ["--ui-status21",var statusReport]) {
            if(!Path.IsPathFullyQualified(statusReport)||File.Exists(statusReport)){Shutdown(12);return;}
            var statusWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=statusWindow;statusWindow.Show();bool passed=await statusWindow.RunStatus21(statusReport);statusWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args is ["--ui-scroll17",var scrollReport]) {
            if(!Path.IsPathFullyQualified(scrollReport)||File.Exists(scrollReport)){Shutdown(12);return;}
            var scrollWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=scrollWindow;scrollWindow.Show();bool passed=await scrollWindow.RunScroll17(scrollReport);scrollWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args is ["--ui-mac16",var macReport]) {
            if(!Path.IsPathFullyQualified(macReport)||File.Exists(macReport)){Shutdown(12);return;}
            var macWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=macWindow;macWindow.Show();bool passed=await macWindow.RunMac16Smoke(macReport);macWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args is ["--ui-research15",var researchReport]) {
            if(!Path.IsPathFullyQualified(researchReport)||File.Exists(researchReport)){Shutdown(12);return;}
            var researchWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=researchWindow;researchWindow.Show();bool passed=await researchWindow.RunResearch15Smoke(researchReport);researchWindow.Close();Shutdown(passed?0:1);return;
        }
        if (e.Args is ["--ui-subscription-contract", var inputs, var reportPath]) {
            if (!Path.IsPathFullyQualified(inputs) || !Path.IsPathFullyQualified(reportPath) || File.Exists(reportPath)) { Shutdown(12); return; }
            var contractWindow=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);
            MainWindow=contractWindow;contractWindow.Show();bool passed=await contractWindow.ContractSmoke(inputs,reportPath);contractWindow.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-protocols25-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunProtocols25Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-maintenance28-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunMaintenance28Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-updater28-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunUpdater28Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-about-language27-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunAboutLanguage27Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-about-support26-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunAboutSupport26Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            if(e.Args.Length==2 && e.Args[0].StartsWith("--ui-",StringComparison.Ordinal))return;
            try { Directory.CreateDirectory(PrivateStore.Root); File.WriteAllText(Path.Combine(PrivateStore.Root, "last-crash.json"), System.Text.Json.JsonSerializer.Serialize(new { code = "UNEXPECTED_ERROR", utc = DateTimeOffset.UtcNow, version = Rovunet.Desktop.MainWindow.Version })); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
        if(e.Args is ["--ui-upgrade-exit-fixture"] or ["--ui-upgrade-exit-fixture","--connected-probe"] or ["--ui-upgrade-exit-fixture","--session-report",_] or ["--ui-upgrade-exit-fixture","--connected-probe","--session-report",_]){
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,Title="Rovunet"};MainWindow=fixture;
            bool connected=e.Args.Contains("--connected-probe");string? report=e.Args.Length>=3?e.Args[^1]:null;
            if(report!=null&&(!Path.IsPathFullyQualified(report)||File.Exists(report))){Shutdown(12);return;}
            if(connected)await fixture.ConnectShutdownFixture30();
            SessionEnding+=(_,args)=>{args.Cancel=false;try{fixture.PrepareSystemShutdown30();}catch{}if(report!=null)File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{cancel=args.Cancel,cleanupInvoked=fixture.SystemSessionEnding30,connectedProbe=connected}));};fixture.Closed+=(_,_)=>Shutdown();fixture.Show();return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-amnezia24-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunAmnezia24Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-default22-smoke"){
            var fixture=new MainWindow(new(){UseSystemProxy=false},true){ShowInTaskbar=false,ShowActivated=false,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunDefault22Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-approved21-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true){ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};MainWindow=fixture;fixture.Show();bool passed=await fixture.RunApproved21Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-card20-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunCard20Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-public20-input"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            string input=Console.In.ReadLine()??"";if(input.Length>6*1024*1024){Shutdown(12);return;}
            var bodies=System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(input);var fixture=new MainWindow(new(){UseSystemProxy=false},true);MainWindow=fixture;bool passed=await fixture.RunPublic20Audit(e.Args[1],bodies);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-public20-audit"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true);MainWindow=fixture;bool passed=await fixture.RunPublic20Audit(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-product20-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunProduct20Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-metadata20-network"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(PrivateStore.Load<UserData>("profiles.dpapi")??new(),true);MainWindow=fixture;bool passed=await fixture.RunMetadata20Network(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-scanner20-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunScanner20Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-scanner20-network"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var owner=PrivateStore.Load<UserData>("profiles.dpapi")??new();
            var fixture=new MainWindow(owner,true);MainWindow=fixture;bool passed=await fixture.RunScanner20Network(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-subscription19-network"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;bool passed=await fixture.RunSubscription19Network(e.Args[1],Console.In.ReadToEnd());fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-public19-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;bool passed=await fixture.RunPublic19Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-scanner19-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunScanner19Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-desktop18-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunDesktop18Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-extra-protocol-network"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunExtraNetworkSmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2&&e.Args[0]=="--ui-desktop17-smoke"){
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunDesktop17Smoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045) || !Environment.Is64BitProcess) { Notice("OS_UNSUPPORTED"); Shutdown(1); return; }
        if(e.Args.Length==2 && e.Args[0]=="--ui-trust-telemetry-smoke")
        {
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunTrustTelemetrySmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2 && e.Args[0]=="--ui-core-network-smoke")
        {
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunCoreNetworkSmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2 && e.Args[0]=="--ui-core-editor-smoke")
        {
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunCoreEditorSmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2 && e.Args[0]=="--ui-flow-smoke")
        {
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunFlowSmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2 && e.Args[0]=="--ui-inner-pages-smoke")
        {
            if(!Path.IsPathFullyQualified(e.Args[1])||File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();bool passed=await fixture.RunInnerPagesSmoke(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if(e.Args.Length==2 && e.Args[0]=="--ui-portrait-preview")
        {
            if(!Path.IsPathFullyQualified(e.Args[1]) || File.Exists(e.Args[1])){Shutdown(12);return;}
            var fixture=new MainWindow(new(){UseSystemProxy=false,IncludeExperimental=true},true);MainWindow=fixture;fixture.Show();
            bool passed=await fixture.RunPortraitPreview(e.Args[1]);fixture.Close();Shutdown(passed?0:1);return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--ui-pool-lifecycle-smoke")
        {
            if (!Path.IsPathFullyQualified(e.Args[1]) || File.Exists(e.Args[1])) { Shutdown(12); return; }
            var fixture = new MainWindow(new() { UseSystemProxy = false }, true); MainWindow = fixture; fixture.Show();
            bool passed = await fixture.RunPoolLifecycleSmoke(e.Args[1]); fixture.Close(); Shutdown(passed ? 0 : 1); return;
        }
        if (e.Args.Length == 2 && e.Args[0] is "--ui-dns-pool-smoke" or "--ui-dns-pool-verified-smoke")
        {
            if (!Path.IsPathFullyQualified(e.Args[1]) || File.Exists(e.Args[1])) { Shutdown(12); return; }
            var fixture = new MainWindow(new() { UseSystemProxy=false }, true); MainWindow=fixture; fixture.Show();
            bool passed=e.Args[0] == "--ui-dns-pool-verified-smoke" ? await fixture.RunDnsPoolVerifiedSmoke(e.Args[1],Console.In.ReadToEnd()) : await fixture.RunDnsPoolSmoke(e.Args[1]); fixture.Close(); Shutdown(passed?0:1);return;
        }
        if (e.Args.Length == 2 && e.Args[0] is "--ui-busy-sleep-smoke" or "--ui-agent-loss-smoke" or "--ui-recovery-failure-smoke" or "--ui-replacement-sleep-smoke" or "--ui-suspend-smoke" or "--ui-cancel-smoke")
        {
            if (!Path.IsPathFullyQualified(e.Args[1]) || File.Exists(e.Args[1])) { Shutdown(12); return; }
            var fixture = new MainWindow(new() { UseSystemProxy = false, SleepTimerMinutes = 5 }, true);
            MainWindow = fixture; fixture.Show(); bool passed = await fixture.RunBusySleepSmoke(e.Args[1], e.Args[0] == "--ui-suspend-smoke" ? "suspend" : e.Args[0] == "--ui-cancel-smoke" ? "cancel" : e.Args[0] == "--ui-replacement-sleep-smoke" ? "replacement" : e.Args[0] == "--ui-agent-loss-smoke" ? "loss" : e.Args[0] == "--ui-recovery-failure-smoke" ? "recovery-failure" : "timer"); fixture.Close(); Shutdown(passed ? 0 : 1); return;
        }
        if (e.Args.Length == 2 && e.Args[0] == "--ui-smoke")
        {
            var sample = Importer.Read(string.Join("\n", Enumerable.Range(0, 20).Select(i => "socks5://127.0.0.1:" + (1080 + i) + "#UI%20fixture%20" + i)));
            foreach (var p in sample) { p.CollectionId = "ui-fixture"; p.CollectionName = "UI fixture"; p.SourceId = "ui-fixture-source"; p.ItemId = p.Id; }
            var fixture = new MainWindow(new() { Profiles = sample, SelectedId = sample[0].Id, UseSystemProxy = false, ExpandedCollections = new() { ["sub:ui-fixture-source"] = true } }, true);
            fixture.PreviewMouseDown += (_, args) => args.Handled = true; fixture.PreviewKeyDown += (_, args) => args.Handled = true; MainWindow = fixture; fixture.Show(); bool passed = await fixture.RunUiSmoke(e.Args[1]); fixture.Close(); Shutdown(passed ? 0 : 1); return;
        }
        if(e.Args is ["--uninstall-recovery-scope"]){try{Shutdown(Recovery.UninstallScope());}catch{Shutdown(12);}return;}
        if ((e.Args.Contains("--recover") || e.Args.Contains("--recover-silent")))
        {
            try { await AgentClient.Recover(); if (!e.Args.Contains("--recover-silent")) Notice("RECOVERY_DONE"); Shutdown(); } catch (SafeError error) { if (!e.Args.Contains("--recover-silent")) Notice(error.Code); Shutdown(1); }
            return;
        }
        string name = @"Global\Rovunet.UI." + WindowsIdentity.GetCurrent().User!.Value;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        instance = new Mutex(true, name, out bool created);
        if (!created) {
            bool other=false;
            foreach(var process in Process.GetProcessesByName("Rovunet"))using(process)try{if(process.Id!=Environment.ProcessId && !string.Equals(process.MainModule?.FileName,Environment.ProcessPath,StringComparison.OrdinalIgnoreCase)){other=true;break;}}catch{}
            if(other)Notice("OTHER_VERSION_RUNNING");else activation.Set();Shutdown();return;
        }
        bool firstRun=!File.Exists(Path.Combine(PrivateStore.Root,"profiles.dpapi"));
        UserData data;
        try { data = PrivateStore.Load<UserData>("profiles.dpapi") ?? new() { Language = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Rovunet", "InstallLanguage", "1065")?.ToString() switch { "1033" => "en", "1049" => "ru", "2052" => "zh-CN", _ => "fa" } }; }
        catch
        {
            var dialog = new DialogWindow(null, L.Text("PRIVATE_STORE_FAILED"), 600, 400);
            var panel = new StackPanel { Margin = new(24) };
            panel.Children.Add(new TextBlock { Text = L.Text("STORE_RECOVERY_HINT") });
            var open = new Button { Content = L.Text("OPEN_DATA") };
            open.Click += (_, _) => { Directory.CreateDirectory(PrivateStore.Root); Process.Start(new ProcessStartInfo(PrivateStore.Root) { UseShellExecute = true }); }; panel.Children.Add(open);
            bool reset = false;
            var backup = new Button { Content = L.Text("BACKUP_RESET") };
            backup.Click += (_, _) =>
            {
                try
                {
                    var path = Path.Combine(PrivateStore.Root, "profiles.dpapi");
                    File.Move(path, path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "." + Guid.NewGuid().ToString("N")[..8] + ".bak");
                    reset = true; dialog.DialogResult = true;
                }
                catch { Notice("SAVE_FAILED"); }
            }; panel.Children.Add(backup); dialog.Content = panel; dialog.ShowDialog();
            if (!reset) { Shutdown(); return; }
            data = new();
            // Preserve proxy-recovery.dpapi, independently of the profile store.
        }
        L.Language = data.Language;
        if(firstRun){
            var chooser=new DialogWindow(null,"Language · زبان · 语言 · Язык",470,300);
            var choices=new StackPanel{Margin=new(20)};
            foreach(var (value,title) in new[]{("fa","فارسی"),("en","English"),("ru","Русский"),("zh-CN","简体中文")}){
                var select=new Button{Content=title};select.Click+=(_,_)=>{data.Language=value;L.Language=value;chooser.DialogResult=true;};choices.Children.Add(select);
            }
            chooser.Content=choices;if(chooser.ShowDialog()!=true){Shutdown();return;}
            try{PrivateStore.Save("profiles.dpapi",data);}catch{Notice("PRIVATE_STORE_FAILED");Shutdown(1);return;}
        }
        try { bool refreshed = false; foreach (var p in data.Profiles) refreshed |= Importer.RefreshCapabilities(p); if (refreshed) PrivateStore.Save("profiles.dpapi", data); }
        catch { Notice("PRIVATE_STORE_FAILED"); Shutdown(1); return; }
        var window = new MainWindow(data); MainWindow = window; ShutdownMode = ShutdownMode.OnMainWindowClose; window.Show();
        activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatcher.BeginInvoke(() => window.ShowExisting()), null, Timeout.Infinite, false);
        SessionEnding += (_, args) => { args.Cancel=false;try{window.PrepareSystemShutdown30();}catch{} };
    }
    static void Notice(string key)
    {
        var d = new DialogWindow(null, "Rovunet", 540, 260);
        d.Content = new TextBlock { Text = L.Text(key), Margin = new(24) }; d.ShowDialog();
    }
    protected override void OnExit(ExitEventArgs e) { activationWait?.Unregister(null); activation?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
