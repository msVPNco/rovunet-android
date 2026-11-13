using System.Text;

namespace Rovunet.Core;

public static partial class CoreLinks
{
    public static string WireGuardLinkConfig(string raw)
    {
        if(raw.Length>65536)throw new SafeError("IMPORT_TOO_LARGE");
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri)||uri.Scheme!="wireguard"||uri.Host.Length==0||uri.Port is <1 or >65535||uri.AbsolutePath is not("" or "/"))throw new SafeError("INVALID_IMPORT");
        var options=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string item in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){var pair=item.Split('=',2);if(!options.TryAdd(Uri.UnescapeDataString(pair[0]),pair.Length>1?Uri.UnescapeDataString(pair[1]):""))throw new SafeError("INVALID_IMPORT");}
        string[] allowed=["publickey","presharedkey","address","allowedips","dns","mtu","keepalive","persistentkeepalive"];
        if(options.Keys.Any(k=>!allowed.Contains(k.ToLowerInvariant())&&!AmneziaConfig.Labels.ContainsKey(k.ToLowerInvariant()))||options.Values.Any(v=>v.Contains('#')||v.Any(char.IsControl))||options.ContainsKey("keepalive")&&options.ContainsKey("persistentkeepalive"))throw new SafeError("UNSUPPORTED_FORMAT");
        string Need(string key)=>options.TryGetValue(key,out var value)&&value.Length>0?value:throw new SafeError("INVALID_IMPORT");
        string Secret(string value){try{byte[] bytes=Convert.FromBase64String(value);if(bytes.Length==32)return Convert.ToBase64String(bytes);}catch{}throw new SafeError("INVALID_IMPORT");}
        string privateKey=Secret(Uri.UnescapeDataString(uri.UserInfo));string host=uri.Host.Trim('[',']');
        var config=new StringBuilder("[Interface]\nPrivateKey = ").Append(privateKey).Append("\nAddress = ").Append(Need("address")).Append('\n');
        foreach(string field in new[]{"dns","mtu"})if(options.TryGetValue(field,out var value)&&value.Length>0)config.Append(field=="dns"?"DNS":"MTU").Append(" = ").Append(value).Append('\n');
        foreach(var option in AmneziaConfig.Labels)if(options.TryGetValue(option.Key,out var value))config.Append(option.Value).Append(" = ").Append(value).Append('\n');
        config.Append("[Peer]\nPublicKey = ").Append(Secret(Need("publickey"))).Append("\nEndpoint = ").Append(host.Contains(':')?"["+host+"]":host).Append(':').Append(uri.Port).Append("\nAllowedIPs = ").Append(Need("allowedips")).Append('\n');
        if(options.TryGetValue("presharedkey",out var psk))config.Append("PresharedKey = ").Append(Secret(psk)).Append('\n');
        string keepalive=options.GetValueOrDefault("persistentkeepalive",options.GetValueOrDefault("keepalive",""));if(keepalive.Length>0)config.Append("PersistentKeepalive = ").Append(keepalive).Append('\n');
        string text=config.ToString();_ = Parse(text);return text;
    }
    static ParsedLink WireGuardLink(string raw)
    {
        var parsed=Parse(WireGuardLinkConfig(raw));var uri=new Uri(raw);string name=Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));if(name.Length>200||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");return new(name.Length>0?name:parsed.Name,parsed.Protocol,parsed.Outbound);
    }
}
