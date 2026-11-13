using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;
// Separate privileged-broker contract. Never add these fields to native feed v1.
public sealed class TunRequest29
{
    public int Version{get;set;}=1;
    public string Action{get;set;}="status";
    public Guid Session{get;set;}
    public int SocksPort{get;set;}
    public string[] Peers{get;set;}=[];
    public bool KillSwitch{get;set;}
    public bool EnableUdp{get;set;}
    public bool ExperimentalAcknowledged{get;set;}
    public override string ToString()=>"NetworkBrokerRequest(redacted)";
}
public sealed class TunReply29
{
    public Guid Session{get;set;}
    public string State{get;set;}="inactive";
    public string Error{get;set;}="";
    public bool TunReady{get;set;}
    public bool ProtectionInstalled{get;set;}
    public bool TrafficVerified{get;set;}
    public bool UdpForwarding{get;set;}
}
public static class TunPlan29
{
    public const string Adapter="Rovunet-TUN";
    // Match the pinned Xray adapter-name identifier (tun_windows.go). This is
    // an identifier, never a cryptographic trust or authentication primitive.
    public static Guid AdapterGuid=>new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(Adapter)));
    public const string Gateway4="172.31.255.1";
    public const string Gateway6="fdab:726f:7675::1";
    public static IPAddress[] Validate(TunRequest29 request)
    {
        if(request.Version is not(1 or 2)||request.Version==1&&request.EnableUdp||request.Session==Guid.Empty||request.Peers==null||request.Action is not("start" or "stop" or "status" or "recover"))throw new SafeError("BROKER_REQUEST_REJECTED");
        if(request.Action!="start"){if(request.Peers.Length!=0||request.SocksPort!=0||request.KillSwitch||request.EnableUdp)throw new SafeError("BROKER_REQUEST_REJECTED");return [];}
        if(!request.ExperimentalAcknowledged||request.SocksPort is <1024 or >65535||request.Peers.Length is <1 or >16)throw new SafeError("BROKER_REQUEST_REJECTED");
        var peers=new HashSet<IPAddress>();foreach(string text in request.Peers){
            if(text==null||text.Length>45||!IPAddress.TryParse(text,out var ip)||ip.IsIPv4MappedToIPv6||IPAddress.IsLoopback(ip)||ip.Equals(IPAddress.Any)||ip.Equals(IPAddress.IPv6Any)||ip.IsIPv6Multicast||ip.IsIPv6LinkLocal||ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&ip.GetAddressBytes()[0]>=224||ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6&&ip.ScopeId!=0)throw new SafeError("BROKER_REQUEST_REJECTED");
            if(!peers.Add(ip))throw new SafeError("BROKER_REQUEST_REJECTED");
        }return peers.ToArray();
    }
    public static string Config(int socksPort,bool enableUdp=false)
    {
        if(socksPort is <1024 or >65535)throw new SafeError("BROKER_REQUEST_REJECTED");
        // Routing/DNS ownership and persistent WFP are managed by the broker.
        // Xray's dynamic autoSystemWfpBlockLeak is not a crash-safe kill switch.
        var config=new JsonObject{
            ["log"]=new JsonObject{["loglevel"]="none"},
            ["dns"]=new JsonObject{["tag"]="resolver",["disableFallback"]=true,["servers"]=new JsonArray("https://1.1.1.1/dns-query")},
            ["inbounds"]=new JsonArray(new JsonObject{["tag"]="system-tun",["protocol"]="tun",["settings"]=new JsonObject{["name"]=Adapter,["desc"]="Rovunet experimental TCP and DNS tunnel",["mtu"]=1400,["gateway"]=new JsonArray(Gateway4+"/30",Gateway6+"/126"),["autoOutboundsInterface"]=""}}),
            ["outbounds"]=new JsonArray(
                new JsonObject{["tag"]="selected-tunnel",["protocol"]="socks",["settings"]=new JsonObject{["servers"]=new JsonArray(new JsonObject{["address"]="127.0.0.1",["port"]=socksPort})}},
                new JsonObject{["tag"]="dns-query",["protocol"]="dns",["settings"]=new JsonObject{["network"]="tcp",["nonIPQuery"]="drop"}},
                new JsonObject{["tag"]="unsupported-udp",["protocol"]="blackhole",["settings"]=new JsonObject()}),
            ["routing"]=new JsonObject{["domainStrategy"]="AsIs",["rules"]=new JsonArray(
                new JsonObject{["type"]="field",["inboundTag"]=new JsonArray("resolver"),["outboundTag"]="selected-tunnel"},
                new JsonObject{["type"]="field",["inboundTag"]=new JsonArray("system-tun"),["port"]="53",["outboundTag"]="dns-query"},
                new JsonObject{["type"]="field",["network"]="udp",["outboundTag"]="unsupported-udp"})}
        };
        if(enableUdp){
            var rules=(JsonArray)config["routing"]!["rules"]!;rules.RemoveAt(rules.Count-1);
            var outbounds=(JsonArray)config["outbounds"]!;outbounds.RemoveAt(outbounds.Count-1);
        }
        return config.ToJsonString();
    }
}
