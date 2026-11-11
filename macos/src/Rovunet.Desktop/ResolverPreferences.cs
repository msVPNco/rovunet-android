using System.Diagnostics;
using Avalonia.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void ShowResolverPreferences(bool pool)
    {
        var page=Page();page.Children.Add(Label(L.Text(pool?"MAC_POOL_HINT":"MAC_GLOBAL_DNS_HINT"),13,"Muted"));
        var enabled=new CheckBox{Content=L.Text(pool?"DNS_POOL_TITLE":"GLOBAL_RESOLVER_TITLE"),IsChecked=pool?data.DnsPoolEnabled:data.GlobalResolverEnabled,MinHeight=44};page.Children.Add(enabled);
        var input=Field(page,"DNS_RESOLVERS",pool?data.DnsPoolList:data.GlobalResolverList,true);input.Name="ResolverPreferenceList";
        var verified=new CheckBox{Content=L.Text("MAC_POOL_HTTPS"),IsChecked=data.DnsPoolVerify,MinHeight=44};if(pool)page.Children.Add(verified);
        page.Children.Add(ActionButton("DNS_LOCAL_NETWORK",()=>{string[] values=MacDnsEditing.NetworkResolvers();if(values.Length==0)throw new SafeError("DNS_LOCAL_UNAVAILABLE");input.Text=string.Join('\n',values);}));
        page.Children.Add(ActionButton("DNS_BANK_TITLE",()=>ShowResolverBanks(data.ResolverBank,file=>input.Text=string.Join('\n',ResolverCatalog.Candidates(file,"tcp",pool?64:16)))));
        page.Children.Add(ActionButton("FIND_DNS",()=>BuildScanner(Selected,null,(text,wire)=>{
            if(pool&&wire is not("udp" or "tcp"))throw new SafeError("MAC_POOL_WIRE");
            input.Text=pool?string.Join('\n',DnsPool.Parse(text)):GlobalResolvers.Normalize(text);
        })));
        page.Children.Add(ActionButton("SAVE",()=>{RequireIdle();string text=pool?string.Join('\n',DnsPool.Parse(input.Text??"")):GlobalResolvers.Normalize(input.Text??"");if(enabled.IsChecked==true&&text.Length==0)throw new SafeError("DNS_IP_REQUIRED");Commit(d=>{if(pool){d.DnsPoolEnabled=enabled.IsChecked==true;d.DnsPoolList=text;d.DnsPoolVerify=verified.IsChecked==true;}else{d.GlobalResolverEnabled=enabled.IsChecked==true;d.GlobalResolverList=text;}});message=L.Text("SETTINGS_SAVED");UpdateStatus();},true));Navigate(pool?"DNS_POOL_TITLE":"GLOBAL_RESOLVER_TITLE",page);
    }
    async Task<Profile> ApplyDnsPool(Profile profile,CancellationToken stop)
    {
        if(!data.DnsPoolEnabled||data.GlobalResolverEnabled&&data.GlobalResolverList.Trim().Length>0||!ProfileEditing.IsDns(profile))return profile;
        var fields=Importer.Fields(profile.Raw);if(fields[22] is not("udp" or "tcp"))return profile;
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(stop);budget.CancelAfter(data.DnsPoolVerify?180000:8000);
        var measured=new System.Collections.Concurrent.ConcurrentDictionary<string,PoolProbe>();
        try{
            var trust=new TunnelScanTrust(data.SshKeys.GetValueOrDefault(SshHostKey.Scope(profile),""),StoredSlipCertificate(profile));
            async Task<PoolProbe> Probe(string endpoint,CancellationToken ct){PoolProbe result;if(data.DnsPoolVerify){var proof=await TunnelScanner.Verify(profile,endpoint,fields[22],data.ProbeUrl,trust,115000,ct);result=new(proof.Verified,proof.ElapsedMs);}else{var proof=await DnsProbe.Run(endpoint,fields[22],ct,1500);result=new(proof.Replied,proof.Milliseconds);}measured[endpoint]=result;return result;}
            string[] selected;
            try{selected=await DnsPool.Select(DnsPool.Parse(data.DnsPoolList),Probe,budget.Token);}catch(OperationCanceledException)when(!stop.IsCancellationRequested){selected=measured.Where(p=>p.Value.Passed).OrderBy(p=>p.Value.Milliseconds).Take(8).Select(p=>p.Key).ToArray();}
            stop.ThrowIfCancellationRequested();if(selected.Length==0){message=L.Text("DNS_POOL_FALLBACK");return profile;}
            var applied=GlobalResolvers.ForConnection(profile,true,string.Join('\n',selected));if(applied.Notice.Length>0)message=L.Text(applied.Notice);return applied.Effective;
        }catch(OperationCanceledException){throw;}catch{message=L.Text("DNS_POOL_FALLBACK");return profile;}
    }
    bool checkingTun;
    async Task InstallTunHelper()
    {
        RequireIdle();if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        if(checkingTun)return;checkingTun=true;try{
        if(await SystemTunLease.InstalledAndReady()){message=L.Text("MAC_TUN_ALREADY");UpdateStatus();return;}
        string package=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","Resources","Rovunet-TUN.pkg"));if(!File.Exists(package))throw new SafeError("MAC_TUN_HELPER_REQUIRED");
        Process.Start(new ProcessStartInfo("/usr/bin/open"){UseShellExecute=false,ArgumentList={package}});message=L.Text("MAC_TUN_INSTALL_HINT");UpdateStatus();}finally{checkingTun=false;}
    }
}
