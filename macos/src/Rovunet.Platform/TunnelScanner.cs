using System.Diagnostics;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed record TunnelScanTrust(string HostKey,string SlipCertificate,string SshTlsCa="")
{
    public override string ToString()=>"TunnelScanTrust(redacted)";
}

// The GUI coordinates the two phases; no phase changes system proxy or TUN.
public interface IResolverChecks
{
    Task Dns(ScanCheckpoint plan,Func<ValueTask> changed,CancellationToken token);
    Task<AgentReply> Server(Profile profile,string address,string wire,string url,TunnelScanTrust trust,int timeout,bool verifyHttps,CancellationToken token);
}
public sealed class MacResolverChecks : IResolverChecks
{
    public Task Dns(ScanCheckpoint plan,Func<ValueTask> changed,CancellationToken token)=>ScanSession.Run(plan,changed,token);
    public Task<AgentReply> Server(Profile p,string address,string wire,string url,TunnelScanTrust trust,int timeout,bool verifyHttps,CancellationToken token)=>
        TunnelScanner.Check(p,address,wire,url,trust,timeout,verifyHttps,token);
}
public static class TunnelScanner
{
    public static Task<AgentReply> Verify(Profile original,string endpoint,string transport,string url,TunnelScanTrust trust,int timeoutMS,CancellationToken stop)=>Check(original,endpoint,transport,url,trust,timeoutMS,true,stop);
    public static async Task<AgentReply> Check(Profile original,string endpoint,string transport,string url,TunnelScanTrust trust,int timeoutMS,bool verifyHttps,CancellationToken stop)
    {
        if(timeoutMS is <5000 or >180000)throw new SafeError("DNS_SCAN_INVALID");
        Profile candidate=ResolverScan.Candidate(original,endpoint,transport,true);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(stop);budget.CancelAfter(timeoutMS);
        string host=Path.Combine(AppContext.BaseDirectory,"Rovunet.Agent"+(OperatingSystem.IsWindows()?".exe":""));
        using var agent=Process.Start(new ProcessStartInfo(host){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--stdio"}})??throw new SafeError("MAC_AGENT_MISSING");
        _=agent.StandardError.BaseStream.CopyToAsync(Stream.Null);var clock=Stopwatch.StartNew();bool tunnel=false,ssh=false,httpsAttempted=false;long handshake=0,httpsStart=0;
        async Task<AgentReply> Send(AgentCommand command){
            await Wire.Write(agent.StandardInput.BaseStream,command,budget.Token).ConfigureAwait(false);
            while(true){var reply=await Wire.Read<AgentReply>(agent.StandardOutput.BaseStream,budget.Token).ConfigureAwait(false);if(reply.State!="engine-started")return reply;}
        }
        try{
            // Scanners never acquire proxy/TUN settings or approve identities.
            var ready=await Send(new(){Action="connect",Profile=candidate,HostKey=trust.HostKey,SlipCertificate=trust.SlipCertificate,SshTlsCa=trust.SshTlsCa}).ConfigureAwait(false);
            tunnel=ready.TunnelEstablished;ssh=ready.SshEstablished;handshake=tunnel?ready.HandshakeMs:0;
            if(ready.State!="connected"){ready.ElapsedMs=clock.ElapsedMilliseconds;return ready;}
            if(!verifyHttps){ready.Verified=false;ready.ElapsedMs=clock.ElapsedMilliseconds;return ready;}
            httpsStart=clock.ElapsedMilliseconds;httpsAttempted=true;var measured=await Send(new(){Action="probe",ProbeUrl=url,ProbeFallbacks=false}).ConfigureAwait(false);
            measured.HttpsElapsedMs=clock.ElapsedMilliseconds-httpsStart;measured.HandshakeMs=handshake;measured.HttpsAttempted=true;measured.TunnelEstablished=tunnel;measured.SshEstablished=ssh;measured.ElapsedMs=clock.ElapsedMilliseconds;return measured;
        }catch(OperationCanceledException){stop.ThrowIfCancellationRequested();return new(){Error="TEST_TIMEOUT",TunnelEstablished=tunnel,SshEstablished=ssh,HandshakeMs=handshake,HttpsAttempted=httpsAttempted,HttpsElapsedMs=httpsAttempted?clock.ElapsedMilliseconds-httpsStart:0,ElapsedMs=clock.ElapsedMilliseconds};}
        catch(Exception e){return new(){Error=e is SafeError safe?safe.Code:"CORE_EXITED",TunnelEstablished=tunnel,SshEstablished=ssh,HandshakeMs=handshake,HttpsAttempted=httpsAttempted,HttpsElapsedMs=httpsAttempted?clock.ElapsedMilliseconds-httpsStart:0,ElapsedMs=clock.ElapsedMilliseconds};}
        finally{try{agent.StandardInput.Close();using var shutdown=new CancellationTokenSource(10000);await agent.WaitForExitAsync(shutdown.Token).ConfigureAwait(false);}catch{try{EngineProcess.Terminate(agent);}catch{}}}
    }
}
