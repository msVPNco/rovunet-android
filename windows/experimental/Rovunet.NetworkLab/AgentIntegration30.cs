using System.Diagnostics;
using System.IO.Pipes;
using System.Net.NetworkInformation;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;

namespace Rovunet.NetworkLab;

// A fixed synthetic integration fixture, not a consumer connection entry point.
internal static class AgentIntegration30
{
    public static async Task<int> Run(string[] args)
    {
        if(args is not ["--test-agent-tun-events" or "--test-agent-tun-data","--accept-isolated-lab"])return 12;
        bool data=args[0]=="--test-agent-tun-data";
        var result=new Dictionary<string,object?>{{"synthetic",true},{"systemProxyChanged",false}};
        try{
            string root=TrustedInstallation29.Root();var hashes=TrustedInstallation29.Manifest(root);TrustedInstallation29.RequireLab(root);
            if(!hashes.ContainsKey("Rovunet.Agent.exe"))throw new SafeError("BROKER_UNTRUSTED_INSTALLATION");
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var service=await ServiceClient30.Open(root,deadline.Token);
            var initial=await ServiceClient30.Send(service,new(){Version=2,Session=Guid.NewGuid()},deadline.Token);
            if(initial.State!="inactive"||initial.Error.Length>0||initial.ProtectionInstalled)throw new SafeError("BROKER_RECOVERY_REQUIRED");
            string name="rovunet."+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            using var job=new ProcessJob();
            using var agent=Process.Start(new ProcessStartInfo(Path.Combine(root,"Rovunet.Agent.exe")){
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,
                ArgumentList={"--probe-pipe",name,"--parent",Environment.ProcessId.ToString()}
            })??throw new SafeError("LAB_AGENT_START_FAILED");
            try{job.Assign(agent);}catch{if(!agent.HasExited)agent.Kill(true);throw;}
            using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
            await pipe.ConnectAsync(deadline.Token);PipePeer28.RequireServer(pipe.SafePipeHandle,agent.Id);
            async Task<AgentReply> Command(AgentCommand command){await Wire.Write(pipe,command,deadline.Token);return await Wire.Read<AgentReply>(pipe,deadline.Token);}
            Task<AgentReply> Status()=>Command(new(){Action="status"});
            AgentReply? connected=null;
            if(data){
                connected=await Command(new(){Action="connect",Profile=Importer.Single("socks5://10.0.2.2:18883#IsolatedFixture"),SystemProxy=false,EnableUdp=true});
                result["agentConnect"]=connected;
                if(connected.State!="connected"||connected.SocksPort==0||connected.Error.Length>0)throw new SafeError("LAB_AGENT_START_FAILED");
            }
            result["before"]=await Status();string epoch=NetworkEnvironment30.Read();int events=0;
            NetworkAddressChangedEventHandler changed=(_,_)=>Interlocked.Increment(ref events);
            Guid session=Guid.NewGuid();bool attempted=false;
            NetworkChange.NetworkAddressChanged+=changed;
            try{
                attempted=true;
                var started=await ServiceClient30.Send(service,new(){Version=2,Action="start",Session=session,SocksPort=connected?.SocksPort??10808,Peers=["10.0.2.2"],EnableUdp=true,KillSwitch=true,ExperimentalAcknowledged=true},deadline.Token);
                result["start"]=started;
                if(!started.TunReady||!started.ProtectionInstalled||started.Error.Length>0)throw new SafeError("LAB_TUN_NOT_READY");
                await Task.Delay(3000,deadline.Token);
                result["eventsDuringStart"]=Volatile.Read(ref events);result["physicalEpochUnchanged"]=NetworkEnvironment30.Read()==epoch;
                result["agentAliveAfterStart"]=!agent.HasExited;result["during"]=await Status();
                if(data){
                    var probes=await Task.WhenAll(new[]{"198.18.0.10","2001:db8:1::10"}.SelectMany(host=>new[]{true,false}.Select(tcp=>NetworkProbe30.Probe(IPAddress.Parse(host),tcp?18880:18881,tcp))));
                    result["payload"]=probes;result["payloadPassed"]=probes.All(probe=>probe.success);
                }
            }finally{
                NetworkChange.NetworkAddressChanged-=changed;
                if(attempted){
                    using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    result["recovery"]=await ServiceClient30.Send(service,new(){Version=2,Action="recover",Session=session},recovery.Token);
                }
            }
            await Task.Delay(1500,deadline.Token);
            result["agentAliveAfterRecovery"]=!agent.HasExited;result["after"]=await Status();
            var recoveryResult=(TunReply29)result["recovery"]!;
            bool success=Volatile.Read(ref events)>0&&(bool)result["physicalEpochUnchanged"]!&&
                (bool)result["agentAliveAfterStart"]!&&(bool)result["agentAliveAfterRecovery"]!&&
                ((AgentReply)result["before"]!).Error.Length==0&&((AgentReply)result["during"]!).Error.Length==0&&((AgentReply)result["after"]!).Error.Length==0&&
                recoveryResult.State=="inactive"&&!recoveryResult.ProtectionInstalled&&recoveryResult.Error.Length==0&&
                (!data||(bool)result["payloadPassed"]!&&((AgentReply)result["during"]!).State=="connected"&&((AgentReply)result["after"]!).State=="connected");
            result["success"]=success;Console.WriteLine(JsonSerializer.Serialize(result));return success?0:1;
        }catch(SafeError e){result["error"]=e.Code;}
        catch{result["error"]="LAB_AGENT_INTEGRATION_FAILED";}
        result["success"]=false;Console.WriteLine(JsonSerializer.Serialize(result));return 1;
    }
}
