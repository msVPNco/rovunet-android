using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    bool reviewingSlipIdentity;

    string StoredSlipCertificate(Profile profile)
    {
        if(!TransportPlan.UsesSlip(profile.Raw))return "";
        return data.SlipCertificates.GetValueOrDefault(SlipCertificate.Scope(profile),
            data.SlipCertificates.GetValueOrDefault(profile.Id,""));
    }

    async Task EnsureSlipIdentity(Profile profile,CancellationToken cancellation,bool review=false)
    {
        if(!TransportPlan.UsesSlip(profile.Raw))return;
        string known=StoredSlipCertificate(profile);
        if(known.Length>0&&!review)return;
        if(reviewingSlipIdentity)throw new SafeError("MAC_OPERATION_BUSY");
        reviewingSlipIdentity=true;
        try
        {
            message=L.Text("MAC_SLIP_FETCHING");UpdateStatus();
            string pem=SlipCertificate.ReadPublic(await discoverSlipCertificate(profile,cancellation));
            cancellation.ThrowIfCancellationRequested();
            if(exiting)return;
            using var certificate=X509Certificate2.CreateFromPem(pem);
            string fingerprint=certificate.GetCertHashString(HashAlgorithmName.SHA256);
            bool changed=known.Length>0&&SlipCertificate.ReadPublic(known)!=pem;
            string prompt=L.Text(changed?"MAC_SLIP_IDENTITY_CHANGED":"MAC_SLIP_TRUST_REVIEW")+"\n\n"+
                Importer.Fields(profile.Raw)[3]+"\nSHA256 "+fingerprint;
            if(!await Confirm(prompt,cancellation))throw new OperationCanceledException();
            cancellation.ThrowIfCancellationRequested();
            var current=data.Profiles.FirstOrDefault(p=>p.Id==profile.Id);
            if(current!=null&&current.Raw!=profile.Raw)throw new SafeError("MAC_SCAN_PROFILE_CHANGED");
            Commit(next=>{
                next.SlipCertificates[SlipCertificate.Scope(profile)]=pem;
                next.SlipCertificates.Remove(profile.Id);
            });
            message=L.Text("SETTINGS_SAVED");UpdateStatus();
        }
        finally{reviewingSlipIdentity=false;}
    }

    async Task ReviewSlipIdentity(Profile profile)
    {
        RequireIdle();
        await Guard(()=>EnsureSlipIdentity(profile,CancellationToken.None,true));
    }
}
