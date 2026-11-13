using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace Rovunet.Core;
public sealed class AgentCommand
{
    public string Action { get; set; } = "";
    public Profile? Profile { get; set; }
    public bool SystemProxy { get; set; }
    public bool EnableUdp { get; set; }
    public int LocalHttpPort {get;set;}
    public int LocalSocksPort {get;set;}
    public string HostKey { get; set; } = "";
    public string SshTlsCa { get; set; } = "";
    public int? SlipInterval { get; set; }
    public string SlipCertificate { get; set; } = "";
    public List<ChainIdentity> ChainIdentities {get;set;}=[];
    public string ProbeUrl { get; set; } = "";
    public bool ProbeFallbacks { get; set; }
    public override string ToString() => "AgentCommand(redacted)";
}
public sealed class AgentReply
{
    public string State { get; set; } = "disconnected";
    public string Error { get; set; } = "";
    public int HttpPort { get; set; }
    public int SocksPort { get; set; }
    public int ChainLayerIndex {get;set;}=-1;
    public bool Verified { get; set; }
    public bool TunnelEstablished { get; set; }
    public bool SshEstablished { get; set; }
    public string HostKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public long ElapsedMs { get; set; }
    public bool TrafficAvailable { get; set; }
    public long UploadedBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string TelemetryError { get; set; } = "";
    public string ProbeStage { get; set; } = "";
    public bool ProbeFallbackUsed { get; set; }
    public int ProbeTargetsTried { get; set; }
}
public static class Wire
{
    public static async Task Write<T>(Stream stream, T value, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        try{
            if (bytes.Length > Importer.MaxBytes) throw new SafeError("MESSAGE_TOO_LARGE");
            byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
            await stream.WriteAsync(header, ct); await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct);
        }finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    static void Unique(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var field in element.EnumerateObject()){if(!names.Add(field.Name))throw new SafeError("INVALID_MESSAGE");Unique(field.Value);}}
        else if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())Unique(item);
    }
    public static async Task<T> Read<T>(Stream stream, CancellationToken ct,int maxBytes=Importer.MaxBytes)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, ct); int count = BinaryPrimitives.ReadInt32LittleEndian(header);
        if(maxBytes is <1 or >Importer.MaxBytes||count<1||count>maxBytes)throw new SafeError("MESSAGE_TOO_LARGE");
        var bytes = new byte[count];
        try{
            await stream.ReadExactlyAsync(bytes, ct);
            using var document=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=16});Unique(document.RootElement);
            return document.RootElement.Deserialize<T>(new JsonSerializerOptions{MaxDepth=16,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new SafeError("INVALID_MESSAGE");
        }catch(JsonException){throw new SafeError("INVALID_MESSAGE");}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
