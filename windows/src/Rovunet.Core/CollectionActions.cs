using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
namespace Rovunet.Core;

public static class CollectionActions
{
    public static (string Host,int Port)? TcpTarget(Profile profile)
    {
        if(ProfileEditing.IsNative(profile.Raw))
        {
            var f=TransportPlan.Normalize(Importer.Fields(profile.Raw));
            if(f[1]=="xray") return TcpTarget(Importer.Single(Importer.Decode(f[84])));
            if(ProfileEditing.IsDns(profile)){
                if(!ResolverScan.TransportChoices(f).Contains("tcp") || f[22]!="tcp" && !(ResolverScan.Family(f)=="soar" && f[81]=="only"))return null;
                var ep=DnsProbe.Endpoint(TransportPlan.SlipResolver(f[4].Split(',')[0]).Address);return(ep.Address.ToString(),ep.Port);
            }
            if(f[1] is "naive" or "naive_ssh")return(f[3],Importer.Port(f[28]));
            if(f[1]=="ssh")return f[52].Length>0?(f[52],Importer.Port(f[53])):(f[19].Length>0?f[19]:f[3],Importer.Port(f[17]));
            if(f[1]=="vless")return(f[66].Length>0?f[66]:f[3],Importer.Port(f[67]));
            if(f[1]=="socks5")return(f[3],Importer.Port(f[40]));
        }
        if(!profile.Raw.StartsWith("vless://",StringComparison.OrdinalIgnoreCase) && !profile.Raw.StartsWith("socks5://",StringComparison.OrdinalIgnoreCase))return null;
        if(Uri.TryCreate(profile.Raw,UriKind.Absolute,out var uri))return(uri.IdnHost.Trim('[',']'),uri.Port>0?uri.Port:profile.Protocol=="SOCKS5"?1080:443);
        return null;
    }
    public static async Task<long?> Tcp(Profile p,CancellationToken stop)
    {
        var target=TcpTarget(p); if(target==null)return null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var client=new TcpClient();var watch=Stopwatch.StartNew();await client.ConnectAsync(target.Value.Host,target.Value.Port,timeout.Token);return watch.ElapsedMilliseconds;
    }
    public static string DuplicateKey(Profile p)
    {
        if(!ProfileEditing.IsNative(p.Raw))return Importer.Fingerprint(p.Raw);
        var f=Importer.Decode(p.Raw[(p.Raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
        f[2]="";if(f.Length>=87){f[85]="";f[86]="";}
        return Importer.Fingerprint(string.Join('|',f));
    }
    public static IEnumerable<List<Profile>> SuggestedGroups(IEnumerable<Profile> profiles)
    {
        // Subscription/account boundaries and explicit v32+ metadata always win.
        var candidates=profiles.Where(p=>p.SourceId.Length==0 && p.CollectionId.StartsWith("manual:",StringComparison.Ordinal) && ProfileEditing.IsNative(p.Raw))
            .Select(p=>(p,f:TransportPlan.Normalize(Importer.Fields(p.Raw))))
            .Select(x=>{
                var users=new[]{x.f[12],x.f[15],x.f[29]}.Where(v=>!string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToList();
                string server=x.f[19].TrimEnd('.').ToLowerInvariant();
                if(server.Length==0 || server.Equals("localhost") || IPAddress.TryParse(server,out var ip) && IPAddress.IsLoopback(ip))server=x.f[3].TrimEnd('.').ToLowerInvariant();
                return(p:x.p,user:users.Count==1?users[0]:"",server);
            })
            .Where(x=>x.user.Length>0 && x.server.Length>0 && x.server!="localhost" && (!IPAddress.TryParse(x.server,out var ip)||!IPAddress.IsLoopback(ip)));
        return candidates.GroupBy(x=>(x.user,x.server)).Where(g=>g.Count()>1).Select(g=>g.Select(x=>x.p).ToList());
    }
}
