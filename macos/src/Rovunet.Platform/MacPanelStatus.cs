using Rovunet.Core;

namespace Rovunet.Platform;

public static class MacPanelStatus
{
    // Only map the two private feed aliases. Keep the exact HTTPS authority and query.
    public static Uri? StatusUri(Uri source)
    {
        _=SubscriptionUrls.Validate(source.AbsoluteUri);
        var path=source.AbsolutePath;
        string prefix=path.StartsWith("/v2/sub/",StringComparison.Ordinal)?"/v2/sub/":"/sub/";
        if(!path.StartsWith(prefix,StringComparison.Ordinal)||!path.EndsWith(".json",StringComparison.Ordinal))return null;
        var token=path[prefix.Length..^5];
        if(token.EndsWith(".windows",StringComparison.Ordinal))token=token[..^8];
        if(token.Length==0||token.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '-' and not '_'))return null;
        return new UriBuilder(source){Path=path[..^5]+".status.json"}.Uri;
    }

    public static ImportReview ReadFeed(Uri source,string body)
    {
        if(source.AbsolutePath.StartsWith("/v2/sub/",StringComparison.Ordinal)&&StatusUri(source)!=null){
            try{
                using var document=System.Text.Json.JsonDocument.Parse(body.TrimStart('\uFEFF'),new(){MaxDepth=16});
                if(document.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Object||!document.RootElement.TryGetProperty("version",out _))
                    throw new SafeError("INVALID_IMPORT");
            }catch(System.Text.Json.JsonException){throw new SafeError("INVALID_IMPORT");}
        }
        return ImportReview.Read(body,reviewTlsRequests:true);
    }

    public static bool SameAuthority(Uri first,Uri second)=>second.IsAbsoluteUri&&second.Scheme=="https"&&
        second.UserInfo.Length==0&&first.Port==second.Port&&string.Equals(first.IdnHost,second.IdnHost,StringComparison.OrdinalIgnoreCase);

    public static StatusView Evaluate(AccountStatus status,StatusReceipt? receipt,string session,DateTimeOffset now,TimeSpan elapsed)
    {
        return new StatusSnapshot {
            Status=status,ReceivedAt=receipt?.Received??now,ServerDate=receipt?.ServerDate,
            Error=receipt==null||receipt.Session!=session?"STATUS_STALE":""
        }.Evaluate(now,elapsed);
    }
}
