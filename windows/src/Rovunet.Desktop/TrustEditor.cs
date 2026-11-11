using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    string StoredSshKey(Profile p)=>SshHostKey.Required(p)&&(!data.SshKeyScopes.TryGetValue(p.Id,out var scope)||scope==SshHostKey.Scope(p))?data.SshKeys.GetValueOrDefault(p.Id,""):"";
    string StoredSshTlsCa(Profile p)=>SshHostKey.UsesTls(p)?data.SshTlsCertificates.GetValueOrDefault(SshHostKey.TlsScope(p),""):"";
    sealed class TrustDraft
    {
        internal required Func<Profile,Action> Prepare;
    }
    TrustDraft AddTrustEditor(Panel panel,Profile profile)
    {
        bool ssh=SshHostKey.Required(profile),slip=TransportPlan.UsesSlip(profile.Raw),tls=SshHostKey.UsesTls(profile);
        string originalKey=ssh?StoredSshKey(profile):"",originalCertificate=slip?StoredSlipPin(profile):"";
        string originalTlsCa=tls?StoredSshTlsCa(profile):"";TextBox? key=null,certificate=null,tlsCertificate=null;
        if(ssh||slip){panel.Children.Add(Text(L.Text("SERVER_IDENTITY"),20));panel.Children.Add(Text(L.Text("SERVER_IDENTITY_HINT"),14,Muted));}
        TextBox PublicField(string label,string initial,bool multiline){var box=new TextBox{Text=initial,Tag=label,FlowDirection=FlowDirection.LeftToRight,AcceptsReturn=multiline,TextWrapping=TextWrapping.Wrap,MinHeight=100,MaxHeight=190,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(OutlinedField(label,box));return box;}
        void AttachFile(TextBox box,string label,string filter){panel.Children.Add(SettingsAction(label,()=>{var picker=new OpenFileDialog{Filter=filter};if(picker.ShowDialog(this)!=true)return;try{if(new FileInfo(picker.FileName).Length>32768)throw new SafeError("SSH_KEY_INVALID");box.Text=File.ReadAllText(picker.FileName);}catch{box.Text="";Notify(L.Text("IDENTITY_FILE_INVALID"));}}));}
        if(ssh){
            key=PublicField("SSH_HOST_PUBLIC_KEY",originalKey,true);var fingerprint=Technical(Text("",14));fingerprint.Tag="SSH_KEY_FINGERPRINT";panel.Children.Add(fingerprint);
            void Show(){try{string normalized=SshHostKey.Normalize(key.Text);fingerprint.Text=normalized.Length==0?L.Text("SSH_KEY_OPTIONAL"):SshHostKey.Fingerprint(normalized);fingerprint.Foreground=normalized.Length==0?Muted:Green;}catch{fingerprint.Text=L.Text("SSH_KEY_INVALID");fingerprint.Foreground=Red;}}
            key.TextChanged+=(_,_)=>Show();Show();AttachFile(key,"SSH_KEY_LOAD","Public key|*.pub;*.txt|All files|*.*");
        }
        if(slip){
            certificate=PublicField("SLIP_CERT_PEM",originalCertificate,true);var fingerprint=Technical(Text("",14));fingerprint.Tag="SLIP_CERT_FINGERPRINT";panel.Children.Add(fingerprint);
            void Show(){try{if(certificate.Text.Trim().Length==0){fingerprint.Text=L.Text("SLIP_CERT_REQUIRED");fingerprint.Foreground=Muted;return;}using var cert=X509Certificate2.CreateFromPem(SlipCertificate.ReadPublic(certificate.Text));fingerprint.Text="SHA256 "+cert.GetCertHashString(HashAlgorithmName.SHA256);fingerprint.Foreground=Green;}catch{fingerprint.Text=L.Text("SLIP_CERT_INVALID");fingerprint.Foreground=Red;}}
            certificate.TextChanged+=(_,_)=>Show();Show();AttachFile(certificate,"SLIP_CERT_CHOOSE","PEM certificate|*.pem;*.crt;*.cer|All files|*.*");
        }
        if(tls){
            tlsCertificate=PublicField("SSH_TLS_CA_PEM",originalTlsCa,true);panel.Children.Add(Text(L.Text("SSH_TLS_CA_HINT"),14,Muted));var fingerprint=Technical(Text("",14));panel.Children.Add(fingerprint);
            void Show(){try{if(tlsCertificate.Text.Trim().Length==0){fingerprint.Text=L.Text("TLS_SYSTEM_TRUST");fingerprint.Foreground=Muted;return;}using var cert=X509Certificate2.CreateFromPem(SlipCertificate.ReadPublic(tlsCertificate.Text));fingerprint.Text="SHA256 "+cert.GetCertHashString(HashAlgorithmName.SHA256);fingerprint.Foreground=Green;}catch{fingerprint.Text=L.Text("IDENTITY_FILE_INVALID");fingerprint.Foreground=Red;}}
            tlsCertificate.TextChanged+=(_,_)=>Show();Show();AttachFile(tlsCertificate,"TLS_CERT_LOAD","PEM certificate|*.pem;*.crt;*.cer|All files|*.*");
        }
        return new(){Prepare=next=>{
            string normalizedKey=key==null?"":SshHostKey.Normalize(key.Text),pem=certificate==null||certificate.Text.Trim().Length==0?"":SlipCertificate.ReadPublic(certificate.Text);
            string tlsPem=tlsCertificate==null||tlsCertificate.Text.Trim().Length==0?"":SlipCertificate.ReadPublic(tlsCertificate.Text);
            // A host edit must not silently move an unchanged pin to a new server.
            if(ssh&&SshHostKey.Scope(profile)!=SshHostKey.Scope(next)&&key!.Text==originalKey)normalizedKey="";
            if(slip&&SlipCertificate.Scope(profile)!=SlipCertificate.Scope(next)&&certificate!.Text==originalCertificate)pem="";
            if(tls&&SshHostKey.TlsScope(profile)!=SshHostKey.TlsScope(next)&&tlsCertificate!.Text==originalTlsCa)tlsPem="";
            return ()=>{
                if(ssh){if(normalizedKey.Length==0){data.SshKeys.Remove(next.Id);data.SshKeyScopes.Remove(next.Id);}else{data.SshKeys[next.Id]=normalizedKey;data.SshKeyScopes[next.Id]=SshHostKey.Scope(next);}}
                if(slip){string scope=SlipCertificate.Scope(next);if(pem.Length==0)data.SlipCertificates.Remove(scope);else data.SlipCertificates[scope]=pem;}
                if(tls&&SshHostKey.UsesTls(next)){string scope=SshHostKey.TlsScope(next);if(tlsPem.Length==0)data.SshTlsCertificates.Remove(scope);else data.SshTlsCertificates[scope]=tlsPem;}
            };
        }};
    }
}
