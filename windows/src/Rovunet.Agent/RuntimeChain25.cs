using Rovunet.Core;
using Rovunet.Platform;
using System.Net;
using System.Net.Sockets;

namespace Rovunet.Agent;
public sealed partial class Runtime
{
    readonly List<Runtime> chainStages=[];
    async Task<AgentReply> ConnectChain25(Profile profile,bool applyProxy,CancellationToken ct,IReadOnlyList<ChainIdentity>? identities,int localHttp,int localSocks)
    {
        var (_,layers)=ChainProfiles.Read(profile.Raw);
        if(identities!=null&&identities.Count!=0&&identities.Count!=layers.Count)throw new SafeError("INVALID_MESSAGE");
        if(localHttp is <0 or >65535||localSocks is <0 or >65535||localHttp!=0&&localHttp==localSocks)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        var watch=System.Diagnostics.Stopwatch.StartNew();int previousSocks=0,previousHttp=0;
        var reservations=new List<TcpListener>();
        try{
            // Hold the requested final ports while random intermediate endpoints
            // are allocated, so a preceding layer cannot accidentally claim one.
            foreach(int port in new[]{localHttp,localSocks})if(port>0){var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();reservations.Add(listener);}
            for(int i=0;i<layers.Count;i++){
                ct.ThrowIfCancellationRequested();var stage=new Runtime(probeOnly:true);chainStages.Add(stage);stage.UnexpectedExit+=ChildChainExited25;
                if(i==layers.Count-1)foreach(var listener in reservations)listener.Stop();
                var identity=identities!=null&&identities.Count>i?identities[i]:new();
                var result=await stage.Connect(layers[i],false,ct,identity.HostKey,identity.SlipCertificate,identity.SlipInterval,identity.SshTlsCa,i==layers.Count-1?localHttp:0,i==layers.Count-1?localSocks:0,previousSocks,previousHttp);
                if(result.State!="connected"){result.ChainLayerIndex=i;Stop();return result;}
                previousSocks=result.SocksPort;previousHttp=result.HttpPort;
            }
            httpPort=previousHttp;socksPort=previousSocks;probeBudget=TimeSpan.FromSeconds(45);
            if(applyProxy&&!probeOnly){Recovery.Arm();new ProxyLease(new WinInetProxy(),new LeaseStore(),AppContext.BaseDirectory).Acquire(httpPort);}
            return new(){State="connected",HttpPort=httpPort,SocksPort=socksPort,ElapsedMs=watch.ElapsedMilliseconds};
        }catch(SocketException error)when(error.SocketErrorCode==SocketError.AddressAlreadyInUse){Stop();throw new SafeError("LOCAL_PROXY_PORT_BUSY");}
        catch{Stop();throw;}
        finally{foreach(var listener in reservations)listener.Stop();}
    }
    void ChildChainExited25()=>UnexpectedExit?.Invoke();
    void StopChain25()
    {
        for(int i=chainStages.Count-1;i>=0;i--){chainStages[i].UnexpectedExit-=ChildChainExited25;chainStages[i].Dispose();}
        chainStages.Clear();
    }
}
