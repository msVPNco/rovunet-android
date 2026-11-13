using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Rovunet.Platform;
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
        new("mahsa", "Mahsa", MahsaUrl, false),
        new("patterniha", "Patterniha", PatternihaUrl, false),
        new("anonymous", "Anonymous", "https://raw.githubusercontent.com/4n0nymou3/multi-proxy-config-fetcher/refs/heads/main/configs/proxy_configs.txt", false),
    ];

    public const string PatternihaUrl="https://raw.githubusercontent.com/patterniha/Free-Configs/refs/heads/main/configs.txt";
    public const string MahsaUrl="https://raw.githubusercontent.com/mahsanet/MahsaFreeConfig/main/app/sub.txt";
    public const string LegacyMahsaUrl="https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa";
    static string SourceKey(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="raw.githubusercontent.com"||!uri.IsDefaultPort||uri.UserInfo.Length>0)return "";
        string path=uri.AbsolutePath.Replace("/refs/heads/main/","/main/",StringComparison.Ordinal);
        return path switch{
            "/mahsanet/MahsaFreeConfig/main/app/sub.txt" or "/hiddify/hiddify-app/main/test.configs/mahsa"=>"mahsa",
            "/4n0nymou3/multi-proxy-config-fetcher/main/configs/proxy_configs.txt"=>"anonymous",
            "/patterniha/Free-Configs/main/configs.txt"=>"patterniha",
            _=>""
        };
    }
    public static bool IsKnown(string url)=>SourceKey(url) is "mahsa" or "anonymous" or "patterniha";
    public static bool IsMahsa(string url)=>SourceKey(url)=="mahsa";
    public static string NameFor(string url)=>SourceKey(url) switch{"mahsa"=>"Mahsa","anonymous"=>"Anonymous","patterniha"=>"Patterniha",_=>""};
    public static string DisplayName(string url)=>SourceKey(url) switch{"mahsa"=>L.Text("MAC_SOURCE_MAHSA"),"anonymous"=>L.Text("MAC_SOURCE_ANONYMOUS"),"patterniha"=>L.Text("MAC_SOURCE_PATTERNIHA"),_=>""};
    public static async Task<SubscriptionClient.Payload> Fetch(string url,CancellationToken cancellation=default,int proxyPort=0,Func<Uri,int,CancellationToken,Task<SubscriptionClient.Payload>>? download=null)
    {
        string key=SourceKey(url);var preset=Presets.SingleOrDefault(p=>p.Key==key)??throw new SafeError("FETCH_FAILED");
        // Map legacy identity before the first request. Keep the caller's stored
        // subscription URI/ID for merging; never contact Hiddify or ads_url.
        var uri=new Uri(preset.Url);
        var payload=download==null?await SubscriptionClient.Fetch(uri,proxyPort,cancellation:cancellation):await download(uri,proxyPort,cancellation);
        cancellation.ThrowIfCancellationRequested();
        return key=="mahsa"?payload with{Body=DecodeMahsa(payload.Body,reviewTlsRejections:true)}:payload;
    }
    public static string DecodeMahsa(string body,bool reviewTlsRejections=false)
    {
        byte[]? clear=null;
        try{
            if(Encoding.UTF8.GetByteCount(body)>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
            using var aes=Aes.Create();aes.Key=Encoding.ASCII.GetBytes("ca815ecfdb1f153a");
            clear=aes.DecryptCbc(Convert.FromBase64String(body),Encoding.ASCII.GetBytes("lvcas56410c97lpb"),PaddingMode.PKCS7);
            string text=new UTF8Encoding(false,true).GetString(clear);
            using var json=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=16});
            if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new SafeError("MAC_MAHSA_DECODE_FAILED");
            var fields=new HashSet<string>(StringComparer.Ordinal);foreach(var property in json.RootElement.EnumerateObject())if(!fields.Add(property.Name))throw new SafeError("MAC_MAHSA_DECODE_FAILED");
            var links=new HashSet<string>(StringComparer.Ordinal);var ordered=new List<string>();int rows=0;
            foreach(string network in new[]{"mci","mtn"}){
                var array=json.RootElement.GetProperty(network);if(array.ValueKind!=JsonValueKind.Array)throw new SafeError("MAC_MAHSA_DECODE_FAILED");
                foreach(var item in array.EnumerateArray()){
                    if(++rows>10000||item.ValueKind!=JsonValueKind.Object)throw new SafeError("IMPORT_TOO_LARGE");
                    if(item.EnumerateObject().Count(p=>p.Name=="config")!=1)throw new SafeError("MAC_MAHSA_DECODE_FAILED");
                    string link=item.GetProperty("config").GetString()?.Trim()??"";
                    if(link.Length==0||link.Contains('\r')||link.Contains('\n'))throw new SafeError("MAC_MAHSA_DECODE_FAILED");
                    if(!links.Add(link))continue;
                    var review=ImportReview.Read(link);
                    // Preserve a rejected TLS item only for the existing explicit
                    // partial-import review. It is never admitted or rewritten to
                    // disable validation. Structural/protected errors stay atomic.
                    bool tlsReview=reviewTlsRejections&&review.Profiles.Count==0&&review.Rejected==1&&review.Issues.All(i=>i.Code=="TLS_UNVERIFIED")
                        &&!link.StartsWith("rovunet",StringComparison.OrdinalIgnoreCase)&&!link.StartsWith("slipnet",StringComparison.OrdinalIgnoreCase);
                    if(!tlsReview&&(review.Rejected>0||review.Profiles.Count!=1||!review.Profiles[0].CanConnect))throw new SafeError(review.Issues.FirstOrDefault()?.Code??"NO_SUPPORTED_IMPORT");
                    ordered.Add(link);
                }
            }
            if(ordered.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");
            string result=string.Join('\n',ordered);if(Encoding.UTF8.GetByteCount(result)>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
            return result;
        }catch(SafeError){throw;}catch{throw new SafeError("MAC_MAHSA_DECODE_FAILED");}
        finally{if(clear!=null)CryptographicOperations.ZeroMemory(clear);}
    }
    public static AppState ApplyMahsa(AppState previous,Uri identity,string decoded,string name,SubscriptionUsage20? usage=null,string provider="")
    {
        if(!IsMahsa(identity.AbsoluteUri))throw new SafeError("INVALID_SUBSCRIPTION_URL");
        var review=ImportReview.Read(decoded);
        if(review.Rejected!=0||review.Profiles.Count==0||review.Profiles.Any(p=>!p.CanConnect))throw new SafeError("NO_SUPPORTED_IMPORT");
        return SubscriptionMerge.Apply(previous,identity,review.Profiles,name,usage,provider);
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
