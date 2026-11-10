using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunPublic19Smoke(string report)
    {
        // Read-only public content audit: never persist or report credentials or configuration bodies.
        var checks=new Dictionary<string,bool>();var counts=new List<object>();
        var sample=PublicFeed.Preview("heading\nsocks5://127.0.0.1:9#Public19%20fixture",true);checks["malformed_public_row_has_visible_rejection_count"]=sample.Total==2&&sample.Profiles.Count==1&&sample.Skipped.Values.Sum()==1;
        var invalidCipher=PublicFeed.Preview("vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=xhttp&security=none&encryption=mlkem768x25519plus.native.0rtt.bad\nsocks5://127.0.0.1:9#fixture",true);checks["invalid_public_cipher_rejected_without_losing_other_rows"]=invalidCipher.Profiles.Count==1&&invalidCipher.Skipped.GetValueOrDefault("VLESS_ENCRYPTION_INVALID")==1;
        var invalidPath=PublicFeed.Preview("trojan://fixture@server.example.invalid:443?type=ws&path=invalid\nsocks5://127.0.0.1:9#fixture",true);checks["invalid_path_rejected_without_losing_other_rows"]=invalidPath.Profiles.Count==1&&invalidPath.Skipped.GetValueOrDefault("CORE_PATH_INVALID")==1;
        foreach(string protectedValue in new[]{"rovunet-enc://fixture","rovunet://broken"}){try{PublicFeed.Preview("socks5://127.0.0.1:9#fixture\n"+protectedValue,true);checks[protectedValue.StartsWith("rovunet-enc")?"protected_feed_is_rejected_atomically":"invalid_native_feed_is_rejected_atomically"]=false;}catch(SafeError){checks[protectedValue.StartsWith("rovunet-enc")?"protected_feed_is_rejected_atomically":"invalid_native_feed_is_rejected_atomically"]=true;}}
        foreach(var source in PublicFeed.Presets){
            try{var payload=await PublicFeed.Fetch(source.Url);var batch=PublicFeed.Preview(payload.Body,true);checks[source.Key+"_download_and_compatible_preview"]=batch.Profiles.Count>0;counts.Add(new{source.Key,supported=batch.Profiles.Count,rejected=batch.Skipped,bytes=System.Text.Encoding.UTF8.GetByteCount(payload.Body)});}
            catch(Exception error){checks[source.Key+"_download_and_compatible_preview"]=false;counts.Add(new{source.Key,error=error is SafeError safe?safe.Code:"FETCH_FAILED"});}
        }
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Read-only public feeds, no profile saved or connected",checks,counts,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
