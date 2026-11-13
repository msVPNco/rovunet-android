using Avalonia.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    async Task ToggleTlsException(Profile profile)
    {
        await Guard(async()=>{
            RequireIdle();if(busy||collectionStop!=null)throw new SafeError("MAC_TLS_EXCEPTION_IDLE");
            if(!MacTlsPolicy.Supports(profile.Raw))throw new SafeError("MAC_TLS_EXCEPTION_INVALID");
            if(MacTlsPolicy.Approved(data,profile)){
                Commit(d=>d.TlsExceptions.Remove(profile.Id));message=L.Text("MAC_TLS_VERIFICATION_ON");messageTone="Green";BuildCards();UpdateStatus();return;
            }
            string fingerprint=Importer.Fingerprint(profile.Raw);var panel=Page();panel.Children.Add(Label(profile.Name,18));panel.Children.Add(Label(L.Text("MAC_TLS_PIN_HINT"),14,"Muted"));
            var pin=Field(panel,"MAC_TLS_PIN","");pin.Name="TlsCertificatePin";
            Button? apply=null,inspect=null;
            bool Valid(){try{_=MacTlsPolicy.NormalizePin(pin.Text??"");return true;}catch{return false;}}
            pin.TextChanged+=(_,_)=>{if(apply!=null)apply.IsEnabled=Valid()&&!busy;};
            inspect=ActionButton("MAC_TLS_INSPECT_CERT",()=>_=Guard(async()=>{
                if(busy)return;busy=true;inspect!.IsEnabled=false;if(apply!=null)apply.IsEnabled=false;
                try{pin.Text=await MacTlsPolicy.InspectCertificate(profile,CancellationToken.None);message=L.Text("MAC_TLS_CERT_UNTRUSTED");messageTone="Red";UpdateStatus();}
                finally{busy=false;inspect.IsEnabled=true;if(apply!=null)apply.IsEnabled=Valid();}
            }));panel.Children.Add(inspect);
            apply=ActionButton("MAC_TLS_SAVE_CERT",()=>_=Guard(async()=>{
                RequireIdle();if(busy)return;string certificate=MacTlsPolicy.NormalizePin(pin.Text??"");
                if(!await Confirm(string.Format(L.Text("MAC_TLS_EXCEPTION_CONFIRM"),profile.Name),certificate:certificate))return;
                var current=data.Profiles.SingleOrDefault(p=>p.Id==profile.Id);
                if(current==null||Importer.Fingerprint(current.Raw)!=fingerprint)throw new SafeError("MAC_TLS_EXCEPTION_INVALID");
                Commit(d=>d.TlsExceptions[profile.Id]=new(fingerprint,certificate));message=L.Text("MAC_TLS_EXCEPTION_ACTIVE");messageTone="Red";GoHome();
            }),true);apply.Name="TlsApproveCertificate";apply.IsEnabled=false;panel.Children.Add(apply);Navigate("MAC_TLS_CERT_PAGE",panel);
        });
    }
}
