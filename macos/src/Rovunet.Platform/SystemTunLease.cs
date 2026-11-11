using System.Diagnostics;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed class TunRequest
{
    public string Action {get;set;}="status";
    public int SocksPort {get;set;}
    public string SocksUser {get;set;}="";
    public string SocksPassword {get;set;}="";
    public override string ToString()=>"TunRequest(redacted)";
}
public sealed class TunReply
{
    public string State {get;set;}="";
    public string Error {get;set;}="";
    public string Version {get;set;}="";
    public string Interface {get;set;}="";
    public bool IPv6 {get;set;}
    public bool DNS {get;set;}
    public bool UDP {get;set;}
}

// This process is an ordinary user. The signed native client authenticates to
// the installed, narrowly scoped root service. No managed process runs as root.
public sealed class SystemTunLease : IDisposable
{
    readonly Process client;
    readonly CancellationTokenSource heartbeatStop=new();
    Task heartbeat=Task.CompletedTask;
    bool stopped;
    volatile bool alive;
    public bool IsAlive=>alive&&!client.HasExited;
    public string Error {get;private set;}="MAC_TUN_WORKER_EXITED";
    SystemTunLease(Process process){client=process;_=client.StandardError.BaseStream.CopyToAsync(Stream.Null);}
    public static async Task<bool> InstalledAndReady(){
        if(!OperatingSystem.IsMacOS()||!File.Exists("/Library/PrivilegedHelperTools/org.rovunet.tun.helper"))return false;
        using var client=Process.Start(new ProcessStartInfo(EnginePaths.Require("rovunet-tun")){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--client"}})??throw new SafeError("MAC_TUN_START_FAILED");
        try{
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(6));_=client.StandardError.BaseStream.CopyToAsync(Stream.Null);
            await Wire.Write(client.StandardInput.BaseStream,new TunRequest(),deadline.Token);
            var reply=await Wire.Read<TunReply>(client.StandardOutput.BaseStream,deadline.Token);
            if(reply.Error is "MAC_TUN_HELPER_REQUIRED" or "MAC_TUN_NOT_AUTHORIZED")return false;
            if(reply.Error.Length>0)throw new SafeError(SafeCode(reply.Error));
            return reply.State=="available"&&reply.Version=="0.1.0-dev.3";
        }catch(OperationCanceledException){throw new SafeError("MAC_TUN_START_FAILED");}
        finally{client.StandardInput.Close();if(!client.HasExited)EngineProcess.Terminate(client);}
    }
    public static async Task<SystemTunLease> Start(int socks,string user,string password,CancellationToken ct)
    {
        if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        var process=Process.Start(new ProcessStartInfo(EnginePaths.Require("rovunet-tun")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--client"}})??throw new SafeError("MAC_TUN_START_FAILED");
        var lease=new SystemTunLease(process);
        try{
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(40));
            await Wire.Write(process.StandardInput.BaseStream,new TunRequest{Action="start",SocksPort=socks,SocksUser=user,SocksPassword=password},deadline.Token).ConfigureAwait(false);
            var reply=await Wire.Read<TunReply>(process.StandardOutput.BaseStream,deadline.Token).ConfigureAwait(false);
            if(reply.Error.Length>0)throw new SafeError(SafeCode(reply.Error));
            // Helper protocol/code is unchanged from dev.3. Preserve the exact
            // client CDHash so an installed dev.3 service needs no reinstall.
            if(reply.State!="active"||!reply.DNS||!reply.IPv6||reply.Version!="0.1.0-dev.3")throw new SafeError("MAC_TUN_START_FAILED");
            lease.alive=true;lease.heartbeat=lease.Heartbeat();return lease;
        }catch{lease.Dispose();throw;}
    }
    static string SafeCode(string value)=>value is "MAC_TUN_HELPER_REQUIRED" or "MAC_TUN_BUSY" or "MAC_TUN_NOT_AUTHORIZED" or "MAC_TUN_CONSOLE_REQUIRED" or "MAC_TUN_RESTORE_PENDING"?value:"MAC_TUN_START_FAILED";
    async Task Heartbeat()
    {
        try{
            while(true){
                await Task.Delay(3000,heartbeatStop.Token).ConfigureAwait(false);
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(heartbeatStop.Token);deadline.CancelAfter(5000);
                await Wire.Write(client.StandardInput.BaseStream,new TunRequest(),deadline.Token).ConfigureAwait(false);
                var reply=await Wire.Read<TunReply>(client.StandardOutput.BaseStream,deadline.Token).ConfigureAwait(false);
                if(reply.State!="active"){Error=SafeCode(reply.Error);alive=false;return;}
            }
        }catch(OperationCanceledException)when(heartbeatStop.IsCancellationRequested){}
        catch{alive=false;Error="MAC_TUN_WORKER_EXITED";try{client.StandardInput.Close();}catch{}}
    }
    public void Stop()=>StopAsync().GetAwaiter().GetResult();
    async Task StopAsync()
    {
        if(stopped)return;stopped=true;heartbeatStop.Cancel();
        try{
            await heartbeat.WaitAsync(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
            if(alive&&!client.HasExited){
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Wire.Write(client.StandardInput.BaseStream,new TunRequest{Action="stop"},deadline.Token).ConfigureAwait(false);
                TunReply reply;
                do{reply=await Wire.Read<TunReply>(client.StandardOutput.BaseStream,deadline.Token).ConfigureAwait(false);}while(reply.State=="active");
                if(reply.State!="disconnected")throw new SafeError("MAC_TUN_RESTORE_PENDING");
            }
        }catch{throw new SafeError("MAC_TUN_RESTORE_PENDING");}
        finally{alive=false;try{client.StandardInput.Close();if(!client.WaitForExit(2000))EngineProcess.Terminate(client);}catch{}}
    }
    public void Dispose(){try{Stop();}catch{}heartbeatStop.Dispose();client.Dispose();}
}
