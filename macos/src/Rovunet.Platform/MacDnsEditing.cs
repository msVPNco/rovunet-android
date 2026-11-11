using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed record DnsCredentials(string SshUser,string SshPass,string SocksUser,string SocksPass)
{
    public override string ToString()=>"DnsCredentials(redacted)";
}

public static class MacDnsEditing
{
    public static string DisplayResolvers(string wire)=>string.Join('\n',wire.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(w=>TransportPlan.SlipResolver(w).Address));
    public static string[] NetworkResolvers()
    {
        var native=MacNetworkDns.Read();if(native.Length>0)return native;
        var addresses=new List<IPAddress>();
        foreach(var network in NetworkInterface.GetAllNetworkInterfaces())try{
            if(network.OperationalStatus==OperationalStatus.Up&&network.NetworkInterfaceType is not(NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))addresses.AddRange(network.GetIPProperties().DnsAddresses);
        }catch(NetworkInformationException){}catch(PlatformNotSupportedException){}
        return addresses.Where(a=>!a.Equals(IPAddress.Any)&&!a.Equals(IPAddress.IPv6Any)).Distinct().Take(16).Select(a=>new IPEndPoint(a,53).ToString()).ToArray();
    }
    public static string NormalizeResolvers(string text,string mode,string original="")
    {
        var old=original.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(TransportPlan.SlipResolver).GroupBy(r=>r.Address).ToDictionary(g=>g.Key,g=>g.Any(r=>r.Authoritative));
        string[] values=text.Split(['\n','\r',',',';',' ','\t'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(values.Length is <1 or >16)throw new SafeError("DNS_IP_REQUIRED");
        var rows=new List<string>();
        foreach(string value in values){
            try{var explicitWire=TransportPlan.SlipResolver(value);rows.Add(explicitWire.Address+(explicitWire.Authoritative?":1":":0"));}
            catch(SafeError){string address=ResolverScan.Endpoint(value,mode);rows.Add(address+(old.GetValueOrDefault(address)?":1":":0"));}
        }
        return string.Join(',',rows.Distinct(StringComparer.Ordinal));
    }
    public static Profile Apply(Profile original,IReadOnlyDictionary<int,string> edits,string addresses,string mode,string doh,DnsCredentials? credentials=null)
    {
        if(!ProfileEditing.IsDns(original))throw new SafeError("UNSUPPORTED_FORMAT");
        var f=TransportPlan.Normalize(Importer.Fields(original.Raw));
        string family=ResolverScan.Family(f);
        int[] familyFields=family switch{"slipstream"=>[5,6,7],"dnstt"=>[11,27,39,60,61],"sayedns"=>[11,27,38,39,60,61],"vaydns"=>[11,41,42,43,44,45,46,47,48,49],"soar"=>[11,79,80,81,82,83],_=>throw new SafeError("UNSUPPORTED_FORMAT")};
        if(edits.Keys.Any(i=>i is not(2 or 3 or 17 or 19)&&!familyFields.Contains(i)))throw new SafeError("INVALID_IMPORT");
        if(mode is not("udp" or "tcp" or "tls" or "https")||family=="slipstream"&&mode!="udp"||family=="soar"&&mode is not("udp" or "tcp"))throw new SafeError("CUSTOM_TRANSPORT_NOT_READY");
        if(family!="slipstream"&&!ScanCheckpoint.ValidServerKey(edits.TryGetValue(11,out var changedKey)?changedKey:f[11],family))throw new SafeError(family=="soar"?"MAC_SOAR_KEY_FORMAT":"MAC_DNS_KEY_FORMAT");
        var ordinary=edits.Where(e=>e.Key is not(17 or 19)&&e.Value!=f[e.Key]).ToDictionary();
        if(mode!=f[22])ordinary[22]=mode;
        if(mode=="https"){
            string url=SubscriptionUrls.Validate(doh.Trim()).AbsoluteUri;if(url!=f[21])ordinary[21]=url;
            // An HTTPS resolver has its own URL; an empty IP list is valid.
        }else if(addresses.Trim()!=DisplayResolvers(f[4]).Trim())ordinary[4]=NormalizeResolvers(addresses,mode,f[4]);
        string raw=ProfileEditing.ChangeNative(original,ordinary);
        var wire=Importer.Decode(raw[(raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
        foreach(int index in new[]{17,19})if(edits.TryGetValue(index,out string? value)&&value!=f[index]){
            if(!f[1].EndsWith("_ssh",StringComparison.Ordinal)||value.Contains('|')||value.Any(char.IsControl))throw new SafeError("INVALID_IMPORT");
            wire[index]=index==17?Importer.Port(value).ToString():value;
        }
        if(credentials!=null){
            if(wire[0]!="33"||new[]{credentials.SshUser,credentials.SshPass,credentials.SocksUser,credentials.SocksPass}.Any(v=>v.Length>8192))throw new SafeError("INVALID_IMPORT");
            if(credentials!=new DnsCredentials(f[15],f[16],f[12],f[13]))wire[87]=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{sshUser=credentials.SshUser,sshPass=credentials.SshPass,socksUser=credentials.SocksUser,socksPass=credentials.SocksPass}));
        }
        raw=raw[..(raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',wire)));
        var next=MacProfileEditing.Replace(original,raw);if(!next.CanConnect)throw new SafeError(next.BlockReason);return next;
    }
}
