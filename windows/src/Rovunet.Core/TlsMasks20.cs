using System.Globalization;
using System.Net.Security;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rovunet.Core;

public static partial class CoreLinks
{
    // Exact pinned Xray schema: cipherSuites + TCP fragment finalmask. No
    // extension is discarded or converted to an approximate freedom fragment.
    static string CipherSuites20(string value)
    {
        if(value.Length>4096)throw new SafeError("UNSUPPORTED_FORMAT");
        foreach(string name in value.Split(':',StringSplitOptions.RemoveEmptyEntries))if(!name.StartsWith("TLS_",StringComparison.Ordinal)||!Enum.TryParse<TlsCipherSuite>(name,out var suite)||!Enum.IsDefined(suite))throw new SafeError("UNSUPPORTED_FORMAT");
        return value;
    }
    static JsonObject? FinalMask20(string value)
    {
        if(value.Length==0)return null;
        try{
            if(value.Length>16384)throw new SafeError("UNSUPPORTED_FORMAT");using var doc=JsonDocument.Parse(value,new JsonDocumentOptions{MaxDepth=7});var root=doc.RootElement;
            void Keys(JsonElement element,params string[] names){if(element.ValueKind!=JsonValueKind.Object||element.EnumerateObject().Any(p=>!names.Contains(p.Name))||element.EnumerateObject().Select(p=>p.Name).Distinct().Count()!=element.EnumerateObject().Count())throw new SafeError("UNSUPPORTED_FORMAT");}
            (int First,int Last) Range(JsonElement element){string text=element.ValueKind==JsonValueKind.String?element.GetString()!:element.GetRawText();var pair=text.Split('-');if(pair.Length is <1 or >2||!int.TryParse(pair[0],NumberStyles.None,CultureInfo.InvariantCulture,out int first)||first<0)throw new SafeError("UNSUPPORTED_FORMAT");int last=first;if(pair.Length==2&&(!int.TryParse(pair[1],NumberStyles.None,CultureInfo.InvariantCulture,out last)||last<first))throw new SafeError("UNSUPPORTED_FORMAT");return(first,last);}
            Keys(root,"tcp");var masks=root.GetProperty("tcp");if(masks.ValueKind!=JsonValueKind.Array||masks.GetArrayLength() is <1 or >8)throw new SafeError("UNSUPPORTED_FORMAT");
            foreach(var mask in masks.EnumerateArray()){
                Keys(mask,"type","settings");if(mask.GetProperty("type").GetString()!="fragment")throw new SafeError("UNSUPPORTED_FORMAT");var settings=mask.GetProperty("settings");Keys(settings,"packets","length","delay","lengths","delays","maxSplit");
                if(settings.TryGetProperty("packets",out var packets)){string p=packets.GetString()!;if(p is not("" or "tlshello")&&Range(packets).First==0)throw new SafeError("UNSUPPORTED_FORMAT");}
                (int First,int Last) Last(string plural,string single){if(settings.TryGetProperty(plural,out var array)){if(array.ValueKind!=JsonValueKind.Array||array.GetArrayLength() is <1 or >16)throw new SafeError("UNSUPPORTED_FORMAT");foreach(var part in array.EnumerateArray())Range(part);return Range(array[array.GetArrayLength()-1]);}return settings.TryGetProperty(single,out var item)?Range(item):(0,0);}
                if(Last("lengths","length").First==0)throw new SafeError("UNSUPPORTED_FORMAT");Last("delays","delay");if(settings.TryGetProperty("maxSplit",out var split))Range(split);
            }
            return JsonNode.Parse(root.GetRawText())!.AsObject();
        }catch{throw new SafeError("UNSUPPORTED_FORMAT");}
    }
}
