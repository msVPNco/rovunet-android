using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Diagnostics;
using System.Globalization;
namespace Rovunet.Platform;
// Naive accepts a config filename. Only the random path appears in argv.
// Owner-only ACL is installed at creation. Chromium does not open config files
// with FILE_SHARE_DELETE, so a DeleteOnClose handle prevents it reading them.
// Explicit deletion follows startup (and every failure/disposal path).
public sealed class EphemeralConfig : IDisposable
{
    readonly FileStream stream;
    public string Path { get; }
    public EphemeralConfig(string text)
    {
        Directory.CreateDirectory(PrivateStore.Root);
        CleanupAbandoned();using var process=Process.GetCurrentProcess();
        Path = System.IO.Path.Combine(PrivateStore.Root, "runtime-" + process.Id + "-" + process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".json");
        var acl = new FileSecurity(); acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(WindowsIdentity.GetCurrent().User!);
        acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
        stream = new FileInfo(Path).Create(FileMode.CreateNew, FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete, FileShare.Read | FileShare.Delete, 4096, FileOptions.WriteThrough, acl);
        var bytes = Encoding.UTF8.GetBytes(text); try { stream.Write(bytes); stream.Flush(true);stream.Dispose(); } catch { Dispose(); throw; } finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    public void Dispose() {stream.Dispose();File.Delete(Path);}
    static void CleanupAbandoned()
    {
        // Only our new, process-stamped transient files. Never touch profiles,
        // journals, legacy filenames, or a live Agent's configuration.
        foreach(string file in Directory.EnumerateFiles(PrivateStore.Root,"runtime-*.json"))try{
            var parts=System.IO.Path.GetFileNameWithoutExtension(file).Split('-');
            if(parts.Length!=4||!int.TryParse(parts[1],out int pid)||!long.TryParse(parts[2],out long stamp)||!Guid.TryParseExact(parts[3],"N",out _))continue;
            bool active;try{using var owner=Process.GetProcessById(pid);active=owner.StartTime.ToUniversalTime().Ticks==stamp;}catch(ArgumentException){active=false;}
            if(active)continue;
            var ownerSid=new FileInfo(file).GetAccessControl().GetOwner(typeof(SecurityIdentifier));if(ownerSid?.Equals(WindowsIdentity.GetCurrent().User)!=true)continue;
            if(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))!=System.IO.Path.GetFullPath(PrivateStore.Root))continue;
            File.Delete(file);
        }catch{ /* A file owned by another/live process is left alone. */ }
    }
}
