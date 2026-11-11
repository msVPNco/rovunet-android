using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    async Task BankSmoke(Dictionary<string,bool> checks,string report,Profile tcp)
    {
        foreach(var bank in ResolverCatalog.Banks){
            checks["bank_"+bank.File+"_embedded_nonempty"]=ResolverCatalog.Count(bank)>0;
            foreach(string mode in new[]{"udp","tcp","tls"}){var selected=ResolverCatalog.Candidates(bank.File,mode,3);checks["bank_"+bank.File+"_"+mode+"_bounded_candidates"]=selected.Count==3&&selected.Distinct().Count()==3&&selected.All(s=>DnsProbe.Endpoint(s).Port==(mode=="tls"?853:53));}
        }
        foreach(string lang in new[]{"en","fa"})foreach(string theme in new[]{"light","dark"}){
            L.Language=lang;data.Theme=theme;ApplyTheme();Build();string chosen="";var bankPage=BuildResolverBankPicker("ir_dns_lite.cidr",s=>chosen=s);bankPage.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);bankPage.UpdateLayout();
            SavePage(bankPage.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,"dns-bank-"+lang+"-"+theme+".png"));
            checks["bank_"+lang+"_"+theme+"_six_real_banks"]=Visuals<RadioButton>(bankPage.PageVisual).Count()==6;
            bankPage.Width=420;bankPage.UpdateLayout();checks["bank_"+lang+"_"+theme+"_touch"]=Visuals<Button>(bankPage.PageVisual).Where(b=>b.IsVisible).All(b=>b.ActualWidth>=44&&b.ActualHeight>=44);
            bankPage.Close();checks["bank_"+lang+"_"+theme+"_cancel_no_change"]=chosen=="";
        }
        L.Language="en";data.Theme="dark";ApplyTheme();Build();var scan=BuildResolverFinder(tcp);scan.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var source=Visuals<ComboBox>(scan.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SOURCE"));
        foreach(var bank in ResolverCatalog.Banks){
            Visuals<Button>(scan.PageVisual).Single(b=>b.Content?.ToString()==L.Text(bank.LabelKey)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["bank_"+bank.File+"_selection_reaches_scan_source"]=source.SelectedValue?.ToString()==bank.File;
            if(bank.File=="resolvers.txt"){checks["bank_default_complete_plan"]=ResolverCatalog.TestCandidates(bank.File,"tcp",1).Count==60370;continue;}
            Visuals<ComboBox>(scan.PageVisual).Single(b=>Equals(b.Tag,"SCAN_MODE")).SelectedValue="advanced";
            foreach(var field in Visuals<TextBox>(scan.PageVisual)){if(Equals(field.Tag,"SCAN_LIMIT")||Equals(field.Tag,"SCAN_WORKERS"))field.Text="1";if(Equals(field.Tag,"SCAN_TIMEOUT"))field.Text="200";}
            var start=Visuals<Button>(scan.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("START_TEST"));start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));using var bound=new CancellationTokenSource(TimeSpan.FromSeconds(15));while(!start.IsEnabled)await Task.Delay(25,bound.Token);
            checks["bank_"+bank.File+"_real_worker_produces_bounded_result"]=((System.Collections.ICollection)Visuals<ListBox>(scan.PageVisual).Single().ItemsSource).Count==1;
        }
        var wire=Visuals<ComboBox>(scan.PageVisual).Single(b=>Equals(b.Tag,"TRANSPORT"));checks["bank_selection_keeps_tcp_only"]=wire.Items.Count==1&&wire.SelectedValue?.ToString()=="tcp";scan.Close();
        var doh=Importer.Single(ManualProfiles.Dns("dnstt","DoH fixture","d.example.invalid","https://resolver.example.invalid/dns-query","https",new string('a',64),"127.0.0.1","22","fixture","fixture"));var https=BuildResolverFinder(doh);https.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        checks["ip_bank_does_not_downgrade_doh"]=!Visuals<Button>(https.PageVisual).Any(b=>b.Content?.ToString()==L.Text("SCAN_SOURCE_APP"));https.Close();
    }
}


