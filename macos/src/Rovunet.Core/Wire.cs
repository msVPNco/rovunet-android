// Mac extension of Windows 562a3d71670ceb7de909146bf68116d5cc614496 Wire.cs.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Rovunet.Core;
public sealed class AgentCommand
{
    public string TlsCertificatePin {get;set;}="";
    public string Action {get;set;}="";
    public Profile? Profile {get;set;}
    public bool SystemProxy {get;set;}
    public bool SystemTun {get;set;}
    public byte[] ProxyAuthorization {get;set;}=[];
    public byte[] ProxyStoreKey {get;set;}=[];
    public int LocalHttpPort {get;set;}
    public int LocalSocksPort {get;set;}
    public string HostKey {get;set;}="";
    public string SshTlsCa {get;set;}="";
    public int? SlipInterval {get;set;}
    public string SlipCertificate {get;set;}="";
    public string ProbeUrl {get;set;}="";
    public bool ProbeFallbacks {get;set;}
    public override string ToString()=>"AgentCommand(redacted)";
}
public sealed class AgentReply
{
    public string State {get;set;}="disconnected";
    public string Error {get;set;}="";
    public int HttpPort {get;set;}
    public int SocksPort {get;set;}
    public bool Verified {get;set;}
    public bool TunActive {get;set;}
    public string TunInterface {get;set;}="";
    public bool TunnelEstablished {get;set;}
    public bool SshEstablished {get;set;}
    public string HostKey {get;set;}="";
    public string Fingerprint {get;set;}="";
    public long ElapsedMs {get;set;}
    public long HandshakeMs {get;set;}
    public long HttpsElapsedMs {get;set;}
    public bool HttpsAttempted {get;set;}
    public bool TrafficAvailable {get;set;}
    public long UploadedBytes {get;set;}
    public long DownloadedBytes {get;set;}
    public string TelemetryError {get;set;}="";
    public string ProbeStage {get;set;}="";
    public bool ProbeFallbackUsed {get;set;}
    public int ProbeTargetsTried {get;set;}
}
public static class Wire
{
    public static async Task Write<T>(Stream stream,T value,CancellationToken ct)
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);
        try{
            if(bytes.Length>Importer.MaxBytes)throw new SafeError("MESSAGE_TOO_LARGE");
            byte[] header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);
            await stream.WriteAsync(header,ct);await stream.WriteAsync(bytes,ct);await stream.FlushAsync(ct);
        }finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public static async Task<T> Read<T>(Stream stream,CancellationToken ct)
    {
        byte[] header=new byte[4];await stream.ReadExactlyAsync(header,ct);int count=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(count is <1 or >Importer.MaxBytes)throw new SafeError("MESSAGE_TOO_LARGE");
        byte[] bytes=new byte[count];
        try{await stream.ReadExactlyAsync(bytes,ct);return JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions{MaxDepth=16})??throw new SafeError("INVALID_MESSAGE");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
