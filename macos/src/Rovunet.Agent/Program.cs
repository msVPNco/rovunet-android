using System.Threading.Channels;
using System.Security.Cryptography;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Agent;

internal static class Program
{
    static async Task Main(string[] args)
    {
        if(args.Length==1&&args[0]=="--proxy-guardian"){Environment.ExitCode=await ProxyGuardian.Serve(Console.OpenStandardInput(),Console.OpenStandardOutput());return;}
        if(args.Length!=1||args[0]!="--stdio")return;
        using var lifetime=new CancellationTokenSource();
        CancellationTokenSource? operation=null;
        var commands=Channel.CreateBounded<AgentCommand>(new BoundedChannelOptions(8){SingleReader=true,SingleWriter=true});
        var input=Console.OpenStandardInput();var output=Console.OpenStandardOutput();
        using var runtime=new Runtime();
        void Send(AgentReply reply)=>Wire.Write(output,reply,CancellationToken.None).GetAwaiter().GetResult();
        runtime.StageChanged+=Send;
        var reader=Task.Run(async()=>{
            try{while(true){var command=await Wire.Read<AgentCommand>(input,lifetime.Token);if(command.Action is "cancel" or "stop")operation?.Cancel();await commands.Writer.WriteAsync(command,lifetime.Token);}}
            catch{lifetime.Cancel();operation?.Cancel();commands.Writer.TryComplete();}
        });
        try {
            await foreach(var command in commands.Reader.ReadAllAsync(lifetime.Token)){
                operation=new CancellationTokenSource();using var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,operation.Token);
                try {
                    AgentReply reply=command.Action switch {
                        "connect" when command.Profile!=null=>await runtime.Connect(command.Profile,command.SystemProxy,linked.Token,command.HostKey,command.SlipCertificate,command.SlipInterval,command.SshTlsCa,command.LocalHttpPort,command.LocalSocksPort,command.ProxyStoreKey,command.SystemTun),
                        "stop" or "cancel"=>runtime.Stop(),
                        "proxy-recover"=>RecoverProxy(command.ProxyStoreKey),
                        "status"=>runtime.Status(),
                        "telemetry"=>await runtime.Telemetry(linked.Token),
                        "probe"=>await runtime.Probe(command.ProbeUrl,linked.Token,fallbacks:command.ProbeFallbacks),
                        _=>throw new SafeError("INVALID_MESSAGE")
                    };
                    Send(reply);
                } catch(OperationCanceledException){runtime.Stop();Send(new(){Error="CANCELLED"});}
                catch(SafeError e){Send(new(){State="error",Error=e.Code});}
                catch{runtime.Stop();Send(new(){State="error",Error="CORE_START_FAILED"});}
                finally{CryptographicOperations.ZeroMemory(command.ProxyStoreKey);operation.Dispose();operation=null;}
            }
        } catch(OperationCanceledException){} finally{lifetime.Cancel();runtime.Stop();}
        _=reader; // EOF or process exit owns shutdown; no raw engine output is logged.
    }
    static AgentReply RecoverProxy(byte[] key){ProxyGuardian.RecoverAbandoned(key);return new(){State="disconnected"};}
}
