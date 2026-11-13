using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Rovunet.Core;

namespace Rovunet.Platform;

// HTTPS still uses HttpClient's normal hostname and system certificate validation.
// Only the TCP socket is bound. An expired/missing utun must never fall back to a
// direct connection or a successful probe through the application's HTTP proxy.
public static class MacTunHttp
{
    public static bool ValidInterface(string? name) => name is { Length: >= 5 and <= 8 }
        && name.StartsWith("utun", StringComparison.Ordinal) && name.AsSpan(4).IndexOfAnyExceptInRange('0', '9') < 0;

    public static SocketsHttpHandler Handler(int localHttp, bool usingTun, string? device, TimeSpan timeout)
    {
        if (!usingTun)
        {
            if (localHttp is < 1 or > 65535) throw new SafeError("LOCAL_PROXY_PORT_INVALID");
            return new SocketsHttpHandler { UseProxy = true, Proxy = new WebProxy($"http://127.0.0.1:{localHttp}"), AllowAutoRedirect = false, ConnectTimeout = timeout };
        }
        if (!OperatingSystem.IsMacOS() || !ValidInterface(device)) throw new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
        uint index = if_nametoindex(device!);
        if (index == 0) throw new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
        return new SocketsHttpHandler {
            UseProxy = false, AllowAutoRedirect = false, ConnectTimeout = timeout,
            ConnectCallback = (ctx, cancel) => Connect(ctx.DnsEndPoint, device!, index, cancel)
        };
    }

    static async ValueTask<Stream> Connect(DnsEndPoint destination, string device, uint index, CancellationToken cancel)
    {
        if (if_nametoindex(device) != index) throw new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
        IPAddress[] addresses = IPAddress.TryParse(destination.Host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(destination.Host, cancel).ConfigureAwait(false);
        // Try both address families without letting a black-holed first address
        // consume the entire probe budget. Every losing socket is disposed.
        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        var distinct = addresses.Distinct().Where(a => !IPAddress.IsLoopback(a) && !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any)).ToArray();
        if (distinct.Length == 0) throw new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
        var ordered = distinct.Take(1).Concat(distinct.Skip(1).OrderBy(a => a.AddressFamily == distinct[0].AddressFamily)).Take(8).ToArray();
        var attempts = ordered.Select((address, i) => Dial(address, destination.Port, device, index, i, race.Token)).ToList();
        Exception? last = null;
        try {
            while (attempts.Count > 0) {
                var done = await Task.WhenAny(attempts).ConfigureAwait(false); attempts.Remove(done);
                try { return await done.ConfigureAwait(false); }
                catch (SocketException e) { last = e; }
            }
        } finally {
            race.Cancel();
            foreach (var attempt in attempts) { try { (await attempt.ConfigureAwait(false)).Dispose(); } catch (Exception) { } }
        }
        cancel.ThrowIfCancellationRequested();
        throw last ?? new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
    }

    static async Task<Stream> Dial(IPAddress address, int port, string device, uint index, int delay, CancellationToken cancel)
    {
        if (delay != 0) await Task.Delay(200 * delay, cancel).ConfigureAwait(false);
        if (if_nametoindex(device) != index) throw new SafeError("MAC_TUN_ROUTE_UNAVAILABLE");
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try {
            bool v6 = address.AddressFamily == AddressFamily.InterNetworkV6;
            if (setsockopt(socket.SafeHandle, v6 ? 41 : 0, v6 ? 125 : 25, ref index, sizeof(uint)) != 0)
                throw new SocketException(Marshal.GetLastPInvokeError());
            await socket.ConnectAsync(new IPEndPoint(address,port),cancel).ConfigureAwait(false);
            return new NetworkStream(socket,ownsSocket:true);
        } catch { socket.Dispose(); throw; }
    }
    [DllImport("libc")] static extern uint if_nametoindex([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport("libc", SetLastError=true)] static extern int setsockopt(SafeSocketHandle socket,int level,int option,ref uint value,uint length);
}
