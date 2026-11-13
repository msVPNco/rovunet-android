using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed class AppUpdateClient28
{
    readonly Func<int,HttpMessageHandler>? handlerFactory;
    internal AppUpdateClient28(Func<int,HttpMessageHandler>? factory=null)=>handlerFactory=factory;
    HttpClient Client(int port)
    {
        if(port is <0 or >65535)throw new SafeError("APP_UPDATE_ROUTE_CHANGED");
        var handler=handlerFactory?.Invoke(port)??new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=port>0,Proxy=port>0?new WebProxy("http://127.0.0.1:"+port):null,ConnectTimeout=TimeSpan.FromSeconds(12)};
        var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};client.DefaultRequestHeaders.UserAgent.ParseAdd("Rovunet-Windows/"+MainWindow.Version.Split('+')[0]);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");return client;
    }
    public async Task<AppUpdate28?> Check(string current,int port,CancellationToken stop)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromSeconds(30));using var client=Client(port);
        try{
            using var response=await client.GetAsync(AppUpdates28.Releases,HttpCompletionOption.ResponseHeadersRead,timeout.Token);if(!response.IsSuccessStatusCode)throw new SafeError("APP_UPDATE_NETWORK_FAILED");
            if(response.Content.Headers.ContentLength>AppUpdates28.MaxMetadataBytes)throw new SafeError("APP_UPDATE_METADATA_INVALID");
            using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var body=new MemoryStream();var buffer=new byte[8192];int count;
            while((count=await stream.ReadAsync(buffer,timeout.Token))>0){if(body.Length+count>AppUpdates28.MaxMetadataBytes)throw new SafeError("APP_UPDATE_METADATA_INVALID");body.Write(buffer,0,count);}
            return AppUpdates28.Select(new UTF8Encoding(false,true).GetString(body.ToArray()),current);
        }catch(SafeError){throw;}catch(OperationCanceledException)when(stop.IsCancellationRequested){throw new SafeError("CANCELLED");}catch{throw new SafeError("APP_UPDATE_NETWORK_FAILED");}
    }
    public async Task Download(AppUpdate28 item,int port,string destination,IProgress<double>? progress,CancellationToken stop,Action<Action>? routeCommit=null)
    {
        if(!Path.IsPathFullyQualified(destination)||!AppUpdates28.AllowedAsset(item.Download,item.Tag,item.Name)||item.Bytes is <1024 or >AppUpdates28.MaxInstallerBytes)throw new SafeError("APP_UPDATE_METADATA_INVALID");
        string temp=Path.Combine(Path.GetDirectoryName(destination)!,".Rovunet-download-"+Guid.NewGuid().ToString("N")+".tmp");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromMinutes(30));using var client=Client(port);
        try{
            Uri uri=item.Download;HttpResponseMessage? response=null;
            try{
                for(int hops=0;hops<4;hops++){
                    response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                    if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308){var location=response.Headers.Location;if(location==null||!location.IsAbsoluteUri||!AppUpdates28.AllowedRedirect(location))throw new SafeError("APP_UPDATE_REDIRECT_REJECTED");uri=location;response.Dispose();response=null;continue;}
                    break;
                }
                if(response==null||!response.IsSuccessStatusCode)throw new SafeError("APP_UPDATE_NETWORK_FAILED");
                if(response.Content.Headers.ContentLength is long declared&&declared!=item.Bytes)throw new SafeError("APP_UPDATE_INTEGRITY_FAILED");
                var sid=WindowsIdentity.GetCurrent().User!;var security=new FileSecurity();security.SetOwner(sid);security.SetAccessRuleProtection(true,false);security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));
                using(var output=new FileInfo(temp).Create(FileMode.CreateNew,FileSystemRights.Write|FileSystemRights.Read|FileSystemRights.Delete,FileShare.None,65536,FileOptions.Asynchronous|FileOptions.WriteThrough,security)){
                    using var input=await response.Content.ReadAsStreamAsync(timeout.Token);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[65536];long total=0;int count;var last=DateTime.UtcNow;
                    while((count=await input.ReadAsync(buffer,timeout.Token))>0){total+=count;if(total>item.Bytes)throw new SafeError("APP_UPDATE_INTEGRITY_FAILED");hash.AppendData(buffer,0,count);await output.WriteAsync(buffer.AsMemory(0,count),timeout.Token);if((DateTime.UtcNow-last).TotalMilliseconds>200){progress?.Report((double)total/item.Bytes);last=DateTime.UtcNow;}}
                    if(total!=item.Bytes||!Convert.ToHexString(hash.GetHashAndReset()).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new SafeError("APP_UPDATE_INTEGRITY_FAILED");
                    await output.FlushAsync(timeout.Token);output.Flush(true);
                }
                void Commit(){stop.ThrowIfCancellationRequested();File.Move(temp,destination,true);}
                if(routeCommit==null)Commit();else routeCommit(Commit);
                progress?.Report(1);
            }finally{response?.Dispose();}
        }catch(SafeError){throw;}catch(OperationCanceledException)when(stop.IsCancellationRequested){throw new SafeError("CANCELLED");}catch{throw new SafeError("APP_UPDATE_NETWORK_FAILED");}
        finally{try{if(File.Exists(temp))File.Delete(temp);}catch{/* Delete only our unique transient; retain a pre-existing destination on any failure. */}}
    }
}
