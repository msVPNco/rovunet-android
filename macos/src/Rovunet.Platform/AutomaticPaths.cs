using Rovunet.Core;

namespace Rovunet.Platform;

public enum PathSelectionMode { Manual, Failover, Smart }

public static class AutomaticPathPolicy
{
    // Android SmartPathPolicy at 5e66f167c85aecac26008d930a7a24df6f2a31f2.
    public const int MaxCandidates=5;
    public const long MinimumStayMs=120000,CycleMs=30000;
    public static string Collection(Profile p)=>p.SourceId.Length>0?"sub:"+p.SourceId:p.CollectionId;
    public static bool Better(long current,long candidate,long stayed)=>
        current>0&&candidate>0&&candidate<=current/2&&stayed>=MinimumStayMs;
    public static long? Median(IEnumerable<long?> samples)
    {
        var values=samples.ToArray();
        return values.Length==3&&values.All(value=>value>0)?values.Select(value=>value!.Value).Order().ElementAt(1):null;
    }
    public static bool CanProbeTogether(Profile first,Profile second)=>
        !(IsWireGuard(first)&&IsWireGuard(second));
    static bool IsWireGuard(Profile p)=>p.Protocol is "WireGuard" or "AmneziaWG"||
        p.Raw.TrimStart().StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)||
        p.Raw.StartsWith("wireguard://",StringComparison.OrdinalIgnoreCase);
}

public interface IAutomaticPathSession
{
    Profile Profile {get;}
    bool IsCurrent {get;}
    bool HasFailed {get;}
    Task<long?> Probe(CancellationToken cancellation);
    Task Close();
}

public interface IAutomaticPathHost
{
    IReadOnlyList<Profile> Members(string collection);
    string NetworkStamp {get;}
    bool Visible {get;}
    long NowMs {get;}
    bool CanProbeSeparately(Profile active,Profile candidate);
    Task<IAutomaticPathSession?> Connect(Profile profile,CancellationToken cancellation);
    Task<long?> ProbeCandidate(Profile profile,CancellationToken cancellation);
    Task WaitForCycle(IAutomaticPathSession? active,CancellationToken cancellation);
    void Record(Profile profile,long? milliseconds);
    void Notify(string key,int index=0,int total=0);
}

/// <summary>One cancellable owner; candidates and evidence never cross collection/network boundaries.</summary>
public sealed class AutomaticPathController(IAutomaticPathHost host)
{
    int cursor;
    List<Profile> Candidates(string key,string excluding="")
    {
        var members=host.Members(key).Where(p=>AutomaticPathPolicy.Collection(p)==key&&p.Id!=excluding&&
            p.CanConnect&&p.Protocol!="DoH · DNS only").ToList();
        if(members.Count==0)return [];
        int offset=cursor%members.Count;
        var next=members.Skip(offset).Concat(members.Take(offset)).Take(AutomaticPathPolicy.MaxCandidates).ToList();
        cursor=(offset+next.Count)%members.Count;
        return next;
    }
    async Task<(long? Median,int Failed)> Measure(IAutomaticPathSession session,CancellationToken cancellation)
    {
        var samples=new List<long?>();
        for(int i=0;i<3;i++){
            cancellation.ThrowIfCancellationRequested();
            if(!session.IsCurrent)return (null,0);
            if(session.HasFailed){samples.Add(null);continue;}
            try{samples.Add(await session.Probe(cancellation));}
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
            catch{samples.Add(null);}
        }
        return (AutomaticPathPolicy.Median(samples),samples.Count(value=>value is null or <=0));
    }

    public async Task Run(string key,PathSelectionMode mode,CancellationToken cancellation)
    {
        if(mode is not(PathSelectionMode.Failover or PathSelectionMode.Smart))return;
        IAutomaticPathSession? owned=null;
        bool established=false;
        long? lastEstablished=null;
        async Task CloseOwned(){if(owned!=null){await owned.Close();owned=null;}established=false;}
        async Task<bool> Establish(Profile profile)
        {
            lastEstablished=null;
            cancellation.ThrowIfCancellationRequested();
            var latest=host.Members(key).FirstOrDefault(p=>p.Id==profile.Id&&p.Raw==profile.Raw);
            if(latest==null||!latest.CanConnect)return false;
            owned=await host.Connect(latest,cancellation);
            if(owned==null)return false;
            string stamp=host.NetworkStamp;
            var sample=await Measure(owned,cancellation);
            if(stamp.Length==0||stamp!=host.NetworkStamp){host.Notify("MAC_NETWORK_CHANGED");await CloseOwned();return false;}
            host.Record(latest,sample.Median);
            if(sample.Median==null){await CloseOwned();return false;}
            lastEstablished=sample.Median;established=true;return true;
        }
        async Task<bool> Find(string excluding="")
        {
            var choices=Candidates(key,excluding);
            for(int i=0;i<choices.Count;i++){
                cancellation.ThrowIfCancellationRequested();
                if(host.NetworkStamp.Length==0){host.Notify("MAC_AUTO_OFFLINE");return false;}
                host.Notify("MAC_AUTO_TRYING",i+1,choices.Count);
                if(await Establish(choices[i]))return true;
            }
            host.Notify("MAC_AUTO_EXHAUSTED");return false;
        }
        try{
            if(!await Find())return;
            long switchedAt=host.NowMs;
            while(owned is {IsCurrent:true}){
                host.Notify("MAC_AUTO_MONITORING");
                await host.WaitForCycle(owned,cancellation);
                cancellation.ThrowIfCancellationRequested();
                if(!owned.IsCurrent)break;
                if(host.NetworkStamp.Length==0){host.Notify("MAC_AUTO_OFFLINE");continue;}
                if(!host.Visible&&!owned.HasFailed)continue;
                string stamp=host.NetworkStamp;
                var sample=owned.HasFailed?(Median:(long?)null,Failed:3):await Measure(owned,cancellation);
                if(stamp!=host.NetworkStamp){host.Notify("MAC_NETWORK_CHANGED");continue;}
                var active=owned.Profile;
                host.Record(active,sample.Median);
                bool failed=owned.HasFailed||sample.Failed>=3;
                if(failed){
                    await CloseOwned();
                    if(!await Find(active.Id))return;
                    switchedAt=host.NowMs;continue;
                }
                if(mode!=PathSelectionMode.Smart||sample.Median is not {} current||
                    host.NowMs-switchedAt<AutomaticPathPolicy.MinimumStayMs)continue;
                foreach(var candidate in Candidates(key,active.Id)){
                    cancellation.ThrowIfCancellationRequested();
                    if(!owned.IsCurrent||stamp!=host.NetworkStamp){host.Notify("MAC_NETWORK_CHANGED");break;}
                    // A working connection is only replaced after an independent measurement.
                    if(!host.CanProbeSeparately(active,candidate))continue;
                    host.Notify("MAC_AUTO_TESTING");
                    long? latency;
                    try{latency=await host.ProbeCandidate(candidate,cancellation);}
                    catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
                    catch{latency=null;}
                    if(!owned.IsCurrent||stamp!=host.NetworkStamp){host.Notify("MAC_NETWORK_CHANGED");break;}
                    host.Record(candidate,latency);
                    if(latency is not {} candidateMs||!AutomaticPathPolicy.Better(current,candidateMs,host.NowMs-switchedAt))continue;
                    host.Notify("MAC_AUTO_SWITCHING");await CloseOwned();
                    if(!await Establish(candidate)||stamp!=host.NetworkStamp||lastEstablished is not {} confirmed||
                        !AutomaticPathPolicy.Better(current,confirmed,host.NowMs-switchedAt)){
                        await CloseOwned();
                        if(!await Establish(active)){host.Notify("MAC_AUTO_EXHAUSTED");return;}
                    }
                    switchedAt=host.NowMs;break;
                }
            }
        }
        finally{
            // Cancelling a stable monitor leaves its established connection alone.
            // Session.Close also checks ownership, so a later manual session is never stopped.
            if(!established)await CloseOwned();
        }
    }
}
