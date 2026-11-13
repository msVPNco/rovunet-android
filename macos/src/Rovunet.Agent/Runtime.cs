// Adapted from Windows 562a3d71670ceb7de909146bf68116d5cc614496. See docs/ARCHITECTURE.md.
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
    ProxyGuardian? systemProxy;
    SystemTunLease? tun;
    string naiveFailure="NAIVE_START_FAILED";
    string? torDirectory;
    Process? core, transport, naive, slip; EphemeralConfig? ephemeral; ProcessJob? job; int httpPort, socksPort; bool verified; TimeSpan probeBudget = TimeSpan.FromSeconds(15);
    public event Action? UnexpectedExit;
    public event Action<AgentReply>? StageChanged;
    void OnCoreExit(object? sender, EventArgs args) => UnexpectedExit?.Invoke();
    public async Task<AgentReply> Connect(Profile profile, bool applyProxy, CancellationToken ct, string hostKey = "", string slipCertificate = "", int? slipInterval = null,string sshTlsCa="",int localHttpPort=0,int localSocksPort=0,byte[]? proxyStoreKey=null,bool systemTun=false,string tlsCertificatePin="",byte[]? proxyAuthorization=null)
    {
        if (core != null || transport != null) throw new SafeError("ALREADY_CONNECTED");
        // Reparse untrusted IPC; derived fields such as BlockReason are not an authorization.
        profile = MacProtocols.Single(profile.Raw);
        if(tlsCertificatePin.Length>0&&!MacTlsPolicy.Supports(profile.Raw))throw new SafeError("MAC_TLS_EXCEPTION_INVALID");
        if (!profile.CanConnect) throw new SafeError(profile.BlockReason);
        if(systemTun&&MacProtocols.Extended(profile.Raw)?.Protocol=="DoH · DNS only")throw new SafeError("DNS_ONLY_NOT_VPN");
        if(TransportPlan.UsesNaive(profile.Raw)||SshHostKey.Required(profile))probeBudget=TimeSpan.FromSeconds(25);
        string exe = EnginePaths.Require("xray");
        if(applyProxy&&!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
        if(systemTun&&(!OperatingSystem.IsMacOS()||applyProxy))throw new SafeError("MAC_TUN_INVALID_REQUEST");
        if(applyProxy&&proxyAuthorization is {Length: not(0 or 32)})throw new SafeError("INVALID_MESSAGE");
        if(applyProxy&&proxyStoreKey?.Length!=32)throw new SafeError("INVALID_MESSAGE");
        if(localHttpPort is <0 or >65535||localSocksPort is <0 or >65535||localHttpPort!=0&&localHttpPort==localSocksPort)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        var first = new TcpListener(IPAddress.Loopback, localHttpPort);
        var second = new TcpListener(IPAddress.Loopback, localSocksPort);
        var clock = Stopwatch.StartNew();
        // Timestamp remote authentication at the helper event, before local proxy startup.
        // A listening port and the profile type alone are never proof of a server session.
        long serverReadyMs=0;bool authenticatedDns=false,authenticatedSsh=false;
        try
        {
            first.Start();httpPort=((IPEndPoint)first.LocalEndpoint).Port;
            second.Start();socksPort=((IPEndPoint)second.LocalEndpoint).Port;
            job = new ProcessJob();
            int helperPort = 0, upstreamPort = 0;
            if(MacProtocols.Extended(profile.Raw)?.Protocol=="Tor"){
                var reserved=new TcpListener(IPAddress.Loopback,0);reserved.Start();helperPort=((IPEndPoint)reserved.LocalEndpoint).Port;reserved.Stop();
                string directory=Path.Combine(OperatingSystem.IsMacOS()?PrivateStore.Root:Path.GetTempPath(),"tor-session-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);if(!OperatingSystem.IsWindows())File.SetUnixFileMode(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
                torDirectory=directory;transport=EngineProcess.Start(new ProcessStartInfo(EnginePaths.Require("tor")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"-f","-"}})??throw new SafeError("CORE_START_FAILED");job.Assign(transport);_=Drain(transport.StandardError);
                await transport.StandardInput.WriteAsync(TorRuntime.Config(profile.Raw,helperPort,directory).AsMemory(),ct);transport.StandardInput.Close();StageChanged?.Invoke(new(){State="engine-started"});using var bootstrap=CancellationTokenSource.CreateLinkedTokenSource(ct);bootstrap.CancelAfter(TimeSpan.FromMinutes(3));bool ready=false;
                while(await transport.StandardOutput.ReadLineAsync(bootstrap.Token) is {} line){if(line.Contains("Bootstrapped 100%",StringComparison.Ordinal)){ready=true;break;}if(transport.HasExited)break;}if(!ready)throw new SafeError("TOR_BOOTSTRAP_FAILED");_=Drain(transport.StandardOutput);transport.EnableRaisingEvents=true;transport.Exited+=OnCoreExit;probeBudget=TimeSpan.FromSeconds(40);
            }
            var extendedProtocol=MacProtocols.Extended(profile.Raw)?.Protocol;
            if(extendedProtocol is "AnyTLS" or "Mieru" or "TUIC"){
                var reserved=new TcpListener(IPAddress.Loopback,0);reserved.Start();helperPort=((IPEndPoint)reserved.LocalEndpoint).Port;reserved.Stop();
                transport=EngineProcess.Start(new ProcessStartInfo(EnginePaths.Require("rovunet-extra")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new SafeError("CORE_START_FAILED");job.Assign(transport);_=Drain(transport.StandardError);
                await transport.StandardInput.WriteAsync(MacSupplemental.Runtime(profile,helperPort).AsMemory(),ct);transport.StandardInput.Close();using var started=CancellationTokenSource.CreateLinkedTokenSource(ct);started.CancelAfter(TimeSpan.FromSeconds(30));
                if(await ReadStatusLine(transport.StandardOutput,started.Token)!="ROVUNET_EXTRA_READY")throw new SafeError("CORE_START_FAILED");
                _=Drain(transport.StandardOutput);transport.EnableRaisingEvents=true;transport.Exited+=OnCoreExit;
            }
            if(extendedProtocol=="AmneziaWG"){
                var reserved=new TcpListener(IPAddress.Loopback,0);reserved.Start();helperPort=((IPEndPoint)reserved.LocalEndpoint).Port;reserved.Stop();
                transport=EngineProcess.Start(new ProcessStartInfo(EnginePaths.Require("rovunet-awg")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true})??throw new SafeError("CORE_START_FAILED");job.Assign(transport);_=Drain(transport.StandardError);
                await transport.StandardInput.WriteAsync(MacAmnezia.Runtime(profile,helperPort).AsMemory(),ct);transport.StandardInput.Close();using var started=CancellationTokenSource.CreateLinkedTokenSource(ct);started.CancelAfter(TimeSpan.FromSeconds(30));
                string marker=await ReadStatusLine(transport.StandardOutput,started.Token);if(marker!="ROVUNET_AWG_READY")throw new SafeError("AWG_START_FAILED");_=Drain(transport.StandardOutput);transport.EnableRaisingEvents=true;transport.Exited+=OnCoreExit;
            }
            if (TransportPlan.UsesSlip(profile.Raw))
            {
                int interval = TransportPlan.EffectiveSlipInterval(Importer.Fields(profile.Raw), slipInterval);
                upstreamPort = await StartSlip(profile, slipCertificate, interval, ct);
                authenticatedDns=true;serverReadyMs=Math.Max(1,clock.ElapsedMilliseconds);
                probeBudget = TimeSpan.FromMilliseconds(Math.Clamp(12000L + 10L * interval, 15000L, 65000L));
            }
            if (TransportPlan.UsesNaive(profile.Raw))
            {
                var reserved = new TcpListener(IPAddress.Loopback, 0); reserved.Start(); upstreamPort = ((IPEndPoint)reserved.LocalEndpoint).Port; reserved.Stop();
                string naiveExe = EnginePaths.Require("naive");
                ephemeral = new EphemeralConfig(TransportPlan.NaiveConfig(profile, upstreamPort));
                naive = EngineProcess.Start(new ProcessStartInfo(naiveExe) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { ephemeral.Path }, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new SafeError("CORE_START_FAILED");
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
                var helper = EnginePaths.Require("rovunet-transports");
                transport = EngineProcess.Start(new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new SafeError("CORE_START_FAILED");
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
                authenticatedSsh=SshHostKey.Required(profile);
                if(ProfileEditing.IsDns(profile)){authenticatedDns=true;serverReadyMs=Math.Max(1,clock.ElapsedMilliseconds);}
                _ = Drain(transport.StandardOutput); transport.EnableRaisingEvents = true; transport.Exited += OnCoreExit;
                if (transport.HasExited) throw new SafeError("TRANSPORT_START_FAILED");
            }
            var statsReservation=new TcpListener(IPAddress.Loopback,0);statsReservation.Start();statsPort=((IPEndPoint)statsReservation.LocalEndpoint).Port;statsReservation.Stop();
            string tunUser=systemTun?Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant():"",tunPassword=systemTun?Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant():"";
            string config = MacProtocols.Runtime(profile, httpPort, socksPort, helperPort,statsPort,tunUser,tunPassword,tlsCertificatePin);
            if(systemTun) config=await MacTunBootstrap.Prepare(config,ct);
            first.Stop(); second.Stop();
            core = EngineProcess.Start(new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, Arguments = "run -c stdin:", RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(exe)! }) ?? throw new SafeError("CORE_START_FAILED");
            StageChanged?.Invoke(new AgentReply { State="engine-started" });
            core.EnableRaisingEvents = true; core.Exited += OnCoreExit;
            job.Assign(core); // Config is sent only after kill-on-close ownership is installed.
            _ = Drain(core.StandardOutput); _ = Drain(core.StandardError);
            await core.StandardInput.WriteAsync(config.AsMemory(), ct); core.StandardInput.Close();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await WaitListener(httpPort, core, deadline.Token); await WaitListener(socksPort, core, deadline.Token);
            if(applyProxy)systemProxy=await ProxyGuardian.Start(httpPort,socksPort,core,proxyStoreKey??[],ct,proxyAuthorization);
            if(systemTun)tun=await SystemTunLease.Start(socksPort,tunUser,tunPassword,ct);
            // DNS helpers report ready only after their authenticated remote handshake.
            // This is distinct from HTTPS verification and from a local listener starting.
            return new() { State = "connected", HttpPort = httpPort, SocksPort = socksPort, TunInterface=tun?.InterfaceName??"",TunActive=tun?.IsAlive==true,ElapsedMs = clock.ElapsedMilliseconds,HandshakeMs=serverReadyMs,TunnelEstablished=authenticatedDns,SshEstablished=authenticatedSsh,ProbeStage=tun!=null?"system-tun":systemProxy!=null?"system-proxy":"local-proxy" };
        }
        catch(SocketException error)when(error.SocketErrorCode==SocketError.AddressAlreadyInUse){Stop();throw new SafeError("LOCAL_PROXY_PORT_BUSY");}
        catch { Stop(); throw; }
        finally { first.Stop(); second.Stop(); }
    }
    async Task<int> StartSlip(Profile profile, string certificate, int interval, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(certificate)) throw new SafeError("SLIP_CERT_REQUIRED");
        var pem = SlipCertificate.ReadPublic(certificate);
        var exe = EnginePaths.Require("slipstream-client");
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
        info.Environment["RUST_LOG"]="info";
        slip=EngineProcess.Start(info) ?? throw new SafeError("CORE_START_FAILED");
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
        if(tun!=null&&!tun.IsAlive){string error=tun.Error;Stop();return new(){State="error",Error=error};}
        if(systemProxy!=null&&!systemProxy.IsAlive){Stop();return new(){State="error",Error="MAC_PROXY_RESTORE_PENDING"};}
        if (core != null && core.HasExited) { Stop(); return new() { State = "error", Error = "CORE_EXITED" }; }
        return new() { State = core == null ? "disconnected" : "connected", HttpPort = httpPort, SocksPort = socksPort, Verified = verified,TunInterface=tun?.InterfaceName??"",TunActive=tun?.IsAlive==true };
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
            using var handler = MacTunHttp.Handler(httpPort, tun != null, tun?.InterfaceName, probeBudget);
            // Default platform TLS validation stays enabled; there is no direct retry.
            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            bool success = response.IsSuccessStatusCode;
            string stage = tun != null ? "tun-https" : "https", error = success ? "" : "HTTPS_STATUS_FAILED";
            if (success && !live && response.StatusCode != HttpStatusCode.NoContent &&
                response.StatusCode != HttpStatusCode.ResetContent && response.Content.Headers.ContentLength != 0)
            {
                stage = tun != null ? "tun-https-payload" : "https-payload";
                success = await CheckPayload(response.Content, deadline.Token);
                if (!success) error = "HTTPS_TEST_FAILED";
            }
            return new() { State = "connected", HttpPort = httpPort, SocksPort = socksPort,TunInterface=tun?.InterfaceName??"",TunActive=tun?.IsAlive==true, Verified = success, ProbeStage = stage, Error = error, ElapsedMs = clock.ElapsedMilliseconds };
        }
        catch(Exception e)
        {
            string error=e is OperationCanceledException?ct.IsCancellationRequested?"CANCELLED":"PROBE_TIMEOUT":e is HttpRequestException http?http.HttpRequestError switch {HttpRequestError.SecureConnectionError=>"PROBE_TLS_FAILED",HttpRequestError.ProxyTunnelError=>"PROBE_PROXY_FAILED",HttpRequestError.NameResolutionError=>"PROBE_DNS_FAILED",_=>"HTTPS_TEST_FAILED"}:"HTTPS_TEST_FAILED";
            return new(){State="connected",HttpPort=httpPort,SocksPort=socksPort,TunInterface=tun?.InterfaceName??"",TunActive=tun?.IsAlive==true,Error=error,ProbeStage=error=="PROBE_TLS_FAILED"?"tls":error=="PROBE_TIMEOUT"?"timeout":"proxy",ElapsedMs=clock.ElapsedMilliseconds};
        }
    }
    internal static Task<bool> CheckPayload(HttpContent content,CancellationToken cancellation)=>
        HttpsProbeBody.Check(content,cancellation);

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
        if(tun!=null){try{tun.Stop();}catch(SafeError e){error=e.Code;}finally{tun.Dispose();tun=null;}}
        if(systemProxy!=null){try{systemProxy.Stop();}catch(SafeError e){error=e.Code;}finally{systemProxy.Dispose();systemProxy=null;}}
        if (core != null) core.Exited -= OnCoreExit;
        if (transport != null) transport.Exited -= OnCoreExit;
        if (naive != null) naive.Exited -= OnCoreExit;
        if (slip != null) slip.Exited -= OnCoreExit;
        try { if (core != null && !core.HasExited) { EngineProcess.Terminate(core); core.WaitForExit(3000); } }
        finally {
            if (transport != null)
            {
                try { transport.StandardInput.Close(); if (!transport.WaitForExit(2000)) { EngineProcess.Terminate(transport); transport.WaitForExit(2000); } } catch { }
                transport.Dispose(); transport = null;
            }
            if (naive != null) { try { if (!naive.HasExited) { EngineProcess.Terminate(naive); naive.WaitForExit(2000); } } catch { } naive.Dispose(); naive = null; }
            if (slip != null) { try { if (!slip.HasExited) { EngineProcess.Terminate(slip); slip.WaitForExit(2000); } } catch { } slip.Dispose(); slip = null; }
            slipLogs?.Dispose(); slipLogs=null; ephemeral?.Dispose(); ephemeral = null;
            if(torDirectory!=null){try{string parent=OperatingSystem.IsMacOS()?PrivateStore.Root:Path.GetTempPath();if(Path.GetFullPath(torDirectory).StartsWith(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal))Directory.Delete(torDirectory,true);}catch{}torDirectory=null;}
            job?.Dispose(); job = null; core?.Dispose(); core = null; httpPort = socksPort = 0; verified = false; probeBudget = TimeSpan.FromSeconds(15); }
        if (error.Length > 0) throw new SafeError(error);
        return new() { State = "disconnected", ElapsedMs = clock.ElapsedMilliseconds, TrafficAvailable=lastTraffic!=null, UploadedBytes=lastTraffic?.Uploaded??0, DownloadedBytes=lastTraffic?.Downloaded??0 };
    }
    public void Dispose() { try { Stop(); } catch { } statsClient.Dispose(); }
}
