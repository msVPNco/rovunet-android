using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;

namespace Rovunet.Platform;

// Imported insecure requests never grant trust. Store verified profiles; only
// an exact, per-profile encrypted approval can enable a runtime exception.
public sealed record TlsCertificateApproval(string ProfileFingerprint,string CertificateSha256);

public static class MacTlsPolicy
{
    static readonly string[] Flags=["insecure","allowInsecure","allow_insecure"];
    static bool Requested(string value)=>value.Equals("true",StringComparison.OrdinalIgnoreCase)||value=="1";
    public static string ApprovedPin(AppState state,Profile p){try{return state.TlsExceptions.TryGetValue(p.Id,out var approval)&&approval.ProfileFingerprint==Importer.Fingerprint(p.Raw)&&Supports(p.Raw)?NormalizePin(approval.CertificateSha256):"";}catch(SafeError){return "";}}
    public static bool Approved(AppState state,Profile p)=>ApprovedPin(state,p).Length>0;
    public static string NormalizePin(string value){string pin=value.Replace(":","").Trim();if(pin.Length!=64||pin.Any(c=>!char.IsAsciiHexDigit(c)))throw new SafeError("MAC_TLS_PIN_INVALID");return pin.ToLowerInvariant();}
    public static bool Supports(string raw)=>IsTls(raw)&&!raw.StartsWith("anytls://",StringComparison.OrdinalIgnoreCase)&&!raw.StartsWith("tuic://",StringComparison.OrdinalIgnoreCase);
    public static bool IsTls(string raw)
    {
        if(ProfileEditing.IsNative(raw))return false;
        try{
            if(raw.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase)&&!raw.Split('#')[0].Contains('@')){
                using var doc=JsonDocument.Parse(Importer.Decode(raw[8..].Split('#')[0]));return doc.RootElement.TryGetProperty("tls",out var tls)&&tls.ToString().Equals("tls",StringComparison.OrdinalIgnoreCase);
            }
            if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri))return false;
            if(uri.Scheme is "trojan" or "hy2" or "hysteria2" or "anytls" or "tuic")return true;
            return uri.Scheme is "vless" or "vmess"&&MacProtocols.Query(uri).GetValueOrDefault("security","")=="tls";
        }catch{return false;}
    }
    public static (string Text,int Adjusted) Normalize(string text)
    {
        int adjusted=0;
        string Single(string raw)
        {
            if(!IsTls(raw))return raw;
            try{
                if(raw.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase)&&!raw.Split('#')[0].Contains('@')){
                    var node=JsonNode.Parse(Importer.Decode(raw[8..].Split('#')[0]))!.AsObject();bool changed=false;
                    foreach(string flag in Flags)if(node[flag] is {} value&&Requested(value.ToString())){node[flag]="false";changed=true;}
                    if(!changed)return raw;adjusted++;int hash=raw.IndexOf('#');return "vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(node.ToJsonString()))+(hash<0?"":raw[hash..]);
                }
                int query=raw.IndexOf('?');if(query<0)return raw;int fragment=raw.IndexOf('#',query);string suffix=fragment<0?"":raw[fragment..];string body=raw[(query+1)..(fragment<0?raw.Length:fragment)];bool update=false;
                // Validate duplicate keys before rewriting a single option.
                _=MacProtocols.Query(new Uri(raw));
                string[] parts=body.Split('&');for(int i=0;i<parts.Length;i++){var pair=parts[i].Split('=',2);if(pair.Length==2&&Flags.Contains(Uri.UnescapeDataString(pair[0]))&&Requested(Uri.UnescapeDataString(pair[1]))){parts[i]=pair[0]+"=false";update=true;}}
                if(!update)return raw;adjusted++;return raw[..(query+1)]+string.Join('&',parts)+suffix;
            }catch{return raw;}
        }
        if(text.StartsWith('{')||text.StartsWith('[')&&!text.StartsWith("[Interface]",StringComparison.OrdinalIgnoreCase)){
            try{
                var root=JsonNode.Parse(text);void Visit(JsonNode? node){
                    if(node is JsonArray a){foreach(var child in a)Visit(child);return;}if(node is not JsonObject o)return;
                    if(o["link"] is JsonValue link&&link.TryGetValue<string>(out var raw))o["link"]=Single(raw);
                    foreach(string key in new[]{"tls","tlsSettings"})if(o[key] is JsonObject tls){bool changed=false;foreach(string flag in Flags)if(tls[flag] is {} value&&Requested(value.ToString())){tls[flag]=false;changed=true;}if(changed)adjusted++;}
                    foreach(var child in o.ToArray())Visit(child.Value);
                }
                Visit(root);return (adjusted>0?root!.ToJsonString():text,adjusted);
            }catch{return(text,0);}
        }
        string normalized=string.Join('\n',text.Split('\n').Select(Single));return(normalized,adjusted);
    }
    public static string XrayRuntime(string config,string certificatePin)
    {
        if(certificatePin.Length==0)return config;string pin=NormalizePin(certificatePin);var root=JsonNode.Parse(config)!.AsObject();
        foreach(var outbound in root["outbounds"]!.AsArray().OfType<JsonObject>())if(outbound["streamSettings"] is JsonObject stream&&stream["security"]?.ToString()=="tls"&&stream["tlsSettings"] is JsonObject tls)tls["pinnedPeerCertSha256"]=pin;
        return root.ToJsonString();
    }
    public static async Task<string> InspectCertificate(Profile profile,CancellationToken cancellation)
    {
        if(!Supports(profile.Raw))throw new SafeError("MAC_TLS_EXCEPTION_INVALID");
        var parsed=MacProtocols.Extended(profile.Raw)??CoreLinks.Parse(profile.Raw);var settings=parsed.Outbound["settings"]!.AsObject();
        var servers=(settings["vnext"]??settings["servers"])!.AsArray();var server=servers[0]!.AsObject();string host=server["address"]!.ToString();int port=server["port"]!.GetValue<int>();
        var tls=parsed.Outbound["streamSettings"]!["tlsSettings"]!.AsObject();string sni=tls["serverName"]?.ToString()??host;if(sni.Length==0)sni=host;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try{
            using var socket=new TcpClient();await socket.ConnectAsync(host,port,deadline.Token);
            using var stream=new SslStream(socket.GetStream(),false,(_,_,_,_)=>true);
            await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost=sni,ApplicationProtocols=[SslApplicationProtocol.Http2,SslApplicationProtocol.Http11]},deadline.Token);
            if(stream.RemoteCertificate==null)throw new SafeError("MAC_TLS_CERT_FETCH_FAILED");
            return Convert.ToHexString(SHA256.HashData(stream.RemoteCertificate.GetRawCertData())).ToLowerInvariant();
        }catch(OperationCanceledException)when(cancellation.IsCancellationRequested){throw;}
        catch{throw new SafeError("MAC_TLS_CERT_FETCH_FAILED");}
    }

}
