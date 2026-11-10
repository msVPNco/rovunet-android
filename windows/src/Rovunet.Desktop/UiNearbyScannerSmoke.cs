using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    async Task NearbyScannerSmoke(Dictionary<string,bool> checks)
    {
        using var listener=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));int port=((IPEndPoint)listener.Client.LocalEndPoint!).Port;
        using var lifetime=new CancellationTokenSource(TimeSpan.FromSeconds(15));int received=0;
        var responder=Task.Run(async()=>{try{while(true){var request=await listener.ReceiveAsync(lifetime.Token);if(request.Buffer.Length<12)continue;Interlocked.Increment(ref received);var reply=request.Buffer;reply[2]=0x81;reply[3]=0x80;reply[6]=reply[7]=reply[8]=reply[9]=0;await listener.SendAsync(reply,request.RemoteEndPoint,lifetime.Token);}}catch(OperationCanceledException){}catch(ObjectDisposedException){}});
        var before=System.Diagnostics.Process.GetProcessesByName("rovunet-transports").Select(p=>{using(p)return p.Id;}).ToHashSet();
        var profile=Importer.Single(ManualProfiles.Dns("dnstt","Nearby fixture","t.example.invalid","127.0.0.1:1","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));string original=profile.Raw;
        data.ResolverOptions[profile.Id]=new(){Mode="advanced",Transport="udp",Source="input",Limit=1,Workers=4,Timeout=1000,Nearby=true,Shuffle=false};
        var page=BuildResolverFinder(profile);page.Show();await Idle17();
        try{
            Visuals<ComboBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SOURCE")).SelectedValue="input";
            Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"SCAN_INPUT")).Text="127.0.0.1";
            Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"SCAN_PORT")).Text=port.ToString();
            var start=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("START_TEST"));start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var list=Visuals<ListBox>(page.PageVisual).Single();var all=(System.Collections.ObjectModel.ObservableCollection<ScanEntry>)list.ItemsSource;while(all.Count<2&&!start.IsEnabled)await Task.Delay(20,lifetime.Token);
            checks["scanner_custom_port_reaches_loopback"]=received>0&&all.Any(e=>e.Address=="127.0.0.1:"+port&&e.Selectable&&e.Result.Dns);
            checks["scanner_success_expands_nearby_candidates"]=all.Count>=2&&all.All(e=>e.Address.StartsWith("127.0.0.")&&e.Address.EndsWith(":"+port));
            Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("STOP_TEST")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            while(!start.IsEnabled)await Task.Delay(20,lifetime.Token);
            checks["scanner_nearby_stop_releases_worker"]=list.Items.Count<=ResolverScan.MaximumCandidates&&!list.Items.Cast<ScanEntry>().Any(e=>e.Pending);
            checks["scanner_nearby_keeps_original_profile"]=profile.Raw==original;
        }finally{page.Close();lifetime.Cancel();await responder;}
        await Task.Delay(100);var after=System.Diagnostics.Process.GetProcessesByName("rovunet-transports").Select(p=>{using(p)return p.Id;}).ToHashSet();checks["scanner_nearby_no_worker_leak"]=after.IsSubsetOf(before);
    }
}
