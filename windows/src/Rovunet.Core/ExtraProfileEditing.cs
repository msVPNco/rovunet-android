using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;

public static class ExtraProfileEditing
{
    static string Inner(Profile p)=>ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[1]=="xray"?Importer.Decode(Importer.Fields(p.Raw)[84]):p.Raw;
    public static CoreProfileForm? Read(Profile p)
    {
        string raw=Inner(p);if(SupplementalLinks.Handles(raw))return SupplementalLinks.Form(raw);string kind=raw.Split("://")[0].ToLowerInvariant();if(kind is not ("trojan" or "vmess" or "ss"))return null;
        var link=CoreLinks.Parse(raw);var outbound=link.Outbound;string family=kind=="vmess"?"vnext":"servers";var server=outbound["settings"]![family]![0]!;
        var f=new Dictionary<string,string>{{"server",server["address"]!.GetValue<string>()},{"port",server["port"]!.ToString()},{"auth",""},{"type","tcp"},{"security","none"},{"sni",""},{"fp",""},{"alpn",""},{"host",""},{"path","/"},{"serviceName",""},{"authority",""},{"method","aes-256-gcm"},{"cipher","auto"}};
        if(kind=="vmess"){f["auth"]=server["users"]![0]!["id"]!.GetValue<string>();f["cipher"]=server["users"]![0]!["security"]!.GetValue<string>();}else f["auth"]=server["password"]!.GetValue<string>();
        if(kind=="ss")f["method"]=server["method"]!.GetValue<string>();
        if(outbound["streamSettings"] is JsonObject stream){
            f["type"]=stream["network"]!.GetValue<string>();f["security"]=stream["security"]!.GetValue<string>();
            if(stream["tlsSettings"] is JsonObject tls){f["sni"]=tls["serverName"]!.ToString();f["fp"]=tls["fingerprint"]?.ToString()??"";f["alpn"]=tls["alpn"] is JsonArray alpn?string.Join(',',alpn.Select(v=>v!.ToString())):"";}
            if(stream["wsSettings"] is JsonObject ws){f["path"]=ws["path"]!.ToString();f["host"]=ws["headers"]!["Host"]!.ToString();}
            if(stream["httpupgradeSettings"] is JsonObject upgrade){f["path"]=upgrade["path"]!.ToString();f["host"]=upgrade["host"]!.ToString();}
            if(stream["grpcSettings"] is JsonObject grpc){f["serviceName"]=grpc["serviceName"]!.ToString();f["authority"]=grpc["authority"]?.ToString()??"";}
        }
        return new(){Kind=kind,Fields=f};
    }
    public static Profile Apply(Profile original,string name,IReadOnlyDictionary<string,string> values)
    {
        if(SupplementalLinks.Handles(Inner(original)))return SupplementalLinks.Apply(original,name,values);
        var form=Read(original)??throw new SafeError("UNSUPPORTED_FORMAT");name=name.Trim();if(name.Length==0||name.Length>200||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        if(values.Count!=form.Fields.Count||values.Keys.Any(k=>!form.Fields.ContainsKey(k))||values.Values.Any(v=>v.Any(char.IsControl)))throw new SafeError("CORE_FIELD_INVALID");
        if(name==original.Name&&form.Fields.All(p=>values[p.Key]==p.Value))return ProfileEditing.Replace(original,original.Raw);
        string host=values["server"].Trim();if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("CORE_HOST_INVALID");int port=Importer.Port(values["port"]);if(values["auth"].Length==0)throw new SafeError("CORE_AUTH_REQUIRED");
        string endpoint=(host.Contains(':')?"["+host+"]":host)+":"+port;string Esc(string v)=>Uri.EscapeDataString(v);string raw;
        if(form.Kind=="ss")raw="ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(values["method"]+":"+values["auth"]))+"@"+endpoint+"#"+Esc(name);
        else{
            string type=values["type"],security=values["security"];var q=new Dictionary<string,string>{{"type",type},{"security",security}};
            if(security=="tls")foreach(string key in new[]{"sni","fp","alpn"})if(values[key].Length>0)q[key]=values[key];
            if(type is "ws" or "httpupgrade"){q["host"]=values["host"];q["path"]=values["path"];}
            if(type=="grpc"){q["serviceName"]=values["serviceName"];q["authority"]=values["authority"];}
            string inner=Inner(original);
            if(form.Kind=="trojan"){
                // Retain supported options that are outside this form, such as gRPC multi mode.
                var uri=new Uri(inner);foreach(var token in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){var kv=token.Split('=',2);string key=Uri.UnescapeDataString(kv[0]);if(key is "mode"&&type=="grpc")q[key]=kv.Length>1?Uri.UnescapeDataString(kv[1]):"";}
                raw="trojan://"+Esc(values["auth"])+"@"+endpoint+"?"+string.Join('&',q.Select(p=>Esc(p.Key)+"="+Esc(p.Value)))+"#"+Esc(name);
            }else{
                if(!Guid.TryParseExact(values["auth"],"D",out _))throw new SafeError("CORE_UUID_INVALID");
                var obj=JsonNode.Parse(Importer.Decode(inner[8..].Split('#')[0]))!.AsObject();
                foreach(string key in new[]{"host","path","sni","fp","alpn","ed","eh","serviceName","authority"})obj.Remove(key);
                obj["v"]="2";obj["ps"]=name;obj["add"]=host;obj["port"]=port;obj["id"]=values["auth"];obj["aid"]=0;obj["scy"]=values["cipher"];obj["net"]=type;obj["tls"]=security=="tls"?"tls":"";
                foreach(var pair in q.Where(p=>p.Key is not ("type" or "security")))obj[pair.Key]=pair.Value;
                raw="vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(obj.ToJsonString()));
            }
        }
        _=Importer.Single(raw);
        if(ProfileEditing.IsNative(original.Raw)){var fields=Importer.Decode(original.Raw[(original.Raw.IndexOf("://")+3)..]).Split('|');fields[2]=name;fields[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));raw=original.Raw[..(original.Raw.IndexOf("://")+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',fields)));}
        var result=ProfileEditing.Replace(original,raw);result.Name=name;return result;
    }
}
