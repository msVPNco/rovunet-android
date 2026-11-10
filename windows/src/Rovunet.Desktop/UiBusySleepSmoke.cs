using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    bool networkFixtureMode;
    // Only fixed synthetic loopback profiles; user storage remains disabled by fixtureMode.
    public async Task<bool> RunBusySleepSmoke(string reportPath, string mode = "timer")
    {
        var checks = new Dictionary<string, bool>();
        var before = new WinInetProxy().Read();
        Process? server = null; using var job = new ProcessJob();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var target = new TcpListener(IPAddress.Loopback, 0); target.Start();
        int targetPort = ((IPEndPoint)target.LocalEndpoint).Port;
        int accepted = 0, received = 0; string probeCode = "not_completed";
        var arrival = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetTask = Task.Run(async () => {
            try {
                while (!lifetime.IsCancellationRequested) {
                    using var socket = await target.AcceptTcpClientAsync(lifetime.Token);
                    Interlocked.Increment(ref accepted);
                    byte[] bytes = new byte[512];
                    int count;
                    try { count = await socket.GetStream().ReadAsync(bytes, lifetime.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2), lifetime.Token); }
                    catch (TimeoutException) { continue; }
                    if (count == 0) continue;
                    Interlocked.Add(ref received, count);
                    arrival.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, lifetime.Token);
                }            } catch (OperationCanceledException) { }
        });
        Task? probe = null;
        int httpPort = 0, socksPort = 0;
        try
        {
            string engine = Path.Combine(AppContext.BaseDirectory, "engines", "xray.exe");
            using (var input = File.OpenRead(engine))
                if (Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant() != "d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e") throw new SafeError("ENGINE_HASH_MISMATCH");
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            string config = JsonSerializer.Serialize(new {
                log = new { loglevel = "none" },
                inbounds = new[] { new { listen = "127.0.0.1", port, protocol = "vless", settings = new { clients = new[] { new { id = "00000000-0000-4000-8000-000000000001" } }, decryption = "none" }, streamSettings = new { network = "ws", wsSettings = new { path = "/busy-fixture" } } } },
                outbounds = new[] { new { protocol = "freedom", settings = new { redirect = "127.0.0.1:0", finalRules = new[] { new { action = "allow", ip = new[] { "127.0.0.1/32" } } } } } }
            });
            server = Process.Start(new ProcessStartInfo(engine) { UseShellExecute = false, CreateNoWindow = true, Arguments = "run -c stdin:", RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
            job.Assign(server); server.BeginOutputReadLine(); server.BeginErrorReadLine(); await server.StandardInput.WriteAsync(config); server.StandardInput.Close();
            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (true) { using var socket = new TcpClient(); try { await socket.ConnectAsync(IPAddress.Loopback, port, ready.Token); break; } catch (SocketException) { await Task.Delay(50, ready.Token); } }
            var profile = Importer.Single($"vless://00000000-0000-4000-8000-000000000001@127.0.0.1:{port}?type=ws&security=none&path=%2Fbusy-fixture");
            data.LiveLatency = false;
            data.Profiles = [profile]; data.SelectedId = profile.Id; Build();
            networkFixtureMode = true;recoverConnection=()=>Task.CompletedTask; // Probe-only fixture owns no system proxy lease.
            var clock = new FixtureSleepClock(); sleepSession = new(clock);
            await Run(Connect); checks["actual_agent_connected"] = state.State == "connected";
            if (client == null || state.State != "connected") throw new SafeError("AGENT_FAILED");
            durationTimer.Stop();
            httpPort = state.HttpPort; socksPort = state.SocksPort;
            if (mode == "replacement")
            {
                long oldGeneration = sleepSession.Generation;
                var oldClient = client;
                await Run(Disconnect); await Run(Connect); durationTimer.Stop();
                var replacement = client;
                await ExpireSleepTimer(oldGeneration);
                checks["replacement_agent_is_distinct"] = replacement != null && !ReferenceEquals(oldClient, replacement);
                checks["stale_expiry_preserves_actual_replacement"] = ReferenceEquals(client, replacement) && state.State == "connected" && sleepSession.Active;
                if (replacement == null || (await replacement.Send(new() { Action = "status" })).State != "connected") throw new SafeError("AGENT_FAILED");
                httpPort = state.HttpPort; socksPort = state.SocksPort;
            }
            var current = client;
            probe = Run(async () => { var reply = await current.Send(new() { Action = "probe", ProbeUrl = "https://10.77.0.1:" + targetPort }); probeCode = reply.Error.Length == 0 ? reply.State : reply.Error; SetState(reply); });
            await arrival.Task.WaitAsync(TimeSpan.FromSeconds(8));
            checks["tls_bytes_reached_stalled_loopback_target"] = true;
            checks["wpf_operation_was_busy_at_expiry"] = busy;
            if (mode is "timer" or "replacement") { clock.Seconds = 300; TickSleepTimer(); }
            else if (mode == "suspend") OnPower(this, new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Suspend));
            else if (mode == "cancel")
            {
                var collectionPower=groups.Single(g=>g.Members.Any(p=>p.Id==data.SelectedId)).Connect;
                checks["collection_cancel_button_enabled"]=collectionPower.IsEnabled;
                collectionPower.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
            else
            {
                // Terminate only the exact child Agent owned by this synthetic fixture.
                if (mode == "recovery-failure") recoverConnection = () => Task.FromException(new SafeError("PROXY_RECOVERY_CONFLICT"));
                using var child = Process.GetProcessById(current.AgentProcessId ?? throw new SafeError("AGENT_FAILED"));
                child.Kill(); await child.WaitForExitAsync();
            }
            await probe.WaitAsync(TimeSpan.FromSeconds(35));
            var end = Stopwatch.StartNew();
            while ((busy || client != null || lostClient != null) && end.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(50);
            checks["busy_work_completed_and_agent_released"] = !busy && client == null && lostClient == null;
            async Task<bool> Closed(int endpoint) { using var socket = new TcpClient(); try { await socket.ConnectAsync(IPAddress.Loopback, endpoint); return false; } catch (SocketException) { return true; } }
            checks["stop_result_matches_fault_mode"] = mode is "timer" or "replacement" or "suspend" or "cancel"
                ? state.State == "disconnected" && state.Error.Length == 0
                : state.State == "error" && state.Error == (mode == "recovery-failure" ? "PROXY_RECOVERY_CONFLICT" : "AGENT_EXITED");
            checks["http_listener_closed"] = await Closed(httpPort);
            checks["socks_listener_closed"] = await Closed(socksPort);
            checks["timer_not_rearmed"] = !sleepSession.Active;
            if (mode == "suspend")
            {
                OnPower(this, new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Resume)); await Task.Delay(100);
                checks["busy_resume_stays_disconnected"] = client == null && state.State == "disconnected";
                await Run(Connect); durationTimer.Stop();
                int idleHttp = state.HttpPort, idleSocks = state.SocksPort;
                checks["idle_actual_agent_connected"] = client != null && state.State == "connected";
                await SuspendConnection();
                OnPower(this, new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Resume)); await Task.Delay(100);
                checks["idle_suspend_resume_disconnected"] = client == null && state.State == "disconnected" && !sleepSession.Active;
                checks["idle_listeners_closed"] = await Closed(idleHttp) && await Closed(idleSocks);
            }
            checks["proxy_unchanged"] = before == new WinInetProxy().Read();
        }
        catch (Exception error) { checks["failure_" + (error is SafeError safe ? safe.Code : error.GetType().Name)] = false; }
        finally
        {
            CancelSleepTimer();
            recoverConnection = () => AgentClient.Recover();
            try { if (client != null) { client.Abort(); if (probe != null) await probe.WaitAsync(TimeSpan.FromSeconds(10)); await Disconnect(); } } catch { checks["cleanup_failed"] = false; }
            networkFixtureMode = false; lifetime.Cancel(); target.Stop();
            try { await targetTask; } catch { }
            if (server is { HasExited: false }) { server.Kill(true); await server.WaitForExitAsync(); } server?.Dispose();
        }
        bool passed = checks.Count > 0 && checks.Values.All(v => v);
        using var output = new FileStream(reportPath, FileMode.CreateNew);
        JsonSerializer.Serialize(output, new { suite = "WPF busy sleep + real Agent", mode, recovery_failure_injected = mode == "recovery-failure", simulated_timer_clock = mode is "timer" or "replacement", physical_suspend_tested = false, external_network_tested = false, https_success_verified = false, final_state = state.State, state_error = state.Error, safe_error = lastError, probe_code = probeCode, accepted_connections = accepted, received_bytes = received, checks, passed }, new JsonSerializerOptions { WriteIndented = true });
        return passed;
    }
}
