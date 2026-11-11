using System.Diagnostics;
using System.Net.NetworkInformation;
using Avalonia.Controls;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    CancellationTokenSource? automaticStop;
    Task? automaticTask;
    long connectionRevision;
    bool automaticMeasuring;
    AgentClient? automaticProbeInFlight;
    readonly Dictionary<string,AutomaticPathController> automaticControllers=[];

    PathSelectionMode SelectionMode(Profile profile){var mode=data.SelectionModes.GetValueOrDefault(AutomaticPathPolicy.Collection(profile));return Enum.IsDefined(mode)?mode:PathSelectionMode.Manual;}
    List<Profile> CollectionMembers(Profile profile)=>data.Profiles.Where(p=>AutomaticPathPolicy.Collection(p)==AutomaticPathPolicy.Collection(profile)).ToList();
    Profile PreferredPath(List<Profile> members)=>
        members.FirstOrDefault(p=>p.Id==data.PreferredPaths.GetValueOrDefault(AutomaticPathPolicy.Collection(members[0]))&&p.CanConnect)??
        members.FirstOrDefault(p=>p.Id==data.SelectedId&&p.CanConnect)??
        members.Where(p=>p.CanConnect).OrderBy(p=>data.Measurements.TryGetValue(p.Id,out var m)&&m.Verified&&
            DateTimeOffset.UtcNow-m.SampledAt<TimeSpan.FromMinutes(5)?m.ElapsedMs:long.MaxValue).FirstOrDefault()??members[0];

    async Task StopAutomatic()
    {
        var stop=automaticStop;var task=automaticTask;
        if(stop==null)return;
        stop.Cancel();
        if(task!=null)try{await task;}catch(OperationCanceledException){}catch(Exception error){Display(error);}
    }
    async Task ConnectProfile(Profile profile)
    {
        await StopAutomatic();
        await ConnectProfileCore(profile,false,CancellationToken.None);
    }
    async Task SelectPath(Profile profile)
    {
        await StopAutomatic();
        Commit(next=>{next.SelectedId=profile.Id;next.PreferredPaths[AutomaticPathPolicy.Collection(profile)]=profile.Id;});
        BuildCards();UpdateStatus();
    }
    async Task Disconnect()
    {
        await StopAutomatic();connectionRevision++;
        await DisconnectCore();
    }
    async Task ConnectCollection(Profile representative)
    {
        var members=CollectionMembers(representative);
        if(members.Count==0)return;
        if(members.Any(p=>p.Id==activeId&&state.State=="connected"||p.Id==connectingId&&busy)){
            await Disconnect();return;
        }
        if(busy)return;
        var mode=SelectionMode(representative);
        if(mode==PathSelectionMode.Manual){await ConnectProfile(PreferredPath(members));return;}
        await StopAutomatic();
        await DisconnectCore();
        var stop=new CancellationTokenSource();automaticStop=stop;
        string key=AutomaticPathPolicy.Collection(representative);
        if(!automaticControllers.TryGetValue(key,out var controller))automaticControllers[key]=controller=new AutomaticPathController(new AutomaticHost(this));
        async Task Run(){
            try{await controller.Run(key,mode,stop.Token);}
            catch(OperationCanceledException)when(stop.IsCancellationRequested){}
            catch(Exception error){Display(error);}
            finally{
                if(ReferenceEquals(automaticStop,stop)){automaticStop=null;automaticTask=null;}
                stop.Dispose();BuildCards();UpdateStatus();
            }
        }
        // Yield before the controller starts so even an immediately exhausted batch
        // cannot race assignment of the owner task.
        async Task Start(){await Task.Yield();await Run();}
        automaticTask=Start();await automaticTask;
    }
    MenuItem AutomaticMenu(ContextMenu menu,Func<Profile> target)
    {
        var parent=new MenuItem{Header=L.Text("MAC_SELECTION_MODE"),Tag="MAC_SELECTION_MODE",MinHeight=44};
        ToolTip.SetTip(parent,L.Text("MAC_SELECTION_HINT"));
        var modes=new[]{(PathSelectionMode.Manual,"MAC_SELECTION_MANUAL"),(PathSelectionMode.Failover,"MAC_SELECTION_FAILOVER"),(PathSelectionMode.Smart,"MAC_SELECTION_SMART")};
        foreach(var (mode,label) in modes){
            var child=Item(label,()=>_=Guard(async()=>{
                await StopAutomatic();var profile=target();
                Commit(next=>next.SelectionModes[AutomaticPathPolicy.Collection(profile)]=mode);
                if(mode==PathSelectionMode.Manual){var members=CollectionMembers(profile);if(members.Count>1)ShowPathSelection(members);}
                BuildCards();UpdateStatus();
            }));
            parent.Items.Add(child);
        }
        void Refresh(){
            var mode=SelectionMode(target());
            for(int i=0;i<modes.Length;i++)((MenuItem)parent.Items[i]!).Icon=modes[i].Item1==mode?VectorIcon("M3,12 L9,18 L21,5",16):null;
        }
        menu.Opened+=(_,_)=>Refresh();Refresh();return parent;
    }

    sealed class AutomaticHost(MainWindow window):IAutomaticPathHost
    {
        public IReadOnlyList<Profile> Members(string collection)=>window.data.Profiles
            .Where(p=>AutomaticPathPolicy.Collection(p)==collection&&p.CanConnect&&p.Protocol!="DoH · DNS only"&&
                (!SshHostKey.Required(p)||window.data.SshKeys.ContainsKey(SshHostKey.Scope(p)))&&
                (!TransportPlan.UsesSlip(p.Raw)||window.StoredSlipCertificate(p).Length>0))
            .OrderByDescending(p=>window.data.LastConnected.GetValueOrDefault(p.Id)).ToArray();
        public bool Visible=>window.IsVisible;
        public long NowMs=>(long)(Stopwatch.GetTimestamp()*1000d/Stopwatch.Frequency);
        public string NetworkStamp{
            get{
                try{return string.Join('|',NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is not(NetworkInterfaceType.Tunnel or NetworkInterfaceType.Loopback)&&
                        !new[]{"utun","tun","tap","lo"}.Any(prefix=>n.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)))
                    .Select(n=>n.Id+":"+string.Join(',',n.GetIPProperties().UnicastAddresses.Select(a=>a.Address.ToString()).Order())).Order());}
                catch{return "";}
            }
        }
        public bool CanProbeSeparately(Profile active,Profile candidate)=>!window.data.SystemTun&&AutomaticPathPolicy.CanProbeTogether(active,candidate);
        public async Task<IAutomaticPathSession?> Connect(Profile profile,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(90));
            await window.ConnectProfileCore(profile,true,deadline.Token);
            if(window.state.State!="connected"||window.activeId!=profile.Id||window.client==null)return null;
            return new AutomaticSession(window,profile,window.client,window.connectionRevision);
        }
        public async Task<long?> ProbeCandidate(Profile profile,CancellationToken cancellation)
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(45));
            using var agent=new AgentClient();
            var effective=GlobalResolvers.ForConnection(profile,window.data.GlobalResolverEnabled,window.data.GlobalResolverList).Effective;
            var selected=await window.ApplyDnsPool(effective,deadline.Token);
            var started=await agent.Send(new(){Action="connect",Profile=selected,HostKey=window.data.SshKeys.GetValueOrDefault(SshHostKey.Scope(profile),""),SlipCertificate=window.StoredSlipCertificate(profile)},deadline.Token);
            if(started.State!="connected")return null;
            var samples=new List<long?>();
            for(int i=0;i<3;i++){
                var reply=await agent.Send(new(){Action="probe",ProbeUrl=window.data.ProbeUrl,ProbeFallbacks=false},deadline.Token);
                samples.Add(reply.Verified?reply.ElapsedMs:null);
            }
            return AutomaticPathPolicy.Median(samples);
        }
        public async Task WaitForCycle(IAutomaticPathSession? active,CancellationToken cancellation)
        {
            long started=NowMs;
            while(NowMs-started<AutomaticPathPolicy.CycleMs&&active is {IsCurrent:true,HasFailed:false})await Task.Delay(1000,cancellation);
            if(active is {HasFailed:true}&&NetworkStamp.Length==0)await Task.Delay(10000,cancellation);
        }
        public void Record(Profile profile,long? milliseconds)
        {
            window.Commit(next=>next.Measurements[profile.Id]=new(milliseconds!=null,milliseconds??0,DateTimeOffset.UtcNow,"https",milliseconds!=null?1:0,1));
            window.freshMeasurements.Add(profile.Id);window.BuildCards();window.UpdateStatus();
        }
        public void Notify(string key,int index=0,int total=0){if(key=="MAC_NETWORK_CHANGED")window.freshMeasurements.Clear();window.message=L.Text(key)+(total>0?$" · {index} / {total}":"");window.UpdateStatus();}
    }
    sealed class AutomaticSession(MainWindow window,Profile profile,AgentClient agent,long revision):IAutomaticPathSession
    {
        public Profile Profile=>profile;
        public bool IsCurrent=>!window.exiting&&revision==window.connectionRevision;
        public bool HasFailed=>!ReferenceEquals(window.client,agent)||window.state.State is "error" or "disconnected";
        public async Task<long?> Probe(CancellationToken cancellation)
        {
            if(!IsCurrent||HasFailed)return null;
            window.automaticMeasuring=true;
            try{
                // Cancelling a monitor must not cancel AgentClient's framed read:
                // cancelling that read disposes the running connection. Drain it
                // privately; explicit disconnect can still terminate this owner.
                window.automaticProbeInFlight=agent;
                var pending=agent.Send(new(){Action="probe",ProbeUrl=window.data.ProbeUrl,ProbeFallbacks=false});
                async Task Drain(){try{await pending;}catch{}finally{if(ReferenceEquals(window.automaticProbeInFlight,agent))window.automaticProbeInFlight=null;}}
                _=Drain();
                var reply=await pending.WaitAsync(cancellation);
                if(!IsCurrent||!ReferenceEquals(window.client,agent))return null;
                window.state=reply;window.UpdateStatus();return reply.Verified?reply.ElapsedMs:null;
            }
            finally{window.automaticMeasuring=false;}
        }
        public async Task Close(){if(IsCurrent)await window.DisconnectCore();}
    }
}
