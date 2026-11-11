using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void NewVless20(string kind)
    {
        string transport=kind switch{"REALITY" or "VLESS Encryption"=>"tcp","XHTTP"=>"xhttp","gRPC"=>"grpc","HTTPUpgrade"=>"httpupgrade","mKCP"=>"kcp",_=>"ws"};
        string security=kind=="REALITY"?"reality":transport=="kcp"?"none":"tls";
        string raw="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type="+transport+"&security="+security+(security=="reality"?"&fp=chrome&pbk="+Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+','-').Replace('/','_'):"")+"#VLESS";
        BuildCoreProfileEditor(Importer.Single(raw),profile=>{
            if(data.Profiles.Any(p=>p.Raw==profile.Raw))throw new SafeError("DUPLICATE_PROFILE");
            var before=data.Profiles.ToList();data.Profiles.Add(profile);try{Save();}catch{data.Profiles=before;throw;}BuildCards();Notify(L.Text("IMPORT_ADDED")+" 1");
        }).ShowDialog();
    }
    void NewExtraProfile(string kind)
    {
        string raw=kind switch{
            "Trojan"=>"trojan://draft@server.example.invalid:443#Trojan",
            "VMess"=>"vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{v="2",ps="VMess",add="server.example.invalid",port=443,id="00000000-0000-4000-8000-000000000001",aid=0,scy="auto",net="tcp",tls="tls"}))),
            _=>"ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-256-gcm:draft"))+"@server.example.invalid:443#Shadowsocks"
        };
        var seed=Importer.Single(raw);BuildExtraProtocolEditor(seed,profile=>{
            if(data.Profiles.Any(p=>p.Raw==profile.Raw))throw new SafeError("DUPLICATE_PROFILE");
            var before=data.Profiles.ToList();data.Profiles.Add(profile);try{Save();}catch{data.Profiles=before;throw;}BuildCards();Notify(L.Text("IMPORT_ADDED")+" 1");
        }).ShowDialog();
    }
    void NewProfileSelection()
    {
        var page=Dialog("ADD");var panel=new StackPanel{Margin=new(20)};
        panel.Children.Add(Text(L.Text("CHOOSE_CONNECTION_TYPE"),16));
        void Section(string key,string[] names){panel.Children.Add(Text(L.Text(key),13,Muted));var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new(0,8,0,16)};foreach(string name in names){var button=Button("ADD",()=>{if(name is "Trojan" or "VMess" or "Shadowsocks")NewExtraProfile(name);else if(name is "REALITY" or "XHTTP" or "VLESS WS" or "VLESS Encryption" or "gRPC" or "HTTPUpgrade" or "mKCP")NewVless20(name);else BuildAddConfiguration(2,manualProtocol:name).ShowDialog();});button.Content=name;button.MinHeight=52;grid.Children.Add(button);}panel.Children.Add(Card(grid));}
        Section("WEB_TCP_PATHS",["REALITY","XHTTP","VLESS WS","VLESS Encryption","gRPC","HTTPUpgrade","SSH","SOCKS5","Trojan","VMess","Shadowsocks","NaiveProxy"]);
        Section("DNS_FAMILIES",["DNSTT","NoizDNS","VayDNS","Soar","Slipstream"]);
        Section("UDP_PATHS",["Hysteria2","WireGuard","mKCP"]);
        page.Content=panel;page.ShowDialog();
    }
}
