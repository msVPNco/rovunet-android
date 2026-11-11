using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

// URI subsets and engine settings follow Android SupplementalCore at
// 5e66f167c85aecac26008d930a7a24df6f2a31f2. Original sharing text is retained.
public static class MacSupplemental
{
    public static bool Handles(string text)=>text.Split("://",2)[0].ToLowerInvariant() is "anytls" or "tuic" or "mierus";

    static readonly Dictionary<string,string[]> options=new()
    {
        ["anytls"]=["sni","peer","alpn","fp","insecure","allowInsecure","allow_insecure","idle-session-check-interval","idle-session-timeout","min-idle-session"],
        ["tuic"]=["sni","peer","alpn","insecure","allowInsecure","allow_insecure","congestion_control","udp_relay_mode","reduce_rtt","heartbeat"],
        ["mierus"]=["profile","port","protocol","multiplexing","handshake-mode","traffic-pattern"]
    };

    public static ParsedLink Parse(string text)
    {
        if(text.Length>65536||!Uri.TryCreate(text,UriKind.Absolute,out var uri)||!options.ContainsKey(uri.Scheme)||
            Uri.CheckHostName(uri.Host.Trim('[',']'))==UriHostNameType.Unknown||uri.AbsolutePath is not("" or "/"))
            throw new SafeError("INVALID_IMPORT");
        string scheme=uri.Scheme;
        var query=MacProtocols.Query(uri);
        foreach(string key in query.Keys)
            if(!options[scheme].Contains(key))throw new ImportFailure("UNSUPPORTED_OPTION",key);
        string auth=Uri.UnescapeDataString(uri.UserInfo);
        if(string.IsNullOrWhiteSpace(auth)||auth.Any(char.IsControl)||query.Values.Any(value=>value.Any(char.IsControl)))
            throw new SafeError("CORE_AUTH_REQUIRED");
        int port=scheme=="mierus"?Importer.Port(query.GetValueOrDefault("port","")):
            uri.Port==-1?443:Importer.Port(uri.Port.ToString());
        if(scheme=="mierus"&&(uri.Port!=-1||!query.ContainsKey("profile")))throw new SafeError("UNSUPPORTED_OPTION");
        string protocol=scheme=="mierus"?"mieru":scheme;
        var settings=new JsonObject{["name"]="proxy",["type"]=protocol,["server"]=uri.Host.Trim('[',']'),["port"]=port,["udp"]=true};
        if(scheme!="mierus")
        {
            foreach(string flag in new[]{"insecure","allowInsecure","allow_insecure"})
                if(query.TryGetValue(flag,out var value)&&value.ToLowerInvariant() is not("" or "0" or "false"))throw new SafeError("TLS_UNVERIFIED");
            settings["skip-cert-verify"]=false;
            string sni=query.GetValueOrDefault("sni",""),peer=query.GetValueOrDefault("peer","");
            if(sni.Length>0&&peer.Length>0&&sni!=peer)throw new SafeError("UNSUPPORTED_OPTION");
            settings["sni"]=sni.Length>0?sni:peer.Length>0?peer:uri.Host.Trim('[',']');
            if(query.TryGetValue("alpn",out var alpn))
            {
                string[] values=alpn.Split(',');
                if(values.Length is <1 or >8||values.Any(value=>string.IsNullOrWhiteSpace(value)||value.Length>255))throw new SafeError("UNSUPPORTED_OPTION");
                settings["alpn"]=new JsonArray(values.Select(value=>(JsonNode?)JsonValue.Create(value)).ToArray());
            }
        }
        void Number(string source,string target,int minimum,int maximum,string suffix="")
        {
            if(!query.TryGetValue(source,out var raw))return;
            if(suffix.Length>0&&raw.EndsWith(suffix,StringComparison.Ordinal))raw=raw[..^suffix.Length];
            if(!int.TryParse(raw,out int value)||value<minimum||value>maximum)throw new ImportFailure("UNSUPPORTED_OPTION",source);
            settings[target]=value;
        }
        void Choice(string source,string target,params string[] values)
        {
            if(!query.TryGetValue(source,out var value))return;
            if(!values.Contains(value))throw new ImportFailure("UNSUPPORTED_OPTION",source);
            settings[target]=value;
        }
        switch(scheme)
        {
            case "anytls":
                settings["password"]=auth;
                Choice("fp","client-fingerprint","chrome","firefox","safari","ios","android","edge","random","randomized");
                Number("idle-session-check-interval","idle-session-check-interval",0,600);
                Number("idle-session-timeout","idle-session-timeout",0,600);
                Number("min-idle-session","min-idle-session",0,8);
                break;
            case "tuic":
                string[] pair=auth.Split(':',2);
                if(pair.Length!=2||string.IsNullOrWhiteSpace(pair[1])||!Guid.TryParseExact(pair[0],"D",out var id))throw new SafeError("CORE_AUTH_REQUIRED");
                settings["uuid"]=id.ToString();settings["password"]=pair[1];
                Choice("congestion_control","congestion-controller","cubic","new_reno","bbr");
                Choice("udp_relay_mode","udp-relay-mode","native","quic");
                if(query.TryGetValue("reduce_rtt",out var reduced))
                {
                    if(reduced.ToLowerInvariant() is not("true" or "false" or "0" or "1"))throw new SafeError("UNSUPPORTED_OPTION");
                    settings["reduce-rtt"]=reduced.ToLowerInvariant() is "true" or "1";
                }
                Number("heartbeat","heartbeat-interval",1000,600000,"ms");
                break;
            case "mierus":
                string[] credentials=uri.UserInfo.Split(':',2).Select(Uri.UnescapeDataString).ToArray();
                if(credentials.Length!=2||credentials.Any(string.IsNullOrWhiteSpace))throw new SafeError("CORE_AUTH_REQUIRED");
                settings["username"]=credentials[0];settings["password"]=credentials[1];
                if(!query.ContainsKey("protocol"))throw new SafeError("UNSUPPORTED_TRANSPORT");
                Choice("protocol","transport","TCP","UDP");
                Choice("multiplexing","multiplexing","MULTIPLEXING_OFF","MULTIPLEXING_LOW","MULTIPLEXING_MIDDLE","MULTIPLEXING_HIGH");
                Choice("handshake-mode","handshake-mode","HANDSHAKE_DEFAULT","HANDSHAKE_STANDARD","HANDSHAKE_NO_WAIT");
                if(query.TryGetValue("traffic-pattern",out var pattern))
                {
                    if(pattern.Length>4096)throw new SafeError("UNSUPPORTED_OPTION");
                    string normalized=pattern.Replace('-','+').Replace('_','/');
                    try{_=Convert.FromBase64String(normalized.PadRight((normalized.Length+3)/4*4,'='));}catch(FormatException){throw new SafeError("UNSUPPORTED_OPTION");}
                    settings["traffic-pattern"]=pattern;
                }
                break;
        }
        string name=Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));
        if(string.IsNullOrWhiteSpace(name))name=query.GetValueOrDefault("profile",protocol.ToUpperInvariant());
        string label=scheme switch{"anytls"=>"AnyTLS","tuic"=>"TUIC",_=>"Mieru"};
        return new(name,label,new JsonObject{["protocol"]=protocol,["settings"]=settings});
    }

    public static CoreProfileForm Form(string raw)
    {
        var parsed=Parse(raw);var uri=new Uri(raw);var query=MacProtocols.Query(uri);
        var fields=new Dictionary<string,string>{{"server",uri.Host.Trim('[',']')},{"port",parsed.Outbound["settings"]!["port"]!.ToString()}};
        if(uri.Scheme=="anytls")fields["auth"]=Uri.UnescapeDataString(uri.UserInfo);
        else
        {
            string[] pair=(uri.Scheme=="mierus"?uri.UserInfo:Uri.UnescapeDataString(uri.UserInfo)).Split(':',2);
            fields[uri.Scheme=="mierus"?"username":"uuid"]=uri.Scheme=="mierus"?Uri.UnescapeDataString(pair[0]):pair[0];
            fields["password"]=uri.Scheme=="mierus"?Uri.UnescapeDataString(pair[1]):pair[1];
        }
        foreach(string key in options[uri.Scheme].Where(key=>key!="port"&&(key is not("insecure" or "allowInsecure" or "allow_insecure")||query.ContainsKey(key))))fields[key]=query.GetValueOrDefault(key,"");
        return new(){Kind=uri.Scheme,Fields=fields};
    }

    public static string Build(string kind,string name,IReadOnlyDictionary<string,string> fields)
    {
        string host=fields["server"].Trim().Trim('[',']');
        if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("CORE_HOST_INVALID");
        host=host.Contains(':')?"["+host+"]":host;
        int port=Importer.Port(fields["port"]);
        string auth=kind=="anytls"?Uri.EscapeDataString(fields["auth"]):
            Uri.EscapeDataString(fields[kind=="mierus"?"username":"uuid"])+":"+Uri.EscapeDataString(fields["password"]);
        var query=options[kind].Where(key=>key!="port"&&fields.GetValueOrDefault(key,"").Length>0)
            .ToDictionary(key=>key,key=>fields[key]);
        if(kind=="mierus"){query["port"]=port.ToString();if(!query.ContainsKey("profile"))query["profile"]=name;}
        string raw=kind+"://"+auth+"@"+host+(kind=="mierus"?"":":"+port)+"?"+
            string.Join('&',query.Select(pair=>Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(pair.Value)))+"#"+Uri.EscapeDataString(name);
        _=Parse(raw);return raw;
    }

    public static string Runtime(Profile profile,int port)
    {
        var parsed=MacProtocols.Extended(profile.Raw)??throw new SafeError("UNSUPPORTED_FORMAT");
        if(parsed.Protocol is not("AnyTLS" or "Mieru" or "TUIC"))throw new SafeError("UNSUPPORTED_FORMAT");
        return new JsonObject{
            ["inbounds"]=new JsonArray(new JsonObject{["listen"]="127.0.0.1",["port"]=port,["protocol"]="socks",["settings"]=new JsonObject{["auth"]="noauth"}}),
            ["outbounds"]=new JsonArray(parsed.Outbound.DeepClone(),new JsonObject{["protocol"]="freedom"})
        }.ToJsonString();
    }
}
