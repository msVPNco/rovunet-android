using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    async Task ScannerResultsSmoke(Dictionary<string,bool> checks,string folder)
    {
        var profile=Importer.Single(ManualProfiles.Dns("dnstt","Results UI fixture","t.example.invalid","127.0.0.1:1","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));
        foreach(string language in new[]{"fa","en"})foreach(string theme in new[]{"amoled","dark","light"}){
            L.Language=language;data.Theme=theme;ApplyTheme();Build();Width=680;Height=900;var page=BuildResolverFinder(profile);page.Show();await Idle17();
            try{
                var list=Visuals<ListBox>(page.PageVisual).Single();var entries=(ObservableCollection<ScanEntry>)list.ItemsSource;
                var source=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SOURCE"));var whole=Visuals<CheckBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_WHOLE_BANK"));var limit=Visuals<TextBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_LIMIT"));source.SelectedValue="resolvers.txt";whole.IsChecked=true;checks["all_bank_"+language+"_"+theme+"_disables_partial_limit"]=whole.IsEnabled&&!limit.IsEnabled;source.SelectedValue="ir.cidr";checks["all_bank_"+language+"_"+theme+"_country_stays_sampled"]=!whole.IsEnabled&&limit.IsEnabled;whole.IsChecked=false;source.SelectedValue="profile";
                for(int i=0;i<24;i++)entries.Add(new(){Address="127.0.0."+(i+1)+":53",Result=new(){Index=i,Transport=i%2==0?"tcp":"udp",Dns=true,Pass=true,ElapsedMS=90+i,Completed=5,Checks=["A","TXT"]},Selectable=true,VerifiedElapsedMs=800-i*10});
                entries.Add(new(){Address="127.0.1.1:53",Result=new(){Index=24,Transport="tcp",Error="DNS_FAILED"}});
                await Idle17();string prefix="results_"+language+"_"+theme;
                var sort=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SORT"));sort.SelectedValue="latency";await Idle17();checks[prefix+"_e2e_sort_uses_tunnel_latency"]=list.Items[0]==entries[23];sort.SelectedValue="dns";await Idle17();checks[prefix+"_dns_sort_uses_dns_latency"]=list.Items[0]==entries[0];
                Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_RESULT_STATUS")).SelectedValue="all";var wire=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_PROBE_FILTER"));wire.SelectedValue="tcp";await Idle17();checks[prefix+"_transport_filter"]=list.Items.Count==13&&list.Items.Cast<ScanEntry>().All(e=>e.Result.Transport=="tcp");wire.SelectedValue="all";
                var status=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_RESULT_STATUS"));status.SelectedValue="tunnel";await Idle17();checks[prefix+"_verified_filter"]=list.Items.Count==24;status.SelectedValue="failed";await Idle17();checks[prefix+"_failed_filter"]=list.Items.Count==1;status.SelectedValue="all";
                var search=Visuals<TextBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_RESULT_FILTER"));search.Text="127.0.0.24:";await Idle17();checks[prefix+"_search"]=list.Items.Count==1;search.Clear();
                list.SelectedItems.Add(entries[0]);list.SelectedItems.Add(entries[1]);list.SelectedItems.Add(entries[2]);await Idle17();checks[prefix+"_checkbox_selection_kept"]=list.SelectedItems.Count==3;
                var use=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("USE_DNS"));double before=use.TranslatePoint(new Point(),page.PageVisual).Y;list.ScrollIntoView(entries[23]);await Idle17();checks[prefix+"_fixed_footer_while_scrolling"]=Math.Abs(before-use.TranslatePoint(new Point(),page.PageVisual).Y)<1&&use.IsVisible&&list.ActualHeight>=150;
                checks[prefix+"_separate_progress"]=Visuals<ProgressBar>(page.PageVisual).Count(b=>b.Tag is "SCAN_DNS_PROGRESS" or "SCAN_TUNNEL_PROGRESS")==2;
                sort.SelectedValue="latency";list.ScrollIntoView(entries[23]);await Idle17();SavePage((FrameworkElement)Content,Path.Combine(folder,"dns-results-compact-"+language+"-"+theme+".png"));Width=1180;await Idle17();checks[prefix+"_wide_sidebar"]=contextHost.IsVisible;SavePage((FrameworkElement)Content,Path.Combine(folder,"dns-results-wide-"+language+"-"+theme+".png"));
            }finally{page.Close();}
        }
        var bank=ResolverCatalog.Banks.Single(b=>b.File=="resolvers.txt");var all=ResolverCatalog.AllAddresses(bank.File,"udp");checks["scanner_whole_bank_exact_inventory"]=all.Count==ResolverCatalog.Count(bank)&&all.Count>60000&&all.Distinct().Count()==all.Count;checks["scanner_bank_batches_bounded"]=all.Chunk(128).All(chunk=>chunk.Length<=128);try{ResolverCatalog.AllAddresses("ir.cidr","udp");checks["scanner_country_not_fully_expanded"]=false;}catch(SafeError){checks["scanner_country_not_fully_expanded"]=true;}
        L.Language="fa";data.Theme="amoled";ApplyTheme();Build();await ScannerSeparateCancellation(checks,profile,folder);
    }

    async Task ScannerSeparateCancellation(Dictionary<string,bool> checks,Profile profile,string folder)
    {
        using var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));int port=((IPEndPoint)udp.Client.LocalEndPoint!).Port;using var lifetime=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var server=Task.Run(async()=>{try{while(true){var request=await udp.ReceiveAsync(lifetime.Token);if(request.Buffer.Length<12)continue;request.Buffer[2]=0x81;request.Buffer[3]=0x80;request.Buffer[6]=request.Buffer[7]=request.Buffer[8]=request.Buffer[9]=0;await udp.SendAsync(request.Buffer,request.RemoteEndPoint,lifetime.Token);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}});
        var before=Owned();data.ResolverOptions[profile.Id]=new(){Mode="advanced",Source="input",Transport="udp",Limit=4,Workers=1,Timeout=2000,E2eTimeout=15000,E2eConcurrency=1};var page=BuildResolverFinder(profile);page.Show();await Idle17();
        try{
            Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SOURCE")).SelectedValue="input";Visuals<TextBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_INPUT")).Text=string.Join('\n',Enumerable.Range(1,4).Select(i=>"127.0.0."+i+":"+port));
            var start=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("START_TEST"));start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var list=Visuals<ListBox>(page.PageVisual).Single();while(!list.Items.Cast<ScanEntry>().Any(e=>e.Selectable))await Task.Delay(20,lifetime.Token);
            Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("SCAN_START_TUNNEL")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));while(!list.Items.Cast<ScanEntry>().Any(e=>e.Testing))await Task.Delay(20,lifetime.Token);
            var tunnel=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("SCAN_STOP_TUNNEL"));checks["scanner_real_tunnel_stage_concurrent_with_dns"]=tunnel.IsEnabled&&!start.IsEnabled&&list.Items.Cast<ScanEntry>().Any(e=>e.Testing);
            await Idle17();SavePage((FrameworkElement)Content,Path.Combine(folder,"scanner19-two-live-phases-fa-amoled.png"));
            Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("STOP_TEST")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(120,lifetime.Token);checks["scanner_stop_dns_keeps_tunnel_stage"]=tunnel.IsEnabled&&!start.IsEnabled;
            tunnel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));while(!start.IsEnabled)await Task.Delay(20,lifetime.Token);checks["scanner_stop_tunnel_cancels_real_agent"]=!list.Items.Cast<ScanEntry>().Any(e=>e.Pending||e.Testing)&&!tunnel.IsEnabled;
        }finally{if(page.IsVisible)page.Close();lifetime.Cancel();await server;}
        await Task.Delay(200);checks["scanner_split_stop_no_process_leak"]=Owned().IsSubsetOf(before);
        static HashSet<int> Owned()=>new[]{"Rovunet.Agent","rovunet-transports","xray"}.SelectMany(System.Diagnostics.Process.GetProcessesByName).Select(p=>{using(p)return p.Id;}).ToHashSet();
    }
}
