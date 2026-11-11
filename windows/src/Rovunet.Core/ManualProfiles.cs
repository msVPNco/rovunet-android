using System.Text;
using System.Text.Json;
namespace Rovunet.Core;
public static class ManualProfiles
{
    public static string Native(string kind,string name,string host,int port,string user,string password)
    {
        if(kind is not("ssh" or "naive")||name.Contains('|')||host.Contains('|')||host.Any(char.IsControl)||Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("INVALID_IMPORT");
        var fields=Enumerable.Repeat("",88).ToArray();fields[0]="33";fields[1]=kind;fields[2]=name;fields[3]=host;
        fields[31]=fields[33]=fields[36]="0";fields[34]="1";
        if(kind=="ssh"){fields[17]=Importer.Port(port.ToString()).ToString();fields[19]=host;fields[23]="password";}
        else{fields[28]=Importer.Port(port.ToString()).ToString();fields[29]=user;fields[30]=Convert.ToBase64String(Encoding.UTF8.GetBytes(password));}
        if(user.Length==0||password.Length==0||user.Contains('|'))throw new SafeError("INVALID_IMPORT");
        fields[87]=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{sshUser=kind=="ssh"?user:"",sshPass=kind=="ssh"?password:"",socksUser="",socksPass=""}));
        string raw="rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',fields)));_=Importer.Single(raw);return raw;
    }
    public static string Dns(string kind,string name,string zone,string endpoints,string transport,string key,
        string sshHost,string sshPort,string username,string password)
    {
        string family=kind.Split('_')[0];bool ssh=kind.EndsWith("_ssh",StringComparison.Ordinal);
        if(family is not ("dnstt" or "sayedns" or "soar" or "vaydns" or "slipstream") ||
            kind!=family && kind!=family+"_ssh" || Uri.CheckHostName(zone)!=UriHostNameType.Dns ||
            name.Length>200 || name.Any(char.IsControl) || name.Contains('|') || key.Contains('|') ||
            transport is not ("udp" or "tcp" or "tls" or "https"))throw new SafeError("INVALID_IMPORT");
        if(family!="slipstream" && key.Trim().Length==0)throw new SafeError("INVALID_IMPORT");
        var f=Enumerable.Repeat("",88).ToArray();
        f[0]="33";f[1]=kind;f[2]=name.Trim();f[3]=zone.Trim();f[11]=key.Trim();
        f[5]=f[10]=f[27]=f[31]=f[33]=f[36]=f[38]=f[39]=f[41]=f[44]=f[48]=f[79]="0";
        f[34]="1";f[6]="5000";f[7]="bbr";f[22]=transport;f[42]="txt";f[43]="101";f[45]="60";f[46]="2";f[47]="500";f[49]="2";f[60]="fanout";f[61]="3";f[81]=transport=="tcp"?"only":"auto";f[82]="soar";
        if(transport=="https")f[21]=SubscriptionUrls.Validate(endpoints.Trim()).AbsoluteUri;
        else f[4]=string.Join(',',ResolverScan.FromText(endpoints,transport,16).Select(x=>x+":0"));
        if(ssh){
            if(username.Length==0 || password.Length==0 || Uri.CheckHostName(sshHost)==UriHostNameType.Unknown)throw new SafeError("INVALID_IMPORT");
            f[19]=sshHost;f[17]=Importer.Port(sshPort).ToString();f[23]="password";
        }
        f[87]=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new{sshUser=ssh?username:"",sshPass=ssh?password:"",socksUser=ssh?"":username,socksPass=ssh?"":password}));
        string raw="rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',f)));
        var parsed=Importer.Single(raw);if(!parsed.CanConnect) throw new SafeError(parsed.BlockReason);
        return raw;
    }
}
