using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Rovunet.Core;

public static class ResolverInputs
{
    // Human input accepts CIDR, an inclusive IPv4 range, or individual IP[:port] entries.
    public static IReadOnlyList<string> Input(string text,string mode,int limit,int defaultPort)
    {
        if(defaultPort is <1 or >65535)throw new SafeError("DNS_SCAN_INVALID");
        if(mode=="https")return ResolverScan.FromText(text,mode,limit);
        var values=text.Split('\n').SelectMany(line=>line.Split('#')[0].Split([' ', '\t', '\r', ',', ';'],StringSplitOptions.RemoveEmptyEntries));
        return ResolverScan.FromText(string.Join('\n',values.Select(value=>IPAddress.TryParse(value,out var ip)?new IPEndPoint(ip,defaultPort).ToString():value)),mode,limit);
    }
    public static IReadOnlyList<string> Ranges(string text,string mode,int limit,int defaultPort=53)
    {
        if(defaultPort is <1 or >65535)throw new SafeError("DNS_SCAN_INVALID");
        if(limit is <1 or >ResolverScan.MaximumCandidates||text.Length>Importer.MaxBytes)throw new SafeError("DNS_SCAN_LIMIT");
        var result=new HashSet<string>();
        foreach(var line in text.Split('\n'))foreach(var value in line.Split('#')[0].Split([' ', '\t', '\r', ',', ';'],StringSplitOptions.RemoveEmptyEntries)){
            if(value.Contains('/')){foreach(var sample in ResolverScan.Catalog([value],mode,limit,true)){var endpoint=DnsProbe.Endpoint(sample);result.Add(new IPEndPoint(endpoint.Address,defaultPort).ToString());}}
            else if(value.Contains('-')){
                var ends=value.Split('-');if(ends.Length!=2||!IPAddress.TryParse(ends[0],out var first)||!IPAddress.TryParse(ends[1],out var last)||first.AddressFamily!=AddressFamily.InterNetwork||last.AddressFamily!=AddressFamily.InterNetwork)throw new SafeError("INVALID_RANGE");
                uint start=BinaryPrimitives.ReadUInt32BigEndian(first.GetAddressBytes()),end=BinaryPrimitives.ReadUInt32BigEndian(last.GetAddressBytes());if(end<start)throw new SafeError("INVALID_RANGE");
                ulong length=(ulong)end-start+1;var offsets=new HashSet<long>();for(long i=Math.Max(0,(long)length-limit);i<(long)length;i++){long chosen=Random.Shared.NextInt64(i+1);if(!offsets.Add(chosen))offsets.Add(i);}
                foreach(long offset in offsets){var bytes=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(bytes,(uint)(start+offset));result.Add(new IPEndPoint(new IPAddress(bytes),defaultPort).ToString());}
            }else result.Add(IPAddress.TryParse(value,out var address)?new IPEndPoint(address,defaultPort).ToString():ResolverScan.Endpoint(value,mode));
            if(result.Count>ResolverScan.MaximumCandidates*2)throw new SafeError("DNS_SCAN_LIMIT");
        }
        if(result.Count==0)throw new SafeError("DNS_IP_REQUIRED");var all=result.ToArray();Random.Shared.Shuffle(all);return all.Take(limit).ToArray();
    }
    public static IReadOnlyList<string> Prepare(IEnumerable<string> candidates,string mode,int port,bool shuffle)
    {
        if(port is <0 or >65535)throw new SafeError("DNS_SCAN_INVALID");
        var prepared=candidates.Select(value=>{
            if(mode=="https"||port==0)return value;
            var endpoint=DnsProbe.Endpoint(value);return ResolverScan.Endpoint((endpoint.Address.AddressFamily==AddressFamily.InterNetworkV6?"["+endpoint.Address+"]":endpoint.Address.ToString())+":"+port,mode);
        }).Distinct(StringComparer.Ordinal).ToArray();
        if(shuffle)Random.Shared.Shuffle(prepared);else Array.Sort(prepared,StringComparer.Ordinal);return prepared;
    }
    public static IEnumerable<string> Neighbours(string candidate)
    {
        var ep=DnsProbe.Endpoint(candidate);if(ep.Address.AddressFamily!=AddressFamily.InterNetwork)yield break;
        var bytes=ep.Address.GetAddressBytes();for(int last=1;last<255;last++){bytes[3]=(byte)last;yield return new IPAddress(bytes)+":"+ep.Port;}
    }
}
