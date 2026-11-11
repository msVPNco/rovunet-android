using System.Globalization;
using System.Net.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

// Same bounded finalmask schema as Android CoreOptions at e6359aae. No Lua,
// executable packet generators or arbitrary engine configuration is admitted.
public static class MacCoreOptions
{
    static void Only(JsonObject value,params string[] keys){if(value.Any(p=>!keys.Contains(p.Key)))throw new SafeError("UNSUPPORTED_FORMAT");}
    static void Bounded(JsonNode? value,int depth=0)
    {
        if(depth>8||value==null)throw new SafeError("UNSUPPORTED_FORMAT");
        if(value is JsonObject obj){if(obj.Count>32)throw new SafeError("UNSUPPORTED_FORMAT");foreach(var p in obj)Bounded(p.Value,depth+1);}
        else if(value is JsonArray array){if(array.Count>64)throw new SafeError("UNSUPPORTED_FORMAT");foreach(var item in array)Bounded(item,depth+1);}
        else if(value is JsonValue scalar){if(scalar.TryGetValue<string>(out var text)){if(text.Length>4096)throw new SafeError("UNSUPPORTED_FORMAT");}else if(scalar.TryGetValue<bool>(out _)){}else if(!double.TryParse(scalar.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double number)||!double.IsFinite(number)||number<0)throw new SafeError("UNSUPPORTED_FORMAT");}
    }
    public static JsonObject FinalMask(string raw)
    {
        try{
            if(raw.Length>16384)throw new SafeError("UNSUPPORTED_FORMAT");var result=JsonNode.Parse(raw,documentOptions:new JsonDocumentOptions{MaxDepth=8})?.AsObject()??throw new SafeError("UNSUPPORTED_FORMAT");Bounded(result);Only(result,"tcp","udp","quicParams");
            foreach(string network in new[]{"tcp","udp"})if(result.ContainsKey(network)){
                var masks=result[network] as JsonArray??throw new SafeError("UNSUPPORTED_FORMAT");if(masks.Count>4)throw new SafeError("UNSUPPORTED_FORMAT");
                foreach(var node in masks){var mask=node as JsonObject??throw new SafeError("UNSUPPORTED_FORMAT");Only(mask,"type","settings");string type=mask["type"]?.GetValue<string>()??"";if(!(network=="tcp"?new[]{"fragment"}:new[]{"salamander","udphop","mkcp-legacy","noise"}).Contains(type))throw new SafeError("UNSUPPORTED_FORMAT");
                    var settings=mask["settings"] as JsonObject??new();Only(settings,type switch{"fragment"=>["packets","length","delay","lengths","delays","maxSplit"],"salamander"=>["password","packetSize"],"udphop"=>["mode","interval","remoteIPs","remotePorts"],"mkcp-legacy"=>["header","value"],_=>["reset","noise"]});
                    if(settings["noise"] is JsonArray noise)foreach(var item in noise)Only(item as JsonObject??throw new SafeError("UNSUPPORTED_FORMAT"),"rand","randRange","type","packet","delay");
                }
            }
            if(result.ContainsKey("quicParams")){var q=result["quicParams"] as JsonObject??throw new SafeError("UNSUPPORTED_FORMAT");Only(q,"congestion","bbrProfile","brutalUp","brutalDown","brutalDisableLossCompensation","initStreamReceiveWindow","maxStreamReceiveWindow","initConnectionReceiveWindow","maxConnectionReceiveWindow","maxIdleTimeout","keepAlivePeriod","disablePathMTUDiscovery","disableChromeParrot","disableGSO","maxIncomingStreams","disableStatelessReset");if(q["congestion"] is {} congestion&&congestion.ToString() is not("reno" or "bbr" or "brutal" or "force-brutal"))throw new SafeError("UNSUPPORTED_FORMAT");}
            return result;
        }catch{throw new SafeError("UNSUPPORTED_FORMAT");}
    }
    public static string CipherSuites(string value){var names=value.Split(':');if(value.Length>4096||names.Length is <1 or >16||names.Any(n=>!n.StartsWith("TLS_",StringComparison.Ordinal)||!Enum.TryParse<TlsCipherSuite>(n,out var suite)||!Enum.IsDefined(suite)))throw new SafeError("UNSUPPORTED_FORMAT");return value;}
    public static byte[] Key(string text,int length){try{string value=text.Replace('-','+').Replace('_','/');var key=Convert.FromBase64String(value.PadRight((value.Length+3)/4*4,'='));if(key.Length==length)return key;}catch{}throw new SafeError("CORE_KEY_INVALID");}
}
