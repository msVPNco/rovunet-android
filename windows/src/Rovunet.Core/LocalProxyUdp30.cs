namespace Rovunet.Core;

public static class LocalProxyUdp30
{
    // Explicit opt-in for the upstream exercised by the native service lab.
    // Other transports need their own UDP data acceptance; TCP readiness is
    // insufficient. Reparse rather than trusting caller-supplied Protocol.
    public static void Require(Profile profile,bool enabled)
    {
        if(!enabled)return;
        var canonical=Importer.Single(profile.Raw);
        if(!canonical.CanConnect||canonical.Protocol!="SOCKS5"||ChainProfiles.IsChain(canonical.Raw))
            throw new SafeError("UDP_UPSTREAM_NOT_VERIFIED");
    }
}
