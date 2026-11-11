using System.Text;
namespace Rovunet.Core;

public sealed class CoreProfileForm
{
    public required string Kind {get;init;}
    public required IReadOnlyDictionary<string,string> Fields {get;init;}
    public bool NativeWebSocket {get;init;}
    public string this[string key]=>Fields.GetValueOrDefault(key,"");
    public override string ToString()=>"CoreProfileForm(redacted)";
}
public sealed class CoreEditResult
{
    public required Profile Profile {get;init;}
    public bool CreatesCopy {get;init;}
    public override string ToString()=>"CoreEditResult(redacted)";
}
public static class CoreProfileEditing
{
    static readonly string[] LinkKeys=["server","port","auth","type","security","encryption","flow","sni","fp","pbk","sid","spx","host","path","mode","extra","alpn","fragment","insecure","obfs","obfs-password","serviceName","authority","heartbeatPeriod","mtu","tti","cs","fm","Telegram"];
    static readonly string[] InterfaceKeys=["privatekey","address","dns","mtu"];
    static readonly string[] PeerKeys=["publickey","presharedkey","endpoint","allowedips","persistentkeepalive"];
    static string[] Wire(Profile p)=>Importer.Decode(p.Raw[(p.Raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
    static string Inner(Profile p){string raw=ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[1]=="xray"?Importer.Decode(Importer.Fields(p.Raw)[84]):p.Raw;return raw.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase)?CoreLinks.WireGuardLinkConfig(raw):raw;}
    public static bool CanEdit(Profile p){try{return Read(p)!=null;}catch(SafeError){return false;}}
    public static CoreProfileForm? Read(Profile p)
    {
        var admitted=Importer.Single(p.Raw);if(!admitted.CanConnect)throw new SafeError(admitted.BlockReason);string raw=Inner(p);var values=new Dictionary<string,string>(StringComparer.Ordinal);
        if(ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[1]=="vless"){
            var f=Importer.Fields(p.Raw);foreach(string key in LinkKeys)values[key]="";
            values["server"]=f[66].Length>0?f[66]:f[3];values["port"]=f[67];values["auth"]=f[62];values["type"]="ws";values["security"]=f[63];values["encryption"]="none";values["host"]=f[3];values["path"]=f[65];values["sni"]=f[78].Length>0?f[78]:f[3];values["mode"]="auto";
            return new(){Kind="vless",Fields=values,NativeWebSocket=true};
        }
        if(raw.TrimStart().StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)){
            foreach(string key in InterfaceKeys.Concat(PeerKeys))values[key]="";
            foreach(string line in raw.Split('\n')){string text=line.Split('#')[0].Trim();int equal=text.IndexOf('=');if(equal>=0)values[text[..equal].Trim().ToLowerInvariant()]=text[(equal+1)..].Trim();}
            return new(){Kind="wireguard",Fields=values};
        }
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||uri.Scheme is not ("vless" or "hysteria2" or "hy2"))return null;
        foreach(string key in LinkKeys)values[key]="";
        foreach(string token in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){var pair=token.Split('=',2);values[Uri.UnescapeDataString(pair[0])]=pair.Length>1?Uri.UnescapeDataString(pair[1]):"";}
        values["server"]=uri.Host.Trim('[',']');values["port"]=(uri.Port>0?uri.Port:443).ToString(System.Globalization.CultureInfo.InvariantCulture);values["auth"]=Uri.UnescapeDataString(uri.UserInfo);
        if(uri.Scheme=="vless"){values["type"]=values["type"] switch{"xhttp"=>"xhttp","ws" or "websocket"=>"ws","grpc"=>"grpc","httpupgrade"=>"httpupgrade","kcp" or "mkcp"=>"kcp",_=>"tcp"};if(values["security"].Length==0)values["security"]="none";if(values["path"].Length==0)values["path"]="/";if(values["mode"].Length==0)values["mode"]="auto";}
        return new(){Kind=uri.Scheme=="vless"?"vless":"hysteria2",Fields=values};
    }
    public static bool CreatesCopy(Profile original,IReadOnlyDictionary<string,string> fields)
    {
        var form=Read(original)??throw new SafeError("UNSUPPORTED_FORMAT");
        return form.Kind=="vless"&&form["type"]=="ws"&&(fields.GetValueOrDefault("type","ws")!="ws"||form.NativeWebSocket&&(fields.GetValueOrDefault("encryption","") is not("" or "none")||fields.GetValueOrDefault("heartbeatPeriod","").Length>0||fields.GetValueOrDefault("security")!="none"&&(fields.GetValueOrDefault("fp","").Length>0||fields.GetValueOrDefault("alpn","").Length>0)));
    }
    public static CoreEditResult Apply(Profile original,string name,IReadOnlyDictionary<string,string> edits)
    {
        var form=Read(original)??throw new SafeError("UNSUPPORTED_FORMAT");var fields=form.Fields.ToDictionary(StringComparer.Ordinal);
        name=name.Trim();if(name.Length==0||name.Length>200||name.Contains('|')||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        foreach(var (key,value) in edits){if(!fields.ContainsKey(key)||value.Any(c=>char.IsControl(c)&&!(key=="extra"&&c is '\r' or '\n' or '\t')))throw new SafeError("CORE_FIELD_INVALID");fields[key]=value;}
        if(form.Kind=="vless"){if(fields["type"]!="kcp"){fields["mtu"]="";fields["tti"]="";}if(fields["type"]!="ws")fields["heartbeatPeriod"]="";}
        var changed=fields.Where(x=>x.Value!=form[x.Key]).ToDictionary(StringComparer.Ordinal);
        if(changed.Count==0&&name==original.Name){var same=ProfileEditing.Replace(original,original.Raw);same.Name=original.Name;return new(){Profile=same};}
        bool copy=CreatesCopy(original,fields);string inner=Inner(original),raw;
        if(form.Kind=="wireguard")raw=changed.Count==0?inner:PatchWireGuard(inner,changed);
        else{
            string host=fields["server"].Trim().Trim('[',']');if(Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("CORE_HOST_INVALID");
            if(!int.TryParse(fields["port"],out int port)||port is <1 or >65535)throw new SafeError("CORE_PORT_INVALID");
            if(changed.ContainsKey("server")){fields["server"]=host;changed["server"]=host;}if(changed.ContainsKey("port")){fields["port"]=port.ToString(System.Globalization.CultureInfo.InvariantCulture);changed["port"]=fields["port"];}
            if(string.IsNullOrWhiteSpace(fields["auth"]))throw new SafeError("CORE_AUTH_REQUIRED");
            if(form.Kind=="vless"&&!Guid.TryParseExact(fields["auth"],"D",out _))throw new SafeError("CORE_UUID_INVALID");
            if(fields["sni"].Length>0&&Uri.CheckHostName(fields["sni"])==UriHostNameType.Unknown)throw new SafeError("CORE_SNI_INVALID");
            if(form.Kind=="vless"&&fields["type"] is "ws" or "xhttp"&&fields["path"].Length>0&&!fields["path"].StartsWith('/'))throw new SafeError("CORE_PATH_INVALID");
            var changes=new Dictionary<string,string>(changed,StringComparer.Ordinal);
            void Remove(params string[] keys){foreach(string key in keys)changes[key]="";}
            if(changed.ContainsKey("type")||copy){if(fields["type"] is "ws" or "xhttp" or "grpc" or "httpupgrade")Remove("flow");if(fields["type"]!="grpc")Remove("serviceName","authority");if(fields["type"]!="xhttp")Remove("mode","extra");if(fields["type"]=="tcp")Remove("host","path");}
            if(changed.ContainsKey("security")){if(fields["security"]!="reality")Remove("pbk","sid","spx");if(fields["security"]=="none"){Remove("sni","fp","alpn");if(fields["encryption"] is "" or "none")Remove("flow");}if(fields["security"]=="reality")Remove("alpn");}
            if(changed.ContainsKey("obfs")&&fields["obfs"].Length==0)Remove("obfs-password");
            if(form.NativeWebSocket&&!copy){
                if(fields["type"]!="ws"||fields["security"] is not ("tls" or "none"))throw new SafeError("CUSTOM_TRANSPORT_NOT_READY");
                var f=Wire(original);var map=new Dictionary<string,int>{{"server",66},{"port",67},{"auth",62},{"security",63},{"host",3},{"path",65},{"sni",78}};
                foreach(var (key,index) in map)if(changed.ContainsKey(key))f[index]=fields[key];f[2]=name;raw=EncodeNative(original,f);
                return new(){Profile=ProfileEditing.Replace(original,raw)};
            }
            string source=inner;
            if(form.NativeWebSocket){source="vless://"+Uri.EscapeDataString(fields["auth"])+"@"+(host.Contains(':')?"["+host+"]":host)+":"+port;foreach(var pair in fields)changes[pair.Key]=pair.Value;foreach(var key in new[]{"flow","mode","extra","host","path"}){if(key=="flow"&&fields["type"]!="tcp"||key is "mode" or "extra"&&fields["type"]!="xhttp"||key is "host" or "path"&&fields["type"]=="tcp")changes[key]="";}}
            if(form.NativeWebSocket){if(fields["security"]!="reality")Remove("pbk","sid","spx");if(fields["security"]=="none")Remove("sni","fp","alpn","flow");if(fields["security"]=="reality")Remove("alpn");}
            raw=PatchLink(source,name,original.Name,changes,host,port,fields["auth"]);
        }
        if(ProfileEditing.IsNative(original.Raw)){
            var f=Wire(original);if(f.Length<85){f=f.Concat(Enumerable.Repeat("",85-f.Length)).ToArray();f[0]="31";}f[1]="xray";f[2]=name;f[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));raw=EncodeNative(original,f);
        }
        var next=ProfileEditing.Replace(original,raw);next.Name=name;if(!next.CanConnect)throw new SafeError(next.BlockReason);
        if(copy){next.Id=Guid.NewGuid().ToString("N");next.CollectionId=original.SourceId.Length>0?"sub:"+original.SourceId:original.CollectionId;next.SourceId="";next.ItemId="";}
        return new(){Profile=next,CreatesCopy=copy};
    }
    static string EncodeNative(Profile original,string[] fields)=>original.Raw[..(original.Raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',fields)));
    static string PatchLink(string source,string name,string originalName,Dictionary<string,string> changes,string host,int port,string auth)
    {
        var uri=new Uri(source);int hash=source.IndexOf('#'),query=source.IndexOf('?');int end=new[]{hash,query,source.Length}.Where(i=>i>=0).Min();string head=source[..end];
        if(changes.Keys.Any(k=>k is "server" or "port" or "auth"))head=uri.Scheme+"://"+(changes.ContainsKey("auth")?Uri.EscapeDataString(auth):uri.UserInfo)+"@"+(host.Contains(':')?"["+host+"]":host)+":"+port+(uri.AbsolutePath=="/"?"/":"");
        var tokens=new List<string>();var consumed=new HashSet<string>();
        foreach(string token in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){string key=Uri.UnescapeDataString(token.Split('=',2)[0]);consumed.Add(key);if(changes.TryGetValue(key,out string? value)){if(value.Length>0)tokens.Add(Uri.EscapeDataString(key)+"="+Uri.EscapeDataString(value));}else tokens.Add(token);}
        foreach(var (key,value) in changes)if(key is not("server" or "port" or "auth")&&!consumed.Contains(key)&&value.Length>0)tokens.Add(Uri.EscapeDataString(key)+"="+Uri.EscapeDataString(value));
        string fragment=name!=originalName||hash<0?"#"+Uri.EscapeDataString(name):source[hash..];
        return head+(tokens.Count>0||query>=0?"?"+string.Join('&',tokens):"")+fragment;
    }
    static string PatchWireGuard(string raw,Dictionary<string,string> changes)
    {
        if(changes.Values.Any(v=>v.Contains('\r')||v.Contains('\n')))throw new SafeError("CORE_FIELD_INVALID");
        string ending=raw.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n";var lines=raw.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n').ToList();var written=new HashSet<string>();
        for(int i=0;i<lines.Count;i++){string text=lines[i];int equal=text.IndexOf('=');if(equal<0||text.TrimStart().StartsWith('#'))continue;string key=text[..equal].Trim().ToLowerInvariant();if(!changes.TryGetValue(key,out var value))continue;written.Add(key);int comment=text.IndexOf('#',equal);string suffix=comment>=0?" "+text[comment..]:"";lines[i]=value.Trim().Length==0?suffix.Trim():text[..(equal+1)]+" "+value.Trim()+suffix;}
        foreach(var (key,value) in changes.Where(x=>!written.Contains(x.Key)&&x.Value.Trim().Length>0)){
            bool iface=InterfaceKeys.Contains(key);int peer=lines.FindIndex(s=>s.Split('#')[0].Trim().Equals("[Peer]",StringComparison.OrdinalIgnoreCase));string label=key switch{"privatekey"=>"PrivateKey","publickey"=>"PublicKey","presharedkey"=>"PresharedKey","allowedips"=>"AllowedIPs","persistentkeepalive"=>"PersistentKeepalive","mtu"=>"MTU","dns"=>"DNS",_=>char.ToUpperInvariant(key[0])+key[1..]};lines.Insert(iface?peer:lines.Count,label+" = "+value.Trim());
        }
        return string.Join(ending,lines);
    }
}
