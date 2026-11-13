using System.Diagnostics;
using System.Globalization;

namespace Rovunet.Core;

// Advisory account information, distinct from device counters and profile count.
// Missing fields remain unknown. An explicit zero total/expiry means unlimited.
public sealed class SubscriptionUsage20
{
    public long? Upload{get;set;}
    public long? Download{get;set;}
    public long? Total{get;set;}
    public long? Expire{get;set;}
    public DateTimeOffset ReceivedAt{get;set;}
    public DateTimeOffset? ServerDate{get;set;}
    public bool CachedOnly{get;set;}
    public static SubscriptionUsage20? Parse(string header,string body,DateTimeOffset? date)
    {
        string text=header.Trim();if(text.Length==0)foreach(string line in DecodeBody(body).Split('\n'))if(MetadataLine(line,out var key,out var value)&&key=="subscription-userinfo"){text=value;break;}
        if(text.Length is 0 or >2048)return null;var fields=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(string part in text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)){
            var pair=part.Split('=',2,StringSplitOptions.TrimEntries);if(pair.Length!=2)return null;
            if(pair[0].ToLowerInvariant() is not("upload" or "download" or "total" or "expire"))continue;
            if(!long.TryParse(pair[1],NumberStyles.None,CultureInfo.InvariantCulture,out long value)||value<0||!fields.TryAdd(pair[0],value))return null;
        }
        if(fields.Count==0)return null;
        long? Get(string key)=>fields.TryGetValue(key,out var value)?value:null;
        var result=new SubscriptionUsage20{Upload=Get("upload"),Download=Get("download"),Total=Get("total"),Expire=Get("expire"),ReceivedAt=DateTimeOffset.UtcNow,ServerDate=date};
        try{if(result.Upload is {} up&&result.Download is {} down)_=checked(up+down);if(result.Expire is >0)_=DateTimeOffset.FromUnixTimeSeconds(result.Expire.Value);}catch{return null;}
        return result;
    }
    public StatusSnapshot Snapshot(string source)
    {
        long? used=Upload.HasValue&&Download.HasValue?checked(Upload.Value+Download.Value):null;
        var timestamp=ServerDate??ReceivedAt;var expires=Expire is >0?DateTimeOffset.FromUnixTimeSeconds(Expire.Value):(DateTimeOffset?)null;
        var account=new AccountStatus{ServerId=source[..32],AccountId=source[32..],PeriodId=new string('0',32),State=expires<=timestamp?"expired":Total is >0&&used>=Total?"limit_reached":"active",LimitBytes=Total is >0?Total:null,UsedBytes=used,ExpiresAt=expires,ExpiryKnown=Expire.HasValue,Metering=Total==0?"not_metered":Total is >0&&used.HasValue?"complete":"partial",GeneratedAt=timestamp,SampledAt=used.HasValue?timestamp:null};
        var age=DateTimeOffset.UtcNow-ReceivedAt;return new(){Status=account,ServerDate=ServerDate,ReceivedAt=ReceivedAt,Error=CachedOnly?"ADVISORY_METADATA_UNAVAILABLE":"",ReceivedTick=Stopwatch.GetTimestamp()-(long)(Math.Clamp(age.TotalSeconds,0,31536000)*Stopwatch.Frequency)};
    }
    public static string DecodeBody(string body)
    {
        string value=body.Trim();if(value.Contains("://")||value.StartsWith('{')||value.StartsWith('[')||value.Contains('\n'))return body;
        try{return Importer.Decode(value);}catch{return body;}
    }
    public static bool MetadataLine(string line,out string key,out string value)
    {
        string text=line.Trim().TrimStart('#').TrimStart();int colon=text.IndexOf(':');key=colon>0?text[..colon].Trim().ToLowerInvariant():"";value=colon>0?text[(colon+1)..].Trim():"";
        return key is "subscription-userinfo" or "profile-title" or "profile-update-interval" or "support-url" or "profile-web-page-url" or "announce";
    }
    public static string ConfigBody(string body)=>string.Join('\n',DecodeBody(body).Split('\n').Where(line=>!MetadataLine(line,out _,out _)));
    public override string ToString()=>"SubscriptionUsage20(redacted)";
}
