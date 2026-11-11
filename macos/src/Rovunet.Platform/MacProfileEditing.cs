using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class MacProfileEditing
{
    static string[] Wire(string raw)=>Importer.Decode(raw[(raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
    static string Wrap(string original,string[] fields)=>original[..(original.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',fields)));
    public static Profile Native(string kind,string name,string value)
    {
        if(kind is not("doh" or "snowflake")||name.Contains('|')||name.Any(char.IsControl))throw new SafeError("INVALID_IMPORT");var fields=Enumerable.Repeat("",88).ToArray();fields[0]="33";fields[1]=kind;fields[2]=name;fields[31]=fields[33]=fields[36]="0";fields[34]="1";fields[87]=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"sshUser\":\"\",\"sshPass\":\"\",\"socksUser\":\"\",\"socksPass\":\"\"}"));if(kind=="doh")fields[21]=value;else fields[26]=Convert.ToBase64String(Encoding.UTF8.GetBytes(value));return MacProtocols.Single("rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',fields))));
    }
    public static string Inner(Profile p)=>ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[1]=="xray"?Importer.Decode(Importer.Fields(p.Raw)[84]):p.Raw;
    public static Profile Replace(Profile previous,string raw)
    {
        var next=MacProtocols.Single(raw);
        if(ProfileEditing.IsNative(previous.Raw)&&Importer.Fields(previous.Raw)[34]=="0"&&(!ProfileEditing.IsNative(next.Raw)||Importer.Fields(next.Raw)[34]!="0"))throw new SafeError("SHARING_DISABLED");
        next.Id=previous.Id;next.SourceId=previous.SourceId;next.ItemId=previous.ItemId;next.CollectionId=previous.CollectionId;next.CollectionName=previous.CollectionName;return next;
    }
    public static Profile MergeSubscription(string baseRaw,Profile local,Profile incoming,string? originalName)
    {
        var baseline=MacProtocols.Single(baseRaw);_=MacProtocols.Single(incoming.Raw);
        Profile KeepName(Profile p){if(Inner(local).TrimStart().StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)&&local.Name!=(originalName??baseline.Name))p.Name=local.Name;return p;}
        if(local.Raw==baseRaw||local.Raw==incoming.Raw)return KeepName(incoming);
        if(incoming.Raw==baseRaw)return KeepName(Replace(incoming,local.Raw));
        if(!ProfileEditing.IsNative(baseRaw)||!ProfileEditing.IsNative(local.Raw)||!ProfileEditing.IsNative(incoming.Raw))throw new SafeError("OVERRIDE_CONFLICT");
        var prior=Wire(baseRaw);var edited=Wire(local.Raw);var remote=Wire(incoming.Raw);
        if(prior.Length!=edited.Length||edited.Length!=remote.Length||prior[0]!=remote[0]||prior[1]!=remote[1]||prior[1]!=edited[1])throw new SafeError("OVERRIDE_CONFLICT");
        for(int i=2;i<prior.Length;i++){if(prior[i]==edited[i]||remote[i]==edited[i])continue;if(i is >=31 and <=37||i is 85 or 86||prior[i]!=remote[i])throw new SafeError("OVERRIDE_CONFLICT");remote[i]=edited[i];}
        return Replace(incoming,Wrap(incoming.Raw,remote));
    }
    public static CoreProfileForm? Read(Profile p)
    {
        var parsed=MacProtocols.Extended(p.Raw);if(parsed==null)return null;_=MacProtocols.Single(p.Raw);string raw=Inner(p);var values=new Dictionary<string,string>();
        if(ProfileEditing.IsNative(raw)&&Importer.Fields(raw)[1] is "doh" or "snowflake"){
            var f=Importer.Fields(raw);return new(){Kind=f[1],Fields=f[1]=="doh"?new Dictionary<string,string>{{"doh",f[21]}}:new Dictionary<string,string>{{"bridges",f[26].Length>0?Importer.Decode(f[26]):""}}};
        }
        if(parsed.Protocol=="AmneziaWG"){
            if(raw.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase)){var uri=new Uri(raw);raw=MacAmnezia.Document(uri,MacProtocols.Query(uri));}
            foreach(string key in new[]{"PrivateKey","Address","DNS","MTU","PublicKey","PresharedKey","Endpoint","AllowedIPs","PersistentKeepalive"}.Concat(MacAmnezia.Labels.Values))values[key]="";
            foreach(string line in raw.Split('\n')){string text=line.Split('#')[0].Trim();int equals=text.IndexOf('=');if(equals>0){string key=text[..equals].Trim();string? known=values.Keys.FirstOrDefault(k=>k.Equals(key,StringComparison.OrdinalIgnoreCase));if(known!=null)values[known]=text[(equals+1)..].Trim();}}
            return new(){Kind="amneziawg",Fields=values};
        }
        if(parsed.Protocol=="Shadowsocks 2022"){
            var server=parsed.Outbound["settings"]!["servers"]![0]!;return new(){Kind="ss2022",Fields=new Dictionary<string,string>{{"server",server["address"]!.ToString()},{"port",server["port"]!.ToString()},{"method",server["method"]!.ToString()},{"auth",server["password"]!.ToString()}}};
        }
        if(raw.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase)&&!raw.Split('#')[0].Contains('@')){
            var json=JsonNode.Parse(Importer.Decode(raw[8..].Split('#')[0]))!.AsObject();foreach(var pair in json)if(pair.Key!="ps")values[pair.Key]=pair.Value?.ToString()??"";return new(){Kind="vmess-json",Fields=values};
        }
        if(MacSupplemental.Handles(raw))return MacSupplemental.Form(raw);
        var link=new Uri(raw);values["server"]=link.Host.Trim('[',']');values["port"]=(link.Port>0?link.Port:443).ToString(System.Globalization.CultureInfo.InvariantCulture);values["auth"]=Uri.UnescapeDataString(link.UserInfo);foreach(var pair in MacProtocols.Query(link))values[pair.Key]=pair.Value;return new(){Kind=link.Scheme,Fields=values};
    }
    public static Profile Apply(Profile original,string name,IReadOnlyDictionary<string,string> values)
    {
        var form=Read(original)??throw new SafeError("UNSUPPORTED_FORMAT");name=name.Trim();if(name.Length is <1 or >200||name.Contains('|')||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        if(values.Count!=form.Fields.Count||values.Keys.Any(k=>!form.Fields.ContainsKey(k))||values.Any(p=>p.Value.Length>65536||p.Value.Any(c=>char.IsControl(c)&&!(p.Key is "fm" or "bridges"&&c is '\n' or '\r' or '\t'))))throw new SafeError("CORE_FIELD_INVALID");
        if(name==original.Name&&form.Fields.All(p=>values[p.Key]==p.Value))return Replace(original,original.Raw);
        string inner=Inner(original),raw;
        if(form.Kind is "doh" or "snowflake"){
            var f=Wire(original.Raw);f[2]=name;if(form.Kind=="doh")f[21]=values["doh"];else f[26]=Convert.ToBase64String(Encoding.UTF8.GetBytes(values["bridges"]));return Replace(original,Wrap(original.Raw,f));
        }
        if(MacSupplemental.Handles(inner))raw=MacSupplemental.Build(form.Kind,name,values);
        else if(form.Kind=="amneziawg"){
            if(inner.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase)){var uri=new Uri(inner);inner=MacAmnezia.Document(uri,MacProtocols.Query(uri));}
            var changed=values.Where(p=>p.Value!=form.Fields[p.Key]).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);var lines=inner.Split('\n').ToList();
            for(int i=0;i<lines.Count;i++){string key=lines[i].Split('=')[0].Trim();if(changed.Remove(key,out var value))lines[i]=value.Length>0?key+" = "+value:"";}
            int peer=lines.FindIndex(l=>l.Trim().Equals("[Peer]",StringComparison.OrdinalIgnoreCase));foreach(var pair in changed.Where(p=>p.Value.Length>0)){bool isPeer=new[]{"PublicKey","PresharedKey","Endpoint","AllowedIPs","PersistentKeepalive"}.Contains(pair.Key,StringComparer.OrdinalIgnoreCase);if(isPeer)lines.Add(pair.Key+" = "+pair.Value);else lines.Insert(peer++,pair.Key+" = "+pair.Value);}raw=string.Join('\n',lines);
        }else if(form.Kind=="vmess-json"){
            var json=JsonNode.Parse(Importer.Decode(inner[8..].Split('#')[0]))!.AsObject();json["ps"]=name;foreach(var pair in values.Where(p=>p.Value!=form.Fields[p.Key]))json[pair.Key]=pair.Value;raw="vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(json.ToJsonString()));
        }else{
            string host=values["server"].Trim().Trim('[',']');if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("CORE_HOST_INVALID");string endpoint=(host.Contains(':')?"["+host+"]":host)+":"+Importer.Port(values["port"]);if(values["auth"].Length==0)throw new SafeError("CORE_AUTH_REQUIRED");
            raw=form.Kind=="ss2022"?"ss://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(values["method"]+":"+values["auth"]))+"@"+endpoint+"#"+Uri.EscapeDataString(name):new Uri(inner).Scheme+"://"+Uri.EscapeDataString(values["auth"])+"@"+endpoint+"?"+string.Join('&',values.Where(p=>p.Key is not("server" or "port" or "auth")&&p.Value.Length>0).Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)))+"#"+Uri.EscapeDataString(name);
        }
        if(ProfileEditing.IsNative(original.Raw)){var f=Wire(original.Raw);f[2]=name;f[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));raw=Wrap(original.Raw,f);}var result=Replace(original,raw);result.Name=name;return result;
    }
    public static async Task<long?> Tcp(Profile p,CancellationToken stop)
    {
        var extension=MacProtocols.Extended(p.Raw);if(extension==null)return await CollectionActions.Tcp(p,stop);var outbound=extension.Outbound;var settings=outbound["settings"];string host;int port;
        if(extension.Protocol is "AmneziaWG" or "Tor" or "DoH · DNS only"||outbound["streamSettings"]?["network"]?.ToString() is "hysteria" or "hysteria2" or "kcp")return null;
        if(settings?["servers"]?[0] is {} ss){host=ss["address"]!.ToString();port=ss["port"]!.GetValue<int>();}else if(settings?["vnext"]?[0] is {} vm){host=vm["address"]!.ToString();port=vm["port"]!.GetValue<int>();}else if(extension.Protocol=="AnyTLS"||extension.Protocol=="Mieru"&&settings?["transport"]?.ToString()=="TCP"){host=settings!["server"]!.ToString();port=settings["port"]!.GetValue<int>();}else return null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromSeconds(3));using var client=new TcpClient();var watch=Stopwatch.StartNew();await client.ConnectAsync(host,port,timeout.Token);return watch.ElapsedMilliseconds;
    }
}
