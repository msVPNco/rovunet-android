namespace Rovunet.Core;
public sealed class CollectionProbePlan
{
    public Profile[] Direct {get;}
    public Profile[] Dns {get;}
    public CollectionProbePlan(IEnumerable<Profile> profiles,IReadOnlyDictionary<string,long>? verifiedLatency=null)
    {
        var all=profiles.ToArray();
        Direct=all.Where(p=>!ProfileEditing.IsDns(p)).OrderBy(p=>verifiedLatency!=null&&verifiedLatency.TryGetValue(p.Id,out long time)&&time>=0?time:long.MaxValue).ToArray();
        Dns=all.Where(ProfileEditing.IsDns).ToArray();
    }
    public bool NeedsDns(bool cancelled,int successfulDirect)=>!cancelled&&successfulDirect==0&&Dns.Length>0;
    public static Profile[] DisplayOrder(IEnumerable<Profile> profiles,IReadOnlyDictionary<string,long> passed)
    {
        var all=profiles.ToArray();bool direct=all.Any(p=>!ProfileEditing.IsDns(p)&&passed.ContainsKey(p.Id));
        return all.OrderBy(p=>passed.ContainsKey(p.Id)?ProfileEditing.IsDns(p)&&direct?2:0:ProfileEditing.IsDns(p)?3:1)
            .ThenBy(p=>passed.GetValueOrDefault(p.Id,long.MaxValue)).ToArray();
    }
}
