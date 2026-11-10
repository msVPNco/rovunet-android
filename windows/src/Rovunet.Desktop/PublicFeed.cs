using System.Text;
using System.Text.RegularExpressions;
using Rovunet.Core;

namespace Rovunet.Desktop;

// منابع عمومی شخص ثالث. هیچ دانلودی بدون اقدام صریح کاربر انجام نمی‌شود
// و هیچ پروفایل قبلی هنگام خطا حذف نمی‌شود.
public static class PublicFeed
{
    public sealed record Preset(string Key, string Name, string Url, bool Expand);

    public static readonly Preset[] Presets =
    [
        new("mahsa", "Mahsa", "https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa", true),
        new("anonymous", "Anonymous", "https://raw.githubusercontent.com/4n0nymou3/multi-proxy-config-fetcher/refs/heads/main/configs/proxy_configs.txt", false),
        new("patterniha", "Patterniha", "https://raw.githubusercontent.com/patterniha/Free-Configs/refs/heads/main/configs.txt", false),
    ];

    static readonly Regex MahsaChild = new(@"^https://raw\.githubusercontent\.com/mahsanet/MahsaFreeConfig/(?:refs/heads/main|main)/(?:mci|mtn)/sub_[1-4]\.txt$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsKnown(string url) => Presets.Any(p => string.Equals(p.Url, url, StringComparison.Ordinal));

    public static string NameFor(string url) => Presets.FirstOrDefault(p => string.Equals(p.Url, url, StringComparison.Ordinal))?.Name ?? "";

    public static async Task<SubscriptionClient.Payload> Fetch(string url, CancellationToken cancellation = default, int proxyPort = 0)
    {
        var first = await SubscriptionClient.Fetch(new Uri(url), proxyPort, cancellation: cancellation);
        if (!Presets.Any(p => p.Expand && string.Equals(p.Url, url, StringComparison.Ordinal))) return first;

        var lines = first.Body.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith('#'))
            .ToList();
        if (lines.Count is < 1 or > 8 || lines.Distinct(StringComparer.Ordinal).Count() != lines.Count || lines.Any(l => !MahsaChild.IsMatch(l)))
            throw new SafeError("FETCH_FAILED");

        var combined = new StringBuilder();
        foreach (var child in lines)
        {
            cancellation.ThrowIfCancellationRequested();
            var payload = await SubscriptionClient.Fetch(new Uri(child), proxyPort, Importer.MaxBytes / 8, cancellation);
            string body=payload.Body.Trim();if(body.Length==0)continue;
            // Each child is a separate base64 subscription, not one segment of a combined base64 document.
            if(!body.Contains("://")&&!body.StartsWith('{')&&!body.StartsWith('['))body=Importer.Decode(body);
            combined.Append(body).Append('\n');
            if (Encoding.UTF8.GetByteCount(combined.ToString()) > Importer.MaxBytes) throw new SafeError("IMPORT_TOO_LARGE");
        }
        return new(combined.ToString(),first.Date,"");
    }

    public static CompatibleImport Preview(string body,bool experimental)
    {
        if(Encoding.UTF8.GetByteCount(body)>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
        body=body.Trim().TrimStart('\uFEFF');if(body.Length==0||body.StartsWith('<'))throw new SafeError("INVALID_IMPORT");
        if(!body.Contains("://")&&!body.StartsWith('{')&&!body.StartsWith('['))body=Importer.Decode(body);
        if(body.StartsWith('{')||body.StartsWith('['))return Importer.ReadCompatible(body,experimental);
        var lines=body.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(lines.Length>10000)throw new SafeError("IMPORT_TOO_LARGE");
        var result=new CompatibleImport();
        foreach(string line in lines){
            if(line.StartsWith('#')||SubscriptionUsage20.MetadataLine(line,out _,out _))continue;
            CompatibleImport batch;
            try{batch=Importer.ReadCompatible(line,experimental);}
            catch(SafeError error)when(!line.StartsWith("rovunet",StringComparison.OrdinalIgnoreCase)&&!line.StartsWith("slipnet",StringComparison.OrdinalIgnoreCase)&&error.Code!="IMPORT_TOO_LARGE"){
                result.Reject(error.Code);continue;
            }
            result.Merge(batch);
        }
        return result;
    }
}
