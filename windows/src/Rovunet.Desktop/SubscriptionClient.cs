using System.Net;
using System.Text;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed class SubscriptionClient
{
    internal static Func<int,HttpMessageHandler>? HttpFixture;
    public sealed record Payload(string Body,DateTimeOffset? Date,string UserInfo)
    {
        public override string ToString()=>"SubscriptionPayload(redacted)";
    }
    public static async Task<Payload> Fetch(Uri uri, int proxyPort = 0, int maxBytes = Importer.MaxBytes, CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var handler = HttpFixture?.Invoke(proxyPort) ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = proxyPort > 0, Proxy = proxyPort > 0 ? new WebProxy($"http://127.0.0.1:{proxyPort}") : null, ConnectTimeout = TimeSpan.FromSeconds(10) };
        using var client = new HttpClient(handler);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new SafeError("STATUS_NOT_FOUND");
            if (!response.IsSuccessStatusCode) throw new SafeError("FETCH_FAILED");
            if (response.Content.Headers.ContentLength > maxBytes) throw new SafeError("IMPORT_TOO_LARGE");
            using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var output = new MemoryStream(); byte[] bytes = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0) { if (output.Length + count > maxBytes) throw new SafeError("IMPORT_TOO_LARGE"); output.Write(bytes, 0, count); }
            string userInfo=response.Headers.TryGetValues("Subscription-Userinfo",out var values)?string.Join(';',values):"";
            return new(new UTF8Encoding(false, true).GetString(output.ToArray()), response.Headers.Date,userInfo.Length<=2048?userInfo:"");
        }
        catch (SafeError) { throw; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw new SafeError("CANCELLED"); }
        catch { throw new SafeError("FETCH_FAILED"); }
    }
}
