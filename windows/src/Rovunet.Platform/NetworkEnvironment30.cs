using System.Net.NetworkInformation;
using Rovunet.Core;

namespace Rovunet.Platform;

public static class NetworkEnvironment30
{
    public static string Read()=>NetworkEpoch30.Fingerprint(NetworkInterface.GetAllNetworkInterfaces().Select(adapter=>
    {
        if(adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback||!(adapter.Supports(NetworkInterfaceComponent.IPv4)||adapter.Supports(NetworkInterfaceComponent.IPv6)))
            return new NetworkAttachment30(adapter.Id,adapter.Name,true,"",[],[]);
        var properties=adapter.GetIPProperties();
        return new NetworkAttachment30(adapter.Id,adapter.Name,false,adapter.OperationalStatus.ToString(),
            properties.UnicastAddresses.Select(a=>a.Address+"/"+a.PrefixLength).ToArray(),
            properties.GatewayAddresses.Select(a=>a.Address.ToString()).ToArray());
    }));
}
