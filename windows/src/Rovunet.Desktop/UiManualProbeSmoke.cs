using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    async Task<Profile> RunManualProbeNetworkFixture(Profile profile,string[] entries,Dictionary<string,bool> checks)
    {
        var effective=GlobalResolvers.ForConnection(profile,true,entries[0]).Effective;data.Profiles=[effective];data.SelectedId=effective.Id;data.ExpandedCollections[CollectionKey(effective)]=true;Build();networkFixtureMode=true;
        using var sink=new TcpListener(IPAddress.Loopback,0);sink.Start();string url="https://127.0.0.1:"+((IPEndPoint)sink.LocalEndpoint).Port+"/";data.LatencyUrl=url;
        var agent=client=new AgentClient(probeOnly:true);await agent.Start();var ready=await agent.Send(new(){Action="connect",Profile=effective,SystemProxy=false});activeProfile=effective;SetState(ready);
        checks["manual_actual_tunnel_connected"]=ready.State=="connected";int? process=agent.AgentProcessId;
        try{
            var accepted=sink.AcceptTcpClientAsync();var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _=Dispatcher.BeginInvoke(async()=>{
                DialogWindow? page=null;
                try{
                    page=DialogWindow.OpenPages.Single();var start=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("VERIFY_HTTPS"));var stop=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("STOP_TEST"));
                    start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));using var connection=await accepted.WaitAsync(TimeSpan.FromSeconds(25));
                    checks["manual_https_stall_observed"]=busy&&!start.IsEnabled&&stop.IsVisible&&stop.IsEnabled;
                    stop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));var limit=DateTime.UtcNow.AddSeconds(4);while(!start.IsEnabled&&DateTime.UtcNow<limit)await Task.Delay(20);
                    checks["manual_stop_returns_without_tunnel_teardown"]=start.IsEnabled&&!busy&&state.State=="connected"&&agent.AgentProcessId==process;
                    checks["manual_cancel_is_reported"]=Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text.Contains(L.Text("CANCELLED")));
                    checks["manual_cancel_does_not_save_failed_ping"]=data.Measurements.Count==0;
                    start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));stop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));limit=DateTime.UtcNow.AddSeconds(4);while(!start.IsEnabled&&DateTime.UtcNow<limit)await Task.Delay(20);
                    checks["manual_immediate_cancel_completes"]=start.IsEnabled&&!busy&&state.State=="connected";
                    page.Close();checks["manual_back_same_main_window"]=NavigationDepth==0;completed.TrySetResult();
                }catch(Exception e){completed.TrySetException(e);}finally{if(page?.IsVisible==true)page.Close();}
            },DispatcherPriority.ApplicationIdle);
            ProbeDialog();await completed.Task;
            var status=await agent.Send(new(){Action="status"});checks["manual_same_agent_still_connected"]=status.State=="connected"&&agent.AgentProcessId==process;
            checks["manual_public_https_after_cancel"]=status.State=="connected"&&(await agent.Send(new(){Action="probe",ProbeUrl="https://example.com/"})).Verified;
        }finally{agent.Abort();await agent.DisposeAsync();client=null;activeProfile=null;networkFixtureMode=false;lastError="";SetState(new());}
        return effective;
    }
}
