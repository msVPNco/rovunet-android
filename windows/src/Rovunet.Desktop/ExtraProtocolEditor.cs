using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    DialogWindow BuildExtraProtocolEditor(Profile profile,Action<Profile>? imported=null)
    {
        var model=ExtraProfileEditing.Read(profile)??throw new SafeError("UNSUPPORTED_FORMAT");var values=model.Fields.ToDictionary(StringComparer.Ordinal);
        bool supplemental=model.Kind is "anytls" or "tuic" or "mierus";
        if(imported!=null){values["server"]="";foreach(string key in new[]{"auth","uuid","username","password"})if(values.ContainsKey(key))values[key]="";}
        var page=Dialog(imported!=null?"ADD":"EDIT_PROFILE");var panel=new StackPanel{Margin=new(20)};panel.Children.Add(Text(profile.Protocol,22));panel.Children.Add(Text(L.Text("CORE_SERVER_MATCH"),12,Muted));
        var name=new TextBox{Text=profile.Name,Tag="extra:name"};panel.Children.Add(OutlinedField("PROFILE_NAME",name));var frames=new Dictionary<string,FrameworkElement>();
        void Field(string key,string label,bool secret=false){
            FrameworkElement frame;
            if(secret){
                var grid=new Grid();grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                var password=new PasswordBox{Password=values[key],Tag="extra:"+key,Margin=new(0),Padding=new(2),Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new(0),FlowDirection=FlowDirection.LeftToRight};
                var visible=new TextBox{Text=values[key],Visibility=Visibility.Collapsed,Tag="extra:"+key+":visible",Margin=new(0),Padding=new(2),Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new(0),FlowDirection=FlowDirection.LeftToRight};
                AutomationProperties.SetName(password,L.Text(label));AutomationProperties.SetName(visible,L.Text(label));
                password.PasswordChanged+=(_,_)=>{values[key]=password.Password;if(visible.Visibility==Visibility.Collapsed)visible.Text=password.Password;};visible.TextChanged+=(_,_)=>{if(visible.Visibility==Visibility.Visible){values[key]=visible.Text;password.Password=visible.Text;}};
                grid.Children.Add(password);grid.Children.Add(visible);
                var reveal=IconButton("SHOW_SECRET","M2,12 Q12,0 22,12 Q12,24 2,12 M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8",()=>{bool show=visible.Visibility!=Visibility.Visible;visible.Text=password.Password;visible.Visibility=show?Visibility.Visible:Visibility.Collapsed;password.Visibility=show?Visibility.Collapsed:Visibility.Visible;});Grid.SetColumn(reveal,1);grid.Children.Add(reveal);
                frame=OutlinedField(label,new ContentControl{Content=grid,HorizontalContentAlignment=HorizontalAlignment.Stretch});
            }else{
                var input=new TextBox{Text=values[key],Tag="extra:"+key,FlowDirection=FlowDirection.LeftToRight};input.TextChanged+=(_,_)=>values[key]=input.Text;frame=OutlinedField(label,input);
            }
            frames[key]=frame;panel.Children.Add(frame);
        }
        void Choice(string key,string label,string[] choices){var box=new ComboBox{Tag="extra:"+key,FlowDirection=FlowDirection.LeftToRight,ItemsSource=choices,SelectedItem=values[key]};var frame=OutlinedField(label,box);frames[key]=frame;panel.Children.Add(frame);box.SelectionChanged+=(_,_)=>{values[key]=box.SelectedItem?.ToString()??"";Refresh();};}
        Field("server","SERVER_HOST");Field("port","SERVER_PORT");if(supplemental){
            if(model.Kind=="anytls")Field("auth","PASSWORD",true);
            else{Field(model.Kind=="tuic"?"uuid":"username",model.Kind=="tuic"?"CORE_UUID":"ID_OR_USER");Field("password","PASSWORD",true);}
            if(model.Kind=="mierus"){
                Choice("protocol","TRANSPORT",["TCP","UDP"]);
                Choice("multiplexing","SUP_MULTIPLEXING",["","MULTIPLEXING_OFF","MULTIPLEXING_LOW","MULTIPLEXING_MIDDLE","MULTIPLEXING_HIGH"]);
                Choice("handshake-mode","SUP_HANDSHAKE",["","HANDSHAKE_DEFAULT","HANDSHAKE_STANDARD","HANDSHAKE_NO_WAIT"]);Field("traffic-pattern","SUP_PATTERN");
            }else{
                Field("sni","CORE_SNI");Field("alpn","CORE_ALPN");
                if(model.Kind=="anytls"){
                    Choice("fp","CORE_FINGERPRINT",["","chrome","firefox","safari","ios","android","edge","random","randomized"]);
                    Field("idle-session-check-interval","SUP_CHECK_INTERVAL");Field("idle-session-timeout","SUP_IDLE_TIMEOUT");Field("min-idle-session","SUP_MIN_IDLE");
                }else{
                    Choice("congestion_control","CONGESTION_CONTROL",["","cubic","new_reno","bbr"]);Choice("udp_relay_mode","SUP_RELAY",["","native","quic"]);
                    Choice("reduce_rtt","SUP_REDUCE_RTT",["","false","true"]);Field("heartbeat","SUP_HEARTBEAT");
                }
            }
            panel.Children.Add(Text(L.Text(model.Kind=="mierus"?"SUP_MIERU_HINT":"SUP_TLS_HINT"),13,Muted));
        }else Field("auth",model.Kind=="vmess"?"CORE_UUID":"PASSWORD",true);
        if(supplemental){}else if(model.Kind=="ss")Choice("method","CORE_CIPHER",CoreLinks.ShadowsocksMethods.ToArray());
        else{if(model.Kind=="vmess")Choice("cipher","CORE_CIPHER",["auto","aes-128-gcm","chacha20-poly1305"]);Choice("type","TRANSPORT",["tcp","ws","grpc","httpupgrade"]);Choice("security","CORE_SECURITY",["tls","none"]);Field("sni","CORE_SNI");Field("fp","CORE_FINGERPRINT");Field("alpn","CORE_ALPN");Field("host","CORE_HTTP_HOST");Field("path","CORE_PATH");Field("serviceName","CORE_SERVICE_NAME");Field("authority","CORE_AUTHORITY");}
        void Refresh(){if(supplemental)return;foreach(string key in new[]{"sni","fp","alpn"})if(frames.TryGetValue(key,out var frame))frame.Visibility=values["security"]=="tls"?Visibility.Visible:Visibility.Collapsed;foreach(string key in new[]{"host","path"})if(frames.TryGetValue(key,out var frame))frame.Visibility=values["type"] is "ws" or "httpupgrade"?Visibility.Visible:Visibility.Collapsed;foreach(string key in new[]{"serviceName","authority"})if(frames.TryGetValue(key,out var frame))frame.Visibility=values["type"]=="grpc"?Visibility.Visible:Visibility.Collapsed;}
        var error=Text("",13,Red);panel.Children.Add(error);void Apply(){try{var next=ExtraProfileEditing.Apply(profile,name.Text,values);if(imported!=null)imported(next);else SaveEdited(profile,next.Raw,next.Name);page.DialogResult=true;}catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"INVALID_IMPORT");error.BringIntoView();}}
        var save=Button("SAVE",Apply,true);page.SetPageAction(save);panel.Children.Add(Button(imported!=null?"IMPORT":"SAVE",Apply,true));Refresh();page.Content=panel;return page;
    }
}
