using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

// Adds the released Android dialects without editing the pinned Windows Core.
// Original sharing text and native restriction/credential envelopes are retained.
public static class MacProtocols
{
    const string Seed="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=tcp&security=tls";
    public static Profile Single(string raw)
    {
        var parsed=Extended(raw);if(parsed==null)return Importer.Single(raw);
        Profile profile;
        if(ProfileEditing.IsNative(raw))profile=Importer.Single(ValidationEnvelope(raw));else profile=new();
        profile.Raw=raw;profile.Protocol=parsed.Protocol;profile.Name=string.IsNullOrWhiteSpace(parsed.Name)?parsed.Protocol:parsed.Name;if(profile.Name.Length>200||profile.Name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");if(profile.CollectionId.Length==0)profile.CollectionId="manual:"+Importer.Fingerprint(raw);profile.BlockReason="";return profile;
    }
    public static bool RefreshCapabilities(Profile stored)
    {
        string protocol=stored.Protocol,blocked;
        try{var parsed=Single(stored.Raw);protocol=parsed.Protocol;blocked=parsed.BlockReason;}
        catch(SafeError error){blocked=error.Code;}catch{blocked="INVALID_IMPORT";}
        bool changed=stored.Protocol!=protocol||stored.BlockReason!=blocked;stored.Protocol=protocol;stored.BlockReason=blocked;return changed;
    }
    static string Substitute(string raw,string inner){var f=Importer.Decode(raw[(raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');f[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(inner));return raw[..(raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',f)));}
    static string ValidationEnvelope(string raw){var fields=Importer.Fields(raw);if(fields[1]=="xray")return Substitute(raw,Seed);var wire=Importer.Decode(raw[(raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');wire[1]="socks5";wire[3]="127.0.0.1";wire[40]="1080";return raw[..(raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',wire)));}
    public static ParsedLink? Extended(string raw)
    {
        if(raw.Length>131072)throw new SafeError("IMPORT_TOO_LARGE");
        if(ProfileEditing.IsNative(raw)){
            var fields=Importer.Fields(raw);
            if(fields[1] is "doh" or "snowflake"){
                _=Importer.Single(ValidationEnvelope(raw));if(fields[2].Length>200||fields[2].Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
                if(fields[1]=="doh"){_=SubscriptionUrls.Validate(fields[21]);return new(fields[2],"DoH · DNS only",new JsonObject{["protocol"]="freedom",["settings"]=new JsonObject{["domainStrategy"]="UseIP"}});}
                _=TorRuntime.Bridges(raw);return new(fields[2],"Tor",new JsonObject{["protocol"]="socks"});
            }
            if(fields[1]!="xray")return null;
            var inner=Extended(Importer.Decode(fields[84]));if(inner==null)return null;
            _=Importer.Single(Substitute(raw,Seed));return new(fields[2],inner.Protocol,inner.Outbound);
        }
        if(raw.TrimStart().StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase))return MacAmnezia.Handles(raw)?MacAmnezia.Parse(raw):null;
        if(MacSupplemental.Handles(raw))return MacSupplemental.Parse(raw);
        if(raw.StartsWith("ss://",StringComparison.OrdinalIgnoreCase))return Shadowsocks(raw);
        if(raw.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase))return Vmess(raw);
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri))return null;
        var q=Query(uri);if(uri.Scheme=="wireguard"&&q.Keys.Any(MacAmnezia.Labels.ContainsKey))return MacAmnezia.Parse(MacAmnezia.Document(uri,q));
        bool hopping=uri.Scheme is "hy2" or "hysteria2"&&(q.ContainsKey("mport")||q.ContainsKey("hop-interval"));
        if(!hopping&&!q.ContainsKey("cs")&&!q.ContainsKey("fm"))return null;
        if(uri.Scheme is not("vless" or "trojan" or "hy2" or "hysteria2"))return null;
        var plain=new Dictionary<string,string>(q);foreach(string key in new[]{"cs","fm","mport","hop-interval"})plain.Remove(key);
        var parsed=CoreLinks.Parse(WithQuery(uri,plain));Apply(parsed.Outbound,q);
        if(hopping){if(!q.TryGetValue("mport",out var ports)||ports.Length is <1 or >512||ports.Split(',').Any(range=>!Ports(range)))throw new SafeError("UNSUPPORTED_FORMAT");
            string interval=q.GetValueOrDefault("hop-interval","30s");if(interval.EndsWith('s'))interval=interval[..^1];if(!int.TryParse(interval,out int seconds)||seconds is <5 or >3600)throw new SafeError("UNSUPPORTED_FORMAT");
            var stream=parsed.Outbound["streamSettings"]!.AsObject();var mask=stream["finalmask"] as JsonObject??new();stream["finalmask"]=mask;var udp=mask["udp"] as JsonArray??new();mask["udp"]=udp;udp.Add(new JsonObject{["type"]="udphop",["settings"]=new JsonObject{["mode"]="intervalremote",["interval"]=seconds,["remotePorts"]=ports}});
        }
        return parsed;
    }
    static bool Ports(string text){var pair=text.Split('-');return pair.Length is 1 or 2&&pair.All(s=>int.TryParse(s,out int n)&&n is >=1 and <=65535)&&(pair.Length==1||int.Parse(pair[0])<=int.Parse(pair[1]));}
    internal static Dictionary<string,string> Query(Uri uri){var q=new Dictionary<string,string>();foreach(string part in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){var p=part.Split('=',2);if(!q.TryAdd(Uri.UnescapeDataString(p[0]),p.Length>1?Uri.UnescapeDataString(p[1]):""))throw new SafeError("INVALID_IMPORT");}return q;}
    static string WithQuery(Uri uri,IReadOnlyDictionary<string,string> q)=>new UriBuilder(uri){Query=string.Join('&',q.Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)))}.Uri.AbsoluteUri;
    static void Apply(JsonObject outbound,IReadOnlyDictionary<string,string> q)
    {
        if(outbound["streamSettings"] is not JsonObject stream)throw new SafeError("UNSUPPORTED_FORMAT");
        if(q.TryGetValue("cs",out string? cs)){if(stream["security"]?.ToString()!="tls")throw new SafeError("UNSUPPORTED_FORMAT");stream["tlsSettings"]!["cipherSuites"]=MacCoreOptions.CipherSuites(cs);}
        if(q.TryGetValue("fm",out string? fm)){
            var incoming=MacCoreOptions.FinalMask(fm);var existing=stream["finalmask"] as JsonObject??new();foreach(var pair in incoming){if(pair.Key is "tcp" or "udp"&&existing[pair.Key] is JsonArray a&&pair.Value is JsonArray b){foreach(var item in b)a.Add(item?.DeepClone());}else existing[pair.Key]=pair.Value?.DeepClone();}stream["finalmask"]=existing;
        }
    }
    static ParsedLink? Shadowsocks(string raw)
    {
        string body=raw[5..];int hash=body.IndexOf('#');string name=hash>=0?Uri.UnescapeDataString(body[(hash+1)..]):"Shadowsocks 2022";if(hash>=0)body=body[..hash];int query=body.IndexOf('?');if(query>=0){if(query!=body.Length-1)return null;body=body[..query];}
        int at=body.LastIndexOf('@');string auth,endpoint;if(at<0){body=Importer.Decode(body);at=body.LastIndexOf('@');if(at<0)return null;auth=body[..at];endpoint=body[(at+1)..];}else{auth=Uri.UnescapeDataString(body[..at]);if(!auth.Contains(':'))auth=Importer.Decode(auth);endpoint=body[(at+1)..];}
        int colon=auth.IndexOf(':');if(colon<1)return null;string method=auth[..colon],password=auth[(colon+1)..];if(!method.StartsWith("2022-",StringComparison.Ordinal))return null;
        int length=method switch{"2022-blake3-aes-128-gcm"=>16,"2022-blake3-aes-256-gcm" or "2022-blake3-chacha20-poly1305"=>32,_=>throw new SafeError("UNSUPPORTED_FORMAT")};foreach(string key in password.Split(':'))_=MacCoreOptions.Key(key,length);
        if(!Uri.TryCreate("ss://"+endpoint,UriKind.Absolute,out var uri)||uri.Host.Length==0||uri.Port is <1 or >65535||uri.Query.Length>0||uri.AbsolutePath is not("" or "/"))throw new SafeError("INVALID_IMPORT");
        return new(name,"Shadowsocks 2022",new JsonObject{["protocol"]="shadowsocks",["settings"]=new JsonObject{["servers"]=new JsonArray(new JsonObject{["address"]=uri.Host.Trim('[',']'),["port"]=uri.Port,["method"]=method,["password"]=password})}});
    }
    static ParsedLink? Vmess(string raw)
    {
        JsonObject json;var options=new Dictionary<string,string>();
        if(raw.Split('#')[0].Contains('@')){
            if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||!Guid.TryParseExact(Uri.UnescapeDataString(uri.UserInfo),"D",out _))throw new SafeError("INVALID_IMPORT");var q=Query(uri);
            string[] keys=["type","security","sni","fp","path","host","alpn","headerType","serviceName","authority","mode","ed","eh","allowInsecure","insecure","cipher","encryption","cs","fm"];
            if(q.Keys.Any(k=>!keys.Contains(k))||q.GetValueOrDefault("headerType","") is not("" or "none"))throw new SafeError("UNSUPPORTED_FORMAT");
            string net=q.GetValueOrDefault("type","tcp"),security=q.GetValueOrDefault("security","none");if(security is not("none" or "tls"))throw new SafeError("UNSUPPORTED_FORMAT");
            json=new(){["v"]="2",["ps"]=Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),["add"]=uri.Host.Trim('[',']'),["port"]=uri.Port<0?443:uri.Port,["id"]=Uri.UnescapeDataString(uri.UserInfo),["aid"]=0,["scy"]=q.GetValueOrDefault("cipher",q.GetValueOrDefault("encryption","auto")),["net"]=net,["tls"]=security,["type"]="none"};
            foreach(string key in new[]{"sni","fp","path","host","alpn","serviceName","authority","ed","eh","allowInsecure","insecure"})if(q.TryGetValue(key,out var value))json[key]=value;
            if(net=="grpc"){json["serviceName"]=q.GetValueOrDefault("serviceName",q.GetValueOrDefault("path",""));options["grpc-mode"]=q.GetValueOrDefault("mode","gun");json.Remove("path");}
            foreach(string key in new[]{"cs","fm"})if(q.TryGetValue(key,out var v))options[key]=v;
        }else{
            try{json=JsonNode.Parse(Importer.Decode(raw[8..].Split('#')[0]),documentOptions:new JsonDocumentOptions{MaxDepth=16})!.AsObject();}catch{throw new SafeError("INVALID_IMPORT");}
            bool special=json.ContainsKey("cs")||json.ContainsKey("fm")||json["net"]?.ToString()=="grpc"&&json["type"]?.ToString() is "multi" or "gun";if(!special)return null;
            foreach(string key in new[]{"cs","fm"})if(json.TryGetPropertyValue(key,out var value)){options[key]=value?.ToString()??"";json.Remove(key);}
            if(json["net"]?.ToString()=="grpc"){options["grpc-mode"]=json["type"]?.ToString()=="multi"?"multi":"gun";json["type"]="none";json["serviceName"]=json["path"]?.DeepClone()??JsonValue.Create("");json.Remove("path");}
        }
        var parsed=CoreLinks.Parse("vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToJsonString())));if(options.Remove("grpc-mode",out var mode)){if(mode is not("" or "gun" or "multi"))throw new SafeError("UNSUPPORTED_FORMAT");parsed.Outbound["streamSettings"]!["grpcSettings"]!["multiMode"]=mode=="multi";}
        if(options.Count>0)Apply(parsed.Outbound,options);return parsed;
    }
    public static string Runtime(Profile profile,int http,int socks,int helper=0,int stats=0,string tunUser="",string tunPassword="")
    {
        var parsed=Extended(profile.Raw);if(parsed==null)return TunInbound(CoreLinks.Runtime(profile,http,socks,helper,stats),tunUser,tunPassword);
        var seed=Importer.Single(Seed);var root=JsonNode.Parse(CoreLinks.Runtime(seed,http,socks,0,stats))!.AsObject();
        if(parsed.Protocol=="DoH · DNS only")root["dns"]=new JsonObject{["queryStrategy"]="UseIP",["servers"]=new JsonArray(new JsonObject{["address"]=Importer.Fields(profile.Raw)[21],["skipFallback"]=true})};
        var outbound=helper>0?CoreLinks.Socks(profile.Name,"127.0.0.1",helper,"","").Outbound:parsed.Outbound.DeepClone().AsObject();outbound["tag"]="proxy";root["outbounds"]=new JsonArray(outbound);return TunInbound(root.ToJsonString(),tunUser,tunPassword);
    }
    static string TunInbound(string config,string user,string password)
    {
        if(user.Length==0&&password.Length==0)return config;
        if(user.Length!=64||password.Length!=64||user.Concat(password).Any(c=>!char.IsAsciiHexDigit(c)))throw new SafeError("MAC_TUN_INVALID_REQUEST");
        var root=JsonNode.Parse(config)!.AsObject();var inbound=root["inbounds"]!.AsArray().OfType<JsonObject>().Single(i=>i["protocol"]?.ToString()=="socks");
        inbound["settings"]!["auth"]="password";inbound["settings"]!["accounts"]=new JsonArray(new JsonObject{["user"]=user,["pass"]=password});inbound["settings"]!["udp"]=true;
        return root.ToJsonString();
    }
}
