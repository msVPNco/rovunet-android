using Rovunet.Core;
internal static class Research15Cases
{
 public static IEnumerable<(string,Action)> All()
 {
  void Check(bool ok){if(!ok)throw new Exception();}
  yield return ("research15-migration-preview-refuses-silent-trust-transfer",()=>{var old=Importer.Single("socks5://127.0.0.1:19001#Old");old.ItemId="same";var incoming=Importer.Single("socks5://127.0.0.2:19001#Old");incoming.ItemId="same";var plan=SubscriptionMigrationPreview.Plan(new("old","account","period"),new("new","account","next"),new[]{old},new[]{incoming},new HashSet<string>{old.Id},new HashSet<string>{old.Id},old.Id);var item=plan.Items.Single();Check(plan.RequiresExplicitDecision&&plan.AddParallelSource&&item.Change=="endpoint-changed"&&item.HadTrust&&item.HadOverride&&item.WasSelected&&!item.TrustTransferAllowed);});
  yield return ("research15-migration-membership-return-uses-id-not-position",()=>{var a=Importer.Single("socks5://127.0.0.1:19001#Same name");a.ItemId="a";var b=Importer.Single("socks5://127.0.0.1:19002#Same name");b.ItemId="b";var plan=SubscriptionMigrationPreview.Plan(new("source","account","period"),new("source","account","period"),new[]{a},new[]{b},new HashSet<string>(),new HashSet<string>(),"",new HashSet<string>{"b"});Check(plan.Items.Single(i=>i.ItemId=="a").Change=="removed"&&plan.Items.Single(i=>i.ItemId=="b").Change=="returned");});
  yield return ("research15-route-aba-cancels-old-token",()=>{var route=new RouteGeneration();var lease=route.Capture();route.Advance();route.Advance();Check(lease.Cancelled.IsCancellationRequested&&!route.IsCurrent(lease));});
  yield return ("research15-route-old-commit-never-invoked",()=>{var route=new RouteGeneration();var lease=route.Capture();route.Advance();bool invoked=false;try{route.Commit(lease,()=>invoked=true);}catch(SafeError e){Check(e.Code=="APP_UPDATE_ROUTE_CHANGED");}Check(!invoked);});
  yield return ("research15-route-new-generation-can-commit",()=>{var route=new RouteGeneration();route.Advance();bool invoked=false;route.Commit(route.Capture(),()=>invoked=true);Check(invoked);});
  yield return ("research15-boot-eight-separate-owned-filters",()=>{var install=Guid.NewGuid();var rules=BootPolicyPlan.Filters(install);Check(rules.Length==8&&rules.Select(r=>r.Key).Distinct().Count()==8&&rules.Count(r=>r.Flags==1)==4&&rules.Count(r=>r.Flags==2)==4);});
  yield return ("research15-boot-never-combines-persistent-and-boot",()=>Check(BootPolicyPlan.Filters(Guid.NewGuid()).All(r=>r.Flags!=3)));
  yield return ("research15-boot-installation-identity-isolation",()=>{var a=Guid.NewGuid();var b=Guid.NewGuid();Check(!BootPolicyPlan.Filters(a).Select(r=>r.Key).Intersect(BootPolicyPlan.Filters(b).Select(r=>r.Key)).Any()&&BootPolicyPlan.Sublayer(a)!=BootPolicyPlan.Sublayer(b));});
  yield return ("research15-boot-guid-recovery-is-deterministic",()=>{var a=Guid.NewGuid();Check(BootPolicyPlan.Filters(a).SequenceEqual(BootPolicyPlan.Filters(a)));});
  yield return ("research15-boot-covers-four-runtime-and-boot-layers",()=>{var rules=BootPolicyPlan.Filters(Guid.NewGuid());Check(BootPolicyPlan.Layers.All(l=>rules.Count(r=>r.Layer==l)==2));});
  yield return ("research15-replay-rejected-after-serialization",()=>{var h=new SessionHistory{Root="synthetic",InstallationReceipt="receipt"};var id=Guid.NewGuid();h.Accept("synthetic","receipt",id);h=System.Text.Json.JsonSerializer.Deserialize<SessionHistory>(System.Text.Json.JsonSerializer.Serialize(h))!;bool rejected=false;try{h.Accept("synthetic","receipt",id);}catch(SafeError e){rejected=e.Code=="BROKER_SESSION_REPLAY_REJECTED";}Check(rejected);});
  yield return ("research15-replay-other-installation-refused",()=>{var h=new SessionHistory{Root="synthetic",InstallationReceipt="receipt"};bool rejected=false;try{h.Accept("other","receipt",Guid.NewGuid());}catch(SafeError){rejected=true;}Check(rejected&&h.Sessions.Count==0);});
  yield return ("research15-replay-capacity-never-evicts-old-nonce",()=>{var h=new SessionHistory{Root="synthetic",InstallationReceipt="receipt",Sessions=Enumerable.Range(0,16384).Select(_=>Guid.NewGuid()).ToList()};var first=h.Sessions[0];bool rejected=false;try{h.Accept("synthetic","receipt",Guid.NewGuid());}catch(SafeError e){rejected=e.Code=="BROKER_SESSION_HISTORY_FULL";}Check(rejected&&h.Sessions.Count==16384&&h.Sessions[0]==first);});
 }
}

