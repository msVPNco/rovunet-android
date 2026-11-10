using System.Windows;
using System.Windows.Controls;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void GlobalResolverSmoke(Dictionary<string, bool> checks)
    {
        bool enabled = data.GlobalResolverEnabled; string list = data.GlobalResolverList;
        var poolPrior = (data.DnsPoolEnabled,data.DnsPoolVerify,data.DnsPoolList);
        var original = data.Profiles.Select(p => p.Raw).ToArray();
        try
        {
            var panel = BuildSettingsPanel(() => { });
            var host = new Border { Child = panel, Width = 660 };
            host.Measure(new Size(660, double.PositiveInfinity)); host.Arrange(new Rect(0, 0, 660, host.DesiredSize.Height)); host.UpdateLayout();
            var input = Visuals<TextBox>(host).Single(t => Equals(t.Tag, "GlobalResolverList"));
            var toggle = Visuals<CheckBox>(host).Single(t => Equals(t.Content, L.Text("GLOBAL_RESOLVER_TITLE")));
            var save = Visuals<Button>(host).Single(t => Equals(t.Content, L.Text("GLOBAL_RESOLVER_SAVE")));
            toggle.IsChecked = true; input.Text = "1.1.1.1\n1.1.1.1:53\n[::1]:5353";
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["global_resolver_ui_saves_normalized_list"] = data.GlobalResolverEnabled && data.GlobalResolverList == "1.1.1.1:53\n[::1]:5353";
            string prior = data.GlobalResolverList;
            toggle.IsChecked = false; input.Text = "not-an-ip";
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["global_resolver_invalid_save_is_atomic"] = data.GlobalResolverEnabled && data.GlobalResolverList == prior;
            input.Clear(); save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["global_resolver_empty_can_be_saved"] = !data.GlobalResolverEnabled && data.GlobalResolverList == "";
            checks["global_resolver_setting_does_not_edit_profiles"] = original.SequenceEqual(data.Profiles.Select(p => p.Raw));
            var poolInput=Visuals<TextBox>(host).Single(t=>Equals(t.Tag,"DnsPoolList"));
            var poolToggle=Visuals<CheckBox>(host).Single(t=>Equals(t.Content,L.Text("DNS_POOL_TITLE")));
            var verifyToggle=Visuals<CheckBox>(host).Single(t=>Equals(t.Content,L.Text("DNS_POOL_VERIFY")));
            var poolSave=Visuals<Button>(host).Single(t=>Equals(t.Content,L.Text("DNS_POOL_SAVE")));
            poolToggle.IsChecked=true;verifyToggle.IsChecked=true;poolInput.Text="127.0.0.1\n127.0.0.1:53";
            poolSave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["dns_pool_ui_saves_options_and_deduplicates"]=data.DnsPoolEnabled&&data.DnsPoolVerify&&data.DnsPoolList=="127.0.0.1:53";
            poolToggle.IsChecked=false;poolInput.Text="invalid";poolSave.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["dns_pool_ui_invalid_save_atomic"]=data.DnsPoolEnabled&&data.DnsPoolList=="127.0.0.1:53";
            var encoded=System.Text.Json.JsonSerializer.Serialize(data);
            var decoded=System.Text.Json.JsonSerializer.Deserialize<Rovunet.Platform.UserData>(encoded)!;
            checks["dns_pool_preferences_json_roundtrip"]=decoded.DnsPoolEnabled&&decoded.DnsPoolVerify&&decoded.DnsPoolList==data.DnsPoolList;
            checks["dns_pool_ui_keeps_profiles_unchanged"]=original.SequenceEqual(data.Profiles.Select(p=>p.Raw));
        }
        finally { data.GlobalResolverEnabled = enabled; data.GlobalResolverList = list; (data.DnsPoolEnabled,data.DnsPoolVerify,data.DnsPoolList)=poolPrior; }
    }
}
