using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
namespace Rovunet.Core;

public sealed record TrafficSnapshot(long Uploaded, long Downloaded);
public sealed record TransferRates(double? UploadPerSecond, double? DownloadPerSecond);
public sealed class TrafficSampler
{
    TrafficSnapshot? previous; long tick;
    public TransferRates Sample(TrafficSnapshot value,long now)
    {
        double elapsed=tick==0?0:Stopwatch.GetElapsedTime(tick,now).TotalSeconds;
        var result=previous==null || elapsed<=0 || value.Uploaded<previous.Uploaded || value.Downloaded<previous.Downloaded
            ? new TransferRates(null,null) : new TransferRates((value.Uploaded-previous.Uploaded)/elapsed,(value.Downloaded-previous.Downloaded)/elapsed);
        previous=value;tick=now;return result;
    }
}
// The pinned Xray StatsService proto is the contract. Aggregate local application inbounds
// only: no user identifiers, hostnames, remote IPs, reset, or configuration API.
public static class XrayStats
{
    const string Prefix="inbound>>>local-";
    public static byte[] Request()
    {
        byte[] text=Encoding.UTF8.GetBytes(Prefix);var proto=new byte[text.Length+2];proto[0]=10;proto[1]=(byte)text.Length;text.CopyTo(proto,2);
        var frame=new byte[proto.Length+5];BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1,4),proto.Length);proto.CopyTo(frame,5);return frame;
    }
    static ulong Varint(ReadOnlySpan<byte> bytes,ref int p)
    {
        ulong value=0;for(int i=0;i<10;i++){if(p>=bytes.Length)throw new SafeError("STATS_UNAVAILABLE");byte b=bytes[p++];if(i==9&&b>1)throw new SafeError("STATS_UNAVAILABLE");value|=(ulong)(b&127)<<(7*i);if((b&128)==0)return value;}throw new SafeError("STATS_UNAVAILABLE");
    }
    static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> bytes,ref int p)
    {
        ulong size=Varint(bytes,ref p);if(size>(ulong)(bytes.Length-p))throw new SafeError("STATS_UNAVAILABLE");var result=bytes.Slice(p,(int)size);p+=(int)size;return result;
    }
    static void Skip(ReadOnlySpan<byte> bytes,ref int p,int wire)
    {
        if(wire==0){_=Varint(bytes,ref p);return;}if(wire==2){_=Slice(bytes,ref p);return;}
        int n=wire==1?8:wire==5?4:throw new SafeError("STATS_UNAVAILABLE");if(p+n>bytes.Length)throw new SafeError("STATS_UNAVAILABLE");p+=n;
    }
    public static TrafficSnapshot Decode(ReadOnlySpan<byte> frame)
    {
        if(frame.Length<5||frame.Length>32768||frame[0]!=0||BinaryPrimitives.ReadInt32BigEndian(frame[1..5])!=frame.Length-5)throw new SafeError("STATS_UNAVAILABLE");
        var body=frame[5..];var counters=new Dictionary<string,long>();int p=0,count=0;
        while(p<body.Length)
        {
            int tag=(int)Varint(body,ref p);if(tag!=10){Skip(body,ref p,tag&7);continue;}
            if(++count>32)throw new SafeError("STATS_UNAVAILABLE");var stat=Slice(body,ref p);int q=0;string name="";ulong value=0;
            while(q<stat.Length){int key=(int)Varint(stat,ref q);if(key==10){var text=Slice(stat,ref q);if(text.Length>128)throw new SafeError("STATS_UNAVAILABLE");name=Encoding.UTF8.GetString(text);}else if(key==16)value=Varint(stat,ref q);else Skip(stat,ref q,key&7);}
            if(value>long.MaxValue)throw new SafeError("STATS_UNAVAILABLE");
            if(name is "inbound>>>local-http>>>traffic>>>uplink" or "inbound>>>local-http>>>traffic>>>downlink" or "inbound>>>local-socks>>>traffic>>>uplink" or "inbound>>>local-socks>>>traffic>>>downlink")
                if(!counters.TryAdd(name,(long)value))throw new SafeError("STATS_UNAVAILABLE");
        }
        if(counters.Count!=4)throw new SafeError("STATS_UNAVAILABLE");
        try{return new(checked(counters["inbound>>>local-http>>>traffic>>>uplink"]+counters["inbound>>>local-socks>>>traffic>>>uplink"]),checked(counters["inbound>>>local-http>>>traffic>>>downlink"]+counters["inbound>>>local-socks>>>traffic>>>downlink"]));}catch(OverflowException){throw new SafeError("STATS_UNAVAILABLE");}
    }
    public static async Task<TrafficSnapshot> Read(HttpClient client,int port,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(2));
        using var request=new HttpRequestMessage(HttpMethod.Post,$"http://127.0.0.1:{port}/xray.app.stats.command.StatsService/QueryStats") {Version=HttpVersion.Version20,VersionPolicy=HttpVersionPolicy.RequestVersionExact,Content=new ByteArrayContent(Request())};
        request.Content.Headers.ContentType=new MediaTypeHeaderValue("application/grpc");request.Headers.TryAddWithoutValidation("TE","trailers");
        using var reply=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);if(!reply.IsSuccessStatusCode||reply.Version.Major!=2)throw new SafeError("STATS_UNAVAILABLE");
        using var stream=await reply.Content.ReadAsStreamAsync(timeout.Token);using var output=new MemoryStream();var buffer=new byte[4096];int size;
        while((size=await stream.ReadAsync(buffer,timeout.Token))>0){if(output.Length+size>32768)throw new SafeError("STATS_UNAVAILABLE");output.Write(buffer,0,size);}
        if(reply.TrailingHeaders.TryGetValues("grpc-status",out var status)&&status.Any(s=>s!="0"))throw new SafeError("STATS_UNAVAILABLE");
        return Decode(output.ToArray());
    }
}
