using System.Net;
using System.Text.Json;
using Rovunet.Platform;
using Rovunet.Core;
if(args is ["probe",var device,var url]){
  try{using var c=new HttpClient(MacTunHttp.Handler(0,true,device,TimeSpan.FromSeconds(5))){Timeout=TimeSpan.FromSeconds(8)};var b=await c.GetByteArrayAsync(url);Console.WriteLine(JsonSerializer.Serialize(new{bytes=b.Length,sha256=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(b))}));return 0;}catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{error=e is SafeError s?s.Code:e.GetType().Name}));return 2;}
}
try
{
    var input=JsonDocument.Parse(await Console.In.ReadToEndAsync()).RootElement;
    var profile=MacProtocols.Single(input.GetProperty("link").GetString()!);
    bool operational=input.TryGetProperty("operational",out var field)&&field.GetBoolean();
    int helper=0;
    if(operational&&TransportPlan.UsesHelper(profile.Raw)){
        if(Importer.Fields(profile.Raw)[1]!="vless")throw new SafeError("UNSUPPORTED_FORMAT");
        helper=input.GetProperty("helper").GetInt32();
    }
    string config=MacProtocols.Runtime(profile,input.GetProperty("http").GetInt32(),input.GetProperty("socks").GetInt32(),helper,
        tunUser:input.GetProperty("user").GetString()!,tunPassword:input.GetProperty("pass").GetString()!,tlsCertificatePin:input.GetProperty("pin").GetString()!);
    if(operational){
        config=await MacTunBootstrap.Prepare(config,CancellationToken.None);
        Console.Write(JsonSerializer.Serialize(new{xray=config,transport=helper==0?"":TransportPlan.Config(profile,helper,"")}));
        return 0;
    }
    if(input.GetProperty("bootstrap").GetBoolean())
        config=await MacTunBootstrap.Prepare(config,CancellationToken.None,(host,ct)=>host=="outer.test.invalid"
            ?Task.FromResult<IPAddress[]>([IPAddress.Loopback]):throw new InvalidOperationException());
    Console.Write(config);return 0;
}
catch(Exception e)
{
    string reason=e is SafeError safe?safe.Code:e.GetType().Name;
    Console.Error.WriteLine(new string(reason.Where(c=>char.IsAsciiLetterOrDigit(c)||c=='_').Take(64).ToArray()));
    return 3;
}
