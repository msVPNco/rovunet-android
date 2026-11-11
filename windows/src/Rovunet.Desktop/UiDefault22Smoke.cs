using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunDefault22Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        try{
            foreach(string language in new[]{"fa","en","ru","zh-CN"}){
                L.Language=language;data.ResolverOptions["standalone"]=new(){Source="resolvers.txt",WholeBank=false,Limit=128};
                ApplyTheme();Build();var page=BuildResolverFinder(null);page.Show();await Idle();
                var source=Visuals<ComboBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SOURCE"));var limit=Visuals<TextBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_LIMIT"));var whole=Visuals<CheckBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_WHOLE_BANK"));
                checks[language+"_old_128_preference_uses_whole_default"]=source.SelectedValue?.ToString()=="resolvers.txt"&&whole.IsChecked==true&&!limit.IsEnabled&&!limit.IsVisible;
                var status=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"DNS_BANK_STATUS"));
                checks[language+"_bank_and_test_counts_separate"]=status.Text.Contains(L.Text("SCAN_SOURCE_APP"))&&status.Text.Split('\n').Length==2&&status.Text.Contains("60");
                source.SelectedValue="ir.cidr";limit.Text="17";await Idle();checks[language+"_country_keeps_selected_limit"]=limit.IsEnabled&&status.Text.Contains("17");
                source.SelectedValue="resolvers.txt";await Idle();checks[language+"_reselect_default_is_full"]=whole.IsChecked==true&&!limit.IsEnabled;
                if(language is "fa" or "en")SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"default-bank-"+language+".png"));page.Close();
            }
            var candidates=ResolverCatalog.TestCandidates("resolvers.txt","udp",128).ToArray();checks["default_actual_plan_is_60370_unique"]=candidates.Length==60370&&candidates.Distinct().Count()==60370;
            checks["country_actual_plan_remains_bounded"]=ResolverCatalog.TestCandidates("ir.cidr","tcp",17).Count==17;
            ScannerSession19? session=null;int batchSize=0;
            var plan=new ScannerPlan19(new(){Source="resolvers.txt",Mode="advanced",WholeBank=true},candidates,["udp"],"dnstt","example.invalid","",false);
            session=new(plan,new ObservableCollection<ScanEntry>(),async(request,callback,token)=>{batchSize=((string[])request.GetType().GetProperty("Candidates")!.GetValue(request)!).Length;await callback(new(){Index=0,Transport="udp",Pass=true,Dns=true});session!.StopDns();token.ThrowIfCancellationRequested();},(_,_,_,_)=>throw new InvalidOperationException());
            await session.ResumeDns();checks["session_total_is_inventory_not_batch"]=session.Total==60370&&session.Scanned==1&&batchSize==128&&!session.DnsRunning&&session.Error.Length==0;
            var p=new ScannerPresentation(new(),new(),new(),new(),new(),new(),new(),new(),new(),new(),new(),new(),new(),new(),new TextBlock(),new TextBlock());
            checks["results_have_separate_bank_scope"]=Equals(p.BankScope.Tag,"SCAN_BANK_SCOPE");
        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{checks,passed,network="Mock worker, no external scan; complete candidate inventory and real WPF settings"},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
