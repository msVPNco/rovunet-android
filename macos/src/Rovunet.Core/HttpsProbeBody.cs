namespace Rovunet.Core;

public static class HttpsProbeBody
{
    public static async Task<bool> Check(HttpContent content, CancellationToken cancellation)
    {
        // Validate short declared bodies fully and bound traffic on larger probes.
        int target = (int)Math.Min(content.Headers.ContentLength ?? 1, 4096);
        using var stream = await content.ReadAsStreamAsync(cancellation);
        var buffer = new byte[target];
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellation);
            return true;
        }
        catch (IOException) { return false; }
    }
}
