using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    enum ProbeKind { Live, Manual, Automatic }
    sealed record ProbeScope(AgentClient? Client, long Revision, string Profile, long Network, string Target, bool Fallbacks, string Route);
    sealed record ProbeRequest(ProbeScope Scope, long Order, ProbeKind Kind);
    sealed record ProbeEvidence(ProbeRequest Request, bool Success, long Tick);
    long probeGeneration, probeOrder;
    ProbeEvidence? probeEvidence;
    bool manualProbeInFlight;
    long? networkChangeNotice;

    ProbeScope CurrentProbeScope() => new(client,connectionRevision,activeId,probeGeneration,data.ProbeUrl,data.ProbeFallbacks,
        state.TunActive ? "tun:" + state.TunInterface : "local-http:" + state.HttpPort);
    ProbeRequest BeginProbe(ProbeKind kind) => new(CurrentProbeScope(),++probeOrder,kind);
    bool Applicable(ProbeRequest request) => state.State == "connected" && request.Order == probeOrder && request.Scope == CurrentProbeScope();
    TimeProvider evidenceClock = TimeProvider.System;
    bool CurrentEvidence() => probeEvidence is {} proof && proof.Request.Scope == CurrentProbeScope()
        && state.State == "connected" && evidenceClock.GetElapsedTime(proof.Tick).TotalSeconds < (proof.Request.Kind == ProbeKind.Live ? 20 : 300);
    bool ConnectionVerified() => probeEvidence == null ? !proofInvalidated && state.Verified : CurrentEvidence() && probeEvidence.Success;
    string ConnectionDetailKey() => ConnectionVerified() ? "HTTPS_VERIFIED" : CurrentEvidence() && probeEvidence is { Success:false } ? "MAC_LIVE_PROBE_FAILED" : "PATH_READY_UNVERIFIED";
    bool proofInvalidated;

    void InvalidateProbeEvidence(bool networkChanged = false)
    {
        liveSample?.Cancel(); ++probeGeneration; ++probeOrder;
        freshMeasurements.Remove(activeId); probeEvidence=null; proofInvalidated=true; state.Verified=false;
        if (networkChanged && state.State == "connected") {
            message=L.Text("MAC_NETWORK_CHANGED"); networkChangeNotice=probeGeneration;
        }
    }
    bool RecordProof(ProbeRequest request, bool success)
    {
        if (!Applicable(request)) return false;
        probeEvidence=new(request,success,evidenceClock.GetTimestamp()); proofInvalidated=false;
        if (success && networkChangeNotice == request.Scope.Network && message == L.Text("MAC_NETWORK_CHANGED")) {
            message=""; networkChangeNotice=null;
        }
        return true;
    }
    bool ApplyProbeResult(ProbeRequest request, AgentReply reply)
    {
        if (reply.Error == "CANCELLED" || !Applicable(request)) return false;
        if (!RecordProof(request,reply.Verified)) return false;
        var id=request.Scope.Profile;
        Commit(d=>d.Measurements[id]=new(reply.Verified,reply.ElapsedMs,DateTimeOffset.UtcNow,"https",reply.Verified?1:0,1));
        freshMeasurements.Add(id);
        if(reply.Verified){ message=L.Text("HTTPS_VERIFIED")+" · "+reply.ElapsedMs+" ms"; messageTone="Green"; }
        else{SafeDiagnostics.Record("probe",reply.Error.Length>0?reply.Error:"PROBE_FAILED");message=L.Text(reply.Error.Length>0?reply.Error:"FAILED");messageTone="Red";}
        RefreshLiveCard(id);UpdateStatus();return true;
    }
}
