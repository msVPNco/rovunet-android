using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Rovunet.Core;

public sealed record NetworkAttachment30(string Id,string Name,bool Loopback,string State,string[] Addresses,string[] Gateways);

public static class NetworkEpoch30
{
    // Creating/removing our own TUN must not terminate the selected upstream.
    // Other adapters, including another VPN, remain part of the network epoch.
    public static string Fingerprint(IEnumerable<NetworkAttachment30> interfaces)
    {
        var rows=interfaces.Where(i=>!i.Loopback&&!(i.Name==TunPlan29.Adapter&&Guid.TryParse(i.Id,out var id)&&id==TunPlan29.AdapterGuid))
            .OrderBy(i=>i.Id,StringComparer.Ordinal)
            .Select(i=>new{i.Id,i.Name,i.State,Addresses=i.Addresses.Distinct().Order(StringComparer.Ordinal).ToArray(),Gateways=i.Gateways.Distinct().Order(StringComparer.Ordinal).ToArray()}).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows))));
    }
}
