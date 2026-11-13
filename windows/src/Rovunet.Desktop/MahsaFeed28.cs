using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Desktop;
public static class MahsaFeed28
{
    public static string[] Decode(string body)
    {
        byte[]? encrypted=null;byte[]? clear=null;
        try{
            if(Encoding.UTF8.GetByteCount(body)>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
            encrypted=Convert.FromBase64String(body.Trim().TrimStart('\uFEFF'));
            using var aes=Aes.Create();aes.Key=Encoding.ASCII.GetBytes("ca815ecfdb1f153a");
            clear=aes.DecryptCbc(encrypted,Encoding.ASCII.GetBytes("lvcas56410c97lpb"),PaddingMode.PKCS7);
            if(clear.Length>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");
            // Validate UTF-8 strictly before parsing; never follow advertising metadata.
            _=new UTF8Encoding(false,true).GetCharCount(clear);
            using var json=JsonDocument.Parse(clear,new JsonDocumentOptions{MaxDepth=16});
            if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new SafeError("MAHSA_DECODE_FAILED");
            var rootNames=new HashSet<string>(StringComparer.Ordinal);foreach(var field in json.RootElement.EnumerateObject())if(!rootNames.Add(field.Name))throw new SafeError("MAHSA_DECODE_FAILED");
            var links=new HashSet<string>(StringComparer.Ordinal);int rows=0,bytes=0;
            foreach(string network in new[]{"mci","mtn"}){
                if(!json.RootElement.TryGetProperty(network,out var array)||array.ValueKind!=JsonValueKind.Array)throw new SafeError("MAHSA_DECODE_FAILED");
                foreach(var item in array.EnumerateArray()){
                    if(++rows>10000)throw new SafeError("IMPORT_TOO_LARGE");
                    if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("config",out var config)||config.ValueKind!=JsonValueKind.String||item.EnumerateObject().Count(p=>p.Name=="config")!=1)throw new SafeError("MAHSA_DECODE_FAILED");
                    string link=(config.GetString()??"").Trim();if(link.Length==0||link.IndexOfAny(['\r','\n','\0'])>=0)throw new SafeError("MAHSA_DECODE_FAILED");
                    if(links.Add(link)){bytes+=Encoding.UTF8.GetByteCount(link)+1;if(bytes>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");}
                }
            }
            if(links.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");return links.ToArray();
        }catch(SafeError){throw;}catch{throw new SafeError("MAHSA_DECODE_FAILED");}
        finally{if(encrypted!=null)CryptographicOperations.ZeroMemory(encrypted);if(clear!=null)CryptographicOperations.ZeroMemory(clear);}
    }
}
