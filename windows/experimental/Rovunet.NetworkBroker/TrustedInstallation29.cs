using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;
internal static class TrustedInstallation29
{
    public const string XrayHash="d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e";
    public const string WintunHash="e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce";
    static readonly string[] WritableAuthorities=["S-1-5-18","S-1-5-32-544","S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"];
    static readonly FileSystemRights Writes=FileSystemRights.WriteData|FileSystemRights.AppendData|FileSystemRights.WriteAttributes|FileSystemRights.WriteExtendedAttributes|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
    public static bool IsAdmin=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public static void RequireAdmin(){if(!IsAdmin)throw new SafeError("BROKER_ADMIN_REQUIRED");}
    public static void ProtectedAcl(FileSystemInfo item)
    {
        if((item.Attributes&FileAttributes.ReparsePoint)!=0)throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        FileSystemSecurity security=item is DirectoryInfo directory?directory.GetAccessControl():((FileInfo)item).GetAccessControl();
        if(new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(),0).DiscretionaryAcl==null)throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        if(!WritableAuthorities.Contains(security.GetOwner(typeof(SecurityIdentifier))?.Value??""))throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier)))if((rule.PropagationFlags&PropagationFlags.InheritOnly)==0&&rule.AccessControlType==AccessControlType.Allow&&(rule.FileSystemRights&Writes)!=0&&!WritableAuthorities.Contains(rule.IdentityReference.Value))throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
    }
    public static string Root()
    {
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));string program=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        if(!root.StartsWith(program+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new SafeError("BROKER_PROGRAM_FILES_REQUIRED");
        for(var directory=new DirectoryInfo(root);directory!=null;directory=directory.Parent){ProtectedAcl(directory);if(directory.FullName.Equals(program,StringComparison.OrdinalIgnoreCase))break;}
        return root;
    }
    public static void RequireLab(string root)
    {
        string path=Path.Combine(root,"NETWORK-LAB-ACCEPTANCE.json");ProtectedAcl(new FileInfo(path));
        if(new FileInfo(path).Length>4096)throw new SafeError("BROKER_ISOLATED_LAB_REQUIRED");using var document=JsonDocument.Parse(File.ReadAllBytes(path));var value=document.RootElement;
        string machine=Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography","MachineGuid","") as string??"";
        if(value.GetProperty("purpose").GetString()!="isolated-windows-network-test"||!value.GetProperty("testOnly").GetBoolean()||machine.Length==0||value.GetProperty("machine").GetString()!=machine)throw new SafeError("BROKER_ISOLATED_LAB_REQUIRED");
    }
    public static Dictionary<string,string> Manifest(string root)
    {
        string path=Path.Combine(root,"NETWORK-LAB-MANIFEST.json");ProtectedAcl(new FileInfo(path));if(new FileInfo(path).Length>262144)throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllBytes(path))??throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        if(hashes.Count is <4 or >2048)throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        foreach(var (name,expected) in hashes){string file=Path.GetFullPath(Path.Combine(root,name));if(!file.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||expected.Length!=64)throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");ProtectedAcl(new FileInfo(file));for(var parent=new DirectoryInfo(Path.GetDirectoryName(file)!);!parent.FullName.Equals(root,StringComparison.OrdinalIgnoreCase);parent=parent.Parent??throw new SafeError("BROKER_UNTRUSTED_INSTALLATION"))ProtectedAcl(parent);using var stream=File.OpenRead(file);if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");}
        foreach(string name in new[]{"Rovunet.NetworkBroker.exe","Rovunet.NetworkLab.exe","engines/xray.exe","engines/wintun.dll"}){
            string path2=Path.Combine(root,name);ProtectedAcl(new FileInfo(path2));if(!hashes.TryGetValue(name,out var expected)||expected.Length!=64||!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path2))).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
        }
        if(hashes["engines/xray.exe"]!=XrayHash||hashes["engines/wintun.dll"]!=WintunHash)throw new SafeError("ENGINE_HASH_MISMATCH");return hashes;
    }
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint rights,out IntPtr token);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    public static string Sid(Process process){if(!OpenProcessToken(process.Handle,8,out var token))throw new SafeError("BROKER_CLIENT_REJECTED");try{return new WindowsIdentity(token).User!.Value;}finally{CloseHandle(token);}}
    public static string Parent(int processId,string root,Dictionary<string,string> hashes)
    {
        using var parent=Process.GetProcessById(processId);string path=parent.MainModule?.FileName??"";string expected=Path.Combine(root,"Rovunet.NetworkLab.exe");
        if(!path.Equals(expected,StringComparison.OrdinalIgnoreCase)||parent.HasExited||Sid(parent)!=WindowsIdentity.GetCurrent().User!.Value||!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(hashes["Rovunet.NetworkLab.exe"],StringComparison.OrdinalIgnoreCase))throw new SafeError("BROKER_CLIENT_REJECTED");return Sid(parent);
    }
}
internal sealed class NetworkJournal29
{
    public int Version{get;set;}=1;
    public Guid Session{get;set;}
    public string Owner{get;set;}="";
    public bool KillSwitch{get;set;}
    public bool EnableUdp{get;set;}
    public string Phase{get;set;}="preparing";
    public Guid[] FilterKeys{get;set;}=[];
    public Guid[] ProtectionKeys{get;set;}=[];
    public List<OwnedRoute29> ConfirmedRoutes{get;set;}=[];
    public OwnedRoute29? PendingRoute{get;set;}
    public Guid InterfaceGuid{get;set;}
    public ulong InterfaceLuid{get;set;}
    public int EnginePid{get;set;}
    public long EngineStartedUtcTicks{get;set;}
}
internal sealed class AdapterReceipt30
{
    public int Version{get;set;}=1;
    public Guid InterfaceGuid{get;set;}
    public string Name{get;set;}="";
    public string Root{get;set;}="";
    public string Machine{get;set;}="";
}
internal sealed class ProtectedJournal29
{
    readonly string directory;
    string Pathname=>Path.Combine(directory,"session.json");
    public ProtectedJournal29()
    {
        TrustedInstallation29.RequireAdmin();directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Rovunet-Network-Lab");
        if(!Directory.Exists(directory)){
            var acl=new DirectorySecurity();var admin=new SecurityIdentifier("S-1-5-32-544");acl.SetOwner(admin);acl.SetAccessRuleProtection(true,false);
            foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            new DirectoryInfo(directory).Create(acl);
        }TrustedInstallation29.ProtectedAcl(new DirectoryInfo(directory));
    }
    public NetworkJournal29? Read()
    {
        if(!File.Exists(Pathname))return null;TrustedInstallation29.ProtectedAcl(new FileInfo(Pathname));if(new FileInfo(Pathname).Length>262144)throw new SafeError("BROKER_JOURNAL_INVALID");
        var value=JsonSerializer.Deserialize<NetworkJournal29>(File.ReadAllBytes(Pathname))??throw new SafeError("BROKER_JOURNAL_INVALID");
        if(value.Version!=1||value.Session==Guid.Empty||value.FilterKeys==null||value.ProtectionKeys==null||value.ConfirmedRoutes==null||value.Owner==null||value.FilterKeys.Length>1024||value.ProtectionKeys.Length>1024||value.ConfirmedRoutes.Count>32||value.Owner.Length>128)throw new SafeError("BROKER_JOURNAL_INVALID");return value;
    }
    public void Write(NetworkJournal29 value)
    {
        string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");var bytes=JsonSerializer.SerializeToUtf8Bytes(value);if(bytes.Length>262144)throw new SafeError("BROKER_JOURNAL_INVALID");
        try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(bytes);file.Flush(true);}File.Move(temp,Pathname,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Delete(){if(File.Exists(Pathname))File.Delete(Pathname);}
    public void RememberSession(string root,Guid session)
    {
        string receipt=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"NETWORK-LAB-ACCEPTANCE.json"))));
        string path=Path.Combine(directory,"spent-sessions-"+receipt[..32].ToLowerInvariant()+".json");SessionHistory history;
        if(File.Exists(path)){
            TrustedInstallation29.ProtectedAcl(new FileInfo(path));if(new FileInfo(path).Length>1048576)throw new SafeError("BROKER_SESSION_HISTORY_INVALID");
            history=JsonSerializer.Deserialize<SessionHistory>(File.ReadAllBytes(path),new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})??throw new SafeError("BROKER_SESSION_HISTORY_INVALID");
        }else history=new(){Root=root,InstallationReceipt=receipt};
        history.Accept(root,receipt,session);
        string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
        try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(JsonSerializer.SerializeToUtf8Bytes(history));file.Flush(true);}File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public bool OwnsAdapter(Guid id,string root)
    {
        string file=Path.Combine(directory,"adapter.json");if(!File.Exists(file))return false;
        TrustedInstallation29.ProtectedAcl(new FileInfo(file));if(new FileInfo(file).Length>4096)throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");
        try{
            var value=JsonSerializer.Deserialize<AdapterReceipt30>(File.ReadAllBytes(file),new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow});
            string machine=Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography","MachineGuid","") as string??"";
            return value is not null&&value.Version==1&&value.InterfaceGuid==id&&id==TunPlan29.AdapterGuid&&value.Name==TunPlan29.Adapter&&value.Root.Equals(root,StringComparison.OrdinalIgnoreCase)&&machine.Length>0&&value.Machine==machine;
        }catch(JsonException){throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");}
    }
    public void RememberAdapter(Guid id,string root)
    {
        if(id!=TunPlan29.AdapterGuid)throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");
        string file=Path.Combine(directory,"adapter.json");if(File.Exists(file)&&!OwnsAdapter(id,root))throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");
        string machine=Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography","MachineGuid","") as string??"";if(machine.Length==0)throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");
        string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
        try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){output.Write(JsonSerializer.SerializeToUtf8Bytes(new AdapterReceipt30{InterfaceGuid=id,Name=TunPlan29.Adapter,Root=root,Machine=machine}));output.Flush(true);}File.Move(temp,file,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void NoteError(Exception error)
    {
        string code=error is SafeError safe?safe.Code:"BROKER_SERVICE_FAILED";
        uint? native=error.Data["nativeStatus"] is uint status?status:null;
        string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
        string? check=error.Data["check"] is string stage&&stage.Length<=64?stage:null;
        try{File.WriteAllBytes(temp,JsonSerializer.SerializeToUtf8Bytes(new{utc=DateTime.UtcNow,code,nativeStatus=native,check,category=error.GetType().Name}));File.Move(temp,Path.Combine(directory,"service-error.json"),true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
