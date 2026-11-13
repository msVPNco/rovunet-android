using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Rovunet.Core;

// Deliberately independent of the legacy private-status v1 parser.
public sealed record AccountStatusV2
{
    public string ServerId {get;init;}="";
    public string AccountId {get;init;}="";
    public string PeriodId {get;init;}="";
    public long Revision {get;init;}
    public string State {get;init;}="";
    public string LimitKind {get;init;}="";
    public long? LimitBytes {get;init;}
    public string ExpiryKind {get;init;}="";
    public DateTimeOffset? ExpiresAt {get;init;}
    public long? ConfirmedBytes {get;init;}
    public long? PendingBytes {get;init;}
    public string Coverage {get;init;}="";
    public string Measurement {get;init;}="";
    public DateTimeOffset? SampledAt {get;init;}
    public DateTimeOffset GeneratedAt {get;init;}
    public static AccountStatusV2 Read(string body)
    {
        try {
            if(body.Length>65536)throw new SafeError("INVALID_STATUS");
            using var doc=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=4});var j=doc.RootElement;
            Importer.Keys(j,"version","server_id","account_id","period_id","revision","account_status","limit_kind","limit_bytes","expiry_kind","expires_at","confirmed_bytes","pending_bytes","coverage","measurement","sampled_at","generated_at");
            string Text(string key)=>j.GetProperty(key).GetString()??throw new SafeError("INVALID_STATUS");
            string Id(string key){var v=Text(key);return Regex.IsMatch(v,"\\A[0-9a-f]{32}\\z")?v:throw new SafeError("INVALID_STATUS");}
            string Choice(string key,params string[] choices){var v=Text(key);return choices.Contains(v,StringComparer.Ordinal)?v:throw new SafeError("INVALID_STATUS");}
            long? Number(string key,bool nullable=false){var v=j.GetProperty(key);if(nullable&&v.ValueKind==JsonValueKind.Null)return null;if(v.ValueKind!=JsonValueKind.Number||!Regex.IsMatch(v.GetRawText(),"\\A(?:0|[1-9][0-9]*)\\z")||!v.TryGetInt64(out long n))throw new SafeError("INVALID_STATUS");return n;}
            DateTimeOffset? Time(string key,bool nullable=false){if(nullable&&j.GetProperty(key).ValueKind==JsonValueKind.Null)return null;var s=Text(key);if(!Regex.IsMatch(s,"\\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]{1,9})?(?:Z|\\+00:00)\\z")||!DateTimeOffset.TryParse(s,CultureInfo.InvariantCulture,DateTimeStyles.None,out var value)||value.Offset!=TimeSpan.Zero)throw new SafeError("INVALID_STATUS");return value;}
            if(Number("version")!=2)throw new SafeError("INVALID_STATUS");
            var limitKind=Choice("limit_kind","finite","unlimited","unknown");var limit=Number("limit_bytes",true);
            var expiryKind=Choice("expiry_kind","at","none","unknown");var expiry=Time("expires_at",true);
            if((limitKind=="finite"?limit is null or <=0:limit!=null)||(expiryKind=="at"?expiry==null:expiry!=null))throw new SafeError("INVALID_STATUS");
            long revision=Number("revision")!.Value;if(revision<1)throw new SafeError("INVALID_STATUS");
            return new(){ServerId=Id("server_id"),AccountId=Id("account_id"),PeriodId=Id("period_id"),Revision=revision,
                State=Choice("account_status","active","expired","limit_reached","hold","archived"),LimitKind=limitKind,LimitBytes=limit,ExpiryKind=expiryKind,ExpiresAt=expiry,
                ConfirmedBytes=Number("confirmed_bytes",true),PendingBytes=Number("pending_bytes",true),Coverage=Choice("coverage","partial","unavailable","not_metered"),Measurement=Choice("measurement","accounted_lower_bound","none","unknown"),SampledAt=Time("sampled_at",true),GeneratedAt=Time("generated_at")!.Value};
        } catch {throw new SafeError("INVALID_STATUS");}
    }
    public AccountStatus ToStatus()=>new(){BodyVersion=2,ServerId=ServerId,AccountId=AccountId,PeriodId=PeriodId,Revision=Revision,State=State,LimitKind=LimitKind,LimitBytes=LimitBytes,ExpiryKnown=ExpiryKind!="unknown",ExpiresAt=ExpiresAt,UsedBytes=ConfirmedBytes,PendingBytes=PendingBytes,Metering=Coverage,Measurement=Measurement,SampledAt=SampledAt,GeneratedAt=GeneratedAt};
    public string SemanticKey()=>JsonSerializer.Serialize(new{State,LimitKind,LimitBytes,ExpiryKind,ExpiresAt,ConfirmedBytes,PendingBytes,Coverage,Measurement});
}

public sealed class Status2Watermark
{
    public long Revision {get;set;}
    public long? ConfirmedBytes {get;set;}
    public string SemanticKey {get;set;}="";
}
// The whole checkpoint is replaced in the same private-store save as StatusBody.
public sealed class Status2Authority
{
    public string ServerId {get;set;}="";
    public string AccountId {get;set;}="";
    public string CurrentPeriodId {get;set;}="";
    public DateTimeOffset GeneratedAt {get;set;}
    public Dictionary<string,Status2Watermark> Periods {get;set;}=[];
    public static Status2Authority? Merge(Status2Authority? first,Status2Authority? second)
    {
        if(first==null)return second;if(second==null)return first;
        if(first.ServerId!=second.ServerId||first.AccountId!=second.AccountId)throw new SafeError("ACCOUNT_BINDING_CHANGED");
        string current=first.CurrentPeriodId;
        if(current!=second.CurrentPeriodId){
            if(second.Periods.ContainsKey(current)&&!first.Periods.ContainsKey(second.CurrentPeriodId))current=second.CurrentPeriodId;
            else if(!first.Periods.ContainsKey(second.CurrentPeriodId)||second.Periods.ContainsKey(current))throw new SafeError("INVALID_STATUS");
        }
        var merged=new Status2Authority{ServerId=first.ServerId,AccountId=first.AccountId,CurrentPeriodId=current,GeneratedAt=first.GeneratedAt>second.GeneratedAt?first.GeneratedAt:second.GeneratedAt};
        foreach(var pair in first.Periods.Concat(second.Periods)){
            if(!merged.Periods.TryGetValue(pair.Key,out var old)){merged.Periods[pair.Key]=new(){Revision=pair.Value.Revision,ConfirmedBytes=pair.Value.ConfirmedBytes,SemanticKey=pair.Value.SemanticKey};continue;}
            if(old.Revision==pair.Value.Revision&&old.SemanticKey.Length>0&&pair.Value.SemanticKey.Length>0&&old.SemanticKey!=pair.Value.SemanticKey)throw new SafeError("INVALID_STATUS");
            if(pair.Value.Revision>old.Revision){old.Revision=pair.Value.Revision;old.SemanticKey=pair.Value.SemanticKey;}
            else if(pair.Value.Revision==old.Revision&&old.SemanticKey.Length==0)old.SemanticKey=pair.Value.SemanticKey;
            if(pair.Value.ConfirmedBytes is {} confirmed&&(old.ConfirmedBytes==null||confirmed>old.ConfirmedBytes))old.ConfirmedBytes=confirmed;
        }
        if(merged.Periods.Count>16384)throw new SafeError("INVALID_STATUS");return merged;
    }
    public static Status2Authority Advance(Status2Authority? previous,AccountStatusV2 next,AccountStatus? legacy,DateTimeOffset? serverDate)
    {
        var result=previous==null?new Status2Authority{ServerId=next.ServerId,AccountId=next.AccountId}:new Status2Authority{ServerId=previous.ServerId,AccountId=previous.AccountId,CurrentPeriodId=previous.CurrentPeriodId,GeneratedAt=previous.GeneratedAt,Periods=previous.Periods.ToDictionary(x=>x.Key,x=>new Status2Watermark{Revision=x.Value.Revision,ConfirmedBytes=x.Value.ConfirmedBytes,SemanticKey=x.Value.SemanticKey})};
        if(previous==null&&legacy!=null){
            if(!next.ServerId.Equals(legacy.ServerId,StringComparison.OrdinalIgnoreCase)||!next.AccountId.Equals(legacy.AccountId,StringComparison.OrdinalIgnoreCase))throw new SafeError("ACCOUNT_BINDING_CHANGED");
            result.CurrentPeriodId=legacy.PeriodId.ToLowerInvariant();result.GeneratedAt=legacy.GeneratedAt;
            result.Periods[result.CurrentPeriodId]=new(){Revision=legacy.Revision,ConfirmedBytes=legacy.UsedBytes};
        }
        if(result.ServerId!=next.ServerId||result.AccountId!=next.AccountId)throw new SafeError("ACCOUNT_BINDING_CHANGED");
        if(next.GeneratedAt<result.GeneratedAt)throw new SafeError("INVALID_STATUS");
        if(result.CurrentPeriodId.Length>0&&result.CurrentPeriodId!=next.PeriodId){
            // Old periods can never become current again, even with a higher revision.
            if(result.Periods.ContainsKey(next.PeriodId)||serverDate==null||(serverDate.Value-next.GeneratedAt).TotalSeconds is < -5 or >900)throw new SafeError("INVALID_STATUS");
        }
        if(result.Periods.TryGetValue(next.PeriodId,out var watermark)){
            if(next.Revision<watermark.Revision||next.Revision==watermark.Revision&&watermark.SemanticKey.Length>0&&next.SemanticKey()!=watermark.SemanticKey||next.ConfirmedBytes is {} n&&watermark.ConfirmedBytes is {} old&&n<old)throw new SafeError("INVALID_STATUS");
        }
        if(!result.Periods.ContainsKey(next.PeriodId)&&result.Periods.Count>=16384)throw new SafeError("INVALID_STATUS");
        result.Periods[next.PeriodId]=new(){Revision=next.Revision,ConfirmedBytes=next.ConfirmedBytes??watermark?.ConfirmedBytes,SemanticKey=next.SemanticKey()};
        result.CurrentPeriodId=next.PeriodId;result.GeneratedAt=next.GeneratedAt;return result;
    }
}

public static class PrivateStatusProtocol
{
    public static string Origin(Uri uri)=>uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    public static string Scope(string origin,string server,string account)
    {
        if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||Origin(uri)!=origin||uri.AbsolutePath!="/"||!Regex.IsMatch(server,"\\A[0-9a-f]{32}\\z")||!Regex.IsMatch(account,"\\A[0-9a-f]{32}\\z"))throw new SafeError("INVALID_STATUS");
        return origin+"|"+server+":"+account;
    }
    public static Uri? V2Status(Uri feed)
    {
        if(feed.Scheme!="https"||feed.UserInfo.Length!=0||feed.Query.Length!=0||feed.Fragment.Length!=0)return null;
        int authority=feed.OriginalString.IndexOf("://",StringComparison.Ordinal)+3;
        int slash=feed.OriginalString.IndexOf('/',authority);if(slash<0)return null;
        string rawPath=feed.OriginalString[slash..];
        if(rawPath!=feed.AbsolutePath||rawPath.Contains('%')||!Regex.IsMatch(rawPath,"\\A/v2/sub/[0-9a-f]{32}(?:\\.windows)?\\.json\\z"))return null;
        return new UriBuilder(feed){Path=rawPath[..^5]+".status.json"}.Uri;
    }
    public static int ExpectedVersion(Uri feed)=>V2Status(feed)!=null?2:1;
    public static AccountStatus ReadStored(Subscription sub)
    {
        if(sub.StatusSchemaVersion is 0 or 1)return AccountStatus.Read(sub.StatusBody);
        if(sub.StatusSchemaVersion!=2)throw new SafeError("INVALID_STATUS");
        var body=AccountStatusV2.Read(sub.StatusBody);var authority=sub.Status2Authority;
        if(authority==null||authority.ServerId!=body.ServerId||authority.AccountId!=body.AccountId||authority.CurrentPeriodId!=body.PeriodId||authority.GeneratedAt!=body.GeneratedAt||!authority.Periods.TryGetValue(body.PeriodId,out var water)||water.Revision!=body.Revision||water.SemanticKey!=body.SemanticKey())throw new SafeError("INVALID_STATUS");
        if(body.ConfirmedBytes is {} confirmed&&(water.ConfirmedBytes==null||water.ConfirmedBytes<confirmed))throw new SafeError("INVALID_STATUS");
        return body.ToStatus();
    }
}
