using Avalonia.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void ShowExtendedEditor(Profile original,CoreProfileForm form,bool isNew)
    {
        var panel=Page();panel.Children.Add(Label(original.Protocol,22));panel.Children.Add(Label(L.Text(form.Kind=="doh"?"MAC_DOH_ONLY_HINT":form.Kind=="snowflake"?"MAC_TOR_BRIDGE_HINT":"MAC_RAW_OPTION_HINT"),12,"Muted"));
        var name=Field(panel,"PROFILE_NAME",original.Name);name.FlowDirection=L.Direction;var values=new Dictionary<string,Func<string>>();var advanced=new StackPanel{Spacing=8};
        string LabelKey(string key)=>key.ToLowerInvariant() switch{"server" or "add"=>"SERVER_HOST","port"=>"SERVER_PORT","auth"=>"CORE_AUTH","id"=>"CORE_UUID","type" or "net"=>"TRANSPORT","security" or "tls"=>"CORE_SECURITY","sni"=>"CORE_SNI","fp"=>"CORE_FINGERPRINT","alpn"=>"CORE_ALPN","host"=>"CORE_HTTP_HOST","path"=>"CORE_PATH","cs"=>"CORE_CIPHER_SUITES","fm"=>"CORE_FINAL_MASK","method"=>"SHADOWSOCKS_METHOD","privatekey"=>"CORE_PRIVATE_KEY","publickey"=>"CORE_PUBLIC_KEY","presharedkey"=>"CORE_PRESHARED_KEY","address"=>"CORE_ADDRESSES","allowedips"=>"CORE_ALLOWED_IPS","endpoint"=>"CORE_ENDPOINT","dns"=>"CORE_DNS","mtu"=>"CORE_MTU","persistentkeepalive"=>"CORE_KEEPALIVE","doh"=>"DOH_URL","bridges"=>"MAC_TOR_BRIDGES","mport"=>"MAC_HOP_PORTS","hop-interval"=>"MAC_HOP_INTERVAL","scy" or "cipher"=>"VMESS_SECURITY","username"=>"USERNAME","password"=>"PASSWORD","uuid"=>"CORE_UUID","protocol"=>"TRANSPORT","idle-session-check-interval"=>"MAC_EXTRA_IDLE_CHECK","idle-session-timeout"=>"MAC_EXTRA_IDLE_TIMEOUT","min-idle-session"=>"MAC_EXTRA_IDLE_MIN","congestion_control"=>"CONGESTION","udp_relay_mode"=>"MAC_EXTRA_UDP_MODE","reduce_rtt"=>"MAC_EXTRA_REDUCE_RTT","heartbeat"=>"MAC_EXTRA_HEARTBEAT","multiplexing"=>"MAC_EXTRA_MULTIPLEXING","handshake-mode"=>"MAC_EXTRA_HANDSHAKE","traffic-pattern"=>"MAC_EXTRA_TRAFFIC_PATTERN","profile"=>"PROFILE_NAME","servicename"=>"CORE_SERVICE_NAME","authority"=>"CORE_AUTHORITY",_=>key};
        foreach(var (key,value) in form.Fields){
            bool awgAdvanced=form.Kind=="amneziawg"&&MacAmnezia.Labels.ContainsKey(key.ToLowerInvariant());bool optional=awgAdvanced||key is "cs" or "fm" or "v" or "aid" or "ed" or "eh"||form.Kind is "anytls" or "tuic" or "mierus"&&key is not("server" or "port" or "auth" or "uuid" or "username" or "password" or "sni" or "protocol");Panel target=optional?advanced:panel;
            string inputValue=isNew&&key.ToLowerInvariant() is "server" or "add" or "auth" or "id" or "uuid" or "username" or "password" or "sni" or "peer" or "privatekey" or "publickey" or "endpoint"?"":value;bool secret=key.ToLowerInvariant() is "auth" or "password" or "privatekey" or "presharedkey" or "headerprotectionkey";
            string[]? supported=key switch{
                "protocol" when form.Kind=="mierus"=>["TCP","UDP"],
                "multiplexing"=>["","MULTIPLEXING_OFF","MULTIPLEXING_LOW","MULTIPLEXING_MIDDLE","MULTIPLEXING_HIGH"],
                "handshake-mode"=>["","HANDSHAKE_DEFAULT","HANDSHAKE_STANDARD","HANDSHAKE_NO_WAIT"],
                "congestion_control"=>["","cubic","new_reno","bbr"],
                "udp_relay_mode"=>["","native","quic"],_=>null};
            if(supported!=null){var select=Choice(target,LabelKey(key),supported,value);values[key]=()=>select.SelectedItem?.ToString()??"";}
            else if(key=="method"){
                var choice=Choice(target,"SHADOWSOCKS_METHOD",["2022-blake3-aes-128-gcm","2022-blake3-aes-256-gcm","2022-blake3-chacha20-poly1305"],value);values[key]=()=>choice.SelectedItem?.ToString()??"";
            }else{var field=Field(target,LabelKey(key),inputValue,key is "fm" or "bridges",secret);values[key]=()=>field.Text??"";}
        }
        if(advanced.Children.Count>0)panel.Children.Add(new Expander{Header=L.Text("SCAN_ADVANCED"),Content=advanced,IsExpanded=false,HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Stretch});
        panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();var changed=MacProfileEditing.Apply(original,name.Text??"",values.ToDictionary(p=>p.Key,p=>p.Value()));if(isNew){changed.Id=Guid.NewGuid().ToString("N");Commit(d=>d.Profiles.Add(changed));}else SaveEdited(original,changed);GoHome();},true));panel.Children.Add(ActionButton("ADVANCED_RAW",()=>ShowRawEditor(original,isNew)));Navigate(isNew?"ADD_MANUAL":"EDIT_PROFILE",panel);
    }
}
