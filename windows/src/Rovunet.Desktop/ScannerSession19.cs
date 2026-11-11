using System.Collections.ObjectModel;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed record ScannerPlan19(ScanOptions Options,string[] Candidates,string[] Methods,string Family,string Zone,string PublicKey,bool AutomaticTunnel,string ProbeUrl="",int QuerySize=0);

    // The dispatcher owns this session. Each phase keeps its own cancellation and unfinished work.
    // A cancelled attempt is never recorded as a measured failure or a completed tunnel test.
    sealed class ScannerSession19
    {
        readonly ScannerPlan19 plan;
        readonly Func<object,Func<ResolverResult,ValueTask>,CancellationToken,Task> scan;
        readonly Func<ScanEntry,string,Action<string>,CancellationToken,Task<AgentReply>> verify;
        readonly ObservableCollection<ScanEntry> entries;
        readonly Dictionary<string,ScanEntry> byAddress=new(StringComparer.Ordinal);
        readonly HashSet<(string Address,string Method)> completed=[];
        readonly HashSet<string> visited=new(StringComparer.Ordinal);
        readonly List<string> candidates;
        readonly Dictionary<string,int> indices;
        readonly List<ScanEntry> pending=[];
        readonly HashSet<ScanEntry> running=[];
        TaskCompletionSource pulse=new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenSource? dnsStop,tunnelStop;
        Task dnsTask=Task.CompletedTask,tunnelTask=Task.CompletedTask;
        bool allowTunnel;
        internal bool DirectTunnel=>plan.Options.Mode=="e2e"||plan.Family=="soar"&&plan.Options.Mode=="simple";
        bool Qualifies(ScanEntry entry)=>DirectTunnel||Eligible(entry);
        int scanned,working,tunnelTotal,tunnelCompleted,tunnelPassed;
        internal Action Changed=()=>{};
        internal Comparison<ScanEntry> Compare=(a,b)=>a.ScanIndex.CompareTo(b.ScanIndex);
        internal Func<ScanEntry,bool> Eligible=e=>e.Result.Pass;
        internal string Error="";
        internal long Revision{get;private set;}
        internal bool DnsRunning{get;private set;}
        internal bool TunnelRunning{get;private set;}
        internal bool TunnelStarted{get;private set;}
        internal bool DnsStopping=>DnsRunning&&dnsStop?.IsCancellationRequested==true;
        internal bool TunnelStopping=>TunnelRunning&&tunnelStop?.IsCancellationRequested==true;
        internal bool Busy=>DnsRunning||TunnelRunning;
        internal bool CanResumeDns=>DirectTunnel?!TunnelStarted:!DnsRunning&&completed.Count<candidates.Count*plan.Methods.Length;
        internal bool CanResumeTunnel=>!TunnelRunning&&entries.Any(e=>Qualifies(e)&&!e.TunnelFinished);
        internal bool CanRetest=>!TunnelRunning&&entries.Any(Qualifies);
        internal int Total=>candidates.Count;
        internal int Scanned=>DirectTunnel?tunnelCompleted:scanned;
        internal int Working=>DirectTunnel?tunnelPassed:working;
        internal int TunnelTotal=>tunnelTotal;
        internal int TunnelCompleted=>tunnelCompleted;
        internal int TunnelPassed=>tunnelPassed;
        internal IEnumerable<ScanEntry> Running=>running;
        internal ScannerSession19(ScannerPlan19 plan,ObservableCollection<ScanEntry> entries,Func<object,Func<ResolverResult,ValueTask>,CancellationToken,Task> scan,Func<ScanEntry,string,Action<string>,CancellationToken,Task<AgentReply>> verify,bool restoredTunnelStarted=false)
        {
            this.plan=plan;this.entries=entries;this.scan=scan;this.verify=verify;
            candidates=plan.Candidates.Distinct(StringComparer.Ordinal).ToList();visited.UnionWith(candidates);allowTunnel=plan.AutomaticTunnel;
            indices=candidates.Select((address,index)=>(address,index)).ToDictionary(x=>x.address,x=>x.index,StringComparer.Ordinal);
            foreach(var entry in entries){byAddress[entry.Address]=entry;foreach(var proof in entry.Proofs){completed.Add((entry.Address,proof.Transport));proof.Index=indices[entry.Address];}if(DirectTunnel)entry.Selectable=entry.TunnelFinished&&entry.TunnelVerified;if(entry.Result.Pass)working++;if(plan.Methods.All(m=>completed.Contains((entry.Address,m))))scanned++;}
            TunnelStarted=restoredTunnelStarted;tunnelCompleted=entries.Count(e=>e.TunnelFinished);tunnelPassed=entries.Count(e=>e.TunnelFinished&&e.TunnelVerified);tunnelTotal=tunnelCompleted;
        }
        internal ScanCheckpoint19 Checkpoint(string fingerprint)=>new(){Fingerprint=fingerprint,SavedAt=DateTimeOffset.UtcNow,Options=plan.Options,Candidates=candidates.ToArray(),Methods=plan.Methods,Family=plan.Family,Zone=plan.Zone,PublicKey=plan.PublicKey,AutomaticTunnel=plan.AutomaticTunnel,QuerySize=plan.QuerySize,TunnelStarted=TunnelStarted,ProbeUrl=plan.ProbeUrl,Results=entries.Select(e=>new ScanCheckpointRow19{Address=e.Address,Proofs=e.Proofs.ToArray(),TunnelFinished=e.TunnelFinished,TunnelVerified=e.TunnelVerified,HttpsVerified=e.HttpsVerified,VerifiedTransport=e.VerifiedTransport,VerifiedElapsedMs=e.VerifiedElapsedMs,Error=e.TunnelError}).ToList()};
        void Notify(){Revision++;var prior=pulse;pulse=new(TaskCreationOptions.RunContinuationsAsynchronously);prior.TrySetResult();Changed();}
        internal Task ResumeDns()
        {
            if(DnsRunning||!CanResumeDns)return dnsTask;
            if(DirectTunnel){
                foreach(string address in candidates)if(!byAddress.ContainsKey(address)){
                    var entry=new ScanEntry{Address=address,Result=new(){Index=indices[address],Transport=plan.Methods[0]}};
                    foreach(string method in plan.Methods){entry.Probes[method]=new(){Index=indices[address],Transport=method};completed.Add((address,method));}
                    byAddress.Add(address,entry);entries.Add(entry);
                }
                Error="";return StartTunnel(false);
            }
            dnsStop?.Dispose();dnsStop=new();Error="";DnsRunning=true;
            if(plan.AutomaticTunnel&&!TunnelRunning){allowTunnel=true;StartTunnel(false);}
            dnsTask=ScanDns(dnsStop.Token);Notify();return dnsTask;
        }
        async Task ScanDns(CancellationToken token)
        {
            try{
                // A stable address plan survives cancellation, including shuffle and discovered neighbours.
                int cursor=0;
                while(cursor<candidates.Count){
                    var group=candidates.Skip(cursor).Take(128).ToArray();cursor+=128;
                    foreach(var remaining in group.GroupBy(address=>string.Join(',',plan.Methods.Where(m=>!completed.Contains((address,m))))).Where(g=>g.Key.Length>0)){
                        var batch=remaining.ToArray();var methods=remaining.Key.Split(',');var seen=new HashSet<(int,string)>();
                        var o=plan.Options;var request=new{Mode=o.Mode,Family=plan.Family,Zone=plan.Zone,PublicKey=plan.PublicKey,Candidates=batch,Transports=methods,o.Workers,TimeoutMS=o.Timeout,Count=o.Attempts,o.Threshold,ResponseSize=o.Size,QuerySize=plan.QuerySize,o.Prefilter};
                        await scan(request,row=>{
                            token.ThrowIfCancellationRequested();
                            if(row.Index<0||row.Index>=batch.Length||!methods.Contains(row.Transport)||!seen.Add((row.Index,row.Transport)))throw new SafeError("INVALID_MESSAGE");
                            string method=row.Transport,address=batch[row.Index];completed.Add((address,method));
                            if(plan.Methods.All(m=>completed.Contains((address,m))))scanned++;
                            bool wasWorking=byAddress.TryGetValue(address,out var prior)&&prior.Result.Pass;
                            if(!byAddress.TryGetValue(address,out var entry)){
                                entry=new(){Address=address,Result=row};byAddress.Add(address,entry);entries.Add(entry);
                            }
                            row.Index=indices[address];entry.Probes[method]=row;
                            // Retain proof per transport; a passing UDP response cannot manufacture a TCP success.
                            entry.Result=entry.Probes.Values.OrderByDescending(r=>r.Pass).ThenByDescending(r=>r.Checks?.Length??0).ThenBy(r=>r.DnsElapsedMS>0?r.DnsElapsedMS:r.ElapsedMS).First();
                            entry.CachedDnsMethods.Remove(method);entry.Selectable=entry.Result.Pass;entry.Stale=false;entry.DnsCached=entry.CachedDnsMethods.Contains(entry.Result.Transport);entry.Refresh();
                            if(!wasWorking&&entry.Result.Pass)working++;
                            if(allowTunnel&&TunnelRunning&&Ready(entry)&&!entry.TunnelFinished&&!entry.Pending&&!entry.Testing){entry.Pending=true;pending.Add(entry);tunnelTotal++;}
                            if(o.Nearby&&row.Pass&&visited.Count<ResolverScan.MaximumCandidates)foreach(string near in ResolverInputs.Neighbours(address)){if(visited.Count>=ResolverScan.MaximumCandidates)break;if(visited.Add(near)){indices[near]=candidates.Count;candidates.Add(near);}}
                            Notify();return ValueTask.CompletedTask;
                        },token);
                        if(seen.Count!=batch.Length*methods.Length)throw new SafeError("DNS_SCAN_INVALID");
                    }
                }
            }catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch(Exception error){Error=error is SafeError safe?safe.Code:"DNS_SCAN_INVALID";}
            finally{DnsRunning=false;Notify();}
        }
        bool Ready(ScanEntry entry)=>Qualifies(entry)&&plan.Methods.All(method=>completed.Contains((entry.Address,method)));
        internal Task StartTunnel(bool fresh)
        {
            if(TunnelRunning)return tunnelTask;
            tunnelStop?.Dispose();tunnelStop=new();allowTunnel=true;
            if(fresh)foreach(var entry in entries.Where(Qualifies)){entry.TunnelFinished=entry.TunnelVerified=entry.HttpsVerified=entry.TunnelCached=false;if(DirectTunnel)entry.Selectable=false;entry.VerifiedTransport="";entry.VerifiedElapsedMs=null;entry.Detail=entry.TunnelError="";entry.Refresh();}
            foreach(var entry in entries.Where(e=>Ready(e)&&!e.TunnelFinished&&!e.Pending&&!e.Testing)){entry.Pending=true;pending.Add(entry);entry.Refresh();}
            tunnelCompleted=entries.Count(e=>Qualifies(e)&&e.TunnelFinished);tunnelPassed=entries.Count(e=>Qualifies(e)&&e.TunnelFinished&&e.TunnelVerified);tunnelTotal=tunnelCompleted+pending.Count;
            TunnelStarted=true;TunnelRunning=true;tunnelTask=RunTunnels(tunnelStop.Token);Notify();return tunnelTask;
        }
        async Task RunTunnels(CancellationToken token)
        {
            async Task Consume(){
                try{while(true){
                    token.ThrowIfCancellationRequested();
                    if(pending.Count==0){if(!DnsRunning)break;await pulse.Task.WaitAsync(token);continue;}
                    pending.Sort(Compare);var entry=pending[0];pending.RemoveAt(0);entry.Pending=false;entry.Testing=true;running.Add(entry);entry.Refresh();Notify();
                    try{
                        AgentReply? result=null;
                        // BOTH is an explicit request. Test only transports actually offered by that plan.
                        foreach(string method in plan.Methods.Where(m=>DirectTunnel||entry.Probes.TryGetValue(m,out var proof)&&proof.Pass)){
                            entry.Phase=L.Text("SCAN_PHASE_SETUP");entry.Refresh();Notify();
                            result=await verify(entry,method,value=>{entry.Phase=value;entry.Refresh();Notify();},token);token.ThrowIfCancellationRequested();
                            if(result.Verified||!plan.Options.FullVerification&&result.TunnelEstablished){entry.VerifiedTransport=method;break;}
                            if(result.Error is "SSH_KEY_CHANGED" or "SSH_TRUST_REQUIRED" or "SSH_AUTH_FAILED")break;
                        }
                        result??=new(){Error="DNS_FAILED"};entry.TunnelFinished=true;entry.TunnelCached=false;entry.TunnelError=result.Error;entry.HttpsVerified=result.Verified;entry.TunnelVerified=result.Verified||!plan.Options.FullVerification&&result.TunnelEstablished;
                        tunnelCompleted++;if(entry.TunnelVerified)tunnelPassed++;
                        if(DirectTunnel)entry.Selectable=entry.TunnelVerified;
                        entry.VerifiedElapsedMs=entry.TunnelVerified?result.ElapsedMs:null;
                        entry.Detail=L.Text(result.Verified?"HTTPS_VERIFIED":entry.TunnelVerified?"SCAN_SERVER_REACHED":result.Error.Length>0?result.Error:"HTTPS_TEST_FAILED");
                    }catch(OperationCanceledException)when(token.IsCancellationRequested){entry.Detail=L.Text("CANCELLED");}
                    catch(Exception error){entry.TunnelFinished=true;entry.TunnelVerified=false;entry.TunnelCached=false;tunnelCompleted++;entry.TunnelError=error is SafeError safe?safe.Code:"HTTPS_TEST_FAILED";entry.Detail=L.Text(entry.TunnelError);}
                    finally{entry.Testing=false;entry.Phase="";running.Remove(entry);entry.Refresh();Notify();}
                }}catch(OperationCanceledException)when(token.IsCancellationRequested){}
            }
            try{await Task.WhenAll(Enumerable.Range(0,Math.Clamp(plan.Options.E2eConcurrency,1,plan.Family=="slipstream"?1:plan.Family=="soar"?4:10)).Select(_=>Consume()));}
            finally{
                foreach(var entry in pending){entry.Pending=false;entry.Detail=L.Text("CANCELLED");entry.Refresh();}pending.Clear();TunnelRunning=false;Notify();
            }
        }
        internal void StopDns(){dnsStop?.Cancel();Notify();}
        internal void StopTunnel(){allowTunnel=false;tunnelStop?.Cancel();Notify();}
        internal async Task StopAll(){StopDns();StopTunnel();await Task.WhenAll(dnsTask,tunnelTask);dnsStop?.Dispose();tunnelStop?.Dispose();dnsStop=tunnelStop=null;}
        internal Task Settled()=>Task.WhenAll(dnsTask,tunnelTask);
    }
}
