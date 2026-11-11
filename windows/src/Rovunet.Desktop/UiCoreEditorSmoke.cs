using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunCoreEditorSmoke(string report)
    {
        var checks=new Dictionary<string,bool>();var proxyBefore=new Rovunet.Platform.WinInetProxy().Read();
        string key=Convert.ToBase64String(Enumerable.Repeat((byte)1,32).ToArray());string urlKey=key.TrimEnd('=').Replace('+','-').Replace('/','_');
        var cases=new Dictionary<string,string>{
            ["vless-tcp"]="vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=tcp&security=reality&fp=chrome&pbk="+Uri.EscapeDataString(urlKey)+"&sid=aabb&flow=xtls-rprx-vision#TCP",
            ["vless-xhttp"]="vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=xhttp&security=tls&path=%2Ffixture&host=http.example.invalid&mode=auto#XHTTP",
            ["vless-ws"]="vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=ws&security=tls&path=%2Ffixture#WS",
            ["hysteria2"]="hysteria2://synthetic%3Apassword@gateway.example.invalid:443?sni=tls.example.invalid#H2",
            ["wireguard"]=$"[Interface]\nPrivateKey = {key}\nAddress = 10.0.0.2/32\n[Peer]\nPublicKey = {key}\nEndpoint = gateway.example.invalid:51820\nAllowedIPs = 0.0.0.0/0\nPersistentKeepalive = 25"
        };
        var native=Importer.Decode(ManualProfiles.Dns("dnstt","Native WS","http.example.invalid","127.0.0.1:5353","udp",new string('a',64),"127.0.0.1","22","fixture","fixture")[10..]).Split('|');native[1]="vless";native[34]="0";native[62]="00000000-0000-4000-8000-000000000001";native[63]="tls";native[64]="ws";native[65]="/fixture";native[66]="gateway.example.invalid";native[67]="443";native[68]="0";native[70]="0";native[77]="0";native[78]="tls.example.invalid";cases["vless-native"]="rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',native)));
        try{
            foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"light","dark"})foreach(var sample in cases){
                L.Language=language;data.Theme=theme;Width=520;Height=900;ApplyTheme();var original=Importer.Single(sample.Value);original.SourceId="synthetic-sub";original.ItemId="fixture-item";original.CollectionId="sub:synthetic-sub";original.CollectionName="Fixture";data.Profiles=[original];data.SelectedId=original.Id;data.LocalOverrideSources=[];data.LocalOverrideNames=[];Build();string before=original.Raw;string id=sample.Key+"_"+language+"_"+theme;
                var page=BuildCoreProfileEditor(original);page.Show();await Idle();
                try{
                    checks[id+"_same_main_window"]=NavigationDepth==1&&Window.GetWindow(page.PageVisual)==this;
                    checks[id+"_secret_fields_masked"]=Visuals<PasswordBox>(page.PageVisual).Any()&&Visuals<TextBox>(page.PageVisual).Where(t=>t.Tag?.ToString()?.StartsWith("core-reveal:")==true).All(t=>!t.IsVisible);
                    var name=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"PROFILE_NAME"));name.Text="Edited fixture";
                    var error=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"CORE_ERROR"));var save=Find("SAVE");
                    if(sample.Key=="wireguard"){
                        Box("mtu").Text="9999";save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[id+"_invalid_save_keeps_original"]=page.IsVisible&&ReferenceEquals(data.Profiles[0],original)&&error.Text.Length>0;
                        Box("mtu").Text="1420";Box("dns").Text="1.1.1.1, 8.8.8.8";Box("address").Text="10.0.0.3/32, fd00::2/128";Box("endpoint").Text="changed.example.invalid:51821";
                    }else{
                        Box("port").Text="0";save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[id+"_invalid_save_keeps_original"]=page.IsVisible&&ReferenceEquals(data.Profiles[0],original)&&error.Text==L.Text("CORE_PORT_INVALID");Box("port").Text="8443";Box("server").Text="changed.example.invalid";
                        var reveal=Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"core-reveal-button:auth"));reveal.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[id+"_explicit_secret_reveal"]=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"core-reveal:auth")).IsVisible;reveal.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    }
                    if(sample.Key=="vless-tcp"){
                        var pbk=Box("pbk");string retained=pbk.Text;Choice("security").SelectedValue="tls";await Idle();checks[id+"_reality_fields_hide_for_tls"]=!pbk.IsVisible;Choice("security").SelectedValue="reality";await Idle();checks[id+"_reality_draft_restored"]=pbk.IsVisible&&pbk.Text==retained;
                    }
                    if(sample.Key=="vless-xhttp"){Choice("mode").SelectedValue="stream-up";Box("extra").Text="{\"noSSEHeader\":true}";}
                    if(sample.Key=="hysteria2"){Choice("obfs").SelectedValue="salamander";await Idle();var password=Visuals<PasswordBox>(page.PageVisual).Single(p=>Equals(p.Tag,"core:obfs-password"));checks[id+"_salamander_field_visible"]=password.IsVisible;password.Password="synthetic-obfs";}
                    if(sample.Key is "vless-ws" or "vless-native"){Choice("type").SelectedValue="xhttp";await Idle();checks[id+"_conversion_copy_notice"]=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"CORE_COPY_HINT")).IsVisible;}
                    await Idle();page.RestoreScroll(0);await Idle();SavePage(page.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,id+"-top.png"));page.RestoreScroll(double.MaxValue);await Idle();SavePage(page.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,id+"-bottom.png"));
                    _=Dispatcher.BeginInvoke(()=>{var raw=DialogWindow.OpenPages.Single(p=>p!=page);var input=Visuals<TextBox>(raw.PageVisual).Single(t=>Equals(t.Tag,"RAW_PROFILE"));var draft=CoreProfileEditing.Read(Importer.Single(input.Text))!;checks[id+"_advanced_receives_draft"]=draft[sample.Key=="wireguard"?"endpoint":"server"].StartsWith("changed.example.invalid",StringComparison.Ordinal);raw.Close();},DispatcherPriority.ApplicationIdle);
                    Find("ADVANCED_RAW").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks[id+"_advanced_cancel_preserves_original_and_draft"]=original.Raw==before&&data.Profiles.Count==1&&name.Text=="Edited fixture"&&page.IsVisible;
                    Width=420;await Idle();checks[id+"_touch_controls_fit_small_window"]=Visuals<Button>(page.PageVisual).Where(b=>b.IsVisible).All(b=>b.ActualHeight>=44&&b.ActualWidth>=44&&b.ActualWidth<=page.ActualWidth);
                    save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
                    bool copy=sample.Key is "vless-ws" or "vless-native";var edited=copy?data.Profiles.Single(p=>p.Id!=original.Id):data.Profiles[0];
                    checks[id+"_save_closes_page"]=!page.IsVisible&&NavigationDepth==0;
                    checks[id+"_original_object_unmodified"]=original.Raw==before;
                    checks[id+"_save_identity_and_subscription_or_clone"]=copy?data.Profiles.Count==2&&data.Profiles.Contains(original)&&edited.SourceId==""&&edited.CollectionId=="sub:synthetic-sub":edited.Id==original.Id&&edited.SourceId==original.SourceId&&edited.ItemId==original.ItemId&&data.LocalOverrideSources[original.Id]==before;
                    checks[id+"_name_survives_serialization"]=JsonSerializer.Deserialize<Rovunet.Platform.UserData>(JsonSerializer.Serialize(data))!.Profiles.Single(p=>p.Id==edited.Id).Name=="Edited fixture";
                    checks[id+"_runtime_config_valid"]=CoreLinks.Runtime(edited,21001,21002).Contains("\"outbounds\"");
                    if(sample.Key=="vless-native"){var saved=Importer.Decode(edited.Raw[10..]).Split('|');checks[id+"_sharing_and_credential_envelope_preserved"]=saved[34]=="0"&&saved[87]==native[87];}
                    if(sample.Key=="wireguard"){
                        var reset=BuildCoreProfileEditor(edited);reset.Show();await Idle();
                        _=Dispatcher.BeginInvoke(()=>{var confirm=DialogWindow.OpenPages.Last();Visuals<Button>(confirm.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("APPROVE")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));},DispatcherPriority.ApplicationIdle);
                        Visuals<Button>(reset.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text("RESET_LOCAL_EDIT")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                        checks[id+"_reset_restores_original_name_and_config"]=data.Profiles[0].Raw==before&&data.Profiles[0].Name==original.Name&&!data.LocalOverrideNames.ContainsKey(original.Id)&&!reset.IsVisible;
                    }
                    TextBox Box(string field)=>Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"core:"+field));
                    ComboBox Choice(string field)=>Visuals<ComboBox>(page.PageVisual).Single(t=>Equals(t.Tag,"core:"+field));
                    Button Find(string keyName)=>Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text(keyName));
                }finally{if(page.IsVisible)page.Close();}
            }
            checks["proxy_unchanged"]=proxyBefore==new Rovunet.Platform.WinInetProxy().Read();
        }catch(Exception e){checks["failure_"+e.GetType().Name+(e is SafeError s?"_"+s.Code:"")]=false;foreach(var page in DialogWindow.OpenPages.ToArray().Reverse())page.Close();}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="Core profile editor actual WPF save/cancel/clone/auth/transport/metadata",synthetic_profiles=true,external_network_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
    }
}
