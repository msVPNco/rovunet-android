using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Agent;

public sealed class Runtime(bool probeOnly = false) : IDisposable
{
    readonly HttpClient statsClient=new(new SocketsHttpHandler {UseProxy=false,AllowAutoRedirect=false});
    int statsPort;TrafficSnapshot? lastTraffic;
    CancellationTokenSource? slipLogs;
    string naiveFailure="NAIVE_START_FAILED";
    Process? core, transport, naive, slip; EphemeralConfig? ephemeral; ProcessJob? job; int httpPort, socksPort; bool verified; TimeSpan probeBudget = TimeSpan.FromSeconds(15);
    public event Action? UnexpectedExit;
    void OnCoreExit(object? sender, EventArgs args) => UnexpectedExit?.Invoke();
    public const string EngineSha256 = "d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e";
    public async Task<AgentReply> Connect(Profile profile, bool applyProxy, CancellationToken ct, string hostKey = "", string slipCertificate = "", int? slipInterval = null,string sshTlsCa="",int localHttpPort=0,int localSocksPort=0)
    {
        if (core != null || transport != null) throw new SafeError("ALREADY_CONNECTED");
        // Reparse untrusted IPC; derived fields such as BlockReason are not an authorization.
        profile = Importer.Single(profile.Raw);
        if (!profile.CanConnect) throw new SafeError(profile.BlockReason);
        if(TransportPlan.UsesNaive(profile.Raw)||SshHostKey.Required(profile))probeBudget=TimeSpan.FromSeconds(25);
        string exe = Path.Combine(AppContext.BaseDirectory, "engines", "xray.exe");
        using (var file = File.OpenRead(exe))
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(EngineSha256, StringComparison.OrdinalIgnoreCase)) throw new SafeError("ENGINE_HASH_MISMATCH");
        if(localHttpPort is <0 or >65535||localSocksPort is <0 or >65535||localHttpPort!=0&&localHttpPort==localSocksPort)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        var first = new TcpListener(IPAddress.Loopback, localHttpPort);
        var second = new TcpListener(IPAddress.Loopback, localSocksPort);
        var clock = Stopwatch.StartNew();
        try
        {
            first.Start();httpPort=((IPEndPoint)first.LocalEndpoint).Port;
            second.Start();socksPort=((IPEndPoint)second.LocalEndpoint).Port;
            job = new ProcessJob();
            int helperPort = 0, upstreamPort = 0;
            if (TransportPlan.UsesSlip(profile.Raw))
            {
                int interval = TransportPlan.EffectiveSlipInterval(Importer.Fields(profile.Raw), slipInterval);
                upstreamPort = await StartSlip(profile, slipCertificate, interval, ct);
                probeBudget = TimeSpan.FromMilliseconds(Math.Clamp(12000L + 10L * interval, 15000L, 65000L));
            }
            if (TransportPlan.UsesNaive(profile.Raw))
            {
                var reserved = new TcpListener(IPAddress.Loopback, 0); reserved.Start(); upstreamPort = ((IPEndPoint)reserved.LocalEndpoint).Port; reserved.Stop();
                string naiveExe = Path.Combine(AppContext.BaseDirectory, "engines", "naive.exe");
                using (var binary = File.OpenRead(naiveExe)) if (!Convert.ToHexString(SHA256.HashData(binary)).Equals(NaiveSha256, StringComparison.OrdinalIgnoreCase)) throw new SafeError("ENGINE_HASH_MISMATCH");
                ephemeral = new EphemeralConfig(TransportPlan.NaiveConfig(profile, upstreamPort));
                naive = Process.Start(new ProcessStartInfo(naiveExe) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { ephemeral.Path }, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new SafeError("CORE_START_FAILED");
                job.Assign(naive);naiveFailure="NAIVE_START_FAILED";var output=ObserveNaive(naive.StandardOutput);var errors=ObserveNaive(naive.StandardError);
                using var naiveDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct); naiveDeadline.CancelAfter(TimeSpan.FromSeconds(10));
                try{await WaitListener(upstreamPort, naive, naiveDeadline.Token);}catch(SafeError e)when(e.Code=="CORE_START_FAILED"){try{await Task.WhenAll(output,errors).WaitAsync(TimeSpan.FromMilliseconds(300));}catch{}throw new SafeError(naiveFailure);}
                // Config has been parsed before the listener starts. Credentials
                // are no longer needed on disk for this running process.
                ephemeral.Dispose();ephemeral=null;
                naive.EnableRaisingEvents = true; naive.Exited += OnCoreExit;
                helperPort = upstreamPort;
            }
            if (TransportPlan.UsesHelper(profile.Raw))
            {
                var reserved = new TcpListener(IPAddress.Loopback, 0); reserved.Start(); helperPort = ((IPEndPoint)reserved.LocalEndpoint).Port; reserved.Stop();
                var helper = Path.Combine(AppContext.BaseDirectory, "engines", "rovunet-transports.exe");
                using (var binary = File.OpenRead(helper)) if (!Convert.ToHexString(SHA256.HashData(binary)).Equals(EnginePins.Transport, StringComparison.OrdinalIgnoreCase)) throw new SafeError("ENGINE_HASH_MISMATCH");
                transport = Process.Start(new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new SafeError("CORE_START_FAILED");
                job.Assign(transport); _ = Drain(transport.StandardError);
                await transport.StandardInput.WriteLineAsync(TransportPlan.Config(profile, helperPort, hostKey, upstreamPort, slipInterval,sshTlsCa).AsMemory(), ct); await transport.StandardInput.FlushAsync(ct);
                using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct); startTimeout.CancelAfter(TimeSpan.FromSeconds(TransportPlan.UsesSlip(profile.Raw) ? 65 : 45));
                string line = await ReadStatusLine(transport.StandardOutput, startTimeout.Token);
                using var message = JsonDocument.Parse(line); var root = message.RootElement; string phase = root.GetProperty("state").GetString() ?? "";
                if (phase == "trust")
                {
                    var reply = new AgentReply { State = "trust", Error = root.GetProperty("error").GetString() ?? "SSH_TRUST_REQUIRED", HostKey = root.GetProperty("hostKey").GetString() ?? "", Fingerprint = root.GetProperty("fingerprint").GetString() ?? "" };
                    Stop(); return reply;
                }
                if (phase != "ready")
                {
                    string reason=root.TryGetProperty("error",out var error)?error.GetString()??"":"";
                    throw new SafeError(reason is "SSH_AUTH_FAILED" or "SSH_CONNECT_FAILED" or "SSH_HANDSHAKE_FAILED" or "DNS_TUNNEL_FAILED" or "DISCOVERY_LIST_NOT_READY" or "TRANSPORT_START_FAILED" ? reason : "TRANSPORT_START_FAILED");
                }
                _ = Drain(transport.StandardOutput); transport.EnableRaisingEvents = true; transport.Exited += OnCoreExit;
                if (transport.HasExited) throw new SafeError("TRANSPORT_START_FAILED");
            }
            var statsReservation=new TcpListener(IPAddress.Loopback,0);statsReservation.Start();statsPort=((IPEndPoint)statsReservation.LocalEndpoint).Port;statsReservation.Stop();
            string config = CoreLinks.Runtime(profile, httpPort, socksPort, helperPort,statsPort);
            first.Stop(); second.Stop();
            core = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, Arguments = "run -c stdin:", RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe)! }) ?? throw new SafeError("CORE_START_FAILED");
            core.EnableRaisingEvents = true; core.Exited += OnCoreExit;
            job.Assign(core); // Config is sent only after kill-on-close ownership is installed.
            _ = Drain(core.StandardOutput); _ = Drain(core.StandardError);
            await core.StandardInput.WriteAsync(config.AsMemory(), ct); core.StandardInput.Close();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await WaitListener(httpPort, core, deadline.Token); await WaitListener(socksPort, core, deadline.Token);
            if (applyProxy && !probeOnly) { Recovery.Arm(); new ProxyLease(new WinInetProxy(), new LeaseStore(),AppContext.BaseDirectory).Acquire(httpPort);  }
            // DNS helpers report ready only after their authenticated remote handshake.
            // This is distinct from HTTPS verification and from a local listener starting.
            return new() { State = "connected", HttpPort = httpPort, SocksPort = socksPort, ElapsedMs = clock.ElapsedMilliseconds,TunnelEstablished=ProfileEditing.IsDns(profile),SshEstablished=SshHostKey.Required(profile) };
        }
        catch(SocketException error)when(error.SocketErrorCode==SocketError.AddressAlreadyInUse){Stop();throw new SafeError("LOCAL_PROXY_PORT_BUSY");}
        catch { Stop(); throw; }
        finally { first.Stop(); second.Stop(); }
    }
    async Task<int> StartSlip(Profile profile, string certificate, int interval, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(certificate)) throw new SafeError("SLIP_CERT_REQUIRED");
        var pem = SlipCertificate.ReadPublic(certificate);
        var exe = Path.Combine(AppContext.BaseDirectory,"engines","slipstream-client.exe");
        using (var file = File.OpenRead(exe))
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals("206b43eacdae8c035176e458005117b7874d22ad82d896f01524f247bae6f43c",StringComparison.OrdinalIgnoreCase)) throw new SafeError("ENGINE_HASH_MISMATCH");
        var f = Importer.Fields(profile.Raw);
        var reservation = new TcpListener(IPAddress.Loopback,0);
        reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        ephemeral = new EphemeralConfig(pem);
        var info = new ProcessStartInfo(exe) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        foreach (var arg in new[]{"--tcp-listen-host","127.0.0.1","--tcp-listen-port",port.ToString(),"--domain",f[3],"--cert",ephemeral.Path,"--congestion-control",f[7],"--keep-alive-interval",interval.ToString(System.Globalization.CultureInfo.InvariantCulture)}) info.ArgumentList.Add(arg);
        foreach (string wire in f[4].Split(','))
        {
            var r=TransportPlan.SlipResolver(wire);
            info.ArgumentList.Add(f[5]=="1" || r.Authoritative ? "--authoritative" : "--resolver");
            info.ArgumentList.Add(r.Address);
        }
        if (!File.Exists(Path.Combine(Environment.SystemDirectory,"vcruntime140.dll"))) throw new SafeError("SLIP_VC_REQUIRED");
        info.Environment["RUST_LOG"]="info";
        slip=Process.Start(info) ?? throw new SafeError("CORE_START_FAILED");
        job!.Assign(slip);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        slipLogs=new CancellationTokenSource();
        slip.Exited+=(_,_) => ready.TrySetException(new SafeError("SLIP_HANDSHAKE_FAILED"));
        slip.EnableRaisingEvents=true;
        _=ObserveSlip(slip.StandardOutput,ready,slipLogs.Token);
        _=ObserveSlip(slip.StandardError,ready,slipLogs.Token);
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(30),ct); }
        catch (TimeoutException) { throw new SafeError("SLIP_HANDSHAKE_FAILED"); }
        slip.Exited+=OnCoreExit;
        if (slip.HasExited) throw new SafeError("SLIP_HANDSHAKE_FAILED");
        return port;
    }
    // The pinned binary emits these lifecycle events. Retain at most 512
    // characters and never forward raw core output to diagnostics or the UI.
    async Task ObserveSlip(StreamReader reader, TaskCompletionSource ready, CancellationToken token)
    {
        char[] buffer=new char[1024]; var line=new System.Text.StringBuilder(); bool oversized=false;
        try
        {
            int n;
            while ((n=await reader.ReadAsync(buffer.AsMemory(),token))>0)
            {
                for(int i=0;i<n;i++)
                {
                    if(buffer[i]!='\n')
                    {
                        if(line.Length<512) line.Append(buffer[i]); else oversized=true;
                        continue;
                    }
                    if(!oversized && !token.IsCancellationRequested)
                    {
                        string entry=line.ToString().Trim();
                        if(entry.EndsWith(" Connection ready",StringComparison.Ordinal)) ready.TrySetResult();
                        else if(ready.Task.IsCompletedSuccessfully && entry.Contains("Connection closed event=",StringComparison.Ordinal))
                            UnexpectedExit?.Invoke();
                    }
                    oversized=false;line.Clear();
                }
            }
        }
        catch(OperationCanceledException) { }
        catch(IOException) { ready.TrySetException(new SafeError("SLIP_HANDSHAKE_FAILED")); }
    }
    public const string NaiveSha256 = "ecf6f56a834a3e945e703ddb766b32c8c474e9767d472bb0b45177c33300aa79";
    async Task ObserveNaive(StreamReader reader)
    {
        char[] buffer=new char[512];var line=new System.Text.StringBuilder();
        try{int n;while((n=await reader.ReadAsync(buffer))>0)for(int i=0;i<n;i++){if(buffer[i]=='\n'){if(line.ToString().Contains("Error reading",StringComparison.Ordinal))naiveFailure="NAIVE_CONFIG_FAILED";line.Clear();}else if(line.Length<1024)line.Append(buffer[i]);}}catch{}
        // No raw output, paths, usernames or proxy URLs are retained or logged.
    }
    static async Task<string> ReadStatusLine(StreamReader reader, CancellationToken ct)
    {
        char[] one = new char[1]; var line = new System.Text.StringBuilder();
        while (line.Length < 16384)
        {
            if (await reader.ReadAsync(one.AsMemory(), ct) == 0) throw new SafeError("TRANSPORT_START_FAILED");
            if (one[0] == '\n') return line.ToString(); line.Append(one[0]);
        }
        throw new SafeError("INVALID_MESSAGE");
    }
    static async Task Drain(StreamReader reader)
    {
        // Bounded discard buffer, no unbounded ReadToEnd/string logs containing secrets.
        char[] buffer = new char[2048]; try { while (await reader.ReadAsync(buffer) > 0) { } } catch { }
    }
    static async Task WaitListener(int port, Process child, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested(); if (child.HasExited) throw new SafeError("CORE_START_FAILED");
            using var socket = new TcpClient();
            try { await socket.ConnectAsync(IPAddress.Loopback, port, ct); if (child.HasExited) throw new SafeError("CORE_START_FAILED"); return; }
            catch (SocketException) { await Task.Delay(60, ct); }
        }
    }
    public AgentReply Status()
    {
        if (core != null && core.HasExited) { Stop(); return new() { State = "error", Error = "CORE_EXITED" }; }
        return new() { State = core == null ? "disconnected" : "connected", HttpPort = httpPort, SocksPort = socksPort, Verified = verified };
    }
    public async Task<AgentReply> Probe(string url, CancellationToken ct, bool live = false,bool fallbacks=false)
    {
        if(core==null||core.HasExited)throw new SafeError("NOT_CONNECTED");
        var targets=HttpsProbePolicy.Targets(url,fallbacks&&!live);AgentReply reply=new();
        for(int i=0;i<targets.Length;i++){
            ct.ThrowIfCancellationRequested();reply=await ProbeTarget(targets[i],ct,live,i>0);reply.ProbeTargetsTried=i+1;
            if(ct.IsCancellationRequested||reply.Error=="CANCELLED"){reply.Verified=verified;return reply;}
            if(reply.Verified){if(!live)verified=true;reply.ProbeFallbackUsed=i>0;return reply;}
        }
        if(!live)verified=false;
        return reply;
    }
    async Task<AgentReply> ProbeTarget(string url,CancellationToken ct,bool live,bool alternative)
    {
        if (core == null || core.HasExited) throw new SafeError("NOT_CONNECTED");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0) throw new SafeError("HTTPS_REQUIRED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(live?TimeSpan.FromSeconds(5):alternative?TimeSpan.FromSeconds(8):probeBudget);
        var clock = Stopwatch.StartNew();
        try
        {
            using var handler = new SocketsHttpHandler { Proxy = new WebProxy($"http://127.0.0.1:{httpPort}"), UseProxy = true, AllowAutoRedirect = false, ConnectTimeout = probeBudget };
            // Default platform TLS validation stays enabled; there is no direct retry.
            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            // A valid TLS session and a 2xx HTTP response are sufficient for
            // this latency check; waiting for a slow body adds false timeouts.
            bool success = (int)response.StatusCode is >= 200 and < 300;
            return new() { State = "connected", HttpPort = httpPort, SocksPort = socksPort, Verified = success, ProbeStage = "https", Error = success ? "" : "HTTPS_STATUS_FAILED", ElapsedMs = clock.ElapsedMilliseconds };
        }
        catch(Exception e)
        {
            string error=e is OperationCanceledException?ct.IsCancellationRequested?"CANCELLED":"PROBE_TIMEOUT":e is HttpRequestException http?http.HttpRequestError switch {HttpRequestError.SecureConnectionError=>"PROBE_TLS_FAILED",HttpRequestError.ProxyTunnelError=>"PROBE_PROXY_FAILED",HttpRequestError.NameResolutionError=>"PROBE_DNS_FAILED",_=>"HTTPS_TEST_FAILED"}:"HTTPS_TEST_FAILED";
            return new(){State="connected",HttpPort=httpPort,SocksPort=socksPort,Error=error,ProbeStage=error=="PROBE_TLS_FAILED"?"tls":error=="PROBE_TIMEOUT"?"timeout":"proxy",ElapsedMs=clock.ElapsedMilliseconds};
        }
    }
    public async Task<AgentReply> Telemetry(CancellationToken ct)
    {
        var reply=Status();if(reply.State!="connected")return reply;
        try { lastTraffic=await XrayStats.Read(statsClient,statsPort,ct);reply.TrafficAvailable=true;reply.UploadedBytes=lastTraffic.Uploaded;reply.DownloadedBytes=lastTraffic.Downloaded; }
        catch { reply.TelemetryError="STATS_UNAVAILABLE"; }
        return reply;
    }
    public AgentReply Stop()
    {
        if(!probeOnly&&core!=null&&!core.HasExited&&statsPort>0){try{lastTraffic=XrayStats.Read(statsClient,statsPort,CancellationToken.None).GetAwaiter().GetResult();}catch{}}
        slipLogs?.Cancel();
        var clock = Stopwatch.StartNew(); string error = "";
        // Restore first so applications don't point at a listener we've just removed.
        try { if (!probeOnly) Recovery.Restore(); } catch (SafeError e) { error = e.Code; } catch { error = "PROXY_RECOVERY_FAILED"; }
        if (core != null) core.Exited -= OnCoreExit;
        if (transport != null) transport.Exited -= OnCoreExit;
        if (naive != null) naive.Exited -= OnCoreExit;
        if (slip != null) slip.Exited -= OnCoreExit;
        try { if (core != null && !core.HasExited) { core.Kill(true); core.WaitForExit(3000); } }
        finally {
            if (transport != null)
            {
                try { transport.StandardInput.Close(); if (!transport.WaitForExit(2000)) { transport.Kill(true); transport.WaitForExit(2000); } } catch { }
                transport.Dispose(); transport = null;
            }
            if (naive != null) { try { if (!naive.HasExited) { naive.Kill(true); naive.WaitForExit(2000); } } catch { } naive.Dispose(); naive = null; }
            if (slip != null) { try { if (!slip.HasExited) { slip.Kill(true); slip.WaitForExit(2000); } } catch { } slip.Dispose(); slip = null; }
            slipLogs?.Dispose(); slipLogs=null; ephemeral?.Dispose(); ephemeral = null;
            job?.Dispose(); job = null; core?.Dispose(); core = null; httpPort = socksPort = 0; verified = false; probeBudget = TimeSpan.FromSeconds(15); }
        if (error.Length > 0) throw new SafeError(error);
        return new() { State = "disconnected", ElapsedMs = clock.ElapsedMilliseconds, TrafficAvailable=lastTraffic!=null, UploadedBytes=lastTraffic?.Uploaded??0, DownloadedBytes=lastTraffic?.Downloaded??0 };
    }
    public void Dispose() { try { Stop(); } catch { } statsClient.Dispose(); }
}
