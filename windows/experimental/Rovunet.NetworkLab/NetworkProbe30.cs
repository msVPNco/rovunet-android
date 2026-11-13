using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.NetworkBroker;

namespace Rovunet.NetworkLab;
internal static class NetworkProbe30
{
    public static async Task<int> Run(string[] args)
    {
        try{
            string root=TrustedInstallation29.Root();TrustedInstallation29.RequireLab(root);TrustedInstallation29.Manifest(root);
            if(args is ["--test-interfaces","--accept-isolated-lab"]){
                Console.WriteLine(JsonSerializer.Serialize(new{expectedTunGuid=TunPlan29.AdapterGuid,interfaces=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Select(i=>new{i.Name,i.Id,state=i.OperationalStatus.ToString(),type=i.NetworkInterfaceType.ToString()}),readOnly=true}));return 0;
            }
            if(args is ["--test-probes",var phase,"--accept-isolated-lab"]&&phase is "direct" or "tunnel"){
                var hosts=phase=="direct"?new[]{"10.0.2.2","fec0::2"}:new[]{"198.18.0.10","2001:db8:1::10"};
                var probes=await Task.WhenAll(hosts.SelectMany(host=>new[]{true,false}.Select(tcp=>Probe(IPAddress.Parse(host),phase=="direct"?(tcp?18884:18885):(tcp?18880:18881),tcp))));
                Console.WriteLine(JsonSerializer.Serialize(new{phase,probes,success=probes.All(p=>p.success),synthetic=true}));return 0;
            }
            if(args is ["--test-dns",var name,"--accept-isolated-lab"]&&name.Length<=128&&name.EndsWith(".test",StringComparison.Ordinal)){
                var watch=Stopwatch.StartNew();
                try{var addresses=await Dns.GetHostAddressesAsync(name).WaitAsync(TimeSpan.FromSeconds(15));Console.WriteLine(JsonSerializer.Serialize(new{probe="system-dns",success=addresses.Any(a=>a.ToString()=="198.18.0.10"),addresses=addresses.Select(a=>a.ToString()),elapsedMs=watch.ElapsedMilliseconds,synthetic=true}));}
                catch{Console.WriteLine(JsonSerializer.Serialize(new{probe="system-dns",success=false,elapsedMs=watch.ElapsedMilliseconds,synthetic=true}));}return 0;
            }
            if(args.Length!=5||args[0]!="--test-probe"||args[4]!="--accept-isolated-lab"||!int.TryParse(args[2],out int port)||!IPAddress.TryParse(args[1],out var address)||args[3] is not("tcp" or "udp"))return 12;
            bool tcp=args[3]=="tcp";
            if(!(address.ToString() is "10.0.2.2" or "fec0::2"&&port==(tcp?18884:18885))&&!(address.ToString() is "198.18.0.10" or "2001:db8:1::10"&&port==(tcp?18880:18881)))return 12;
            Console.WriteLine(JsonSerializer.Serialize(await Probe(address,port,tcp)));return 0;
        }catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{error=e.Code}));return 1;}catch{Console.WriteLine("{\"error\":\"LAB_PROBE_FAILED\"}");return 1;}
    }
    internal sealed record ProbeResult(string probe,string address,int port,bool success,int bytesSent,int bytesReceived,string error,long elapsedMs,bool systemProxyUsed=false,bool synthetic=true);
    internal static async Task<ProbeResult> Probe(IPAddress address,int port,bool tcp)
    {
            var timer=Stopwatch.StartNew();bool success=false;string error="";int sent=0,received=0;
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(3));byte[] payload=RandomNumberGenerator.GetBytes(tcp?16384:1200);
            try{
                if(tcp){
                    using var client=new TcpClient(address.AddressFamily);await client.ConnectAsync(address,port,stop.Token);using var stream=client.GetStream();
                    byte[] frame=new byte[4+payload.Length];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(frame,payload.Length);payload.CopyTo(frame,4);
                    await stream.WriteAsync(frame,stop.Token);sent=payload.Length;byte[] reply=new byte[frame.Length];await stream.ReadExactlyAsync(reply,stop.Token);received=reply.Length-4;success=reply.AsSpan().SequenceEqual(frame);
                }else{
                    using var client=new UdpClient(address.AddressFamily);client.Connect(new IPEndPoint(address,port));sent=await client.SendAsync(payload,stop.Token);var reply=await client.ReceiveAsync(stop.Token);received=reply.Buffer.Length;success=reply.Buffer.AsSpan().SequenceEqual(payload);
                }
            }catch(Exception e){error=e is OperationCanceledException?"timeout":e is SocketException socket?socket.SocketErrorCode.ToString():e.GetType().Name;}
            return new(tcp?"tcp":"udp",address.ToString(),port,success,sent,received,error,timer.ElapsedMilliseconds);
    }
}
