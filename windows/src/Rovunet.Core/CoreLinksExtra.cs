using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;

public static partial class CoreLinks
{
    static readonly HashSet<string> ShadowMethods=new(["aes-128-gcm","aes-256-gcm","chacha20-ietf-poly1305","xchacha20-ietf-poly1305"]);
    static (string Host,int Port) HostPort(string text)
    {
        if(!Uri.TryCreate("ss://"+text,UriKind.Absolute,out var uri)||uri.Query.Length>0||uri.AbsolutePath is not ("" or "/")||uri.Host.Length==0||uri.Port is <1 or >65535)throw new SafeError("INVALID_IMPORT");
        return(uri.Host.Trim('[',']'),uri.Port);
    }
    static ParsedLink Shadowsocks(string raw,string fallbackName)
    {
        var body=raw[5..];int hash=body.IndexOf('#');string name=hash<0?fallbackName:Uri.UnescapeDataString(body[(hash+1)..]);if(hash>=0)body=body[..hash];
        int query=body.IndexOf('?');if(query>=0){if(query!=body.Length-1)throw new SafeError("UNSUPPORTED_FORMAT");body=body[..query];}
        string auth,endpoint;int at=body.LastIndexOf('@');
        if(at>=0){auth=Uri.UnescapeDataString(body[..at]);if(!auth.Contains(':'))auth=Importer.Decode(auth);endpoint=body[(at+1)..];}
        else{body=Importer.Decode(body);at=body.LastIndexOf('@');if(at<1)throw new SafeError("INVALID_IMPORT");auth=body[..at];endpoint=body[(at+1)..];}
        int colon=auth.IndexOf(':');if(colon<1||colon==auth.Length-1)throw new SafeError("INVALID_IMPORT");string method=auth[..colon],password=auth[(colon+1)..];
        if(!ShadowMethods.Contains(method))throw new SafeError("UNSUPPORTED_FORMAT");var hp=HostPort(endpoint);
        return new(name.Length>0?name:"Shadowsocks","Shadowsocks",Obj(("protocol","shadowsocks"),("settings",Obj(("servers",Arr(Obj(("address",hp.Host),("port",hp.Port),("method",method),("password",password))))))));
    }
    static JsonObject ExtraStream(string host,Dictionary<string,string> query,string defaultSecurity)
    {
        string Get(string key,string fallback="")=>query.GetValueOrDefault(key,fallback);
        foreach(string flag in new[]{"allowInsecure","insecure"})if(Get(flag).ToLowerInvariant() is not ("" or "false" or "0"))throw new SafeError("TLS_UNVERIFIED");
        if(Get("headerType") is not ("" or "none")||Get("eh").Length>0)throw new SafeError("UNSUPPORTED_FORMAT");
        string transport=Get("type","tcp") switch{"" or "raw" or "tcp"=>"tcp","ws" or "websocket"=>"ws","grpc"=>"grpc","httpupgrade"=>"httpupgrade",_=>throw new SafeError("UNSUPPORTED_FORMAT")};
        string security=Get("security",defaultSecurity);if(security is not ("tls" or "none"))throw new SafeError("UNSUPPORTED_FORMAT");
        var stream=Obj(("network",transport),("security",security));
        if(security=="tls"){
            string sni=Get("sni",host);if(sni.Length==0)sni=host;if(Uri.CheckHostName(sni)==UriHostNameType.Unknown)throw new SafeError("CORE_SNI_INVALID");
            var tls=Obj(("serverName",sni),("fingerprint",Get("fp").Length>0?Get("fp"):"chrome"),("allowInsecure",false));if(Get("alpn").Length>0)tls["alpn"]=JsonSerializer.SerializeToNode(Get("alpn").Split(','));stream["tlsSettings"]=tls;
        }else if(new[]{"sni","fp","alpn"}.Any(k=>Get(k).Length>0)){
            // Some VMess exporters retain TLS hints with security=none. Keep
            // them in the configuration; the pinned engine does not activate TLS.
            var tls=Obj(("serverName",Get("sni",host)),("fingerprint",Get("fp","chrome")),("allowInsecure",false));if(Get("alpn").Length>0)tls["alpn"]=JsonSerializer.SerializeToNode(Get("alpn").Split(','));stream["tlsSettings"]=tls;
        }
        string path=Get("path","/");if(path.Length==0)path="/";string header=Get("host",host);if(header.Length==0)header=host;
        if(transport is "ws" or "httpupgrade"){
            if(!path.StartsWith('/'))throw new SafeError("CORE_PATH_INVALID");
            if(Get("ed").Length>0){if(transport!="ws"||!int.TryParse(Get("ed"),out int early)||early is <0 or >65536||path.Contains("ed=",StringComparison.Ordinal))throw new SafeError("UNSUPPORTED_FORMAT");if(early>0)path+=(path.Contains('?')?"&":"?")+"ed="+early;}
            stream[transport=="ws"?"wsSettings":"httpupgradeSettings"]=transport=="ws"?Obj(("path",path),("headers",Obj(("Host",header)))):Obj(("path",path),("host",header));
        }else if(transport=="grpc"){
            if(Get("ed").Length>0||Get("mode") is not ("" or "gun" or "multi"))throw new SafeError("UNSUPPORTED_FORMAT");
            stream["grpcSettings"]=Obj(("serviceName",Get("serviceName",Get("path"))),("authority",Get("authority")),("multiMode",Get("mode")=="multi"));
        }else if(new[]{"host","path","serviceName","authority","ed","mode"}.Any(k=>Get(k).Length>0&&Get(k)!="/"))throw new SafeError("UNSUPPORTED_FORMAT");
        return stream;
    }
    static ParsedLink Trojan(string name,string host,int port,string password,Dictionary<string,string> query)
    {
        string[] allowed=["sni","alpn","fp","type","security","host","path","serviceName","authority","mode","allowInsecure","insecure","headerType","ed","eh"];
        if(query.Keys.Any(k=>!allowed.Contains(k))||password.Length==0)throw new SafeError("UNSUPPORTED_FORMAT");
        return new(name,"Trojan",Obj(("protocol","trojan"),("settings",Obj(("servers",Arr(Obj(("address",host),("port",port),("password",password)))))),("streamSettings",ExtraStream(host,query,"tls"))));
    }
    static ParsedLink Vmess(string raw,string fallbackName)
    {
        string body=raw[8..].Split('#')[0];using var doc=JsonDocument.Parse(Importer.Decode(body));var root=doc.RootElement;
        string[] allowed=["v","ps","name","add","port","id","aid","scy","net","type","host","path","tls","sni","alpn","fp","allowInsecure","insecure","ed","eh","serviceName","authority","pcs","vcn"];
        if(root.ValueKind!=JsonValueKind.Object||root.EnumerateObject().Any(p=>!allowed.Contains(p.Name))||root.EnumerateObject().Select(p=>p.Name).Distinct().Count()!=root.EnumerateObject().Count())throw new SafeError("UNSUPPORTED_FORMAT");
        string Get(string key,string fallback=""){if(!root.TryGetProperty(key,out var value))return fallback;return value.ValueKind switch{JsonValueKind.Null=>fallback,JsonValueKind.String=>value.GetString()??"",JsonValueKind.Number=>value.GetRawText(),JsonValueKind.False=>"false",JsonValueKind.True=>"true",_=>throw new SafeError("INVALID_IMPORT")};}
        if(Get("pcs").Length>0||Get("vcn").Length>0)throw new SafeError("UNSUPPORTED_FORMAT");
        if(Get("v") is not ("" or "2")||Get("aid","0") is not ("" or "0")||Get("type") is not ("" or "none"))throw new SafeError("UNSUPPORTED_FORMAT");
        string host=Get("add"),id=Get("id"),cipher=Get("scy","auto");if(cipher.Length==0)cipher="auto";
        if(Uri.CheckHostName(host)==UriHostNameType.Unknown||!Guid.TryParseExact(id,"D",out _)||!int.TryParse(Get("port"),NumberStyles.None,CultureInfo.InvariantCulture,out int port)||port is <1 or >65535)throw new SafeError("INVALID_IMPORT");
        if(cipher is not ("auto" or "aes-128-gcm" or "chacha20-poly1305"))throw new SafeError("UNSUPPORTED_FORMAT");
        string tls=Get("tls");if(tls is not ("" or "none" or "tls"))throw new SafeError("UNSUPPORTED_FORMAT");
        var query=new Dictionary<string,string>{{"type",Get("net","tcp")},{"security",tls=="tls"?"tls":"none"}};
        foreach(string key in new[]{"host","path","sni","fp","alpn","allowInsecure","insecure","ed","eh","serviceName","authority"})if(Get(key).Length>0)query[key]=Get(key);
        var stream=ExtraStream(host,query,"none");string name=Get("ps",Get("name"));if(name.Length==0)name=fallbackName.Length>0?fallbackName:"VMess";
        var user=Obj(("id",id),("alterId",0),("security",cipher));
        var server=Obj(("address",host),("port",port),("users",Arr(user)));
        return new(name,"VMess",Obj(("protocol","vmess"),("settings",Obj(("vnext",Arr(server)))),("streamSettings",stream)));
    }
}
