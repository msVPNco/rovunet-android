using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.NetworkBroker;
using Rovunet.Platform;

namespace Rovunet.NetworkLab;

internal static class ServiceBoundary30
{
    public static async Task<int> Run(string[] args)
    {
        if(args is not ["--test-service-boundary","--accept-isolated-lab"])return 12;
        try{
            string root=TrustedInstallation29.Root();TrustedInstallation29.RequireLab(root);TrustedInstallation29.Manifest(root);
            int expected=NetworkService30.ExpectedServerPid(root);
            async Task<NamedPipeClientStream> Open(CancellationToken stop){var pipe=new NamedPipeClientStream(".",NetworkService30.Pipe,PipeDirection.InOut,PipeOptions.Asynchronous,System.Security.Principal.TokenImpersonationLevel.Impersonation);try{await pipe.ConnectAsync(stop);PipePeer28.RequireServer(pipe.SafePipeHandle,expected);return pipe;}catch{pipe.Dispose();throw;}}
            async Task<TunReply29> Status(){using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var pipe=await Open(stop.Token);await Wire.Write(pipe,new TunRequest29{Version=2,Session=Guid.NewGuid()},stop.Token);return await Wire.Read<TunReply29>(pipe,stop.Token,4096);}
            var before=await Status();var checks=new Dictionary<string,bool>();string id=Guid.NewGuid().ToString();
            async Task Reject(string label,string? body,int declared=0){
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var pipe=await Open(stop.Token);
                byte[] bytes=body==null?[]:Encoding.UTF8.GetBytes(body);byte[] header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,body==null?declared:bytes.Length);
                await pipe.WriteAsync(header,stop.Token);if(bytes.Length>0)await pipe.WriteAsync(bytes,stop.Token);await pipe.FlushAsync(stop.Token);
                try{byte[] one=new byte[1];checks[label]=await pipe.ReadAsync(one,stop.Token)==0;}catch(IOException){checks[label]=true;}catch(OperationCanceledException){checks[label]=false;}
            }
            await Reject("unknown-field-rejected","{\"Version\":2,\"Session\":\""+id+"\",\"Action\":\"status\",\"Command\":\"unrequested\"}");
            await Reject("duplicate-action-rejected","{\"Version\":2,\"Session\":\""+id+"\",\"Action\":\"status\",\"Action\":\"recover\"}");
            await Reject("unsupported-operation-rejected","{\"Version\":2,\"Session\":\""+id+"\",\"Action\":\"run\"}");
            await Reject("legacy-version-cannot-enable-udp","{\"Version\":1,\"Session\":\""+id+"\",\"EnableUdp\":true}");
            await Reject("oversized-frame-rejected",null,4097);
            await Reject("zero-frame-rejected",null,0);
            var after=await Status();checks["service-and-protection-unchanged"]=before.Session==after.Session&&before.State==after.State&&before.Error==after.Error&&before.ProtectionInstalled==after.ProtectionInstalled;
            bool passed=checks.Values.All(v=>v);Console.WriteLine(JsonSerializer.Serialize(new{passed,checks,actualNamedPipe=true,synthetic=true}));return passed?0:1;
        }catch(SafeError e){Console.WriteLine(JsonSerializer.Serialize(new{error=e.Code}));return 1;}catch{Console.WriteLine("{\"error\":\"LAB_BOUNDARY_FAILED\"}");return 1;}
    }
}
