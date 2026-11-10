using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;
public static partial class CoreLinks
{
    static ParsedLink Hysteria(string name, string host, int port, string auth, Dictionary<string,string> q)
    {
        string Get(string k) => q.GetValueOrDefault(k, "");
        if(Get("insecure").ToLowerInvariant() is not("" or "0" or "false"))throw new SafeError("TLS_UNVERIFIED");
        if (q.Keys.Any(k => !new[] { "sni", "insecure", "obfs", "obfs-password", "alpn", "security" }.Contains(k))||Get("security") is not("" or "tls")) throw new SafeError("UNSUPPORTED_FORMAT");
        if (auth.Length == 0 || Get("alpn") is not ("" or "h3")) throw new SafeError("INVALID_IMPORT");
        var stream = Obj(("network", "hysteria"), ("security", "tls"), ("tlsSettings", Obj(("serverName", q.GetValueOrDefault("sni", host)), ("allowInsecure", false), ("alpn", new[] { "h3" }))), ("hysteriaSettings", Obj(("version", 2), ("auth", auth))));
        if (Get("obfs") == "salamander" && Get("obfs-password").Length > 0) stream["finalmask"] = Obj(("udp", Arr(Obj(("type", "salamander"), ("settings", Obj(("password", Get("obfs-password"))))))));
        else if (Get("obfs").Length > 0 || Get("obfs-password").Length > 0) throw new SafeError("INVALID_IMPORT");
        return new(name, "Hysteria2", Obj(("protocol", "hysteria"), ("settings", Obj(("version", 2), ("address", host), ("port", port))), ("streamSettings", stream)));
    }
    static ParsedLink WireGuard(string raw)
    {
        if (raw.Length > 65536) throw new SafeError("IMPORT_TOO_LARGE");
        var iface = new Dictionary<string,string>(); var peer = new Dictionary<string,string>(); Dictionary<string,string>? current = null;
        foreach (var line in raw.Split('\n').Select(l => l.Split('#')[0].Trim()).Where(l => l.Length > 0))
        {
            if (line.Equals("[Interface]", StringComparison.OrdinalIgnoreCase)) { if (current != null) throw new SafeError("INVALID_IMPORT"); current = iface; }
            else if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase)) { if (current != iface) throw new SafeError("INVALID_IMPORT"); current = peer; }
            else { var kv = line.Split('=', 2); if (kv.Length != 2 || current == null || !current.TryAdd(kv[0].Trim().ToLowerInvariant(), kv[1].Trim())) throw new SafeError("INVALID_IMPORT"); }
        }
        if (iface.Keys.Any(k => !new[] { "privatekey", "address", "dns", "mtu" }.Contains(k)) || peer.Keys.Any(k => !new[] { "publickey", "presharedkey", "endpoint", "allowedips", "persistentkeepalive" }.Contains(k))) throw new SafeError("UNSUPPORTED_FORMAT");
        string Need(Dictionary<string,string> map, string key) => map.TryGetValue(key, out var v) && v.Length > 0 ? v : throw new SafeError("INVALID_IMPORT");
        string Key(string v) { try { if (Convert.FromBase64String(v).Length == 32) return v; } catch { } throw new SafeError("INVALID_IMPORT"); }
        string[] Cidrs(string v) => v.Split(',').Select(c => { var s = c.Trim().Split('/'); if (s.Length != 2 || !IPAddress.TryParse(s[0], out var ip) || !int.TryParse(s[1], out var n) || n < 0 || n > ip.GetAddressBytes().Length * 8) throw new SafeError("INVALID_IMPORT"); return c.Trim(); }).ToArray();
        string endpoint = Need(peer, "endpoint");
        if (!Uri.TryCreate("udp://" + endpoint, UriKind.Absolute, out var ep) || ep.Host.Length == 0 || ep.Port is < 1 or > 65535 || ep.UserInfo.Length > 0 || ep.AbsolutePath is not ("" or "/") || ep.Query.Length > 0 || ep.Fragment.Length > 0) throw new SafeError("INVALID_IMPORT");
        var p = Obj(("endpoint", endpoint), ("publicKey", Key(Need(peer, "publickey"))), ("allowedIPs", Cidrs(Need(peer, "allowedips"))));
        if (peer.TryGetValue("presharedkey", out var psk)) p["preSharedKey"] = Key(psk);
        if (peer.TryGetValue("persistentkeepalive", out var ka)) { if (!int.TryParse(ka, out var n) || n is < 0 or > 65535) throw new SafeError("INVALID_IMPORT"); p["keepAlive"] = n; }
        int mtu = 1280; if (iface.TryGetValue("mtu", out var m) && (!int.TryParse(m, out mtu) || mtu is < 1280 or > 1500)) throw new SafeError("INVALID_IMPORT");
        var settings = Obj(("secretKey", Key(Need(iface, "privatekey"))), ("address", Cidrs(Need(iface, "address"))), ("peers", Arr(p)), ("noKernelTun", true), ("mtu", mtu));
        if (iface.TryGetValue("dns", out var dns)) { var ips = dns.Split(',').Select(x => x.Trim()).ToArray(); if (ips.Any(x => !IPAddress.TryParse(x, out _))) throw new SafeError("INVALID_IMPORT"); settings["remoteDNS"] = JsonSerializer.SerializeToNode(ips); }
        return new("WireGuard", "WireGuard", Obj(("protocol", "wireguard"), ("settings", settings)));
    }
}
