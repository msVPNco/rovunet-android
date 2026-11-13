using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    void SaveCoreProfile(Profile original,CoreEditResult result)
    {
        if(!result.CreatesCopy){SaveEdited(original,result.Profile.Raw,result.Profile.Name);return;}
        var previous=data.Profiles.ToList();string selected=data.SelectedId;data.Profiles.Add(result.Profile);data.SelectedId=result.Profile.Id;
        try{Save();}catch{data.Profiles=previous;data.SelectedId=selected;throw;}BuildCards();Notify(L.Text("CORE_COPY_SAVED"));
    }
    DialogWindow BuildCoreProfileEditor(Profile profile,Action<Profile>? install=null)
    {
        var form=CoreProfileEditing.Read(profile)??throw new SafeError("UNSUPPORTED_FORMAT");var fields=form.Fields.ToDictionary(StringComparer.Ordinal);
        if(install!=null){if(form.Kind is "wireguard" or "amneziawg"){fields["privatekey"]="";fields["publickey"]="";fields["endpoint"]="";}else{fields["server"]="";fields["auth"]="";}}
        var d=Dialog(install==null?"EDIT_PROFILE":"ADD",500,800);d.UsePageChrome();var panel=new StackPanel{Margin=new(18,8,18,20)};
        panel.Children.Add(Text(form.Kind=="amneziawg"?"AmneziaWG":form.Kind=="wireguard"?"WireGuard":form.Kind=="hysteria2"?"Hysteria2":"VLESS",22));panel.Children.Add(Text(L.Text("CORE_SERVER_MATCH"),12,Muted));
        if(profile.SourceId.Length>0)panel.Children.Add(Text(L.Text("LOCAL_OVERRIDE_HINT"),12,Muted));
        var copy=Text(L.Text("CORE_COPY_HINT"),12,Accent);copy.Tag="CORE_COPY_HINT";panel.Children.Add(copy);
        var name=new TextBox{Text=profile.Name,Tag="PROFILE_NAME",FlowDirection=L.Direction};panel.Children.Add(OutlinedField("PROFILE_NAME",name));
        var frames=new Dictionary<string,FrameworkElement>();var error=Text("",12,Red);error.Tag="CORE_ERROR";
        name.TextChanged+=(_,_)=>error.Text="";
        void Field(string key,string label,bool secret=false,bool multi=false)
        {
            if(!secret){var box=new TextBox{Text=fields[key],Tag="core:"+key,FlowDirection=FlowDirection.LeftToRight,AcceptsReturn=multi,TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap,MinHeight=multi?100:48,MaxHeight=multi?220:double.PositiveInfinity,VerticalScrollBarVisibility=multi?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden};box.TextChanged+=(_,_)=>{fields[key]=box.Text;error.Text="";Refresh();};var frame=OutlinedField(label,box);frames[key]=frame;panel.Children.Add(frame);return;}
            var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var hidden=new PasswordBox{Password=fields[key],Tag="core:"+key,MinHeight=44,Padding=new(2),Background=Brushes.Transparent,BorderThickness=new(0),FlowDirection=FlowDirection.LeftToRight};AutomationProperties.SetName(hidden,L.Text(label));
            var plain=new TextBox{Text=fields[key],Tag="core-reveal:"+key,Visibility=Visibility.Collapsed,MinHeight=44,Padding=new(2),Background=Brushes.Transparent,BorderThickness=new(0),FlowDirection=FlowDirection.LeftToRight};AutomationProperties.SetName(plain,L.Text(label));grid.Children.Add(hidden);grid.Children.Add(plain);
            bool syncing=false;hidden.PasswordChanged+=(_,_)=>{if(syncing)return;syncing=true;fields[key]=hidden.Password;plain.Text=hidden.Password;syncing=false;error.Text="";Refresh();};plain.TextChanged+=(_,_)=>{if(syncing)return;syncing=true;fields[key]=plain.Text;hidden.Password=plain.Text;syncing=false;error.Text="";Refresh();};
            var reveal=Button("CORE_SHOW",()=>{});reveal.Tag="core-reveal-button:"+key;reveal.Width=72;reveal.MinHeight=44;reveal.Padding=new(4);reveal.Margin=new(2,0,0,0);reveal.FontSize=14;reveal.Click+=(_,_)=>{bool show=plain.Visibility!=Visibility.Visible;plain.Visibility=show?Visibility.Visible:Visibility.Collapsed;hidden.Visibility=show?Visibility.Collapsed:Visibility.Visible;reveal.Content=L.Text(show?"CORE_HIDE":"CORE_SHOW");AutomationProperties.SetName(reveal,L.Text(show?"CORE_HIDE":"CORE_SHOW"));if(show)plain.Focus();else hidden.Focus();};Grid.SetColumn(reveal,1);grid.Children.Add(reveal);
            var secretHost=new ContentControl{Content=grid,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};var border=OutlinedField(label,secretHost);frames[key]=border;panel.Children.Add(border);
        }
        void Choice(string key,string label,params string[] values)
        {
            var box=new ComboBox{Tag="core:"+key,DisplayMemberPath="Label",SelectedValuePath="Value",ItemsSource=values.Select(v=>new{Value=v,Label=v is "" or "none"?L.Text("CORE_NONE"):v=="auto"?L.Text("CORE_AUTO"):v.ToUpperInvariant()}).ToArray(),Visibility=Visibility.Collapsed};box.SelectedValue=fields[key];
            var body=new StackPanel();body.Children.Add(Text(L.Text(label),14));body.Children.Add(box);body.Children.Add(ChoiceChips(box));frames[key]=body;panel.Children.Add(body);box.SelectionChanged+=(_,_)=>{fields[key]=box.SelectedValue?.ToString()??"";error.Text="";Refresh();};
        }
        if(form.Kind is "wireguard" or "amneziawg"){
            panel.Children.Add(Text(L.Text("CORE_INTERFACE"),18));Field("privatekey","CORE_PRIVATE_KEY",true);Field("address","CORE_ADDRESSES");Field("dns","CORE_DNS");Field("mtu","CORE_MTU");
            panel.Children.Add(Text(L.Text("CORE_PEER"),18));Field("publickey","CORE_PUBLIC_KEY");Field("presharedkey","CORE_PRESHARED_KEY",true);Field("endpoint","CORE_ENDPOINT");Field("allowedips","CORE_ALLOWED_IPS");Field("persistentkeepalive","CORE_KEEPALIVE");
            if(form.Kind=="amneziawg"){
                var advanced=new StackPanel();int firstAdvanced=panel.Children.Count;
                foreach(var option in AmneziaConfig.Labels)Field(option.Key,option.Value,option.Key=="headerprotectionkey");
                while(panel.Children.Count>firstAdvanced){var child=panel.Children[firstAdvanced];panel.Children.RemoveAt(firstAdvanced);advanced.Children.Add(child);}
                panel.Children.Add(new Expander{Header=L.Text("ADVANCED_SETTINGS"),Content=advanced,Margin=new(0,12,0,12)});
            }
        }else{
            Field("server","SERVER_HOST");Field("port","SERVER_PORT");Field("auth",form.Kind=="vless"?"CORE_UUID":"CORE_AUTH",true);
            if(form.Kind=="vless"){Field("encryption","CORE_VLESS_ENCRYPTION",true);panel.Children.Add(Text(L.Text("CORE_VLESS_ENCRYPTION_HINT"),12,Muted));}
            if(form.Kind=="vless"){
                Choice("type","TRANSPORT","ws","tcp","xhttp","grpc","httpupgrade","kcp");Choice("security","CORE_SECURITY","none","tls","reality");Field("sni","CORE_SNI");Field("fp","CORE_FINGERPRINT");Field("alpn","CORE_ALPN");Field("cs","CORE_CIPHER_SUITES");Field("fm","CORE_FINAL_MASK",false,true);Field("pbk","CORE_REALITY_KEY");Field("sid","CORE_SHORT_ID");Field("spx","CORE_SPIDERX");Field("host","CORE_HTTP_HOST");Field("path","CORE_PATH");Field("heartbeatPeriod","CORE_WS_HEARTBEAT");Field("mtu","CORE_KCP_MTU");Field("tti","CORE_KCP_TTI");Field("serviceName","CORE_SERVICE_NAME");Field("authority","CORE_AUTHORITY");Choice("mode","CORE_XHTTP_MODE","auto","packet-up","stream-up","stream-one");Field("extra","CORE_XHTTP_EXTRA",false,true);Choice("flow","CORE_FLOW","","xtls-rprx-vision");
            }else{Field("sni","CORE_SNI");panel.Children.Add(Text(L.Text("CORE_TLS_VERIFIED"),12,Muted));Choice("obfs","CORE_OBFS","","salamander");Field("obfs-password","CORE_OBFS_PASSWORD",true);}
        }
        void Refresh()
        {
            copy.Visibility=CoreProfileEditing.CreatesCopy(profile,fields)?Visibility.Visible:Visibility.Collapsed;
            bool vless=form.Kind=="vless",tls=fields.GetValueOrDefault("security")!="none",reality=fields.GetValueOrDefault("security")=="reality",xhttp=fields.GetValueOrDefault("type")=="xhttp",ws=fields.GetValueOrDefault("type")=="ws";
            void Visible(string key,bool value){if(frames.TryGetValue(key,out var frame))frame.Visibility=value?Visibility.Visible:Visibility.Collapsed;}
            if(vless){Visible("sni",tls);Visible("fp",tls);Visible("alpn",fields["security"]=="tls");foreach(string key in new[]{"pbk","sid","spx"})Visible(key,reality);Visible("host",xhttp||ws||fields["type"]=="httpupgrade");Visible("cs",fields["security"]=="tls");Visible("fm",fields["security"]=="tls");Visible("path",xhttp||ws||fields["type"]=="httpupgrade");Visible("heartbeatPeriod",ws);Visible("mtu",fields["type"]=="kcp");Visible("tti",fields["type"]=="kcp");Visible("serviceName",fields["type"]=="grpc");Visible("authority",fields["type"]=="grpc");Visible("mode",xhttp);Visible("extra",xhttp);Visible("flow",fields["type"]=="tcp"&&tls);}else if(form.Kind=="hysteria2")Visible("obfs-password",fields["obfs"]=="salamander");
        }
        CoreEditResult Draft()=>CoreProfileEditing.Apply(profile,name.Text,fields);
        void SaveDraft(){try{var draft=Draft();if(install!=null)install(draft.Profile);else SaveCoreProfile(profile,draft);d.DialogResult=true;}catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"INVALID_IMPORT");error.BringIntoView();}}
        panel.Children.Add(error);var save=Button("SAVE",SaveDraft,true);save.IsDefault=true;d.SetPageAction(save);panel.Children.Add(Button("CORE_SAVE_PROFILE",SaveDraft,true));
        if(data.LocalOverrideSources.ContainsKey(profile.Id))panel.Children.Add(SettingsAction("RESET_LOCAL_EDIT",()=>ResetLocalOverride(profile,d)));
        panel.Children.Add(SettingsAction("ADVANCED_RAW",()=>{try{var draft=Draft();var advanced=BuildRawEditor(profile,draft.Profile.Raw,raw=>{var next=ProfileEditing.Replace(profile,raw);var editedForm=CoreProfileEditing.Read(next);if(editedForm?.Kind is "wireguard" or "amneziawg")next.Name=name.Text.Trim();bool clone=draft.CreatesCopy||form.Kind=="vless"&&form["type"]=="ws"&&(editedForm?.Kind!="vless"||editedForm["type"]!="ws");if(clone){next.Id=Guid.NewGuid().ToString("N");next.CollectionId=profile.SourceId.Length>0?"sub:"+profile.SourceId:profile.CollectionId;next.SourceId="";next.ItemId="";}if(install!=null)install(next);else SaveCoreProfile(profile,new(){Profile=next,CreatesCopy=clone});});advanced.Owner=d;if(advanced.ShowDialog()==true)d.DialogResult=true;}catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"INVALID_IMPORT");}}));
        Refresh();d.Content=panel;return d;
    }
}
