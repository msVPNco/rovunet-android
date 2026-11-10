using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunScanner20Smoke(string report)
    {
        async Task Idle20(){await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);await Task.Delay(100);}
        var checks=new Dictionary<string,bool>();var before=new WinInetProxy().Read();
        try{
            foreach(var shape in new[]{(Mode:"e2e",Family:"dnstt"),(Mode:"simple",Family:"soar")}){
                var entries=new ObservableCollection<ScanEntry>();var options=new ScanOptions{Mode=shape.Mode,E2eConcurrency=2};var plan=new ScannerPlan19(options,["127.0.0.1:53","127.0.0.2:53","127.0.0.3:53"],["udp"],shape.Family,"t.example.invalid",new string('a',64),true);
                int dnsCalls=0,tunnelCalls=0;bool hold=true;var active=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                async Task<AgentReply> Verify(ScanEntry entry,string method,Action<string> phase,CancellationToken stop){tunnelCalls++;phase("setup");active.TrySetResult();if(hold)await Task.Delay(Timeout.Infinite,stop);return new(){TunnelEstablished=entry.Address!="127.0.0.2:53",ElapsedMs=120,Error=entry.Address=="127.0.0.2:53"?"DNS_TUNNEL_FAILED":""};}
                var session=new ScannerSession19(plan,entries,(_,_,_)=>{dnsCalls++;throw new InvalidOperationException();},Verify);var run=session.ResumeDns();await active.Task;
                checks[shape.Mode+"_no_fake_dns_progress"]=dnsCalls==0&&session.Scanned==0&&session.Working==0&&entries.All(e=>!e.Selectable&&!e.Result.Pass&&!e.Result.Dns)&&session.Total==3;
                await session.StopAll();await run;checks[shape.Mode+"_cancel_preserves_unfinished_tests"]=session.TunnelCompleted==0&&session.CanResumeTunnel&&entries.All(e=>!e.TunnelFinished&&!e.Testing&&!e.Pending);
                hold=false;await session.StartTunnel(false);checks[shape.Mode+"_resume_measures_actual_server_results"]=session.Scanned==3&&session.Working==2&&session.TunnelCompleted==3&&entries.Count(e=>e.Selectable)==2&&entries.All(e=>e.DnsPing=="—")&&dnsCalls==0;
                int calls=tunnelCalls;await session.StartTunnel(false);checks[shape.Mode+"_finished_results_not_retested_on_resume"]=calls==tunnelCalls;
                var saved=DecodeScannerCheckpoint19(EncodeScannerCheckpoint19(session.Checkpoint("fixture20")),"fixture20");var restored=new ObservableCollection<ScanEntry>();RestoreScannerRows19(saved,restored);var resumed=new ScannerSession19(plan,restored,(_,_,_)=>throw new InvalidOperationException(),Verify,true);checks[shape.Mode+"_restored_results_keep_real_progress"]=resumed.Scanned==3&&resumed.Working==2&&restored.Count(e=>e.Selectable)==2;
            }
            checks["transport_labels_preserve_wire_values"]=DnsTransportLabel20("tls")=="DoT"&&DnsTransportLabel20("https")=="DoH"&&DnsTransportLabel20("tcp")=="TCP"&&DnsTransportLabel20("udp")=="UDP";
            checks["local_means_network_dns_not_loopback"]=NetworkResolvers20().All(a=>DnsProbe.Endpoint(a).Port==53&&!IPAddress.IsLoopback(DnsProbe.Endpoint(a).Address));
            var profile=Importer.Single(ManualProfiles.Dns("dnstt","Scanner20 fixture","t.example.invalid","127.0.0.1:9","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));data.Profiles=[profile];
            foreach(string language in new[]{"fa","en"})foreach(string theme in new[]{"amoled","light"}){
                L.Language=data.Language=language;data.Theme=theme;Width=680;Height=940;ApplyTheme();Build();var page=BuildResolverFinder(profile);page.Show();await Idle20();
                var source=Visuals<ComboBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SOURCE"));checks[language+theme+"_sources_are_touch_buttons"]=source.Visibility==Visibility.Collapsed&&source.Items.Count>=8;
                checks[language+theme+"_eligibility_reason_visible"]=Visuals<TextBlock>(page.PageVisual).Any(t=>Equals(t.Tag,"SCAN_STAGE_HINT")&&t.Text.Length>0);
                SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"scanner20-config-"+language+"-"+theme+".png"));page.Close();
                var editor=BuildDnsProfileEditor(profile);editor.Show();await Idle20();checks[language+theme+"_local_resolver_action_in_editor"]=Visuals<Button>(editor.PageVisual).Any(b=>Equals(b.Tag,"DNS_LOCAL_NETWORK")&&b.IsVisible);editor.Close();
            }
            checks["windows_proxy_unchanged"]=before==new WinInetProxy().Read();
        }catch(Exception error){checks[error is SafeError safe?safe.Code:error.GetType().Name]=false;}
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Scanner20 direct modes, truthful progress, cancel/resume and native UI",checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
