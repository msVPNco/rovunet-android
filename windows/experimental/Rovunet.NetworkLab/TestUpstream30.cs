using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;

namespace Rovunet.NetworkLab;
// VM-only controlled SOCKS fixture. It does not load owner profiles or accept
// arbitrary executable/config paths, and is not installed with the product.
internal static class TestUpstream30
{
    public static async Task<int> Run(string[] args)
    {
        try{
            if(args.Length is not(5 or 6)||args[4]!="--accept-isolated-lab"||args.Length==6&&args[5]!="--tcp-only"||!IPAddress.TryParse(args[1],out var peer)||!int.TryParse(args[2],out int remote)||!int.TryParse(args[3],out int local)||remote is <1024 or >65535)return 12;
            TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="start",SocksPort=local,Peers=[peer.ToString()],ExperimentalAcknowledged=true});
            string root=TrustedInstallation29.Root();TrustedInstallation29.RequireLab(root);TrustedInstallation29.Manifest(root);
            var config=new JsonObject{
                ["log"]=new JsonObject{["loglevel"]="none"},
                ["inbounds"]=new JsonArray(new JsonObject{["listen"]="127.0.0.1",["port"]=local,["protocol"]="socks",["settings"]=new JsonObject{["auth"]="noauth",["udp"]=!args.Contains("--tcp-only")}}),
                ["outbounds"]=new JsonArray(new JsonObject{["protocol"]="socks",["settings"]=new JsonObject{["servers"]=new JsonArray(new JsonObject{["address"]=peer.ToString(),["port"]=remote})}})
            };
            using var job=new ProcessJob();using var child=Process.Start(new ProcessStartInfo(Path.Combine(root,"engines","xray.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-c","stdin:"}})??throw new SafeError("CORE_START_FAILED");job.Assign(child);
            _=child.StandardOutput.ReadToEndAsync();_=child.StandardError.ReadToEndAsync();await child.StandardInput.WriteAsync(config.ToJsonString());child.StandardInput.Close();
            for(int i=0;i<100;i++){
                if(child.HasExited)throw new SafeError("CORE_START_FAILED");
                try{using var socket=new TcpClient();await socket.ConnectAsync(IPAddress.Loopback,local);Console.WriteLine("{\"ready\":true,\"fixture\":true,\"ownerDataUsed\":false}");break;}catch(SocketException){await Task.Delay(100);}
            }
            await child.WaitForExitAsync();return child.ExitCode;
        }catch(SafeError e){Console.WriteLine("{\"error\":\""+e.Code+"\"}");return 1;}catch{Console.WriteLine("{\"error\":\"LAB_UPSTREAM_FAILED\"}");return 1;}
    }
}
