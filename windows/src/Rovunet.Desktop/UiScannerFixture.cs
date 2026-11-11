using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    // Entered only by the explicit synthetic loopback fixture. Uses production scanner controls/worker/Agent.
    async Task<Profile> RunScannerFixture(Profile profile,string[] entries,Dictionary<string,bool>? checks=null)
    {
        data.ResolverOptions[profile.Id]=new(){Mode="e2e",Transport=Importer.Fields(profile.Raw)[22],Source="input",Limit=entries.Length,Workers=1,Timeout=3000};
        var dialog=BuildResolverFinder(profile,p=>scannerFixtureResult=p);scannerFixtureResult=null;
        var completed=new TaskCompletionSource<Profile>(TaskCreationOptions.RunContinuationsAsynchronously);
        _=Dispatcher.BeginInvoke(async()=>{
            try
            {
                var source=Visuals<ComboBox>(dialog.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SOURCE"));source.SelectedValue="input";
                Visuals<TextBox>(dialog.PageVisual).Single(t=>Equals(t.Tag,"SCAN_INPUT")).Text=string.Join('\n',entries);
                var start=Visuals<Button>(dialog.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("START_TEST"));start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(145));
                while(!start.IsEnabled)await Task.Delay(25,stop.Token);
                var list=Visuals<ListBox>(dialog.PageVisual).Single();var valid=list.Items.Cast<ScanEntry>().Where(e=>e.Selectable).ToArray();
                if(valid.Length!=entries.Length)throw new SafeError("HTTPS_TEST_FAILED");foreach(var entry in valid)list.SelectedItems.Add(entry);
                var sort=Visuals<ComboBox>(dialog.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SORT"));sort.SelectedValue="latency";
                if(checks!=null){checks["scanner_all_candidates_https_verified"]=valid.All(e=>e.Selectable&&e.VerifiedElapsedMs.HasValue);checks["scanner_sort_keeps_multi_selection"]=list.SelectedItems.Count==entries.Length&&list.Items.Cast<ScanEntry>().Select(e=>e.Latency).SequenceEqual(valid.Select(e=>e.Latency).Order());checks["scanner_verified_ping_green"]=valid.All(e=>e.ResultBrush==Green&&e.Ping.EndsWith(" ms"));}
                var apply=Visuals<Button>(dialog.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("USE_DNS"));apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                completed.TrySetResult(scannerFixtureResult??throw new SafeError("DNS_SCAN_INVALID"));
            }
            catch(Exception error){completed.TrySetException(error);}
            finally{if(dialog.IsVisible)dialog.Close();}
        },DispatcherPriority.ApplicationIdle);
        dialog.ShowDialog();return await completed.Task;
    }
    Profile? scannerFixtureResult;
}
