using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;

public static class AlternativeProfiles
{
    public static string Kind(string raw)=>ProfileEditing.IsNative(raw)?Importer.Fields(raw)[1]:"";
    public static bool Handles(string raw)=>Kind(raw) is "snowflake" or "doh";
    public static string DohUrl(string raw)
    {
        var f=Importer.Fields(raw);if(f[1]!="doh")throw new SafeError("INVALID_IMPORT");
        var uri=SubscriptionUrls.Validate(f[21]);if(uri.Fragment.Length>0)throw new SafeError("INVALID_IMPORT");
        return uri.AbsoluteUri;
    }
    public static string[] Bridges(string raw)
    {
        var f=Importer.Fields(raw);if(f[1]!="snowflake")throw new SafeError("INVALID_IMPORT");
        string value=f[26].Length==0?"":Importer.Decode(f[26]);if(value.Length>16384)throw new SafeError("IMPORT_TOO_LARGE");
        string[] lines=value.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(l=>!l.StartsWith('#')).ToArray();
        if(lines.Length>32)throw new SafeError("INVALID_IMPORT");
        string[] transports=["obfs4","obfs3","obfs2","scramblesuit","meek_lite","webtunnel","snowflake"];
        foreach(string line in lines){
            if(line.Any(char.IsControl)||line.Contains('"')||line.Contains('\\'))throw new SafeError("INVALID_IMPORT");
            var words=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);int first=words[0].Equals("Bridge",StringComparison.OrdinalIgnoreCase)?1:0;
            if(words.Length<first+2||!transports.Contains(words[first])&&!IPEndPoint.TryParse(words[first],out _))throw new SafeError("TOR_BRIDGE_NOT_SUPPORTED");
        }
        return lines.Select(l=>l.StartsWith("Bridge ",StringComparison.OrdinalIgnoreCase)?l[7..]:l).ToArray();
    }
    public static string Create(string kind,string name,string value)
    {
        name=name.Trim();if(kind is not("snowflake" or "doh")||name.Length is <1 or >200||name.Contains('|')||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        var f=Enumerable.Repeat("",88).ToArray();f[0]="33";f[1]=kind;f[2]=name;f[31]=f[33]=f[36]="0";f[34]="1";
        f[87]=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{sshUser="",sshPass="",socksUser="",socksPass=""}));
        if(kind=="doh")f[21]=value.Trim();else f[26]=Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string raw="rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',f)));Validate(raw);return raw;
    }
    public static void Validate(string raw){if(Kind(raw)=="doh")_=DohUrl(raw);else _=Bridges(raw);}
    public static ParsedLink Descriptor(Profile p)
    {
        Validate(p.Raw);
        return new(p.Name,Kind(p.Raw)=="doh"?"DoH":"Tor",new JsonObject{["protocol"]=Kind(p.Raw)=="doh"?"doh":"tor",["settings"]=new JsonObject()});
    }
}
