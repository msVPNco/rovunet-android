using System.Net;
using System.Net.Sockets;
using Rovunet.Core;

namespace Rovunet.NetworkBroker;

internal static class SocksUdp30
{
    // RFC1928 UDP ASSOCIATE on the already verified loopback listener. This
    // checks the local capability, not delivery to every remote destination.
    // Nothing from the returned relay address is used for a privileged action.
    public static async Task Require(int port,CancellationToken stop)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop);deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try{
            using var client=new TcpClient();await client.ConnectAsync(IPAddress.Loopback,port,deadline.Token);using var stream=client.GetStream();
            await stream.WriteAsync(new byte[]{5,1,0},deadline.Token);byte[] method=new byte[2];await stream.ReadExactlyAsync(method,deadline.Token);
            if(method[0]!=5||method[1]!=0)throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");
            await stream.WriteAsync(new byte[]{5,3,0,1,127,0,0,1,0,0},deadline.Token);byte[] header=new byte[4];await stream.ReadExactlyAsync(header,deadline.Token);
            if(header[0]!=5||header[1]!=0||header[2]!=0)throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");
            int length=header[3] switch{1=>4,4=>16,3=>-1,_=>throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED")};
            if(length<0){byte[] count=new byte[1];await stream.ReadExactlyAsync(count,deadline.Token);length=count[0];if(length==0)throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");}
            byte[] relay=new byte[length+2];await stream.ReadExactlyAsync(relay,deadline.Token);
            if(relay[^1]==0&&relay[^2]==0)throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");
        }catch(OperationCanceledException)when(stop.IsCancellationRequested){throw;}
        catch{throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");}
    }
}
