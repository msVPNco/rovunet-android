using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.NetworkBroker;

internal sealed class ServicePolicy30
{
    public int Version { get; set; } = 1;
    public string OwnerSid { get; set; } = "";
    public static ServicePolicy30 Read(string root)
    {
        string file=Path.Combine(root,"NETWORK-SERVICE-POLICY.json");
        TrustedInstallation29.ProtectedAcl(new FileInfo(file));
        if(new FileInfo(file).Length>4096)throw new SafeError("BROKER_SERVICE_POLICY_INVALID");
        var value=JsonSerializer.Deserialize<ServicePolicy30>(File.ReadAllBytes(file),new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})??throw new SafeError("BROKER_SERVICE_POLICY_INVALID");
        if(value.Version!=1||value.OwnerSid.Length>128)throw new SafeError("BROKER_SERVICE_POLICY_INVALID");
        var sid=new SecurityIdentifier(value.OwnerSid);
        if(!sid.IsAccountSid()&&value.OwnerSid!="S-1-5-18")throw new SafeError("BROKER_SERVICE_POLICY_INVALID");
        return value;
    }
}

// SCM host owns the privileged runtime. GUI/controller exit does not dispose a
// live session. Explicit stop/recover is distinct from shutdown or process loss.
internal static class NetworkService30
{
    public const string Name="RovunetNetworkLab";
    public const string Pipe="rovunet-network-service-v2";
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void ServiceMain(uint count,IntPtr arguments);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint Handler(uint control,uint eventType,IntPtr data,IntPtr context);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Entry{[MarshalAs(UnmanagedType.LPWStr)]public string? Name;public ServiceMain? Main;}
    [StructLayout(LayoutKind.Sequential)]struct ServiceStatus{public uint Type,State,Accepted,Win32Exit,SpecificExit,Checkpoint,WaitHint;}
    [StructLayout(LayoutKind.Sequential)]internal struct ProcessStatus{public uint Type,State,Accepted,Win32Exit,SpecificExit,Checkpoint,WaitHint,Pid,Flags;}
    [StructLayout(LayoutKind.Sequential)]struct ServiceConfig{public uint Type,Start,Error;public IntPtr Image,Group;public uint Tag;public IntPtr Dependencies,Account,Display;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool StartServiceCtrlDispatcherW([In]Entry[] table);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr RegisterServiceCtrlHandlerExW(string name,Handler handler,IntPtr context);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool SetServiceStatus(IntPtr handle,ref ServiceStatus status);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint processId);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe,out uint sessionId);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenSCManagerW(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenServiceW(IntPtr manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool QueryServiceStatusEx(IntPtr service,uint level,out ProcessStatus status,uint length,out uint required);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryServiceConfigW(IntPtr service,IntPtr config,uint length,out uint required);
    [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(IntPtr handle);
    static readonly ServiceMain main=ServiceEntry;
    static readonly Handler handler=Control;
    static readonly CancellationTokenSource lifetime=new();
    static IntPtr statusHandle;
    static bool explicitStop;
    static uint checkpoint;
    public static int Run()
    {
        TrustedInstallation29.RequireAdmin();
        return StartServiceCtrlDispatcherW([new(){Name=Name,Main=main},new()])?0:20;
    }
    static void Report(uint state,uint exit=0)
    {
        var status=new ServiceStatus{Type=0x10,State=state,Accepted=state==4?1u|4u|0x40u|0x100u:0u,Win32Exit=exit,SpecificExit=exit==1066?1u:0u,Checkpoint=state is 2 or 3?++checkpoint:0,WaitHint=state is 2 or 3?30000u:0u};
        if(statusHandle!=IntPtr.Zero)SetServiceStatus(statusHandle,ref status);
    }
    static uint Control(uint control,uint eventType,IntPtr data,IntPtr context)
    {
        if(control is 1 or 5 or 15){explicitStop=control==1;Report(3);lifetime.Cancel();}
        return 0;
    }
    static void ServiceEntry(uint count,IntPtr arguments)
    {
        statusHandle=RegisterServiceCtrlHandlerExW(Name,handler,IntPtr.Zero);if(statusHandle==IntPtr.Zero)return;
        Report(2);
        // Mutex acquisition and release stay on this synchronous SCM thread.
        // Native mutex ownership must never cross async continuation threads.
        using var mutex=new Mutex(false,@"Global\Rovunet.ExperimentalNetworkBroker");bool acquired=false;
        try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");Serve().GetAwaiter().GetResult();Report(1);}catch(Exception error){
            try{new ProtectedJournal29().NoteError(error);}catch{}
            // Serve has disposed the live policy/job before this point. Exit
            // without SERVICE_STOPPED so configured SCM crash recovery also
            // works on existing installations whose non-crash flag is false.
            if(error is SafeError{Code:"BROKER_NETWORK_PATH_LOST"})Environment.Exit(20);
            Report(1,1066);
        }finally{if(acquired)mutex.ReleaseMutex();}
    }
    static (int Pid,bool Owner,string Sid,int Session,long Started) Client(NamedPipeServerStream pipe,string root,string owner,Dictionary<string,string> hashes)
    {
        if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint pid)||pid==0||!GetNamedPipeClientSessionId(pipe.SafePipeHandle,out uint session))throw new SafeError("BROKER_CLIENT_REJECTED");
        using var process=Process.GetProcessById(checked((int)pid));
        string? impersonated=null;bool elevatedAdministrator=false;
        pipe.RunAsClient(()=>{using var identity=WindowsIdentity.GetCurrent(true);impersonated=identity?.User?.Value;elevatedAdministrator=identity!=null&&new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);});
        bool owns=impersonated==owner;
        if((!owns&&!elevatedAdministrator)||TrustedInstallation29.Sid(process)!=impersonated||process.HasExited||process.SessionId!=session)throw new SafeError("BROKER_CLIENT_REJECTED");
        string image=process.MainModule?.FileName??"";string file=Path.GetFileName(image);
        if(file is not("Rovunet.NetworkLab.exe" or "Rovunet.exe")||!image.Equals(Path.Combine(root,file),StringComparison.OrdinalIgnoreCase)||!hashes.ContainsKey(file))throw new SafeError("BROKER_CLIENT_REJECTED");
        TrustedInstallation29.ProtectedAcl(new FileInfo(image));
        if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(image))).Equals(hashes[file],StringComparison.OrdinalIgnoreCase))throw new SafeError("BROKER_CLIENT_REJECTED");
        return (process.Id,owns,impersonated!,process.SessionId,process.StartTime.ToUniversalTime().Ticks);
    }
    static async Task Serve()
    {
        string root=TrustedInstallation29.Root();TrustedInstallation29.RequireLab(root);var hashes=TrustedInstallation29.Manifest(root);var policy=ServicePolicy30.Read(root);
        using var runtime=new NetworkRuntime29(root,policy.OwnerSid);
        int unexpectedFailure=0;
        runtime.UnexpectedExit+=()=>{Interlocked.Exchange(ref unexpectedFailure,1);lifetime.Cancel();};
        try{
            var acl=new PipeSecurity();acl.SetOwner(new SecurityIdentifier("S-1-5-18"));acl.SetAccessRuleProtection(true,false);
            acl.AddAccessRule(new(new SecurityIdentifier("S-1-5-2"),PipeAccessRights.FullControl,AccessControlType.Deny)); // Network logons must not use this local control channel.
            acl.AddAccessRule(new(new SecurityIdentifier("S-1-5-18"),PipeAccessRights.FullControl,AccessControlType.Allow));
            acl.AddAccessRule(new(new SecurityIdentifier("S-1-5-32-544"),PipeAccessRights.ReadWrite,AccessControlType.Allow));
            acl.AddAccessRule(new(new SecurityIdentifier(policy.OwnerSid),PipeAccessRights.ReadWrite,AccessControlType.Allow));
            Report(4);
            while(!lifetime.IsCancellationRequested){
                using var pipe=NamedPipeServerStreamAcl.Create(Pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.FirstPipeInstance,4096,4096,acl);
                try{
                    await pipe.WaitForConnectionAsync(lifetime.Token);
                    (int Pid,bool Owner,string Sid,int Session,long Started)? bound=null;
                    while(pipe.IsConnected&&!lifetime.IsCancellationRequested){
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);deadline.CancelAfter(TimeSpan.FromSeconds(45));
                        var request=await Wire.Read<TunRequest29>(pipe,deadline.Token,4096);
                        // Impersonate the writer of the message just read, on every request.
                        // RunAsClient restores the thread token even if validation throws.
                        var client=Client(pipe,root,policy.OwnerSid,hashes);
                        if(bound is {} previous&&previous!=client)throw new SafeError("BROKER_CLIENT_REJECTED");bound=client;
                        TunPlan29.Validate(request);PipePeer28.RequireClient(pipe.SafePipeHandle,client.Pid);
                        // Installation/removal under another elevated administrator
                        // may inspect/recover, but may not start a user's tunnel.
                        if(!client.Owner&&request.Action=="start")throw new SafeError("BROKER_CLIENT_REJECTED");
                        TunReply29 reply;
                        try{reply=request.Action switch{"status"=>runtime.Status(),"start"=>await runtime.Start(request,deadline.Token),"stop" or "recover"=>runtime.Recover(request.Session),_=>throw new SafeError("BROKER_REQUEST_REJECTED")};}
                        catch(Exception error){try{new ProtectedJournal29().NoteError(error);}catch{}try{reply=runtime.Status();}catch{reply=new(){State="recovery-required"};}reply.Error=error is SafeError safe?safe.Code:error is OperationCanceledException?"CANCELLED":"BROKER_OPERATION_FAILED";}
                        await Wire.Write(pipe,reply,deadline.Token);
                    }
                }catch(OperationCanceledException)when(lifetime.IsCancellationRequested){break;}
                catch{ /* Reject/disconnect this client, retaining the protected session. */ }
            }
            if(explicitStop){var state=runtime.Status();if(state.Session!=Guid.Empty)runtime.Recover(state.Session);}
            else{
                runtime.LostController(); // Shutdown/loss retains opted-in baseline protection.
                if(Volatile.Read(ref unexpectedFailure)!=0)throw new SafeError("BROKER_NETWORK_PATH_LOST");
            }
        }finally{ /* Runtime disposal retains persistent filters after failure. */ }
    }
    public static int ExpectedServerPid(string root)
    {
        IntPtr manager=OpenSCManagerW(null,null,1);if(manager==IntPtr.Zero)throw new SafeError("BROKER_SERVICE_UNAVAILABLE");
        try{IntPtr service=OpenServiceW(manager,Name,1|4);if(service==IntPtr.Zero)throw new SafeError("BROKER_SERVICE_UNAVAILABLE");try{
            if(!QueryServiceStatusEx(service,0,out var state,(uint)Marshal.SizeOf<ProcessStatus>(),out _)||state.State!=4||state.Pid==0)throw new SafeError("BROKER_SERVICE_UNAVAILABLE");
            // SCM configuration is protected by its DACL. Querying a SYSTEM
            // process with Process.MainModule would fail for the ordinary GUI.
            const uint size=8192;IntPtr data=Marshal.AllocHGlobal((int)size);try{
                if(!QueryServiceConfigW(service,data,size,out _))throw new SafeError("BROKER_SERVICE_UNAVAILABLE");var config=Marshal.PtrToStructure<ServiceConfig>(data);
                if(config.Type!=0x10||Marshal.PtrToStringUni(config.Account)!="LocalSystem"||Marshal.PtrToStringUni(config.Image)!="\""+Path.Combine(root,"Rovunet.NetworkBroker.exe")+"\" --service")throw new SafeError("BROKER_SERVICE_UNAVAILABLE");
            }finally{Marshal.FreeHGlobal(data);}
            return checked((int)state.Pid);
        }finally{CloseServiceHandle(service);}}finally{CloseServiceHandle(manager);}
    }
}
