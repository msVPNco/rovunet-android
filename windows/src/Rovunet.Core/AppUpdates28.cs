using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rovunet.Core;

// GitHub metadata supplies transport integrity, not an Authenticode publisher.
// This policy never grants permission to execute or install a downloaded file.
public sealed record AppUpdate28(string Version,string Tag,string Name,Uri Release,Uri Download,long Bytes,string Sha256,bool Prerelease)
{
    public override string ToString()=>"WindowsUpdate("+Version+", x64, download only)";
}
public static class AppUpdates28
{
    public const string Releases="https://api.github.com/repos/msVPNco/rovunet-app/releases?per_page=100";
    public const long MaxInstallerBytes=768L*1024*1024;
    public const int MaxMetadataBytes=2*1024*1024;
    static readonly Regex VersionPattern=new(@"^(\d{1,5})\.(\d{1,5})\.(\d{1,5})(?:-(preview|alpha|beta|rc)(?:\.?([0-9]{1,5}))?)?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    public static (int Major,int Minor,int Patch,int Stage,int Number) ParseVersion(string value)
    {
        var match=VersionPattern.Match(value.Split('+')[0]);if(!match.Success)throw new SafeError("APP_UPDATE_METADATA_INVALID");
        int stage=match.Groups[4].Value.ToLowerInvariant() switch{"preview"=>0,"alpha"=>1,"beta"=>2,"rc"=>3,_=>4};
        return(int.Parse(match.Groups[1].Value),int.Parse(match.Groups[2].Value),int.Parse(match.Groups[3].Value),stage,match.Groups[5].Success?int.Parse(match.Groups[5].Value):0);
    }
    public static int Compare(string a,string b)=>ParseVersion(a).CompareTo(ParseVersion(b));
    static bool Https(Uri uri)=>uri.IsAbsoluteUri&&uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&uri.Fragment.Length==0;
    public static bool AllowedMetadata(Uri uri)=>Https(uri)&&uri.Host=="api.github.com"&&uri.AbsoluteUri==Releases;
    public static bool AllowedAsset(Uri uri,string tag,string name)=>Https(uri)&&uri.Host=="github.com"&&uri.Query.Length==0&&uri.AbsolutePath=="/msVPNco/rovunet-app/releases/download/"+tag+"/"+name;
    public static bool AllowedRedirect(Uri uri)=>Https(uri)&&uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com"&&uri.AbsolutePath.Length>1;
    static void Unique(JsonElement node)
    {
        if(node.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var p in node.EnumerateObject()){if(!names.Add(p.Name))throw new SafeError("APP_UPDATE_METADATA_INVALID");Unique(p.Value);}}
        else if(node.ValueKind==JsonValueKind.Array)foreach(var item in node.EnumerateArray())Unique(item);
    }
    public static AppUpdate28? Select(string json,string current,string architecture="x64",bool includeBeta=true)
    {
        if(architecture!="x64")throw new SafeError("APP_UPDATE_ARCH_UNSUPPORTED");_ =ParseVersion(current);
        try{
            if(System.Text.Encoding.UTF8.GetByteCount(json)>MaxMetadataBytes)throw new SafeError("APP_UPDATE_METADATA_INVALID");
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=24});var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Array||root.GetArrayLength()>100)throw new SafeError("APP_UPDATE_METADATA_INVALID");Unique(root);AppUpdate28? best=null;
            foreach(var release in root.EnumerateArray()){
                string tag=release.GetProperty("tag_name").GetString()??"";if(!tag.StartsWith("windows-v",StringComparison.Ordinal)||release.GetProperty("draft").GetBoolean())continue;
                string version=tag[9..];if(!VersionPattern.IsMatch(version))continue;var parsed=ParseVersion(version);
                if(parsed.Stage<2||!includeBeta&&parsed.Stage<4||Compare(version,current)<=0)continue;
                bool prerelease=release.GetProperty("prerelease").GetBoolean();if(parsed.Stage<4&&!prerelease)throw new SafeError("APP_UPDATE_METADATA_INVALID");
                var page=new Uri(release.GetProperty("html_url").GetString()!);if(!Https(page)||page.AbsoluteUri!="https://github.com/msVPNco/rovunet-app/releases/tag/"+tag)throw new SafeError("APP_UPDATE_METADATA_INVALID");
                var assets=release.GetProperty("assets");if(assets.GetArrayLength()>100)throw new SafeError("APP_UPDATE_METADATA_INVALID");AppUpdate28? candidate=null;
                foreach(var asset in assets.EnumerateArray()){
                    string name=asset.GetProperty("name").GetString()??"";if(!name.Equals("Rovunet-Windows-"+version+"-x64-Setup.exe",StringComparison.OrdinalIgnoreCase))continue;
                    string digest=asset.TryGetProperty("digest",out var d)?d.GetString()??"":"";
                    if(!Regex.IsMatch(digest,@"^sha256:[0-9a-fA-F]{64}$",RegexOptions.CultureInvariant))throw new SafeError("APP_UPDATE_METADATA_INVALID");
                    long bytes=asset.GetProperty("size").GetInt64();var download=new Uri(asset.GetProperty("browser_download_url").GetString()!);
                    if(bytes is <1024 or >MaxInstallerBytes||!AllowedAsset(download,tag,name)||candidate!=null)throw new SafeError("APP_UPDATE_METADATA_INVALID");
                    candidate=new(version,tag,name,page,download,bytes,digest[7..].ToLowerInvariant(),prerelease);
                }
                if(candidate!=null&&(best==null||Compare(candidate.Version,best.Version)>0))best=candidate;
            }
            return best;
        }catch(SafeError){throw;}catch{throw new SafeError("APP_UPDATE_METADATA_INVALID");}
    }
}
