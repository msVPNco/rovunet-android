using System.Text.Json;
using Rovunet.Core;
namespace Rovunet.NetworkBroker;

internal sealed class BootReceipt
{
    public int Version{get;set;}=1;
    public Guid Installation{get;set;}
    public string Root{get;set;}="";
    public string Machine{get;set;}="";
    public string Phase{get;set;}="arming";
    public Guid RuntimeSession{get;set;}
}
internal sealed class BootPolicy(string root)
{
    string DirectoryPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Rovunet-Network-Lab");
    string Pathname=>Path.Combine(DirectoryPath,"boot-policy.json");
    string Machine=>Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography","MachineGuid","") as string??"";
    Guid Installation()
    {
        string file=Path.Combine(root,"NETWORK-LAB-ACCEPTANCE.json");TrustedInstallation29.ProtectedAcl(new FileInfo(file));
        using var doc=JsonDocument.Parse(File.ReadAllBytes(file));
        if(!doc.RootElement.TryGetProperty("installId",out var id)||!id.TryGetGuid(out var result)||result==Guid.Empty)throw new SafeError("BROKER_BOOT_RECEIPT_INVALID");return result;
    }
    public BootReceipt? Read()
    {
        if(!File.Exists(Pathname))return null;TrustedInstallation29.ProtectedAcl(new FileInfo(Pathname));
        if(new FileInfo(Pathname).Length>4096)throw new SafeError("BROKER_BOOT_RECEIPT_INVALID");
        var value=JsonSerializer.Deserialize<BootReceipt>(File.ReadAllBytes(Pathname),new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow});
        if(value==null||value.Version!=1||value.Installation!=Installation()||!value.Root.Equals(root,StringComparison.OrdinalIgnoreCase)||value.Machine!=Machine||value.Phase is not("arming" or "armed" or "runtime-cutover" or "runtime-owned" or "disarming"))throw new SafeError("BROKER_BOOT_RECEIPT_INVALID");return value;
    }
    void Write(BootReceipt value)
    {
        _=new ProtectedJournal29(); // Creates/validates the shared SYSTEM/administrator-only directory.
        string temp=Path.Combine(DirectoryPath,Guid.NewGuid().ToString("N")+".tmp");
        try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){file.Write(JsonSerializer.SerializeToUtf8Bytes(value));file.Flush(true);}File.Move(temp,Pathname,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Arm()
    {
        TrustedInstallation29.RequireAdmin();TrustedInstallation29.RequireLab(root);if(new ProtectedJournal29().Read()!=null)throw new SafeError("BROKER_RECOVERY_REQUIRED");
        if(Read()!=null)throw new SafeError("BROKER_BOOT_RECOVERY_REQUIRED");
        var receipt=new BootReceipt{Installation=Installation(),Root=root,Machine=Machine};Write(receipt);
        using var wfp=new PersistentWfp29();wfp.BootBaseline(receipt.Installation,false);receipt.Phase="armed";Write(receipt);
    }
    // Called only after the normal persistent protection transaction committed.
    public void RuntimeCommitted(Guid session)
    {
        var receipt=Read();if(receipt==null)return;
        if(receipt.RuntimeSession!=Guid.Empty&&receipt.RuntimeSession!=session)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");
        receipt.RuntimeSession=session;receipt.Phase="runtime-cutover";Write(receipt);
        using var wfp=new PersistentWfp29();wfp.BootBaseline(receipt.Installation,true,runtimeOnly:true);
        receipt.Phase="runtime-owned";Write(receipt);
    }
    public void Disarm(Guid session=default)
    {
        var receipt=Read();if(receipt==null)return;
        if(receipt.RuntimeSession!=Guid.Empty&&receipt.RuntimeSession!=session)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");
        receipt.Phase="disarming";Write(receipt);
        using var wfp=new PersistentWfp29();wfp.BootBaseline(receipt.Installation,true);
        File.Delete(Pathname);
    }
    public static int Command(string[] args)
    {
        if(args is not ["--boot-arm" or "--boot-disarm","--accept-isolated-lab"])return 12;
        try{
            TrustedInstallation29.RequireAdmin();string root=TrustedInstallation29.Root();TrustedInstallation29.Manifest(root);TrustedInstallation29.RequireLab(root);
            using var mutex=new Mutex(false,@"Global\Rovunet.ExperimentalNetworkBroker");bool acquired;
            try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");
            try{var policy=new BootPolicy(root);if(args[0]=="--boot-arm")policy.Arm();else{if(new ProtectedJournal29().Read()!=null)throw new SafeError("BROKER_RECOVERY_REQUIRED");policy.Disarm();}}finally{mutex.ReleaseMutex();}
            Console.WriteLine(JsonSerializer.Serialize(new{operation=args[0],committed=true,actualBootVerified=false}));return 0;
        }catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{error=e.Code}));return 1;}catch{Console.WriteLine("{\"error\":\"BROKER_BOOT_POLICY_FAILED\"}");return 1;}
    }
}
