using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    async Task ScannerSelectionSmoke(Dictionary<string,bool> checks,string report)
    {
        var profile=Importer.Single(ManualProfiles.Dns("dnstt","DNS selection fixture","t.example.invalid","1.1.1.1:53","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));string original=profile.Raw;
        foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"light","dark"}){
            L.Language=language;data.Theme=theme;ApplyTheme();Build();Width=520;Height=900;Profile? applied=null;
            data.ScannerViews[profile.Id]=new(){Score=0};var scanner=BuildResolverFinder(profile,p=>applied=p);scanner.Show();await Idle();
            try{
                var list=Visuals<ListBox>(scanner.PageVisual).Single();var entries=(ObservableCollection<ScanEntry>)list.ItemsSource;
                var bad=new ScanEntry{Address="127.0.0.1:1",Result=new(){Index=0,Transport="udp",Error="DNS_FAILED"}};
                var good=new[]{("1.1.1.1:53",120L),("8.8.8.8:53",40L),("9.9.9.9:53",80L)}.Select((p,i)=>new ScanEntry{Address=p.Item1,Result=new(){Index=i+1,Transport="udp",Pass=true,Dns=true,ElapsedMS=p.Item2},Selectable=true}).ToArray();
                entries.Add(bad);foreach(var entry in good)entries.Add(entry);entries.Add(new(){Address="127.0.0.2:53",Result=new(){Index=4,Transport="udp"},Pending=true});entries.Add(new(){Address="127.0.0.3:53",Result=new(){Index=5,Transport="udp"},Stale=true});
                await Idle();string key="scan_selection_"+language+"_"+theme;
                checks[key+"_passed_first"]=list.Items.Cast<ScanEntry>().Take(3).All(e=>e.Selectable);
                foreach(var entry in good){list.ScrollIntoView(entry);await Idle();var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(entry);var check=Visuals<CheckBox>(container).Single();var provider=(IToggleProvider)new CheckBoxAutomationPeer(check).GetPattern(PatternInterface.Toggle);provider.Toggle();}
                checks[key+"_three_real_checkbox_toggles"]=list.SelectedItems.Count==3;
                var sort=Visuals<ComboBox>(scanner.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SORT"));sort.SelectedValue="dns";await Idle();
                checks[key+"_latency_sort_keeps_selection"]=list.Items[0]==good[1]&&list.Items[1]==good[2]&&list.SelectedItems.Count==3;
                list.ScrollIntoView(good[1]);await Idle();var green=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(good[1]);checks[key+"_green_ping"]=Visuals<TextBlock>(green).Any(t=>t.Text=="40 ms"&&Same(t.Foreground,Green));
                Visuals<ComboBox>(scanner.PageVisual).Single(c=>Equals(c.Tag,"SCAN_RESULT_STATUS")).SelectedValue="all";await Idle();list.ScrollIntoView(bad);await Idle();var failed=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(bad);checks[key+"_red_failure_reason"]=Visuals<TextBlock>(failed).Any(t=>t.Text.Contains(L.Text("DNS_FAILED"))&&Same(t.Foreground,Red));checks[key+"_failed_checkbox_disabled"]=failed.IsEnabled&&!Visuals<CheckBox>(failed).Single().IsEnabled;
                sort.SelectedValue="address";await Idle();checks[key+"_address_sort"]=list.Items.Cast<ScanEntry>().Select(e=>e.Address).SequenceEqual(entries.OrderBy(e=>e.AddressSortKey,StringComparer.Ordinal).Select(e=>e.Address));
                sort.SelectedValue="order";await Idle();checks[key+"_scan_order_sort"]=list.Items[0]==bad;
                var mixed=new ScanEntry{Address="4.4.4.4:53",Result=new(){Index=6,Transport="tcp",Pass=true,Dns=true,ElapsedMS=90},Selectable=true};entries.Add(mixed);list.SelectedItems.Add(mixed);
                var use=Visuals<Button>(scanner.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("USE_DNS"));use.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[key+"_mixed_transport_refused"]=applied==null&&scanner.IsVisible&&Visuals<TextBlock>(scanner.PageVisual).Any(t=>t.Text==L.Text("SCAN_SAME_TRANSPORT"));list.SelectedItems.Remove(mixed);entries.Remove(mixed);
                sort.SelectedValue="latency";list.ScrollIntoView(good[1]);scanner.RestoreScroll(double.MaxValue);await Idle();SavePage(scanner.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,"scanner-selection-"+language+"-"+theme+".png"));
                sort.SelectedValue="order";list.ScrollIntoView(bad);await Idle();SavePage(scanner.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,"scanner-failure-"+language+"-"+theme+".png"));
                use.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks[key+"_three_selected_applied"]=applied!=null&&Importer.Fields(applied.Raw)[4].Split(',').Length==3&&Importer.Fields(applied.Raw)[22]=="udp"&&!scanner.IsVisible;
                checks[key+"_original_and_main_window_preserved"]=profile.Raw==original&&NavigationDepth==0;
            }finally{if(scanner.IsVisible)scanner.Close();}
        }
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        static bool Same(Brush a,Brush b)=>a is SolidColorBrush x&&b is SolidColorBrush y&&x.Color==y.Color;
    }
}
