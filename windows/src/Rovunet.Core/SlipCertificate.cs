using System.Security.Cryptography.X509Certificates;
namespace Rovunet.Core;

public static class SlipCertificate
{
    public static string ReadPublic(string text)
    {
        if (text.Length > 32768 || text.Contains("PRIVATE KEY", StringComparison.Ordinal)) throw new SafeError("SLIP_CERT_INVALID");
        try
        {
            using var certificate = X509Certificate2.CreateFromPem(text);
            if (DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime()) throw new SafeError("SLIP_CERT_INVALID");
            return certificate.ExportCertificatePem();
        }
        catch { throw new SafeError("SLIP_CERT_INVALID"); }
    }
    public static string Scope(Profile profile) => Importer.Fingerprint("slip/" + Importer.Fields(profile.Raw)[3].TrimEnd('.').ToLowerInvariant());
}
