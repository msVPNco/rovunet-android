using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using Rovunet.Core;

namespace Rovunet.Platform;
public static class PrivateStore
{
    // Win32 UI_FORBIDDEN is 0x1; LOCAL_MACHINE is 0x4 and is intentionally absent.
    const int CryptProtectUiForbidden=0x1;
    const int MaxBytes=32*1024*1024;
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rovunet");
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true)] static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    static byte[] Crypt(byte[] data, bool protect)
    {
        var input = new Blob { Size = data.Length, Data = Marshal.AllocHGlobal(data.Length) }; Marshal.Copy(data, 0, input.Data, data.Length);
        Blob output = default;
        try
        {
            bool ok = protect ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);
            if (!ok) throw new SafeError("PRIVATE_STORE_FAILED");
            var bytes = new byte[output.Size]; Marshal.Copy(output.Data, bytes, 0, bytes.Length); return bytes;
        }
        finally { for (int i = 0; i < input.Size; i++) Marshal.WriteByte(input.Data, i, 0); Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) {for(int i=0;i<output.Size;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);} }
    }
    static string StorePath(string directory,string name)
    {
        if(!Path.IsPathFullyQualified(directory)||string.IsNullOrWhiteSpace(name)||name.Length>128||name is "." or ".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Path.GetFileName(name)!=name)throw new SafeError("PRIVATE_STORE_FAILED");
        return Path.Combine(Path.GetFullPath(directory),name);
    }
    public static void Save<T>(string name,T value)=>SaveIn(Root,name,value);
    // The explicit directory permits independent Windows fixtures without an
    // environment-variable override of the real user's store.
    public static void SaveIn<T>(string directory,string name,T value)
    {
        var path=StorePath(directory,name);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            Directory.CreateDirectory(directory);byte[] plain=JsonSerializer.SerializeToUtf8Bytes(value);byte[] encrypted;
            try{if(plain.Length>MaxBytes-4096)throw new SafeError("PRIVATE_STORE_FAILED");encrypted=Crypt(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}
            var sid=WindowsIdentity.GetCurrent().User!;var acl=new FileSecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(sid);acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
            using(var file=new FileInfo(temp).Create(FileMode.CreateNew,FileSystemRights.Read|FileSystemRights.Write|FileSystemRights.Delete,FileShare.None,4096,FileOptions.WriteThrough,acl)){file.Write(encrypted);file.Flush(true);}
            File.Move(temp,path,true);
        }catch(SafeError){throw;}catch{throw new SafeError("PRIVATE_STORE_FAILED");}
        finally{try{if(File.Exists(temp))File.Delete(temp);}catch{/* Encrypted, owner-only transient; never delete the destination on failure. */}}
    }
    public static T? Load<T>(string name)=>LoadFrom<T>(Root,name);
    public static T? LoadFrom<T>(string directory,string name)
    {
        var path=StorePath(directory,name);if(!File.Exists(path))return default;
        byte[]? plain=null;
        try{
            using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);if(file.Length>MaxBytes||file.Length==0)throw new SafeError("PRIVATE_STORE_FAILED");
            var encrypted=new byte[(int)file.Length];file.ReadExactly(encrypted);plain=Crypt(encrypted,false);var value=JsonSerializer.Deserialize<T>(plain);if(value is null)throw new SafeError("PRIVATE_STORE_FAILED");return value;
        }catch(SafeError){throw;}catch{throw new SafeError("PRIVATE_STORE_FAILED");}finally{if(plain!=null)CryptographicOperations.ZeroMemory(plain);}
    }
}

public sealed class LeaseStore : ILeaseStore
{
    const string Name = "proxy-recovery.dpapi";
    public ProxyJournal? Read() => PrivateStore.Load<ProxyJournal>(Name);
    public void Write(ProxyJournal journal) => PrivateStore.Save(Name, journal);
    public void Delete() => File.Delete(Path.Combine(PrivateStore.Root, Name));
}

public sealed class UserData
{
    public List<Profile> Profiles { get; set; } = [];
    public List<Subscription> Subscriptions { get; set; } = [];
    public Dictionary<string,Status2Authority> Status2Authorities {get;set;}=[];
    public string Language { get; set; } = "fa";
    public string SelectedId { get; set; } = "";
    public List<string> RecentProfileIds { get; set; } = [];
    public HashSet<string> PinnedProfileIds { get; set; } = [];
    public HashSet<string> FastSlipProfiles { get; set; } = [];
    public Dictionary<string,string> SlipCertificates { get; set; } = [];
    public Dictionary<string,string> SshKeys { get; set; } = [];
    public Dictionary<string,string> SshKeyScopes { get; set; } = [];
    public Dictionary<string,string> SshTlsCertificates { get; set; } = [];
    public bool UseSystemProxy { get; set; } = true;
    public int LocalHttpPort {get;set;}=10809;
    public int LocalSocksPort {get;set;}=10808;
    public bool CloseToTray { get; set; } = true;
    public int SleepTimerMinutes { get; set; }
    public bool GlobalResolverEnabled { get; set; }
    public string GlobalResolverList { get; set; } = "";
    public bool DnsPoolEnabled { get; set; }
    public string DnsPoolList { get; set; } = "";
    public bool DnsPoolVerify { get; set; }
    public string Theme { get; set; } = "system";
    public string MarqueeMotion { get; set; } = "on";
    public Dictionary<string,ScanOptions> ResolverOptions { get; set; } = [];
    public Dictionary<string,ScannerViewOptions> ScannerViews {get;set;}=[];
    public Dictionary<string,GroupChange> RegroupUndo { get; set; }=[];
    public Dictionary<string,ScanHistory> ResolverHistory { get; set; } = [];
    public bool IncludeExperimental { get; set; } = true;
    public bool LiveLatency { get; set; } = true;
    public int LiveLatencyRevision { get; set; }
    public bool ProbeFallbacks { get; set; } = true;
    public long RecordedUpload { get; set; }
    public long RecordedDownload { get; set; }
    public string LatencyUrl { get; set; } = "https://www.gstatic.com/generate_204";
    public Dictionary<string,string> LocalOverrideSources { get; set; } = [];
    public Dictionary<string,string> LocalOverrideNames { get; set; } = [];
    public Dictionary<string, bool> ExpandedCollections { get; set; } = [];
    public string? OpenCollectionId { get; set; }
    public Dictionary<string, DateTimeOffset> SubscriptionUpdated { get; set; } = [];
    public Dictionary<string, PathMeasurement> Measurements { get; set; } = [];
    public Dictionary<string, DateTimeOffset> CollectionTestStarted { get; set; } = [];
    public double WindowWidth { get; set; } = 480;
    public bool PortraitShell { get; set; }
    public double WindowHeight { get; set; } = 900;
}

public sealed class PathMeasurement
{
    public bool ProbeFallbackUsed { get; set; }
    public bool Verified { get; set; }
    public long ElapsedMs { get; set; }
    public string Error { get; set; } = "";
    public DateTimeOffset SampledAt { get; set; }
    public int Attempts { get; set; }
    public int Passes { get; set; }
    public string Stage { get; set; } = "";
}

public sealed class ScanOptions
{
    public int Port { get; set; }
    public bool Shuffle { get; set; } = true;
    public bool Nearby { get; set; }
    public bool WholeBank { get; set; }
    public int E2eTimeout { get; set; } = 15000;
    public int E2eConcurrency { get; set; } = 6;
    public bool FullVerification {get;set;}
    public string Mode { get; set; } = "simple";
    public string Transport { get; set; } = "";
    public string Source { get; set; } = "profile";
    public int Limit { get; set; } = 128;
    public int Workers { get; set; } = 50;
    public int Timeout { get; set; } = 3000;
    public int Attempts { get; set; } = 5;
    public int Threshold { get; set; } = 2;
    public int Size { get; set; }
    public bool Prefilter { get; set; }
}
public sealed class ScannerViewOptions
{
    public string Sort {get;set;}="order";
    public int Score {get;set;}=2;
    public string[] Probes {get;set;}=[];
    public string Status {get;set;}="passed";
    public string Transport {get;set;}="all";
    public int PrismMinimum {get;set;}
}
public sealed class ScanHistory
{
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset Time { get; set; }
    public List<SavedResolver> Entries { get; set; } = [];
}
public sealed class SavedResolver
{
    public string Address { get; set; } = "";
    public string Transport { get; set; } = "";
}

public sealed class GroupChange
{
    public string Previous { get; set; }="";
    public string Name { get; set; }="";
    public string Group { get; set; }="";
}
