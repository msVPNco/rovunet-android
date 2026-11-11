using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly Dictionary<string,SubscriptionUsage20> subscriptionUsage20=[];
    readonly HashSet<string> ownAccountSources20=[];
    readonly HashSet<string> updatingSubscriptions20=[];
    readonly System.Windows.Threading.DispatcherTimer usageRefresh20=new(){Interval=TimeSpan.FromMinutes(10)};
    readonly CancellationTokenSource usageLifetime20=new();
    bool refreshingUsage20;
    async Task RefreshAllUsage20()
    {
        if(refreshingUsage20||fixtureMode||closing)return;refreshingUsage20=true;
        try{
            foreach(var sub in data.Subscriptions.ToArray()){
                if(usageLifetime20.IsCancellationRequested)return;if(PublicFeed.IsKnown(sub.Url))continue;
                try{
                    var uri=SubscriptionUrls.Validate(sub.Url);
                    if(SubscriptionUrls.Status(uri)!=null)await RefreshStatus(uri,sub,usageLifetime20.Token);
                    else{
                        var payload=await SubscriptionClient.Fetch(uri,state.State=="connected"?state.HttpPort:0,cancellation:usageLifetime20.Token);CaptureUsage20(sub.Id,payload);
                        if(subscriptionUsage20.TryGetValue(sub.Id,out var usage)){sub.Usage=usage;Save();}
                    }
                    RefreshCards();
                }catch{ /* Retain the labelled cached snapshot when the network is unavailable. */ }
            }
        }finally{refreshingUsage20=false;}
    }
    void CaptureUsage20(string source,SubscriptionClient.Payload payload)
    {
        var usage=SubscriptionUsage20.Parse(payload.UserInfo,payload.Body,payload.Date);if(usage==null)return;
        subscriptionUsage20[source]=usage;
        if(!ownAccountSources20.Contains(source)||!statuses.TryGetValue(source,out var current)||current.Status==null||!current.Evaluate().Trustworthy)statuses[source]=usage.Snapshot(source);
    }
    void CommitSubscription20(Uri uri,List<Profile> imported,string name)
    {
        if(imported.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");string source=Importer.Fingerprint(uri.AbsoluteUri);
        var old=data.Profiles.Where(p=>p.SourceId==source).GroupBy(p=>p.ItemId.Length>0?p.ItemId:Importer.Fingerprint(p.Raw)).ToDictionary(g=>g.Key,g=>g.First());
        string collectionName=name.Trim().Length>0?name.Trim():data.Subscriptions.FirstOrDefault(s=>s.Id==source)?.Name??PublicFeed.NameFor(uri.AbsoluteUri);
        var overrides=new Dictionary<string,string>(data.LocalOverrideSources);var names=new Dictionary<string,string>(data.LocalOverrideNames);
        foreach(var profile in imported){
            profile.SourceId=source;profile.CollectionId="sub:"+source;if(collectionName.Length>0)profile.CollectionName=collectionName;
            if(profile.ItemId.Length==0)profile.ItemId=Importer.Fingerprint(profile.Raw);
            if(!old.TryGetValue(profile.ItemId,out var prior))continue;profile.Id=prior.Id;
            if(data.LocalOverrideSources.TryGetValue(prior.Id,out string? baseline)){
                string incoming=profile.Raw,incomingName=profile.Name;var local=ProfileEditing.MergeSubscription(baseline,prior,profile,data.LocalOverrideNames.GetValueOrDefault(prior.Id));
                profile.Raw=local.Raw;profile.Name=local.Name;profile.Protocol=local.Protocol;profile.BlockReason=local.BlockReason;overrides[prior.Id]=incoming;names[prior.Id]=incomingName;
            }
        }
        var profiles=data.Profiles.Where(p=>p.SourceId!=source||!SupportPolicy.Allowed(p,data.IncludeExperimental)).Concat(imported).GroupBy(p=>p.Id).Select(g=>g.Last()).ToList();
        var subscriptions=data.Subscriptions.Where(s=>s.Id!=source).ToList();var previousSub=data.Subscriptions.FirstOrDefault(s=>s.Id==source);
        subscriptions.Add(new(){Id=source,Url=uri.AbsoluteUri,Name=imported[0].CollectionName.Length>0?imported[0].CollectionName:L.Text("COLLECTION"),StatusBody=statusBodies.GetValueOrDefault(source,previousSub?.StatusBody??""),Usage=subscriptionUsage20.GetValueOrDefault(source)??previousSub?.Usage});
        var previous=(data.Profiles,data.Subscriptions,data.LocalOverrideSources,data.LocalOverrideNames);var timestamp=data.SubscriptionUpdated.GetValueOrDefault(source);
        data.Profiles=profiles;data.Subscriptions=subscriptions;data.LocalOverrideSources=overrides;data.LocalOverrideNames=names;data.SubscriptionUpdated[source]=DateTimeOffset.UtcNow;
        try{Save();}catch{(data.Profiles,data.Subscriptions,data.LocalOverrideSources,data.LocalOverrideNames)=previous;if(timestamp==default)data.SubscriptionUpdated.Remove(source);else data.SubscriptionUpdated[source]=timestamp;throw;}
        BuildCards();
    }
    async Task UpdateCollection20(string source)
    {
        var sub=data.Subscriptions.FirstOrDefault(s=>s.Id==source);if(sub==null||!updatingSubscriptions20.Add(source))return;
        try{
            RefreshCards();Notify(L.Text("SUBSCRIPTION_UPDATING"));var uri=SubscriptionUrls.Validate(sub.Url);bool publicFeed=PublicFeed.IsKnown(sub.Url);int port=state.State=="connected"?state.HttpPort:0;
            if(!publicFeed)await RefreshStatus(uri,sub);
            var payload=publicFeed?await PublicFeed.Fetch(sub.Url,proxyPort:port):await SubscriptionClient.Fetch(uri,port);
            if(!publicFeed)CaptureUsage20(source,payload);
            var batch=publicFeed?PublicFeed.Preview(payload.Body,data.IncludeExperimental):Importer.ReadCompatible(SubscriptionUsage20.ConfigBody(payload.Body),data.IncludeExperimental);
            CommitSubscription20(uri,batch.Profiles,sub.Name);Notify(L.Text("SUBSCRIPTION_UPDATED")+" · "+ImportSummary(batch));
        }catch(Exception error){Notify(L.Text(error is SafeError safe?safe.Code:"FETCH_FAILED"));}
        finally{updatingSubscriptions20.Remove(source);RefreshCards();}
    }
}
