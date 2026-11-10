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
            if(e.Args.Length==2 && e.Args[0].StartsWith("--ui-",StringComparison.Ordinal))
            {
                if(Path.IsPathFullyQualified(e.Args[1])&&!File.Exists(e.Args[1]))File.WriteAllText(e.Args[1],System.Text.Json.JsonSerializer.Serialize(new{passed=false,error=args.Exception.GetType().Name}));Shutdown(1);return;
            }
            if (MainWindow is MainWindow main) await main.RecoverUiError();
            else { Notice("UNEXPECTED_ERROR"); Shutdown(1); }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            if(e.Args.Length==2 && e.Args[0].StartsWith("--ui-",StringComparison.Ordinal))return;
            try { Directory.CreateDirectory(PrivateStore.Root); File.WriteAllText(Path.Combine(PrivateStore.Root, "last-crash.json"), System.Text.Json.JsonSerializer.Serialize(new { code = "UNEXPECTED_ERROR", utc = DateTimeOffset.UtcNow, version = Rovunet.Desktop.MainWindow.Version })); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
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
        SessionEnding += (_, args) => { args.Cancel = true; _ = window.ExitApplication(); };
    }
    static void Notice(string key)
    {
        var d = new DialogWindow(null, "Rovunet", 540, 260);
        d.Content = new TextBlock { Text = L.Text(key), Margin = new(24) }; d.ShowDialog();
    }
    protected override void OnExit(ExitEventArgs e) { activationWait?.Unregister(null); activation?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
