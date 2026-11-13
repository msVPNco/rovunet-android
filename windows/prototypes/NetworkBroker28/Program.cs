using System.Text.Json;
using System.Text.Json.Serialization;
using Rovunet.Core;

// Executable specification, excluded from the installer. No route, DNS, WFP,
// driver, service or elevation API is called here. This is not a working TUN.
var checks=new Dictionary<string,bool>();
void Check(string name,bool value)=>checks[name]=value;
void Reject(string name,Action action){try{action();checks[name]=false;}catch(SafeError){checks[name]=true;}}
var request=new BrokerRequest(1,Guid.NewGuid(),"start",10808,true);
Check("bounded_start_request",BrokerPolicy.Valid(request));
Reject("unknown_schema_rejected",()=>BrokerPolicy.Require(request with{Version=2}));
Reject("arbitrary_action_rejected",()=>BrokerPolicy.Require(request with{Action="run-executable"}));
Reject("zero_session_rejected",()=>BrokerPolicy.Require(request with{Session=Guid.Empty}));
Reject("invalid_port_rejected",()=>BrokerPolicy.Require(request with{SocksPort=0}));
Reject("unknown_executable_property_rejected",()=>BrokerPolicy.Decode("{\"Version\":1,\"Session\":\""+request.Session+"\",\"Action\":\"start\",\"SocksPort\":10808,\"Executable\":\"fixture\"}"));
Reject("duplicate_action_rejected",()=>BrokerPolicy.Decode("{\"Action\":\"status\",\"Action\":\"start\"}"));
var backend=new MemoryNetwork();var journal=new MemoryJournal{Network=backend};var lifecycle=new BrokerLifecycle(backend,journal);
lifecycle.Start(request);
Check("durable_journal_precedes_any_network_change",backend.Events[0]=="journal"&&backend.Events[1]=="protect");
Check("kill_switch_is_separate_from_tun",backend.Events.SequenceEqual(new[]{"journal","protect","tun","routes-dns"}));
lifecycle.NetworkLost(request.Session);Check("network_loss_keeps_opted_in_filter",backend.Events.Last()=="pause-tun"&&journal.Value!.KillSwitch);
lifecycle.Stop(request.Session);Check("restore_ownership_before_filter_release",backend.Events.TakeLast(3).SequenceEqual(new[]{"restore-owned-network","release-filter","delete-journal"})&&journal.Value==null);
backend=new();journal=new(){FailWrite=true};lifecycle=new(backend,journal);Reject("journal_failure_makes_no_network_change",()=>lifecycle.Start(request));Check("journal_failure_no_mutation",backend.Events.Count==0);
backend=new(){FailApply=true};journal=new(){Network=backend};lifecycle=new(backend,journal);Reject("route_failure_is_not_connected",()=>lifecycle.Start(request));Check("route_failure_rolls_back_owned_changes",journal.Value==null&&backend.Events.Contains("restore-owned-network"));
backend=new();journal=new(){Network=backend};lifecycle=new(backend,journal);lifecycle.Start(request);backend.ExternalChange=true;Reject("external_change_not_overwritten",()=>lifecycle.Stop(request.Session));Check("recovery_conflict_retains_journal_and_filter",journal.Value!=null&&!backend.Events.Contains("release-filter"));
Reject("wrong_session_cannot_stop_owner",()=>lifecycle.Stop(Guid.NewGuid()));
Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Values.All(v=>v),checks,evidence="policy/lifecycle model only",realTun=false,realKillSwitch=false,systemSettingsChanged=false}));
return checks.Values.All(v=>v)?0:1;

sealed record BrokerRequest(int Version,Guid Session,string Action,int SocksPort=0,bool KillSwitch=false);
static class BrokerPolicy
{
    public static bool Valid(BrokerRequest r)=>r.Version==1&&r.Session!=Guid.Empty&&r.Action is "start" or "stop" or "status" or "recover"&&(r.Action!="start"||r.SocksPort is >=1024 and <=65535);
    public static void Require(BrokerRequest r){if(!Valid(r))throw new SafeError("BROKER_REQUEST_REJECTED");}
    public static BrokerRequest Decode(string text){
        try{if(text.Length>4096)throw new SafeError("BROKER_REQUEST_REJECTED");using var doc=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=3});var names=new HashSet<string>();foreach(var field in doc.RootElement.EnumerateObject())if(!names.Add(field.Name))throw new SafeError("BROKER_REQUEST_REJECTED");var r=doc.RootElement.Deserialize<BrokerRequest>(new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})!;Require(r);return r;}
        catch(SafeError){throw;}catch{throw new SafeError("BROKER_REQUEST_REJECTED");}
    }
}
sealed record NetworkOwnership(Guid Session,bool KillSwitch);
interface IScopedNetwork{void Protect(NetworkOwnership owner);void CreateTun(NetworkOwnership owner);void ApplyOwnedNetwork(NetworkOwnership owner);void PauseTun(NetworkOwnership owner);void RestoreOwnedNetwork(NetworkOwnership owner);void ReleaseFilter(NetworkOwnership owner);}
interface INetworkJournal{NetworkOwnership? Value{get;}void Write(NetworkOwnership owner);void Delete();}
sealed class BrokerLifecycle(IScopedNetwork network,INetworkJournal store)
{
    public void Start(BrokerRequest r){BrokerPolicy.Require(r);if(r.Action!="start"||store.Value!=null)throw new SafeError("BROKER_BUSY");var owner=new NetworkOwnership(r.Session,r.KillSwitch);store.Write(owner);try{if(owner.KillSwitch)network.Protect(owner);network.CreateTun(owner);network.ApplyOwnedNetwork(owner);}catch{Stop(r.Session);throw new SafeError("BROKER_START_FAILED");}}
    NetworkOwnership Owner(Guid session)=>store.Value is {} owner&&owner.Session==session?owner:throw new SafeError("BROKER_SESSION_REJECTED");
    public void NetworkLost(Guid session)=>network.PauseTun(Owner(session));
    public void Stop(Guid session){var owner=Owner(session);network.RestoreOwnedNetwork(owner);if(owner.KillSwitch)network.ReleaseFilter(owner);store.Delete();}
}
sealed class MemoryNetwork:IScopedNetwork
{
    public List<string> Events=[];public bool FailApply,ExternalChange;
    public void Protect(NetworkOwnership o)=>Events.Add("protect");public void CreateTun(NetworkOwnership o)=>Events.Add("tun");public void ApplyOwnedNetwork(NetworkOwnership o){Events.Add("routes-dns");if(FailApply)throw new SafeError("SIMULATED_FAILURE");}public void PauseTun(NetworkOwnership o)=>Events.Add("pause-tun");public void RestoreOwnedNetwork(NetworkOwnership o){if(ExternalChange)throw new SafeError("BROKER_OWNERSHIP_CONFLICT");Events.Add("restore-owned-network");}public void ReleaseFilter(NetworkOwnership o)=>Events.Add("release-filter");
}
sealed class MemoryJournal:INetworkJournal
{
    public NetworkOwnership? Value{get;private set;}public bool FailWrite;public MemoryNetwork? Network;
    public void Write(NetworkOwnership owner){if(FailWrite)throw new SafeError("SIMULATED_STORAGE_FAILURE");Value=owner;Network?.Events.Add("journal");}
    public void Delete(){Value=null;Network?.Events.Add("delete-journal");}
}
