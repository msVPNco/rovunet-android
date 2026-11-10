using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rovunet.Core;

public enum Severity { Normal, Warning, Critical, Unknown }
public sealed class AccountStatus
{
    public string ServerId { get; init; } = "";
    public string AccountId { get; init; } = "";
    public string PeriodId { get; init; } = "";
    public string State { get; init; } = "";
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool ExpiryKnown {get;init;}=true;
    public long? LimitBytes { get; init; }
    public long? UsedBytes { get; init; }
    public string Metering { get; init; } = "";
    public DateTimeOffset? SampledAt { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public long Revision { get; init; }
    public long? RemainingBytes => LimitBytes is long l && UsedBytes is long u ? Math.Max(0, l - u) : null;
    public string Identity => ServerId + ":" + AccountId + ":" + PeriodId;
    public override string ToString() => "AccountStatus(redacted)";
    public static AccountStatus Read(string text)
    {
        try
        {
            if (text.Length > 65536) throw new SafeError("INVALID_STATUS");
            using var d = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 }); var j = d.RootElement;
            Importer.Keys(j, "version", "server_id", "account_id", "period_id", "account_status", "expires_at", "limit_bytes", "used_bytes", "metering_status", "sampled_at", "generated_at", "revision");
            string Str(string k) => j.GetProperty(k).GetString() ?? throw new SafeError("INVALID_STATUS");
            string Id(string k) { var s = Str(k); return Regex.IsMatch(s, "\\A[0-9a-fA-F]{32}\\z") ? s : throw new SafeError("INVALID_STATUS"); }
            long? Number(string k, bool nullable = false)
            {
                var v = j.GetProperty(k); if (nullable && v.ValueKind == JsonValueKind.Null) return null;
                if (v.ValueKind != JsonValueKind.Number || !Regex.IsMatch(v.GetRawText(), "\\A(?:0|[1-9][0-9]*)\\z") || !v.TryGetInt64(out var n)) throw new SafeError("INVALID_STATUS"); return n;
            }
            DateTimeOffset? Time(string k, bool nullable = false)
            {
                if (nullable && j.GetProperty(k).ValueKind == JsonValueKind.Null) return null;
                var s = Str(k); if (!Regex.IsMatch(s, "(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant) || !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) throw new SafeError("INVALID_STATUS"); return t;
            }
            if (Number("version") != 1) throw new SafeError("INVALID_STATUS");
            var state = Str("account_status"); var metering = Str("metering_status");
            if (state is not ("active" or "expired" or "limit_reached") || metering is not ("complete" or "partial" or "unavailable" or "not_metered")) throw new SafeError("INVALID_STATUS");
            var limit = Number("limit_bytes", true); var used = Number("used_bytes", true); var revision = Number("revision")!.Value;
            if (revision < 1 || (metering == "not_metered" ? limit != null || used != null : limit is null or <= 0 || used == null)) throw new SafeError("INVALID_STATUS");
            return new() { ServerId = Id("server_id"), AccountId = Id("account_id"), PeriodId = Id("period_id"), State = state, Metering = metering, LimitBytes = limit, UsedBytes = used, Revision = revision,
                ExpiresAt = Time("expires_at", true), SampledAt = Time("sampled_at", true), GeneratedAt = Time("generated_at")!.Value };
        }
        catch { throw new SafeError("INVALID_STATUS"); }
    }
    public bool Accepts(AccountStatus previous) => Revision > previous.Revision || Revision == previous.Revision && Identity == previous.Identity && State == previous.State && Metering == previous.Metering && LimitBytes == previous.LimitBytes && UsedBytes == previous.UsedBytes && ExpiresAt == previous.ExpiresAt;
}

public sealed class StatusSnapshot
{
    public AccountStatus? Status { get; init; }
    public DateTimeOffset? ServerDate { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public long ReceivedTick { get; init; } = Stopwatch.GetTimestamp();
    public string Error { get; init; } = "";
    public StatusView Evaluate() => Evaluate(DateTimeOffset.UtcNow, Stopwatch.GetElapsedTime(ReceivedTick));
    public StatusView Evaluate(DateTimeOffset localNow, TimeSpan elapsed)
    {
        var s = Status;
        var clockOK = elapsed >= TimeSpan.Zero && Math.Abs(((localNow - ReceivedAt) - elapsed).TotalSeconds) <= 5;
        var now = clockOK && ServerDate.HasValue ? ServerDate.Value + elapsed : (DateTimeOffset?)null;
        bool Within(DateTimeOffset t) => now.HasValue && (now.Value - t).TotalSeconds is >= -5 and <= 900;
        bool isStatic = s != null && (s.State != "active" || s.LimitBytes == null && s.ExpiresAt == null);
        bool fresh = s != null && now.HasValue && elapsed.TotalMinutes <= 15 && (isStatic || Within(s.GeneratedAt));
        bool measured = s != null && (s.State != "active" || s.Metering == "not_metered" || s.Metering == "complete" && s.SampledAt.HasValue && Within(s.SampledAt.Value));
        if (!fresh || !measured || Error.Length > 0 || s == null) return new(s, now, false, Severity.Unknown);
        var time = s.ExpiresAt - now; double fraction = s.LimitBytes is long l ? (double)s.RemainingBytes!.Value / l : 1;
        var severity = s.State != "active" || time <= TimeSpan.Zero || s.RemainingBytes == 0 || fraction <= .05 || time <= TimeSpan.FromDays(1) ? Severity.Critical : fraction <= .20 || time <= TimeSpan.FromDays(3) ? Severity.Warning : Severity.Normal;
        return new(s, now, true, severity);
    }
}
public sealed record StatusView(AccountStatus? Status, DateTimeOffset? ServerNow, bool Trustworthy, Severity Severity);

public sealed class Subscription
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public string StatusBody { get; set; } = "";
    public SubscriptionUsage20? Usage {get;set;}
    public override string ToString() => "Subscription(redacted)";
}
public static class SubscriptionUrls
{
    public static Uri Validate(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.AbsoluteUri.Length > 4096) throw new SafeError("HTTPS_REQUIRED");
        return uri;
    }
    public static Uri? Status(Uri uri)
    {
        if (!uri.AbsolutePath.StartsWith("/sub/", StringComparison.Ordinal) || !uri.AbsolutePath.EndsWith(".json", StringComparison.Ordinal) || uri.AbsolutePath.EndsWith(".status.json", StringComparison.Ordinal)) return null;
        var b = new UriBuilder(uri) { Path = uri.AbsolutePath[..^5] + ".status.json" }; return b.Uri;
    }
}
