using System.Diagnostics;
using System.Security.Cryptography;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed class ProxyGuardRequest
{
    public string Action {get;set;}="start";
    public int OwnerPid {get;set;}
    public long OwnerStarted {get;set;}
    public int EnginePid {get;set;}
    public long EngineStarted {get;set;}
    public int HttpPort {get;set;}
    public int SocksPort {get;set;}
    public byte[] StoreKey {get;set;}=[];
    public override string ToString()=>"ProxyGuardRequest(redacted)";
}
public sealed class ProxyGuardReply
{
    public string State {get;set;}="";
    public string Error {get;set;}="";
    public int Services {get;set;}
}

public sealed class ProxyGuardian : IDisposable
{
    readonly Process process;
    bool stopped;
    public bool IsAlive=>!stopped&&!process.HasExited;
    public bool HasExited=>process.HasExited;
    public int Services {get;private set;}
    ProxyGuardian(Process child){process=child;_=process.StandardError.BaseStream.CopyToAsync(Stream.Null);}
    public static async Task<ProxyGuardian> Start(int http,int socks,Process engine,byte[] storeKey,CancellationToken ct)
    {
        if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        if(storeKey.Length!=32)throw new SafeError("INVALID_MESSAGE");
        string host=Path.Combine(AppContext.BaseDirectory,"Rovunet.Agent");
        var child=Process.Start(new ProcessStartInfo(host){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--proxy-guardian"}})??throw new SafeError("MAC_PROXY_APPLY_FAILED");
        var guardian=new ProxyGuardian(child);
        try{
            using var owner=Process.GetCurrentProcess();
            await Wire.Write(child.StandardInput.BaseStream,new ProxyGuardRequest{OwnerPid=owner.Id,OwnerStarted=owner.StartTime.ToUniversalTime().Ticks,EnginePid=engine.Id,EngineStarted=engine.StartTime.ToUniversalTime().Ticks,HttpPort=http,SocksPort=socks,StoreKey=storeKey},ct);
            // The authorization dialog is owned by macOS. Cancellation closes the
            // channel; the independent guardian restores any acquired lease.
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(3));
            var reply=await Wire.Read<ProxyGuardReply>(child.StandardOutput.BaseStream,deadline.Token);
            if(reply.Error.Length>0)throw new SafeError(reply.Error);if(reply.State!="active")throw new SafeError("MAC_PROXY_APPLY_FAILED");
            guardian.Services=reply.Services;return guardian;
        }catch{guardian.Dispose();throw;}
    }
    public void Stop()
    {
        if(stopped)return;stopped=true;
        try{
            process.StandardInput.Close();
            if(!process.WaitForExit(12000))throw new SafeError("MAC_PROXY_RESTORE_PENDING");
            if(process.ExitCode!=0)throw new SafeError("MAC_PROXY_RESTORE_PENDING");
        }catch(SafeError){throw;}catch{throw new SafeError("MAC_PROXY_RESTORE_PENDING");}
    }
    public void Dispose(){try{Stop();}catch{}process.Dispose();}
    public static bool Alive(int pid,long stamp){try{using var p=Process.GetProcessById(pid);return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==stamp;}catch{return false;}}
    public static async Task<int> Serve(Stream input,Stream output)
    {
        SystemProxyLease? lease=null;
        try{
            using var wait=new CancellationTokenSource(TimeSpan.FromSeconds(15));var request=await Wire.Read<ProxyGuardRequest>(input,wait.Token);
            if(request.Action!="start"||!Alive(request.OwnerPid,request.OwnerStarted)||!Alive(request.EnginePid,request.EngineStarted))throw new SafeError("INVALID_MESSAGE");
            var eof=Task.Run(async()=>{try{await Wire.Read<ProxyGuardRequest>(input,CancellationToken.None);}catch{};});
            using var vault=new PipeSecretVault(request.StoreKey);CryptographicOperations.ZeroMemory(request.StoreKey);
            using var journal=new PrivateProxyJournal(Path.Combine(PrivateStore.Root,"system-proxy"),vault);
            using var backend=new MacProxyPreferences();lease=new SystemProxyLease(backend,journal,()=>!eof.IsCompleted&&Alive(request.OwnerPid,request.OwnerStarted)&&Alive(request.EnginePid,request.EngineStarted));
            try{
                lease.Start(request.HttpPort,request.SocksPort);
                await Wire.Write(output,new ProxyGuardReply{State="active",Services=lease.ServiceCount},CancellationToken.None);
                // EOF works even after UI/Agent death; the guardian is deliberately
                // not a child in the native engine supervisor's process group.
                while(!eof.IsCompleted&&Alive(request.OwnerPid,request.OwnerStarted)&&Alive(request.EnginePid,request.EngineStarted)){
                    if(await Task.WhenAny(eof,Task.Delay(1500))==eof)break;lease.Refresh();
                }
                lease.Stop();return 0;
            }catch{try{lease.Stop();}catch{throw new SafeError("MAC_PROXY_RESTORE_PENDING");}throw;}
        }catch(Exception error){try{await Wire.Write(output,new ProxyGuardReply{State="error",Error=error is SafeError safe?safe.Code:"MAC_PROXY_APPLY_FAILED"},CancellationToken.None);}catch{}return 1;}
    }
    public static void RecoverAbandoned(byte[] storeKey)
    {
        string root=Path.Combine(PrivateStore.Root,"system-proxy");if(!File.Exists(Path.Combine(root,"profiles.enc")))return;
        using var vault=new PipeSecretVault(storeKey);
        using var journal=new PrivateProxyJournal(root,vault);var saved=journal.Load();if(saved.Services.Count==0)return;
        using var backend=new MacProxyPreferences();new SystemProxyLease(backend,journal).Recover();
    }
}
