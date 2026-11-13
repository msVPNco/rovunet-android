using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunProtocols25Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string folder=Path.GetDirectoryName(report)!;
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();await Task.Delay(50);}
        string[] links=["anytls://fixture@gateway.example.invalid:443?sni=tls.example.invalid&min-idle-session=2#AnyTLS","tuic://00000000-0000-4000-8000-000000000001:fixture@gateway.example.invalid:443?alpn=h3&udp_relay_mode=native#TUIC","mierus://user:fixture@gateway.example.invalid?profile=Mieru&port=443&protocol=UDP#Mieru"];
        try{
            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();data.Profiles=links.Select(Importer.Single).ToList();data.SelectedId=data.Profiles[0].Id;Build();await Idle();
                foreach(var p in data.Profiles.ToArray()){
                    var page=BuildExtraProtocolEditor(p);page.Show();await Idle();string id=p.Protocol+"_"+language+"_"+theme;
                    checks[id+"_same_window"]=Window.GetWindow(page.PageVisual)==this&&NavigationDepth==1;
                    checks[id+"_masked"]=Visuals<PasswordBox>(page.PageVisual).Any(b=>b.IsVisible);
                    var input=Visuals<TextBox>(page.PageVisual).Single(b=>Equals(b.Tag,"extra:server"));input.Text="changed.example.invalid";
                    Width=1180;await Idle();checks[id+"_resize_preserves_draft"]=input.Text=="changed.example.invalid";
                    Width=480;await Idle();
                    if(language=="en")SavePage((FrameworkElement)Content,Path.Combine(folder,"editor-"+id+".png"));
                    Visuals<Button>(page.PageVisual).First(b=>AutomationProperties.GetName(b)==L.Text("SAVE")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
                    checks[id+"_saved"]=!page.IsVisible&&ExtraProfileEditing.Read(data.Profiles.Single(x=>x.Id==p.Id))!["server"]=="changed.example.invalid";
                }
                Width=1180;Build();await Idle();
                if(language=="en")SavePage((FrameworkElement)Content,Path.Combine(folder,"home-wide-"+theme+".png"));
                var more=Visuals<Button>((FrameworkElement)Content).First(b=>b.ContextMenu is {Items.Count:>2});var menu=more.ContextMenu!;
                string[] order=["CONNECT","SHOW_PATHS","TEST_COLLECTION","STABILITY_TEST","STOP_TEST","PATH_SELECTION","SORT_RESULTS","TCP_CHECK","RENAME","MOVE_COLLECTION","EXPORT_COLLECTION","SHARE_QR","DELETE_COLLECTION"];
                menu.PlacementTarget=more;menu.IsOpen=true;await Idle();
                checks[language+theme+"_menu_surface"]=Equals(menu.Background,Application.Current.Resources["MenuSurface"]);
                checks[language+theme+"_menu_order"]=order.SequenceEqual(menu.Items.OfType<MenuItem>().Where(x=>x.Tag is string).Select(x=>x.Tag!.ToString()));
                checks[language+theme+"_touch_targets"]=menu.Items.OfType<MenuItem>().Where(x=>x.IsEnabled).All(x=>x.MinHeight>=44);
                if(language=="en")SavePage(menu,Path.Combine(folder,"ellipsis-"+theme+".png"));menu.IsOpen=false;
                Width=480;Build();await Idle();if(language=="en")SavePage((FrameworkElement)Content,Path.Combine(folder,"home-compact-"+theme+".png"));
            }
            foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language="en";data.Theme=theme;Width=480;ApplyTheme();Build();
                _=Dispatcher.BeginInvoke(()=>{
                    var page=DialogWindow.OpenPages.Last();UpdateLayout();
                    var buttons=Visuals<Button>(page.PageVisual).Where(b=>b.Tag is string value&&value.StartsWith("new-protocol:")).ToArray();
                    string[] legacy=["REALITY","XHTTP","VLESS WS","VLESS Encryption","gRPC","HTTPUpgrade","SSH","SOCKS5","Trojan","VMess","Shadowsocks","NaiveProxy","DNSTT","NoizDNS","VayDNS","Soar","Slipstream","Hysteria2","WireGuard","AmneziaWG","mKCP"];
                    checks[theme+"_legacy_protocol_order"]=buttons.Take(legacy.Length).Select(b=>b.Tag!.ToString()![13..]).SequenceEqual(legacy);
                    checks[theme+"_new_protocols_present"]=buttons.Select(b=>b.Tag!.ToString()).Contains("new-protocol:AnyTLS")&&buttons.Any(b=>Equals(b.Tag,"new-protocol:Tor"))&&buttons.Any(b=>Equals(b.Tag,"new-protocol:DoH"));
                    checks[theme+"_flag_vector_and_proportional"]=FlagImage("fa").Source is DrawingImage image&&Math.Abs(image.Width/image.Height-1.75)<.01&&FlagImage("fa").Stretch==Stretch.Uniform;
                    SavePage((FrameworkElement)Content,Path.Combine(folder,"new-profile-"+theme+".png"));page.Close();
                },DispatcherPriority.ApplicationIdle);
                NewProfileSelection();await Idle();
                var alt=BuildAlternativeEditor25("doh");alt.Show();await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"doh-editor-"+theme+".png"));alt.Close();
                var chain=BuildChainEditor25();chain.Show();await Idle();checks[theme+"_chain_starts_with_two_layers"]=Visuals<ComboBox>(chain.PageVisual).Count(b=>b.Tag is string value&&value.StartsWith("chain:layer:"))==2;chain.Close();
            }        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;}
        Directory.CreateDirectory(folder);File.WriteAllText(report,JsonSerializer.Serialize(new{passed=checks.Values.All(x=>x),checks,renderTier=RenderCapability.Tier>>16,imageRenderer="RenderTargetBitmap; not GPU performance evidence"},new JsonSerializerOptions{WriteIndented=true}));
        return checks.Values.All(x=>x);
    }
}
