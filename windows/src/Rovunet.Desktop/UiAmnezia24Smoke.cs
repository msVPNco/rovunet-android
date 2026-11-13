using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunAmnezia24Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();var proxy=new Rovunet.Platform.WinInetProxy().Read();
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        string key=Convert.ToBase64String(Enumerable.Repeat((byte)1,32).ToArray());
        string raw=$"[Interface]\nPrivateKey={key}\nAddress=10.0.0.2/32\nVersion=3.1\nHeaderProtectionKey={key}\nJc=3\nJmin=10\nJmax=50\n[Peer]\nPublicKey={key}\nEndpoint=fixture.example.invalid:51820\nAllowedIPs=0.0.0.0/0";
        try{
            foreach(string language in new[]{"en","fa","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();var original=Importer.Single(raw);original.SourceId="synthetic-sub";original.ItemId="fixture";data.Profiles=[original];data.LocalOverrideSources=[];data.LocalOverrideNames=[];Build();string id=language+"_"+theme;
                var page=BuildCoreProfileEditor(original);page.Show();await Idle();
                checks[id+"_same_window"]=NavigationDepth==1&&Window.GetWindow(page.PageVisual)==this;
                checks[id+"_private_key_masked"]=Visuals<PasswordBox>(page.PageVisual).Any(p=>Equals(p.Tag,"core:privatekey"));
                var advanced=Visuals<Expander>(page.PageVisual).Single();advanced.IsExpanded=true;await Idle();
                checks[id+"_header_protection_masked"]=Visuals<PasswordBox>(page.PageVisual).Any(p=>Equals(p.Tag,"core:headerprotectionkey"))&&Visuals<TextBox>(page.PageVisual).Where(p=>Equals(p.Tag,"core-reveal:headerprotectionkey")).All(p=>p.Visibility==Visibility.Collapsed);
                var jc=Visuals<TextBox>(page.PageVisual).Single(p=>Equals(p.Tag,"core:jc"));var save=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("SAVE"));
                jc.Text="65536";save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[id+"_invalid_is_atomic"]=page.IsVisible&&ReferenceEquals(data.Profiles[0],original);
                jc.Text="4";Visuals<TextBox>(page.PageVisual).Single(p=>Equals(p.Tag,"PROFILE_NAME")).Text="Edited AWG";
                save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();var edited=data.Profiles.Single();
                checks[id+"_save_preserves_subscription"]=!page.IsVisible&&edited.Id==original.Id&&edited.SourceId==original.SourceId&&edited.ItemId==original.ItemId;
                checks[id+"_save_retains_v3_identity"]=Importer.Connection(edited).Outbound["settings"]!["amnezia-wg-option"]!["jc"]!.GetValue<int>()==4&&edited.Raw.Contains("HeaderProtectionKey="+key)&&edited.Name=="Edited AWG";
                var cancel=BuildCoreProfileEditor(edited);cancel.Show();await Idle();Visuals<Expander>(cancel.PageVisual).Single().IsExpanded=true;await Idle();Visuals<TextBox>(cancel.PageVisual).Single(p=>Equals(p.Tag,"core:jc")).Text="5";cancel.Close();checks[id+"_cancel_keeps_saved"]=data.Profiles.Single().Raw==edited.Raw;
                if(language is "fa" or "en"){
                    var picture=BuildCoreProfileEditor(edited);picture.Show();await Idle();SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"awg-editor-"+id+".png"));picture.Close();
                }
            }
            foreach(string language in new[]{"en","fa","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"})foreach(string protocol in new[]{"SSH","SOCKS5","NaiveProxy"}){
                L.Language=language;data.Theme=theme;ApplyTheme();Build();string id="manual_"+language+"_"+theme+"_"+protocol;
                var page=BuildAddConfiguration(2,manualProtocol:protocol);page.Show();await Idle();
                checks[id+"_no_import_tabs"]=!Visuals<TabControl>(page.PageVisual).Any();
                checks[id+"_no_unrelated_shortcuts"]=!Visuals<Button>(page.PageVisual).Any(b=>b.Content?.ToString() is "Trojan" or "VMess" or "Shadowsocks");
                checks[id+"_protocol_title"]=Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text==L.Text("ADD")+" · "+protocol);
                checks[id+"_correct_protocol"]=Visuals<ComboBox>(page.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible).SelectedItem?.ToString()==protocol;
                checks[id+"_default_port"]=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"SERVER_PORT")&&t.IsVisible).Text==(protocol=="SSH"?"22":protocol=="SOCKS5"?"1080":"443");
                if(language=="fa"&&theme=="amoled")SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"manual-"+protocol+"-fa.png"));
                page.Close();
            }
            checks["system_proxy_unchanged"]=proxy==new Rovunet.Platform.WinInetProxy().Read();
        }catch(Exception e){checks["failure_"+e.GetType().Name]=false;foreach(var page in DialogWindow.OpenPages.ToArray().Reverse())page.Close();}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="AmneziaWG actual WPF editor",synthetic_profiles=true,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
    }
}
