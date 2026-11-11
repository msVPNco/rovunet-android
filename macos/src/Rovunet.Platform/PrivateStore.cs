using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Platform;

public interface ISecretVault { byte[] GetOrCreateKey(); }

// The complete profile document is encrypted. The random key stays in Keychain.
public sealed class PrivateStore(string root, ISecretVault vault)
{
    // A scan checkpoint is a document containing many results, not one import.
    // Individual profile/subscription input retains Importer.MaxBytes limits.
    const int MaximumDocumentBytes=16*1024*1024;
    public static bool MayInitializeKey(string root) =>
        !File.Exists(Path.Combine(root,"profiles.enc"))&&!File.Exists(Path.Combine(root,"system-proxy","profiles.enc"));
    public byte[] AcquireSessionKey()=>vault.GetOrCreateKey();
    public static string Root => OperatingSystem.IsMacOS()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Rovunet-macOS-dev")
        : throw new SafeError("MACOS_KEYCHAIN_REQUIRED");
    public T Load<T>() where T : new()
    {
        var path = Path.Combine(root, "profiles.enc");
        if (!File.Exists(path)) return new();
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length is < 29 or > MaximumDocumentBytes + 29) throw new SafeError("PRIVATE_STORE_FAILED");
        byte[] key = vault.GetOrCreateKey();
        byte[] plain = new byte[bytes.Length - 28];
        try {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(0,12), bytes.AsSpan(28), bytes.AsSpan(12,16), plain, "Rovunet-macOS-v1"u8);
            return JsonSerializer.Deserialize<T>(plain) ?? throw new SafeError("PRIVATE_STORE_FAILED");
        } catch { throw new SafeError("PRIVATE_STORE_FAILED"); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public void Save<T>(T value)
    {
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new SafeError("PRIVATE_STORE_FAILED");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(value); byte[]? key=null;
        string temp = Path.Combine(root, ".profiles-" + Guid.NewGuid().ToString("N"));
        try {
            key=vault.GetOrCreateKey();
            if (plain.Length > MaximumDocumentBytes) throw new SafeError("IMPORT_TOO_LARGE");
            byte[] bytes = new byte[plain.Length + 28]; RandomNumberGenerator.Fill(bytes.AsSpan(0,12));
            using var aes = new AesGcm(key,16);
            aes.Encrypt(bytes.AsSpan(0,12),plain,bytes.AsSpan(28),bytes.AsSpan(12,16),"Rovunet-macOS-v1"u8);
            var options = new FileStreamOptions { Mode=FileMode.CreateNew, Access=FileAccess.Write, Share=FileShare.None, Options=FileOptions.WriteThrough };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temp, Path.Combine(root,"profiles.enc"),true);
        } catch(SafeError) { throw; } catch { throw new SafeError("PRIVATE_STORE_FAILED"); }
        finally { if(key!=null)CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); if(File.Exists(temp))File.Delete(temp); }
    }
}

public sealed class EphemeralConfig : IDisposable
{
    public string Path { get; }
    public EphemeralConfig(string text)
    {
        string root = OperatingSystem.IsMacOS() ? PrivateStore.Root : System.IO.Path.GetTempPath();
        Directory.CreateDirectory(root);
        CleanupAbandoned(root);
        using var owner=System.Diagnostics.Process.GetCurrentProcess();
        Path = System.IO.Path.Combine(root, "runtime-"+owner.Id+"-"+owner.StartTime.ToUniversalTime().Ticks+"-" + Guid.NewGuid().ToString("N")+".json");
        var options = new FileStreamOptions { Mode=FileMode.CreateNew, Access=FileAccess.Write, Share=FileShare.Read };
        if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        using var file = new FileStream(Path,options); using var writer = new StreamWriter(file); writer.Write(text);
    }
    public void Dispose() { if(File.Exists(Path))File.Delete(Path); }
    static void CleanupAbandoned(string root)
    {
        foreach(string file in Directory.EnumerateFiles(root,"runtime-*.json"))try{
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
            var parts=System.IO.Path.GetFileNameWithoutExtension(file).Split('-');
            if(parts.Length!=4||!int.TryParse(parts[1],out int pid)||!long.TryParse(parts[2],out long stamp)||!Guid.TryParseExact(parts[3],"N",out _))continue;
            bool alive;try{using var process=System.Diagnostics.Process.GetProcessById(pid);alive=process.StartTime.ToUniversalTime().Ticks==stamp;}catch(ArgumentException){alive=false;}
            if(!alive&&System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))==System.IO.Path.GetFullPath(root))File.Delete(file);
        }catch{} // Another/live process and legacy files are left alone.
    }
}
