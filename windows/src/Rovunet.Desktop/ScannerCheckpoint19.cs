using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed class ScanCheckpoint19
    {
        public ScanCheckpoint19(){}
        public int Version{get;set;}=2;
        public int QuerySize{get;set;}
        public string Fingerprint{get;set;}="";
        public DateTimeOffset SavedAt{get;set;}
        public ScanOptions Options{get;set;}=new();
        public string[] Candidates{get;set;}=[];
        public string[] Methods{get;set;}=[];
        public string Family{get;set;}="";
        public string Zone{get;set;}="";
        public string PublicKey{get;set;}="";
        public string ProbeUrl{get;set;}="";
        public bool AutomaticTunnel{get;set;}
        public bool TunnelStarted{get;set;}
        public List<ScanCheckpointRow19> Results{get;set;}=[];
    }
    sealed class ScanCheckpointRow19
    {
        public ScanCheckpointRow19(){}
        public string Address{get;set;}="";
        public ResolverResult[] Proofs{get;set;}=[];
        public bool TunnelFinished{get;set;}
        public bool TunnelVerified{get;set;}
        public bool HttpsVerified{get;set;}
        public string VerifiedTransport{get;set;}="";
        public long? VerifiedElapsedMs{get;set;}
        public string Error{get;set;}="";
    }
    readonly Dictionary<string,byte[]> fixtureScanCheckpoints19=[];
    static byte[] EncodeScannerCheckpoint19(ScanCheckpoint19 value)
    {
        byte[] plain=JsonSerializer.SerializeToUtf8Bytes(value);if(plain.Length>128*1024*1024)throw new SafeError("DNS_SCAN_LIMIT");
        using var memory=new MemoryStream();using(var gzip=new GZipStream(memory,CompressionLevel.Fastest,true))gzip.Write(plain);return memory.ToArray();
    }
    static ScanCheckpoint19 DecodeScannerCheckpoint19(byte[] compressed,string fingerprint)
    {
        using var memory=new MemoryStream(compressed);using var gzip=new GZipStream(memory,CompressionMode.Decompress);using var plain=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=gzip.Read(buffer))>0){if(plain.Length+read>128*1024*1024)throw new SafeError("DNS_SCAN_INVALID");plain.Write(buffer,0,read);}
        var saved=JsonSerializer.Deserialize<ScanCheckpoint19>(plain.ToArray(),new JsonSerializerOptions{MaxDepth=20})??throw new SafeError("DNS_SCAN_INVALID");
        if(saved.QuerySize is <0 or >4096||saved.Version is not(1 or 2)||saved.Version==1&&(saved.Options?.Mode=="e2e"||saved.Family=="soar"&&saved.Options?.Mode=="simple")||saved.Fingerprint!=fingerprint||saved.Options==null||saved.Candidates==null||saved.Methods==null||saved.Results==null||saved.Candidates.Length is <1 or >ResolverScan.MaximumCandidates||saved.Methods.Length is <1 or >2||saved.Methods.Distinct().Count()!=saved.Methods.Length||saved.Methods.Any(m=>m is not("udp" or "tcp" or "tls" or "https"))||Uri.CheckHostName(saved.Zone)!=UriHostNameType.Dns||saved.Results.Count>saved.Candidates.Length)throw new SafeError("DNS_SCAN_INVALID");
        var options=saved.Options;if(options.Workers is <1 or >50||options.Timeout is <200 or >90000||options.E2eTimeout is <1000 or >180000||options.E2eConcurrency is <1 or >10||options.Attempts is <1 or >30||options.Threshold<1||options.Threshold>options.Attempts||options.Mode is not("simple" or "advanced" or "prism" or "e2e"))throw new SafeError("DNS_SCAN_INVALID");if(options.FullVerification)SubscriptionUrls.Validate(saved.ProbeUrl);
        var candidates=saved.Candidates.ToHashSet(StringComparer.Ordinal);if(candidates.Count!=saved.Candidates.Length||saved.Results.Select(r=>r.Address).Distinct().Count()!=saved.Results.Count||saved.Results.Any(r=>!candidates.Contains(r.Address)||r.Proofs.Length is <1 or >2||r.Proofs.Select(p=>p.Transport).Distinct().Count()!=r.Proofs.Length||r.Proofs.Any(p=>!saved.Methods.Contains(p.Transport))||r.Error.Length>100))throw new SafeError("DNS_SCAN_INVALID");
        return saved;
    }
    void SaveScannerCheckpoint19(string scope,ScannerSession19 session,string fingerprint)
    {
        var saved=session.Checkpoint(fingerprint);byte[] bytes=EncodeScannerCheckpoint19(saved);
        if(fixtureMode)fixtureScanCheckpoints19[scope]=bytes;else PrivateStore.Save("scanner-"+Importer.Fingerprint(scope)+".dpapi",bytes);
    }
    ScanCheckpoint19? ReadScannerCheckpoint19(string scope,string fingerprint)
    {
        try{byte[]? bytes=fixtureMode?fixtureScanCheckpoints19.GetValueOrDefault(scope):PrivateStore.Load<byte[]>("scanner-"+Importer.Fingerprint(scope)+".dpapi");return bytes==null?null:DecodeScannerCheckpoint19(bytes,fingerprint);}catch{return null;}
    }
    static void RestoreScannerRows19(ScanCheckpoint19 saved,ObservableCollection<ScanEntry> entries)
    {
        foreach(var row in saved.Results){var result=row.Proofs.OrderByDescending(p=>p.Pass).ThenByDescending(p=>p.Checks?.Length??0).ThenBy(p=>p.DnsElapsedMS>0?p.DnsElapsedMS:p.ElapsedMS).First();var entry=new ScanEntry{Address=row.Address,Result=result,Selectable=result.Pass,DnsCached=true,TunnelCached=true,TunnelFinished=row.TunnelFinished,TunnelVerified=row.TunnelFinished&&row.TunnelVerified,HttpsVerified=row.TunnelFinished&&row.HttpsVerified,VerifiedTransport=row.VerifiedTransport,VerifiedElapsedMs=row.TunnelFinished?row.VerifiedElapsedMs:null,TunnelError=row.Error,Detail=row.TunnelFinished?L.Text(row.HttpsVerified?"HTTPS_VERIFIED":row.TunnelVerified?"SCAN_SERVER_REACHED":row.Error.Length>0?row.Error:"HTTPS_TEST_FAILED"):""};foreach(var proof in row.Proofs){entry.Probes[proof.Transport]=proof;entry.CachedDnsMethods.Add(proof.Transport);}entries.Add(entry);}
    }
}
