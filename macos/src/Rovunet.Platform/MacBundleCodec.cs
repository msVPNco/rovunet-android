using System.Security.Cryptography;
using System.Text;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class MacBundleCodec
{
    public static bool IsBundle(string text)=>BundleCodec.IsBundle(text);
    static byte[] Derive(string password,ReadOnlySpan<byte> salt){if(password.Length is <1 or >4096)throw new SafeError("BUNDLE_PASSWORD_ERROR");return Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);}
    public static string Seal(IEnumerable<Profile> profiles,string password)
    {
        var list=profiles.ToArray();if(list.Length==0)throw new SafeError("INVALID_IMPORT");foreach(var p in list){_=MacProtocols.Single(p.Raw);if(ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[34]!="1")throw new SafeError("SHARING_DISABLED");if(p.Raw.Any(c=>c is '\r' or '\n'))throw new SafeError("BUNDLE_LINE_REQUIRED");}
        byte[] plain=Encoding.UTF8.GetBytes(string.Join('\n',list.Select(p=>p.Raw)));try{if(plain.Length>(Importer.MaxBytes-128)*3/4)throw new SafeError("IMPORT_TOO_LARGE");byte[] result=new byte[45+plain.Length];result[0]=1;RandomNumberGenerator.Fill(result.AsSpan(1,28));byte[] key=Derive(password,result.AsSpan(1,16));try{using var aes=new AesGcm(key,16);aes.Encrypt(result.AsSpan(17,12),plain,result.AsSpan(29,plain.Length),result.AsSpan(29+plain.Length,16));return "rovunet-bundle-enc://"+Convert.ToBase64String(result);}finally{CryptographicOperations.ZeroMemory(key);}}finally{CryptographicOperations.ZeroMemory(plain);}
    }
    public static ImportReview Open(string text,string password)
    {
        if(!IsBundle(text)||text.Length>Importer.MaxBytes)throw new SafeError("INVALID_IMPORT");byte[] payload;try{payload=Convert.FromBase64String(text[(text.IndexOf("://",StringComparison.Ordinal)+3)..]);}catch{throw new SafeError("BUNDLE_PASSWORD_ERROR");}if(payload.Length<45||payload[0]!=1)throw new SafeError("BUNDLE_PASSWORD_ERROR");byte[] key=Derive(password,payload.AsSpan(1,16)),plain=new byte[payload.Length-45];try{using var aes=new AesGcm(key,16);aes.Decrypt(payload.AsSpan(17,12),payload.AsSpan(29,plain.Length),payload.AsSpan(29+plain.Length,16),plain);return ImportReview.Read(new UTF8Encoding(false,true).GetString(plain));}catch(CryptographicException){throw new SafeError("BUNDLE_PASSWORD_ERROR");}catch(DecoderFallbackException){throw new SafeError("BUNDLE_PASSWORD_ERROR");}finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(plain);}
    }
}
