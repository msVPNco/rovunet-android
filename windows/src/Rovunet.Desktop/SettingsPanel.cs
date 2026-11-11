using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void SettingsDialog()
    {
        OpenContext("settings");
    }

    // Same section order as Android SettingsScreen; only working Windows options are exposed.
    StackPanel BuildSettingsPanel(Action close)
    {
        var panel = new StackPanel { Margin = new(18,8,18,20) };

        StackPanel Section(string key)
        {
            var section = new StackPanel();
            var title = Text(L.Text(key), 16, Accent); title.FontWeight = FontWeights.SemiBold;
            title.FontSize=15;title.Margin=new(2,12,2,10);panel.Children.Add(title);var card=Card(section);card.CornerRadius=new(18);card.BorderThickness=new(0);card.Padding=new(16,8,16,10);panel.Children.Add(card);return section;
        }
        var connection = Section("SECTION_CONNECTION");

        var mode = new CheckBox { Content = L.Text("SYSTEM_PROXY"), IsChecked = data.UseSystemProxy, IsEnabled = !busy && state.State is "disconnected" or "error" };
        mode.Checked += (_, _) => proxy.IsChecked = true; mode.Unchecked += (_, _) => proxy.IsChecked = false;
        connection.Children.Add(SettingsRow(mode,"SYSTEM_PROXY_ONLY"));
        connection.Children.Add(BuildLocalProxyPanel19());
        var trayChoice = new CheckBox { Content = L.Text("CLOSE_TO_TRAY"), IsChecked = data.CloseToTray };
        RoutedEventHandler trayChanged = (_, _) => { bool prior = data.CloseToTray; data.CloseToTray = trayChoice.IsChecked == true; try { Save(); } catch { data.CloseToTray = prior; throw; } };
        trayChoice.Checked += trayChanged; trayChoice.Unchecked += trayChanged; connection.Children.Add(SettingsRow(trayChoice));
        connection.Children.Add(Text(L.Text("SLEEP_TIMER"), 14));
        connection.Children.Add(Text(L.Text("SLEEP_TIMER_HINT"), 12, Muted));
        var sleepControls = new DockPanel();
        var sleepValue = Text("", 15);
        var lessSleep = Button("SLEEP_DECREASE", () => { }); lessSleep.Content = "−";
        var moreSleep = Button("SLEEP_INCREASE", () => { }); moreSleep.Content = "+";
        void RefreshSleepChoice() {
            int value = SleepTimerSession.ValidMinutes(data.SleepTimerMinutes) ? data.SleepTimerMinutes : 0;
            sleepValue.Text = value == 0 ? L.Text("SLEEP_OFF") : string.Format(L.Text("SLEEP_MINUTES"), value);
            lessSleep.IsEnabled = value > 0; moreSleep.IsEnabled = value < 120;
        }
        void ChangeSleep(int step) {
            int prior = data.SleepTimerMinutes;
            int value = SleepTimerSession.ValidMinutes(prior) ? prior : 0;
            data.SleepTimerMinutes = Math.Clamp(value + step, 0, 120);
            try { Save(); } catch { data.SleepTimerMinutes = prior; lastError = "PRIVATE_STORE_FAILED"; SetState(state); return; }
            RefreshSleepChoice();
        }
        lessSleep.Click += (_, _) => ChangeSleep(-5); moreSleep.Click += (_, _) => ChangeSleep(5);
        DockPanel.SetDock(lessSleep, Dock.Left); DockPanel.SetDock(moreSleep, Dock.Right);
        sleepControls.Children.Add(lessSleep); sleepControls.Children.Add(moreSleep); sleepControls.Children.Add(sleepValue);
        connection.Children.Add(sleepControls); RefreshSleepChoice();
        var tools = Section("DNS_SETTINGS");



        var globalEnabled = new CheckBox { Content = L.Text("GLOBAL_RESOLVER_TITLE"), IsChecked = data.GlobalResolverEnabled };
        var globalList = new TextBox { Tag = "GlobalResolverList", Text = data.GlobalResolverList, AcceptsReturn = true, MinHeight = 88, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FlowDirection = FlowDirection.LeftToRight };
        tools.Children.Add(SettingsRow(globalEnabled,"GLOBAL_RESOLVER_HINT"));
        var globalEditor=new StackPanel{Visibility=Visibility.Collapsed};globalEditor.Children.Add(OutlinedField("DNS_RESOLVERS",globalList));
        tools.Children.Add(SettingsAction("EDIT_RESOLVERS",()=>globalEditor.Visibility=globalEditor.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible));tools.Children.Add(globalEditor);
        globalEnabled.Checked+=(_,_)=>globalEditor.Visibility=Visibility.Visible;globalEnabled.Unchecked+=(_,_)=>globalEditor.Visibility=Visibility.Visible;
        var globalFeedback = Text("", 12, Muted);
        globalList.TextChanged += (_, _) => globalFeedback.Text = "";
        globalEnabled.Checked += (_, _) => globalFeedback.Text = "";
        globalEnabled.Unchecked += (_, _) => globalFeedback.Text = "";
        globalEditor.Children.Add(Button("GLOBAL_RESOLVER_SAVE", () => {
            try {
                string validated = GlobalResolvers.Normalize(globalList.Text);
                bool oldEnabled = data.GlobalResolverEnabled; string oldList = data.GlobalResolverList;
                data.GlobalResolverEnabled = globalEnabled.IsChecked == true; data.GlobalResolverList = validated;
                try { Save(); } catch { data.GlobalResolverEnabled = oldEnabled; data.GlobalResolverList = oldList; throw; }
                globalFeedback.Text = L.Text("SETTINGS_SAVED"); globalFeedback.Foreground = Muted;
            } catch (Exception error) { globalFeedback.Text = L.Text(error is SafeError safe ? safe.Code : "SAVE_FAILED"); globalFeedback.Foreground = Red; }
        }, true));
        globalEditor.Children.Add(globalFeedback);
        var poolEnabled = new CheckBox { Content = L.Text("DNS_POOL_TITLE"), IsChecked = data.DnsPoolEnabled };
        var poolVerify = new CheckBox { Content = L.Text("DNS_POOL_VERIFY"), IsChecked = data.DnsPoolVerify };
        var poolList = new TextBox { Tag = "DnsPoolList", Text = data.DnsPoolList, AcceptsReturn = true, MinHeight = 88, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FlowDirection = FlowDirection.LeftToRight };
        tools.Children.Add(SettingsRow(poolEnabled,"DNS_POOL_HINT"));
        var poolEditor=new StackPanel{Visibility=Visibility.Collapsed};poolEditor.Children.Add(SettingsRow(poolVerify));poolEditor.Children.Add(OutlinedField("DNS_RESOLVERS",poolList));
        tools.Children.Add(SettingsAction("EDIT_DNS_POOL",()=>poolEditor.Visibility=poolEditor.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible));tools.Children.Add(poolEditor);
        poolEnabled.Checked+=(_,_)=>poolEditor.Visibility=Visibility.Visible;poolEnabled.Unchecked+=(_,_)=>poolEditor.Visibility=Visibility.Visible;
        var poolFeedback = Text("",12,Muted);
        poolEditor.Children.Add(Button("DNS_POOL_SAVE", () => {
            try {
                string validated = string.Join('\n', DnsPool.Parse(poolList.Text));
                var prior = (data.DnsPoolEnabled, data.DnsPoolVerify, data.DnsPoolList);
                data.DnsPoolEnabled = poolEnabled.IsChecked == true; data.DnsPoolVerify = poolVerify.IsChecked == true; data.DnsPoolList = validated;
                try { Save(); } catch { (data.DnsPoolEnabled,data.DnsPoolVerify,data.DnsPoolList)=prior; throw; }
                poolFeedback.Text=L.Text("SETTINGS_SAVED"); poolFeedback.Foreground=Muted;
            } catch(Exception error) { poolFeedback.Text=L.Text(error is SafeError safe?safe.Code:"SAVE_FAILED");poolFeedback.Foreground=Red; }
        },true)); poolEditor.Children.Add(poolFeedback);
        tools.Children.Add(SettingsAction("FIND_DNS",()=>{close();ScanMenu();}));
        poolList.TextChanged += (_,_) => poolFeedback.Text="";

        var probe = Section("SECTION_PROBE");
        var fallbackProbe=new CheckBox{Content=L.Text("PROBE_FALLBACKS"),Tag="PROBE_FALLBACKS",IsChecked=data.ProbeFallbacks};
        RoutedEventHandler fallbackChanged=(_,_)=>{bool prior=data.ProbeFallbacks;data.ProbeFallbacks=fallbackProbe.IsChecked==true;try{Save();}catch{data.ProbeFallbacks=prior;throw;}};
        fallbackProbe.Checked+=fallbackChanged;fallbackProbe.Unchecked+=fallbackChanged;probe.Children.Add(SettingsRow(fallbackProbe,"PROBE_FALLBACKS_HINT"));
        var liveProbe=new CheckBox{Content=L.Text("LIVE_LATENCY"),Tag="LIVE_LATENCY",IsChecked=data.LiveLatency};
        RoutedEventHandler liveProbeChanged=(_,_)=>{bool prior=data.LiveLatency;data.LiveLatency=liveProbe.IsChecked==true;try{Save();lastLatencyTick=0;}catch{data.LiveLatency=prior;throw;}};
        liveProbe.Checked+=liveProbeChanged;liveProbe.Unchecked+=liveProbeChanged;probe.Children.Add(SettingsRow(liveProbe));

        var target = new TextBox { Text = data.LatencyUrl, FlowDirection = FlowDirection.LeftToRight };
        probe.Children.Add(OutlinedField("LATENCY_URL",target));
        var feedback = Text("", 12, Muted); feedback.Visibility = Visibility.Collapsed;
        target.TextChanged += (_, _) => feedback.Visibility = Visibility.Collapsed;
        probe.Children.Add(Button("SAVE", () => {
            try
            {
                string validated = SubscriptionUrls.Validate(target.Text).AbsoluteUri;
                string prior = data.LatencyUrl; data.LatencyUrl = validated;
                try { Save(); } catch { data.LatencyUrl = prior; throw; }
                feedback.Text = L.Text("SETTINGS_SAVED"); feedback.Foreground = Muted;
            }
            catch (Exception error)
            {
                feedback.Text = L.Text(error is SafeError safe ? safe.Code : "SAVE_FAILED");
                feedback.Foreground = Red;
            }
            feedback.Visibility = Visibility.Visible;
        }, true));
        probe.Children.Add(feedback);

        var appearance = Section("SECTION_APPEARANCE");
        var themes = new WrapPanel();
        foreach (var (value, label) in new[] { ("system", "SYSTEM_THEME"), ("light", "LIGHT_THEME"), ("dark", "DARK_THEME"), ("amoled", "AMOLED_THEME") })
            themes.Children.Add(Button(label, () => { SetTheme(value); close(); }, data.Theme == value));
        appearance.Children.Add(themes);
        appearance.Children.Add(Text(L.Text("TEXT_MOTION"),14));
        var textMotion=new ComboBox{Tag="TEXT_MOTION",ItemsSource=new[]{L.Text("TEXT_MOTION_SYSTEM"),L.Text("TEXT_MOTION_ON"),L.Text("TEXT_MOTION_OFF")},SelectedIndex=data.MarqueeMotion=="on"?1:data.MarqueeMotion=="off"?2:0};
        textMotion.SelectionChanged+=(_,_)=>{
            string prior=data.MarqueeMotion;data.MarqueeMotion=textMotion.SelectedIndex==1?"on":textMotion.SelectedIndex==2?"off":"system";
            try{Save();}catch{data.MarqueeMotion=prior;throw;}
            MarqueeLine.RefreshAll(homeContent);MarqueeLine.RefreshAll((DependencyObject)Content);
        };
        appearance.Children.Add(textMotion);appearance.Children.Add(Text(L.Text("TEXT_MOTION_HINT"),12,Muted));

        var diagnostics = Section("SECTION_DIAGNOSTICS");
        var experimental = new CheckBox { Content = L.Text("INCLUDE_EXPERIMENTAL"), IsChecked = data.IncludeExperimental };
        RoutedEventHandler experimentalChanged = (_, _) => {
            bool prior = data.IncludeExperimental; data.IncludeExperimental = experimental.IsChecked == true;
            try { Save(); } catch { data.IncludeExperimental = prior; throw; } BuildCards();
        };
        experimental.Checked += experimentalChanged; experimental.Unchecked += experimentalChanged;
        diagnostics.Children.Add(SettingsRow(experimental,"EXPERIMENTAL_HINT"));
        diagnostics.Children.Add(SettingsAction("DIAGNOSTICS", ExportDiagnostics));

        var recovery = Section("SECTION_RECOVERY");
        recovery.Children.Add(Text(L.Text("SETTINGS_HINT"), 12, Muted));
        recovery.Children.Add(SettingsAction("RECOVER", () => _ = Run(ResolveRecovery)));
        return panel;
    }
}

