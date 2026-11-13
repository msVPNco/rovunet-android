using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rovunet.Core;

// Private subscription audit: URL enters through stdin; output contains no links, identities or option values.
if(args is ["--subscription-audit"])
{
    try{
        var uri=SubscriptionUrls.Validate(Console.In.ReadLine()??"");using var handler=new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(10)};using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(25)};
        using var response=http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();if(!response.IsSuccessStatusCode){Console.WriteLine(JsonSerializer.Serialize(new{error="FETCH_FAILED",status=(int)response.StatusCode}));return 2;}
        using var stream=response.Content.ReadAsStream();using var body=new MemoryStream();var buffer=new byte[8192];int read;while((read=stream.Read(buffer))>0){if(body.Length+read>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");body.Write(buffer,0,read);}string text=new UTF8Encoding(false,true).GetString(body.ToArray()).Trim();if(!text.Contains("://"))text=Importer.Decode(text);
        var rows=new List<object>();foreach(string line in text.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)){
            string protocol="other",error="";bool accepted=false;try{var profile=Importer.Single(line);accepted=profile.CanConnect;error=profile.BlockReason;}catch(SafeError e){error=e.Code;}
            var options=new Dictionary<string,string>();if(Uri.TryCreate(line,UriKind.Absolute,out var auditLink)){protocol=new[]{"vless","vmess","trojan","ss","socks5","hy2","hysteria2","rovunet","slipnet","wireguard"}.Contains(auditLink.Scheme)?auditLink.Scheme:"other";foreach(var part in auditLink.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){var pair=part.Split('=',2);string key=Uri.UnescapeDataString(pair[0]);if(System.Text.RegularExpressions.Regex.IsMatch(key,"^[A-Za-z][A-Za-z0-9_-]{0,39}$"))options[key]=pair.Length>1?Uri.UnescapeDataString(pair[1]):"";}}
            string Enum(string key,params string[] allowed)=>!options.TryGetValue(key,out var value)?"absent":allowed.Contains(value)?value:"other";
            rows.Add(new{row=rows.Count+1,protocol,accepted,error,keys=options.Keys.Order().ToArray(),transport=Enum("type","tcp","raw","ws","xhttp","splithttp","grpc","httpupgrade","h2","kcp","quic"),security=Enum("security","none","tls","reality"),flow=Enum("flow","","xtls-rprx-vision"),encryption=Enum("encryption","none"),header=Enum("headerType","none","http"),packet=Enum("packetEncoding","xudp","packetaddr")});
        }
        Console.WriteLine(JsonSerializer.Serialize(new{suite="private-subscription-redacted-audit",bytes=body.Length,rows}));return 0;
    }catch(SafeError error){Console.WriteLine(JsonSerializer.Serialize(new{error=error.Code}));return 2;}catch{Console.WriteLine("{\"error\":\"FETCH_FAILED\"}");return 2;}
}

if(args is ["--core-editor-runtime"])
{
    try{
        using var request=JsonDocument.Parse(Console.In.ReadToEnd());var root=request.RootElement;var original=Importer.Single(root.GetProperty("raw").GetString()!);var form=CoreProfileEditing.Read(original)??throw new SafeError("UNSUPPORTED_FORMAT");
        string endpoint=form.Kind=="wireguard"?form["endpoint"]:form["server"];if(!endpoint.Contains("example.invalid",StringComparison.Ordinal)&&!endpoint.StartsWith("127.0.0.1",StringComparison.Ordinal))throw new SafeError("INVALID_IMPORT");
        var edits=JsonSerializer.Deserialize<Dictionary<string,string>>(root.GetProperty("edits").GetRawText())!;var edited=CoreProfileEditing.Apply(original,"Synthetic editor fixture",edits).Profile;Console.Write(CoreLinks.Runtime(edited,21001,21002));return 0;
    }catch(SafeError){return 2;}
}

// Synthetic fixture adapter; credential-bearing fields travel only through redirected pipes.
if (args is ["--global-resolver-fixture"])
{
    try {
        var request = JsonSerializer.Deserialize<Dictionary<string,string>>(Console.In.ReadToEnd())!;
        var original = Importer.Single(ManualProfiles.Dns(request["family"], "Fixture", "t.example.invalid", "127.0.0.1:5353", "udp", new string('a',64), request.GetValueOrDefault("sshHost","127.0.0.1"), request.GetValueOrDefault("sshPort","22"), "fixture", request.GetValueOrDefault("password", "fixture")));
        original = ProfileEditing.Replace(original, ProfileEditing.ChangeNative(original, new Dictionary<int,string> { [4] = request["bad"] + ":0", [11] = request["key"], [22] = request["mode"], [21] = "https://original.invalid/dns-query" }));
        if (request.TryGetValue("edits", out var edits)) original = ProfileEditing.Replace(original, ProfileEditing.ChangeNative(original, JsonSerializer.Deserialize<Dictionary<int,string>>(edits)!));
        if (request.GetValueOrDefault("output") == "original") { Console.Write(original.Raw); return 0; }
        if (request.GetValueOrDefault("output") == "candidate") { Console.Write(JsonSerializer.Serialize(TransportPlan.Normalize(Importer.Fields(ResolverScan.Candidate(original,request["good"],request["mode"],true).Raw)))); return 0; }
        string raw = original.Raw;
        var plan = GlobalResolvers.ForConnection(original, true, request["good"]);
        if (plan.Notice.Length != 0 || raw != original.Raw) return 2;
        Console.Write(JsonSerializer.Serialize(TransportPlan.Normalize(Importer.Fields(plan.Effective.Raw)))); return 0;
    } catch (SafeError) { return 2; }
}
// Redacted audit of a pasted panel export. Never print imported text or account names.
if (args is ["--panel-audit"])
{
    try
    {
        var profiles = Importer.Read(Console.In.ReadToEnd());
        Console.WriteLine(JsonSerializer.Serialize(profiles.Select((p,i) => new { row=i+1, p.Protocol, eligible=p.CanConnect, reason=p.BlockReason, requires_certificate=TransportPlan.UsesSlip(p.Raw) })));
        return profiles.All(p => p.CanConnect) ? 0 : 1;
    }
    catch (SafeError e) { Console.WriteLine(e.Code); return 2; }
}
// Used only by the loopback fixture. Input and generated secrets travel through pipes.
if (args.Length == 3 && args[0] == "--runtime")
{
    var profile = Importer.Single(Console.In.ReadToEnd());
    Console.Write(CoreLinks.Runtime(profile, int.Parse(args[1]), int.Parse(args[2]))); return 0;
}
if (args.Length == 4 && args[0] == "--runtime-helper")
{
    var profile = Importer.Single(Console.In.ReadToEnd());
    Console.Write(CoreLinks.Runtime(profile, int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]))); return 0;
}
if (args.Length == 2 && args[0] == "--helper-config")
{
    var profile = Importer.Single(Console.In.ReadToEnd()); Console.Write(TransportPlan.Config(profile, int.Parse(args[1]), "")); return 0;
}
if (args.Length == 2 && args[0] == "--resources")
{
    var assembly = System.Reflection.Assembly.LoadFile(Path.GetFullPath(args[1]));
    var names = assembly.GetManifestResourceNames();
    var sets = new List<HashSet<string>>();
    foreach (var lang in new[] { "fa", "en", "ru", "zh-CN" })
    {
        string name = names.Single(n => n.EndsWith(".Locales." + lang + ".json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        var locale = JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        if (locale.Values.Any(string.IsNullOrWhiteSpace)) return 1;
        sets.Add(locale.Keys.ToHashSet());
    }
    if (sets.Skip(1).Any(s => !s.SetEquals(sets[0]))) return 1;
    Console.WriteLine(JsonSerializer.Serialize(new { embedded_locales = 4, keys_each = sets[0].Count, passed = true, ui_executed = false })); return 0;
}
if(args is ["--runtime-stats",var httpListener,var socksListener,var metricListener])
{
    Console.Write(CoreLinks.Runtime(Importer.Single(Console.In.ReadToEnd()),int.Parse(httpListener),int.Parse(socksListener),0,int.Parse(metricListener)));return 0;
}
if(args is ["--stats",var statsPort])
{
    using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false});var value=await XrayStats.Read(http,int.Parse(statsPort),CancellationToken.None);Console.WriteLine(JsonSerializer.Serialize(value));return 0;
}
if(args is ["--dns-probe",var dnsAddress,var dnsMode])
{
    var value=await DnsProbe.Run(dnsAddress,dnsMode,CancellationToken.None,700);Console.WriteLine(JsonSerializer.Serialize(value));return value.Replied?0:1;
}
if(args is ["--panel-compatibility-audit"])
{
    var text=Console.In.ReadToEnd();var normal=Importer.ReadCompatible(text);var core=Importer.ReadCompatible(text,false);
    Console.WriteLine(JsonSerializer.Serialize(new{parsed=normal.Total,normal_import=normal.Profiles.Count,core_only_import=core.Profiles.Count,core_only_skipped=core.Skipped,network_tested=false}));return 0;
}
var tests = new List<(string Name, Action Test)>();
void Test(string name, Action test) => tests.Add((name, test));
void Check(bool value) { if (!value) throw new Exception("ASSERTION_FAILED"); }
void Reject(Action test, string code)
{ try { test(); } catch (SafeError e) { Check(e.Code == code); return; } throw new Exception("EXPECTED_REJECTION"); }
const string id = "00000000-0000-4000-8000-000000000001";
string link = $"vless://{id}@example.invalid:443?type=ws&security=tls&host=ws.example.invalid&path=%2Ftest&sni=tls.example.invalid#Example";
string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
string[] Fields()
{
    var f = Enumerable.Repeat("", 88).ToArray(); f[0] = "33"; f[1] = "socks5"; f[2] = "Example"; f[3] = "127.0.0.1"; f[31] = f[33] = f[34] = f[36] = "0"; f[40] = "1080";
    f[85] = B64("group"); f[86] = B64("Group"); f[87] = B64(JsonSerializer.Serialize(new { sshUser = "u|سلام", sshPass = "p\n|", socksUser = "s|+", socksPass = "pw|\nسلام" })); return f;
}
string Native(string[] f) => "rovunet://" + B64(string.Join('|', f));
Test("vless-ws-distinct-host-sni", () => { var j = CoreLinks.Parse(link).Outbound; Check(j["streamSettings"]!["wsSettings"]!["headers"]!["Host"]!.GetValue<string>() == "ws.example.invalid"); Check(j["streamSettings"]!["tlsSettings"]!["serverName"]!.GetValue<string>() == "tls.example.invalid"); });
Test("no-direct-fallback-no-udp-claim", () => { var j = JsonNode.Parse(CoreLinks.Runtime(Importer.Single(link), 10001, 10002))!; Check(j["outbounds"]!.AsArray().Count == 1); Check(!j["inbounds"]![1]!["settings"]!["udp"]!.GetValue<bool>()); });
Test("duplicate-query-rejected", () => Reject(() => CoreLinks.Parse(link.Replace("#Example", "&type=tcp")), "INVALID_IMPORT"));
Test("insecure-tls-rejected", () => Reject(() => CoreLinks.Parse(link.Replace("#Example", "&allowInsecure=1")), "TLS_UNVERIFIED"));
Test("fragmentation-not-silently-lost", () => Reject(() => CoreLinks.Parse(link.Replace("#Example", "&fragment=tlshello")), "UNSUPPORTED_FORMAT"));
Test("empty-panel-fragment-accepted", () => Check(CoreLinks.Parse(link.Replace("#Example", "&fragment=")).Protocol == "VLESS WS"));
Test("unknown-transport-rejected", () => Reject(() => CoreLinks.Parse(link.Replace("type=ws", "type=quic")), "ENGINE_NOT_READY"));
Test("vision-on-ws-rejected", () => Reject(() => CoreLinks.Parse(link.Replace("#Example", "&flow=xtls-rprx-vision")), "UNSUPPORTED_FORMAT"));
Test("reality-key-required", () => Reject(() => CoreLinks.Parse(link.Replace("type=ws", "type=tcp").Replace("security=tls", "security=reality")), "INVALID_IMPORT"));
Test("xhttp-extra-not-ignored", () => Reject(() => CoreLinks.Parse(link.Replace("type=ws", "type=xhttp").Replace("#Example", "&extra=%7B%22DownloadSettings%22%3A%7B%7D%7D")), "XHTTP_EXTRA_NOT_READY"));
Test("xhttp-mode-kept", () => Check(CoreLinks.Parse(link.Replace("type=ws", "type=xhttp").Replace("#Example", "&mode=packet-up")).Outbound["streamSettings"]!["xhttpSettings"]!["mode"]!.GetValue<string>() == "packet-up"));
Test("socks-percent-credentials", () => Check(CoreLinks.Parse("socks5://u%3A%7C:p%2B%7C@127.0.0.1:1080").Outbound["settings"]!["servers"]![0]!["users"]![0]!["user"]!.GetValue<string>() == "u:|"));
Test("v33-lossless-credentials", () => { var p = Importer.Single(Native(Fields())); var j = Importer.Connection(p).Outbound; Check(j["settings"]!["servers"]![0]!["users"]![0]!["pass"]!.GetValue<string>() == "pw|\nسلام"); Check(p.CollectionId == "group"); });
foreach (int index in new[] { 31, 32, 33, 35, 36, 37 })
{ int field = index; Test("protected-field-" + field, () => { var f = Fields(); f[field] = field is 31 or 33 or 36 ? "1" : "restricted"; Reject(() => Importer.Single(Native(f)), "PROTECTED_PROFILE"); }); }
Test("encrypted-not-downgraded", () => Reject(() => Importer.Read("rovunet-enc://ZmFrZQ=="), "PROTECTED_PROFILE"));
Test("future-format-rejected", () => { var f = Fields(); f[0] = "34"; Reject(() => Importer.Read(Native(f)), "UNSUPPORTED_FORMAT"); });
Test("unexpected-native-fields-rejected", () => Reject(() => Importer.Read(Native(Fields().Concat(new[] { "security" }).ToArray())), "INVALID_IMPORT"));
Test("atomic-import", () => Reject(() => Importer.Read(link + "\nrovunet-enc://eA=="), "PROTECTED_PROFILE"));
Test("feed-unknown-key", () => Reject(() => Importer.Read(JsonSerializer.Serialize(new { version = 1, active = true, items = new[] { new { id = "a", link } } })), "UNSUPPORTED_FORMAT"));
Test("feed-duplicate-item-id", () => Reject(() => Importer.Read(JsonSerializer.Serialize(new { version = 1, items = new[] { new { id = "a", link }, new { id = "a", link } } })), "INVALID_IMPORT"));
Test("feed-shared-collection-name", () => { var list = Importer.Read(JsonSerializer.Serialize(new { version = 1, name = "Group", items = new[] { new { id = "a", link }, new { id = "b", link } } })); Check(list.Count == 2 && list.All(p => p.CollectionName == "Group")); });
Test("dns-tcp-choice-retained-unavailable", () => { var f = Fields(); f[1] = "soar"; f[81] = "tcp"; var raw = Native(f); var p = Importer.Single(raw); Check(!p.CanConnect && p.Raw == raw && Importer.Fields(p.Raw)[81] == "tcp"); });
Test("hy2-import-available-with-tls", () => { var p = Importer.Single("hy2://example@example.invalid:443?sni=tls.example.invalid&obfs=salamander&obfs-password=abc"); Check(p.CanConnect); var j = Importer.Connection(p).Outbound; Check(j["streamSettings"]!["tlsSettings"]!["allowInsecure"]!.GetValue<bool>() == false); Check(j["streamSettings"]!["finalmask"]!["udp"]![0]!["settings"]!["password"]!.GetValue<string>() == "abc"); });
Test("hy2-insecure-not-accepted", () => Reject(() => Importer.Single("hy2://example@example.invalid:443?insecure=1"), "TLS_UNVERIFIED"));
Test("hy2-port-hopping-not-silently-ignored", () => Reject(() => Importer.Single("hy2://example@example.invalid:443?mport=2000-3000"), "UNSUPPORTED_FORMAT"));
string wg = "[Interface]\nPrivateKey = " + B64(new string('a',32)) + "\nAddress = 10.1.0.2/32\nDNS = 1.1.1.1\n[Peer]\nPublicKey = " + B64(new string('b',32)) + "\nEndpoint = example.invalid:51820\nAllowedIPs = 0.0.0.0/0, ::/0\nPersistentKeepalive = 25";
Test("wireguard-userspace-allowedips-dns-kept", () => { var p = Importer.Single(wg); Check(p.CanConnect); var j = Importer.Connection(p).Outbound["settings"]!; Check(j["noKernelTun"]!.GetValue<bool>()); Check(j["peers"]![0]!["allowedIPs"]!.AsArray().Count == 2); Check(j["remoteDNS"]![0]!.GetValue<string>() == "1.1.1.1"); });
Test("wireguard-scripts-never-executed", () => Reject(() => Importer.Single(wg.Replace("[Peer]", "PostUp = run-command\n[Peer]")), "UNSUPPORTED_FORMAT"));
Test("wireguard-duplicate-peer-rejected", () => Reject(() => Importer.Single(wg + "\n[Peer]"), "INVALID_IMPORT"));
Test("wireguard-invalid-key-rejected", () => Reject(() => Importer.Single(wg.Replace(B64(new string('a',32)), "AAAA")), "INVALID_IMPORT"));
Test("xhttp-safe-extra-preserved", () => { var p = CoreLinks.Parse(link.Replace("type=ws","type=xhttp").Replace("#Example", "&extra=%7B%22xPaddingBytes%22%3A%22100-500%22%7D")); Check(p.Outbound["streamSettings"]!["xhttpSettings"]!["extra"]!["xPaddingBytes"]!.GetValue<string>() == "100-500"); });
Test("xhttp-extra-case-bypass-denied", () => Reject(() => CoreLinks.Parse(link.Replace("type=ws","type=xhttp").Replace("#Example", "&extra=%7B%22downloadsettings%22%3A%7B%7D%7D")), "XHTTP_EXTRA_NOT_READY"));
Test("ssh-helper-lossless-and-no-direct-config", () => { var f = Fields(); f[1]="ssh"; f[17]="22"; f[23]="password"; var p = Importer.Single(Native(f)); Check(p.CanConnect && TransportPlan.UsesHelper(p.Raw)); var j = JsonNode.Parse(TransportPlan.Config(p,10003,""))!; Check(j["fields"]![16]!.GetValue<string>() == "p\n|"); var r = JsonNode.Parse(CoreLinks.Runtime(p,10001,10002,10003))!; Check(r["outbounds"]!.AsArray().Count==1 && r["outbounds"]![0]!["settings"]!["servers"]![0]!["port"]!.GetValue<int>()==10003); });
Test("naive-credentials-stay-in-config", () => { var f=Fields(); f[1]="naive"; f[28]="443"; f[29]="u:"; f[30]=B64("p|سلام"); var p=Importer.Single(Native(f)); Check(p.CanConnect && TransportPlan.UsesNaive(p.Raw) && !TransportPlan.UsesHelper(p.Raw)); var c=JsonNode.Parse(TransportPlan.NaiveConfig(p,10003))!; var u=new Uri(c["proxy"]!.GetValue<string>()); Check(Uri.UnescapeDataString(u.UserInfo)=="u::p|سلام"); });
Test("naive-ssh-supervised-chain", () => { var f=Fields(); f[1]="naive_ssh"; f[17]="22"; f[23]="password"; f[28]="443"; f[29]="u"; f[30]=B64("p"); var p=Importer.Single(Native(f)); Check(p.CanConnect && TransportPlan.UsesHelper(p.Raw) && TransportPlan.UsesNaive(p.Raw)); });
Test("noiz-tcp-kept-without-scan-gate", () => { var f=Fields(); f[1]="sayedns"; f[3]="t.example.invalid"; f[4]="127.0.0.1:5300:0"; f[11]=new string('a',64); f[22]="tcp"; f[60]="fanout"; f[61]="3"; var p=Importer.Single(Native(f)); Check(p.CanConnect && TransportPlan.UsesHelper(p.Raw)); Check(JsonNode.Parse(TransportPlan.Config(p,10003,""))!["fields"]![22]!.GetValue<string>()=="tcp"); });
Test("vless-fragment-options-preserved", () => { var f=Fields(); f[1]="vless"; f[3]="example.invalid"; f[62]=id; f[63]="tls"; f[64]="ws"; f[65]="/a"; f[67]="443"; f[68]="1"; f[69]="sni_split"; f[70]="10"; f[72]="1"; f[73]="1"; f[74]="1"; f[77]="-1"; var p=Importer.Single(Native(f)); Check(p.CanConnect && TransportPlan.UsesHelper(p.Raw)); Reject(() => CoreLinks.Runtime(p,10001,10002), "ENGINE_NOT_READY"); var c=JsonNode.Parse(TransportPlan.Config(p,10003,""))!; Check(c["fields"]![72]!.GetValue<string>()=="1"); f[69]="fake"; Check(Importer.Single(Native(f)).BlockReason=="CUSTOM_TRANSPORT_NOT_READY"); f[69]="half";f[77]="100";Check(!Importer.Single(Native(f)).CanConnect); });
Test("panel-padding-defaults-preserve-explicit-values", () => { var f=Fields(); Check(TransportPlan.Normalize(f)[60]=="fanout" && TransportPlan.Normalize(f)[61]=="3");f[60]="roundrobin";f[61]="2";Check(TransportPlan.Normalize(f)[60]=="roundrobin" && TransportPlan.Normalize(f)[61]=="2"); });
Test("slipstream-alias-and-resolver-mode", () => { Check(TransportPlan.IsSlip("ss"));var r=TransportPlan.SlipResolver("[2001:db8::1]:5353:1");Check(r.Authoritative && r.Address=="[2001:db8::1]:5353"); });
Test("upgrade-refreshes-only-derived-capabilities", () => { var f=Fields();f[1]="ssh";f[17]="22";f[23]="password";var p=Importer.Single(Native(f));string raw=p.Raw;p.Id="saved-id";p.Name="Custom name";p.CollectionId="saved-group";p.SourceId="source";p.ItemId="item";p.BlockReason="ENGINE_NOT_READY";Check(Importer.RefreshCapabilities(p));Check(p.CanConnect && p.Id=="saved-id" && p.Name=="Custom name" && p.CollectionId=="saved-group" && p.SourceId=="source" && p.ItemId=="item" && p.Raw==raw);Check(!Importer.RefreshCapabilities(p)); });
Test("panel-report-imports-native-and-preserves-locks", () => {
    string header="[*] Configs for this user:\n";
    string raw=Native(Fields());
    Check(Importer.Read(header+raw+"\n# Standard link for other apps:\n"+link).Single().Raw==raw);
    Reject(()=>Importer.Read(header+raw+"\nrovunet-enc://eA=="),"PROTECTED_PROFILE");
});
Test("slip-timing-keeps-imported-value", () => {
    var f=Fields(); f[6]="5000"; Check(TransportPlan.EffectiveSlipInterval(f,null)==5000);
    Check(TransportPlan.EffectiveSlipInterval(f,400)==400 && f[6]=="5000");
    Reject(()=>TransportPlan.EffectiveSlipInterval(f,1),"INVALID_IMPORT");
});
Test("private-key-file-not-a-slip-certificate", () => Reject(()=>SlipCertificate.ReadPublic("-----BEGIN PRIVATE KEY-----"),"SLIP_CERT_INVALID"));
Test("safe-string-representation", () => Check(!Importer.Single(link).ToString().Contains(id)));
Test("status-url-exact-query-preserved", () => Check(SubscriptionUrls.Status(SubscriptionUrls.Validate("https://example.invalid/sub/abc.json?key=x"))!.AbsoluteUri == "https://example.invalid/sub/abc.status.json?key=x"));
Test("http-subscription-rejected", () => Reject(() => SubscriptionUrls.Validate("http://example.invalid/sub/a.json"), "HTTPS_REQUIRED"));
Test("revision-scoped-url-identity", () => Check(Importer.Fingerprint("https://e.invalid/sub/a.json") != Importer.Fingerprint("https://e.invalid/sub/b.json")));
var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
string Status(long? limit = 100, long? used = 50, string meter = "complete", string state = "active", long revision = 5, string? expiry = null, string? sampled = "now", string? generated = "now") => JsonSerializer.Serialize(new { version = 1, server_id = new string('a', 32), account_id = new string('b', 32), period_id = new string('c', 32), account_status = state, expires_at = expiry, limit_bytes = limit, used_bytes = used, metering_status = meter, sampled_at = sampled == "now" ? now.ToString("O") : sampled, generated_at = generated == "now" ? now.ToString("O") : generated, revision });
StatusView View(string body, double seconds = 0, double wallShift = 0, string error = "") => new StatusSnapshot { Status = AccountStatus.Read(body), ServerDate = now, ReceivedAt = now, Error = error }.Evaluate(now.AddSeconds(seconds + wallShift), TimeSpan.FromSeconds(seconds));
Test("quota-normal", () => Check(View(Status()).Severity == Severity.Normal));
Test("quota-20-warning", () => Check(View(Status(used: 80)).Severity == Severity.Warning));
Test("quota-5-critical", () => Check(View(Status(used: 95)).Severity == Severity.Critical));
Test("quota-over-limit-clamped", () => Check(View(Status(used: 120)).Status!.RemainingBytes == 0));
Test("3-days-warning", () => Check(View(Status(expiry: now.AddDays(3).ToString("O"))).Severity == Severity.Warning));
Test("24-hours-critical", () => Check(View(Status(expiry: now.AddDays(1).ToString("O"))).Severity == Severity.Critical));
Test("expired-state", () => Check(View(Status(state: "expired", sampled: null)).Severity == Severity.Critical));
Test("404-not-expiry", () => Check(View(Status(), error: "STATUS_NOT_FOUND").Severity == Severity.Unknown));
Test("partial-meter-unknown", () => Check(View(Status(meter: "partial")).Severity == Severity.Unknown));
Test("unavailable-meter-unknown", () => Check(View(Status(meter: "unavailable", sampled: null)).Severity == Severity.Unknown));
Test("stale-counter-unknown", () => Check(View(Status(sampled: now.AddMinutes(-16).ToString("O"))).Severity == Severity.Unknown));
Test("stale-receipt-unknown", () => Check(View(Status(), seconds: 901).Severity == Severity.Unknown));
Test("clock-jump-unknown", () => Check(View(Status(), seconds: 20, wallShift: 10).Severity == Severity.Unknown));
Test("unlimited-null-distinct", () => { var v = View(Status(limit: null, used: null, meter: "not_metered", sampled: null)); Check(v.Trustworthy && v.Status!.RemainingBytes == null); });
Test("null-not-zero", () => Reject(() => AccountStatus.Read(Status(limit: null, used: null)), "INVALID_STATUS"));
Test("negative-rejected", () => Reject(() => AccountStatus.Read(Status(used: -1)), "INVALID_STATUS"));
Test("decimal-rejected", () => Reject(() => AccountStatus.Read(Status().Replace("\"used_bytes\":50", "\"used_bytes\":1.5")), "INVALID_STATUS"));
Test("missing-field-rejected", () => { var j = JsonNode.Parse(Status())!.AsObject(); j.Remove("sampled_at"); Reject(() => AccountStatus.Read(j.ToJsonString()), "INVALID_STATUS"); });
Test("revision-rollback-rejected", () => Check(!AccountStatus.Read(Status(revision: 4)).Accepts(AccountStatus.Read(Status()))));
Test("same-revision-changed-counter-rejected", () => Check(!AccountStatus.Read(Status(used: 51)).Accepts(AccountStatus.Read(Status()))));
Test("same-revision-new-sample-allowed", () => Check(AccountStatus.Read(Status(sampled: now.AddSeconds(1).ToString("O"))).Accepts(AccountStatus.Read(Status()))));
Test("higher-revision-renewal-allowed", () => Check(AccountStatus.Read(Status(revision: 6).Replace(new string('c', 32), new string('d', 32))).Accepts(AccountStatus.Read(Status()))));
Test("proxy-restores-pac-and-autodetect", () => { var b = new Backend(); var before = b.Value; var store = new Store(); var lease = new ProxyLease(b, store); lease.Acquire(10001); Check(store.Journal != null && b.Value.Server.Contains("10001")); lease.Recover(); Check(b.Value == before && store.Journal == null); });
Test("proxy-failed-apply-rolls-back", () => { var b = new Backend { FailOnce = true }; var before = b.Value; var store = new Store(); try { new ProxyLease(b, store).Acquire(10001); } catch { } Check(b.Value == before && store.Journal == null); });
Test("proxy-external-change-never-clobbered", () => { var b = new Backend(); var store = new Store(); var lease = new ProxyLease(b, store); lease.Acquire(10001); b.Value = new(3, "external:8", "", ""); var external = b.Value; Reject(lease.Recover, "PROXY_RECOVERY_CONFLICT"); Check(b.Value == external && store.Journal != null); });
Test("proxy-journal-before-mutation", () => { var b = new Backend(); var store = new Store { FailWrite = true }; var before = b.Value; try { new ProxyLease(b, store).Acquire(10001); } catch { } Check(b.Value == before); });
Test("wire-roundtrip", () => { using var stream = new MemoryStream(); Wire.Write(stream, new AgentReply { State = "connected", HttpPort = 1234 }, default).GetAwaiter().GetResult(); stream.Position = 0; Check(Wire.Read<AgentReply>(stream, default).GetAwaiter().GetResult().HttpPort == 1234); });
Test("wire-size-bound", () => { byte[] h = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(h, Importer.MaxBytes + 1); using var stream = new MemoryStream(h); Reject(() => Wire.Read<AgentCommand>(stream, default).GetAwaiter().GetResult(), "MESSAGE_TOO_LARGE"); });

Test("window-bounds-small-workarea", () => { Check(Rovunet.Desktop.UiPolicy.WindowSize(790,728,740)==696); Check(Rovunet.Desktop.UiPolicy.WindowSize(1080,640,1100)==608); });
Test("window-corrupt-saved-geometry", () => { Check(Rovunet.Desktop.UiPolicy.WindowSize(double.NaN,768,740)==736); Check(Rovunet.Desktop.UiPolicy.WindowSize(double.PositiveInfinity,900,740)==740); });
Test("portrait-saved-narrow-width-is-preserved",()=>Check(Rovunet.Desktop.UiPolicy.WindowSize(420,1080,520)==420));
Test("slip-authoritative-editor-change-is-scoped-and-keeps-wire",()=>{var p=Importer.Single(FakeDns("slipstream"));var before=Importer.Decode(p.Raw[10..]).Split('|');var next=Importer.Decode(ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{5,"1"}})[10..]).Split('|');Check(next[5]=="1"&&before.Where((v,i)=>i!=5&&v!=next[i]).Count()==0);Reject(()=>ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{5,"2"}}),"INVALID_IMPORT");Reject(()=>ProfileEditing.ChangeNative(Importer.Single(FakeDns()),new Dictionary<int,string>{{5,"1"}}),"INVALID_IMPORT");});
Test("saved-measurement-time-and-age", () => { var now=DateTimeOffset.UtcNow; Check(Rovunet.Desktop.UiPolicy.MeasurementFresh(now.AddMinutes(-14),now)); Check(!Rovunet.Desktop.UiPolicy.MeasurementFresh(now.AddMinutes(-15),now)); Check(!Rovunet.Desktop.UiPolicy.MeasurementFresh(now.AddSeconds(1),now)); });
Test("test-budget-preserves-slow-slip", () => { Check(Rovunet.Desktop.UiPolicy.TestBudget(true,30)==TimeSpan.FromSeconds(90)); Check(Rovunet.Desktop.UiPolicy.TestBudget(false,30)==TimeSpan.FromSeconds(30)); });

Test("compatible-import-rejects-unsupported-keeps-working",()=>{var f=Fields();f[1]="future_engine";var batch=Importer.ReadCompatible(link+"\n"+Native(f));Check(batch.Total==2&&batch.Profiles.Count==1&&batch.Skipped["ENGINE_NOT_READY"]==1);});
Test("compatible-import-protected-is-still-atomic",()=>Reject(()=>Importer.ReadCompatible(link+"\nrovunet-enc://eA=="),"PROTECTED_PROFILE"));
Test("compatible-import-malformed-is-still-atomic",()=>Reject(()=>Importer.ReadCompatible(link+"\nvless://bad"),"INVALID_IMPORT"));
Test("all-adapters-by-default-core-only-on-request",()=>{var f=Fields();f[1]="dnstt";f[3]="t.example.invalid";f[4]="1.1.1.1:53:0";f[11]=new string('0',64);f[22]="tcp";var raw=Native(f);Check(Importer.ReadCompatible(raw,false).Profiles.Count==0&&Importer.ReadCompatible(raw).Profiles.Count==1);});
Test("native-resolver-edit-keeps-credentials-tcp-and-restrictions",()=>{var f=Fields();f[1]="dnstt";f[3]="t.example.invalid";f[4]="1.1.1.1:53:0";f[11]=new string('0',64);f[22]="tcp";var p=Importer.Single(Native(f));var raw=ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{4,"8.8.8.8:53:0"}});var wire=Importer.Decode(raw.Split("://")[1]).Split('|');Check(wire[87]==f[87]&&wire[34]=="0"&&wire[22]=="tcp"&&wire[4]=="8.8.8.8:53:0");});
Test("local-edit-preserves-subscription-identity",()=>{var p=Importer.Single(Native(Fields()));p.SourceId="source";p.ItemId="item";var result=ProfileEditing.Replace(p,ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{2,"Edited"}}));Check(result.Id==p.Id&&result.SourceId==p.SourceId&&result.ItemId==p.ItemId&&result.CollectionId==p.CollectionId&&result.Name=="Edited");});
Test("editing-cannot-promote-sharing",()=>{var f=Fields();var original=Importer.Single(Native(f));f[34]="1";Reject(()=>ProfileEditing.Replace(original,Native(f)),"SHARING_DISABLED");Reject(()=>ProfileEditing.Replace(original,link),"SHARING_DISABLED");});
Test("native-editor-cannot-change-security-fields",()=>Reject(()=>ProfileEditing.ChangeNative(Importer.Single(Native(Fields())),new Dictionary<int,string>{{34,"1"}}),"INVALID_IMPORT"));
Test("metrics-api-loopback-stats-only-no-direct-outbound",()=>{var j=JsonNode.Parse(CoreLinks.Runtime(Importer.Single(link),10001,10002,0,10003))!;Check(j["api"]!["listen"]!.GetValue<string>()=="127.0.0.1:10003"&&j["api"]!["services"]!.AsArray().Count==1&&j["api"]!["services"]![0]!.GetValue<string>()=="StatsService"&&j["outbounds"]!.AsArray().Count==1);});
byte[] StatsFrame(params (string Name,ulong Value)[] stats)
{
    var body=new List<byte>();void V(List<byte>b,ulong value){while(value>=128){b.Add((byte)((value&127)|128));value>>=7;}b.Add((byte)value);}
    foreach(var item in stats){var msg=new List<byte>{10};var name=Encoding.UTF8.GetBytes(item.Name);V(msg,(ulong)name.Length);msg.AddRange(name);msg.Add(16);V(msg,item.Value);body.Add(10);V(body,(ulong)msg.Count);body.AddRange(msg);}
    var frame=new byte[body.Count+5];BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1,4),body.Count);body.ToArray().CopyTo(frame,5);return frame;
}
const string statsPrefix="inbound>>>local-http>>>traffic>>>",socksStatsPrefix="inbound>>>local-socks>>>traffic>>>";
Test("proxy-journal-retains-installation-owner",()=>{var backend=new Backend();var store=new Store();var lease=new ProxyLease(backend,store,"fixture-directory");lease.Acquire(10001);Check(store.Journal!.OwnerDirectory=="fixture-directory");lease.Recover();Check(store.Journal==null);});
Test("proxy-legacy-journal-remains-readable",()=>{var old=new {Before=new ProxySettings(1,"","",""),Applied=new ProxySettings(3,"fixture","","")};var decoded=JsonSerializer.Deserialize<ProxyJournal>(JsonSerializer.Serialize(old))!;Check(decoded.OwnerDirectory==""&&decoded.Applied.Server=="fixture");});
Test("metrics-frame-parses-real-proto-wire",()=>{var value=XrayStats.Decode(StatsFrame((statsPrefix+"uplink",2048),(statsPrefix+"downlink",8192),(socksStatsPrefix+"uplink",1024),(socksStatsPrefix+"downlink",4096),("inbound>>>metrics>>>traffic>>>uplink",99999)));Check(value.Uploaded==3072&&value.Downloaded==12288);});
Test("metrics-missing-counter-does-not-invent-zero",()=>Reject(()=>XrayStats.Decode(StatsFrame((statsPrefix+"uplink",1))),"STATS_UNAVAILABLE"));
Test("metrics-negative-overflow-and-duplicate-rejected",()=>{Reject(()=>XrayStats.Decode(StatsFrame((statsPrefix+"uplink",ulong.MaxValue),(statsPrefix+"downlink",0))),"STATS_UNAVAILABLE");Reject(()=>XrayStats.Decode(StatsFrame((statsPrefix+"uplink",1),(statsPrefix+"uplink",2),(statsPrefix+"downlink",0))),"STATS_UNAVAILABLE");});
Test("metrics-aggregate-overflow-rejected",()=>Reject(()=>XrayStats.Decode(StatsFrame((statsPrefix+"uplink",long.MaxValue),(statsPrefix+"downlink",0),(socksStatsPrefix+"uplink",1),(socksStatsPrefix+"downlink",0))),"STATS_UNAVAILABLE"));
Test("metrics-truncated-frame-rejected",()=>{var frame=StatsFrame((statsPrefix+"uplink",1),(statsPrefix+"downlink",2));Reject(()=>XrayStats.Decode(frame[..^1]),"STATS_UNAVAILABLE");});
Test("traffic-rates-come-from-counter-deltas",()=>{var sampler=new TrafficSampler();long tick=System.Diagnostics.Stopwatch.GetTimestamp();Check(sampler.Sample(new(100,200),tick).UploadPerSecond==null);var rates=sampler.Sample(new(1124,2248),tick+System.Diagnostics.Stopwatch.Frequency);Check(rates.UploadPerSecond==1024&&rates.DownloadPerSecond==2048);Check(sampler.Sample(new(0,0),tick+2*System.Diagnostics.Stopwatch.Frequency).DownloadPerSecond==null);});
Test("qr-roundtrip-and-protected-gate",()=>{var writer=new ZXing.BarcodeWriterPixelData{Format=ZXing.BarcodeFormat.QR_CODE,Options=new ZXing.Common.EncodingOptions{Width=360,Height=360,Margin=4}};var qr=writer.Write("rovunet-enc://eA==");var decoded=Rovunet.Desktop.QrCodeReader.Read(qr.Pixels,qr.Width,qr.Height);Check(decoded=="rovunet-enc://eA==");Reject(()=>Importer.ReadCompatible(decoded),"PROTECTED_PROFILE");});
Test("qr-invalid-pixel-budget-rejected",()=>Reject(()=>Rovunet.Desktop.QrCodeReader.Read(new byte[4],100000,100000),"QR_TOO_LARGE"));
Test("dns-endpoints-keep-ipv6-and-explicit-port",()=>{Check(DnsProbe.Endpoint("[::1]:5353").Port==5353);Check(DnsProbe.Endpoint("1.1.1.1",853).Port==853);Reject(()=>DnsProbe.Endpoint("not-a-resolver"),"DNS_IP_REQUIRED");});
Test("manual-ssh-keeps-json-credential-envelope",()=>{var raw=ManualProfiles.Native("ssh","Fixture","example.invalid",22,"fixture","p|\nسلام");var fields=Importer.Fields(raw);Check(fields[15]=="fixture"&&fields[16]=="p|\nسلام"&&Importer.Single(raw).CanConnect);});
Test("manual-naive-credential-encoding",()=>{var raw=ManualProfiles.Native("naive","Fixture","example.invalid",443,"fixture","p|\nسلام");Check(Importer.Decode(Importer.Fields(raw)[30])=="p|\nسلام"&&Importer.Single(raw).CanConnect);});
string FakeDns(string family="dnstt",string mode="udp",string password="fixture") =>
    ManualProfiles.Dns(family,"Fixture","t.example.invalid","127.0.0.1:5353",mode,new string('a',64),"127.0.0.1","22","fixture",password);
Test("scanner-explicit-tcp-never-tries-udp",()=>{
    var p=Importer.Single(FakeDns("sayedns","tcp"));Check(ResolverScan.TransportChoices(Importer.Fields(p.Raw)).SequenceEqual(["tcp"]));
    Reject(()=>ResolverScan.Candidate(p,"127.0.0.2","udp",true),"CUSTOM_TRANSPORT_NOT_READY");
});
Test("scanner-soar-test-cannot-escape-through-discovery",()=>{
    var p=Importer.Single(FakeDns("soar"));
    p.Raw=ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{79,"1"},{82,"app"}});
    var test=Importer.Fields(ResolverScan.Candidate(p,"127.0.0.2","tcp",true).Raw);
    var saved=Importer.Fields(ResolverScan.Candidate(p,"127.0.0.2","tcp",false).Raw);
    Check(test[79]=="0" && test[81]=="only" && saved[79]=="1" && saved[81]=="only" && saved[82]=="app");
});
Test("scanner-manual-ten-dns-families-keep-secrets",()=>{
    foreach(string kind in new[]{"dnstt","sayedns","soar","vaydns","slipstream"})
    foreach(string suffix in new[]{"","_ssh"}){
        string raw=FakeDns(kind+suffix,"udp","p|\nسلام");var p=Importer.Single(raw);Check(p.CanConnect && ProfileEditing.IsDns(p));
        var f=Importer.Fields(raw);Check((suffix==""?f[13]:f[16])=="p|\nسلام" && f[4]=="127.0.0.1:5353:0");
    }
});
Test("scanner-private-edits-remain-bound-to-original-profile",()=>{
    var p=Importer.Single(FakeDns());p.SourceId="sub";p.ItemId="item";
    var edit=ResolverScan.Candidate(p,"[::1]:5053","tcp",false);var f=Importer.Fields(edit.Raw);
    Check(edit.Id==p.Id && edit.SourceId=="sub" && edit.ItemId=="item" && f[4]=="[::1]:5053:0" && f[22]=="tcp" && f[87]==Importer.Fields(p.Raw)[87]);
});
Test("scanner-vaydns-tcp-and-ssh-admitted",()=>Check(Importer.Single(FakeDns("vaydns_ssh","tcp")).CanConnect));
Test("scanner-cidr-union-no-duplicates",()=>{
    var addresses=ResolverScan.Catalog(["127.0.0.0/30","127.0.0.0/31"],"tcp",16,true);
    Check(addresses.Count==4 && addresses.Distinct().Count()==4 && addresses.Contains("127.0.0.3:53"));
    Check(ResolverScan.Catalog(["0.0.0.0/0"],"udp",128,true).Count==128);
});
Test("scanner-cidr-invalid-and-budget",()=>{
    Reject(()=>ResolverScan.Catalog(["10.1.2.3/24"],"udp",8,true),"INVALID_RANGE");
    Reject(()=>ResolverScan.FromText("127.0.0.1 127.0.0.2","udp",1),"DNS_SCAN_LIMIT");
    Check(ResolverScan.FromText("# comment\n1.1.1.1 1.1.1.1 # note","udp",2).Count==1);
});
Test("scanner-doh-url-never-demoted-to-udp",()=>{
    var p=Importer.Single(ManualProfiles.Dns("dnstt","Fixture","t.example.invalid","https://dns.example.invalid/dns-query","https",new string('b',64),"","","",""));
    Check(ResolverScan.TransportChoices(Importer.Fields(p.Raw)).SequenceEqual(["https"]));
    Reject(()=>ResolverScan.Candidate(p,"127.0.0.1","udp",true),"CUSTOM_TRANSPORT_NOT_READY");
});
Test("subscription-rotated-credentials-survive-local-resolver-edit",()=>{
    string from=FakeDns("dnstt_ssh");var old=Importer.Single(from);var edited=ResolverScan.Candidate(old,"127.0.0.2","udp",false);
    var incoming=Importer.Single(FakeDns("dnstt_ssh","udp","rotated"));
    var f=Importer.Fields(ProfileEditing.MergeSubscription(from,edited,incoming).Raw);Check(f[16]=="rotated" && f[4]=="127.0.0.2:53:0");
});
Test("subscription-conflicting-edits-are-atomic",()=>{
    string from=FakeDns();var p=Importer.Single(from);var mine=ResolverScan.Candidate(p,"127.0.0.2","udp",false);
    var other=ResolverScan.Candidate(p,"127.0.0.3","udp",false);
    Reject(()=>ProfileEditing.MergeSubscription(from,mine,other),"OVERRIDE_CONFLICT");
});
Test("same-user-host-regroup-does-not-cross-subscriptions",()=>{
    var p=Importer.Single(FakeDns("dnstt_ssh"));var q=Importer.Single(FakeDns("sayedns_ssh"));var sub=Importer.Single(FakeDns("soar_ssh"));sub.SourceId="fake-sub";
    Check(CollectionActions.SuggestedGroups([p,q,sub]).Single().Count==2);
});
Test("quick-tcp-does-not-probe-udp-protocols",()=>{
    Check(CollectionActions.TcpTarget(Importer.Single(FakeDns("sayedns")))==null);
    Check(CollectionActions.TcpTarget(Importer.Single(FakeDns("sayedns","tcp")))?.Port==5353);
});
Test("bundle-cross-format-aesgcm-vector",()=>{
    string encrypted="rovunet-bundle-enc://AQABAgMEBQYHCAkKCwwNDg8QERITFBUWFxgZGhtIXBvGdoMEVcdYdSlOqUCB3FnsB8T2YWDoHNljRnp/lacLF00jXYFQTTgOt1Rk+yU=";var r=BundleCodec.Open(encrypted,"رمز آزمایشی",true);
    Check(r.Profiles.Count==1 && r.Profiles[0].Raw=="socks5://127.0.0.1:1080#portable");
    Reject(()=>BundleCodec.Open(encrypted,"wrong",true),"BUNDLE_PASSWORD_ERROR");
});
Test("bundle-sharing-disabled-stays-disabled",()=>{
    var p=Importer.Single(FakeDns());var f=Importer.Decode(p.Raw[10..]).Split('|');f[34]="0";p=Importer.Single(Native(f));
    Reject(()=>BundleCodec.Seal([p],"fixture"),"SHARING_DISABLED");
});
Test("bundle-roundtrip-preserves-v33",()=>{
    var p=Importer.Single(FakeDns("sayedns_ssh","tcp","p|\nسلام"));var encrypted=BundleCodec.Seal([p],"رمز");
    var copy=BundleCodec.Open(encrypted,"رمز",true).Profiles.Single();Check(copy.Raw==p.Raw);
});
Test("sleep-off-and-corrupt-setting-do-not-arm",()=>{foreach(int minutes in new[]{0,-5,1,125}){var timer=new SleepTimerSession();timer.UpdateConnection(true,minutes);Check(!timer.Active);}});
Test("sleep-deadline-expires-once",()=>{var clock=new SleepClock();var timer=new SleepTimerSession(clock);timer.UpdateConnection(true,5);clock.Seconds=299;Check(timer.RemainingSeconds==1&&!timer.PollExpired());clock.Seconds=300;Check(timer.PollExpired()&&!timer.PollExpired()&&!timer.Active);});
Test("sleep-status-refresh-does-not-reset-or-apply-new-setting",()=>{var clock=new SleepClock();var timer=new SleepTimerSession(clock);timer.UpdateConnection(true,5);clock.Seconds=60;timer.UpdateConnection(true,120);Check(timer.RemainingSeconds==240);});
Test("sleep-session-cancel-does-not-change-next-connection",()=>{var timer=new SleepTimerSession();timer.UpdateConnection(true,5);timer.Cancel();timer.UpdateConnection(true,5);Check(!timer.Active);timer.UpdateConnection(false,5);timer.UpdateConnection(true,5);Check(timer.Active);});
Test("sleep-disconnect-invalidates-old-deadline",()=>{var clock=new SleepClock();var timer=new SleepTimerSession(clock);timer.UpdateConnection(true,5);timer.UpdateConnection(false,5);clock.Seconds=600;Check(!timer.PollExpired());timer.UpdateConnection(true,120);Check(timer.RemainingSeconds==7200);});
Test("sleep-wall-clock-change-does-not-alter-deadline",()=>{var clock=new SleepClock();var timer=new SleepTimerSession(clock);timer.UpdateConnection(true,5);clock.Utc=DateTimeOffset.UtcNow.AddYears(3);Check(timer.RemainingSeconds==300);clock.Seconds=300;Check(timer.PollExpired());});
Test("global-resolver-disabled-empty-and-nondns-preserve-reference",()=>{var p=Importer.Single(FakeDns());Check(ReferenceEquals(p,GlobalResolvers.ForConnection(p,false,"1.1.1.1").Effective));Check(ReferenceEquals(p,GlobalResolvers.ForConnection(p,true,"").Effective));var socks=Importer.Single("socks5://127.0.0.1:1080");Check(ReferenceEquals(socks,GlobalResolvers.ForConnection(socks,true,"invalid").Effective));});
Test("global-resolver-temporary-preserves-wire-security-and-identity",()=>{var p=Importer.Single(FakeDns("sayedns_ssh","tcp","p|\nسلام"));p.SourceId="fixture";string raw=p.Raw;var plan=GlobalResolvers.ForConnection(p,true,"1.1.1.1:5353\n[::1]:53");Check(plan.Notice==""&&p.Raw==raw&&plan.Effective.Id==p.Id&&plan.Effective.SourceId==p.SourceId);var a=Importer.Decode(raw[10..]).Split('|');var b=Importer.Decode(plan.Effective.Raw[10..]).Split('|');Check(a.Where((value,index)=>index!=4&&value!=b[index]).Count()==0);Check(b[22]=="tcp"&&b[4]=="1.1.1.1:5353:0,[::1]:53:0");});
Test("global-resolver-invalid-setting-does-not-block-manual-profile",()=>{var p=Importer.Single(FakeDns());var plan=GlobalResolvers.ForConnection(p,true,"not-an-ip");Check(ReferenceEquals(p,plan.Effective)&&plan.Notice=="GLOBAL_RESOLVER_NOT_APPLIED");});
Test("global-resolver-vaydns-multi-preserves-candidates",()=>{var p=Importer.Single(FakeDns("vaydns"));var plan=GlobalResolvers.ForConnection(p,true,"1.1.1.1,8.8.8.8");Check(!ReferenceEquals(p,plan.Effective)&&plan.Notice==""&&Importer.Fields(plan.Effective.Raw)[4].Contains(','));});
Test("global-resolver-ipv6-dedup-and-default-port",()=>{Check(GlobalResolvers.Normalize("1.1.1.1,1.1.1.1:53\n[::1]:5353")=="1.1.1.1:53\n[::1]:5353");});
Test("global-resolver-encrypted-transports-kept",()=>{foreach(var mode in new[]{"tls","https"}){var p=Importer.Single(FakeDns());p=ProfileEditing.Replace(p,ProfileEditing.ChangeNative(p,new Dictionary<int,string>{{22,mode},{21,"https://original.invalid/dns-query"}}));var plan=GlobalResolvers.ForConnection(p,true,mode=="tls"?"1.1.1.1:853":"https://resolver.invalid/dns-query");Check(plan.Notice==""&&Importer.Fields(plan.Effective.Raw)[22]==mode);Check(mode=="tls"?Importer.Fields(plan.Effective.Raw)[4]=="1.1.1.1:853:0":Importer.Fields(plan.Effective.Raw)[21]=="https://resolver.invalid/dns-query");}});
Test("dns-pool-selection-bounds-failures-and-ranks",()=>{int active=0,peak=0;var input=Enumerable.Range(1,12).Select(i=>"127.0.0."+i+":53").ToArray();var selected=DnsPool.Select(input,async(address,ct)=>{int n=Interlocked.Increment(ref active);int previous;do{previous=peak;}while(n>previous&&Interlocked.CompareExchange(ref peak,n,previous)!=previous);await Task.Delay(10,ct);Interlocked.Decrement(ref active);return new PoolProbe(address!=input[0],Array.IndexOf(input,address));},CancellationToken.None).GetAwaiter().GetResult();Check(peak<=4&&selected.Length==8&&selected[0]==input[1]);});
Test("dns-pool-cancel-and-empty-results",()=>{var result=DnsPool.Select(["127.0.0.1:53"],(_,_)=>Task.FromResult(new PoolProbe(false,0)),CancellationToken.None).GetAwaiter().GetResult();Check(result.Length==0);using var stop=new CancellationTokenSource();stop.Cancel();try{DnsPool.Select(["127.0.0.1:53"],(_,_)=>Task.FromResult(new PoolProbe(true,1)),stop.Token).GetAwaiter().GetResult();throw new Exception();}catch(OperationCanceledException){}});
Test("endpoint-uri-preserves-exact-query-and-credentials",()=>{var p=Importer.Single(link);var edited=EndpointEditing.Apply(p,"نام آزمایشی","new.example.invalid","8443");Check(edited==$"vless://{id}@new.example.invalid:8443?type=ws&security=tls&host=ws.example.invalid&path=%2Ftest&sni=tls.example.invalid#"+Uri.EscapeDataString("نام آزمایشی"));Check(p.Raw==link);});
Test("endpoint-socks-password-escaping-and-ipv6",()=>{var p=Importer.Single("socks5://user:p%40ss%3Aword@127.0.0.1:1080#Fixture");var edited=EndpointEditing.Apply(p,"Fixture","::1","1081");Check(edited=="socks5://user:p%40ss%3Aword@[::1]:1081#Fixture");Check(EndpointEditing.Read(Importer.Single(edited))!.Host=="::1");});
Test("endpoint-native-credentials-and-locks-unchanged",()=>{foreach(var kind in new[]{"ssh","naive"}){var p=Importer.Single(ManualProfiles.Native(kind,"Fixture","old.example.invalid",443,"fixture","p|\nسلام"));var before=Importer.Decode(p.Raw[10..]).Split('|');var after=Importer.Decode(EndpointEditing.Apply(p,"Renamed","new.example.invalid","8443")[10..]).Split('|');int host=kind=="ssh"?19:3,port=kind=="ssh"?17:28;Check(after[host]=="new.example.invalid"&&after[port]=="8443"&&after[2]=="Renamed");Check(before.Where((v,i)=>i!=2&&i!=host&&i!=port&&v!=after[i]).Count()==0);}});
Test("endpoint-native-socks-sharing-restriction-retained",()=>{var p=Importer.Single(Native(Fields()));var next=Importer.Single(EndpointEditing.Apply(p,"Renamed","new.example.invalid","1081"));Check(Importer.Fields(next.Raw)[34]=="0"&&Importer.Fields(next.Raw)[87]==Importer.Fields(p.Raw)[87]&&EndpointEditing.Read(next)!.Port==1081);});
Test("endpoint-native-vless-keeps-ws-host-and-sni",()=>{var f=Fields();f[1]="vless";f[3]="ws.example.invalid";f[62]=id;f[63]="tls";f[64]="ws";f[65]="/test";f[66]="old.example.invalid";f[67]="443";f[78]="sni.example.invalid";var p=Importer.Single(Native(f));Check(EndpointEditing.Read(p)!.Host=="old.example.invalid");var next=Importer.Fields(EndpointEditing.Apply(p,"Renamed","new.example.invalid","8443"));Check(next[3]==f[3]&&next[78]==f[78]&&next[66]=="new.example.invalid");});
Test("endpoint-invalid-input-does-not-mutate",()=>{var p=Importer.Single(link);foreach(var input in new[]{("bad|name","example.invalid","443"),("Fixture","bad host","443"),("Fixture","example.invalid","0"),("Fixture","example.invalid","65536")})Reject(()=>EndpointEditing.Apply(p,input.Item1,input.Item2,input.Item3),"INVALID_IMPORT");Check(p.Raw==link);});
Test("endpoint-metadata-preserved-through-replacement",()=>{var p=Importer.Single(link);p.SourceId="sub";p.ItemId="item";p.CollectionId="group";p.CollectionName="Group";var next=ProfileEditing.Replace(p,EndpointEditing.Apply(p,"Renamed","example.invalid","443"));Check(next.Id==p.Id&&next.SourceId==p.SourceId&&next.ItemId==p.ItemId&&next.CollectionId==p.CollectionId&&next.CollectionName==p.CollectionName);});
Test("endpoint-nested-xray-retains-outer-wire-and-restrictions",()=>{var f=Fields();f[1]="xray";f[84]=B64(link);var p=Importer.Single(Native(f));Check(EndpointEditing.Read(p)!.Host=="example.invalid");var next=Importer.Decode(EndpointEditing.Apply(p,"Renamed","new.example.invalid","8443")[10..]).Split('|');Check(next[34]=="0"&&next[87]==f[87]&&Importer.Decode(next[84]).Contains("@new.example.invalid:8443?type=ws&security=tls&host=ws.example.invalid"));Check(f.Where((v,i)=>i!=2&&i!=84&&next[i]!=v).Count()==0);});
Test("endpoint-default-ports-match-runtime",()=>{Check(EndpointEditing.Read(Importer.Single("socks5://127.0.0.1"))!.Port==1080);Check(EndpointEditing.Read(Importer.Single(link.Replace(":443?","?")))!.Port==443);});
Test("group-fast-direct-before-dns",()=>{var slow=Importer.Single("socks5://127.0.0.1:1001");var fast=Importer.Single("socks5://127.0.0.1:1002");var dns=Importer.Single(FakeDns("dnstt"));var plan=new CollectionProbePlan([dns,slow,fast],new Dictionary<string,long>{{slow.Id,100},{fast.Id,10}});Check(plan.Direct.SequenceEqual(new[]{fast,slow})&&plan.Dns.SequenceEqual(new[]{dns}));});
Test("group-dns-only-after-all-direct-fail",()=>{var plan=new CollectionProbePlan([Importer.Single(FakeDns("dnstt"))]);Check(plan.NeedsDns(false,0)&&!plan.NeedsDns(false,1)&&!plan.NeedsDns(true,0));});
Test("group-unmeasured-keeps-order-and-is-not-invented-fast",()=>{var a=Importer.Single("socks5://127.0.0.1:1001");var b=Importer.Single("socks5://127.0.0.1:1002");var c=Importer.Single("socks5://127.0.0.1:1003");var plan=new CollectionProbePlan([a,b,c],new Dictionary<string,long>{{c.Id,20}});Check(plan.Direct.SequenceEqual(new[]{c,a,b})&&!plan.NeedsDns(false,0));});
Test("scanner-multi-dns-preserves-flags-and-other-fields",()=>{var p=Importer.Single(FakeDns("dnstt","tcp"));var prior=Importer.Fields(p.Raw);var changed=ResolverScan.Selected(p,new[]{("127.0.0.1:5353","tcp"),("127.0.0.2:5353","tcp"),("127.0.0.3:5353","tcp")});var next=Importer.Fields(changed.Raw);Check(next[4].Split(',').Length==3&&next[22]=="tcp"&&next.Where((v,i)=>i!=4&&v!=prior[i]).Count()==0&&p.Raw==FakeDns("dnstt","tcp"));});
Test("scanner-multi-dns-deduplicates",()=>{var p=Importer.Single(FakeDns("dnstt","tcp"));var next=ResolverScan.Selected(p,new[]{("127.0.0.1:5353","tcp"),("127.0.0.1:5353","tcp")});Check(Importer.Fields(next.Raw)[4].Split(',').Length==1);});
Test("scanner-multi-dns-rejects-mixed-transport",()=>Reject(()=>ResolverScan.Selected(Importer.Single(FakeDns("dnstt")),new[]{("127.0.0.1:5353","udp"),("127.0.0.2:5353","tcp")}),"SCAN_SAME_TRANSPORT"));
Test("scanner-multi-dns-bounds",()=>{var p=Importer.Single(FakeDns("dnstt"));Reject(()=>ResolverScan.Selected(p,Array.Empty<(string,string)>()),"DNS_SCAN_LIMIT");Reject(()=>ResolverScan.Selected(p,Enumerable.Range(1,17).Select(i=>($"127.0.0.{i}:5353","udp"))),"DNS_SCAN_LIMIT");});
Test("scanner-multi-doh-does-not-invent-url-list",()=>Reject(()=>ResolverScan.Selected(Importer.Single(ManualProfiles.Dns("dnstt","DoH fixture","t.example.invalid","https://original.example.invalid/dns-query","https",new string('a',64),"127.0.0.1","22","fixture","fixture")),new[]{("https://one.example.invalid/dns-query","https"),("https://two.example.invalid/dns-query","https")}),"SCAN_DOH_SINGLE"));
Test("group-display-does-not-pin-imported-dns-above-direct",()=>{var dns=Importer.Single(FakeDns());var slow=Importer.Single("socks5://127.0.0.1:1001");var fast=Importer.Single("socks5://127.0.0.1:1002");Check(CollectionProbePlan.DisplayOrder([dns,slow,fast],new Dictionary<string,long>{{fast.Id,10},{slow.Id,30},{dns.Id,5}}).SequenceEqual(new[]{fast,slow,dns}));});
Test("group-display-dns-success-above-failed-direct",()=>{var dns=Importer.Single(FakeDns());var direct=Importer.Single("socks5://127.0.0.1:1001");Check(CollectionProbePlan.DisplayOrder([direct,dns],new Dictionary<string,long>{{dns.Id,20}}).SequenceEqual(new[]{dns,direct}));});
Test("group-display-waiting-direct-before-dns",()=>{var dns=Importer.Single(FakeDns());var direct=Importer.Single("socks5://127.0.0.1:1001");Check(CollectionProbePlan.DisplayOrder([dns,direct],new Dictionary<string,long>()).SequenceEqual(new[]{direct,dns}));});
Test("core-vless-unchanged-is-byte-exact",()=>{var p=Importer.Single(link);var f=CoreProfileEditing.Read(p)!;Check(CoreProfileEditing.Apply(p,p.Name,f.Fields).Profile.Raw==link);});
Test("core-vless-uuid-tls-ws-fields",()=>{var p=Importer.Single(link);var next=CoreProfileEditing.Apply(p,"Edited",new Dictionary<string,string>{{"auth","00000000-0000-4000-8000-000000000099"},{"server","new.example.invalid"},{"port","8443"},{"sni","tls.example.invalid"},{"path","/new"},{"host","http.example.invalid"}});var stream=Importer.Connection(next.Profile).Outbound["streamSettings"]!;Check(!next.CreatesCopy&&next.Profile.Raw.Contains("host=http.example.invalid")&&stream["wsSettings"]!["path"]!.GetValue<string>()=="/new"&&next.Profile.Id==p.Id&&p.Raw==link);});
Test("core-query-untouched-encoding-preserved",()=>{var raw=link.Replace("%2Ftest","%2ftest");var p=Importer.Single(raw);var next=CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"fp","firefox"}}).Profile;Check(next.Raw.Contains("path=%2ftest")&&next.Raw.Contains("fp=firefox"));});
Test("core-ws-conversion-creates-copy-and-original-survives",()=>{var p=Importer.Single(link);p.SourceId="source";p.ItemId="item";var next=CoreProfileEditing.Apply(p,"TCP copy",new Dictionary<string,string>{{"type","tcp"},{"security","tls"},{"flow","xtls-rprx-vision"}});Check(next.CreatesCopy&&next.Profile.Id!=p.Id&&next.Profile.SourceId==""&&next.Profile.CollectionId=="sub:source"&&p.Raw==link&&Importer.Connection(next.Profile).Outbound["streamSettings"]!["network"]!.GetValue<string>()=="raw");});
Test("core-reality-and-xhttp-settings",()=>{string key=Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+','-').Replace('/','_');var p=Importer.Single(link);var result=CoreProfileEditing.Apply(p,"XHTTP",new Dictionary<string,string>{{"type","xhttp"},{"security","reality"},{"pbk",key},{"sid","aabb"},{"spx","/probe"},{"mode","stream-up"},{"extra","{\"noSSEHeader\":true}"}});var stream=Importer.Connection(result.Profile).Outbound["streamSettings"]!;Check(stream["realitySettings"]!["shortId"]!.GetValue<string>()=="aabb"&&stream["xhttpSettings"]!["mode"]!.GetValue<string>()=="stream-up"&&stream["xhttpSettings"]!["extra"]!["noSSEHeader"]!.GetValue<bool>());});
Test("core-native-ws-keeps-credential-envelope-and-flags",()=>{var f=Fields();f[1]="vless";f[62]="00000000-0000-4000-8000-000000000001";f[63]="tls";f[64]="ws";f[65]="/fixture";f[66]="gateway.example.invalid";f[67]="443";f[68]="1";f[69]="micro";f[70]="300";f[77]="0";f[78]="tls.example.invalid";var p=Importer.Single(Native(f));var next=Importer.Decode(CoreProfileEditing.Apply(p,"Edited",new Dictionary<string,string>{{"auth","00000000-0000-4000-8000-000000000002"},{"path","/changed"}}).Profile.Raw[10..]).Split('|');Check(next[62].EndsWith("2")&&next[65]=="/changed"&&f.Where((v,i)=>i!=2&&i!=62&&i!=65&&next[i]!=v).Count()==0);});
Test("core-native-ws-conversion-keeps-sharing-restriction",()=>{var f=Fields();f[1]="vless";f[34]="0";f[62]="00000000-0000-4000-8000-000000000001";f[63]="tls";f[64]="ws";f[65]="/";f[66]="gateway.example.invalid";f[67]="443";f[68]="0";f[70]="0";f[77]="0";var p=Importer.Single(Native(f));var next=CoreProfileEditing.Apply(p,"Copy",new Dictionary<string,string>{{"type","xhttp"}});var wire=Importer.Decode(next.Profile.Raw[10..]).Split('|');Check(next.CreatesCopy&&wire[1]=="xray"&&wire[34]=="0"&&wire[87]==f[87]&&p.Raw==Native(f));});
Test("core-nested-xray-keeps-all-outer-wire-fields",()=>{var f=Fields();f[1]="xray";f[84]=B64(link);var p=Importer.Single(Native(f));var next=Importer.Decode(CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"server","new.example.invalid"}}).Profile.Raw[10..]).Split('|');Check(Importer.Decode(next[84]).Contains("@new.example.invalid:443")&&f.Where((v,i)=>i!=84&&next[i]!=v).Count()==0);});
Test("core-hysteria-auth-obfuscation-and-tls",()=>{var p=Importer.Single("hy2://fixture%3Apassword@gateway.example.invalid:443?sni=tls.example.invalid&insecure=0#H2");var next=CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"auth","new:+password"},{"obfs","salamander"},{"obfs-password","salt+value"}});var stream=Importer.Connection(next.Profile).Outbound["streamSettings"]!;Check(!next.CreatesCopy&&next.Profile.Raw.StartsWith("hy2://")&&stream["tlsSettings"]!["allowInsecure"]!.GetValue<bool>()==false&&stream["hysteriaSettings"]!["auth"]!.GetValue<string>()=="new:+password"&&stream["finalmask"]!["udp"]![0]!["settings"]!["password"]!.GetValue<string>()=="salt+value");});
Test("core-hysteria-remove-obfs-removes-password",()=>{var p=Importer.Single("hysteria2://fixture@gateway.example.invalid?obfs=salamander&obfs-password=fixture#H2");var next=CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"obfs",""}});Check(!next.Profile.Raw.Contains("obfs-password")&&Importer.Connection(next.Profile).Outbound["streamSettings"]!["finalmask"]==null);});
Test("core-wg-edits-and-metadata-name",()=>{string key=Convert.ToBase64String(new byte[32]);string raw=$"[Interface]\nPrivateKey = {key}\nAddress = 10.0.0.2/32\n\n[Peer]\nPublicKey = {key}\nEndpoint = gateway.example.invalid:51820\nAllowedIPs = 0.0.0.0/0\n";var p=Importer.Single(raw);p.Name="Custom WG";Check(CoreProfileEditing.Apply(p,p.Name,CoreProfileEditing.Read(p)!.Fields).Profile.Name=="Custom WG");var next=CoreProfileEditing.Apply(p,"Edited WG",new Dictionary<string,string>{{"address","10.0.0.3/32, fd00::2/128"},{"dns","1.1.1.1, 8.8.8.8"},{"mtu","1420"},{"persistentkeepalive","25"},{"presharedkey",key}});var settings=Importer.Connection(next.Profile).Outbound["settings"]!;Check(next.Profile.Name=="Edited WG"&&settings["address"]!.AsArray().Count==2&&settings["remoteDNS"]!.AsArray().Count==2&&settings["mtu"]!.GetValue<int>()==1420&&settings["peers"]![0]!["keepAlive"]!.GetValue<int>()==25&&p.Raw==raw.Trim());});
Test("core-wg-rejects-line-injection",()=>{string key=Convert.ToBase64String(new byte[32]);var p=Importer.Single($"[Interface]\nPrivateKey={key}\nAddress=10.0.0.2/32\n[Peer]\nPublicKey={key}\nEndpoint=gateway.example.invalid:51820\nAllowedIPs=0.0.0.0/0");Reject(()=>CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"dns","1.1.1.1\nPostUp=fixture"}}),"CORE_FIELD_INVALID");});
Test("core-validation-errors-do-not-mutate",()=>{var p=Importer.Single(link);Reject(()=>CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"port","0"}}),"CORE_PORT_INVALID");Reject(()=>CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"auth","invalid"}}),"CORE_UUID_INVALID");Reject(()=>CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"server","bad host"}}),"CORE_HOST_INVALID");Check(p.Raw==link);});
Test("core-secret-models-are-redacted",()=>{var p=Importer.Single(link);Check(CoreProfileEditing.Read(p)!.ToString()=="CoreProfileForm(redacted)"&&CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>()).ToString()=="CoreEditResult(redacted)");});
Test("core-native-conversion-keeps-effective-sni",()=>{var f=Fields();f[1]="vless";f[3]="tls.example.invalid";f[62]="00000000-0000-4000-8000-000000000001";f[63]="tls";f[64]="ws";f[65]="/";f[66]="127.0.0.1";f[67]="443";f[68]="0";f[70]="0";f[77]="0";f[78]="";var p=Importer.Single(Native(f));var next=CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"type","tcp"}}).Profile;Check(Importer.Connection(next).Outbound["streamSettings"]!["tlsSettings"]!["serverName"]!.GetValue<string>()=="tls.example.invalid");});
Test("core-wg-name-only-preserves-mixed-line-endings",()=>{string key=Convert.ToBase64String(new byte[32]);var p=Importer.Single($"[Interface]\r\nPrivateKey={key}\nAddress=10.0.0.2/32\r\n[Peer]\nPublicKey={key}\r\nEndpoint=gateway.example.invalid:51820\nAllowedIPs=0.0.0.0/0");Check(CoreProfileEditing.Apply(p,"Name only",new Dictionary<string,string>()).Profile.Raw==p.Raw);});
Test("core-wg-commented-peer-allows-new-interface-option",()=>{string key=Convert.ToBase64String(new byte[32]);var p=Importer.Single($"[Interface]\nPrivateKey={key}\nAddress=10.0.0.2/32\n[Peer] # retained comment\nPublicKey={key}\nEndpoint=gateway.example.invalid:51820\nAllowedIPs=0.0.0.0/0");var next=CoreProfileEditing.Apply(p,p.Name,new Dictionary<string,string>{{"dns","1.1.1.1"}}).Profile;Check(next.Raw.Contains("[Peer] # retained comment")&&Importer.Connection(next).Outbound["settings"]!["remoteDNS"]![0]!.GetValue<string>()=="1.1.1.1");});
Test("core-wg-subscription-retains-local-display-name",()=>{string key=Convert.ToBase64String(new byte[32]);var incoming=Importer.Single($"[Interface]\nPrivateKey={key}\nAddress=10.0.0.2/32\n[Peer]\nPublicKey={key}\nEndpoint=gateway.example.invalid:51820\nAllowedIPs=0.0.0.0/0");var local=ProfileEditing.Replace(incoming,incoming.Raw);local.Name="My WG";var merged=ProfileEditing.MergeSubscription(incoming.Raw,local,incoming,"WireGuard");Check(merged.Name=="My WG"&&merged.Raw==local.Raw);});
string PublicFixture(){string kind="ssh-ed25519";byte[] wire=new byte[4+kind.Length+4+32];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(wire,kind.Length);System.Text.Encoding.ASCII.GetBytes(kind).CopyTo(wire,4);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(wire.AsSpan(4+kind.Length),32);return Convert.ToBase64String(wire);}
Test("ssh-public-line-and-wire-key-same-pin",()=>{string pin=PublicFixture();Check(SshHostKey.Normalize("ssh-ed25519 "+pin+" fixture")==pin&&SshHostKey.Normalize(pin)==pin&&SshHostKey.Fingerprint(pin).StartsWith("SHA256:"));});
Test("ssh-pin-rejects-fingerprint-private-and-truncated",()=>{foreach(string text in new[]{"SHA256:fixture","-----BEGIN OPENSSH PRIVATE KEY-----",Convert.ToBase64String(new byte[10]),"ssh-rsa "+PublicFixture()})Reject(()=>SshHostKey.Normalize(text),"SSH_KEY_INVALID");});
Test("ssh-pin-rejects-trailing-wire-data",()=>Reject(()=>SshHostKey.Normalize(Convert.ToBase64String(Convert.FromBase64String(PublicFixture()).Concat(new byte[]{1}).ToArray())),"SSH_KEY_INVALID"));
Test("ssh-pin-scope-uses-real-endpoint",()=>{var p=Importer.Single(ManualProfiles.Native("ssh","Fixture","Gateway.Example.Invalid",22,"fixture","fixture"));Check(SshHostKey.Required(p)&&SshHostKey.Scope(p)=="gateway.example.invalid:22");var next=ProfileEditing.Replace(p,EndpointEditing.Apply(p,p.Name,"other.example.invalid","2222"));Check(SshHostKey.Scope(next)=="other.example.invalid:2222");});
Test("https-probe-alternatives-stay-https-and-deduplicate",()=>{var targets=HttpsProbePolicy.Targets("https://example.com/",true);Check(targets.Length==2&&targets[0]=="https://example.com/"&&targets.All(t=>new Uri(t).Scheme=="https"));Check(HttpsProbePolicy.Targets("https://example.com/",false).Length==1);});
Test("https-probe-rejects-credentials-and-plain-http",()=>{Reject(()=>HttpsProbePolicy.Targets("http://example.com/",true),"HTTPS_REQUIRED");Reject(()=>HttpsProbePolicy.Targets("https://fixture:fixture@example.com/",true),"HTTPS_REQUIRED");});
Test("probe-uncertain-does-not-weaken-changed-key-or-auth",()=>Check(HttpsProbePolicy.Unconfirmed("PROBE_TIMEOUT")&&HttpsProbePolicy.Unconfirmed("SSH_TRUST_REQUIRED")&&!HttpsProbePolicy.Unconfirmed("SSH_KEY_CHANGED")&&!HttpsProbePolicy.Unconfirmed("SSH_AUTH_FAILED")&&!HttpsProbePolicy.Unconfirmed("CORE_START_FAILED")));
foreach(var (name,test) in ExtraCases.All())Test(name,test);
foreach(var (name,test) in Review21Cases.All())Test(name,test);
foreach(var (name,test) in Maintenance28Cases.All())Test(name,test);
foreach(var (name,test) in Network30Cases.All())Test(name,test);
foreach(var (name,test) in Research15Cases.All())Test(name,test);
foreach(var (name,test) in Status21Cases.All())Test(name,test);
int failed = 0;
foreach (var (name, test) in tests)
{ try { test(); Console.WriteLine("PASS " + name); } catch { failed++; Console.WriteLine("FAIL " + name); } }
Console.WriteLine(JsonSerializer.Serialize(new { suite = "portable-domain", passed = tests.Count - failed, failed, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription }));
return failed == 0 ? 0 : 1;

sealed class Backend : IProxyBackend
{
    public ProxySettings Value = new(13, "original:8", "localhost;*.invalid", "https://pac.invalid/a"); public bool FailOnce;
    public ProxySettings Read() => Value;
    public void Write(ProxySettings value) { Value = value; if (FailOnce) { FailOnce = false; throw new SafeError("PROXY_WRITE_FAILED"); } }
}
sealed class Store : ILeaseStore
{
    public ProxyJournal? Journal; public bool FailWrite;
    public ProxyJournal? Read() => Journal;
    public void Write(ProxyJournal value) { if (FailWrite) throw new IOException(); Journal = value; }
    public void Delete() => Journal = null;
}

sealed class SleepClock : TimeProvider
{
    public long Seconds; public DateTimeOffset Utc=DateTimeOffset.UtcNow;
    public override long TimestampFrequency => 1;
    public override long GetTimestamp() => Seconds;
    public override DateTimeOffset GetUtcNow() => Utc;
}
