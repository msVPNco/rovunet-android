using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed class AgentClient(bool probeOnly = false) : IAsyncDisposable
{
    Process? agent; NamedPipeClientStream? pipe; readonly SemaphoreSlim commands = new(1, 1);
    readonly SemaphoreSlim writes=new(1,1);
    public event Action? Lost;
    bool stopping;
    internal int? AgentProcessId => agent?.Id;
    public async Task Start(CancellationToken cancellation = default)
    {
        if (agent != null) return;
        string name = "rovunet." + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        agent = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Rovunet.Agent.exe")) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { probeOnly ? "--probe-pipe" : "--pipe", name, "--parent", Environment.ProcessId.ToString() } }) ?? throw new SafeError("AGENT_FAILED");
        agent.EnableRaisingEvents = true;
        agent.Exited += (_, _) => { if (!stopping) Lost?.Invoke(); };
        pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try { await pipe.ConnectAsync(timeout.Token);PipePeer28.RequireServer(pipe.SafePipeHandle,agent.Id); } catch { await DisposeAsync(); throw new SafeError("AGENT_FAILED"); }
    }
    public async Task<AgentReply> Send(AgentCommand cmd,CancellationToken pendingCancellation=default,Action? written=null)
    {
        await commands.WaitAsync(pendingCancellation);
        try
        {
            if (pipe == null || !pipe.IsConnected) throw new SafeError("AGENT_FAILED");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(cmd.Action is "telemetry" or "latency" ? 8 : cmd.Action == "disconnect" ? 12 : cmd.Action=="probe"?110:cmd.Profile != null && TransportPlan.UsesSlip(cmd.Profile.Raw) ? 105 : 80));
            pendingCancellation.ThrowIfCancellationRequested();await Write(cmd,timeout.Token);written?.Invoke();return await Wire.Read<AgentReply>(pipe, timeout.Token);
        }
        catch(OperationCanceledException) when(pendingCancellation.IsCancellationRequested){throw;}
        catch (SafeError) { throw; }
        catch { throw new SafeError("AGENT_FAILED"); }
        finally { commands.Release(); }
    }
    public void Abort() => pipe?.Dispose();
    internal async Task StopForSystemShutdown(CancellationToken stop)
    {
        stopping=true;pipe?.Dispose();
        var owned=agent;
        if(owned!=null)await owned.WaitForExitAsync(stop).ConfigureAwait(false);
    }
    async Task Write(AgentCommand command,CancellationToken token)
    {
        await writes.WaitAsync(token);try{if(pipe==null||!pipe.IsConnected)throw new SafeError("AGENT_FAILED");await Wire.Write(pipe,command,token);}finally{writes.Release();}
    }
    public async Task CancelProbe()
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try{await Write(new(){Action="cancel-probe"},deadline.Token);}catch{throw new SafeError("AGENT_FAILED");}
    }
    public async ValueTask DisposeAsync()
    {
        stopping = true; pipe?.Dispose(); pipe = null;
        if (agent != null)
        {
            bool exited = false;
            try { using var t = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await agent.WaitForExitAsync(t.Token); exited = true; }
            catch { /* Never kill an Agent before its proxy recovery. Report incomplete cleanup. */ }
            finally { agent.Dispose(); agent = null; }
            if (!exited) throw new SafeError("RECOVERY_REQUIRED");
        }
    }
    public static async Task Recover(bool keepCurrent = false)
    {
        using var p = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Rovunet.Agent.exe")) { UseShellExecute = false, CreateNoWindow = true, Arguments = keepCurrent ? "--keep-current-proxy" : "--recover" }) ?? throw new SafeError("AGENT_FAILED");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await p.WaitForExitAsync(timeout.Token); if (p.ExitCode != 0) throw new SafeError(p.ExitCode == 21 ? "PROXY_RECOVERY_CONFLICT" : "RECOVERY_REQUIRED");
    }
}
