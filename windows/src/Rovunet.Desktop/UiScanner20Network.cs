using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    // Explicit diagnostic entry point: read the owner's encrypted store, never show it,
    // never save it, and use isolated probe-only agents with ephemeral loopback ports.
    public async Task<bool> RunScanner20Network(string report)
    {
        var checks=new Dictionary<string,bool>();var results=new List<object>();
        var proxyBefore=new WinInetProxy().Read();var ownedBefore=Owned();
        string store=Path.Combine(PrivateStore.Root,"profiles.dpapi");
        byte[] before=SHA256.HashData(File.ReadAllBytes(store));
        try{
            var profiles=data.Profiles.Where(ProfileEditing.IsDns).GroupBy(p=>ResolverScan.Family(TransportPlan.Normalize(Importer.Fields(p.Raw)))).Select(g=>g.OrderBy(p=>SshHostKey.Required(p)).First()).ToArray();
            checks["owner_has_dns_profiles"]=profiles.Length>0;
            foreach(var profile in profiles){
                var fields=TransportPlan.Normalize(Importer.Fields(profile.Raw));string family=ResolverScan.Family(fields);
                string method=ResolverScan.TransportChoices(fields).First();if(method=="both")method="udp";
                var candidates=method=="https"?new[]{fields[21]}:ResolverScan.FromText(string.Join('\n',fields[4].Split(',').Select(x=>TransportPlan.SlipResolver(x).Address)),method,16).Take(1).Concat(new[]{"1.1.1.1:53","8.8.8.8:53"}).Distinct().ToArray();
                foreach(string mode in family=="slipstream"?new[]{"advanced","simple"}:new[]{"advanced","simple","prism"}){
                    using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(18));
                    await ResolverWorker.Run(new{Mode=mode,Family=family,Zone=fields[3],PublicKey=fields[11],Candidates=candidates,Transports=new[]{method},Workers=3,TimeoutMS=5000,Count=3,Threshold=2,ResponseSize=0,Prefilter=false},r=>{
                        results.Add(new{family,mode,method,candidate=r.Index+1,configuredResolver=r.Index==0,dns=r.Dns,handshake=r.Handshake,pass=r.Pass,checks=r.Checks,completed=r.Completed,elapsedMs=r.ElapsedMS,dnsElapsedMs=r.DnsElapsedMS,error=r.Error});return ValueTask.CompletedTask;
                    },stop.Token);
                }
                for(int index=0;index<candidates.Length;index++){
                    using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));await using var probe=new AgentClient(probeOnly:true);using var abort=stop.Token.Register(probe.Abort);
                    bool ready=false,handshake=false,https=false;string error="",stage="";var clock=Stopwatch.StartNew();
                    try{
                        await probe.Start(stop.Token);var reply=await probe.Send(new(){Action="connect",Profile=ResolverScan.Candidate(profile,candidates[index],method,true),SystemProxy=false,HostKey=StoredSshKey(profile),SshTlsCa=StoredSshTlsCa(profile),SlipCertificate=StoredSlipPin(profile),SlipInterval=SlipInterval(profile)});
                        ready=reply.State=="connected";handshake=reply.TunnelEstablished;error=reply.Error;
                        if(ready){var result=await probe.Send(new(){Action="probe",ProbeUrl="https://example.com/",ProbeFallbacks=false});https=result.Verified;error=result.Error;stage=result.ProbeStage;await probe.Send(new(){Action="disconnect"});}
                    }catch(Exception e){error=stop.IsCancellationRequested?"TEST_TIMEOUT":e is SafeError safe?safe.Code:"TRANSPORT_START_FAILED";}
                    finally{probe.Abort();}
                    results.Add(new{family,mode="actual-profile",method,candidate=index+1,configuredResolver=index==0,ready,handshake,httpsVerified=https,error,stage,elapsedMs=clock.ElapsedMilliseconds});
                }
                foreach(string mode in new[]{"simple","e2e"}){
                    var entries=new System.Collections.ObjectModel.ObservableCollection<ScanEntry>();
                    var options=new ScanOptions{Mode=mode,Timeout=5000,Workers=3,E2eConcurrency=2,E2eTimeout=15000,FullVerification=family=="soar"&&mode=="e2e"};
                    var plan=new ScannerPlan19(options,candidates,new[]{method},family,fields[3],fields[11],true,"https://example.com/");
                    var session=new ScannerSession19(plan,entries,ResolverWorker.Run,(entry,wire,phase,stop)=>VerifyCandidate(ResolverScan.Candidate(profile,entry.Address,wire,true),profile,plan.ProbeUrl,stop,()=>true,_=>{},options.E2eTimeout,options.FullVerification,phase));
                    session.Eligible=e=>e.Result.Pass&&(e.Result.Completed==0||e.Score>=2);
                    await session.ResumeDns();await session.Settled();
                    results.Add(new{family,mode="managed-"+mode,method,total=session.Total,scanned=session.Scanned,working=session.Working,tunnelCompleted=session.TunnelCompleted,tunnelPassed=session.TunnelPassed,error=session.Error,selectable=entries.Count(e=>e.Selectable),errors=entries.Select(e=>e.TunnelError).Where(e=>e.Length>0).Distinct().ToArray()});
                    checks[family+"_"+mode+"_both_stages_complete"]=session.Error.Length==0&&session.Scanned==candidates.Length&&session.TunnelCompleted==(session.DirectTunnel?candidates.Length:entries.Count(session.Eligible));
                }
            }
        }catch(Exception error){checks[error is SafeError safe?safe.Code:"SCANNER_AUDIT_FAILED"]=false;}
        checks["windows_proxy_unchanged"]=proxyBefore==new WinInetProxy().Read();
        bool ownerStoreChangedDuringAudit=!before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(store)));checks["fixture_has_no_owner_store_writes"]=fixtureMode;
        await Task.Delay(300);checks["no_fixture_process_leak"]=Owned().IsSubsetOf(ownedBefore);
        bool passed=checks.Count>0&&checks.Values.All(v=>v);
        File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Read-only owner DNS scanner comparison: native DNS and actual profile",privateValuesIncluded=false,ownerProfilesChangedByFixture=false,ownerStoreChangedDuringAudit,results,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
        static HashSet<int> Owned()=>new[]{"Rovunet.Agent","xray","rovunet-transports","slipstream-client"}.SelectMany(Process.GetProcessesByName).Select(p=>{using(p)return p.Id;}).ToHashSet();
    }
}
