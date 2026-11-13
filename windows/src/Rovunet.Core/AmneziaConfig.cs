using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Rovunet.Core;

namespace Rovunet.Core;

// Released Android single-peer AWG schema, with the same pinned Mihomo adapter.
public static class AmneziaConfig
{
    public static readonly IReadOnlyDictionary<string,string> Labels=new Dictionary<string,string>{
        ["jc"]="Jc",["jmin"]="Jmin",["jmax"]="Jmax",["s1"]="S1",["s2"]="S2",["s3"]="S3",["s4"]="S4",["h1"]="H1",["h2"]="H2",["h3"]="H3",["h4"]="H4",["i1"]="I1",["i2"]="I2",["i3"]="I3",["i4"]="I4",["i5"]="I5",["j1"]="J1",["j2"]="J2",["j3"]="J3",["itime"]="Itime",["version"]="Version",["headerprotectionkey"]="HeaderProtectionKey",["contentpaddingaddition"]="ContentPaddingAddition",["rekeyaftertime"]="RekeyAfterTime",["rekeytimeout"]="RekeyTimeout",["rejectaftertime"]="RejectAfterTime",["keepalivetimeout"]="KeepaliveTimeout",["maxhandshakeattempts"]="MaxHandshakeAttempts",["randomtrailers"]="RandomTrailers",["disablecookies"]="DisableCookies"};
    static readonly Dictionary<string,string> mapped=new(){["headerprotectionkey"]="header-protection-key",["contentpaddingaddition"]="content-padding-addition",["rekeyaftertime"]="rekey-after-time",["rekeytimeout"]="rekey-timeout",["rejectaftertime"]="reject-after-time",["keepalivetimeout"]="keepalive-timeout",["maxhandshakeattempts"]="max-handshake-attempts",["randomtrailers"]="random-trailers",["disablecookies"]="disable-cookies"};
    public static bool Handles(string raw)=>raw.Split('\n').Any(l=>Labels.ContainsKey(l.Split('=')[0].Trim().ToLowerInvariant()));
    public static string Document(Uri uri,IReadOnlyDictionary<string,string> q)
    {
        if(uri.Host.Length==0||uri.Port is <1 or >65535||q.Keys.Any(k=>!new[]{"publickey","address","allowedips","dns","mtu","presharedkey","persistentkeepalive"}.Contains(k)&&!Labels.ContainsKey(k)))throw new SafeError("UNSUPPORTED_FORMAT");
        string Need(string key)=>q.TryGetValue(key,out var value)&&value.Length>0?value:throw new SafeError("INVALID_IMPORT");string secret=Uri.UnescapeDataString(uri.UserInfo);
        if(q.Values.Append(secret).Any(v=>v.Any(char.IsControl)))throw new SafeError("INVALID_IMPORT");
        var text=new StringBuilder("[Interface]\nPrivateKey = "+secret+"\nAddress = "+Need("address"));foreach(var p in new[]{("dns","DNS"),("mtu","MTU")})if(q.TryGetValue(p.Item1,out var v))text.Append('\n').Append(p.Item2).Append(" = ").Append(v);
        foreach(var p in Labels)if(q.TryGetValue(p.Key,out var v))text.Append('\n').Append(p.Value).Append(" = ").Append(v);
        text.Append("\n[Peer]\nPublicKey = ").Append(Need("publickey")).Append("\nEndpoint = ").Append(uri.Host.Contains(':')?"["+uri.Host.Trim('[',']')+"]":uri.Host).Append(':').Append(uri.Port).Append("\nAllowedIPs = ").Append(Need("allowedips"));
        foreach(var p in new[]{("presharedkey","PresharedKey"),("persistentkeepalive","PersistentKeepalive")})if(q.TryGetValue(p.Item1,out var v))text.Append('\n').Append(p.Item2).Append(" = ").Append(v);return text.ToString();
    }
    public static ParsedLink Parse(string raw)
    {
        if(raw.Length>65536)throw new SafeError("IMPORT_TOO_LARGE");var options=new Dictionary<string,string>();var plain=new List<string>();string section="";
        foreach(string original in raw.Split('\n')){string line=original.Split('#')[0].Trim();if(line.StartsWith('['))section=line.ToLowerInvariant();string name=line.Split('=')[0].Trim().ToLowerInvariant();
            if(!Labels.ContainsKey(name)){plain.Add(original);continue;}if(section!="[interface]"||!line.Contains('='))throw new SafeError("UNSUPPORTED_FORMAT");string value=line.Split('=',2)[1].Trim();if(value.Length>2048||value.Any(char.IsControl)||!options.TryAdd(name,value))throw new SafeError("UNSUPPORTED_FORMAT");}
        var basic=CoreLinks.Parse(string.Join('\n',plain));var wg=basic.Outbound["settings"]!.AsObject();var peer=wg["peers"]![0]!.AsObject();var endpoint=new Uri("wg://"+peer["endpoint"]!.ToString());
        var settings=new JsonObject{["name"]="proxy",["type"]="wireguard",["server"]=endpoint.Host.Trim('[',']'),["port"]=endpoint.Port,["private-key"]=wg["secretKey"]!.DeepClone(),["public-key"]=peer["publicKey"]!.DeepClone(),["allowed-ips"]=peer["allowedIPs"]!.DeepClone(),["mtu"]=wg["mtu"]!.DeepClone(),["udp"]=true};
        if(peer["preSharedKey"] is {} psk)settings["pre-shared-key"]=psk.DeepClone();if(peer["keepAlive"] is {} keep)settings["persistent-keepalive"]=keep.DeepClone();
        foreach(var node in wg["address"]!.AsArray()){string address=node!.ToString(),field=address.Contains(':')?"ipv6":"ip";if(settings.ContainsKey(field))throw new SafeError("UNSUPPORTED_FORMAT");settings[field]=address;}
        if(wg["remoteDNS"] is {} dns){settings["dns"]=dns.DeepClone();settings["remote-dns-resolve"]=true;}
        bool v3=options.ContainsKey("headerprotectionkey")||options.GetValueOrDefault("version") is "3" or "3.0" or "3.1";var awg=new JsonObject();
        foreach(var (key,value) in options){string target=mapped.GetValueOrDefault(key,key);
            if(key=="version"){if(value is not("1" or "1.0" or "1.5" or "2" or "2.0" or "3" or "3.0" or "3.1"))throw new SafeError("UNSUPPORTED_FORMAT");}
            else if(key=="headerprotectionkey"){try{if(Convert.FromBase64String(value).Length!=32)throw new FormatException();}catch{throw new SafeError("INVALID_IMPORT");}awg[target]=value;}
            else if(key is "randomtrailers" or "disablecookies"){if(value.ToLowerInvariant() is not("true" or "false" or "0" or "1"))throw new SafeError("UNSUPPORTED_FORMAT");awg[target]=value.ToLowerInvariant() is "true" or "1";}
            else if(key is "jc" or "jmin" or "jmax" or "s1" or "s2" or "s3" or "s4" or "itime"){if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int number)||number is <0 or >65535)throw new SafeError("UNSUPPORTED_FORMAT");awg[target]=number;}
            else if(key.StartsWith('h')||mapped.ContainsKey(key)){if(!Regex.IsMatch(value,@"^[0-9]+(?:-[0-9]+)?$",RegexOptions.CultureInvariant))throw new SafeError("UNSUPPORTED_FORMAT");var parts=value.Split('-');if(parts.Any(p=>!uint.TryParse(p,NumberStyles.None,CultureInfo.InvariantCulture,out _))||uint.Parse(parts[0],CultureInfo.InvariantCulture)>uint.Parse(parts[^1],CultureInfo.InvariantCulture))throw new SafeError("UNSUPPORTED_FORMAT");awg[target]=value;}
            else awg[target]=value;
        }
        if(v3){if(options.Keys.Any(k=>k is "j1" or "j2" or "j3" or "itime"))throw new SafeError("UNSUPPORTED_FORMAT");awg["version"]=3;}else if(options.Keys.Any(k=>mapped.ContainsKey(k)||k=="headerprotectionkey"))throw new SafeError("UNSUPPORTED_FORMAT");
        if(int.Parse(options.GetValueOrDefault("jmin","0"),CultureInfo.InvariantCulture)>int.Parse(options.GetValueOrDefault("jmax","65535"),CultureInfo.InvariantCulture))throw new SafeError("UNSUPPORTED_FORMAT");
        settings["amnezia-wg-option"]=awg;return new("AmneziaWG","AmneziaWG",new JsonObject{["protocol"]="amneziawg",["settings"]=settings});
    }
    public static string Runtime(Profile p,int port,string user,string password)
    {
        var parsed=Importer.Connection(p);if(parsed.Protocol!="AmneziaWG")throw new SafeError("INVALID_IMPORT");
        if(port is <1 or >65535||user.Length<16||password.Length<16)throw new SafeError("INVALID_MESSAGE");
        return new JsonObject{["inbounds"]=new JsonArray(new JsonObject{["listen"]="127.0.0.1",["port"]=port,["protocol"]="socks",["settings"]=new JsonObject{["auth"]="password",["accounts"]=new JsonArray(new JsonObject{["user"]=user,["pass"]=password})}}),["outbounds"]=new JsonArray(parsed.Outbound.DeepClone(),new JsonObject{["protocol"]="freedom"})}.ToJsonString();
    }
}
