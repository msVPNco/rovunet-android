using System.Text;
using System.Text.Json.Nodes;
using Rovunet.Core;
static class Review21Cases
{
    static void Check(bool value){if(!value)throw new Exception("assertion");}
    static void Reject(Action action,string code){try{action();}catch(SafeError error)when(error.Code==code){return;}throw new Exception("expected rejection");}
    static string Encode(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    static string Wrap(string inner,Action<string[]>? modify=null){var f=Importer.Decode(ManualProfiles.Native("ssh","Fixture","server.example.invalid",22,"fixture","fixture")[10..]).Split('|');f[1]="xray";f[84]=Encode(inner);modify?.Invoke(f);return "rovunet://"+Encode(string.Join('|',f));}
    static string Ss(string method,string key)=>"ss://"+Encode(method+":"+key)+"@gateway.example.invalid:443#SS2022";
    public static IEnumerable<(string,Action)> All()
    {
        foreach(string method in new[]{"2022-blake3-aes-128-gcm","2022-blake3-aes-256-gcm","2022-blake3-chacha20-poly1305"}){
            string cipher=method,key=Convert.ToBase64String(new byte[method.Contains("128")?16:32]);
            yield return("review21-"+cipher,()=>{var p=Importer.Single(Ss(cipher,key+":"+key));var server=Importer.Connection(p).Outbound["settings"]!["servers"]![0]!;Check(server["method"]!.GetValue<string>()==cipher&&server["password"]!.GetValue<string>()==key+":"+key);var form=ExtraProfileEditing.Read(p)!;var edited=ExtraProfileEditing.Apply(p,"Renamed",form.Fields);Check(JsonNode.DeepEquals(Importer.Connection(edited).Outbound["settings"],Importer.Connection(p).Outbound["settings"]));});
        }
        yield return("review21-aes128-oversized-key-rejected",()=>Reject(()=>Importer.Single(Ss("2022-blake3-aes-128-gcm",Convert.ToBase64String(new byte[32]))),"INVALID_IMPORT"));
        yield return("review21-ss2022-invalid-keys-rejected",()=>{foreach(string key in new[]{"not-base64",Convert.ToBase64String(new byte[15]),Convert.ToBase64String(new byte[16])+":"})Reject(()=>Importer.Single(Ss("2022-blake3-aes-128-gcm",key)),"INVALID_IMPORT");});
        const string unsupported="[Interface]\nPrivateKey=fixture\nUnknownAwgOption=4\n[Peer]\nPublicKey=fixture";
        yield return("review21-compatible-native-xray-skips-unsupported-inner-only",()=>{var batch=Importer.ReadCompatible("socks5://127.0.0.1:9#Working\n"+Wrap(unsupported));Check(batch.Total==2&&batch.Profiles.Count==1&&batch.Skipped["UNSUPPORTED_FORMAT"]==1);Reject(()=>Importer.Read(Wrap(unsupported)),"UNSUPPORTED_FORMAT");});
        yield return("review21-compatible-native-xray-protected-stays-atomic",()=>Reject(()=>Importer.ReadCompatible("socks5://127.0.0.1:9\n"+Wrap(unsupported,f=>f[31]="1")),"PROTECTED_PROFILE"));
        yield return("review21-compatible-unknown-native-version-stays-atomic",()=>Reject(()=>Importer.ReadCompatible("socks5://127.0.0.1:9\n"+Wrap(unsupported,f=>f[0]="34")),"UNSUPPORTED_FORMAT"));
        yield return("review21-compatible-unknown-credential-schema-stays-atomic",()=>Reject(()=>Importer.ReadCompatible("socks5://127.0.0.1:9\n"+Wrap(unsupported,f=>f[87]=Encode("{\"unexpected\":\"fixture\"}"))),"UNSUPPORTED_FORMAT"));
        yield return("review21-native-ss2022-wrapper-admitted",()=>Check(Importer.ReadCompatible(Wrap(Ss("2022-blake3-aes-256-gcm",Convert.ToBase64String(new byte[32])))).Profiles.Single().Protocol=="Shadowsocks"));
    }
}

