using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.NetworkBroker;
internal sealed class NetworkRuntime29(string root,string owner):IDisposable
{
    readonly ProtectedJournal29 store=new();
    NetworkJournal29? journal;
    ProcessJob? job;Process? core,upstream;
    PersistentWfp29? tunnelPolicy;
    volatile bool stopping,upstreamLost;
    void UpstreamExited(object? sender,EventArgs args){if(!ReferenceEquals(sender,upstream))return;upstreamLost=true;if(!stopping&&journal?.Phase=="connected")UnexpectedExit?.Invoke();}
    void CoreExited(object? sender,EventArgs args){if(ReferenceEquals(sender,core)&&!stopping&&journal?.Phase=="connected")UnexpectedExit?.Invoke();}
    void ReleaseUpstream(){if(upstream==null)return;upstream.Exited-=UpstreamExited;upstream.Dispose();upstream=null;}
    public event Action? UnexpectedExit;
    public TunReply29 Status()
    {
        journal??=store.Read();if(journal==null){using var wfp=new PersistentWfp29();if(new BootPolicy(root).Read() is {} boot){bool installed=wfp.BootRuntimePresent(boot.Installation);return new(){State="boot-policy-armed",TunReady=false,ProtectionInstalled=installed,Error=installed?"":"BROKER_BOOT_RECOVERY_REQUIRED",TrafficVerified=false};}return wfp.OwnedKeys().Length==0?new():new(){State="recovery-required",ProtectionInstalled=true,Error="BROKER_ORPHANED_PROTECTION"};}
        if(journal.Owner!=owner)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");
        bool protection=false,allPermits=false;if(journal.KillSwitch){using var wfp=new PersistentWfp29();protection=wfp.AllPresent(journal.ProtectionKeys);allPermits=wfp.AllPresent(journal.FilterKeys);}
        var tunnelRoutes=journal.ConfirmedRoutes.Where(r=>r.Luid==journal.InterfaceLuid).ToArray();
        var physicalRoutes = journal.ConfirmedRoutes.Where(route => route.Luid != journal.InterfaceLuid).ToArray();
        var onlineInterfaces = new HashSet<ulong>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus == OperationalStatus.Up && Guid.TryParse(adapter.Id, out var id)
                && NativeRoutes29.ConvertInterfaceGuidToLuid(ref id, out var luid) == 0)
                onlineInterfaces.Add(luid);
        }
        bool physicalReady = physicalRoutes.Length > 0 && physicalRoutes.All(route =>
            onlineInterfaces.Contains(route.Luid) && NativeRoutes29.Present(route));
        bool ready=physicalReady && journal.Phase=="connected"&&core is {HasExited:false}&&upstream is {HasExited:false}&&!upstreamLost&&tunnelRoutes.Length==4&&tunnelRoutes.All(NativeRoutes29.Present)&&(!journal.KillSwitch||protection&&allPermits);
        return new(){Session=journal.Session,State=ready?(journal.EnableUdp?"experimental-tcp-udp-dns":"experimental-tcp-dns"):"recovery-required",TunReady=ready,ProtectionInstalled=protection,TrafficVerified=false,UdpForwarding=ready&&journal.EnableUdp};
    }
    public async Task<TunReply29> Start(TunRequest29 request,CancellationToken stop)
    {
        TrustedInstallation29.RequireLab(root);MaintenanceGate.RequireOpen(root);var peers=TunPlan29.Validate(request);if(journal!=null||store.Read()!=null)throw new SafeError("BROKER_RECOVERY_REQUIRED");
        if(new BootPolicy(root).Read()!=null&&!request.KillSwitch)throw new SafeError("BROKER_BOOT_REQUIRES_PROTECTION");
        using(var existing=new PersistentWfp29())if(existing.OwnedKeys().Length>0)throw new SafeError("BROKER_ORPHANED_PROTECTION");
        int upstreamId=LocalListener29.Require(request.SocksPort,root,owner);
        if(request.EnableUdp){await SocksUdp30.Require(request.SocksPort,stop);if(LocalListener29.Require(request.SocksPort,root,owner)!=upstreamId)throw new SafeError("BROKER_LISTENER_REJECTED");}
        // .NET also lists a removed Wintun instance as Down. Reuse requires a
        // durable, protected, machine/installation-bound creation receipt.
        // A matching name or deterministic GUID alone never grants ownership.
        if(NetworkInterface.GetAllNetworkInterfaces().Any(i=>i.Name==TunPlan29.Adapter&&!(i.OperationalStatus is OperationalStatus.Down or OperationalStatus.NotPresent&&Guid.TryParse(i.Id,out var idle)&&store.OwnsAdapter(idle,root))))throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");
        var routes=peers.Select(NativeRoutes29.Peer).ToArray();foreach(var route in routes)if(NativeRoutes29.Present(route)||NativeRoutes29.Conflicts(route))throw new SafeError("BROKER_ROUTE_OWNERSHIP_CONFLICT");
        string[] applications=[Path.Combine(root,"engines","xray.exe")];
        stopping=false;upstreamLost=false;
        store.RememberSession(root,request.Session); // Burn once, durably, before any privileged change.
        journal=new(){Session=request.Session,Owner=owner,KillSwitch=request.KillSwitch,EnableUdp=request.EnableUdp,FilterKeys=request.KillSwitch?PersistentWfp29.Labels(applications.Length,peers,true).Select(label=>PersistentWfp29.Key(request.Session,label)).ToArray():[],ProtectionKeys=request.KillSwitch?PersistentWfp29.Labels(applications.Length,peers,false).Select(label=>PersistentWfp29.Key(request.Session,label)).ToArray():[]};
        store.Write(journal); // Write intent before WFP, routes, DNS or driver.
        try{
            upstream=Process.GetProcessById(upstreamId);upstream.Exited+=UpstreamExited;upstream.EnableRaisingEvents=true;
            if(upstream.HasExited||upstreamLost||LocalListener29.Require(request.SocksPort,root,owner)!=upstreamId)throw new SafeError("BROKER_LISTENER_REJECTED");
            if(request.KillSwitch){using var wfp=new PersistentWfp29();wfp.Install(request.Session,applications,peers,0);journal.Phase="protected-starting";store.Write(journal);new BootPolicy(root).RuntimeCommitted(request.Session);}
            foreach(var route in routes)Apply(route);
            job=new ProcessJob();core=Process.Start(new ProcessStartInfo(Path.Combine(root,"engines","xray.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.Combine(root,"engines"),RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-c","stdin:"}})??throw new SafeError("CORE_START_FAILED");job.Assign(core);_=Drain(core.StandardOutput);_=Drain(core.StandardError);
            core.EnableRaisingEvents=true;core.Exited+=CoreExited;journal.EnginePid=core.Id;journal.EngineStartedUtcTicks=core.StartTime.ToUniversalTime().Ticks;store.Write(journal);
            await core.StandardInput.WriteAsync(TunPlan29.Config(request.SocksPort,request.EnableUdp).AsMemory(),stop);core.StandardInput.Close();
            NetworkInterface? adapter=null;var until=DateTime.UtcNow.AddSeconds(20);
            while(DateTime.UtcNow<until){stop.ThrowIfCancellationRequested();if(core.HasExited)throw new SafeError("BROKER_TUN_START_FAILED");adapter=NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(i=>i.Name==TunPlan29.Adapter&&i.OperationalStatus==OperationalStatus.Up);if(adapter!=null)break;await Task.Delay(100,stop);}
            if(adapter==null||!Guid.TryParse(adapter.Id,out var guid)||guid!=TunPlan29.AdapterGuid||NativeRoutes29.ConvertInterfaceGuidToLuid(ref guid,out var luid)!=0)throw new SafeError("BROKER_TUN_START_FAILED");
            journal.InterfaceGuid=guid;journal.InterfaceLuid=luid;store.Write(journal);store.RememberAdapter(guid,root);
            // Only baseline blocks and narrowly scoped upstream permits persist.
            // A reused LUID must never inherit a stale TUN permit after crash or
            // reboot; the live interface permits belong to this dynamic handle.
            if(request.KillSwitch){tunnelPolicy=new PersistentWfp29(dynamicSession:true);tunnelPolicy.Install(request.Session,applications,peers,luid);}
            uint index=(uint)adapter.GetIPProperties().GetIPv4Properties().Index;foreach(var route in NativeRoutes29.Tunnel(luid,index))Apply(route);
            NativeDns29.Set(guid);journal.Phase="connected";store.Write(journal);
            if(upstreamLost||upstream.HasExited||core.HasExited){UnexpectedExit?.Invoke();throw new SafeError("BROKER_NETWORK_PATH_LOST");}
            return Status();
        }catch{
            ReleaseUpstream();
            if(request.KillSwitch){journal.Phase="recovery-required";try{store.Write(journal);}finally{tunnelPolicy?.Dispose();tunnelPolicy=null;job?.Dispose();job=null;}}
            else Recover(request.Session);
            throw;
        }
    }
    void Apply(OwnedRoute29 route){journal!.PendingRoute=route;store.Write(journal);NativeRoutes29.Add(route);journal.ConfirmedRoutes.Add(route);journal.PendingRoute=null;store.Write(journal);}
    public TunReply29 Recover(Guid session)
    {
        journal??=store.Read();if(journal==null)return new();if(journal.Session!=session||journal.Owner!=owner)throw new SafeError("BROKER_SESSION_OWNERSHIP_CONFLICT");
        // A crash between native create and durable confirmation is ambiguous on
        // a physical interface. Retain protection/journal rather than deleting a
        // route that might now belong to another administrator/application.
        if(journal.PendingRoute is {} pending&&NativeRoutes29.Present(pending)&&pending.Luid!=journal.InterfaceLuid)throw new SafeError("BROKER_ROUTE_RECONCILIATION_REQUIRED");
        stopping=true;ReleaseUpstream();tunnelPolicy?.Dispose();tunnelPolicy=null;job?.Dispose();job=null;
        if(core!=null){if(!core.WaitForExit(5000))throw new SafeError("BROKER_ENGINE_STOP_PENDING");core.Dispose();core=null;}else if(journal.EnginePid>0){
            try{using var previous=Process.GetProcessById(journal.EnginePid);if(previous.StartTime.ToUniversalTime().Ticks==journal.EngineStartedUtcTicks&&string.Equals(previous.MainModule?.FileName,Path.Combine(root,"engines","xray.exe"),StringComparison.OrdinalIgnoreCase)){previous.Kill();if(!previous.WaitForExit(5000))throw new SafeError("BROKER_ENGINE_STOP_PENDING");}}catch(ArgumentException){}
        }
        foreach(var route in journal.ConfirmedRoutes.AsEnumerable().Reverse())NativeRoutes29.Remove(route);
        if(journal.InterfaceGuid!=Guid.Empty){var id=journal.InterfaceGuid;if(NativeRoutes29.ConvertInterfaceGuidToLuid(ref id,out var current)==0){if(current!=journal.InterfaceLuid)throw new SafeError("BROKER_ADAPTER_OWNERSHIP_CONFLICT");NativeDns29.Set(id,true);}}
        if(journal.KillSwitch){new BootPolicy(root).Disarm(session);using var wfp=new PersistentWfp29();wfp.Remove(journal.FilterKeys);}store.Delete();journal=null;return new();
    }
    public void LostController(){journal??=store.Read();if(journal!=null&&!journal.KillSwitch)Recover(journal.Session);}
    public void Dispose(){stopping=true;ReleaseUpstream();tunnelPolicy?.Dispose();job?.Dispose();core?.Dispose();} // Opted-in baseline remains until authorized recovery.
    static async Task Drain(StreamReader reader){try{while(await reader.ReadLineAsync()!=null){}}catch{}} // Never forward raw engine logs/addresses.
}
