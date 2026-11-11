namespace Rovunet.Core;

public static class HttpsProbePolicy
{
    public static string[] Targets(string configured,bool fallbacks)
    {
        if(!Uri.TryCreate(configured,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Fragment.Length>0)throw new SafeError("HTTPS_REQUIRED");
        var targets=new List<string>{uri.AbsoluteUri};
        if(fallbacks)foreach(string candidate in new[]{"https://cp.cloudflare.com/generate_204","https://example.com/"})if(!targets.Any(t=>new Uri(t).IdnHost.Equals(new Uri(candidate).IdnHost,StringComparison.OrdinalIgnoreCase)))targets.Add(candidate);
        return targets.ToArray();
    }
    public static bool Unconfirmed(string error)=>error is "" or "PROBE_PARTIAL" or "PROBE_TIMEOUT" or "TEST_TIMEOUT" or "PROBE_TLS_FAILED" or "PROBE_DNS_FAILED" or "HTTPS_STATUS_FAILED" or "HTTPS_TEST_FAILED" or "PROBE_PROXY_FAILED" or "SSH_TRUST_REQUIRED" or "SLIP_CERT_REQUIRED";
}
