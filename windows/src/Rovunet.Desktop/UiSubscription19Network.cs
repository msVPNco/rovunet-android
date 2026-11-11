using System.Diagnostics;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunSubscription19Network(string report,string privateUrl)
    {
        var checks=new Dictionary<string,bool>();var results=new List<object>();var before=new WinInetProxy().Read();int imported=0,verified=0,rows=0;var ownedBefore=Owned();
        try{
            var input=privateUrl.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);bool sample=input.Length>1&&input[1]=="sample";var uri=SubscriptionUrls.Validate(input[0]);var payload=await SubscriptionClient.Fetch(uri);var batch=Importer.ReadCompatible(payload.Body,true);imported=batch.Profiles.Count;checks["all_subscription_rows_imported"]=batch.Total>0&&imported==batch.Total;
            string Shape(Profile p){var o=Importer.Connection(p).Outbound;if(o["protocol"]!.GetValue<string>()!="vless")return p.Protocol;var cipher=o["settings"]!["vnext"]![0]!["users"]![0]!["encryption"]!.GetValue<string>();return p.Protocol+"|"+o["streamSettings"]!["security"]+"|"+string.Join('.',cipher.Split('.').Take(3));}
            var selected=sample?batch.Profiles.Select((p,i)=>new{Profile=p,Row=i+1}).GroupBy(p=>Shape(p.Profile)).SelectMany(g=>{var a=g.ToArray();return new[]{a[0].Row,a[a.Length/2].Row,a[^1].Row};}).ToHashSet():Enumerable.Range(1,imported).ToHashSet();
            foreach(var profile in batch.Profiles){
                int row=++rows;string engine=Path.Combine(AppContext.BaseDirectory,"engines","xray.exe");using var configTest=Process.Start(new ProcessStartInfo(engine){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-test","-c","stdin:"}})!;var stdout=configTest.StandardOutput.BaseStream.CopyToAsync(Stream.Null);var stderr=configTest.StandardError.BaseStream.CopyToAsync(Stream.Null);await configTest.StandardInput.WriteAsync(CoreLinks.Runtime(profile,21001,21002));configTest.StandardInput.Close();await configTest.WaitForExitAsync();await Task.WhenAll(stdout,stderr);checks["row_"+row+"_pinned_engine_accepts_configuration"]=configTest.ExitCode==0;
                if(!selected.Contains(row))continue;
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(35));await using var agent=new AgentClient(probeOnly:true);using var abort=deadline.Token.Register(agent.Abort);string error="",stage="";bool ready=false,success=false;long elapsed=0;
                try{await agent.Start(deadline.Token);var connected=await agent.Send(new(){Action="connect",Profile=profile,SystemProxy=false});ready=connected.State=="connected";error=connected.Error;if(ready){var reply=await agent.Send(new(){Action="probe",ProbeUrl="https://example.com/",ProbeFallbacks=false});success=reply.Verified;error=reply.Error;stage=reply.ProbeStage;elapsed=reply.ElapsedMs;if(success)verified++;await agent.Send(new(){Action="disconnect"});}}
                catch(Exception e){error=e is SafeError safe?safe.Code:deadline.IsCancellationRequested?"TEST_TIMEOUT":"TRANSPORT_START_FAILED";}
                finally{agent.Abort();}
                results.Add(new{row,protocol=profile.Protocol,imported=true,ready,httpsVerified=success,error,stage,elapsedMs=elapsed});
            }
            checks["all_requested_rows_probed"]=rows==imported&&results.Count==selected.Count;checks["windows_proxy_unchanged"]=before==new WinInetProxy().Read();await Task.Delay(250);checks["no_fixture_process_leak"]=Owned().IsSubsetOf(ownedBefore);
        }catch(Exception error){checks[error is SafeError safe?safe.Code:"PRIVATE_AUDIT_FAILED"]=false;}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Owner-authorized private subscription: redacted import, pinned engine and actual HTTPS probes",url_saved_in_report=false,owner_profiles_changed=false,imported,networkTested=results.Count,httpsVerified=verified,results,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
        static HashSet<int> Owned()=>new[]{"Rovunet.Agent","xray","rovunet-transports"}.SelectMany(Process.GetProcessesByName).Select(p=>{using(p)return p.Id;}).ToHashSet();
    }
}
