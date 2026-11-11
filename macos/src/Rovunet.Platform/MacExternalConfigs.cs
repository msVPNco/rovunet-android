// Adapted from pinned Windows source 562a3d71670ceb7de909146bf68116d5cc614496.
using Rovunet.Core;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Platform;

// Convert explicit outbound settings only. Global routing/DNS/inbounds are never silently installed.
public static class MacExternalConfigs
{
    public static bool Recognizes(string text)
    {
        try{
        using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=24});var root=doc.RootElement;
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("version",out _))return false;
        return root.ValueKind==JsonValueKind.Array||root.ValueKind==JsonValueKind.Object&&(root.TryGetProperty("outbounds",out _)||root.TryGetProperty("protocol",out _)||root.TryGetProperty("type",out _));
        }catch(JsonException){return false;}
    }
    static void Only(JsonObject value,params string[] allowed)
    {
        if(value.Any(p=>!allowed.Contains(p.Key)))throw new SafeError("JSON_UNSUPPORTED_OPTION");
    }
    static string Str(JsonObject obj,string key,string fallback="")
    {
        if(obj[key]==null)return fallback;
        if(obj[key] is JsonValue value){
            if(value.TryGetValue<string>(out var text))return text;
            if(key is "port" or "server_port" or "alterId" or "alter_id" or "level"&&value.TryGetValue<int>(out int number))return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(key is "enabled" or "insecure" or "allowInsecure" or "multiMode"&&value.TryGetValue<bool>(out bool flag))return flag?"true":"false";
        }
        throw new SafeError("JSON_UNSUPPORTED_OPTION");
    }
    static JsonObject Object(JsonObject obj,string key)=>obj[key] as JsonObject??throw new SafeError("JSON_UNSUPPORTED_OPTION");
    static JsonObject One(JsonObject obj,string key)
    {
        if(obj[key] is not JsonArray{Count:1} list||list[0] is not JsonObject item)throw new SafeError("JSON_UNSUPPORTED_OPTION");return item;
    }
    public static List<Profile> Read(string text,CompatibleImport? admission,bool experimental)
    {
        var root=JsonNode.Parse(text,documentOptions:new JsonDocumentOptions{MaxDepth=24})??throw new SafeError("INVALID_IMPORT");
        if(root is JsonObject document&&document.ContainsKey("outbounds"))Only(document,"outbounds","log","dns","inbounds","routing","route","policy","api","stats","transport","observatory","burstObservatory","metrics","reverse","fakedns","experimental","ntp","endpoints","services");
        IEnumerable<JsonNode?> nodes=root is JsonArray array?array:root is JsonObject obj&&obj["outbounds"] is JsonArray outbounds?outbounds:[root];
        var all=nodes.ToArray();if(all.Length is <1 or >1000)throw new SafeError("INVALID_IMPORT");var profiles=new List<Profile>();
        admission?.Notices.Add("JSON_OUTBOUND_ONLY");
        foreach(var node in all){
            string protocol=node is JsonObject item?Str(item,"protocol",Str(item,"type","JSON")):"JSON";
            try{
                if(node is not JsonObject outbound)throw new SafeError("INVALID_IMPORT");
                string link=Convert(outbound);var profile=MacProtocols.Single(link);string reason=SupportPolicy.Reason(profile,experimental);if(reason.Length>0)throw new SafeError(reason);
                profile.ItemId=Importer.Fingerprint(profile.Raw);profiles.Add(profile);if(admission!=null){admission.Protocols[profile.Protocol]=admission.Protocols.GetValueOrDefault(profile.Protocol)+1;}
            }catch(SafeError error) when(admission!=null){admission.Reject(error.Code);string key=protocol+" · "+error.Code;admission.Protocols[key]=admission.Protocols.GetValueOrDefault(key)+1;}
        }
        return profiles;
    }
    static string Convert(JsonObject node)
    {
        bool xray=node.ContainsKey("protocol");string kind=Str(node,xray?"protocol":"type"),name=Str(node,"tag",kind);string host,port,auth="",cipher="auto",flow="";var q=new Dictionary<string,string>();
        if(xray){
            Only(node,"tag","protocol","settings","streamSettings");var settings=Object(node,"settings");Only(settings,kind is "vless" or "vmess"?"vnext":"servers");var server=One(settings,kind is "vless" or "vmess"?"vnext":"servers");
            if(kind is "vless" or "vmess"){
                Only(server,"address","port","users");var user=One(server,"users");Only(user,kind=="vless"?["id","encryption","flow","email","level"]:["id","alterId","security","email","level"]);
                if(Str(user,"level","0")!="0"||kind=="vmess"&&Str(user,"alterId","0")!="0")throw new SafeError("JSON_UNSUPPORTED_OPTION");auth=Str(user,"id");cipher=Str(user,"security","auto");flow=Str(user,"flow");if(kind=="vless")q["encryption"]=CoreLinks.NormalizeVlessEncryption(Str(user,"encryption","none"));
            }else{Only(server,kind=="shadowsocks"?["address","port","method","password"]:["address","port","password"]);auth=Str(server,"password");cipher=Str(server,"method");}
            host=Str(server,"address");port=Str(server,"port");
            if(node["streamSettings"] is JsonObject stream){
                Only(stream,"network","security","tlsSettings","wsSettings","grpcSettings","httpupgradeSettings","xhttpSettings","finalmask");string transport=Str(stream,"network","tcp");q["type"]=transport=="raw"?"tcp":transport;q["security"]=Str(stream,"security","none");
                if(stream["tlsSettings"] is JsonObject tls){Only(tls,"serverName","allowInsecure","fingerprint","alpn","cipherSuites");if(tls["cipherSuites"]!=null)q["cs"]=MacCoreOptions.CipherSuites(Str(tls,"cipherSuites"));if(Str(tls,"allowInsecure","false")!="false")throw new SafeError("TLS_UNVERIFIED");q["sni"]=Str(tls,"serverName");q["fp"]=Str(tls,"fingerprint");if(tls["alpn"] is JsonArray alpn)q["alpn"]=string.Join(',',alpn.Select(v=>v!.GetValue<string>()));}
                if(stream["finalmask"] is {} mask){q["fm"]=mask.ToJsonString();_=MacCoreOptions.FinalMask(q["fm"]);}
                if(stream["wsSettings"] is JsonObject ws){Only(ws,"path","headers");q["path"]=Str(ws,"path","/");if(ws["headers"] is JsonObject headers){Only(headers,"Host");q["host"]=Str(headers,"Host");}}
                if(stream["grpcSettings"] is JsonObject grpc){Only(grpc,"serviceName","authority","multiMode");q["serviceName"]=Str(grpc,"serviceName");q["authority"]=Str(grpc,"authority");q["mode"]=Str(grpc,"multiMode","false")=="true"?"multi":"gun";}
                if(stream["httpupgradeSettings"] is JsonObject upgrade){Only(upgrade,"host","path");q["host"]=Str(upgrade,"host");q["path"]=Str(upgrade,"path","/");}
                if(stream["xhttpSettings"] is JsonObject xhttp){Only(xhttp,"host","path","mode","extra");q["host"]=Str(xhttp,"host");q["path"]=Str(xhttp,"path","/");q["mode"]=Str(xhttp,"mode","auto");if(xhttp["extra"]!=null)q["extra"]=xhttp["extra"]!.ToJsonString();}
            }else{q["type"]="tcp";q["security"]="none";}
        }else{
            string[] common=["type","tag","server","server_port","tls","transport"];
            Only(node,common.Concat(kind switch{"vless"=>["uuid","flow"],"vmess"=>["uuid","security","alter_id"],"trojan"=>["password"],"shadowsocks"=>new[]{"password","method"},_=>Array.Empty<string>()}).ToArray());host=Str(node,"server");port=Str(node,"server_port");auth=Str(node,kind is "vless" or "vmess"?"uuid":"password");cipher=Str(node,kind=="shadowsocks"?"method":"security","auto");flow=Str(node,"flow");if(Str(node,"alter_id","0")!="0")throw new SafeError("JSON_UNSUPPORTED_OPTION");
            q["type"]="tcp";q["security"]="none";
            if(node["tls"] is JsonObject tls){Only(tls,"enabled","server_name","insecure","alpn","utls");if(Str(tls,"insecure","false")!="false")throw new SafeError("TLS_UNVERIFIED");q["security"]=Str(tls,"enabled","false")=="true"?"tls":"none";q["sni"]=Str(tls,"server_name");if(tls["alpn"] is JsonArray alpn)q["alpn"]=string.Join(',',alpn.Select(v=>v!.GetValue<string>()));if(tls["utls"] is JsonObject utls){Only(utls,"enabled","fingerprint");if(Str(utls,"enabled","false")=="true")q["fp"]=Str(utls,"fingerprint");}}
            if(node["transport"] is JsonObject transport){Only(transport,"type","path","headers","service_name","host");q["type"]=Str(transport,"type");q["path"]=Str(transport,"path");if(transport["headers"] is JsonObject headers){Only(headers,"Host");q["host"]=Str(headers,"Host");}else q["host"]=Str(transport,"host");if(q["type"]=="grpc"){q.Remove("path");q.Remove("host");q["serviceName"]=Str(transport,"service_name");}}
        }
        if(kind is not ("vless" or "vmess" or "trojan" or "shadowsocks"))throw new SafeError("UNSUPPORTED_FORMAT");
        if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("CORE_HOST_INVALID");_ = Importer.Port(port);string endpoint=(host.Contains(':')?"["+host+"]":host)+":"+port;string Esc(string s)=>Uri.EscapeDataString(s);
        if(kind=="shadowsocks"){
            if(q.GetValueOrDefault("type","tcp")!="tcp"||q.GetValueOrDefault("security","none")!="none"||q.Any(p=>p.Key is not ("type" or "security")&&p.Value.Length>0))throw new SafeError("JSON_UNSUPPORTED_OPTION");
            return "ss://"+System.Convert.ToBase64String(Encoding.UTF8.GetBytes(cipher+":"+auth))+"@"+endpoint+"#"+Esc(name);
        }
        if(kind=="vmess"){
            var vmess=new JsonObject{["v"]="2",["ps"]=name,["add"]=host,["port"]=int.Parse(port),["id"]=auth,["aid"]=0,["scy"]=cipher,["net"]=q.GetValueOrDefault("type","tcp"),["tls"]=q.GetValueOrDefault("security")=="tls"?"tls":""};foreach(var (key,value) in q.Where(p=>p.Key is not ("type" or "security"))){if(key=="mode"&&q.GetValueOrDefault("type")=="grpc")vmess["type"]=value;if(key!="mode"&&value.Length>0)vmess[key]=value;}
            return "vmess://"+System.Convert.ToBase64String(Encoding.UTF8.GetBytes(vmess.ToJsonString()));
        }
        if(kind=="vless"&&flow.Length>0)q["flow"]=flow;
        return kind+"://"+Esc(auth)+"@"+endpoint+"?"+string.Join('&',q.Where(p=>p.Value.Length>0).Select(p=>Esc(p.Key)+"="+Esc(p.Value)))+"#"+Esc(name);
    }
}
