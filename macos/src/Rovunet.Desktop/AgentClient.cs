using System.Diagnostics;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed class AgentClient : IDisposable
{
    readonly Process process;
    readonly SemaphoreSlim gate=new(1,1);
    public event Action<AgentReply>? StageChanged;
    public AgentClient()
    {
        string path=Path.Combine(AppContext.BaseDirectory,"Rovunet.Agent"+(OperatingSystem.IsWindows()?".exe":""));
        if(!File.Exists(path))throw new SafeError("MAC_AGENT_MISSING");
        process=Process.Start(new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"--stdio"}})??throw new SafeError("CORE_START_FAILED");
        _=process.StandardError.BaseStream.CopyToAsync(Stream.Null);
    }
    public async Task<AgentReply> Send(AgentCommand command,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct);
        try{
            await Wire.Write(process.StandardInput.BaseStream,command,ct);
            while(true){var reply=await Wire.Read<AgentReply>(process.StandardOutput.BaseStream,ct);if(reply.State=="engine-started"){StageChanged?.Invoke(reply);continue;}return reply;}
        }catch(SafeError){throw;}catch(OperationCanceledException){Dispose();throw;}catch{throw new SafeError("CORE_EXITED");}finally{gate.Release();}
    }
    public void Dispose()
    {
        try{process.StandardInput.Close();if(!process.WaitForExit(3500)){EngineProcess.Terminate(process);process.WaitForExit(1000);}}catch{}
    }
}
