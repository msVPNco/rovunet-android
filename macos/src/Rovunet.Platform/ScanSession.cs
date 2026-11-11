using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed class ScanResult
{
    public int Index {get;set;}
    public string Transport {get;set;}="";
    public bool Dns {get;set;}
    public bool Handshake {get;set;}
    public bool Pass {get;set;}
    public int Passed {get;set;}
    public int Attempted {get;set;}
    public int Completed {get;set;}
    public int Edns {get;set;}
    public string[]? Checks {get;set;}
    public string[]? CompletedChecks {get;set;}
    public long ElapsedMS {get;set;}
    public long DnsElapsedMS {get;set;}
    public string Error {get;set;}="";
    public bool TunnelFinished {get;set;}
    public bool TunnelEstablished {get;set;}
    public bool SshEstablished {get;set;}
    public bool HttpsVerified {get;set;}
    public long TunnelElapsedMS {get;set;}
    public string TunnelError {get;set;}="";
    public DateTimeOffset? TestedAt {get;set;}
    public bool ServerChecked {get;set;}
    public bool ServerReached {get;set;}
    public long ServerElapsedMS {get;set;}
    public string ServerError {get;set;}="";
    public bool HttpsAttempted {get;set;}
    public long HttpsElapsedMS {get;set;}
    // A failing HTTP destination cannot erase an authenticated server session.
    public bool LatestServerReached=>TunnelFinished?TunnelEstablished:ServerChecked&&ServerReached;
    public bool Selectable=>TunnelFinished?(TunnelEstablished||HttpsVerified):ServerChecked?ServerReached:Pass;
}

public sealed class ScanCheckpoint
{
    public const int SelectionLimit=8;
    public bool FullVerification {get;set;}
    public string Mode {get;set;}="simple";
    public string Source {get;set;}="input";
    public string Bank {get;set;}="resolvers.txt";
    public int SampleLimit {get;set;}=256;
    public int ScoreMinimum {get;set;}=0;
    public int ResponseSize {get;set;}=0;
    public int QuerySize {get;set;}=0;
    public bool Prefilter {get;set;}
    public static string[] Modes(string family,string wire,bool profile)=>!profile?["advanced"]:family=="dnstt"&&wire=="https"?["advanced","prism"]:family=="slipstream"?["simple","advanced","e2e"]:["simple","advanced","prism","e2e"];
    public static bool ValidServerKey(string key,string family){
        if(key.Length==64&&key.All(Uri.IsHexDigit))return true;if(family!="soar")return false;
        try{var b=key.Trim().Replace('-','+').Replace('_','/');return Convert.FromBase64String(b.PadRight((b.Length+3)/4*4,'=')).Length==32;}catch(FormatException){return false;}
    }
    [System.Text.Json.Serialization.JsonIgnore] public bool Direct=>Mode=="simple"&&Family=="soar";
    [System.Text.Json.Serialization.JsonIgnore] public bool ServerFirst=>Mode=="e2e";
    public int ScannedAddresses()=>Results.Where(r=>!ServerFirst||r.ServerChecked).GroupBy(r=>r.Index).Count(g=>g.Select(r=>r.Transport).Distinct().Count()==Transports.Length);
    public int WorkingAddresses()=>Results.Where(r=>Direct?r.TunnelEstablished:FirstPassed(r)).Select(r=>r.Index).Distinct().Count();
    public bool FirstPassed(ScanResult row)=>ServerFirst?row.ServerChecked&&(FullVerification||Family=="soar"?row.HttpsVerified:row.LatestServerReached):row.Pass;
    public bool CanApply(ScanResult row)=>ServerFirst?FirstPassed(row):row.Selectable;
    public bool HasDnsRemaining()=>!Direct&&(ServerFirst?Results.Count(r=>r.ServerChecked):Results.Count)<Candidates.Length*Transports.Length;
    public bool TunnelDone(ScanResult row)=>row.TunnelFinished&&(!FullVerification||row.HttpsAttempted||row.HttpsVerified||!row.TunnelEstablished);
    // In Both mode never substitute a DNS reply on one wire for the other.
    public IEnumerable<ScanResult> EligibleRows(){
        if(ServerFirst)return Results.Where(r=>r.ServerChecked);
        bool Qualified(ScanResult r)=>FirstPassed(r)&&(r.Completed==0||(r.Checks?.Length??0)>=ScoreMinimum);
        if(Transports.Length==1)return Results.Where(Qualified);
        var complete=Results.GroupBy(r=>r.Index).Where(g=>g.Count()==Transports.Length).Select(g=>g.Key).ToHashSet();
        return Results.Where(r=>complete.Contains(r.Index)&&Qualified(r));
    }
    public bool HasTunnelWork(bool repeat)=>Direct?(repeat||Results.Count(TunnelDone)<Candidates.Length*Transports.Length):EligibleRows().Any(r=>(Selected.Count==0||Mode=="simple"||Selected.Contains(r.Index+":"+r.Transport))&&(repeat||!TunnelDone(r)));
    public void NormalizeSelection(){
        // A pre-dev.5 E2E checkpoint has a full test but no separate preflight.
        // Preserve actual evidence; never infer a measured preflight latency.
        if(ServerFirst)foreach(var old in Results.Where(r=>!r.ServerChecked&&r.TunnelFinished)){old.ServerChecked=true;old.ServerReached=old.TunnelEstablished||old.HttpsVerified;}
        foreach(var row in Results.Where(r=>r.HttpsVerified))row.HttpsAttempted=true;
        var rows=Results.Where(r=>CanApply(r)&&Selected.Contains(r.Index+":"+r.Transport)).ToArray();
        if(rows.Length>SelectionLimit||rows.Select(r=>r.Transport).Distinct().Count()>1||rows.Length>1&&rows[0].Transport=="https"){Selected.Clear();return;}
        Selected.IntersectWith(rows.Select(r=>r.Index+":"+r.Transport));
    }
    public string SelectionError(ScanResult row){
        if(!CanApply(row)||!Results.Contains(row))return "MAC_SCAN_SELECT_FIRST";
        var chosen=Results.Where(r=>CanApply(r)&&Selected.Contains(r.Index+":"+r.Transport)).ToArray();
        if(chosen.Contains(row))return "";
        if(chosen.Any(r=>r.Transport!=row.Transport))return "SCAN_SAME_TRANSPORT";
        if(row.Transport=="https"&&chosen.Length>0)return "SCAN_DOH_SINGLE";
        return chosen.Length>=SelectionLimit?"MAC_SCAN_SELECTION_LIMIT":"";
    }
    public string Family {get;set;}="dnstt";
    public string Zone {get;set;}="";
    public string PublicKey {get;set;}="";
    public string ProfileId {get;set;}="";
    public string ProfileFingerprint {get;set;}="";
    public int TunnelWorkers {get;set;}=2;
    public int TunnelTimeoutMS {get;set;}=115000;
    public string ProbeUrl {get;set;}="https://example.com/";
    public string[] Candidates {get;set;}=[];
    public string[] Transports {get;set;}=["tcp"];
    public int Workers {get;set;}=6;
    public int TimeoutMS {get;set;}=3000;
    public int Count {get;set;}=3;
    public int Threshold {get;set;}=2;
    public List<ScanResult> Results {get;set;}=[];
    public HashSet<string> Selected {get;set;}=[];
    public void Validate()
    {
        if(Mode is not("simple" or "advanced" or "prism" or "e2e")||Workers is <1 or >50||TimeoutMS is <200 or >90000||TunnelWorkers is <1 or >4||TunnelTimeoutMS is <5000 or >180000||Count is <1 or >30||Threshold<1||Threshold>Count||Candidates.Length is <1 or >2048||Transports.Length is <1 or >2||Transports.Any(t=>t is not("udp" or "tcp" or "tls" or "https")))throw new SafeError("DNS_SCAN_INVALID");
        if(Transports.Length==2&&!Transports.SequenceEqual(new[]{"udp","tcp"}))throw new SafeError("DNS_SCAN_INVALID");
        if(Uri.CheckHostName(Zone)!=UriHostNameType.Dns||Results.Any(r=>r.Index<0||r.Index>=Candidates.Length||!Transports.Contains(r.Transport)))throw new SafeError("DNS_SCAN_INVALID");
        if(Candidates.Distinct(StringComparer.Ordinal).Count()!=Candidates.Length||Results.Select(r=>(r.Index,r.Transport)).Distinct().Count()!=Results.Count)throw new SafeError("DNS_SCAN_INVALID");
        if(ScoreMinimum is <0 or >6||QuerySize is <0 or >4096||ResponseSize!=0&&ResponseSize is <200 or >4096)throw new SafeError("DNS_SCAN_INVALID");
        if(Mode=="prism"&&!ValidServerKey(PublicKey,Family))throw new SafeError("MAC_PRISM_KEY");
        if(Mode=="prism"&&TimeoutMS/Threshold<200)throw new SafeError("DNS_SCAN_INVALID");
        if(Family=="slipstream"&&(Mode=="prism"||Transports.Any(t=>t!="udp")))throw new SafeError("DNS_SCAN_INVALID");
        foreach(string candidate in Candidates)foreach(string transport in Transports)_=ResolverScan.Endpoint(candidate,transport);
    }
    public override string ToString()=>"ScanCheckpoint(redacted)";
}

public static class ScanSession
{
    static readonly JsonSerializerOptions nativeJson=new(){PropertyNameCaseInsensitive=true,MaxDepth=8};
    public static ScanResult DecodeNativeResult(string line)
    {
        // The pinned Go worker emits Pascal case. Accept lower camel case too
        // when reading an upgraded worker; neither form may lose its index.
        using var doc=JsonDocument.Parse(line,new JsonDocumentOptions{MaxDepth=8});
        if(doc.RootElement.ValueKind!=JsonValueKind.Object||!doc.RootElement.EnumerateObject().Any(p=>p.Name.Equals("Index",StringComparison.OrdinalIgnoreCase))||!doc.RootElement.EnumerateObject().Any(p=>p.Name.Equals("Transport",StringComparison.OrdinalIgnoreCase)))throw new SafeError("INVALID_MESSAGE");
        return JsonSerializer.Deserialize<ScanResult>(line,nativeJson)??throw new SafeError("INVALID_MESSAGE");
    }
    public static async Task Run(ScanCheckpoint state,Func<ValueTask> changed,CancellationToken stop)
    {
        state.Validate();
        // Group by missing transport. A resumed TCP trial must not overwrite
        // completed UDP evidence, selections, or a finished tunnel check.
        var done=state.Results.Select(r=>(r.Index,r.Transport)).ToHashSet();
        foreach(var group in Enumerable.Range(0,state.Candidates.Length).GroupBy(i=>string.Join(',',state.Transports.Where(t=>!done.Contains((i,t))))).Where(g=>g.Key.Length>0)){
            stop.ThrowIfCancellationRequested();
            await RunBatch(state,group.ToArray(),group.Key.Split(','),changed,stop);
        }
    }
    static async Task RunBatch(ScanCheckpoint state,int[] remaining,string[] methods,Func<ValueTask> changed,CancellationToken stop){
        var request=new{state.Mode,state.Family,state.Zone,state.PublicKey,Candidates=remaining.Select(i=>state.Candidates[i]).ToArray(),Transports=methods,state.Workers,state.TimeoutMS,state.Count,state.Threshold,state.ResponseSize,state.QuerySize,state.Prefilter};
        var seen=new HashSet<(int,string)>();
        using var job=new ProcessJob();
        using var process=EngineProcess.Start(new ProcessStartInfo(EnginePaths.Require("rovunet-transports")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--scan"}})??throw new SafeError("TRANSPORT_START_FAILED");
        job.Assign(process);
        try {
            _=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(),stop);await process.StandardInput.FlushAsync(stop);
            using var cancelled=stop.Register(()=>{try{process.StandardInput.Close();}catch{}});
            while(await ReadBoundedLine(process.StandardOutput,stop) is {} line){
                var row=DecodeNativeResult(line);
                if(row.Error=="CANCELLED")continue;
                if(row.Index<0||row.Index>=remaining.Length||!methods.Contains(row.Transport)||!seen.Add((row.Index,row.Transport)))throw new SafeError("INVALID_MESSAGE");
                stop.ThrowIfCancellationRequested();row.Index=remaining[row.Index];row.TestedAt=DateTimeOffset.UtcNow;state.Results.Add(row);await changed();
            }
            await process.WaitForExitAsync(stop);if(process.ExitCode!=0)throw new SafeError("DNS_SCAN_INVALID");
            if(seen.Count!=remaining.Length*methods.Length)throw new SafeError("DNS_SCAN_INCOMPLETE");
        } finally {try{process.StandardInput.Close();}catch{}EngineProcess.Terminate(process);await changed();}
    }
    static async Task<string?> ReadBoundedLine(StreamReader reader,CancellationToken ct)
    {
        var line=new StringBuilder();char[] one=new char[1];
        while(await reader.ReadAsync(one,ct)>0){if(one[0]=='\n')return line.ToString();if(line.Length>=4096)throw new SafeError("INVALID_MESSAGE");line.Append(one[0]);}
        return line.Length==0?null:throw new SafeError("INVALID_MESSAGE");
    }
}
