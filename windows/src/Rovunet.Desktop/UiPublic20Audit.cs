using System.Text.Json;
using System.Text.RegularExpressions;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunPublic20Audit(string report,Dictionary<string,string>? captured=null)
    {
        var sources=new List<object>();
        foreach(var source in PublicFeed.Presets){
            var groups=new Dictionary<string,int>();
            try{
                var payload=captured?.TryGetValue(source.Key,out var content)==true?new SubscriptionClient.Payload(content,null,""):await PublicFeed.Fetch(source.Url);var body=SubscriptionUsage20.ConfigBody(payload.Body);var batch=PublicFeed.Preview(body,true);
                foreach(string line in body.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)){
                    if(line.StartsWith('#'))continue;
                    try{var parsed=Importer.ReadCompatible(line,true);if(parsed.Profiles.Count>0)continue;}catch(SafeError){}
                    string shape=Shape(line);groups[shape]=groups.GetValueOrDefault(shape)+1;
                }
                int engineTested=0,engineAccepted=0;
                if(captured!=null)foreach(var profile in batch.Profiles){
                    try{using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"engines","xray.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-test","-c","stdin:"}})!;var output=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);var error=process.StandardError.BaseStream.CopyToAsync(Stream.Null);await process.StandardInput.WriteAsync(CoreLinks.Runtime(profile,21001,21002));process.StandardInput.Close();await process.WaitForExitAsync();await Task.WhenAll(output,error);engineTested++;if(process.ExitCode==0)engineAccepted++;}catch(SafeError){engineTested++;}
                }
                sources.Add(new{source.Key,supported=batch.Profiles.Count,rejected=batch.Skipped,engineTested,engineAccepted,shapes=groups});
            }catch(SafeError error){sources.Add(new{source.Key,error=error.Code});}
        }
        File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Public rows: field names and protocol enums only",credentialsIncluded=false,sources},new JsonSerializerOptions{WriteIndented=true}));return true;
        static string Shape(string raw)
        {
            var parts=new List<string>();int sep=raw.IndexOf("://",StringComparison.Ordinal);string scheme=sep>0?raw[..sep].ToLowerInvariant():"non-config";parts.Add(scheme is "vless" or "vmess" or "ss" or "hysteria2" or "hy2" or "trojan"?scheme:"non-config");
            string Field(string key)=>Regex.IsMatch(key,"\\A[a-zA-Z][a-zA-Z0-9_-]{0,40}\\z")?key:"other";
            string Enum(string value)=>new[]{"ws","tcp","grpc","httpupgrade","h2","http","xhttp","splithttp","kcp","quic","tls","reality","none","auto","zero","aes-128-gcm","aes-256-gcm","chacha20-poly1305","chacha20-ietf-poly1305","2022-blake3-aes-128-gcm","2022-blake3-aes-256-gcm","0","1","2","true","false","salamander","vision"}.Contains(value.ToLowerInvariant())?value.ToLowerInvariant():"other";
            try{
                if(scheme=="vmess"){
                    using var doc=JsonDocument.Parse(Importer.Decode(raw[(sep+3)..].Split('#')[0]));parts.Add("fields="+string.Join(',',doc.RootElement.EnumerateObject().Select(p=>Field(p.Name)).Order()));foreach(string key in new[]{"v","aid","scy","net","tls","type"})if(doc.RootElement.TryGetProperty(key,out var value))parts.Add(key+"="+Enum(value.ToString()));
                }else{
                    var uri=new Uri(raw);var query=uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>p.Split('=',2)).ToArray();parts.Add("fields="+string.Join(',',query.Select(p=>Field(Uri.UnescapeDataString(p[0]))).Order()));foreach(var pair in query)if(pair.Length==2&&pair[0] is "type" or "security" or "allowInsecure" or "insecure" or "headerType" or "obfs" or "encryption" or "fragment")parts.Add(pair[0]+"="+Enum(Uri.UnescapeDataString(pair[1])));
                    if(scheme=="ss"){string user=Uri.UnescapeDataString(uri.UserInfo);if(!user.Contains(':'))user=Importer.Decode(user);parts.Add("cipher="+Enum(user.Split(':')[0]));}
                }
            }catch{parts.Add("malformed");}
            return string.Join(" | ",parts);
        }
    }
}
