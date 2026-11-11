using System.Diagnostics;
using System.IO.Pipes;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Threading.Channels;
using Rovunet.Core;
using Rovunet.Agent;
using Rovunet.Platform;

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045) || !Environment.Is64BitProcess) return 10;
// Keep all state mutations on this thread, including a thread-affine named Mutex.
bool probeOnly = args.Length == 4 && args[0] == "--probe-pipe";
using var mutex = probeOnly ? null : new Mutex(false, @"Global\Rovunet.NetworkOwner." + WindowsIdentity.GetCurrent().User!.Value);
bool owned;
try { owned = mutex?.WaitOne(0) ?? true; } catch (AbandonedMutexException) { owned = true; }
if (!owned) return 11;
try
{
    if (args.Length == 1 && args[0] == "--recover") { Recovery.Restore(); return 0; }
    if (args.Length == 1 && args[0] == "--keep-current-proxy") { Recovery.KeepCurrent(); return 0; }
    if (args.Length != 4 || (args[0] != "--pipe" && !probeOnly) || args[2] != "--parent" || !int.TryParse(args[3], out var parentId)) return 12;
    using var parent = Process.GetProcessById(parentId);
    string parentPath = parent.MainModule?.FileName ?? "";
    if (!string.Equals(Path.GetDirectoryName(parentPath), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)) return 13;
    using var lifetime = new CancellationTokenSource();
    // Event-driven cancellation: no perpetual heartbeat/polling in idle state.
    parent.EnableRaisingEvents = true; parent.Exited += (_, _) => lifetime.Cancel();
    if (parent.HasExited) return 14;
    if (!probeOnly) Recovery.Restore();
    using var pipe = new NamedPipeServerStream(args[1], PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    using var runtime = new Runtime(probeOnly);
    runtime.UnexpectedExit += () => lifetime.Cancel();
    NetworkAddressChangedEventHandler network = (_, _) => lifetime.Cancel();
    NetworkChange.NetworkAddressChanged += network;
    try
    {
        // A probe can be cancelled before its client opens the pipe. Bound that idle
        // server lifetime without shortening the normal connection/recovery path.
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); wait.CancelAfter(TimeSpan.FromSeconds(probeOnly ? 3 : 15));
        pipe.WaitForConnectionAsync(wait.Token).GetAwaiter().GetResult();
        var commands = Channel.CreateBounded<(AgentCommand Command,CancellationTokenSource? Probe)>(new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true });
        var probeGate=new object();CancellationTokenSource? probeCancellation=null;
        _ = Task.Run(async () =>
        {
            try { while (!lifetime.IsCancellationRequested) {
                var command = await Wire.Read<AgentCommand>(pipe, lifetime.Token);
                // Cancellation must reach a pending HTTPS request without closing the
                // connection pipe or tearing down the user's established tunnel.
                if(command.Action=="cancel-probe"){lock(probeGate)probeCancellation?.Cancel();continue;}
                CancellationTokenSource? probe=null;
                if(command.Action is "probe" or "latency")lock(probeGate){if(probeCancellation!=null)throw new SafeError("INVALID_MESSAGE");probeCancellation=probe=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);}
                await commands.Writer.WriteAsync((command,probe), lifetime.Token);
            } }
            catch { lifetime.Cancel(); } finally { commands.Writer.TryComplete(); }
        });
        while (!lifetime.IsCancellationRequested)
        {
            var pending=commands.Reader.ReadAsync(lifetime.Token).AsTask().GetAwaiter().GetResult();var cmd=pending.Command;
            AgentReply reply;
            try
            {
                reply = cmd.Action switch
                {
                    "connect" => runtime.Connect(cmd.Profile ?? throw new SafeError("INVALID_MESSAGE"), cmd.SystemProxy && !probeOnly, lifetime.Token, cmd.HostKey, cmd.SlipCertificate, cmd.SlipInterval,cmd.SshTlsCa,cmd.LocalHttpPort,cmd.LocalSocksPort).GetAwaiter().GetResult(),
                    "disconnect" => runtime.Stop(),
                    "probe" => runtime.Probe(cmd.ProbeUrl, pending.Probe!.Token,false,cmd.ProbeFallbacks).GetAwaiter().GetResult(),
                    "status" => runtime.Status(),
                    "telemetry" => runtime.Telemetry(lifetime.Token).GetAwaiter().GetResult(),
                    "latency" => runtime.Probe(cmd.ProbeUrl,pending.Probe!.Token,true).GetAwaiter().GetResult(),
                    _ => throw new SafeError("INVALID_MESSAGE")
                };
            }
            catch (Exception e)
            {
                var code = e is SafeError se ? se.Code : e is OperationCanceledException ? "CANCELLED" : "CONNECTION_FAILED";
                if (cmd.Action is "probe" or "latency" or "telemetry") { reply = runtime.Status(); reply.Error = code; }
                else { try { runtime.Stop(); } catch (SafeError recoveryError) { code = recoveryError.Code; } reply = new() { State = "error", Error = code, ProbeStage=code.StartsWith("SSH_")?"ssh":code.StartsWith("DNS_")||code.StartsWith("SLIP_")?"tunnel":"startup" }; }
            }
            if(pending.Probe!=null)lock(probeGate){if(ReferenceEquals(probeCancellation,pending.Probe))probeCancellation=null;pending.Probe.Dispose();}
            Wire.Write(pipe, reply, lifetime.Token).GetAwaiter().GetResult();
        }
    }
    finally { lifetime.Cancel(); NetworkChange.NetworkAddressChanged -= network; }
    return 0;
}
catch (SafeError e) { return e.Code == "PROXY_RECOVERY_CONFLICT" ? 21 : 20; }
catch { return 20; } // Never print raw engine, URI, account or exception messages.
finally { mutex?.ReleaseMutex(); }
