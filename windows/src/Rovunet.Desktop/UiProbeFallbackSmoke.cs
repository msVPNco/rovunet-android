using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    async Task<Profile> RunProbeFallbackNetwork(Profile originalProfile,string[] entries,Dictionary<string,bool> checks)
    {
        string original=originalProfile.Raw;var profile=GlobalResolvers.ForConnection(originalProfile,true,entries[0]).Effective;networkFixtureMode=true;data.LiveLatency=false;data.ProbeFallbacks=true;
        using var sink=new TcpListener(IPAddress.Loopback,0);sink.Start();int port=((IPEndPoint)sink.LocalEndpoint).Port;string blocked="https://127.0.0.1:"+port+"/";
        profile.CollectionId="probe-fallback-fixture";data.Profiles=[profile];data.SelectedId=profile.Id;data.ExpandedCollections[profile.CollectionId]=true;data.LatencyUrl=blocked;BuildCards();
        var accepted=sink.AcceptTcpClientAsync();var clock=Stopwatch.StartNew();StartCollectionTest(data.Profiles,1,false);using var stalled=await accepted.WaitAsync(TimeSpan.FromSeconds(25));await collectionTask!.WaitAsync(TimeSpan.FromSeconds(90));await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var measured=data.Measurements[profile.Id];checks["primary_tls_stall_was_actually_attempted"]=clock.Elapsed.TotalSeconds>=14;
        checks["working_tunnel_verified_via_alternative_https"]=measured.Verified&&measured.ProbeFallbackUsed;
        checks["latency_is_successful_target_not_failed_target_wait"]=measured.ElapsedMs<8000;
        checks["alternative_success_green_and_explicit_on_card"]=rows[0].Timing.Foreground==Green&&rows[0].Result.Text.Contains(L.Text("TEST_OK_FALLBACK"));
        await using var probe=new AgentClient(probeOnly:true);await probe.Start();var ready=await probe.Send(new(){Action="connect",Profile=profile,SystemProxy=false});checks["fallback_test_preserves_connection_readiness"]=ready.State=="connected";
        var verified=await probe.Send(new(){Action="probe",ProbeUrl="https://example.com/"});checks["strict_success_uses_one_target"]=verified.Verified&&!verified.ProbeFallbackUsed&&verified.ProbeTargetsTried==1;
        var another=sink.AcceptTcpClientAsync();var pending=probe.Send(new(){Action="probe",ProbeUrl=blocked,ProbeFallbacks=true});using var interrupted=await another.WaitAsync(TimeSpan.FromSeconds(25));await probe.CancelProbe();var stopped=await pending.WaitAsync(TimeSpan.FromSeconds(4));
        checks["cancel_stops_before_alternative_target"]=stopped.Error=="CANCELLED"&&stopped.ProbeTargetsTried==1;
        var status=await probe.Send(new(){Action="status"});checks["cancel_preserves_tunnel_and_previous_verification"]=status.State=="connected"&&status.Verified;
        using var unused=new TcpListener(IPAddress.Loopback,0);unused.Start();int dead=((IPEndPoint)unused.LocalEndpoint).Port;unused.Stop();
        var strict=await probe.Send(new(){Action="probe",ProbeUrl="https://127.0.0.1:"+dead+"/",ProbeFallbacks=false});checks["disabled_fallback_does_not_turn_failure_green"]=!strict.Verified&&!strict.ProbeFallbackUsed&&strict.ProbeTargetsTried==1;
        await using var broken=new AgentClient(probeOnly:true);await broken.Start();await broken.Send(new(){Action="connect",Profile=Importer.Single("socks5://127.0.0.1:"+dead+"#Closed%20fixture"),SystemProxy=false});var allFail=await broken.Send(new(){Action="probe",ProbeUrl=blocked,ProbeFallbacks=true});
        checks["broken_proxy_has_no_direct_escape_or_fake_success"]=!allFail.Verified&&allFail.ProbeTargetsTried==3&&HttpsProbePolicy.Unconfirmed(allFail.Error);
        checks["probe_round_keeps_original_raw"]=originalProfile.Raw==original;
        networkFixtureMode=false;return profile;
    }
}
