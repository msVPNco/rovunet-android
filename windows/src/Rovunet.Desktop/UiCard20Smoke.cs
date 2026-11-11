using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunCard20Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();var before=new WinInetProxy().Read();var ownedBefore=Owned();networkFixtureMode=true;recoverConnection=()=>Task.CompletedTask;
        try{
            var first=Importer.Single("socks5://127.0.0.1:9#First");var second=Importer.Single("socks5://127.0.0.1:10#Second");foreach(var p in new[]{first,second}){p.CollectionId="card20";p.CollectionName="Gesture fixture";}data.Profiles=[first,second];data.SelectedId="";data.ExpandedCollections["card20"]=true;Build();await Idle();
            void Click(Border tile){tile.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});tile.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});}
            var tile=rows.Single(r=>r.Profile.Id==first.Id).Tile;Click(tile);checks["first_routed_click_only_selects"]=data.SelectedId==first.Id&&client==null&&state.State=="disconnected";
            Click(tile);await Settled();checks["second_routed_click_starts_isolated_agent"]=state.State=="connected"&&activeProfile?.Id==first.Id&&client?.AgentProcessId>0;
            var oldClient=client;tile=rows.Single(r=>r.Profile.Id==second.Id).Tile;Click(tile);checks["selecting_other_card_keeps_active_connection"]=data.SelectedId==second.Id&&activeProfile?.Id==first.Id&&ReferenceEquals(client,oldClient);
            Click(tile);await Settled();checks["second_click_switches_without_confirmation_page"]=state.State=="connected"&&activeProfile?.Id==second.Id&&!ReferenceEquals(client,oldClient)&&NavigationDepth==0;
            foreach(var row in rows){
                var body=(Grid)row.Tile.Child;var tools=body.Children.OfType<StackPanel>().Single(p=>Grid.GetColumn(p)==2);var edge=tools.TranslatePoint(new Point(),row.Tile);double boundary=Math.Min(edge.X,edge.X+tools.ActualWidth);
                checks["whole_body_hit_test_"+row.Profile.Name]=new[]{new Point(12,row.Tile.ActualHeight/2),new Point(row.Tile.ActualWidth/2,row.Tile.ActualHeight/2),new Point(row.Tile.ActualWidth-12,row.Tile.ActualHeight/2)}.All(point=>row.Tile.InputHitTest(point)!=null);
            }
            await Run(Disconnect);data.SelectedId="";checks["tray_can_connect_without_selected_card"]=CanConnectFromTray20();ToggleTrayConnection();await Settled();checks["empty_selection_connects_to_one_available_profile"]=state.State=="connected"&&activeProfile!=null&&data.Profiles.Any(p=>p.Id==activeProfile.Id);
            await Run(Disconnect);checks["windows_proxy_unchanged"]=before==new WinInetProxy().Read();
        }catch(Exception error){checks[error is SafeError safe?safe.Code:error.GetType().Name]=false;}
        finally{if(client!=null)try{await Run(Disconnect);}catch{client?.Abort();}networkFixtureMode=false;}
        await Task.Delay(200);checks["no_isolated_agent_or_core_left"]=Owned().IsSubsetOf(ownedBefore);bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Actual WPF routed card gestures with isolated local proxy readiness and agent replacement",externalDataTransferTested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);UpdateLayout();}
        async Task Settled(){if(operationDone!=null)await operationDone.Task.WaitAsync(TimeSpan.FromSeconds(25));await Idle();}
        static HashSet<int> Owned()=>new[]{"Rovunet.Agent","xray"}.SelectMany(Process.GetProcessesByName).Select(p=>{using(p)return p.Id;}).ToHashSet();
    }
}
