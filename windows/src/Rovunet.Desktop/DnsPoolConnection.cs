using System.Diagnostics;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    CancellationTokenSource? poolStop;
    readonly List<int> poolFixtureAgents = [];
    async Task<Profile> ApplyDnsPool(Profile profile)
    {
        if (!data.DnsPoolEnabled || data.GlobalResolverEnabled && !string.IsNullOrWhiteSpace(data.GlobalResolverList) || !ProfileEditing.IsDns(profile)) return profile;
        var fields = Importer.Fields(profile.Raw);
        // Matches Android: pool uses UDP/TCP; encrypted DNS keeps its explicit endpoint.
        if (fields[22] is not ("udp" or "tcp")) return profile;
        string[] entries;
        try { entries = DnsPool.Parse(data.DnsPoolList); } catch (SafeError) { lastError = "DNS_POOL_FALLBACK"; return profile; }
        if (entries.Length == 0) return profile;
        bool fullVerification = data.DnsPoolVerify;
        string verificationUrl = data.LatencyUrl;
        string hostKey = StoredSshKey(profile);
        string certificate = StoredSlipPin(profile);
        int? interval = SlipInterval(profile);
        SetState(new() { State = "connecting" });
        using var userStop = new CancellationTokenSource(); poolStop = userStop;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(userStop.Token);
        // Slipstream's preserved keepalive can require several slow round trips
        // for QUIC, SSH and HTTPS. Do not cut it off at the generic DNS budget.
        budget.CancelAfter(TimeSpan.FromSeconds(fullVerification ? TransportPlan.UsesSlip(profile.Raw) ? 180 : 90 : 8));
        var measured = new Dictionary<string, PoolProbe>();
        try
        {
            async Task<PoolProbe> Probe(string endpoint, CancellationToken ct)
            {
                var clock = Stopwatch.StartNew();
                if (!fullVerification) { var result = await DnsProbe.Run(endpoint, fields[22], ct, 1500); return new(result.Replied, result.Milliseconds); }
                var candidate = ResolverScan.Candidate(profile, endpoint, fields[22], true);
                await using var inspector = new AgentClient(probeOnly:true);
                using var cancel = ct.Register(inspector.Abort);
                await inspector.Start(ct);
                if (fixtureMode && inspector.AgentProcessId is int id) poolFixtureAgents.Add(id);
                var resultConnect = await inspector.Send(new() { Action = "connect", Profile = candidate, HostKey = hostKey, SlipCertificate = certificate, SlipInterval = interval });
                // Pool never accepts a new identity; manual connect retains the normal trust dialog.
                if (resultConnect.State != "connected") return new(false, clock.ElapsedMilliseconds);
                var https = await inspector.Send(new() { Action = "probe", ProbeUrl = verificationUrl,ProbeFallbacks=data.ProbeFallbacks });
                return new(https.Verified, clock.ElapsedMilliseconds);
            }
            string[] selected;
            try {
                selected = await DnsPool.Select(entries, async (address, ct) => { var result = await Probe(address,ct); lock(measured) measured[address]=result; return result; }, budget.Token);
            } catch (OperationCanceledException) when (!userStop.IsCancellationRequested) {
                selected = measured.Where(v=>v.Value.Passed).OrderBy(v=>v.Value.Milliseconds).Take(8).Select(v=>v.Key).ToArray();
            }
            userStop.Token.ThrowIfCancellationRequested();
            if (selected.Length == 0) { lastError = "DNS_POOL_FALLBACK"; return profile; }
            var plan = GlobalResolvers.ForConnection(profile, true, string.Join('\n',selected));
            if (plan.Notice.Length > 0) lastError = plan.Notice;
            return plan.Effective;
        }
        catch (OperationCanceledException) { throw; }
        catch { lastError = "DNS_POOL_FALLBACK"; return profile; }
        finally { poolStop = null; }
    }
}
