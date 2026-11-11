using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunTrustTelemetrySmoke(string report)
    {
        var checks=new Dictionary<string,bool>();var proxyBefore=new WinInetProxy().Read();var outputDirectory=Path.GetDirectoryName(report)!;
        using var lifetime=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        string algorithm="ssh-ed25519";byte[] keyWire=new byte[4+algorithm.Length+4+32];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(keyWire,algorithm.Length);Encoding.ASCII.GetBytes(algorithm).CopyTo(keyWire,4);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(keyWire.AsSpan(4+algorithm.Length),32);RandomNumberGenerator.Fill(keyWire.AsSpan(8+algorithm.Length));string key=Convert.ToBase64String(keyWire),publicLine=algorithm+" "+key+" fixture";
        using var rsa=RSA.Create(2048);using var cert=new CertificateRequest("CN=t.example.invalid",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddDays(1));
        try{
            var scopeA=Path.Combine(outputDirectory,"ownership-a");var scopeB=Path.Combine(outputDirectory,"ownership-b");Directory.CreateDirectory(scopeA);Directory.CreateDirectory(scopeB);foreach(string directory in new[]{scopeA,scopeB})File.Copy(Path.Combine(AppContext.BaseDirectory,"Rovunet.Agent.exe"),Path.Combine(directory,"Rovunet.Agent.exe"));
            var settings=new ProxySettings(1,"","","");var owned=new ProxyJournal(settings,settings,scopeA);var legacy=new ProxyJournal(settings,settings);string Command(string directory)=>"\""+Path.Combine(directory,"Rovunet.Agent.exe")+"\" --recover";
            checks["uninstall_no_journal_needs_no_recovery"]=Recovery.UninstallScope(null,scopeA,"")==0;
            checks["uninstall_own_journal_requires_recovery"]=Recovery.UninstallScope(owned,scopeA,"")==10;
            checks["uninstall_other_installation_left_alone"]=Recovery.UninstallScope(owned,scopeB,"")==0;
            checks["uninstall_legacy_own_arm_requires_recovery"]=Recovery.UninstallScope(legacy,scopeA,Command(scopeA))==10;
            checks["uninstall_legacy_other_arm_left_alone"]=Recovery.UninstallScope(legacy,scopeB,Command(scopeA))==0;
            checks["uninstall_ambiguous_legacy_refuses"]=Recovery.UninstallScope(legacy,scopeA,"")==12;
            checks["uninstall_unknown_arm_arguments_refuse"]=Recovery.UninstallScope(legacy,scopeA,Command(scopeA)+" unexpected")==12;
            checks["uninstall_missing_owner_refuses"]=Recovery.UninstallScope(new(settings,settings,Path.Combine(outputDirectory,"missing-owner")),scopeA,"")==12;
            foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"light","dark"}){
                string id=language+"_"+theme;L.Language=language;data.Theme=theme;data.Profiles=[];data.SelectedId="";data.SlipCertificates.Clear();SetState(new());ApplyTheme();Build();await Idle();
                checks[id+"_first_run_power_visible_and_actionable"]=connect.IsEnabled&&Equals(connect.Background,Tone("AddSurface"))&&connect.ActualWidth>=44&&connect.IsVisible;
                SavePage((FrameworkElement)Content,Path.Combine(outputDirectory,"first-run-"+id+".png"));
                if(language=="en"&&theme=="light"){
                    Click(connect);await Idle();checks["first_power_click_opens_add_hub"]=addHub?.IsOpen==true&&Visuals<Button>(addHub.Child).Count()==4&&NavigationDepth==0;CloseAddHub();
                }
                var ssh=Importer.Single(ManualProfiles.Native("ssh","SSH fixture","gateway.example.invalid",22,"fixture","fixture"));data.Profiles=[ssh];data.SelectedId=ssh.Id;BuildCards();
                var page=BuildEndpointEditor(ssh);page.Show();await Idle();var field=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"SSH_HOST_PUBLIC_KEY"));field.Text=publicLine;await Idle();
                checks[id+"_public_key_preview_no_early_save"]=!data.SshKeys.ContainsKey(ssh.Id)&&Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text==SshHostKey.Fingerprint(key))&&Window.GetWindow(field)==this;
                SavePage(page.PageVisual,Path.Combine(outputDirectory,"ssh-identity-"+id+".png"));field.Text="SHA256:invalid";Click(Find(page,"SAVE"));
                checks[id+"_invalid_pin_keeps_editor_and_original"]=page.IsVisible&&!data.SshKeys.ContainsKey(ssh.Id)&&data.Profiles[0].Raw==ssh.Raw;
                field.Text=publicLine;Click(Find(page,"SAVE"));checks[id+"_public_key_saved_for_connection"]=!page.IsVisible&&StoredSshKey(data.Profiles[0])==key&&data.SshKeyScopes[ssh.Id]==SshHostKey.Scope(ssh);
                var next=BuildEndpointEditor(data.Profiles[0]);next.Show();await Idle();Visuals<TextBox>(next.PageVisual).Single(t=>Equals(t.Tag,"SSH_HOST_PUBLIC_KEY")).Text="";next.Close();checks[id+"_back_discards_pin_edit"]=StoredSshKey(data.Profiles[0])==key;
                var prior=data.Profiles[0];SaveEdited(prior,EndpointEditing.Apply(prior,prior.Name,"other.example.invalid","22"));checks[id+"_host_change_clears_old_identity"]=StoredSshKey(data.Profiles[0]).Length==0&&!data.SshKeys.ContainsKey(prior.Id);
                var tlsWire=Importer.Decode(ssh.Raw[(ssh.Raw.IndexOf("://",StringComparison.Ordinal)+3)..]).Split('|');tlsWire[50]="1";tlsWire[51]="t.example.invalid";var tlsProfile=Importer.Single("rovunet://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|',tlsWire))));data.Profiles=[tlsProfile];data.SshTlsCertificates.Clear();BuildCards();var tlsPage=BuildEndpointEditor(tlsProfile);tlsPage.Show();await Idle();var tlsPem=Visuals<TextBox>(tlsPage.PageVisual).Single(t=>Equals(t.Tag,"SSH_TLS_CA_PEM"));tlsPem.Text=cert.ExportCertificatePem();checks[id+"_tls_certificate_draft_not_saved"]=StoredSshTlsCa(tlsProfile).Length==0&&Visuals<TextBox>(tlsPage.PageVisual).Any(t=>Equals(t.Tag,"SSH_TLS_SNI"));Click(Find(tlsPage,"SAVE"));checks[id+"_tls_public_certificate_saved"]=!tlsPage.IsVisible&&StoredSshTlsCa(data.Profiles[0]).Contains("BEGIN CERTIFICATE");
                var slip=Importer.Single(ManualProfiles.Dns("slipstream_ssh","Slip fixture","t.example.invalid","127.0.0.1:53","udp","","gateway.example.invalid","22","fixture","fixture"));data.Profiles=[slip];BuildCards();var slipPage=BuildDnsProfileEditor(slip);slipPage.Show();await Idle();var pem=Visuals<TextBox>(slipPage.PageVisual).Single(t=>Equals(t.Tag,"SLIP_CERT_PEM"));pem.Text=cert.ExportCertificatePem();Visuals<TextBox>(slipPage.PageVisual).Single(t=>Equals(t.Tag,"SSH_HOST_PUBLIC_KEY")).Text=publicLine;await Idle();slipPage.RestoreScroll(300);await Idle();SavePage(slipPage.PageVisual,Path.Combine(outputDirectory,"slip-identities-"+id+".png"));
                checks[id+"_slip_certificate_and_ssh_visible"]=pem.IsVisible&&!data.SlipCertificates.ContainsKey(SlipCertificate.Scope(slip));Click(Find(slipPage,"SAVE"));checks[id+"_slip_certificate_and_ssh_saved"]=!slipPage.IsVisible&&StoredSlipPin(slip).Contains("BEGIN CERTIFICATE")&&StoredSshKey(slip)==key;
            }
            checks["fresh_default_periodic_latency_enabled"]=new UserData().LiveLatency;
            var settingsPage=Dialog("SETTINGS");settingsPage.Content=BuildSettingsPanel(()=>{});settingsPage.Show();await Idle();var liveChoice=Visuals<CheckBox>(settingsPage.PageVisual).Single(c=>Equals(c.Tag,"LIVE_LATENCY"));liveChoice.IsChecked=true;checks["automatic_latency_setting_enables_saved_choice"]=data.LiveLatency;liveChoice.IsChecked=false;checks["automatic_latency_setting_disables_saved_choice"]=!data.LiveLatency;var fallbackChoice=Visuals<CheckBox>(settingsPage.PageVisual).Single(c=>Equals(c.Tag,"PROBE_FALLBACKS"));fallbackChoice.IsChecked=false;checks["alternative_probe_can_be_disabled"]=!data.ProbeFallbacks;fallbackChoice.IsChecked=true;checks["alternative_probe_choice_saved"]=data.ProbeFallbacks;settingsPage.Close();
            L.Language="en";data.Theme="light";ApplyTheme();data.LiveLatency=false;
            await CounterNetwork();
            var afterCounter=new WinInetProxy().Read();checks["proxy_unchanged_after_counter"]=proxyBefore==afterCounter;
            await using(var naiveAgent=new AgentClient(probeOnly:true)){
                await naiveAgent.Start();var naive=Importer.Single(ManualProfiles.Native("naive","Naive startup fixture","gateway.example.invalid",443,"fixture","fixture"));var ready=await naiveAgent.Send(new(){Action="connect",Profile=naive,SystemProxy=false});
                checks["pinned_windows_naive_listener_starts"]=ready.State=="connected";if(!checks["pinned_windows_naive_listener_starts"])checks["naive_failure_"+ready.Error]=false;
            }
            string transientPath;using(var transient=new EphemeralConfig("{\"fixture\":true}")){transientPath=transient.Path;checks["transient_owner_file_readable_during_startup"]=File.ReadAllText(transientPath).Contains("fixture");}checks["transient_file_removed_after_dispose"]=!File.Exists(transientPath);
            var afterProxy=new WinInetProxy().Read();checks["proxy_unchanged"]=proxyBefore==afterProxy;
            checks["proxy_server_unchanged"]=proxyBefore.Server==afterProxy.Server;checks["proxy_bypass_unchanged"]=proxyBefore.Bypass==afterProxy.Bypass;checks["proxy_pac_unchanged"]=proxyBefore.AutoConfigUrl==afterProxy.AutoConfigUrl;checks["proxy_flags_unchanged"]=proxyBefore.Flags==afterProxy.Flags;
        }catch(Exception e){checks["failure_"+e.GetType().Name+(e is SafeError s?"_"+s.Code:"")]=false;}
        finally{foreach(var page in DialogWindow.OpenPages.ToArray().Reverse())page.Close();if(client!=null){client.Abort();await client.DisposeAsync();client=null;}networkFixtureMode=false;lastError="";activeProfile=null;SetState(new());Show();}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="First run power, SSH/Slip identity editing, real background Xray counters and native Windows Naive startup",synthetic_profiles=true,loopback_network_tested=true,external_servers_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        static Button Find(DialogWindow p,string key)=>Visuals<Button>(p.PageVisual).Single(b=>AutomationProperties.GetName(b)==L.Text(key));
        async Task CounterNetwork(){
            using var relay=new TcpListener(IPAddress.Loopback,0);relay.Start();int relayPort=((IPEndPoint)relay.LocalEndpoint).Port;
            _=Task.Run(async()=>{try{while(!lifetime.IsCancellationRequested){var socket=await relay.AcceptTcpClientAsync(lifetime.Token);_=Task.Run(async()=>{using(socket)try{var stream=socket.GetStream();var head=new byte[2];await stream.ReadExactlyAsync(head,lifetime.Token);await stream.ReadExactlyAsync(new byte[head[1]],lifetime.Token);await stream.WriteAsync(new byte[]{5,0},lifetime.Token);var req=new byte[4];await stream.ReadExactlyAsync(req,lifetime.Token);if(req[1]!=1||req[3]!=1)return;var endpoint=new byte[6];await stream.ReadExactlyAsync(endpoint,lifetime.Token);if(!IPAddress.IsLoopback(new IPAddress(endpoint.AsSpan(0,4))))return;await stream.WriteAsync(new byte[]{5,0,0,1,127,0,0,1,0,1},lifetime.Token);
                // Serve a deterministic HTTP body through the real Xray SOCKS outbound.
                int crlf=0;while(crlf<4){var one=new byte[1];await stream.ReadExactlyAsync(one,lifetime.Token);crlf=(crlf,one[0])switch{(0,13)=>1,(1,10)=>2,(2,13)=>3,(3,10)=>4,(_,13)=>1,_=>0};}var body=new byte[32768];RandomNumberGenerator.Fill(body);await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 32768\r\nConnection: close\r\n\r\n"),lifetime.Token);await stream.WriteAsync(body,lifetime.Token);
            }catch{}});}}catch{}},lifetime.Token);
            var p=Importer.Single("socks5://127.0.0.1:"+relayPort+"#Counter%20fixture");data.Profiles=[p];data.SelectedId=p.Id;Build();ResetTelemetry();networkFixtureMode=true;client=new AgentClient(probeOnly:true);await client.Start();var ready=await client.Send(new(){Action="connect",Profile=p,SystemProxy=false});activeProfile=p;SetState(ready);checks["counter_actual_agent_connected"]=ready.State=="connected";
            using var http=new HttpClient(new SocketsHttpHandler{UseProxy=true,Proxy=new WebProxy("http://127.0.0.1:"+ready.HttpPort)}){Timeout=TimeSpan.FromSeconds(10)};
            Hide();checks["counter_timer_runs_in_tray"]=durationTimer.IsEnabled&&!IsVisible;durationTimer.Stop();long start=0;var bytes=await http.GetByteArrayAsync("http://127.0.0.1:12345/fixture",lifetime.Token);await Task.Delay(250,lifetime.Token);await PollTelemetry();checks["counter_first_sample_includes_immediate_page_load"]=rates.DownloadPerSecond>0;durationTimer.Start();
            checks["counter_hidden_real_body_counted"]=bytes.Length==32768&&lastTraffic!=null&&lastTraffic.Downloaded-start>=32768;
            checks["counter_hidden_real_rate_nonzero"]=graphSamples.Any(value=>value>0);long total=lastTraffic!.Downloaded;await Task.Delay(1200,lifetime.Token);await PollTelemetry();checks["counter_idle_no_fabricated_bytes"]=lastTraffic.Downloaded==total;
            Show();await PollTelemetry();checks["counter_restore_keeps_total"]=lastTraffic.Downloaded==total;var stopped=await client.Send(new(){Action="disconnect"});long recorded=data.RecordedDownload;RecordTraffic(stopped);checks["counter_recorded_total_keeps_last_session"]=data.RecordedDownload-recorded>=32768&&lastTraffic==null;client.Abort();await client.DisposeAsync();client=null;activeProfile=null;SetState(new());
        }
    }
}

