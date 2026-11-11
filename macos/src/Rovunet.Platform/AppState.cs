using Rovunet.Core;
using System.Text.Json;

namespace Rovunet.Platform;

public sealed class AppState
{
    public List<Profile> Profiles {get;set;}=[];
    public List<Subscription> Subscriptions {get;set;}=[];
    public Dictionary<string,string> OverrideSources {get;set;}=[];
    public Dictionary<string,string> OverrideNames {get;set;}=[];
    public Dictionary<string,string> SshKeys {get;set;}=[];
    public Dictionary<string,string> SlipCertificates {get;set;}=[];
    public Dictionary<string,bool> Expanded {get;set;}=[];
    public Dictionary<string,Measurement> Measurements {get;set;}=[];
    public Dictionary<string,PathSelectionMode> SelectionModes {get;set;}=[];
    public Dictionary<string,string> PreferredPaths {get;set;}=[];
    public Dictionary<string,DateTimeOffset> LastConnected {get;set;}=[];
    public HashSet<string> Pinned {get;set;}=[];
    public Dictionary<string,GroupUndo> GroupUndo {get;set;}=[];
    public string SelectedId {get;set;}="";
    public string Language {get;set;}="fa";
    public string Theme {get;set;}="system";
    public string ProbeUrl {get;set;}="https://example.com/";
    public bool CloseToTray {get;set;}=true;
    public bool LiveLatency {get;set;}
    public bool ProbeFallbacks {get;set;}
    public int LocalHttpPort {get;set;}
    public int LocalSocksPort {get;set;}
    public bool SystemProxy {get;set;}=true;
    public bool SystemTun {get;set;}
    public bool GlobalResolverEnabled {get;set;}
    public string GlobalResolverList {get;set;}="";
    public bool DnsPoolEnabled {get;set;}
    public string DnsPoolList {get;set;}="";
    public bool DnsPoolVerify {get;set;}
    public int SleepTimerMinutes {get;set;}
    public double WindowWidth {get;set;}
    public double WindowHeight {get;set;}
    public int LayoutVersion {get;set;}
    public ScanCheckpoint? Scan {get;set;} // Latest plan (also reads dev.1–4 data).
    public List<ScanCheckpoint> EarlierScans {get;set;}=[];
    public ScanCheckpoint? FindScan(string id,string fingerprint)=>new[]{Scan}.Concat(EarlierScans)
        .FirstOrDefault(s=>s!=null&&s.ProfileId==id&&s.ProfileFingerprint==fingerprint);
    public void SaveScan(ScanCheckpoint? next){
        if(next==null)return;
        if(Scan!=null&&(Scan.ProfileId!=next.ProfileId||Scan.ProfileFingerprint!=next.ProfileFingerprint)){
            EarlierScans.RemoveAll(p=>p.ProfileId==Scan.ProfileId||p.ProfileId==next.ProfileId);
            if(Scan.ProfileId!=next.ProfileId)EarlierScans.Insert(0,Scan);
            int bytes=0;int keep=0;
            foreach(var old in EarlierScans.Take(3)){bytes+=JsonSerializer.SerializeToUtf8Bytes(old).Length;if(bytes>2*1024*1024)break;keep++;}
            if(keep<EarlierScans.Count)EarlierScans.RemoveRange(keep,EarlierScans.Count-keep);
        }
        Scan=next;
    }
    public string ResolverBank {get;set;}="resolvers.txt";
    public int ScanLimit {get;set;}=256;
    public List<DiagnosticEvent> RecentDiagnostics {get;set;}=[];
    public AppState Copy()=>JsonSerializer.Deserialize<AppState>(JsonSerializer.Serialize(this))!;
    public override string ToString()=>"AppState(redacted)";
}

public sealed record Measurement(bool Verified,long ElapsedMs,DateTimeOffset SampledAt,string Stage,int Passes=0,int Attempts=0);
public sealed record GroupUndo(string Id,string Name,string NewGroup);

public static class SubscriptionMerge
{
    // Work on a copy; the caller saves encrypted state before installing this result.
    public static AppState Apply(AppState previous,Uri uri,IReadOnlyList<Profile> imported,string requestedName,SubscriptionUsage20? usage,string safeProviderName="")
    {
        if(imported.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");
        var next=previous.Copy();string source=Importer.Fingerprint(uri.AbsoluteUri);
        var subscription=next.Subscriptions.FirstOrDefault(s=>s.Id==source);
        string name=requestedName.Trim().Length>0?requestedName.Trim():subscription?.Name.Length>0?subscription.Name:safeProviderName.Length>0?safeProviderName:uri.IdnHost;
        if(name.Length>120||name.Any(char.IsControl))throw new SafeError("CORE_NAME_INVALID");
        var old=next.Profiles.Where(p=>p.SourceId==source).GroupBy(p=>p.ItemId.Length>0?p.ItemId:Importer.Fingerprint(p.Raw)).ToDictionary(g=>g.Key,g=>g.First());
        var fresh=new List<Profile>();
        foreach(var item in imported){
            var p=JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(item))!;
            p.SourceId=source;p.CollectionId="sub:"+source;p.CollectionName=name;
            if(p.ItemId.Length==0)p.ItemId=Importer.Fingerprint(p.Raw);
            if(old.TryGetValue(p.ItemId,out var prior)){
                p.Id=prior.Id;
                if(next.OverrideSources.TryGetValue(prior.Id,out var baseline)){
                    string incoming=p.Raw, incomingName=p.Name;
                    p=MacProfileEditing.MergeSubscription(baseline,prior,p,next.OverrideNames.GetValueOrDefault(prior.Id));
                    next.OverrideSources[prior.Id]=incoming;next.OverrideNames[prior.Id]=incomingName;
                }
            }
            fresh.Add(p);
        }
        next.Profiles.RemoveAll(p=>p.SourceId==source);next.Profiles.AddRange(fresh);
        next.Subscriptions.RemoveAll(s=>s.Id==source);next.Subscriptions.Add(new(){Id=source,Url=uri.AbsoluteUri,Name=name,Usage=usage??subscription?.Usage,StatusBody=subscription?.StatusBody??""});
        var ids=next.Profiles.Select(p=>p.Id).ToHashSet();
        foreach(string id in next.OverrideSources.Keys.Where(k=>!ids.Contains(k)).ToArray()){next.OverrideSources.Remove(id);next.OverrideNames.Remove(id);}
        if(next.SelectedId.Length>0&&!ids.Contains(next.SelectedId))next.SelectedId="";
        return next;
    }
}

public sealed class ImportFailure(string code,string option) : Exception(code)
{
    public string Code {get;}=code;
    public string Option {get;}=option;
}

public static class MacImport
{
    public static List<Profile> Read(string body)
    {
        // Strict shared import: an unsupported item cannot disappear silently.
        string decoded=SubscriptionUsage20.ConfigBody(body).Trim();
        if(!decoded.Contains("://")&&!decoded.StartsWith('{')&&!decoded.StartsWith('[')){try{decoded=Importer.Decode(decoded);}catch{}}
        if(!decoded.Contains('\n')||decoded.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)||ProfileEditing.IsNative(decoded)){
            var extension=MacProtocols.Extended(decoded);if(extension!=null)return [MacProtocols.Single(decoded)];
        }
        foreach(string line in decoded.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){
            if(line.StartsWith('#'))continue;
            if(line.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase)){
                try{using var json=JsonDocument.Parse(Importer.Decode(line[8..].Split('#')[0]));foreach(string option in new[]{"cs","fm"})if(json.RootElement.TryGetProperty(option,out _))throw new ImportFailure("MAC_OPTION_NOT_READY",option);}catch(ImportFailure){throw;}catch{}
            }
            if(decoded.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)&&line.Contains('=')){
                string option=line.Split('=')[0].Trim();
                if(new[]{"jc","jmin","jmax","s1","s2","s3","s4","h1","h2","h3","h4","i1","i2","i3","i4","i5"}.Contains(option.ToLowerInvariant()))throw new ImportFailure("MAC_OPTION_NOT_READY","AmneziaWG."+option);
            }
            if(Uri.TryCreate(line,UriKind.Absolute,out var uri)){
                if(uri.Scheme=="mieru")throw new ImportFailure("MAC_OPTION_NOT_READY","mieru");
                if(uri.Scheme=="ss"){
                    string cipher=uri.UserInfo.Split(':')[0];try{cipher=Importer.Decode(cipher).Split(':')[0];}catch{}if(cipher.StartsWith("2022-",StringComparison.Ordinal))throw new ImportFailure("MAC_OPTION_NOT_READY","method="+cipher);
                }
                var keys=uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(p=>Uri.UnescapeDataString(p.Split('=')[0])).ToArray();
                string[] pending=uri.Scheme is "hy2" or "hysteria2"?["mport","hop-interval"]:uri.Scheme is "vmess" or "trojan"?["cs","fm"]:[];
                foreach(string option in pending)if(keys.Contains(option))throw new ImportFailure("MAC_OPTION_NOT_READY",option);
                if(uri.Scheme=="ss"&&line.Contains("2022-",StringComparison.Ordinal))throw new ImportFailure("MAC_OPTION_NOT_READY","Shadowsocks 2022");
            }
        }
        var links=decoded.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Where(line=>!line.StartsWith('#')).ToArray();
        if(links.Length>1&&links.All(line=>Uri.TryCreate(line,UriKind.Absolute,out _)))return links.SelectMany(Read).ToList();
        try{return Importer.Read(string.Join('\n',decoded.Split('\n').Where(l=>!l.TrimStart().StartsWith('#'))));}
        catch(SafeError e){
            string options=string.Join(", ",decoded.Split('\n').Where(l=>Uri.TryCreate(l,UriKind.Absolute,out _)).Select(l=>new Uri(l)).SelectMany(u=>u.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)).Select(p=>Uri.UnescapeDataString(p.Split('=')[0])).Where(p=>p.Length<=40&&p.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_')).Distinct().Take(12));
            throw new ImportFailure(e.Code,options);
        }
    }
}
