using System.Text;
using System.Text.Json;
namespace Rovunet.Core;

public sealed class CompatibleImport
{
    public List<Profile> Profiles { get; } = [];
    public Dictionary<string,int> Skipped { get; } = [];
    public Dictionary<string,int> Protocols { get; } = [];
    public HashSet<string> Notices { get; } = [];
    public int Total { get; internal set; }
    public void Skip(string code) => Skipped[code] = Skipped.GetValueOrDefault(code) + 1;
    public void Reject(string code){Total++;Skip(code);}
    public void Merge(CompatibleImport batch)
    {
        Total+=batch.Total;Profiles.AddRange(batch.Profiles);
        foreach(var pair in batch.Skipped)Skipped[pair.Key]=Skipped.GetValueOrDefault(pair.Key)+pair.Value;
        foreach(var pair in batch.Protocols)Protocols[pair.Key]=Protocols.GetValueOrDefault(pair.Key)+pair.Value;
        Notices.UnionWith(batch.Notices);
    }
}
public static class SupportPolicy
{
    // Shipping an adapter is not Windows/network validation. Experimental adapters
    // are included by default. Users may explicitly request core-only admission.
    public static string Reason(Profile p, bool experimental = true) => !p.CanConnect ? p.BlockReason :
        !experimental && (TransportPlan.UsesHelper(p.Raw) || TransportPlan.UsesNaive(p.Raw)) ? "EXPERIMENTAL_PATH" : "";
    public static bool Allowed(Profile p, bool experimental = true) => Reason(p, experimental).Length == 0;
}
public static class ProfileEditing
{
    public static bool IsNative(string raw) => raw.StartsWith("rovunet://") || raw.StartsWith("slipnet://");
    public static bool IsDns(Profile p) => IsNative(p.Raw) && Importer.Fields(p.Raw)[1] is "dnstt" or "dnstt_ssh" or "sayedns" or "sayedns_ssh" or "soar" or "soar_ssh" or "vaydns" or "vaydns_ssh" or "ss" or "slipstream" or "slipstream_ssh";
    public static Profile Replace(Profile previous, string raw)
    {
        var next=Importer.Single(raw);
        if(IsNative(previous.Raw) && Importer.Fields(previous.Raw)[34]=="0" &&
            (!IsNative(next.Raw) || Importer.Fields(next.Raw)[34]!="0")) throw new SafeError("SHARING_DISABLED");
        next.Id=previous.Id;next.SourceId=previous.SourceId;next.ItemId=previous.ItemId;
        next.CollectionId=previous.CollectionId;next.CollectionName=previous.CollectionName;
        return next;
    }
    public static Profile MergeSubscription(string baseRaw, Profile local, Profile incoming,string? originalName=null)
    {
        // Authenticated server restrictions are re-parsed before reconciling any local edit.
        var baseline=Importer.Single(baseRaw);_=Importer.Single(incoming.Raw);
        SubscriptionEndpoint.RequireSame(baseRaw,incoming);
        Profile KeepName(Profile value){if(local.Name!=(originalName??baseline.Name))value.Name=local.Name;return value;}
        if(local.Raw==baseRaw || local.Raw==incoming.Raw)return KeepName(incoming);
        if(incoming.Raw==baseRaw)return KeepName(Replace(incoming,local.Raw));
        if(!IsNative(baseline.Raw) || !IsNative(local.Raw) || !IsNative(incoming.Raw))throw new SafeError("OVERRIDE_CONFLICT");
        static string[] Raw(string value)=>Importer.Decode(value[(value.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
        var prior=Raw(baseRaw);var edited=Raw(local.Raw);var remote=Raw(incoming.Raw);
        if(prior.Length!=edited.Length || edited.Length!=remote.Length || prior[0]!=remote[0] || prior[1]!=remote[1] || prior[1]!=edited[1])throw new SafeError("OVERRIDE_CONFLICT");
        for(int i=2;i<prior.Length;i++){
            if(prior[i]==edited[i] || remote[i]==edited[i])continue;
            if(i is >=31 and <=37 || i is 85 or 86 || prior[i]!=remote[i])throw new SafeError("OVERRIDE_CONFLICT");
            remote[i]=edited[i];
        }
        return Replace(incoming,incoming.Raw[..(incoming.Raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',remote))));
    }
    public static string ChangeNative(Profile p, IReadOnlyDictionary<int,string> edits)
    {
        _=Importer.Single(p.Raw);
        if(!IsNative(p.Raw))throw new SafeError("UNSUPPORTED_FORMAT");
        // Do not use Fields(): v33 credentials there are decoded for runtime use.
        // Preserve every untouched wire field and the v33 credential envelope.
        var f=Importer.Decode(p.Raw[(p.Raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');
        int[] permitted=[2,3,4,5,6,7,11,21,22,27,38,39,41,42,43,44,45,46,47,48,49,51,60,61,65,66,67,78,79,80,81,82,83];
        foreach(var (index,value)in edits)
        {
            if(!permitted.Contains(index)||index>=f.Length||value.Contains('|')||value.Any(char.IsControl)||index==5 && (!TransportPlan.IsSlip(f[1]) || value is not ("0" or "1")))throw new SafeError("INVALID_IMPORT");
            f[index]=value;
        }
        string raw=p.Raw[..(p.Raw.IndexOf("://",StringComparison.Ordinal)+3)]+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',f)));
        _=Replace(p,raw);return raw;
    }
}
