using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;
using Rovunet.NetworkLab;

// Developer lab controller, excluded from the ordinary installer/UI. Starting
// real networking additionally needs a protected machine-bound lab marker.
if(args.Length>0&&args[0].StartsWith("--service-",StringComparison.Ordinal))return await ServiceClient30.Run(args);
if(args.Length>0&&args[0]=="--test-upstream")return await TestUpstream30.Run(args);
if(args.Length>0&&args[0]=="--test-service-boundary")return await ServiceBoundary30.Run(args);
if(args.Length>0&&args[0] is "--test-agent-tun-events" or "--test-agent-tun-data")return await AgentIntegration30.Run(args);
if(args.Length>0&&args[0] is "--test-probe" or "--test-probes" or "--test-dns" or "--test-interfaces")return await NetworkProbe30.Run(args);
if(args.Length==0){Console.WriteLine("Experimental isolated Windows network lab. Service commands: --service-status, --service-recover, --service-stop, --service-start <SOCKS port> <peer IP[,IP]> --accept-isolated-lab [--kill-switch] [--udp]. UDP requires a compatible upstream. Not a certified protection claim. Legacy --status/--recover remain available.");return 0;}
try{
    bool start=(args.Length==4||args.Length==5&&args[4]=="--kill-switch")&&args[0]=="--start"&&args[3]=="--accept-isolated-lab";if(!start&&args is not(["--status"]or["--recover"]))return 12;
    string root=TrustedInstallation29.Root();TrustedInstallation29.Manifest(root);Guid session=Guid.NewGuid();var request=new TunRequest29{Session=session};
    if(start){if(!int.TryParse(args[1],out int port)||Console.IsInputRedirected)return 12;request=new(){Session=session,Action="start",SocksPort=port,Peers=args[2].Split(','),KillSwitch=args.Length==5&&args[4]=="--kill-switch",ExperimentalAcknowledged=true};TunPlan29.Validate(request);TrustedInstallation29.RequireLab(root);}
    string name="rovunet-network-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    using var broker=Process.Start(new ProcessStartInfo(Path.Combine(root,"Rovunet.NetworkBroker.exe")){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden,ArgumentList={"--pipe",name,"--parent",Environment.ProcessId.ToString()}})??throw new SafeError("BROKER_ADMIN_REQUIRED");
    using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));await pipe.ConnectAsync(deadline.Token);PipePeer28.RequireServer(pipe.SafePipeHandle,broker.Id);
    async Task<TunReply29> Send(TunRequest29 value){using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(55));await Wire.Write(pipe,value,stop.Token);return await Wire.Read<TunReply29>(pipe,stop.Token,4096);}
    if(args[0]=="--recover"){var status=await Send(request);request.Session=status.Session==Guid.Empty?session:status.Session;request.Action="recover";var reply=await Send(request);Console.WriteLine(JsonSerializer.Serialize(reply));return reply.Error.Length==0?0:1;}
    var result=await Send(request);Console.WriteLine(JsonSerializer.Serialize(result));
    if(start){Console.WriteLine("Press Enter to stop and restore owned settings. Closing/crashing this controller with kill switch enabled deliberately retains protection; use --recover from this same installation/account.");var enter=Task.Run(Console.ReadLine);var ended=broker.WaitForExitAsync();if(await Task.WhenAny(enter,ended)==ended){Console.WriteLine("Network broker ended. Recovery may be required; run --recover from this installation/account.");return 1;}var stopped=await Send(new(){Action="stop",Session=result.Session==Guid.Empty?session:result.Session});Console.WriteLine(JsonSerializer.Serialize(stopped));return stopped.Error.Length==0?0:1;}
    return result.Error.Length==0?0:1;
}catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{error=e.Code}));return 1;}catch{Console.WriteLine("{\"error\":\"BROKER_LAB_FAILED\"}");return 1;}
