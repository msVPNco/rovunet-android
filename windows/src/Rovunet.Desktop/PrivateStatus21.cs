using System.Diagnostics;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    readonly object statusCommitGate21=new();
    readonly Dictionary<string,long> statusEpoch21=[];
    readonly Dictionary<string,int> statusSchemas21=[];
    readonly Dictionary<string,Status2Authority> statusAuthorities21=[];
    readonly Dictionary<string,string> statusOrigins21=[];
    static bool V2Feed21(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&PrivateStatusProtocol.V2Status(uri)!=null;
    void RequireV2StatusReady21(Uri uri,string source)
    {
        if(V2Feed21(uri.AbsoluteUri)&&statuses.TryGetValue(source,out var snapshot)&&snapshot.Error.Length>0)throw new SafeError(snapshot.Error);
    }
    void LoadPrivateStatus21(Subscription sub)
    {
        ownAccountSources20.Add(sub.Id);
        if(sub.StatusBody.Length==0&&sub.StatusSchemaVersion!=2){statuses[sub.Id]=new(){Error="ACCOUNT_UNKNOWN"};return;}
        try{
            var value=PrivateStatusProtocol.ReadStored(sub);
            if(value.BodyVersion==2&&!data.Status2Authorities.ContainsKey(PrivateStatusProtocol.Scope(sub.StatusOrigin,value.ServerId,value.AccountId)))throw new SafeError("INVALID_STATUS");
            statuses[sub.Id]=new(){Status=value,Error="ACCOUNT_STALE"};statusBodies[sub.Id]=sub.StatusBody;
            statusSchemas21[sub.Id]=sub.StatusSchemaVersion;statusOrigins21[sub.Id]=sub.StatusOrigin;
            if(sub.Status2Authority!=null)statusAuthorities21[sub.Id]=sub.Status2Authority;
        }catch{statuses[sub.Id]=new(){Error="INVALID_STATUS"};}
    }
    Status2Authority? SharedStatus21(string origin,string server,string account)
    {
        string key=PrivateStatusProtocol.Scope(origin,server,account);
        if(data.Status2Authorities.TryGetValue(key,out var saved))return saved;
        if(data.Subscriptions.Any(s=>s.StatusSchemaVersion==2&&s.StatusOrigin==origin&&
            (s.Status2Authority==null||s.Status2Authority.ServerId==server&&s.Status2Authority.AccountId==account)))throw new SafeError("INVALID_STATUS");
        return null;
    }
    Status2Authority? StatusBase21(Uri uri,string id,Subscription? sub,AccountStatusV2 next,AccountStatus? previous)
    {
        if(previous!=null&&(!next.ServerId.Equals(previous.ServerId,StringComparison.OrdinalIgnoreCase)||!next.AccountId.Equals(previous.AccountId,StringComparison.OrdinalIgnoreCase)))throw new SafeError("ACCOUNT_BINDING_CHANGED");
        var own=sub?.Status2Authority??statusAuthorities21.GetValueOrDefault(id);
        if(own!=null&&(own.ServerId!=next.ServerId||own.AccountId!=next.AccountId))throw new SafeError("ACCOUNT_BINDING_CHANGED");
        if(sub?.StatusSchemaVersion==2){
            var savedKey=PrivateStatusProtocol.Scope(sub.StatusOrigin,next.ServerId,next.AccountId);
            if(!data.Status2Authorities.TryGetValue(savedKey,out var saved))throw new SafeError("INVALID_STATUS");
            own=Status2Authority.Merge(own,saved);
        }
        return Status2Authority.Merge(own,SharedStatus21(PrivateStatusProtocol.Origin(uri),next.ServerId,next.AccountId));
    }
    (Status2Authority Authority,string Origin,string Scope)? PrepareStatusCommit21(string source,Subscription? prior)
    {
        int version=statusSchemas21.GetValueOrDefault(source,prior?.StatusSchemaVersion??0);
        if(version!=2)return null;
        string body=statusBodies.GetValueOrDefault(source,prior?.StatusBody??"");
        var dto=AccountStatusV2.Read(body);
        string origin=statusOrigins21.GetValueOrDefault(source,prior?.StatusOrigin??"");
        string scope=PrivateStatusProtocol.Scope(origin,dto.ServerId,dto.AccountId);
        var own=statusAuthorities21.GetValueOrDefault(source)??prior?.Status2Authority;
        if(own==null)throw new SafeError("INVALID_STATUS");
        _=PrivateStatusProtocol.ReadStored(new(){StatusSchemaVersion=2,StatusBody=body,Status2Authority=own});
        var basis=Status2Authority.Merge(own,SharedStatus21(origin,dto.ServerId,dto.AccountId));
        return(Status2Authority.Advance(basis,dto,null,null),origin,scope);
    }
    void InvalidateAliases21(string source,string origin,Status2Authority authority)
    {
        foreach(var pair in statuses.ToArray()){
            var s=pair.Value.Status;
            if(pair.Key==source||s?.BodyVersion!=2||s.ServerId!=authority.ServerId||s.AccountId!=authority.AccountId||statusOrigins21.GetValueOrDefault(pair.Key)!=origin)continue;
            if(s.PeriodId==authority.CurrentPeriodId&&s.Revision>=authority.Periods[authority.CurrentPeriodId].Revision)continue;
            var prior=pair.Value;statuses[pair.Key]=new(){Status=s,ServerDate=prior.ServerDate,ReceivedAt=prior.ReceivedAt,ReceivedTick=prior.ReceivedTick,Error="ACCOUNT_STALE"};
        }
    }
    async Task RefreshPrivateStatus21(Uri uri,Subscription? subscription,CancellationToken cancellation)
    {
        string id=subscription?.Id??Importer.Fingerprint(uri.AbsoluteUri);
        string? originalUrl=subscription?.Url;var endpoint=SubscriptionUrls.Status(uri);long epoch;
        lock(statusCommitGate21){epoch=statusEpoch21.GetValueOrDefault(id)+1;statusEpoch21[id]=epoch;}
        bool Current()=>statusEpoch21.GetValueOrDefault(id)==epoch&&!cancellation.IsCancellationRequested&&
            (subscription==null||subscription.Url==originalUrl&&ReferenceEquals(data.Subscriptions.FirstOrDefault(s=>s.Id==id),subscription));
        if(endpoint==null){lock(statusCommitGate21){if(Current())statuses[id]=new(){Status=statuses.GetValueOrDefault(id)?.Status,Error="INVALID_STATUS"};}return;}
        ownAccountSources20.Add(id);int version=PrivateStatusProtocol.ExpectedVersion(uri);
        try{
            var payload=await SubscriptionClient.Fetch(endpoint,state.State=="connected"?state.HttpPort:0,65536,cancellation);
            lock(statusCommitGate21){
                if(!Current())return;
                statuses.TryGetValue(id,out var prior);
                var previous=prior?.Status??(subscription?.StatusBody.Length>0?PrivateStatusProtocol.ReadStored(subscription):null);
                AccountStatus next;Status2Authority? authority=null;string origin=PrivateStatusProtocol.Origin(uri);string? scope=null;
                if(version==2){
                    var dto=AccountStatusV2.Read(payload.Body);
                    if(payload.Date is not {} transportDate||(dto.GeneratedAt-transportDate).TotalSeconds>5)throw new SafeError("ACCOUNT_STALE");
                    authority=Status2Authority.Advance(StatusBase21(uri,id,subscription,dto,previous),dto,previous,payload.Date);
                    next=dto.ToStatus();scope=PrivateStatusProtocol.Scope(origin,dto.ServerId,dto.AccountId);
                }else{
                    if(previous?.BodyVersion==2)throw new SafeError("INVALID_STATUS");
                    next=AccountStatus.Read(payload.Body);
                    if(previous!=null&&(!next.ServerId.Equals(previous.ServerId,StringComparison.OrdinalIgnoreCase)||!next.AccountId.Equals(previous.AccountId,StringComparison.OrdinalIgnoreCase)))throw new SafeError("ACCOUNT_BINDING_CHANGED");
                    if(previous!=null&&!next.Accepts(previous))throw new SafeError("INVALID_STATUS");
                }
                if(subscription!=null){
                    var old=(subscription.StatusBody,subscription.StatusSchemaVersion,subscription.Status2Authority,subscription.StatusOrigin);
                    var oldAuthorities=data.Status2Authorities;
                    subscription.StatusBody=payload.Body;subscription.StatusSchemaVersion=version;subscription.Status2Authority=authority;subscription.StatusOrigin=origin;
                    if(scope!=null)data.Status2Authorities=new(oldAuthorities){[scope]=authority!};
                    try{Save();}catch{(subscription.StatusBody,subscription.StatusSchemaVersion,subscription.Status2Authority,subscription.StatusOrigin)=old;data.Status2Authorities=oldAuthorities;throw;}
                }
                statusBodies[id]=payload.Body;statusSchemas21[id]=version;statusOrigins21[id]=origin;
                if(authority!=null)statusAuthorities21[id]=authority;
                statuses[id]=new(){Status=next,ServerDate=payload.Date};
                if(authority!=null)InvalidateAliases21(id,origin,authority);
            }
        }catch(Exception error){
            lock(statusCommitGate21){
                if(!Current())return;
                statuses.TryGetValue(id,out var prior);
                statuses[id]=new(){Status=prior?.Status,ServerDate=prior?.ServerDate,ReceivedAt=prior?.ReceivedAt??DateTimeOffset.UtcNow,ReceivedTick=prior?.ReceivedTick??Stopwatch.GetTimestamp(),Error=error is SafeError safe?safe.Code:"INVALID_STATUS"};
            }
        }
    }
}
