using Rovunet.Core;
using Rovunet.Platform;

static class AutomaticPathCases
{
    public static async Task Run(Action<bool,string> check)
    {
        check(AutomaticPathPolicy.Better(200,100,120000)&&!AutomaticPathPolicy.Better(200,101,120000)&&!AutomaticPathPolicy.Better(3000,1501,120000)&&
            !AutomaticPathPolicy.Better(3000,500,119999)&&AutomaticPathPolicy.Better(3000,1500,120000),"smart-policy-relative-half-and-minimum-stay-boundaries");
        check(AutomaticPathPolicy.Median([900,100,500])==500&&AutomaticPathPolicy.Median([100,null,300])==null&&
            AutomaticPathPolicy.Median([100,200])==null,"smart-policy-three-positive-samples-required");
        var wg=new Profile{Protocol="WireGuard"};var awg=new Profile{Protocol="AmneziaWG"};
        check(!AutomaticPathPolicy.CanProbeTogether(wg,awg)&&AutomaticPathPolicy.CanProbeTogether(wg,new(){Protocol="TUIC"}),"smart-policy-shared-wireguard-peer-never-probed-concurrently");
        var saved=new AppState{SelectionModes=new(){{"unit",PathSelectionMode.Smart}},PreferredPaths=new(){{"unit","B"}}}.Copy();
        check(saved.SelectionModes["unit"]==PathSelectionMode.Smart&&saved.PreferredPaths["unit"]=="B"&&
            saved.SelectionModes.GetValueOrDefault("other")==PathSelectionMode.Manual,"selection-policy-persists-per-collection-and-defaults-manual");

        async Task Execute(FakeHost host,PathSelectionMode mode=PathSelectionMode.Smart){
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token,host.Stop.Token);
            try{await new AutomaticPathController(host).Run("unit",mode,linked.Token);}
            catch(OperationCanceledException)when(host.Stop.IsCancellationRequested){}
            check(!timeout.IsCancellationRequested,"automatic-fixture-finished-within-deadline-"+host.Name);
        }
        var manual=new FakeHost("manual");await Execute(manual,PathSelectionMode.Manual);
        check(manual.Connected.Count==0,"manual-mode-never-starts-a-probe-or-connect");
        var unknown=new FakeHost("unknown");await Execute(unknown,(PathSelectionMode)999);
        check(unknown.Connected.Count==0,"unknown-mode-falls-back-to-manual");
        var bounded=new FakeHost("bounded",8){ConnectFails=true};await Execute(bounded);
        check(bounded.Connected.Count==5&&bounded.Notices.Last()=="MAC_AUTO_EXHAUSTED","automatic-batch-bounded-to-five-candidates");
        var rotating=new FakeHost("rotating",8){ConnectFails=true};var rotatingController=new AutomaticPathController(rotating);
        using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5))){await rotatingController.Run("unit",PathSelectionMode.Smart,timeout.Token);await rotatingController.Run("unit",PathSelectionMode.Smart,timeout.Token);}
        check(rotating.Connected.Count==10&&rotating.Connected.Distinct().Count()==8,"retry-rotates-to-untried-candidates-within-the-same-collection");
        var isolated=new FakeHost("isolated",2);isolated.Profiles[1].CollectionId="foreign";isolated.ConnectFails=true;await Execute(isolated);
        check(isolated.Connected.SequenceEqual(["A"]),"automatic-candidates-never-cross-collection-boundary");
        var failover=new FakeHost("failure"){FailedAtCycle=1};await Execute(failover,PathSelectionMode.Failover);
        check(failover.Connected.SequenceEqual(["A","B"])&&failover.Current?.Profile.Id=="B","failover-recovers-after-real-engine-failure");
        var failedProbes=new FakeHost("failed-probes"){Latency=(host,p)=>host.Cycle>0&&p.Id=="A"?null:100};await Execute(failedProbes,PathSelectionMode.Failover);
        check(failedProbes.Connected.SequenceEqual(["A","B"]),"failover-recovers-after-three-failed-health-probes");
        var transient=new FakeHost("transient"){Latency=(host,p)=>host.Cycle>0&&p.Id=="A"&&host.Current!.Queries%3!=0?null:100};await Execute(transient,PathSelectionMode.Failover);
        check(transient.Connected.SequenceEqual(["A"]),"two-intermittent-failures-do-not-trigger-failover");
        var slowFailover=new FakeHost("slow-failover"){Latency=(_,_)=>4000,StopAtCycle=6};await Execute(slowFailover,PathSelectionMode.Failover);
        check(slowFailover.Connected.SequenceEqual(["A"])&&slowFailover.CandidateProbes==0,"failover-does-not-switch-a-slow-working-path");
        var early=new FakeHost("early"){Latency=(_,_)=>3000};await Execute(early);
        check(early.CandidateProbes==0,"smart-honors-two-minute-minimum-stay");
        var smart=new FakeHost("smart"){StopAtCycle=6,CandidateLatency=100,Latency=(_,p)=>p.Id=="A"?200:100};await Execute(smart);
        check(smart.Connected.SequenceEqual(["A","B"])&&smart.Current?.Profile.Id=="B","smart-confirms-at-least-two-times-improvement-before-switch");
        var tooSmall=new FakeHost("small"){StopAtCycle=6,Latency=(_,_)=>3000,CandidateLatency=2000};await Execute(tooSmall);
        check(tooSmall.Connected.SequenceEqual(["A"])&&tooSmall.CandidateProbes>0,"smart-rejects-small-latency-improvement");
        var regression=new FakeHost("regression"){StopAtCycle=6,Latency=(_,p)=>p.Id=="A"?3000:4000};await Execute(regression);
        check(regression.Connected.SequenceEqual(["A","B","A"])&&regression.Current?.Profile.Id=="A","smart-restores-old-path-if-live-confirmation-is-worse");
        var serial=new FakeHost("serial"){Separate=false,StopAtCycle=6,Latency=(_,_)=>3000};await Execute(serial);
        check(serial.Connected.SequenceEqual(["A"])&&serial.CandidateProbes==0&&serial.Current?.Profile.Id=="A","tun-keeps-current-without-independent-evidence");
        var serialWin=new FakeHost("serial-win"){Separate=false,StopAtCycle=6,Latency=(_,p)=>p.Id=="A"?3000:600};await Execute(serialWin);
        check(serialWin.Connected.SequenceEqual(["A"])&&serialWin.Current?.Profile.Id=="A","tun-skips-unmeasurable-candidate-even-if-faster");
        var hidden=new FakeHost("hidden"){Visible=false};await Execute(hidden);
        check(hidden.Current?.Queries==3&&hidden.CandidateProbes==0,"hidden-window-generates-no-periodic-network-probes");
        var hiddenFailure=new FakeHost("hidden-failure"){Visible=false,FailedAtCycle=1};await Execute(hiddenFailure,PathSelectionMode.Failover);
        check(hiddenFailure.Current?.Profile.Id=="B","hidden-window-still-recovers-after-engine-exit");
        var network=new FakeHost("network"){StopAtCycle=5,Latency=(_,_)=>3000,ChangeNetworkDuringCandidate=true};await Execute(network);
        check(network.Connected.SequenceEqual(["A"])&&!network.Recorded.Any(row=>row.Id=="B"),"changed-network-discards-candidate-proof");
        var cancelled=new FakeHost("cancel-setup"){CancelDuringProbe=true};await Execute(cancelled);
        check(cancelled.Closed.SequenceEqual(["A"])&&cancelled.Current==null,"cancellation-cleans-only-unfinished-owned-session");
        var stable=new FakeHost("stable");await Execute(stable);
        check(stable.Current?.Profile.Id=="A"&&stable.Closed.Count==0,"cancel-monitor-retains-established-connection");
        var manualOwner=new FakeHost("manual-owner"){ManualTakeover=true};await Execute(manualOwner);
        check(manualOwner.Current?.Profile.Id=="manual"&&!manualOwner.Closed.Contains("manual"),"automatic-owner-never-stops-new-manual-session");
    }

    sealed class FakeHost:IAutomaticPathHost
    {
        public FakeHost(string name,int count=2){Name=name;Profiles=Enumerable.Range(0,count).Select(i=>new Profile{Id=((char)('A'+i)).ToString(),Name="Fixture",Protocol="VLESS",Raw="fixture",CollectionId="unit"}).ToList();}
        public string Name;public List<Profile> Profiles;public CancellationTokenSource Stop=new();
        public List<string> Connected=[],Closed=[],Notices=[];
        public List<(string Id,long? Ms)> Recorded=[];
        public FakeSession? Current;
        public bool ConnectFails,Separate=true,ChangeNetworkDuringCandidate,CancelDuringProbe,ManualTakeover;
        public int Cycle,StopAtCycle=2,FailedAtCycle=-1,CandidateProbes;
        public long CandidateLatency=500;
        public Func<FakeHost,Profile,long?> Latency=(_,_)=>100;
        public string NetworkStamp {get;set;}="physical-fixture";
        public bool Visible {get;set;}=true;
        public long NowMs=>Cycle*30000L;
        public IReadOnlyList<Profile> Members(string key)=>Profiles;
        public bool CanProbeSeparately(Profile active,Profile candidate)=>Separate;
        public Task<IAutomaticPathSession?> Connect(Profile profile,CancellationToken cancellation){
            Connected.Add(profile.Id);if(ConnectFails)return Task.FromResult<IAutomaticPathSession?>(null);
            Current=new(this,profile);return Task.FromResult<IAutomaticPathSession?>(Current);
        }
        public Task<long?> ProbeCandidate(Profile profile,CancellationToken cancellation){
            CandidateProbes++;if(ChangeNetworkDuringCandidate)NetworkStamp="other-network";
            return Task.FromResult<long?>(CandidateLatency);
        }
        public async Task WaitForCycle(IAutomaticPathSession? current,CancellationToken cancellation){
            await Task.Yield();Cycle++;
            if(ManualTakeover){Current=new(this,new(){Id="manual",CollectionId="other"});return;}
            if(Cycle>=StopAtCycle)Stop.Cancel();cancellation.ThrowIfCancellationRequested();
        }
        public void Record(Profile profile,long? milliseconds)=>Recorded.Add((profile.Id,milliseconds));
        public void Notify(string key,int index=0,int total=0)=>Notices.Add(key);
        public sealed class FakeSession(FakeHost host,Profile profile):IAutomaticPathSession
        {
            public int Queries;
            public Profile Profile=>profile;
            public bool IsCurrent=>ReferenceEquals(host.Current,this);
            public bool HasFailed=>host.FailedAtCycle>0&&host.Cycle>=host.FailedAtCycle&&profile.Id=="A";
            public Task<long?> Probe(CancellationToken cancellation){
                Queries++;if(host.CancelDuringProbe){host.Stop.Cancel();cancellation.ThrowIfCancellationRequested();}
                return Task.FromResult(host.Latency(host,profile));
            }
            public Task Close(){if(IsCurrent){host.Closed.Add(profile.Id);host.Current=null;}return Task.CompletedTask;}
        }
    }
}
