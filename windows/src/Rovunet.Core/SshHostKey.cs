using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace Rovunet.Core;

public static class SshHostKey
{
    public static bool Required(Profile p){try{return ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[1] is var kind&&(kind=="ssh"||kind.EndsWith("_ssh",StringComparison.Ordinal));}catch(SafeError){return false;}}
    public static string Scope(Profile p){if(!Required(p))return "";var f=Importer.Fields(p.Raw);return (f[19].Length>0?f[19]:f[3]).TrimEnd('.').ToLowerInvariant()+":"+Importer.Port(f[17]);}
    public static bool UsesTls(Profile p){if(!Required(p))return false;var f=Importer.Fields(p.Raw);return f[1]=="ssh"&&(f[50]=="1"||f[55]=="1"&&f[57]=="1");}
    public static string TlsScope(Profile p){if(!UsesTls(p))return "";var f=Importer.Fields(p.Raw);return Scope(p)+"/"+(f[51].Length>0?f[51]:(f[19].Length>0?f[19]:f[3])).TrimEnd('.').ToLowerInvariant();}
    // Accept OpenSSH public lines and the SSH wire blob returned by the helper.
    // Fingerprints alone cannot be used as the helper's key pin.
    public static string Normalize(string text)
    {
        text=text.Trim();if(text.Length==0)return "";
        try{
            if(text.Length>16384||text.Contains("PRIVATE KEY",StringComparison.Ordinal)||text.Contains('\n')||text.Contains('\r'))throw new FormatException();
            var parts=text.Split([' ','\t'],StringSplitOptions.RemoveEmptyEntries);string? declared=parts.Length>1?parts[0]:null;
            byte[] wire=Convert.FromBase64String(parts.Length>1?parts[1]:parts[0]);int offset=0;
            byte[] Field(){if(offset+4>wire.Length)throw new FormatException();int n=BinaryPrimitives.ReadInt32BigEndian(wire.AsSpan(offset,4));offset+=4;if(n<1||n>wire.Length-offset)throw new FormatException();byte[] value=wire.AsSpan(offset,n).ToArray();offset+=n;return value;}
            string algorithm=Encoding.ASCII.GetString(Field());if(declared!=null&&declared!=algorithm)throw new FormatException();
            switch(algorithm){
                case "ssh-ed25519":if(Field().Length!=32)throw new FormatException();break;
                case "ssh-rsa":if(Field().Length>8||Field().Length<256)throw new FormatException();break;
                case "ecdsa-sha2-nistp256":case "ecdsa-sha2-nistp384":case "ecdsa-sha2-nistp521":
                    string curve=Encoding.ASCII.GetString(Field());var point=Field();int size=curve switch{"nistp256"=>65,"nistp384"=>97,"nistp521"=>133,_=>0};if(algorithm!="ecdsa-sha2-"+curve||point.Length!=size||point[0]!=4)throw new FormatException();break;
                default:throw new FormatException();
            }
            if(offset!=wire.Length)throw new FormatException();return Convert.ToBase64String(wire);
        }catch{throw new SafeError("SSH_KEY_INVALID");}
    }
    public static string Fingerprint(string normalized)=>"SHA256:"+Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(Normalize(normalized)))).TrimEnd('=');
}
