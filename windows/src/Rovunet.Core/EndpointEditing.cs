using System.Text;
namespace Rovunet.Core;

public sealed record EditableEndpoint(string Name,string Host,int Port);
public static class EndpointEditing
{
    public static EditableEndpoint? Read(Profile profile)
    {
        if(ProfileEditing.IsNative(profile.Raw))
        {
            var f=Importer.Fields(profile.Raw);
            if(f[1]=="xray")return Read(Importer.Single(Importer.Decode(f[84]))) is {} inner?inner with{Name=profile.Name}:null;
            return f[1] switch{
                "ssh"=>new(profile.Name,f[19].Length>0?f[19]:f[3],int.Parse(f[17])),
                "naive" or "naive_ssh"=>new(profile.Name,f[3],int.Parse(f[28])),
                "vless"=>new(profile.Name,f[66].Length>0?f[66]:f[3],int.Parse(f[67])),
                "socks5"=>new(profile.Name,f[3],int.Parse(f[40])),_=>null};
        }
        if(Uri.TryCreate(profile.Raw,UriKind.Absolute,out var uri)&&uri.Host.Length>0&&uri.Scheme is "vless" or "socks5" or "hysteria2" or "hy2")return new(profile.Name,uri.Host.Trim('[',']'),uri.Port>0?uri.Port:uri.Scheme=="socks5"?1080:443);
        return null;
    }
    public static string Apply(Profile profile,string name,string host,string port)
    {
        _=Importer.Single(profile.Raw);if(Read(profile)==null)throw new SafeError("UNSUPPORTED_FORMAT");
        host=host.Trim().Trim('[',']');int number=Importer.Port(port);
        if(name.Any(char.IsControl)||name.Length>200||name.Contains('|')||Uri.CheckHostName(host)==UriHostNameType.Unknown)throw new SafeError("INVALID_IMPORT");
        if(ProfileEditing.IsNative(profile.Raw))
        {
            // Preserve the complete credential envelope and every untouched wire option.
            var f=Importer.Decode(profile.Raw[(profile.Raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
            f[2]=name;
            if(f[1]=="xray")f[84]=Convert.ToBase64String(Encoding.UTF8.GetBytes(Apply(Importer.Single(Importer.Decode(f[84])),name,host,port)));
            else{f[f[1]=="ssh"?19:f[1]=="vless"?66:3]=host;f[f[1]=="ssh"?17:f[1]=="vless"?67:f[1]=="socks5"?40:28]=number.ToString(System.Globalization.CultureInfo.InvariantCulture);}
            string raw=profile.Raw[..(profile.Raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',f)));return ProfileEditing.Replace(profile,raw).Raw;
        }
        int start=profile.Raw.IndexOf("://",StringComparison.Ordinal)+3;
        int end=profile.Raw.IndexOfAny(['/','?','#'],start);if(end<0)end=profile.Raw.Length;
        string authority=profile.Raw[start..end];int at=authority.LastIndexOf('@');string credentials=at>=0?authority[..(at+1)]:"";
        string tail=profile.Raw[end..];int hash=tail.IndexOf('#');if(hash>=0)tail=tail[..hash];
        string next=profile.Raw[..start]+credentials+(host.Contains(':')?"["+host+"]":host)+":"+number+tail+"#"+Uri.EscapeDataString(name);
        return ProfileEditing.Replace(profile,next).Raw;
    }
}
