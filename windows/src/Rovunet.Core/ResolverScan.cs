using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;

namespace Rovunet.Core;

public static class ResolverScan
{
    public const int MaximumCandidates = 100000;
    public static string Family(string[] fields) => fields[1].Split('_')[0] switch { "ss" => "slipstream", var family => family };
    public static string[] TransportChoices(string[] fields)
    {
        if (fields[22] is "tls" or "https" or "tcp") return [fields[22]];
        string family = Family(fields);
        if (family == "slipstream") return ["udp"];
        if (family == "soar") return fields[81] switch { "only" => ["tcp"], "off" => ["udp"], _ => ["both", "udp", "tcp"] };
        return ["udp", "tcp", "both"];
    }
    public static string Endpoint(string value, string mode)
    {
        if (mode == "https") return SubscriptionUrls.Validate(value).AbsoluteUri;
        var ep = DnsProbe.Endpoint(value, mode == "tls" ? 853 : 53);
        if (mode == "tls" && ep.Port == 53) ep.Port = 853; // Matches the native transport's default.
        return ep.ToString();
    }
    public static Profile Candidate(Profile original, string value, string mode, bool forTest)
    {
        var f = TransportPlan.Normalize(Importer.Fields(original.Raw));
        if (!TransportChoices(f).SelectMany(x => x == "both" ? new[] { "udp", "tcp" } : new[] { x }).Contains(mode)) throw new SafeError("CUSTOM_TRANSPORT_NOT_READY");
        var edits = new Dictionary<int,string>();
        string endpoint = Endpoint(value, mode);
        if (mode == "https") edits[21] = endpoint;
        else
        {
            bool authoritative = f[4].Split(',').Any(wire =>
            {
                try { var r = TransportPlan.SlipResolver(wire); return r.Authoritative && Endpoint(r.Address, mode) == endpoint; }
                catch (SafeError) { return false; }
            });
            edits[4] = endpoint + (authoritative ? ":1" : ":0");
        }
        if (Family(f) == "soar")
        {
            // A resolver test must not fall back through a second path or built-in discovery.
            if (forTest) edits[79] = "0";
            edits[81] = mode == "tcp" ? "only" : "off";
        }
        else edits[22] = mode;
        return ProfileEditing.Replace(original, ProfileEditing.ChangeNative(original, edits));
    }
    public static Profile Selected(Profile original,IEnumerable<(string Address,string Transport)> values)
    {
        var selected=values.Distinct().ToArray();
        if(selected.Length==0||selected.Length>16)throw new SafeError("DNS_SCAN_LIMIT");
        if(selected.Select(x=>x.Transport).Distinct().Count()!=1)throw new SafeError("SCAN_SAME_TRANSPORT");
        string mode=selected[0].Transport;
        if(mode=="https"&&selected.Length>1)throw new SafeError("SCAN_DOH_SINGLE");
        var candidates=selected.Select(x=>Candidate(original,x.Address,x.Transport,false)).ToArray();
        if(candidates.Length==1)return candidates[0];
        string resolvers=string.Join(',',candidates.Select(p=>Importer.Fields(p.Raw)[4]).Distinct());
        return ProfileEditing.Replace(original,ProfileEditing.ChangeNative(candidates[0],new Dictionary<int,string>{{4,resolvers}}));
    }
    public static IReadOnlyList<string> FromText(string text, string mode, int limit)
    {
        if (limit is <1 or >MaximumCandidates || text.Length > Importer.MaxBytes) throw new SafeError("DNS_SCAN_LIMIT");
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in text.Split('\n'))
        {
            var content = line.Trim();
            if (content.StartsWith('#') || content.Length == 0) continue;
            foreach (string value in content.Split([' ', '\t', ',', ';', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (value.StartsWith('#')) break;
                selected.Add(Endpoint(value, mode));
                if (selected.Count > limit) throw new SafeError("DNS_SCAN_LIMIT");
            }
        }
        if (selected.Count == 0) throw new SafeError("DNS_IP_REQUIRED");
        return selected.ToList();
    }
    public static IReadOnlyList<string> Catalog(IEnumerable<string> lines, string mode, int limit, bool networks)
    {
        if (limit is <1 or >MaximumCandidates || mode=="https") throw new SafeError("DNS_SCAN_LIMIT");
        var samples = new List<(uint First, ulong End)>();
        var addresses = new HashSet<string>();
        foreach (string line in lines)
        {
            string value = line.Split('#')[0].Trim();
            if (value.Length == 0) continue;
            if (!networks)
            {
                if (IPAddress.TryParse(value,out var ip)) addresses.Add(Endpoint(ip.ToString(),mode));
                continue;
            }
            var parts=value.Split('/');
            if(parts.Length!=2 || !IPAddress.TryParse(parts[0],out var address) || address.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork ||
                !int.TryParse(parts[1],out int prefix) || prefix is <0 or >32) throw new SafeError("INVALID_RANGE");
            uint first=BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
            ulong count=1UL<<(32-prefix);
            uint network=(uint)(first&~(count-1));
            if(first!=network)throw new SafeError("INVALID_RANGE");
            samples.Add((network,network+count));
        }
        if (!networks)
        {
            var all=addresses.ToArray();Random.Shared.Shuffle(all);
            if(all.Length==0) throw new SafeError("DNS_IP_REQUIRED");
            return all.Take(limit).ToArray();
        }
        // Merge overlapping prefixes before sampling. Never expand a whole country or a /0 into memory.
        var union=new List<(uint First,ulong End)>();
        foreach(var next in samples.OrderBy(x=>x.First))
        {
            if(union.Count==0 || union[^1].End<next.First) union.Add(next);
            else if(next.End>union[^1].End) union[^1]=(union[^1].First,next.End);
        }
        if(union.Count==0) throw new SafeError("INVALID_RANGE");
        long available=union.Sum(x=>(long)(x.End-x.First));
        var offsets=new HashSet<long>();
        for(long i=Math.Max(0,available-limit);i<available;i++)
        {
            long choice=Random.Shared.NextInt64(i+1);
            if(!offsets.Add(choice))offsets.Add(i);
        }
        var output=new List<string>(); int range=0; long start=0;
        foreach(long offset in offsets.Order())
        {
            while(offset>=start+(long)(union[range].End-union[range].First))
            {start+=(long)(union[range].End-union[range].First);range++;}
            byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(bytes,(uint)(union[range].First+offset-start));
            output.Add(Endpoint(new IPAddress(bytes).ToString(),mode));
        }
        var mixed=output.ToArray();Random.Shared.Shuffle(mixed);
        return mixed;
    }
}
