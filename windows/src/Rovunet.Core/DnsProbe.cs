using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
namespace Rovunet.Core;
public sealed record DnsResult(bool Replied,long Milliseconds,string Error);
public static class DnsProbe
{
    public static IPEndPoint Endpoint(string input,int defaultPort=53)
    {
        string value=input.Trim();if(IPEndPoint.TryParse(value,out var ep)&&ep.Port>0)return ep;
        if(IPAddress.TryParse(value,out var ip))return new(ip,defaultPort);
        throw new SafeError("DNS_IP_REQUIRED");
    }
    public static async Task<DnsResult> Run(string candidate,string mode,CancellationToken cancellation,int milliseconds=3000)
    {
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation);stop.CancelAfter(milliseconds);var clock=Stopwatch.StartNew();
        try
        {
            byte[] query=[0,0,1,0,0,1,0,0,0,0,0,0,7,101,120,97,109,112,108,101,3,99,111,109,0,0,1,0,1];RandomNumberGenerator.Fill(query.AsSpan(0,2));byte[] response;
            if(mode=="https")
            {
                var uri=SubscriptionUrls.Validate(candidate);using var handler=new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false};using var client=new HttpClient(handler);
                using var request=new HttpRequestMessage(HttpMethod.Post,uri){Content=new ByteArrayContent(query)};request.Content.Headers.ContentType=new("application/dns-message");request.Headers.Accept.ParseAdd("application/dns-message");
                using var reply=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,stop.Token);if(!reply.IsSuccessStatusCode)throw new SafeError("DNS_FAILED");
                using var stream=await reply.Content.ReadAsStreamAsync(stop.Token);using var bytes=new MemoryStream();var buf=new byte[1024];int n;while((n=await stream.ReadAsync(buf,stop.Token))>0){if(bytes.Length+n>4096)throw new SafeError("DNS_FAILED");bytes.Write(buf,0,n);}response=bytes.ToArray();
            }
            else
            {
                var ep=Endpoint(candidate,mode=="tls"?853:53);
                if(mode=="udp"){using var socket=new UdpClient(ep.AddressFamily);socket.Connect(ep);await socket.SendAsync(query,stop.Token);response=(await socket.ReceiveAsync(stop.Token)).Buffer;}
                else if(mode is "tcp" or "tls")
                {
                    using var socket=new TcpClient(ep.AddressFamily);await socket.ConnectAsync(ep,stop.Token);using var net=socket.GetStream();
                    using var tls=mode=="tls"?new SslStream(net,true):null;Stream stream=net;
                    if(tls!=null){await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost=ep.Address.ToString()},stop.Token);stream=tls;}
                    var frame=new byte[2];BinaryPrimitives.WriteUInt16BigEndian(frame,(ushort)query.Length);await stream.WriteAsync(frame,stop.Token);await stream.WriteAsync(query,stop.Token);await stream.ReadExactlyAsync(frame,stop.Token);
                    int size=BinaryPrimitives.ReadUInt16BigEndian(frame);if(size is <12 or >4096)throw new SafeError("DNS_FAILED");response=new byte[size];await stream.ReadExactlyAsync(response,stop.Token);
                }
                else throw new SafeError("UNSUPPORTED_FORMAT");
            }
            if(response.Length is <12 or >4096||response[0]!=query[0]||response[1]!=query[1]||(response[2]&0x80)==0||(response[3]&15)!=0||(response[2]&2)!=0)throw new SafeError("DNS_FAILED");
            return new(true,clock.ElapsedMilliseconds,"");
        }
        catch(OperationCanceledException){return new(false,clock.ElapsedMilliseconds,cancellation.IsCancellationRequested?"CANCELLED":"TEST_TIMEOUT");}
        catch{return new(false,clock.ElapsedMilliseconds,"DNS_FAILED");}
    }
}
