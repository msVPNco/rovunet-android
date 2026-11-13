using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    internal bool SystemSessionEnding30 {get;private set;}
    internal void PrepareSystemShutdown30()
    {
        if(SystemSessionEnding30)return;
        SystemSessionEnding30=true;closing=true;allowClose=true;
        SaveWindowSize();CancelSleepTimer();poolStop?.Cancel();testStop?.Cancel();usageLifetime20.Cancel();
        var owned=new[]{client,testing}.OfType<AgentClient>().Distinct().ToArray();
        // Disconnected shutdown needs no Agent, proxy recovery process or UI.
        // Only our live clients are closed. Their Agents restore their own
        // lease on pipe loss; the pre-armed RunOnce remains the crash fallback.
        if(owned.Length==0)return;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try{Task.Run(()=>Task.WhenAll(owned.Select(item=>item.StopForSystemShutdown(deadline.Token)))).GetAwaiter().GetResult();}
        catch{ /* Never veto Windows shutdown or show a recovery dialog here. */ }
    }
    internal async Task ConnectShutdownFixture30()
    {
        client=new AgentClient(true);await client.Start();
        var reply=await client.Send(new(){Action="connect",Profile=Importer.Single("socks5://127.0.0.1:9#ShutdownFixture"),SystemProxy=false});
        if(reply.State!="connected"||reply.Error.Length>0)throw new SafeError("FIXTURE_CONNECTION_FAILED");
        SetState(reply);
    }
}
