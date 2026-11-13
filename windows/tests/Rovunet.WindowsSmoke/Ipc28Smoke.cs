using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Rovunet.Core;
using Rovunet.Platform;

static class Ipc28Smoke
{
    public static async Task<Dictionary<string,bool>> Run()
    {
        var checks=new Dictionary<string,bool>();
        async Task Case(string key,string json,bool malformed,int? length=null){
            string pipeName="rovunet."+Convert.ToHexString(RandomNumberGenerator.GetBytes(24));using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var child=Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Rovunet.Agent.exe")){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--probe-pipe",pipeName,"--parent",Environment.ProcessId.ToString()}})!;
            using(var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly)){
                await pipe.ConnectAsync(deadline.Token);PipePeer28.RequireServer(pipe.SafePipeHandle,child.Id);checks["ipc28_expected_child_pid_verified"]=true;
                try{PipePeer28.RequireServer(pipe.SafePipeHandle,Environment.ProcessId);checks["ipc28_wrong_server_pid_rejected"]=false;}catch(SafeError){checks["ipc28_wrong_server_pid_rejected"]=true;}
                byte[] bytes=Encoding.UTF8.GetBytes(json),header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,length??bytes.Length);await pipe.WriteAsync(header,deadline.Token);if(length==null)await pipe.WriteAsync(bytes,deadline.Token);await pipe.FlushAsync(deadline.Token);
                if(!malformed){var reply=await Wire.Read<AgentReply>(pipe,deadline.Token);checks[key]=json.Contains("\"status\"")?reply.State=="disconnected"&&reply.HttpPort==0:reply.State=="error"&&reply.Error=="INVALID_MESSAGE";}
            }
            await child.WaitForExitAsync(deadline.Token);if(malformed)checks[key]=child.ExitCode==20;
        }
        await Case("ipc28_valid_status_on_isolated_agent","{\"Action\":\"status\"}",false);
        await Case("ipc28_unknown_action_not_executed","{\"Action\":\"arbitrary-command\"}",false);
        await Case("ipc28_unknown_command_field_terminates_pipe","{\"Action\":\"status\",\"Executable\":\"not-a-command\"}",true);
        await Case("ipc28_duplicate_action_terminates_pipe","{\"Action\":\"status\",\"Action\":\"connect\"}",true);
        await Case("ipc28_oversized_message_terminates_before_body","",true,Importer.MaxBytes+1);
        if(checks.Values.Any(value=>!value))throw new SafeError("INVALID_MESSAGE");
        return checks;
    }
}
