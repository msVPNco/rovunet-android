using System.IO.Pipes;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;

namespace Rovunet.NetworkLab;
internal static class ServiceClient30
{
    internal static async Task<NamedPipeClientStream> Open(string root,CancellationToken stop)
    {
        int expected=NetworkService30.ExpectedServerPid(root);
        var pipe=new NamedPipeClientStream(".",NetworkService30.Pipe,PipeDirection.InOut,PipeOptions.Asynchronous,System.Security.Principal.TokenImpersonationLevel.Impersonation);
        try{await pipe.ConnectAsync(stop);PipePeer28.RequireServer(pipe.SafePipeHandle,expected);return pipe;}
        catch{pipe.Dispose();throw;}
    }
    internal static async Task<TunReply29> Send(NamedPipeClientStream pipe,TunRequest29 value,CancellationToken stop)
    {
        await Wire.Write(pipe,value,stop);return await Wire.Read<TunReply29>(pipe,stop,4096);
    }
    public static async Task<int> Run(string[] args)
    {
        try{
            bool start=args.Length>=4&&args[0]=="--service-start"&&args[3]=="--accept-isolated-lab";
            if(!start&&args is not(["--service-status"]or["--service-stop"]or["--service-recover"]))return 12;
            string root=TrustedInstallation29.Root();TrustedInstallation29.Manifest(root);TrustedInstallation29.RequireLab(root);
            Guid session=Guid.NewGuid();var request=new TunRequest29{Version=2,Session=session};
            if(start){
                if(!int.TryParse(args[1],out int port)||args.Skip(4).Any(x=>x is not("--udp" or "--kill-switch"))||args.Skip(4).Distinct().Count()!=args.Length-4)return 12;
                request=new(){Version=2,Action="start",Session=session,SocksPort=port,Peers=args[2].Split(','),KillSwitch=args.Contains("--kill-switch"),EnableUdp=args.Contains("--udp"),ExperimentalAcknowledged=true};TunPlan29.Validate(request);
            }
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(55));using var pipe=await Open(root,deadline.Token);
            Task<TunReply29> SendRequest(TunRequest29 value)=>Send(pipe,value,deadline.Token);
            if(args[0] is "--service-stop" or "--service-recover"){
                var status=await SendRequest(request);if(status.Error.Length>0){Console.WriteLine(JsonSerializer.Serialize(status));return 1;}
                request.Session=status.Session==Guid.Empty?session:status.Session;request.Action=args[0]=="--service-stop"?"stop":"recover";
            }
            var reply=await SendRequest(request);Console.WriteLine(JsonSerializer.Serialize(reply));return reply.Error.Length==0?0:1;
        }catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{error=e.Code}));return 1;}
        catch{Console.WriteLine("{\"error\":\"BROKER_SERVICE_CLIENT_FAILED\"}");return 1;}
    }
}
