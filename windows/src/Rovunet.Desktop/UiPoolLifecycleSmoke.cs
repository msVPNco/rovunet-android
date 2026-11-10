using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Win32;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunPoolLifecycleSmoke(string report)
    {
        var checks = new Dictionary<string,bool>(); var before = new WinInetProxy().Read();
        using var sink = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        string endpoint = sink.Client.LocalEndPoint!.ToString()!;
        networkFixtureMode = true;
        try
        {
            foreach (bool full in new[] { false, true })
            foreach (bool suspend in new[] { false, true })
            {
                string prefix = (full ? "verified" : "basic") + (suspend ? "_suspend" : "_cancel");
                var profile = Importer.Single(ManualProfiles.Dns("dnstt", "Lifecycle fixture", "t.example.invalid", endpoint, "udp", new string('a',64), "127.0.0.1", "22", "fixture", "fixture"));
                data.Profiles = [profile]; data.SelectedId = profile.Id; data.DnsPoolEnabled = true; data.DnsPoolVerify = full; data.DnsPoolList = endpoint; Build();
                poolFixtureAgents.Clear(); var pending = Run(Connect);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (poolStop == null || full && poolFixtureAgents.Count == 0) await Task.Delay(25, deadline.Token);
                checks[prefix + "_busy_pool_observed"] = busy && state.State == "connecting";
                if (suspend) OnPower(this, new PowerModeChangedEventArgs(PowerModes.Suspend)); else RequestDisconnect();
                await pending.WaitAsync(TimeSpan.FromSeconds(30));
                while (busy || client != null || poolStop != null || state.State != "disconnected") await Task.Delay(25, deadline.Token);
                checks[prefix + "_operation_stopped"] = !busy && client == null && poolStop == null && state.State == "disconnected";
                bool exited = true;
                foreach (int id in poolFixtureAgents)
                {
                    try { using var child = Process.GetProcessById(id); await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (ArgumentException) { } catch { exited = false; }
                }
                checks[prefix + "_probe_agents_exited"] = exited && (!full || poolFixtureAgents.Count > 0);
                OnPower(this, new PowerModeChangedEventArgs(PowerModes.Resume)); await Task.Delay(100);
                checks[prefix + "_resume_does_not_reconnect"] = !busy && client == null && state.State == "disconnected";
                checks[prefix + "_original_preserved"] = ReferenceEquals(data.Profiles[0], profile) && Importer.Fields(profile.Raw)[4] == endpoint + ":0";
            }
            checks["proxy_unchanged"] = before == new WinInetProxy().Read();
        }
        catch (Exception error) { checks["failure_" + error.GetType().Name] = false; }
        finally { poolStop?.Cancel(); networkFixtureMode = false; }
        bool passed = checks.Count > 0 && checks.Values.All(v => v);
        using var output = new FileStream(report, FileMode.CreateNew);
        JsonSerializer.Serialize(output, new { suite = "WPF pool cancel and suspend notification", physical_suspend_tested = false, external_network_tested = false, checks, passed }, new JsonSerializerOptions { WriteIndented = true });
        return passed;
    }
}
