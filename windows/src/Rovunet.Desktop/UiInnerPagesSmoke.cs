using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunInnerPagesSmoke(string report)
    {
        var checks=new Dictionary<string,bool>();var beforeProxy=new Rovunet.Platform.WinInetProxy().Read();
        var profile=Importer.Single(ManualProfiles.Dns("slipstream","slipstream-socks","s.example.invalid","127.0.0.1:5353","udp","","127.0.0.1","22","fixture","fixture"));
        profile.Raw=ProfileEditing.ChangeNative(profile,new Dictionary<int,string>{{4,"127.0.0.1:5353:1"}});profile.SourceId="synthetic-source";data.Profiles=[profile];data.SelectedId=profile.Id;
        try
        {
            foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"dark","light"})
            {
                L.Language=language;data.Theme=theme;ApplyTheme();Build();
                foreach(string page in new[]{"settings","scanner","editor"})
                {
                    DialogWindow d;
                    if(page=="settings"){d=Dialog("SETTINGS",500,800);d.UsePageChrome();d.Content=BuildSettingsPanel(()=>d.Close());}
                    else d=page=="scanner"?BuildResolverFinder(profile):BuildDnsProfileEditor(profile);
                    d.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);d.UpdateLayout();
                    SavePage(d.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,page+"-"+language+"-"+theme+".png"));
                    checks[page+"_"+language+"_"+theme+"_responsive"]=d.ActualWidth<=ActualWidth;
                    checks[page+"_"+language+"_"+theme+"_back_target"]=Visuals<Button>(d.PageVisual).Any(b=>AutomationProperties.GetName(b)==L.Text("BACK")&&b.ActualHeight>=44);
                    if(page=="settings")
                    {
                        Visuals<Button>(d.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("EDIT_RESOLVERS")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));d.UpdateLayout();
                        checks["settings_"+language+"_"+theme+"_resolver_editor_opens"]=Visuals<Button>(d.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("GLOBAL_RESOLVER_SAVE")).IsVisible;
                        Visuals<Button>(d.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("EDIT_DNS_POOL")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));d.UpdateLayout();
                        checks["settings_"+language+"_"+theme+"_pool_editor_opens"]=Visuals<Button>(d.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("DNS_POOL_SAVE")).ActualHeight>=44;
                    }
                    if(page=="editor"){
                        var resolver=Visuals<TextBox>(d.PageVisual).Single(t=>Equals(t.Tag,"DNS_RESOLVERS"));
                        checks["editor_"+language+"_"+theme+"_wire_flag_hidden"]=resolver.Text=="127.0.0.1:5353";
                        checks["editor_"+language+"_"+theme+"_keepalive_preserved"]=Visuals<TextBox>(d.PageVisual).Single(t=>Equals(t.Tag,"SLIP_INTERVAL")).Text=="5000";
                    }
                    if(page=="scanner"){
                        var transport=Visuals<ComboBox>(d.PageVisual).Single(c=>Equals(c.Tag,"TRANSPORT"));
                        checks["scanner_"+language+"_"+theme+"_udp_only"]=transport.Items.Count==1 && transport.SelectedValue?.ToString()=="udp";
                        var source=Visuals<ComboBox>(d.PageVisual).Single(c=>Equals(c.Tag,"SCAN_SOURCE"));
                        checks["scanner_"+language+"_"+theme+"_source_label_readable"]=source.Visibility==Visibility.Collapsed&&Visuals<Button>(d.PageVisual).Any(b=>b.Content?.ToString()==L.Text("SCAN_SOURCE_PROFILE")&&b.MinHeight>=44);
                        Visuals<Button>(d.PageVisual).Single(b=>b.Content?.ToString()==L.Text("SCAN_ADVANCED")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        checks["scanner_"+language+"_"+theme+"_mode_chips_work"]=Visuals<ComboBox>(d.PageVisual).Single(c=>Equals(c.Tag,"SCAN_MODE")).SelectedValue?.ToString()=="advanced";
                    }
                    d.Width=680;d.Height=820;d.UpdateLayout();
                    checks[page+"_"+language+"_"+theme+"_narrow_controls"]=Visuals<Button>(d.PageVisual).Where(b=>b.IsVisible).All(b=>b.ActualWidth>=44&&b.ActualHeight>=44&&b.ActualWidth<=680);
                    Visuals<Button>(d.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("BACK")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[page+"_"+language+"_"+theme+"_back_closes"]=!d.IsVisible;
                }
            }
            L.Language="en";data.Theme="dark";ApplyTheme();Build();
            // Exercise the production modal editor, including invalid save and immutable subscription wire.
            var editor=BuildDnsProfileEditor(profile);string original=profile.Raw;
            _ = Dispatcher.BeginInvoke(()=>{
                try{
                    var name=Visuals<TextBox>(editor.PageVisual).Single(t=>Equals(t.Tag,"PROFILE_NAME"));var resolver=Visuals<TextBox>(editor.PageVisual).Single(t=>Equals(t.Tag,"DNS_RESOLVERS"));var save=Visuals<Button>(editor.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("SAVE"));
                    name.Text="Renamed fixture";resolver.Text="invalid";save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks["invalid_editor_save_keeps_original"]=ReferenceEquals(data.Profiles[0],profile)&&profile.Raw==original&&editor.IsVisible;
                    resolver.Text="127.0.0.1:5353";Visuals<CheckBox>(editor.PageVisual).Single(c=>c.Content?.ToString()==L.Text("AUTHORITATIVE")).IsChecked=true;save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    var fields=Importer.Fields(data.Profiles[0].Raw);checks["valid_editor_save_keeps_flags_and_interval"]=fields[4]=="127.0.0.1:5353:1"&&fields[5]=="1"&&fields[6]=="5000";
                    checks["subscription_origin_and_identity_preserved"]=profile.Raw==original&&data.Profiles[0].Id==profile.Id&&data.LocalOverrideSources[profile.Id]==original;
                }catch(Exception e){checks["editor_failure_"+e.GetType().Name]=false;}finally{if(editor.IsVisible)editor.Close();}
            },DispatcherPriority.ApplicationIdle);
            editor.ShowDialog();
            // A stored TCP-only profile must offer only TCP in both chips and actual selector.
            var tcp=Importer.Single(ManualProfiles.Dns("sayedns","TCP fixture","t.example.invalid","127.0.0.1:5353","tcp",new string('a',64),"127.0.0.1","22","fixture","fixture"));var scan=BuildResolverFinder(tcp);scan.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var wire=Visuals<ComboBox>(scan.PageVisual).Single(c=>Equals(c.Tag,"TRANSPORT"));checks["scanner_tcp_only_retained"]=wire.Items.Count==1&&wire.SelectedValue?.ToString()=="tcp";
            scan.Close();await BankSmoke(checks,report,tcp);await ImportAndEndpointSmoke(checks,report);await ScannerSelectionSmoke(checks,report);checks["proxy_unchanged"]=beforeProxy==new Rovunet.Platform.WinInetProxy().Read();
        }
        catch(Exception e){checks["failure_"+e.GetType().Name]=false;foreach(Window d in Application.Current.Windows.Cast<Window>().ToArray())if(d!=this)d.Close();}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="WPF inner pages visual and interaction",synthetic_profiles=true,external_network_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
    }
    static void SavePage(FrameworkElement visual,string path)
    {
        var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(visual.ActualWidth)),Math.Max(1,(int)Math.Ceiling(visual.ActualHeight)),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var output=new FileStream(path,FileMode.CreateNew);png.Save(output);
    }
}
