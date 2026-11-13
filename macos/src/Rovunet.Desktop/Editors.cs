using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void ShowManual()
    {
        var panel=Page();panel.Children.Add(Label(L.Text("CHOOSE_CONNECTION_TYPE"),16));
        void Section(string key,string[] names){panel.Children.Add(Label(L.Text(key),13,"Muted"));var choices=new WrapPanel();foreach(string name in names){var b=ActionButton("ADD",()=>NewProfile(name));b.Content=name;b.MinWidth=145;b.MinHeight=52;choices.Children.Add(b);}panel.Children.Add(Card(choices));}
        Section("WEB_TCP_PATHS",["REALITY","XHTTP","VLESS WS","VLESS Encryption","gRPC","HTTPUpgrade","SSH","SOCKS5","Trojan","VMess","Shadowsocks","Shadowsocks 2022","AnyTLS","Mieru","NaiveProxy","Tor"]);
        Section("DNS_FAMILIES",["DNSTT","NoizDNS","VayDNS","Soar","Slipstream","DoH"]);Section("UDP_PATHS",["Hysteria2","Hysteria2 hopping","WireGuard","AmneziaWG","TUIC","mKCP"]);Navigate("ADD_MANUAL",panel);
    }
    void NewProfile(string kind)
    {
        if(kind is "SSH" or "SOCKS5" or "NaiveProxy"){ShowNativeManual(kind);return;}
        if(kind is "DNSTT" or "NoizDNS" or "VayDNS" or "Soar" or "Slipstream"){ShowDnsManual(kind);return;}
        if(kind is "Tor" or "DoH"){ShowEditor(MacProfileEditing.Native(kind=="Tor"?"snowflake":"doh",kind,kind=="DoH"?"https://dns.example.invalid/dns-query":""),true);return;}
        string transport=kind switch{"REALITY" or "VLESS Encryption"=>"tcp","XHTTP"=>"xhttp","gRPC"=>"grpc","HTTPUpgrade"=>"httpupgrade","mKCP"=>"kcp",_=>"ws"};string security=kind=="REALITY"?"reality":transport=="kcp"?"none":"tls";
        string raw=kind switch{
            "AnyTLS"=>"anytls://draft@server.example.invalid:443?sni=server.example.invalid#AnyTLS",
            "TUIC"=>"tuic://00000000-0000-4000-8000-000000000001:draft@server.example.invalid:443?sni=server.example.invalid#TUIC",
            "Mieru"=>"mierus://draft:draft@server.example.invalid?profile=Mieru&port=8443&protocol=TCP#Mieru",
            "Trojan"=>"trojan://draft@server.example.invalid:443#Trojan",
            "VMess"=>"vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{v="2",ps="VMess",add="server.example.invalid",port=443,id="00000000-0000-4000-8000-000000000001",aid=0,scy="auto",net="tcp",tls="tls"}))),
            "Shadowsocks"=>"ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-256-gcm:draft"))+"@server.example.invalid:443#Shadowsocks",
            "Shadowsocks 2022"=>"ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("2022-blake3-aes-128-gcm:"+Convert.ToBase64String(new byte[16])))+"@server.example.invalid:443#Shadowsocks%202022",
            "Hysteria2"=>"hy2://draft@server.example.invalid:443#Hysteria2",
            "Hysteria2 hopping"=>"hy2://draft@server.example.invalid:443?mport=443,10000-10010&hop-interval=30s#Hysteria2",
            "AmneziaWG"=>"[Interface]\nPrivateKey="+Convert.ToBase64String(new byte[32])+"\nAddress=10.0.0.2/32\nJc=3\nJmin=10\nJmax=50\nH1=123\n[Peer]\nPublicKey="+Convert.ToBase64String(new byte[32])+"\nEndpoint=server.example.invalid:51820\nAllowedIPs=0.0.0.0/0, ::/0",
            "WireGuard"=>"[Interface]\nPrivateKey="+Convert.ToBase64String(new byte[32])+"\nAddress=10.0.0.2/32\n[Peer]\nPublicKey="+Convert.ToBase64String(new byte[32])+"\nEndpoint=server.example.invalid:51820\nAllowedIPs=0.0.0.0/0, ::/0",
            _=>"vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type="+transport+"&security="+security+(security=="reality"?"&fp=chrome&pbk="+Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+','-').Replace('/','_'):"")+"#VLESS"
        };
        ShowEditor(MacProtocols.Single(raw),true);
    }
    void ShowEditor(Profile original,bool isNew=false)
    {
        if(MacProfileEditing.Read(original) is {} extended){ShowExtendedEditor(original,extended,isNew);return;}
        var core=CoreProfileEditing.Read(original);var extra=core==null?ExtraProfileEditing.Read(original):null;var form=core??extra;
        if(form==null){if(ProfileEditing.IsDns(original)){ShowDnsEditor(original);return;}ShowEndpointEditor(original);return;}
        var fields=form.Fields.ToDictionary();var panel=Page();panel.Children.Add(Label(form.Kind=="wireguard"?"WireGuard":form.Kind.ToUpperInvariant(),22));panel.Children.Add(Label(L.Text("CORE_SERVER_MATCH"),12,"Muted"));panel.Children.Add(Label(L.Text("MAC_RAW_OPTION_HINT"),12,"Muted"));
        var name=Field(panel,"PROFILE_NAME",original.Name);name.FlowDirection=L.Direction;var controls=new Dictionary<string,Control>();var values=new Dictionary<string,Func<string>>();
        string LabelKey(string key)=>key switch{"server"=>"SERVER_HOST","port"=>"SERVER_PORT","auth"=>form.Kind is "vless" or "vmess"?"CORE_UUID":"CORE_AUTH","type"=>"TRANSPORT","security"=>"CORE_SECURITY","encryption"=>"CORE_VLESS_ENCRYPTION","flow"=>"CORE_FLOW","sni"=>"CORE_SNI","fp"=>"CORE_FINGERPRINT","pbk"=>"CORE_REALITY_KEY","sid"=>"CORE_SHORT_ID","spx"=>"CORE_SPIDERX","host"=>"CORE_HTTP_HOST","path"=>"CORE_PATH","mode"=>"CORE_XHTTP_MODE","extra"=>"CORE_XHTTP_EXTRA","alpn"=>"CORE_ALPN","obfs"=>"CORE_OBFS","obfs-password"=>"CORE_OBFS_PASSWORD","serviceName"=>"CORE_SERVICE_NAME","authority"=>"CORE_AUTHORITY","heartbeatPeriod"=>"CORE_WS_HEARTBEAT","mtu"=>form.Kind=="wireguard"?"CORE_MTU":"CORE_KCP_MTU","tti"=>"CORE_KCP_TTI","cs"=>"CORE_CIPHER_SUITES","fm"=>"CORE_FINAL_MASK","privatekey"=>"CORE_PRIVATE_KEY","address"=>"CORE_ADDRESSES","dns"=>"CORE_DNS","publickey"=>"CORE_PUBLIC_KEY","presharedkey"=>"CORE_PRESHARED_KEY","endpoint"=>"CORE_ENDPOINT","allowedips"=>"CORE_ALLOWED_IPS","persistentkeepalive"=>"CORE_KEEPALIVE","method"=>"SHADOWSOCKS_METHOD","cipher"=>"VMESS_SECURITY",_=>key};
        foreach(var (key,value) in fields){
            if(key is "Telegram" or "insecure" or "fragment")continue;
            string[]? choices=key switch{"type"=>form.Kind=="vless"?["ws","tcp","xhttp","grpc","httpupgrade","kcp"]:["tcp","ws","grpc","httpupgrade"],"security"=>form.Kind=="vless"?["none","tls","reality"]:["none","tls"],"mode"=>["auto","packet-up","stream-up","stream-one"],"flow"=>["","xtls-rprx-vision"],"obfs"=>["","salamander"],"method"=>["aes-128-gcm","aes-256-gcm","chacha20-poly1305","xchacha20-poly1305"],_=>null};
            if(choices!=null){var c=Choice(panel,LabelKey(key),choices,value);controls[key]=c.Parent as Control??c;values[key]=()=>c.SelectedItem?.ToString()??value;c.SelectionChanged+=(_,_)=>Refresh();}
            else{var input=Field(panel,LabelKey(key),isNew&&key is "server" or "auth" or "privatekey" or "publickey"?"":value,key is "extra" or "fm",key is "auth" or "privatekey" or "presharedkey" or "encryption" or "obfs-password");controls[key]=input.Parent as Control??input;values[key]=()=>input.Text??"";}
        }
        void Refresh(){if(form.Kind is not("vless" or "trojan" or "vmess"))return;string Get(string k)=>values.TryGetValue(k,out var read)?read():fields.GetValueOrDefault(k,"");string type=Get("type"),security=Get("security");void Visible(string k,bool yes){if(controls.TryGetValue(k,out var c))c.IsVisible=yes;}foreach(string k in new[]{"sni","fp"})Visible(k,security!="none");Visible("alpn",security=="tls");Visible("cs",security=="tls");Visible("fm",security=="tls");foreach(string k in new[]{"pbk","sid","spx"})Visible(k,security=="reality");Visible("host",type is "ws" or "xhttp" or "httpupgrade");Visible("path",type is "ws" or "xhttp" or "httpupgrade");Visible("heartbeatPeriod",type=="ws");Visible("mtu",type=="kcp");Visible("tti",type=="kcp");foreach(string k in new[]{"serviceName","authority"})Visible(k,type=="grpc");foreach(string k in new[]{"mode","extra"})Visible(k,type=="xhttp");Visible("flow",type=="tcp"&&security!="none");}
        panel.Children.Add(ActionButton("CORE_SAVE_PROFILE",()=>{RequireIdle();var edits=new Dictionary<string,string>(fields);foreach(var (k,get) in values)edits[k]=get();Profile changed;bool copy=false;if(core!=null){var result=CoreProfileEditing.Apply(original,name.Text??"",edits);changed=result.Profile;copy=result.CreatesCopy;}else changed=ExtraProfileEditing.Apply(original,name.Text??"",edits);if(isNew){changed.Id=Guid.NewGuid().ToString("N");Commit(d=>d.Profiles.Add(changed));}else SaveEdited(original,changed,copy);if(isNew)GoHome();else Back();},true));
        panel.Children.Add(ActionButton("ADVANCED_RAW",()=>ShowRawEditor(original,isNew)));Refresh();Navigate(isNew?"ADD_MANUAL":"EDIT_PROFILE",panel);
    }
    void ShowRawEditor(Profile original,bool isNew=false)
    {
        var panel=Page();panel.Children.Add(Label(L.Text("MAC_RAW_OPTION_HINT"),13,"Muted"));var raw=Field(panel,"ADVANCED_RAW",original.Raw,true);panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();var draft=MacImport.Read(raw.Text??"");if(draft.Count!=1)throw new SafeError("INVALID_IMPORT");if(isNew)Commit(d=>d.Profiles.Add(draft[0]));else SaveEdited(original,MacProfileEditing.Replace(original,draft[0].Raw));
            if(isNew){GoHome();return;}
            var saved=data.Profiles.Single(p=>p.Id==original.Id);Back();
            if(route=="EDIT_PROFILE"){Back();ShowEditor(saved);}
        },true));Navigate("ADVANCED_RAW",panel);
    }
    void SaveEdited(Profile original,Profile changed,bool copy=false)
    {
        Commit(d=>{
            if(copy){changed.Id=Guid.NewGuid().ToString("N");changed.SourceId="";changed.ItemId="";d.Profiles.Add(changed);return;}
            int index=d.Profiles.FindIndex(p=>p.Id==original.Id);if(index<0)throw new SafeError("INVALID_IMPORT");
            if(original.SourceId.Length>0&&!d.OverrideSources.ContainsKey(original.Id)){d.OverrideSources[original.Id]=original.Raw;d.OverrideNames[original.Id]=original.Name;}
            // Host keys are scoped to host:port. A changed endpoint cannot reuse a pin.
            if(TransportPlan.UsesSlip(original.Raw)&&(!TransportPlan.UsesSlip(changed.Raw)||SlipCertificate.Scope(original)!=SlipCertificate.Scope(changed)))d.SlipCertificates.Remove(original.Id);
            d.Profiles[index]=changed;d.Measurements.Remove(original.Id);
        });
    }
    void ShowEndpointEditor(Profile original)
    {
        var endpoint=EndpointEditing.Read(original);if(endpoint==null){ShowRawEditor(original);return;}var panel=Page();var name=Field(panel,"PROFILE_NAME",original.Name);var host=Field(panel,"SERVER_HOST",endpoint.Host);var port=Field(panel,"SERVER_PORT",endpoint.Port.ToString());panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();string raw=EndpointEditing.Apply(original,name.Text??"",host.Text??"",port.Text??"");SaveEdited(original,ProfileEditing.Replace(original,raw));Back();},true));panel.Children.Add(ActionButton("ADVANCED_RAW",()=>ShowRawEditor(original)));Navigate("EDIT_PROFILE",panel);
    }
    void ShowNativeManual(string kind)
    {
        var panel=Page();panel.Children.Add(Label(kind,22));var name=Field(panel,"PROFILE_NAME",kind);var host=Field(panel,"SERVER_HOST");var port=Field(panel,"SERVER_PORT",kind=="SSH"?"22":kind=="SOCKS5"?"1080":"443");var user=Field(panel,"USERNAME");var password=Field(panel,"PASSWORD","",secret:true);
        panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();string raw=kind=="SOCKS5"?"socks5://"+(string.IsNullOrEmpty(user.Text)?"":Uri.EscapeDataString(user.Text)+":"+Uri.EscapeDataString(password.Text??"")+"@")+(host.Text?.Contains(':')==true?"["+host.Text+"]":host.Text)+":"+Importer.Port(port.Text??"")+"#"+Uri.EscapeDataString(name.Text??""):ManualProfiles.Native(kind=="SSH"?"ssh":"naive",name.Text??"",host.Text??"",Importer.Port(port.Text??""),user.Text??"",password.Text??"");var p=Importer.Single(raw);Commit(d=>d.Profiles.Add(p));GoHome();},true));Navigate("ADD_MANUAL",panel);
    }
    void ShowDnsManual(string title)
    {
        string kind=title switch{"DNSTT"=>"dnstt","NoizDNS"=>"sayedns","VayDNS"=>"vaydns","Soar"=>"soar",_=>"slipstream"};
        var seed=MacProtocols.Single(ManualProfiles.Dns(kind,title,"t.example.invalid","127.0.0.1:53",kind=="slipstream"?"udp":"tcp",new string('a',64),"","22","",""));ShowDnsForm(seed,true);
    }
    void ShowDnsEditor(Profile original)=>ShowDnsForm(original,false);
    async Task ChooseCertificate(Profile profile)
    {
        await Guard(async()=>{var files=await StorageProvider.OpenFilePickerAsync(new(){Title=L.Text("SLIP_CERT_CHOOSE"),AllowMultiple=false,FileTypeFilter=[new("PEM"){Patterns=["*.pem","*.crt"]}]});if(files.Count==0)return;await using var stream=await files[0].OpenReadAsync();string cert=SlipCertificate.ReadPublic(await ReadText(stream));using var identity=System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(cert);
            string fingerprint=identity.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
            if(!await Confirm(L.Text("MAC_SLIP_TRUST_REVIEW")+"\n\n"+Importer.Fields(profile.Raw)[3]+"\nSHA256 "+fingerprint))return;
            Commit(d=>{d.SlipCertificates[SlipCertificate.Scope(profile)]=cert;d.SlipCertificates.Remove(profile.Id);});message=L.Text("SETTINGS_SAVED");UpdateStatus();});
    }
}
