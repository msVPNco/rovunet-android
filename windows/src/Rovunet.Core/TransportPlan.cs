using System.Text.Json;
namespace Rovunet.Core;
public static class TransportPlan
{
    static bool Native(string raw) => raw.StartsWith("rovunet://") || raw.StartsWith("slipnet://");
    public static bool UsesHelper(string raw)
    {
        if (!Native(raw)) return false;
        var f = Importer.Fields(raw);
        if (f[1] == "vless") return new[] {68,72,73,74}.Any(i => f[i] == "1");
        return Supports(f[1]) && f[1] != "naive";
    }
    public static bool IsSlip(string kind) => kind is "ss" or "slipstream" or "slipstream_ssh";
    public static bool UsesSlip(string raw) => Native(raw) && IsSlip(Importer.Fields(raw)[1]);
    public static bool Supports(string kind) => IsSlip(kind) || kind is "doh" or "vaydns" or "vaydns_ssh" or "ssh" or "dnstt" or "dnstt_ssh" or "sayedns" or "sayedns_ssh" or "soar" or "soar_ssh" or "naive" or "naive_ssh";
    public static string[] Normalize(string[] original)
    {
        var f = original.Concat(Enumerable.Repeat("", Math.Max(0, 88-original.Length))).ToArray();
        if (string.IsNullOrEmpty(f[60])) f[60] = "fanout";
        if (string.IsNullOrEmpty(f[61])) f[61] = "3";
        return f;
    }
    public static string Validate(string[] f)
    {
        if(f[1]=="doh"){_=SubscriptionUrls.Validate(f[21]);return "";}

        f = Normalize(f);
        bool ssh = f[1] == "ssh" || f[1].EndsWith("_ssh", StringComparison.Ordinal);
        if (ssh)
        {
            if (f[15].Length == 0 || f[23] is not ("password" or "key")) return "INVALID_IMPORT";
            _ = Importer.Port(f[17]); if (f[23] == "key" && Importer.Decode(f[24]).Length == 0) return "INVALID_IMPORT";
            if (f[19].Length == 0 && f[3].Length == 0) return "INVALID_IMPORT";
            if (f[52].Length > 0) _ = Importer.Port(f[53]);
            if (new[] {3,19,51,52,54,58}.Any(i => f[i].Any(char.IsControl))) return "INVALID_IMPORT";
            if (f[55] == "1" && (!f[56].StartsWith('/') || f[56].Any(char.IsControl))) return "INVALID_IMPORT";
            string payload = Importer.Decode(f[59]);
            if (payload.Length > 8192) return "INVALID_IMPORT";
            // Payloads that need Android's injector semantics stay explicit rather than guessed.
            if (payload.Contains('[') && System.Text.RegularExpressions.Regex.Replace(payload, @"\[(host_port|host|crlf|lf|cr)\]", "").Contains('[')) return "CUSTOM_TRANSPORT_NOT_READY";
            if (f[1] != "ssh" && (new[] {50,55}.Any(i => f[i] == "1") || f[52].Length > 0 || payload.Length > 0)) return "CUSTOM_TRANSPORT_NOT_READY";
        }
        if (f[1] == "vless")
        {
            if (f.Length < 79 || f[64] != "ws" || f[63] is not ("tls" or "none") || !Guid.TryParseExact(f[62], "D", out _)) return "INVALID_IMPORT";
            _ = Importer.Port(f[67]); if (!f[65].StartsWith('/') || f[65].Any(char.IsControl) || f[3].Any(char.IsControl)) return "INVALID_IMPORT";
            if (f[68] == "1" && f[69] is not ("sni_split" or "half" or "multi" or "micro")) return "CUSTOM_TRANSPORT_NOT_READY";
            if (!int.TryParse(f[77], out var mss) || mss > 0) return "CUSTOM_TRANSPORT_NOT_READY";
            if (!int.TryParse(f[70], out var delay) || delay is < 0 or > 500) return "INVALID_IMPORT";
            return "";
        }
        if (f[1] is "naive" or "naive_ssh")
        {
            _ = Importer.Port(f[28]);
            if (f[3].Length == 0 || f[3].Any(char.IsControl) || f[29].Length == 0 || Importer.Decode(f[30]).Length == 0) return "INVALID_IMPORT";
            return "";
        }
        if (IsSlip(f[1]))
        {
            if (Uri.CheckHostName(f[3]) != UriHostNameType.Dns || f[22] != "udp" || f[10] == "1" ||
                f[7] is not ("bbr" or "dcubic") || !int.TryParse(f[6], out int keep) || keep is < 0 or > 65535 || f[4].Split(',').Length > 16)
                return "CUSTOM_TRANSPORT_NOT_READY";
            foreach (var r in f[4].Split(',')) _ = SlipResolver(r);
            return "";
        }
        if (f[1] is "vaydns" or "vaydns_ssh")
        {

            if (f[42] is not ("txt" or "cname" or "a" or "aaaa" or "mx" or "ns" or "srv" or "null" or "caa") || f[41] is not ("0" or "1")) return "INVALID_IMPORT";
            static bool Range(string text, int maximum) => int.TryParse(text, out int n) && n >= 0 && n <= maximum;
            if (!Range(f[43],253) || !Range(f[48],128) || !Range(f[49],8) || !Range(f[45],86400) || !Range(f[46],86400) || !Range(f[47],120000) ||
                !double.TryParse(f[44], System.Globalization.CultureInfo.InvariantCulture,out var rps) || !double.IsFinite(rps) || rps < 0 || rps > 10000)
                return "INVALID_IMPORT";
            if (f[39] != "0") return "CUSTOM_TRANSPORT_NOT_READY";
        }
        if (f[1] != "ssh")
        {
            if (f[3].Length == 0 || f[11].Length == 0 || f[22] is not ("udp" or "tcp" or "tls" or "https")) return "INVALID_IMPORT";
            if (f[22] == "https") { if (!Uri.TryCreate(f[21], UriKind.Absolute, out var u) || u.Scheme != "https") return "INVALID_IMPORT"; }
            else if (f[4].Length == 0 || f[4].Split(',').Length > 16) return "INVALID_IMPORT";
            if (f[60] is not ("fanout" or "roundrobin") || !int.TryParse(f[61], out var spread) || spread is < 1 or > 16) return "INVALID_IMPORT";
        }
        if (f[1].StartsWith("soar", StringComparison.Ordinal))
        {
            if (f.Length < 84 || f[81] is not ("auto" or "on" or "only" or "off")) return "INVALID_IMPORT";
            // This module's own resolver dialect is IP[:port], TCP policy lives in SetTCPMode.
            if (f[22] is not ("udp" or "tcp")) return "CUSTOM_TRANSPORT_NOT_READY";
            // Panel v33 includes account credentials even for Soar direct. Its native
            // session authenticates the server key; SSH variants use the SSH credentials.
        }
        return "";
    }
    public static (string Address, bool Authoritative) SlipResolver(string wire)
    {
        int flag = wire.LastIndexOf(':');
        if (flag < 0 || wire[(flag+1)..] is not ("0" or "1")) throw new SafeError("INVALID_IMPORT");
        int colon = wire[..flag].LastIndexOf(':');
        if (colon < 1 || !System.Net.IPAddress.TryParse(wire[..colon].Trim('[',']'), out var ip)) throw new SafeError("INVALID_IMPORT");
        int port = Importer.Port(wire[(colon+1)..flag]);
        return (new System.Net.IPEndPoint(ip,port).ToString(), wire[(flag+1)..] == "1");
    }
    public static int EffectiveSlipInterval(string[] fields, int? selected)
    {
        if (selected is null) return int.Parse(fields[6]);
        if (selected != 400) throw new SafeError("INVALID_IMPORT");
        return selected.Value;
    }
    public static bool UsesNaive(string raw) => (raw.StartsWith("rovunet://") || raw.StartsWith("slipnet://")) && Importer.Fields(raw)[1] is "naive" or "naive_ssh";
    public static string NaiveConfig(Profile p, int port,int chainHttpPort=0)
    {
        var f = Importer.Fields(p.Raw); string password = Importer.Decode(f[30]);
        string host = f[3].Contains(':') ? "[" + f[3].Trim('[', ']') + "]" : f[3];
        return JsonSerializer.Serialize(new Dictionary<string,object> { ["listen"] = "socks://127.0.0.1:" + port, ["proxy"] = (chainHttpPort>0?"http://127.0.0.1:"+Importer.Port(chainHttpPort.ToString())+",":"")+"https://" + Uri.EscapeDataString(f[29]) + ":" + Uri.EscapeDataString(password) + "@" + host + ":" + Importer.Port(f[28]), ["log"] = "" });
    }
    public static string Config(Profile p, int port, string hostKey, int upstreamPort = 0, int? slipInterval = null,string sshTlsCa="",int chainSocksPort=0)
    {
        var f = Normalize(Importer.Fields(p.Raw)); var error = Validate(f); if (error.Length > 0) throw new SafeError(error);
        if (IsSlip(f[1]) && slipInterval != null) f[6] = EffectiveSlipInterval(f, slipInterval).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string tlsCa=sshTlsCa.Length==0?"":SlipCertificate.ReadPublic(sshTlsCa);
        return JsonSerializer.Serialize(new { fields = f, port, hostKey, upstreamPort, tlsCa, chainSocksPort });
    }
}
