using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rovunet.Core;

namespace Rovunet.Platform;

public readonly record struct UpdateRoute(bool Connected,int HttpPort)
{
    public void Validate(){if(Connected&&HttpPort is <1 or >65535)throw new SafeError("MAC_UPDATE_ROUTE_NOT_READY");}
}
public sealed record MacUpdateAsset(string Version,string Rid,string FileName,Uri Url,long Size,string Sha256);
public sealed record MacUpdateProgress(string Stage,long Received=0,long Total=0,bool ViaTunnel=false);

// Download-only updater. HTTPS release metadata and artifact digests establish
// download integrity; this is not a Developer ID or automatic-install claim.
public sealed class MacUpdateClient(Func<UpdateRoute> route,Func<UpdateRoute,HttpMessageHandler>? handlers=null)
{
    public const string Publisher="msVPNco/rovunet-app";
    public const long MaximumPackageBytes=1024L*1024*1024;
    const int MetadataLimit=4*1024*1024;
    static readonly Regex VersionPattern=new(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",RegexOptions.CultureInvariant);

    public static int CompareVersions(string a,string b)
    {
        var left=VersionPattern.Match(a);var right=VersionPattern.Match(b);
        if(!left.Success||!right.Success||a.Length>100||b.Length>100)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        for(int i=1;i<=3;i++){
            if(!long.TryParse(left.Groups[i].Value,out long l)||!long.TryParse(right.Groups[i].Value,out long r))throw new SafeError("MAC_UPDATE_METADATA_INVALID");
            if(l!=r)return l.CompareTo(r);
        }
        string lp=left.Groups[4].Value,rp=right.Groups[4].Value;
        if(lp.Length==0||rp.Length==0)return lp.Length==rp.Length?0:lp.Length==0?1:-1;
        var ls=lp.Split('.');var rs=rp.Split('.');
        for(int i=0;i<Math.Min(ls.Length,rs.Length);i++){
            string l=ls[i],r=rs[i];bool ln=l.All(char.IsAsciiDigit),rn=r.All(char.IsAsciiDigit);
            if(ln&&l.Length>1&&l[0]=='0'||rn&&r.Length>1&&r[0]=='0')throw new SafeError("MAC_UPDATE_METADATA_INVALID");
            int cmp=ln&&rn?l.Length!=r.Length?l.Length.CompareTo(r.Length):string.CompareOrdinal(l,r):ln!=rn?ln?-1:1:string.CompareOrdinal(l,r);
            if(cmp!=0)return cmp;
        }
        return ls.Length.CompareTo(rs.Length);
    }
    public static bool AllowedUrl(Uri uri)
    {
        if(uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Fragment.Length!=0)return false;
        if(uri.Host=="api.github.com")return uri.AbsolutePath=="/repos/"+Publisher+"/releases";
        if(uri.Host=="github.com")return uri.AbsolutePath.StartsWith("/"+Publisher+"/releases/download/macos-v",StringComparison.Ordinal);
        return uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com" &&
            Regex.IsMatch(uri.AbsolutePath,@"^/github-production-release-asset(?:-[a-z0-9]+)?/1445854036/",RegexOptions.CultureInvariant);
    }
    static string Text(JsonElement value,string key)=>value.GetProperty(key).GetString()??throw new SafeError("MAC_UPDATE_METADATA_INVALID");
    static void UniqueProperties(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var property in value.EnumerateObject()){if(!names.Add(property.Name))throw new SafeError("MAC_UPDATE_METADATA_INVALID");UniqueProperties(property.Value);}}
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())UniqueProperties(item);
    }
    static void Keys(JsonElement value,params string[] keys)
    {
        if(value.ValueKind!=JsonValueKind.Object||value.EnumerateObject().Any(p=>!keys.Contains(p.Name,StringComparer.Ordinal)))throw new SafeError("MAC_UPDATE_METADATA_INVALID");
    }
    static string Digest(string value)
    {
        if(!Regex.IsMatch(value,@"^sha256:[0-9a-fA-F]{64}$",RegexOptions.CultureInvariant))throw new SafeError("MAC_UPDATE_HASH_REQUIRED");
        return value[7..].ToLowerInvariant();
    }
    static MacUpdateAsset ReadAsset(JsonElement release,string name,string version,string rid)
    {
        var matches=release.GetProperty("assets").EnumerateArray().Where(x=>Text(x,"name")==name&&Text(x,"state")=="uploaded").ToArray();
        if(matches.Length!=1)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        var asset=matches[0];long size=asset.GetProperty("size").GetInt64();
        if(size<1||size>MaximumPackageBytes)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        var url=new Uri(Text(asset,"browser_download_url"));
        var expected=new Uri($"https://github.com/{Publisher}/releases/download/macos-v{version}/{name}");
        if(url!=expected||!AllowedUrl(url))throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        return new(version,rid,name,url,size,Digest(Text(asset,"digest")));
    }
    static void Verify(ReadOnlySpan<byte> bytes,MacUpdateAsset asset)
    {
        if(bytes.Length!=asset.Size||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes),Convert.FromHexString(asset.Sha256)))throw new SafeError("MAC_UPDATE_HASH_MISMATCH");
    }
    public async Task<MacUpdateAsset?> Check(string current,string rid,Version operatingSystem,bool includePrereleases,IProgress<MacUpdateProgress>? progress=null,CancellationToken cancellation=default)
    {
        try{
            _=CompareVersions(current,current);
            if(rid is not("osx-arm64" or "osx-x64"))throw new SafeError("MAC_UPDATE_ARCH_UNSUPPORTED");
            var releases=new List<JsonElement>();
            for(int page=1;page<=5;page++){
                byte[] bytes=await Fetch(new($"https://api.github.com/repos/{Publisher}/releases?per_page=100&page={page}"),MetadataLimit,progress,cancellation);
                using var document=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=24});
                UniqueProperties(document.RootElement);
                var rows=document.RootElement.EnumerateArray().ToArray();
                releases.AddRange(rows.Select(x=>x.Clone()));if(rows.Length<100)break;
                if(page==5)throw new SafeError("MAC_UPDATE_LIST_LIMIT");
            }
            var candidates=new List<(string Version,JsonElement Release)>();
            foreach(var release in releases){
                string tag=Text(release,"tag_name");
                if(!tag.StartsWith("macos-v",StringComparison.Ordinal)||release.GetProperty("draft").GetBoolean()||!includePrereleases&&release.GetProperty("prerelease").GetBoolean())continue;
                string version=tag[7..];
                if(!VersionPattern.IsMatch(version)||!includePrereleases&&version.Contains('-'))continue;
                if(CompareVersions(version,current)>0)candidates.Add((version,release));
            }
            candidates.Sort((a,b)=>CompareVersions(b.Version,a.Version));
            bool newerUnsupported=false;
            foreach(var candidate in candidates){
                var manifestAsset=ReadAsset(candidate.Release,"UPDATE-MANIFEST.json",candidate.Version,rid);
                if(manifestAsset.Size>MetadataLimit)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                byte[] manifest=await Fetch(manifestAsset.Url,MetadataLimit,progress,cancellation);Verify(manifest,manifestAsset);
                using var doc=JsonDocument.Parse(manifest,new JsonDocumentOptions{MaxDepth=24});var root=doc.RootElement;
                UniqueProperties(root);Keys(root,"schemaVersion","publisher","version","artifacts");
                if(root.GetProperty("schemaVersion").GetInt32()!=1||Text(root,"publisher")!=Publisher||Text(root,"version")!=candidate.Version)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                var artifacts=root.GetProperty("artifacts").EnumerateArray().Where(x=>Text(x,"rid")==rid).ToArray();
                if(artifacts.Length==0){newerUnsupported=true;continue;}
                if(artifacts.Length!=1)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                var artifact=artifacts[0];var minimum=Version.Parse(Text(artifact,"minimumMacOS"));
                Keys(artifact,"rid","minimumMacOS","fileName","size","sha256");
                if(minimum<new Version(15,0))throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                if(operatingSystem<minimum){newerUnsupported=true;continue;}
                string name=$"Rovunet-macOS-{candidate.Version}-{(rid=="osx-arm64"?"AppleSilicon-arm64":"Intel-x64")}.pkg";
                if(Text(artifact,"fileName")!=name)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                var asset=ReadAsset(candidate.Release,name,candidate.Version,rid);
                if(asset.Size!=artifact.GetProperty("size").GetInt64()||asset.Sha256!=Text(artifact,"sha256").ToLowerInvariant())throw new SafeError("MAC_UPDATE_METADATA_INVALID");
                return asset;
            }
            if(newerUnsupported)throw new SafeError("MAC_UPDATE_OS_UNSUPPORTED");
            return null;
        }catch(SafeError){throw;}catch(OperationCanceledException){throw;}catch(HttpRequestException){throw new SafeError("MAC_UPDATE_NETWORK_FAILED");}catch{throw new SafeError("MAC_UPDATE_METADATA_INVALID");}
    }
    public async Task Download(MacUpdateAsset asset,string destination,IProgress<MacUpdateProgress>? progress=null,CancellationToken cancellation=default)
    {
        if(!AllowedUrl(asset.Url)||Path.GetFileName(asset.FileName)!=asset.FileName||asset.Size is <1 or >MaximumPackageBytes||!Regex.IsMatch(asset.Sha256,@"^[a-f0-9]{64}$"))throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        string full=Path.GetFullPath(destination);string temporary=full+"."+Guid.NewGuid().ToString("N")+".partial";
        try{
            await Routed(async(client,snapshot,ct)=>{
                var options=new FileStreamOptions{Mode=FileMode.Create,Access=FileAccess.Write,Share=FileShare.None,Options=FileOptions.Asynchronous};
                if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
                await using var output=new FileStream(temporary,options);
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using var response=await Open(client,asset.Url,ct);
                if(response.Content.Headers.ContentLength is {} length&&length!=asset.Size)throw new SafeError("MAC_UPDATE_HASH_MISMATCH");
                await using var stream=await response.Content.ReadAsStreamAsync(ct);byte[] buffer=new byte[64*1024];long total=0;int count;
                while((count=await Read(stream,buffer,ct))>0){
                    total+=count;if(total>asset.Size)throw new SafeError("MAC_UPDATE_HASH_MISMATCH");
                    hash.AppendData(buffer,0,count);await output.WriteAsync(buffer.AsMemory(0,count),ct);
                    progress?.Report(new("downloading",total,asset.Size,snapshot.Connected));
                }
                if(total!=asset.Size||!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),Convert.FromHexString(asset.Sha256)))throw new SafeError("MAC_UPDATE_HASH_MISMATCH");
                await output.FlushAsync(ct);return true;
            },TimeSpan.FromMinutes(30),progress,cancellation);
            cancellation.ThrowIfCancellationRequested();File.Move(temporary,full,true);
            progress?.Report(new("ready",asset.Size,asset.Size,route().Connected));
        }catch(SafeError){throw;}catch(OperationCanceledException){throw;}catch(HttpRequestException){throw new SafeError("MAC_UPDATE_NETWORK_FAILED");}catch(IOException){throw new SafeError("MAC_UPDATE_DOWNLOAD_FAILED");}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    async Task<byte[]> Fetch(Uri url,int limit,IProgress<MacUpdateProgress>? progress,CancellationToken cancellation)=>await Routed(async(client,snapshot,ct)=>{
        progress?.Report(new("checking",ViaTunnel:snapshot.Connected));
        using var response=await Open(client,url,ct);
        if(response.Content.Headers.ContentLength>limit)throw new SafeError("MAC_UPDATE_METADATA_INVALID");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();byte[] buffer=new byte[8192];int count;
        while((count=await Read(stream,buffer,ct))>0){if(output.Length+count>limit)throw new SafeError("MAC_UPDATE_METADATA_INVALID");output.Write(buffer,0,count);}
        return output.ToArray();
    },TimeSpan.FromSeconds(60),progress,cancellation);
    static async Task<int> Read(Stream stream,byte[] bytes,CancellationToken cancellation){using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(20));return await stream.ReadAsync(bytes,deadline.Token);}
    async Task<T> Routed<T>(Func<HttpClient,UpdateRoute,CancellationToken,Task<T>> action,TimeSpan timeout,IProgress<MacUpdateProgress>? progress,CancellationToken cancellation)
    {
        for(int attempt=0;attempt<8;attempt++){
            cancellation.ThrowIfCancellationRequested();var snapshot=route();snapshot.Validate();
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(timeout);
            using var monitorStop=new CancellationTokenSource();
            async Task Monitor(){try{while(true){await Task.Delay(100,monitorStop.Token);if(route()!=snapshot){deadline.Cancel();return;}}}catch(OperationCanceledException){}}
            var monitor=Monitor();
            using var handler=handlers?.Invoke(snapshot)??new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=snapshot.Connected,Proxy=snapshot.Connected?new WebProxy($"http://127.0.0.1:{snapshot.HttpPort}"):null,ConnectTimeout=TimeSpan.FromSeconds(15),AutomaticDecompression=DecompressionMethods.None};
            using var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Rovunet-macOS/"+BuildVersion.SemVer);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            try{var result=await action(client,snapshot,deadline.Token);if(route()==snapshot)return result;}
            catch(Exception e) when(!cancellation.IsCancellationRequested&&route()!=snapshot&&e is OperationCanceledException or HttpRequestException or IOException){progress?.Report(new("route-changed",ViaTunnel:route().Connected));}
            catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new SafeError("MAC_UPDATE_TIMEOUT");}
            finally{monitorStop.Cancel();await monitor;}
        }
        throw new SafeError("MAC_UPDATE_ROUTE_CHANGED");
    }
    static async Task<HttpResponseMessage> Open(HttpClient client,Uri start,CancellationToken cancellation)
    {
        var uri=start;
        for(int redirects=0;redirects<=5;redirects++){
            if(!AllowedUrl(uri))throw new SafeError("MAC_UPDATE_URL_REJECTED");
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);request.Headers.CacheControl=new(){NoCache=true};
            var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308){
                var location=response.Headers.Location;response.Dispose();
                if(location==null||redirects==5)throw new SafeError("MAC_UPDATE_URL_REJECTED");uri=new Uri(uri,location);continue;
            }
            if(!response.IsSuccessStatusCode){response.Dispose();throw new SafeError("MAC_UPDATE_NETWORK_FAILED");}
            return response;
        }
        throw new SafeError("MAC_UPDATE_URL_REJECTED");
    }
}
