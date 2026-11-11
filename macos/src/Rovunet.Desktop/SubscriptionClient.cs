using System.Net;
using System.Text;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed class SubscriptionClient
{
    public sealed record Payload(string Body,DateTimeOffset? Date,string UserInfo,string Title=""){public override string ToString()=>"SubscriptionPayload(redacted)";}
    public static async Task<Payload> Fetch(Uri uri,int proxyPort=0,int maxBytes=Importer.MaxBytes,CancellationToken cancellation=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(40));
        using var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=proxyPort>0,Proxy=proxyPort>0?new WebProxy($"http://127.0.0.1:{proxyPort}"):null,ConnectTimeout=TimeSpan.FromSeconds(15),AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate|DecompressionMethods.Brotli};
        using var client=new HttpClient(handler);
        try{
            for(int redirect=0;redirect<=3;redirect++){
                _=SubscriptionUrls.Validate(uri.AbsoluteUri);
                using var request=new HttpRequestMessage(HttpMethod.Get,uri);request.Headers.TryAddWithoutValidation("Cache-Control","no-cache");request.Headers.UserAgent.ParseAdd("Rovunet/0.1.0");
                request.Headers.Accept.ParseAdd("application/json, text/plain");
                using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308){if(redirect==3||response.Headers.Location==null)throw new SafeError("FETCH_REDIRECT_FAILED");uri=new Uri(uri,response.Headers.Location);continue;}
                if(!response.IsSuccessStatusCode)throw new SafeError(response.StatusCode switch{HttpStatusCode.NotFound=>"STATUS_NOT_FOUND",HttpStatusCode.Forbidden=>"FETCH_FORBIDDEN",HttpStatusCode.TooManyRequests=>"FETCH_RATE_LIMITED",_=>"FETCH_HTTP_FAILED"});
                if(response.Content.Headers.ContentType?.MediaType?.Equals("text/html",StringComparison.OrdinalIgnoreCase)==true)throw new SafeError("INVALID_IMPORT");
                if(response.Content.Headers.ContentLength>maxBytes)throw new SafeError("IMPORT_TOO_LARGE");
                await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var output=new MemoryStream();byte[] bytes=new byte[8192];int count;
                while((count=await stream.ReadAsync(bytes,timeout.Token))>0){if(output.Length+count>maxBytes)throw new SafeError("IMPORT_TOO_LARGE");output.Write(bytes,0,count);}
                string info=response.Headers.TryGetValues("Subscription-Userinfo",out var values)?string.Join(';',values):"";
                string title=response.Headers.TryGetValues("Profile-Title",out var titles)?titles.FirstOrDefault()??"":"";
                return new(new UTF8Encoding(false,true).GetString(output.ToArray()),response.Headers.Date,info.Length<=2048?info:"",title.Length<=4096?title:"");
            }
            throw new SafeError("FETCH_REDIRECT_FAILED");
        }catch(SafeError e){SafeDiagnostics.Record("subscription-download",e.Code);throw;}
        catch(OperationCanceledException){string code=cancellation.IsCancellationRequested?"CANCELLED":"FETCH_TIMEOUT";SafeDiagnostics.Record("subscription-download",code);throw new SafeError(code);}
        catch(HttpRequestException error){string code=error.HttpRequestError switch{HttpRequestError.NameResolutionError=>"FETCH_DNS_FAILED",HttpRequestError.SecureConnectionError=>"FETCH_TLS_FAILED",_=>"FETCH_CONNECTION_FAILED"};SafeDiagnostics.Record("subscription-download",code);throw new SafeError(code);}
        catch{SafeDiagnostics.Record("subscription-download","FETCH_FAILED");throw new SafeError("FETCH_FAILED");}
    }
}
