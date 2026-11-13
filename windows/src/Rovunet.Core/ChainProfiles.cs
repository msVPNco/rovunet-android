using System.Text;
using System.Text.Json;

namespace Rovunet.Core;

public sealed class ChainLayer
{
    public string Id {get;set;}="";
    public string Raw {get;set;}="";
    public override string ToString()=>"ChainLayer(redacted)";
}
public sealed class ChainIdentity
{
    public string HostKey {get;set;}="";
    public string SlipCertificate {get;set;}="";
    public string SshTlsCa {get;set;}="";
    public int? SlipInterval {get;set;}
    public override string ToString()=>"ChainIdentity(redacted)";
}
public static class ChainProfiles
{
    public const string Prefix="rovunet-chain://1.";
    public static bool IsChain(string raw)=>raw.StartsWith("rovunet-chain://",StringComparison.OrdinalIgnoreCase);
    public static string Kind(Profile p)=>ProfileEditing.IsNative(p.Raw)?Importer.Fields(p.Raw)[1]:p.Protocol=="SOCKS5"?"socks5":"xray";
    // Android ChainValidation: 2–4 layers; only a directly reachable Xray
    // transport followed by SSH; Slipstream is outermost only; singleton
    // bridge groups cannot repeat (SSH and SOCKS5 can repeat).
    public static void Validate(IReadOnlyList<Profile> layers)
    {
        if(layers.Count is <2 or >4)throw new SafeError("CHAIN_LAYER_COUNT");
        if(layers.Any(p=>IsChain(p.Raw)||!p.CanConnect))throw new SafeError("CHAIN_UNSUPPORTED");
        string[] kinds=layers.Select(Kind).ToArray();
        string[] allowed=["dnstt","sayedns","vaydns","soar","ss","slipstream","ssh","naive","snowflake","doh","socks5","xray"];
        if(kinds.Any(k=>!allowed.Contains(k)))throw new SafeError("CHAIN_SINGLE_LAYER_ONLY");
        if(kinds.Contains("xray")&&(layers.Count!=2||kinds[0]!="xray"||kinds[1]!="ssh"))throw new SafeError("XRAY_CHAIN_REQUIRES_OUTER_SSH");
        var groups=new HashSet<string>();
        for(int i=0;i<layers.Count;i++){
            string kind=kinds[i];if(i>0&&kind is "ss" or "slipstream")throw new SafeError("CHAIN_SLIP_OUTERMOST");
            string group=kind switch{"sayedns"=>"dnstt","ss"=>"slipstream",_=>kind};
            if(kind is not("ssh" or "socks5")&&!groups.Add(group))throw new SafeError("CHAIN_DUPLICATE_ENGINE");
            if(ProfileEditing.IsNative(layers[i].Raw)&&Importer.Fields(layers[i].Raw)[34]!="1")throw new SafeError("SHARING_DISABLED");
        }
    }
    public static Profile Parse(string raw)
    {
        var (name,layers)=Read(raw);Validate(layers);
        return new(){Name=name,Raw=raw,Protocol="Chain",CollectionId="manual:"+Importer.Fingerprint(raw)};
    }
    public static (string Name,List<Profile> Layers) Read(string raw)
    {
        if(!raw.StartsWith(Prefix,StringComparison.Ordinal)||raw.Length>131072)throw new SafeError("INVALID_IMPORT");
        using var doc=JsonDocument.Parse(Importer.Decode(raw[Prefix.Length..]),new JsonDocumentOptions{MaxDepth=4});
        var root=doc.RootElement;Importer.Keys(root,"name","layers");string name=root.GetProperty("name").GetString()??"";
        if(name.Length is <1 or >200||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        var items=root.GetProperty("layers");if(items.GetArrayLength() is <2 or >4)throw new SafeError("CHAIN_LAYER_COUNT");
        var layers=new List<Profile>();var ids=new HashSet<string>();
        foreach(var layer in items.EnumerateArray()){
            Importer.Keys(layer,"id","raw");string id=layer.GetProperty("id").GetString()??"",text=layer.GetProperty("raw").GetString()??"";
            if(id.Length is <1 or >128||id.Any(char.IsControl)||!ids.Add(id)||IsChain(text))throw new SafeError("INVALID_IMPORT");
            var p=Importer.Single(text);p.Id=id;layers.Add(p);
        }
        Validate(layers);return(name,layers);
    }
    public static string Create(string name,IReadOnlyList<Profile> layers)
    {
        Validate(layers);string raw=Prefix+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{name=name.Trim(),layers=layers.Select(p=>new{id=p.Id,raw=p.Raw})})));
        _=Parse(raw);return raw;
    }
}
