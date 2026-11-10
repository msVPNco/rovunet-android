using System.Security.Cryptography;
using System.Text;

namespace Rovunet.Core;

public static class BundleCodec
{
    const string Prefix = "rovunet-bundle-enc://";
    public static bool IsBundle(string text) => text.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) || text.StartsWith("slipnet-bundle-enc://",StringComparison.OrdinalIgnoreCase);
    static byte[] Derive(string password,ReadOnlySpan<byte> salt)
    {
        if(password.Length is <1 or >4096)throw new SafeError("BUNDLE_PASSWORD_ERROR");
        return Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);
    }
    public static string Seal(IEnumerable<Profile> profiles,string password)
    {
        var list=profiles.ToList();
        if(list.Count==0)throw new SafeError("INVALID_IMPORT");
        foreach(var p in list)
        {
            _=Importer.Single(p.Raw);
            if(ProfileEditing.IsNative(p.Raw) && Importer.Fields(p.Raw)[34]!="1")throw new SafeError("SHARING_DISABLED");
            // A WireGuard INI contains newlines; Android's bundle payload is one URI per line.
            if(p.Raw.Any(c=>c=='\r'||c=='\n'))throw new SafeError("BUNDLE_LINE_REQUIRED");
        }
        byte[] plaintext=Encoding.UTF8.GetBytes(string.Join('\n',list.Select(p=>p.Raw)));
        try{
            if(plaintext.Length>(Importer.MaxBytes-128)*3/4) throw new SafeError("IMPORT_TOO_LARGE");
            byte[] result=new byte[45+plaintext.Length];result[0]=1;
            RandomNumberGenerator.Fill(result.AsSpan(1,28));
            byte[] key=Derive(password,result.AsSpan(1,16));
            try{using var aes=new AesGcm(key,16);aes.Encrypt(result.AsSpan(17,12),plaintext,result.AsSpan(29,plaintext.Length),result.AsSpan(29+plaintext.Length,16));return Prefix+Convert.ToBase64String(result);}
            finally{CryptographicOperations.ZeroMemory(key);}
        }finally{CryptographicOperations.ZeroMemory(plaintext);}
    }
    public static CompatibleImport Open(string text,string password,bool experimental)
    {
        if(!IsBundle(text) || text.Length>Importer.MaxBytes)throw new SafeError("INVALID_IMPORT");
        byte[] payload;
        try{payload=Convert.FromBase64String(text[(text.IndexOf("://",StringComparison.Ordinal)+3)..]);}catch(FormatException){throw new SafeError("BUNDLE_PASSWORD_ERROR");}
        if(payload.Length<45 || payload[0]!=1)throw new SafeError("BUNDLE_PASSWORD_ERROR");
        byte[] key=Derive(password,payload.AsSpan(1,16)); byte[] plaintext=new byte[payload.Length-45];
        try{
            using var aes=new AesGcm(key,16);
            aes.Decrypt(payload.AsSpan(17,12),payload.AsSpan(29,plaintext.Length),payload.AsSpan(29+plaintext.Length,16),plaintext);
            // ReadCompatible rejects protected items atomically and keeps the original v33 envelope.
            return Importer.ReadCompatible(new UTF8Encoding(false,true).GetString(plaintext),experimental);
        }
        catch(CryptographicException){throw new SafeError("BUNDLE_PASSWORD_ERROR");}
        catch(DecoderFallbackException){throw new SafeError("BUNDLE_PASSWORD_ERROR");}
        finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(plaintext);}
    }
}
