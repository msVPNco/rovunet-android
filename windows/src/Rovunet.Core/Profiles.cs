using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;

public sealed class SafeError(string code) : Exception(code) { public string Code { get; } = code; }

// Never use generated record ToString() on a credential-bearing type.
public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string CollectionId { get; set; } = "";
    public string CollectionName { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Raw { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string BlockReason { get; set; } = "";
    public bool CanConnect => BlockReason.Length == 0;
    public override string ToString() => "Profile(redacted)";
}

public static class Importer
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string Decode(string value)
    {
        var b = value.Replace('-', '+').Replace('_', '/');
        b = b.PadRight((b.Length + 3) / 4 * 4, '=');
        try { return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(b)); }
        catch { throw new SafeError("INVALID_IMPORT"); }
    }
    public static List<Profile> Read(string body)
    {
        try { return ReadInternal(body); }
        catch (SafeError) { throw; }
        catch { throw new SafeError("INVALID_IMPORT"); }
    }
    static List<Profile> ReadInternal(string body, CompatibleImport? admission = null, bool experimental = false)
    {
        if (Encoding.UTF8.GetByteCount(body) > MaxBytes) throw new SafeError("IMPORT_TOO_LARGE");
        body = body.Trim().TrimStart('\uFEFF');
        if (body.Length == 0 || body.StartsWith('<')) throw new SafeError("INVALID_IMPORT");
        if((body.StartsWith('{')||body.StartsWith('['))&&!body.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)&&ExternalConfigs.Recognizes(body))return ExternalConfigs.Read(body,admission,experimental);
        var items = new List<(string Id, string Link)>();
        string sourceName = "";
        if (body.StartsWith('{'))
        {
            using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            Keys(root, "version", "items", "name");
            if (root.GetProperty("version").GetInt32() != 1) throw new SafeError("UNSUPPORTED_FORMAT");
            sourceName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (sourceName.Length > 120) throw new SafeError("INVALID_IMPORT");
            foreach (var item in root.GetProperty("items").EnumerateArray())
            {
                Keys(item, "id", "link");
                string id = item.GetProperty("id").GetString()!, link = item.GetProperty("link").GetString()!;
                if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || items.Any(i => i.Id == id)) throw new SafeError("INVALID_IMPORT");
                items.Add((id, link));
            }
        }
        else if (body.StartsWith("[*] Configs for this user:", StringComparison.Ordinal))
        {
            foreach (var line in body.Split('\n', StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("rovunet", StringComparison.Ordinal) || line.StartsWith("slipnet", StringComparison.Ordinal))
                    items.Add(("", line));
            }
        }
        else
        {
            if (!body.Contains("://") && !body.StartsWith("[Interface]", StringComparison.OrdinalIgnoreCase)) body = Decode(body);
            if (body.StartsWith("[Interface]", StringComparison.OrdinalIgnoreCase)) items.Add(("", body));
            else foreach (var line in body.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) items.Add(("", line));
        }
        if (items.Count is < 1 or > 1000) throw new SafeError("INVALID_IMPORT");
        var results = new List<Profile>();
        foreach (var item in items)
        {
            Profile p;
            try { p = Single(item.Link); }
            catch(SafeError e) when(admission!=null&&(e.Code is "ENGINE_NOT_READY" or "CUSTOM_TRANSPORT_NOT_READY" or "XHTTP_EXTRA_NOT_READY"||(e.Code is "UNSUPPORTED_FORMAT" or "TLS_UNVERIFIED")&&!ProfileEditing.IsNative(item.Link)||e.Code=="UNSUPPORTED_FORMAT"&&CanSkipNativeXray(item.Link))){admission.Total++;admission.Skip(e.Code);string proto=item.Link.Split("://")[0].ToUpperInvariant()+" · "+e.Code;admission.Protocols[proto]=admission.Protocols.GetValueOrDefault(proto)+1;continue;}
            if(admission != null) { admission.Total++; string reason=SupportPolicy.Reason(p,experimental); if(reason.Length>0){admission.Skip(reason);continue;} }
            p.ItemId = item.Id;
            if(admission!=null)admission.Protocols[p.Protocol]=admission.Protocols.GetValueOrDefault(p.Protocol)+1;
            if (sourceName.Length > 0) p.CollectionName = sourceName;
            results.Add(p);
        }
        return results; // Atomic: a bad/protected item never yields a partial import.
    }
    public static CompatibleImport ReadCompatible(string body, bool experimental = true)
    {
        var result=new CompatibleImport();
        try { result.Profiles.AddRange(ReadInternal(body,result,experimental)); return result; }
        catch(SafeError) { throw; } catch { throw new SafeError("INVALID_IMPORT"); }
    }
    static bool CanSkipNativeXray(string raw)
    {
        if(!ProfileEditing.IsNative(raw))return false;
        try{
            // Validate the complete native envelope, identity and protections first.
            // Unknown native versions/credential fields remain atomic failures.
            var f=Fields(raw);if(f.Length<85||f[1]!="xray")return false;
            string inner=Decode(f[84]);
            return inner.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)||new[]{"vless://","socks5://","hy2://","hysteria2://","ss://","trojan://","vmess://","wireguard://"}.Any(s=>inner.StartsWith(s,StringComparison.OrdinalIgnoreCase));
        }catch{return false;}
    }
    public static void Keys(JsonElement obj, params string[] keys)
    {
        var seen = new HashSet<string>();
        foreach (var p in obj.EnumerateObject()) if (!keys.Contains(p.Name) || !seen.Add(p.Name)) throw new SafeError("UNSUPPORTED_FORMAT");
    }
    public static Profile Single(string raw)
    {
        raw = raw.Trim();
        if (raw.Length > 131072) throw new SafeError("IMPORT_TOO_LARGE");
        if (raw.StartsWith("rovunet-enc://") || raw.StartsWith("slipnet-enc://") || raw.StartsWith("rovunet-bundle-enc://") || raw.StartsWith("slipnet-bundle-enc://")) throw new SafeError("PROTECTED_PROFILE");
        var p = new Profile { Raw = raw };
        if (raw.StartsWith("rovunet://") || raw.StartsWith("slipnet://"))
        {
            var f = Fields(raw);
            p.Name = f[2];
            if (p.Name.Length > 200) throw new SafeError("INVALID_IMPORT");
            if (f.Length >= 87)
            {
                p.CollectionId = Decode(f[85]); p.CollectionName = Decode(f[86]);
                if (p.CollectionId.Length > 128 || p.CollectionName.Length > 120) throw new SafeError("INVALID_IMPORT");
            }
            if (f[1] == "vless" && f.Length < 79) throw new SafeError("INVALID_IMPORT");
            p.Protocol = f[1] switch { "vless" => "VLESS WS", "sayedns" => "NoizDNS", "sayedns_ssh" => "NoizDNS + SSH", "ssh" => "SSH", "dnstt" => "DNSTT", "dnstt_ssh" => "DNSTT + SSH", "soar" => "Soar", "soar_ssh" => "Soar + SSH", "ss" or "slipstream" => "Slipstream", "slipstream_ssh" => "Slipstream + SSH", "vaydns" => "VayDNS", "vaydns_ssh" => "VayDNS + SSH", "naive" => "NaiveProxy", "naive_ssh" => "NaiveProxy + SSH", _ => f[1] };
            if (f[1] == "xray") { if (f.Length < 85) throw new SafeError("INVALID_IMPORT"); var inner = Single(Decode(f[84])); p.Protocol = inner.Protocol; p.BlockReason = inner.BlockReason; }
            else if (TransportPlan.Supports(f[1])) p.BlockReason = TransportPlan.Validate(f);
            else if (f[1] is not ("vless" or "socks5")) p.BlockReason = "ENGINE_NOT_READY";
            if (f[1] == "vless") p.BlockReason = TransportPlan.Validate(f);
        }
        else if (raw.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("[Interface]", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase)||raw.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase))
        {
            var parsed = CoreLinks.Parse(raw); p.Name = parsed.Name; p.Protocol = parsed.Protocol;
        }
        else throw new SafeError("UNSUPPORTED_FORMAT");
        if (string.IsNullOrWhiteSpace(p.Name)) p.Name = p.Protocol;
        if (p.CollectionId.Length == 0) p.CollectionId = "manual:" + Fingerprint(raw);
        if (p.CanConnect && !TransportPlan.UsesHelper(p.Raw) && !TransportPlan.UsesNaive(p.Raw)) _ = Connection(p);
        return p;
    }
    public static bool RefreshCapabilities(Profile stored)
    {
        string protocol = stored.Protocol, blocked;
        try { var parsed = Single(stored.Raw); protocol = parsed.Protocol; blocked = parsed.BlockReason; }
        catch (SafeError e) { blocked = e.Code; }
        catch { blocked = "INVALID_IMPORT"; }
        bool changed = stored.Protocol != protocol || stored.BlockReason != blocked;
        stored.Protocol = protocol; stored.BlockReason = blocked;
        // Names, source/item IDs, groups, raw choices and selected IDs are not migrated.
        return changed;
    }
    public static string[] Fields(string raw)
    {
        var f = Decode(raw[(raw.IndexOf("://", StringComparison.Ordinal) + 3)..]).Split('|');
        if (f.Length == 0 || !int.TryParse(f[0], out int version)) throw new SafeError("INVALID_IMPORT");
        int length = version switch { 22 => 60, 28 => 79, 29 => 82, 30 => 84, 31 => 85, 32 => 87, 33 => 88, _ => throw new SafeError("UNSUPPORTED_FORMAT") };
        if (f.Length != length) throw new SafeError("INVALID_IMPORT");
        if (f[31] != "0" || f[32] != "" || f[33] != "0" || f[35] != "" || f[36] != "0" || f[37] != "") throw new SafeError("PROTECTED_PROFILE");
        if (f[34] is not ("0" or "1")) throw new SafeError("INVALID_IMPORT");
        if (version == 33)
        {
            using var c = JsonDocument.Parse(Decode(f[87]));
            Keys(c.RootElement, "sshUser", "sshPass", "socksUser", "socksPass");
            f[12] = c.RootElement.GetProperty("socksUser").GetString()!; f[13] = c.RootElement.GetProperty("socksPass").GetString()!;
            f[15] = c.RootElement.GetProperty("sshUser").GetString()!; f[16] = c.RootElement.GetProperty("sshPass").GetString()!;
        }
        return f;
    }
    public static ParsedLink Connection(Profile p)
    {
        if (!p.CanConnect) throw new SafeError(p.BlockReason);
        if (!p.Raw.StartsWith("rovunet://") && !p.Raw.StartsWith("slipnet://")) return CoreLinks.Parse(p.Raw);
        var f = Fields(p.Raw);
        if (f[1] == "xray") return CoreLinks.Parse(Decode(f[84]));
        if (f[1] == "socks5") return CoreLinks.Socks(p.Name, f[3], Port(f[40]), f[12], f[13]);
        if (f[1] != "vless" || f[64] != "ws") throw new SafeError("ENGINE_NOT_READY");
        var host = string.IsNullOrEmpty(f[66]) ? f[3] : f[66];
        string sni = f.Length > 78 && f[78].Length > 0 ? f[78] : f[3];
        return CoreLinks.Vless(p.Name, host, Port(f[67]), f[62], new() { ["type"] = "ws", ["security"] = f[63], ["host"] = f[3], ["path"] = f[65], ["sni"] = sni });
    }
    public static int Port(string text) => int.TryParse(text, out var n) && n is > 0 and <= 65535 ? n : throw new SafeError("INVALID_IMPORT");
}

public sealed class ParsedLink(string name, string protocol, JsonObject outbound)
{
    public string Name { get; } = name;
    public string Protocol { get; } = protocol;
    public JsonObject Outbound { get; } = outbound;
    public override string ToString() => "ParsedLink(redacted)";
}

public static partial class CoreLinks
{
    static JsonObject Obj(params (string Key, object? Value)[] fields) { var o = new JsonObject(); foreach (var f in fields) o[f.Key] = JsonSerializer.SerializeToNode(f.Value); return o; }
    static JsonArray Arr(params JsonNode[] nodes) => new(nodes);
    static string Unescape(string s) => Uri.UnescapeDataString(s);
    public static ParsedLink Parse(string raw)
    {
        try
        {
            if (raw.TrimStart().StartsWith("[Interface]", StringComparison.OrdinalIgnoreCase)) return WireGuard(raw);
            if(raw.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase))return WireGuardLink(raw);
            if(raw.StartsWith("ss://",StringComparison.OrdinalIgnoreCase))return Shadowsocks(raw,"Shadowsocks");
            if(raw.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase))return Vmess(raw,"VMess");
            if (raw.Length > 65536 || !Uri.TryCreate(raw, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Host) || u.AbsolutePath is not ("" or "/")) throw new SafeError("INVALID_IMPORT");
            var q = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in u.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            { var kv = field.Split('=', 2); if (!q.TryAdd(Unescape(kv[0]), kv.Length == 2 ? Unescape(kv[1]) : "")) throw new SafeError("INVALID_IMPORT"); }
            var auth = Unescape(u.UserInfo); var name = Unescape(u.Fragment.TrimStart('#'));
            if (name.Length == 0) name = u.Scheme.ToUpperInvariant();
            var host = u.Host.Trim('[', ']'); int port = u.Port < 0 ? (u.Scheme == "socks5" ? 1080 : 443) : u.Port;
            if (u.Scheme == "ss") return Shadowsocks(raw, name);
            if (u.Scheme == "vmess") return Vmess(raw, name);
            if (u.Scheme == "socks5")
            {
                if (q.Count != 0) throw new SafeError("UNSUPPORTED_FORMAT");
                var a = u.UserInfo.Split(':', 2); return Socks(name, host, port, Unescape(a[0]), a.Length == 2 ? Unescape(a[1]) : "");
            }
            if (u.Scheme is "hy2" or "hysteria2") return Hysteria(name, host, port, auth, q);
            if (u.Scheme == "trojan") return Trojan(name, host, port, auth, q);
            if (u.Scheme != "vless") throw new SafeError("ENGINE_NOT_READY");
            return Vless(name, host, port, auth, q);
        }
        catch (SafeError) { throw; } catch { throw new SafeError("INVALID_IMPORT"); }
    }
    public static ParsedLink Socks(string name, string host, int port, string user, string password)
    {
        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535) throw new SafeError("INVALID_IMPORT");
        var server = Obj(("address", host), ("port", port));
        if (user.Length > 0) server["users"] = Arr(Obj(("user", user), ("pass", password)));
        else if (password.Length > 0) throw new SafeError("INVALID_IMPORT");
        return new(name, "SOCKS5", Obj(("protocol", "socks"), ("settings", Obj(("servers", Arr(server))))));
    }
    public static ParsedLink Vless(string name, string host, int port, string id, Dictionary<string, string> q)
    {
        var allowed = new HashSet<string>(["type", "security", "encryption", "flow", "sni", "fp", "pbk", "sid", "spx", "path", "host", "mode", "alpn", "extra", "fragment","serviceName","authority","ed","eh","headerType","allowInsecure","insecure","heartbeatPeriod","mtu","tti","cs","fm","Telegram"]);
        if (q.Keys.Any(k => !allowed.Contains(k)) || (q.TryGetValue("fragment", out var frag) && frag.Length != 0)) throw new SafeError("UNSUPPORTED_FORMAT");
        string Get(string k, string fallback = "") => q.GetValueOrDefault(k, fallback);
        if (!Guid.TryParseExact(id, "D", out _) || host.Length == 0 || port is < 1 or > 65535) throw new SafeError("INVALID_IMPORT");
        string encryption=NormalizeVlessEncryption(Get("encryption"));
        foreach(string flag in new[]{"allowInsecure","insecure"})if(Get(flag).ToLowerInvariant() is not ("" or "0" or "false"))throw new SafeError("TLS_UNVERIFIED");
        if(Get("headerType") is not ("" or "none")||Get("eh").Length>0)throw new SafeError("UNSUPPORTED_FORMAT");
        string transport = Get("type", "tcp") switch { "tcp" or "raw" => "raw", "ws" or "websocket" => "ws", "xhttp" => "xhttp", "grpc"=>"grpc","httpupgrade"=>"httpupgrade","kcp" or "mkcp"=>"kcp", _ => throw new SafeError("ENGINE_NOT_READY") };
        var security = Get("security", "none"); var flow = Get("flow");
        if (security is not ("none" or "tls" or "reality") || (security == "reality" && transport == "ws")) throw new SafeError("UNSUPPORTED_FORMAT");
        if (flow.Length > 0 && (flow != "xtls-rprx-vision" || transport != "raw" || security == "none"&&encryption=="none")) throw new SafeError("UNSUPPORTED_FORMAT");
        var stream = Obj(("network", transport), ("security", security));
        if (security == "tls")
        {
            var tls = Obj(("serverName", Get("sni", host)), ("fingerprint", Get("fp", "chrome")), ("allowInsecure", false));
            if (q.ContainsKey("alpn")) tls["alpn"] = JsonSerializer.SerializeToNode(Get("alpn").Split(','));
            if(Get("cs").Length>0)tls["cipherSuites"]=CipherSuites20(Get("cs"));
            stream["tlsSettings"] = tls;
        }
        if (security == "reality")
        {
            var key = Get("pbk").Replace('-', '+').Replace('_', '/'); key = key.PadRight((key.Length + 3) / 4 * 4, '=');
            try { if (Convert.FromBase64String(key).Length != 32) throw new Exception(); } catch { throw new SafeError("INVALID_IMPORT"); }
            var sid = Get("sid"); if (sid.Length > 16 || sid.Length % 2 != 0 || sid.Any(c => !Uri.IsHexDigit(c))) throw new SafeError("INVALID_IMPORT");
            stream["realitySettings"] = Obj(("serverName", Get("sni", host)), ("fingerprint", Get("fp", "chrome")), ("publicKey", Get("pbk")), ("shortId", sid), ("spiderX", Get("spx", "/")));
        }
        if(Get("cs").Length>0&&security!="tls")throw new SafeError("UNSUPPORTED_FORMAT");
        var mask20=FinalMask20(Get("fm"));if(mask20!=null){if(security!="tls")throw new SafeError("UNSUPPORTED_FORMAT");stream["finalmask"]=mask20;}
        if (transport == "ws") stream["wsSettings"] = Obj(("path", Get("path", "/")), ("headers", Obj(("Host", Get("host", host)))));
        if(Get("heartbeatPeriod").Length>0){if(transport!="ws"||!uint.TryParse(Get("heartbeatPeriod"),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out uint heartbeat))throw new SafeError("CORE_FIELD_INVALID");stream["wsSettings"]!["heartbeatPeriod"]=heartbeat;}
        if(transport=="kcp"){
            var kcp=new JsonObject();foreach(var(key,min,max)in new[]{("mtu",21,65507),("tti",10,1000)})if(Get(key).Length>0){if(!int.TryParse(Get(key),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int number)||number<min||number>max)throw new SafeError("CORE_FIELD_INVALID");kcp[key]=number;}stream["kcpSettings"]=kcp;
        }else if(Get("mtu").Length>0||Get("tti").Length>0)throw new SafeError("UNSUPPORTED_FORMAT");
        if(transport is "grpc" or "httpupgrade"){
            if(security=="reality")throw new SafeError("UNSUPPORTED_FORMAT");
            var converted=ExtraStream(host,q,"none");stream[transport=="grpc"?"grpcSettings":"httpupgradeSettings"]=converted[transport=="grpc"?"grpcSettings":"httpupgradeSettings"]!.DeepClone();
        }
        if(Get("ed").Length>0){if(transport!="ws"||!int.TryParse(Get("ed"),out int early)||early is <0 or >65536)throw new SafeError("UNSUPPORTED_FORMAT");if(early>0){string path=Get("path","/");if(path.Contains("ed=",StringComparison.Ordinal))throw new SafeError("UNSUPPORTED_FORMAT");stream["wsSettings"]!["path"]=path+(path.Contains('?')?"&":"?")+"ed="+early;}}
        if (transport == "xhttp")
        {
            var mode = Get("mode", "auto"); if (mode is not ("auto" or "packet-up" or "stream-up" or "stream-one")) throw new SafeError("INVALID_IMPORT");
            var settings = Obj(("path", Get("path", "/")), ("host", Get("host", host)), ("mode", mode));
            // Separate download settings can change identity/routing; do not guess.
            if (q.ContainsKey("extra"))
            {
                var extra = JsonNode.Parse(Get("extra"), documentOptions: new JsonDocumentOptions { MaxDepth = 12 }) as JsonObject ?? throw new SafeError("INVALID_IMPORT");
                if(extra.TryGetPropertyValue("mode",out var extraMode)){string value=extraMode?.GetValue<string>()??"";if(value is not("auto" or "packet-up" or "stream-up" or "stream-one")||Get("mode").Length>0&&Get("mode")!=value)throw new SafeError("XHTTP_EXTRA_NOT_READY");settings["mode"]=value;extra.Remove("mode");}
                // Exact allow-list: alternate case and separate download endpoints cannot bypass it.
                string[] allowedExtra = ["headers", "xPaddingBytes", "xPaddingObfsMode", "xPaddingKey", "xPaddingHeader", "xPaddingPlacement", "xPaddingMethod", "uplinkHTTPMethod", "sessionIDPlacement", "sessionIDKey", "sessionIDTable", "sessionIDLength", "seqPlacement", "seqKey", "uplinkDataPlacement", "uplinkDataKey", "uplinkChunkSize", "noGRPCHeader", "noSSEHeader", "scMaxEachPostBytes", "scMinPostsIntervalMs", "scMaxBufferedPosts", "scStreamUpServerSecs", "serverMaxHeaderBytes", "xmux", "h2Params"];
                if (extra.Any(e => !allowedExtra.Contains(e.Key))) throw new SafeError("XHTTP_EXTRA_NOT_READY");
                settings["extra"] = extra;
            }
            stream["xhttpSettings"] = settings;
        }
        var user = Obj(("id", id), ("encryption", encryption), ("flow", flow));
        var outbound = Obj(("protocol", "vless"), ("settings", Obj(("vnext", Arr(Obj(("address", host), ("port", port), ("users", Arr(user))))))), ("streamSettings", stream));
        return new(name, transport == "ws" ? "VLESS WS" : transport == "xhttp" ? "VLESS XHTTP" : transport=="kcp"?"VLESS mKCP":transport=="grpc"?"VLESS gRPC":transport=="httpupgrade"?"VLESS HTTPUpgrade":security == "reality" ? "VLESS REALITY" : "VLESS TCP", outbound);
    }
    public static string Runtime(Profile p, int httpPort, int socksPort, int helperPort = 0, int statsPort = 0)
    {
        p = Importer.Single(p.Raw);
        if (!p.CanConnect) throw new SafeError(p.BlockReason);
        if (helperPort == 0 && (TransportPlan.UsesHelper(p.Raw) || TransportPlan.UsesNaive(p.Raw))) throw new SafeError("ENGINE_NOT_READY");
        var outbound = (helperPort > 0 ? Socks(p.Name, "127.0.0.1", helperPort, "", "") : Importer.Connection(p)).Outbound.DeepClone().AsObject(); outbound["tag"] = "proxy";
        var inbounds = Arr(Obj(("tag","local-http"),("listen", "127.0.0.1"), ("port", httpPort), ("protocol", "http"), ("settings", new JsonObject())),
            Obj(("tag","local-socks"),("listen", "127.0.0.1"), ("port", socksPort), ("protocol", "socks"), ("settings", Obj(("auth", "noauth"), ("udp", false)))));
        // No freedom/direct fallback: every accepted inbound uses this one outbound.
        var config=Obj(("log", Obj(("loglevel", "none"))), ("inbounds", inbounds), ("outbounds", Arr(outbound)));
        if(statsPort>0){_=Importer.Port(statsPort.ToString());config["stats"]=new JsonObject();config["policy"]=Obj(("system",Obj(("statsInboundUplink",true),("statsInboundDownlink",true))));config["api"]=Obj(("tag","metrics"),("listen","127.0.0.1:"+statsPort),("services",Arr("StatsService")));}
        return config.ToJsonString();
    }
}
