using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Rovunet.Core;

static class Maintenance28Cases
{
    static void Check(bool value){if(!value)throw new Exception("assertion");}
    static void Reject(Action action,string code){try{action();}catch(SafeError e)when(e.Code==code){return;}throw new Exception("expected rejection");}
    static AgentCommand Read(string json){byte[] bytes=Encoding.UTF8.GetBytes(json);using var stream=new MemoryStream();var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);stream.Write(header);stream.Write(bytes);stream.Position=0;return Wire.Read<AgentCommand>(stream,default).GetAwaiter().GetResult();}
    public static string Feed(string version="0.2.0-beta",string digest="",string architecture="x64",long size=2048,bool draft=false,bool prerelease=true)
    {
        digest=digest.Length==0?"sha256:"+new string('a',64):digest;string tag="windows-v"+version,name="Rovunet-Windows-"+version+"-"+architecture+"-Setup.exe";
        return JsonSerializer.Serialize(new[]{new{tag_name=tag,html_url="https://github.com/msVPNco/rovunet-app/releases/tag/"+tag,draft,prerelease,assets=new[]{new{name,size,digest,browser_download_url="https://github.com/msVPNco/rovunet-app/releases/download/"+tag+"/"+name}}}});
    }
    public static IEnumerable<(string,Action)> All()
    {
        yield return("network29-valid-bounded-start",()=>Check(TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=["192.0.2.1","2001:db8::1"],ExperimentalAcknowledged=true}).Length==2));
        yield return("network30-explicit-v2-udp-start",()=>Check(TunPlan29.Validate(new(){Version=2,EnableUdp=true,Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=["192.0.2.1"],ExperimentalAcknowledged=true}).Length==1));
        yield return("network30-v1-cannot-silently-enable-udp",()=>Reject(()=>TunPlan29.Validate(new(){EnableUdp=true,Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=["192.0.2.1"],ExperimentalAcknowledged=true}),"BROKER_REQUEST_REJECTED"));
        yield return("network30-unknown-version-rejected",()=>Reject(()=>TunPlan29.Validate(new(){Version=99,Session=Guid.NewGuid()}),"BROKER_REQUEST_REJECTED"));
        yield return("network30-status-cannot-change-udp",()=>Reject(()=>TunPlan29.Validate(new(){Version=2,EnableUdp=true,Session=Guid.NewGuid()}),"BROKER_REQUEST_REJECTED"));
        foreach(string bad in new[]{"0.0.0.0/0","::/0","example.invalid","127.0.0.1","::ffff:192.0.2.1","fe80::1%1","239.1.1.1","0.0.0.0","::"}){
            string value=bad;yield return("network29-peer-reject-"+value,()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=[value],ExperimentalAcknowledged=true}),"BROKER_REQUEST_REJECTED"));
        }
        yield return("network29-explicit-experimental-action-required",()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=["192.0.2.1"]}),"BROKER_REQUEST_REJECTED"));
        yield return("network29-stop-cannot-supply-routing-settings",()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="stop",SocksPort=10808}),"BROKER_REQUEST_REJECTED"));
        yield return("network29-no-shell-or-executable-command",()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="exec"}),"BROKER_REQUEST_REJECTED"));
        yield return("network29-null-peers-rejected",()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Peers=null!}),"BROKER_REQUEST_REJECTED"));
        yield return("network29-duplicate-peer-rejected",()=>Reject(()=>TunPlan29.Validate(new(){Session=Guid.NewGuid(),Action="start",SocksPort=10808,Peers=["192.0.2.1","192.0.2.1"],ExperimentalAcknowledged=true}),"BROKER_REQUEST_REJECTED"));
        yield return("network29-config-has-no-direct-fallback-and-explicit-udp-limit",()=>{using var d=JsonDocument.Parse(TunPlan29.Config(10808));var protocols=d.RootElement.GetProperty("outbounds").EnumerateArray().Select(x=>x.GetProperty("protocol").GetString()).ToArray();Check(!protocols.Contains("freedom")&&protocols.Contains("blackhole")&&protocols.Contains("socks")&&protocols.Contains("dns"));});
        yield return("network29-dynamic-xray-wfp-is-not-kill-switch",()=>Check(!TunPlan29.Config(10808).Contains("autoSystemWfpBlockLeak")&&!TunPlan29.Config(10808).Contains("autoSystemRoutingTable")));
        yield return("network29-small-broker-frame-limit-before-allocation",()=>{using var stream=new MemoryStream();var bytes=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,4097);stream.Write(bytes);stream.Position=0;Reject(()=>Wire.Read<TunRequest29>(stream,default,4096).GetAwaiter().GetResult(),"MESSAGE_TOO_LARGE");});
        using(var stream=typeof(Maintenance28Cases).Assembly.GetManifestResourceStream("Rovunet.ContractFixtures28")!)using(var json=JsonDocument.Parse(stream))foreach(var fixture in json.RootElement.GetProperty("cases").EnumerateArray()){
            string id=fixture.GetProperty("id").GetString()!,raw=fixture.GetProperty("feed").GetRawText(),expect=fixture.GetProperty("expect").GetString()!;
            yield return("maintenance28-contract-"+id,()=>{try{var result=Importer.Read(raw);Check(expect=="accepted"&&result.Count==1&&result[0].CanConnect);}catch(SafeError){Check(expect=="rejected");}});
        }
        yield return("maintenance28-ipc-roundtrip-profile",()=>{using var stream=new MemoryStream();var command=new AgentCommand{Action="connect",Profile=Importer.Single("socks5://127.0.0.1:9#Fixture"),SystemProxy=false};Wire.Write(stream,command,default).GetAwaiter().GetResult();stream.Position=0;var result=Wire.Read<AgentCommand>(stream,default).GetAwaiter().GetResult();Check(result.Action=="connect"&&result.Profile!.Raw==command.Profile.Raw&&!result.SystemProxy);});
        yield return("maintenance28-ipc-duplicate-action-rejected",()=>Reject(()=>Read("{\"Action\":\"status\",\"Action\":\"connect\"}"),"INVALID_MESSAGE"));
        yield return("maintenance28-ipc-unknown-executable-rejected",()=>Reject(()=>Read("{\"Action\":\"status\",\"Executable\":\"cmd.exe\"}"),"INVALID_MESSAGE"));
        yield return("maintenance28-ipc-case-confusion-rejected",()=>Reject(()=>Read("{\"action\":\"status\"}"),"INVALID_MESSAGE"));
        yield return("maintenance28-ipc-unknown-nested-profile-field-rejected",()=>Reject(()=>Read("{\"Action\":\"connect\",\"Profile\":{\"Raw\":\"fixture\",\"Shell\":\"fixture\"}}"),"INVALID_MESSAGE"));
        yield return("maintenance28-ipc-malformed-json-rejected",()=>Reject(()=>Read("{\"Action\":"),"INVALID_MESSAGE"));
        yield return("maintenance28-ipc-oversized-before-allocation",()=>{using var stream=new MemoryStream();var header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,Importer.MaxBytes+1);stream.Write(header);stream.Position=0;Reject(()=>Wire.Read<AgentCommand>(stream,default).GetAwaiter().GetResult(),"MESSAGE_TOO_LARGE");});
        yield return("maintenance28-update-chooses-newer-windows-beta",()=>{var value=AppUpdates28.Select(Feed(),"0.1.2-preview.5")!;Check(value.Version=="0.2.0-beta"&&value.Sha256==new string('a',64)&&value.Prerelease);});
        yield return("maintenance28-update-never-downgrades-local-preview",()=>Check(AppUpdates28.Select(Feed("0.1.1-beta"),"0.1.2-preview.5")==null));
        yield return("maintenance28-update-beta-after-preview-same-version",()=>Check(AppUpdates28.Select(Feed("0.1.2-beta"),"0.1.2-preview.5")!=null));
        yield return("maintenance28-update-semantic-version-order",()=>Check(AppUpdates28.Compare("0.10.0-beta","0.2.9")>0&&AppUpdates28.Compare("0.2.0","0.2.0-rc.9")>0));
        yield return("maintenance28-update-android-latest-ignored",()=>Check(AppUpdates28.Select(Feed().Replace("windows-v","android-v"),"0.1.0")==null));
        yield return("maintenance28-update-draft-ignored",()=>Check(AppUpdates28.Select(Feed(draft:true),"0.1.0")==null));
        yield return("maintenance28-update-preview-not-public-channel",()=>Check(AppUpdates28.Select(Feed("0.3.0-preview.9"),"0.1.0")==null));
        yield return("maintenance28-update-wrong-architecture-ignored",()=>Check(AppUpdates28.Select(Feed(architecture:"arm64"),"0.1.0")==null));
        yield return("maintenance28-update-channel-stable-excludes-beta",()=>Check(AppUpdates28.Select(Feed(),"0.1.0",includeBeta:false)==null));
        yield return("maintenance28-update-missing-digest-rejected",()=>Reject(()=>AppUpdates28.Select(Feed(digest:"sha256:bad"),"0.1.0"),"APP_UPDATE_METADATA_INVALID"));
        yield return("maintenance28-update-size-rejected",()=>Reject(()=>AppUpdates28.Select(Feed(size:AppUpdates28.MaxInstallerBytes+1),"0.1.0"),"APP_UPDATE_METADATA_INVALID"));
        yield return("maintenance28-update-external-url-rejected",()=>Reject(()=>AppUpdates28.Select(Feed().Replace("github.com/msVPNco/rovunet-app","github.com/other/repository"),"0.1.0"),"APP_UPDATE_METADATA_INVALID"));
        yield return("maintenance28-update-duplicate-critical-key-rejected",()=>Reject(()=>AppUpdates28.Select(Feed().Replace("\"draft\":false","\"draft\":false,\"draft\":true"),"0.1.0"),"APP_UPDATE_METADATA_INVALID"));
        yield return("maintenance28-update-redirect-allowlist",()=>Check(AppUpdates28.AllowedRedirect(new("https://release-assets.githubusercontent.com/fixture?token=synthetic"))&&!AppUpdates28.AllowedRedirect(new("http://release-assets.githubusercontent.com/fixture"))&&!AppUpdates28.AllowedRedirect(new("https://release-assets.githubusercontent.com.attacker.invalid/fixture"))&&!AppUpdates28.AllowedRedirect(new("https://user:password@objects.githubusercontent.com/fixture"))));
    }
}
