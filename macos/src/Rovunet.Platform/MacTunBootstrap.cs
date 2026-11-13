using System.Net;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

// Resolve only the outer proxy endpoint, on the network in place before this
// lease. Keep the configured hostname for TLS, REALITY, WS and XHTTP. Xray's
// Darwin system dialer receives the session-local IPs via its own DNS hosts.
public static class MacTunBootstrap
{
    public static async Task<string> Prepare(string generated, CancellationToken cancellation,
        Func<string,CancellationToken,Task<IPAddress[]>>? resolve = null)
    {
        var root = JsonNode.Parse(generated)!.AsObject();
        var outbound = root["outbounds"]!.AsArray().OfType<JsonObject>().Single(o => o["tag"]?.ToString() == "proxy");
        var settings = outbound["settings"] as JsonObject;
        string? host = outbound["protocol"]?.ToString() switch {
            "vless" or "vmess" => settings?["vnext"]?[0]?["address"]?.ToString(),
            "trojan" or "shadowsocks" or "socks" => settings?["servers"]?[0]?["address"]?.ToString(),
            "hysteria" => settings?["address"]?.ToString(),
            _ => null
        };
        if (string.IsNullOrEmpty(host) || IPAddress.TryParse(host.Trim('[',']'), out _)) return generated;
        if (Uri.CheckHostName(host) != UriHostNameType.Dns) throw new SafeError("MAC_TUN_BOOTSTRAP_FAILED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        IPAddress[] addresses;
        try { addresses = await (resolve ?? Dns.GetHostAddressesAsync)(host,deadline.Token).ConfigureAwait(false); }
        catch (Exception error) when (error is System.Net.Sockets.SocketException or OperationCanceledException) {
            cancellation.ThrowIfCancellationRequested(); throw new SafeError("MAC_TUN_BOOTSTRAP_FAILED");
        }
        var ips = addresses.Where(a => !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any) && !(a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6 && a.ScopeId!=0)).Distinct().Take(16).ToArray();
        if (ips.Length == 0) throw new SafeError("MAC_TUN_BOOTSTRAP_FAILED");
        var stream = outbound["streamSettings"] as JsonObject;
        if (stream == null) { stream = new JsonObject(); outbound["streamSettings"] = stream; }
        var sockopt = stream["sockopt"] as JsonObject;
        if (sockopt == null) { sockopt = new JsonObject(); stream["sockopt"] = sockopt; }
        sockopt["domainStrategy"] = "ForceIP";
        var dns = root["dns"] as JsonObject;
        if (dns == null) { dns = new JsonObject(); root["dns"] = dns; }
        var hosts = dns["hosts"] as JsonObject;
        if (hosts == null) { hosts = new JsonObject(); dns["hosts"] = hosts; }
        hosts["full:" + host] = new JsonArray(ips.Select(a => (JsonNode?)JsonValue.Create(a.ToString())).ToArray());
        return root.ToJsonString();
    }
}
