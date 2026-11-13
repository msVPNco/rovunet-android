using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Desktop;
using Rovunet.Platform;

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045)) return 10;
bool applyProxy = args.Contains("--system-proxy");
int reportIndex=Array.IndexOf(args,"--report");
if(reportIndex>=0 && (reportIndex+1>=args.Length || !Path.IsPathFullyQualified(args[reportIndex+1])))return 12;
string reportPath=reportIndex>=0?args[reportIndex+1]:Path.Combine(PrivateStore.Root,"windows-smoke-report.json");
if(reportIndex>=0 && File.Exists(reportPath))return 12;
// Explicit --system-proxy is required to touch the real user's proxy in this fixture.
// Certificate trust is local to this fixture's handler, never installed in Windows.
var report = new Dictionary<string, object?> { ["suite"] = "windows-local-smoke", ["utc"] = DateTimeOffset.UtcNow, ["system_proxy_tested"] = applyProxy, ["external_network_tested"] = false };
string stage = "setup";
int result = 1; Process? server = null; ProcessJob? job = null;
await using var client = new AgentClient(probeOnly: !applyProxy);
using var lifetime = new CancellationTokenSource();
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int targetPort = ((IPEndPoint)listener.LocalEndpoint).Port;
using var rsa = RSA.Create(2048);
var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Parse("10.77.0.1")); req.CertificateExtensions.Add(san.Build()); req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
using var generatedCertificate = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
// Schannel requires an imported key context for this self-signed Windows fixture.
using var cert = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.UserKeySet);
var target = Task.Run(async () =>
{
    try
    {
        using var socket = await listener.AcceptTcpClientAsync(lifetime.Token);
        using var tls = new SslStream(socket.GetStream());
        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = cert, EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 }, lifetime.Token);
        byte[] header = new byte[8192]; int count = 0;
        while (count < header.Length) { int n = await tls.ReadAsync(header.AsMemory(count, 1), lifetime.Token); if (n == 0) throw new IOException(); count += n; if (count >= 4 && Encoding.ASCII.GetString(header, count - 4, 4) == "\r\n\r\n") break; }
        var body = Encoding.UTF8.GetBytes("rovunet-windows-real-https-fixture");
        await tls.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"), lifetime.Token); await tls.WriteAsync(body, lifetime.Token);
    }
    catch (OperationCanceledException) { }
});
try
{
    foreach(var ipcCheck28 in await Ipc28Smoke.Run())report[ipcCheck28.Key]=ipcCheck28.Value;
    string transient;
    using (var secret = new EphemeralConfig("{\"fixture\":true}"))
    {
        transient = secret.Path; if (!File.Exists(transient)) throw new SafeError("PRIVATE_STORE_FAILED");
        var security = new FileInfo(transient).GetAccessControl();
        if (!security.AreAccessRulesProtected) throw new SafeError("PRIVATE_STORE_FAILED");
    }
    if (File.Exists(transient)) throw new SafeError("PRIVATE_STORE_FAILED");
    report["ephemeral_config_acl_and_delete"] = true;
    var before = new WinInetProxy().Read();
    int serverPort; var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); serverPort = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
    var config = JsonSerializer.Serialize(new { log = new { loglevel = "none" }, inbounds = new[] { new { listen = "127.0.0.1", port = serverPort, protocol = "vless", settings = new { clients = new[] { new { id = "00000000-0000-4000-8000-000000000001" } }, decryption = "none" }, streamSettings = new { network = "ws", security = "none", wsSettings = new { path = "/rovunet-fixture" } } } }, outbounds = new[] { new { protocol = "freedom", settings = new { redirect = "127.0.0.1:0", finalRules = new[] { new { action = "allow", ip = new[] { "127.0.0.1/32" } } } } } } });
    using (var engine = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "engines", "xray.exe"))) if (Convert.ToHexString(SHA256.HashData(engine)).ToLowerInvariant() != "d5b558191cccb1e92a5772b68b8cab67717b26f634b0f15153215f65ae982a7e") throw new SafeError("ENGINE_HASH_MISMATCH");
    job = new ProcessJob();
    server = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "engines", "xray.exe")) { UseShellExecute = false, CreateNoWindow = true, Arguments = "run -c stdin:", RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true }) ?? throw new SafeError("CORE_START_FAILED");
    job.Assign(server); server.BeginOutputReadLine(); server.BeginErrorReadLine(); await server.StandardInput.WriteAsync(config); server.StandardInput.Close();
    using var startDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
    while (true) { using var socket = new TcpClient(); try { await socket.ConnectAsync(IPAddress.Loopback, serverPort, startDeadline.Token); break; } catch (SocketException) { await Task.Delay(50, startDeadline.Token); } }
    await client.Start(); var reply = await client.Send(new() { Action = "connect", Profile = Importer.Single($"vless://00000000-0000-4000-8000-000000000001@127.0.0.1:{serverPort}?type=ws&security=none&path=%2Frovunet-fixture"), SystemProxy = applyProxy });
    if (reply.State != "connected") throw new SafeError(reply.Error);
    report["connect_ms"] = reply.ElapsedMs;
    var baselineProxy=new WinInetProxy().Read();
    await using(var inspector=new AgentClient(probeOnly:true)){
        await inspector.Start();
        var isolated=await inspector.Send(new(){Action="connect",Profile=Importer.Single($"vless://00000000-0000-4000-8000-000000000001@127.0.0.1:{serverPort}?type=ws&security=none&path=%2Frovunet-fixture"),SystemProxy=true});
        if(isolated.State!="connected" || new WinInetProxy().Read()!=baselineProxy)throw new SafeError("PROBE_ISOLATION_FAILED");
        var ended=await inspector.Send(new(){Action="disconnect"});
        if(ended.State!="disconnected" || new WinInetProxy().Read()!=baselineProxy)throw new SafeError("PROBE_ISOLATION_FAILED");
    }
    report["isolated_probe_preserved_proxy"]=true;
    using var handler = new SocketsHttpHandler { Proxy = new WebProxy($"http://127.0.0.1:{reply.HttpPort}"), UseProxy = true, AllowAutoRedirect = false };
    handler.SslOptions.CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
    handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(cert);
    // Domain in CONNECT is unreachable locally, and redirected only at the test server.

    // TLS verifies the fixture IP SAN against our ephemeral test CA.
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
    stage = "https";
    var text = await http.GetStringAsync($"https://10.77.0.1:{targetPort}/");
    if (text != "rovunet-windows-real-https-fixture") throw new SafeError("HTTPS_TEST_FAILED");
    report["https_verified"] = true;
    if (args.Contains("--sleep-timer") || args.Contains("--sleep-cancel"))
    {
        stage = "sleep-timer";
        // Only the fixture accelerates elapsed time: product minutes remain 0 or 5..120.
        var timer = new SleepTimerSession(new Rovunet.WindowsSmoke.AcceleratedFixtureClock());
        timer.UpdateConnection(true, 5);
        bool cancelled = args.Contains("--sleep-cancel");
        if (cancelled) timer.Cancel();
        var watch = Stopwatch.StartNew(); bool expired = false;
        while (watch.Elapsed < TimeSpan.FromSeconds(3))
        {
            if (timer.PollExpired()) { expired = true; break; }
            await Task.Delay(25);
        }
        if (expired == cancelled) throw new SafeError("SLEEP_TIMER_TEST_FAILED");
        var live = await client.Send(new() { Action = "status" });
        if (live.State != "connected") throw new SafeError("SLEEP_TIMER_TEST_FAILED");
        report["sleep_fixture_acceleration"] = 150;
        report["sleep_product_minutes"] = 5;
        report["sleep_elapsed_real_ms"] = watch.ElapsedMilliseconds;
        report["sleep_expired"] = expired;
        report["sleep_cancel_preserved_connection"] = cancelled;
        report["sleep_scope"] = "Core deadline + real Agent/HTTPS/cleanup; not the WPF busy-operation callback";
    }
    stage = "disconnect";
    var stop = await client.Send(new() { Action = "disconnect" }); if (stop.State != "disconnected" || stop.Error.Length != 0) throw new SafeError(stop.Error);
    report["disconnect_ms"] = stop.ElapsedMs;
    await client.DisposeAsync();
    report["proxy_restored"] = new WinInetProxy().Read() == before;
    if (report["proxy_restored"] is not true) throw new SafeError("PROXY_RECOVERY_FAILED");
    using var check = new TcpClient(); bool closed = false; try { await check.ConnectAsync(IPAddress.Loopback, reply.HttpPort); } catch (SocketException) { closed = true; }
    report["listener_closed"] = closed; if (!closed) throw new SafeError("CORE_EXITED"); result = 0;
}
catch (Exception e) { report["error_code"] = e is SafeError se ? se.Code : "SMOKE_FAILED"; report["failure_stage"] = stage; report["exception_type"] = e.GetType().Name; report["inner_exception_type"] = e.InnerException?.GetType().Name; if (e is HttpRequestException request) report["http_error"] = request.HttpRequestError.ToString(); }
finally
{
    await client.DisposeAsync(); lifetime.Cancel(); listener.Stop();
    try { await target; } catch { }
    try { if (server != null && !server.HasExited) { server.Kill(true); await server.WaitForExitAsync(); } } catch { }
    server?.Dispose(); job?.Dispose();
    report["passed"] = result == 0; var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    using(var output=new FileStream(reportPath,reportIndex>=0?FileMode.CreateNew:FileMode.Create,FileAccess.Write,FileShare.None))
    using(var writer=new StreamWriter(output))writer.Write(json);
    Console.WriteLine(json);
}
return result;

