using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    async Task ExtraFormsSmoke(Dictionary<string,bool> checks,string folder)
    {
        string[] links=["trojan://fixture@gateway.example.invalid:443?type=ws&path=%2Fsocket#Trojan","vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{v="2",ps="VMess",add="gateway.example.invalid",port="443",id="00000000-0000-4000-8000-000000000001",aid="0",scy="auto",net="grpc",serviceName="fixture",tls="tls"}))),"ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-256-gcm:fixture"))+"@gateway.example.invalid:443#SS"];
        foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"amoled","light"})foreach(string raw in links){
            L.Language=language;data.Theme=theme;Width=1180;Height=860;ApplyTheme();
            var profile=Importer.Single(raw);profile.SourceId="synthetic-source";profile.ItemId="synthetic-item";data.Profiles=[profile];data.SelectedId=profile.Id;Build();
            var editor=BuildExtraProtocolEditor(profile);editor.Show();await Idle17();string key=profile.Protocol+"-"+language+"-"+theme;
            checks[key+"_secrets_masked"]=Visuals<PasswordBox>(editor.PageVisual).Any(p=>p.IsVisible)&&!Visuals<TextBox>(editor.PageVisual).Any(t=>Equals(t.Tag,"extra:auth:visible")&&t.IsVisible);
            string original=profile.Raw;var host=Visuals<TextBox>(editor.PageVisual).Single(t=>Equals(t.Tag,"extra:server"));host.Text="changed.example.invalid";
            Width=680;await Idle17();checks[key+"_edit_survives_compact"]=host.Text=="changed.example.invalid"&&!contextHost.IsVisible;
            Width=1180;await Idle17();SavePage((FrameworkElement)Content,Path.Combine(folder,"extra-editor-"+key+".png"));
            var save=Visuals<Button>(editor.PageVisual).First(b=>AutomationProperties.GetName(b)==L.Text("SAVE"));save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle17();
            var next=data.Profiles.Single();checks[key+"_saved_real_model"]=!editor.IsVisible&&next.Raw!=original&&ExtraProfileEditing.Read(next)!["server"]=="changed.example.invalid"&&next.Id==profile.Id&&next.SourceId==profile.SourceId&&next.ItemId==profile.ItemId;
            var discard=BuildExtraProtocolEditor(next);discard.Show();await Idle17();Visuals<TextBox>(discard.PageVisual).Single(t=>Equals(t.Tag,"extra:server")).Text="discard.example.invalid";discard.Close();checks[key+"_back_discards_draft"]=ExtraProfileEditing.Read(data.Profiles.Single())!["server"]=="changed.example.invalid";
        }
        L.Language="en";data.Theme="amoled";ApplyTheme();Build();
        foreach(string protocol in new[]{"REALITY","XHTTP","DNSTT","Soar"}){
            var page=BuildAddConfiguration(2,manualProtocol:protocol);page.Show();await Idle17();
            if(protocol is "REALITY" or "XHTTP")checks["manual_choice_"+protocol]=Visuals<ComboBox>(page.PageVisual).Any(b=>b.SelectedItem?.ToString()==(protocol=="REALITY"?"reality":"xhttp"));
            else checks["manual_choice_"+protocol]=Visuals<ComboBox>(page.PageVisual).Any(b=>b.SelectedItem?.ToString()==protocol&&b.IsVisible);
            page.Close();
        }
    }
}
