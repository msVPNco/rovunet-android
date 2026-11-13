namespace Rovunet.Core;

// Draft planning model only: no Apply/Save operation and no credential/pin values.
public sealed record SubscriptionBoundary(string SourceId,string AccountId,string PeriodId);
public sealed record MigrationItemPreview(string ItemId,string Change,string? BeforeProfile,string? AfterProfile,string? BeforeEndpoint,string? AfterEndpoint,bool HadOverride,bool WasSelected,bool HadTrust,bool TrustTransferAllowed=false);
public sealed record SubscriptionMigrationPreview(SubscriptionBoundary Before,SubscriptionBoundary After,MigrationItemPreview[] Items,bool AddParallelSource,bool RequiresExplicitDecision=true)
{
    public static SubscriptionMigrationPreview Plan(SubscriptionBoundary before,SubscriptionBoundary after,IReadOnlyList<Profile> previous,IReadOnlyList<Profile> incoming,IReadOnlySet<string> overrides,IReadOnlySet<string> trusted,string selected,IReadOnlySet<string>? removedHistory=null)
    {
        string Key(Profile p)=>p.ItemId.Length>0?p.ItemId:Importer.Fingerprint(p.Raw);
        Dictionary<string,Profile> Index(IReadOnlyList<Profile> list){var result=new Dictionary<string,Profile>(StringComparer.Ordinal);foreach(var p in list)if(!result.TryAdd(Key(p),p))throw new SafeError("SUBSCRIPTION_IDENTITY_CONFLICT");return result;}
        var old=Index(previous);var fresh=Index(incoming);var rows=new List<MigrationItemPreview>();
        foreach(string id in old.Keys.Concat(fresh.Keys).Distinct(StringComparer.Ordinal)){
            old.TryGetValue(id,out var a);fresh.TryGetValue(id,out var b);
            string? oldEndpoint=a==null?null:SubscriptionEndpoint.Binding(a),newEndpoint=b==null?null:SubscriptionEndpoint.Binding(b);
            string change=a==null?(removedHistory?.Contains(id)==true?"returned":"added"):b==null?"removed":oldEndpoint!=newEndpoint?"endpoint-changed":"same-endpoint";
            rows.Add(new(id,change,a?.Id,b?.Id,oldEndpoint,newEndpoint,a!=null&&overrides.Contains(a.Id),a?.Id==selected,a!=null&&trusted.Contains(a.Id)));
        }
        return new(before,after,rows.ToArray(),before.SourceId!=after.SourceId);
    }
}
