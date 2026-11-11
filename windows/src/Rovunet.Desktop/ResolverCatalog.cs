using System.Net;
using System.Buffers.Binary;
using Rovunet.Core;
namespace Rovunet.Desktop;

internal sealed record ResolverBank(string File,string LabelKey,bool Networks);
internal static class ResolverCatalog
{
    internal static readonly ResolverBank[] Banks=[new("resolvers.txt","SCAN_SOURCE_APP",false),new("ir_dns_lite.cidr","SCAN_IR_LITE",true),new("ir_dns.cidr","SCAN_IR_FULL",true),new("ir.cidr","DNS_BANK_IR",true),new("ru.cidr","DNS_BANK_RU",true),new("cn.cidr","DNS_BANK_CN",true)];
    static readonly Dictionary<string,string[]> cache=[];
    internal static string[] Lines(string file)
    {
        if(!Banks.Any(b=>b.File==file))throw new SafeError("DNS_BANK_UNAVAILABLE");
        if(cache.TryGetValue(file,out var lines))return lines;
        using var stream=typeof(ResolverCatalog).Assembly.GetManifestResourceStream("Rovunet.ResolverData."+file)??throw new SafeError("DNS_BANK_UNAVAILABLE");using var reader=new StreamReader(stream);
        var bank=Banks.Single(b=>b.File==file);
        lines=reader.ReadToEnd().Split('\n').Select(l=>l.Split('#')[0].Trim()).Where(l=>l.Length>0).Select(l=>bank.Networks?CanonicalNetwork(l):l).Distinct(StringComparer.Ordinal).ToArray();
        if(lines.Length==0)throw new SafeError("DNS_BANK_UNAVAILABLE");cache[file]=lines;return lines;
    }
    static string CanonicalNetwork(string value)
    {
        var parts=value.Split('/');if(parts.Length!=2||!IPAddress.TryParse(parts[0],out var address)||address.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork||!int.TryParse(parts[1],out int prefix)||prefix is <0 or >32)throw new SafeError("INVALID_RANGE");
        uint first=BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());ulong size=1UL<<(32-prefix);byte[] bytes=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(bytes,(uint)(first&~(size-1)));return new IPAddress(bytes)+"/"+prefix;
    }
    internal static int Count(ResolverBank bank)=>bank.Networks?Lines(bank.File).Length:Lines(bank.File).Count(l=>IPAddress.TryParse(l,out _));
    internal static IReadOnlyList<string> TestCandidates(string file,string mode,int limit) => file=="resolvers.txt"?AllAddresses(file,mode):Candidates(file,mode,limit);
    internal static IReadOnlyList<string> Candidates(string file,string mode,int limit)
    {
        var bank=Banks.SingleOrDefault(b=>b.File==file)??throw new SafeError("DNS_BANK_UNAVAILABLE");
        return ResolverScan.Catalog(Lines(file),mode,limit,bank.Networks);
    }
    internal static IReadOnlyList<string> AllAddresses(string file,string mode)
    {
        var bank=Banks.SingleOrDefault(b=>b.File==file)??throw new SafeError("DNS_BANK_UNAVAILABLE");
        // Country ranges remain sampled: never expand CIDR or a /0 for the UI.
        if(bank.Networks)throw new SafeError("DNS_SCAN_LIMIT");
        var addresses=Lines(file).Where(value=>IPAddress.TryParse(value,out _)).Select(value=>ResolverScan.Endpoint(value,mode)).Distinct(StringComparer.Ordinal).ToArray();
        if(addresses.Length is <1 or >100000)throw new SafeError("DNS_SCAN_LIMIT");return addresses;
    }
}
