using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunDnsPoolVerifiedSmoke(string report, string input)
    {
        var checks=new Dictionary<string,bool>(); var before=new Rovunet.Platform.WinInetProxy().Read();string fixtureAction="pool";
        try {
            var request=JsonSerializer.Deserialize<Dictionary<string,string>>(input)!;
            fixtureAction=request.GetValueOrDefault("action","pool");
            var profile=Importer.Single(request["raw"]);var fields=Importer.Fields(profile.Raw);
            var entries=DnsPool.Parse(request["pool"]);
            if(fields[3]!="t.example.invalid" || fields[1] is not ("dnstt" or "sayedns" or "soar" or "soar_ssh" or "slipstream" or "slipstream_ssh") || entries.Any(e=>!IPAddress.IsLoopback(DnsProbe.Endpoint(e).Address))) throw new SafeError("INVALID_IMPORT");
            string original=profile.Raw;
            if (request.TryGetValue("certificate", out var cert)) data.SlipCertificates[SlipCertificate.Scope(profile)] = SlipCertificate.ReadPublic(cert);
            if (request.TryGetValue("hostKey", out var key)) data.SshKeys[profile.Id] = key;
            string priorKeys=JsonSerializer.Serialize(data.SshKeys), priorCertificates=JsonSerializer.Serialize(data.SlipCertificates);
            data.DnsPoolEnabled=true;data.DnsPoolVerify=true;data.DnsPoolList=request["pool"];data.LatencyUrl="https://example.com/";
            Profile effective;
            if (request.GetValueOrDefault("action") == "probe-fallback")
            {
                effective=await RunProbeFallbackNetwork(profile,entries,checks);
            }
            else if (request.GetValueOrDefault("action") == "global")
            {
                var plan = GlobalResolvers.ForConnection(profile,true,request["pool"]); effective = plan.Effective;
                await using var inspector = new AgentClient(probeOnly:true); await inspector.Start();
                var connected = await inspector.Send(new() { Action="connect", Profile=effective, SlipCertificate=StoredSlipPin(effective), HostKey=data.SshKeys.GetValueOrDefault(profile.Id,"") });
                checks["global_actual_agent_connected"] = connected.State == "connected" && plan.Notice.Length == 0;
                checks["global_actual_https_verified"] = connected.State == "connected" && (await inspector.Send(new() { Action="probe", ProbeUrl=data.LatencyUrl })).Verified;
            }
            else if(request.GetValueOrDefault("action")=="manual-probe")effective=await RunManualProbeNetworkFixture(profile,entries,checks);
            else if(request.GetValueOrDefault("action")=="collection") effective=await RunCollectionNetworkFixture(profile,entries,request["direct"],checks);
            else if(request.GetValueOrDefault("action")=="scanner") {
                effective=await RunScannerFixture(profile,entries,checks);
                await using var combined=new AgentClient(probeOnly:true);await combined.Start();var ready=await combined.Send(new(){Action="connect",Profile=effective,SlipCertificate=StoredSlipPin(effective),HostKey=data.SshKeys.GetValueOrDefault(profile.Id,"")});
                checks["scanner_selected_profile_actual_https"]=ready.State=="connected"&&(await combined.Send(new(){Action="probe",ProbeUrl=data.LatencyUrl})).Verified;
            }
            else effective=await ApplyDnsPool(profile);
            if (request.GetValueOrDefault("reject") == "true")
            {
                checks["untrusted_pool_keeps_original"] = ReferenceEquals(effective,profile) && lastError == "DNS_POOL_FALLBACK";
                checks["unknown_identity_not_saved"] = JsonSerializer.Serialize(data.SshKeys)==priorKeys && JsonSerializer.Serialize(data.SlipCertificates)==priorCertificates;
            }
            else checks["full_verification_selected_loopback_resolver"]=!ReferenceEquals(effective,profile)&&(fixtureAction=="scanner"?Importer.Fields(effective.Raw)[4].Split(',').ToHashSet().SetEquals(entries.Select(e=>e+":0")):Importer.Fields(effective.Raw)[4]==entries[0]+":0");
            checks["original_profile_preserved"]=profile.Raw==original;
            checks["proxy_unchanged"]=before==new Rovunet.Platform.WinInetProxy().Read();
        } catch(Exception error){checks["failure_"+error.GetType().Name]=false;}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);
        using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite=fixtureAction=="probe-fallback"?"WPF real primary timeout, alternate HTTPS, strict failure and cancellation":fixtureAction=="manual-probe"?"WPF HTTPS cancellation preserves active Agent tunnel":fixtureAction=="collection"?"WPF inline collection + actual Agent direct-first and DNS fallback":fixtureAction=="scanner"?"WPF scanner controls + worker + Agent tunnel":"WPF DNS pool verified Agent tunnel",external_network_tested=passed&&checks.GetValueOrDefault("full_verification_selected_loopback_resolver"),target="https://example.com/",checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
    }
    async Task<Profile> RunCollectionNetworkFixture(Profile profile,string[] entries,string directEndpoint,Dictionary<string,bool> checks)
    {
        if(!IPAddress.IsLoopback(IPEndPoint.Parse(directEndpoint).Address))throw new SafeError("INVALID_IMPORT");
        var effective=GlobalResolvers.ForConnection(profile,true,entries[0]).Effective;
        var bad=Importer.Single("socks5://127.0.0.1:1#Closed%20direct");var good=Importer.Single("socks5://"+directEndpoint+"#Direct%20fixture");
        foreach(var p in new[]{effective,bad,good}){p.CollectionId="network-flow";p.CollectionName="Network fixture";}
        data.Profiles=[bad,effective];data.SelectedId=bad.Id;data.ExpandedCollections["network-flow"]=true;networkFixtureMode=true;Build();
        try{
            groups[0].Test.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await collectionTask!.WaitAsync(TimeSpan.FromSeconds(65));
            var job=collectionRuns["network-flow"];
            checks["actual_closed_direct_failed_before_dns"]=job.Results.TryGetValue(bad.Id,out var failed)&&!failed.Verified;
            checks["actual_dns_https_verified_on_card"]=job.Results.TryGetValue(effective.Id,out var passed)&&passed.Verified&&job.Completed==2&&job.Passed==1&&measurements[effective.Id].Verified;
            checks["actual_group_no_popup"]=NavigationDepth==0&&DialogWindow.OpenPages.Count==0;
            data.Profiles=[good,effective];data.SelectedId=good.Id;BuildCards();StartCollectionTest(data.Profiles,1,false);await collectionTask!.WaitAsync(TimeSpan.FromSeconds(40));job=collectionRuns["network-flow"];
            checks["actual_direct_https_skips_dns"]=job.Results.TryGetValue(good.Id,out var direct)&&direct.Verified&&job.Completed==1&&job.Pending[effective.Id]=="GROUP_DNS_SKIPPED";
            data.Profiles=[effective];data.SelectedId=effective.Id;BuildCards();StartCollectionTest(data.Profiles,1,false);
            var until=DateTime.UtcNow.AddSeconds(5);while(testing?.AgentProcessId==null&&DateTime.UtcNow<until)await Task.Delay(10);int? process=testing?.AgentProcessId;
            groups[0].Test.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await collectionTask!.WaitAsync(TimeSpan.FromSeconds(5));
            checks["actual_agent_cancel_from_card"]=process.HasValue&&collectionRuns["network-flow"].Cancelled&&testing==null&&!busy;
            bool gone=true;if(process.HasValue)try{using var owned=System.Diagnostics.Process.GetProcessById(process.Value);gone=owned.HasExited;}catch(ArgumentException){}
            checks["cancel_owned_agent_exited"]=gone;
        }finally{CancelCollectionTest();networkFixtureMode=false;}
        return effective;
    }
    public async Task<bool> RunDnsPoolSmoke(string report)
    {
        var checks = new Dictionary<string,bool>();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        string endpoint = udp.Client.LocalEndPoint!.ToString()!;
        var server = Task.Run(async () => {
            try { while(!stop.IsCancellationRequested) {
                var packet = await udp.ReceiveAsync(stop.Token); var bytes = packet.Buffer;
                if(bytes.Length >= 12) { bytes[2]=0x81;bytes[3]=0x80;await udp.SendAsync(bytes,packet.RemoteEndPoint,stop.Token); }
            }} catch(OperationCanceledException) { }
        });
        try
        {
            var profile = Importer.Single(ManualProfiles.Dns("dnstt","Pool fixture","t.example.invalid","127.0.0.1:1","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));
            string raw = profile.Raw;
            data.DnsPoolEnabled=true;data.DnsPoolList="127.0.0.1:1\n"+endpoint;
            var effective = await ApplyDnsPool(profile);
            checks["actual_dns_pool_selects_responding_candidate"] = Importer.Fields(effective.Raw)[4] == endpoint+":0";
            checks["pool_does_not_persist_profile"] = profile.Raw==raw;
            data.GlobalResolverEnabled=true;data.GlobalResolverList="1.1.1.1";
            var skipped = await ApplyDnsPool(profile);
            checks["global_override_preempts_pool"] = ReferenceEquals(skipped,profile);
            data.GlobalResolverEnabled=false;data.DnsPoolList="127.0.0.1:1";
            var fallback = await ApplyDnsPool(profile);
            checks["empty_pool_result_preserves_manual_profile"] = ReferenceEquals(fallback,profile) && lastError=="DNS_POOL_FALLBACK";
            data.DnsPoolList=endpoint; data.DnsPoolVerify=true;
            // A synthetic key with no tunnel server must not pass as full verification.
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            var pending = ApplyDnsPool(profile); poolStop?.Cancel();
            try { await pending; checks["pool_user_cancel_is_propagated"]=false; }
            catch(OperationCanceledException) { checks["pool_user_cancel_is_propagated"]=true; }
        }
        catch(Exception error) { checks["failure_"+error.GetType().Name]=false; }
        finally { poolStop?.Cancel(); stop.Cancel(); await server; }
        bool passed=checks.Count>0&&checks.Values.All(v=>v);
        using var output=new FileStream(report,FileMode.CreateNew);
        JsonSerializer.Serialize(output,new{suite="actual DNS pool WPF orchestration",external_network_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
    }
}
