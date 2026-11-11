using System.Text;
using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;
using Rovunet.Desktop;
int checks=0,failures=0;var results=new List<object>();void Check(bool good,string name){checks++;results.Add(new{name,passed=good});if(!good){failures++;Console.WriteLine("FAIL "+name);}}
string valid="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&path=%2Fdemo#fixture";
var mixed=ImportReview.Read(valid+"\nanytls://fixture@server.example.invalid:443\n"+valid.Replace("#fixture","&unknown=1#fixture")+"\nmieru://fixture@server.example.invalid:443\n");
Check(mixed.Profiles.Count==2&&mixed.Profiles.Any(p=>p.Protocol=="AnyTLS")&&mixed.Rejected==2,"mixed-feed-retains-newly-supported-and-reports-rejections");
Check(mixed.Issues.Any(i=>i.Option=="mieru"),"unsupported-full-mieru-format-name-recorded");
var base64=ImportReview.Read(Convert.ToBase64String(Encoding.UTF8.GetBytes(valid+"\n# comment\n")));Check(base64.Profiles.Count==1&&base64.Rejected==0,"base64-subscription-decoded-once");
var empty=ImportReview.Read("mieru://fixture@server.example.invalid:443");Check(empty.Profiles.Count==0&&empty.Rejected==1,"all-unsupported-retains-reason-without-replacement");
bool protectedRejected=false;try{ImportReview.Read(valid+"\nrovunet-enc://fixture");}catch{protectedRejected=true;}Check(protectedRejected,"protected-item-keeps-atomic-rejection");
foreach(var bank in ResolverCatalog.Banks){var sample=ResolverCatalog.Candidates(bank.File,"tcp",64);Check(ResolverCatalog.Count(bank)>0&&sample.Count is >0 and <=64&&sample.All(v=>v.EndsWith(":53",StringComparison.Ordinal)),"offline-bank-"+bank.File);}
var live=new List<object>();if(args.Contains("--live"))foreach(var preset in PublicFeed.Presets){try{var payload=await PublicFeed.Fetch(preset.Url);var review=ImportReview.Read(payload.Body);var record=new{name=preset.Name,fetchedAt=DateTimeOffset.UtcNow,sha256=Importer.Fingerprint(payload.Body),bytes=Encoding.UTF8.GetByteCount(payload.Body),accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0,"live-public-feed-"+preset.Key);}catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name=preset.Name,error=code});Console.WriteLine(preset.Name+": "+code);Check(false,"live-public-feed-"+preset.Key);}}
int privateIndex=Array.IndexOf(args,"--private-body-dir");
if(privateIndex>=0)foreach(string file in Directory.GetFiles(args[privateIndex+1],"subscription-repro-*.data")){
    string name=Path.GetFileNameWithoutExtension(file);
    try{
        var review=ImportReview.Read(await File.ReadAllTextAsync(file));
        var record=new{name,accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};
        live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0,"owner-body-accepted-"+name);
    }catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name,error=code});Console.WriteLine(JsonSerializer.Serialize(new{name,error=code}));Check(false,"owner-body-accepted-"+name);}
}
int sourcesIndex=Array.IndexOf(args,"--sources-file");
if(sourcesIndex>=0){
    var addresses=JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[sourcesIndex+1]))!;
    for(int i=0;i<addresses.Length;i++){
        string name="owner-subscription-"+(i+1);
        try{
            var payload=await SubscriptionClient.Fetch(SubscriptionUrls.Validate(addresses[i]));var review=ImportReview.Read(payload.Body);
            var record=new{name,downloaded=true,accepted=review.Profiles.Count,rejected=review.Rejected,protocols=review.Profiles.GroupBy(p=>p.Protocol).ToDictionary(g=>g.Key,g=>g.Count()),issues=review.Issues};
            live.Add(record);Console.WriteLine(JsonSerializer.Serialize(record));Check(review.Profiles.Count>0&&review.Rejected==0,"live-owner-subscription-accepted-"+(i+1));
        }catch(Exception error){string code=error is SafeError safe?safe.Code:error.GetType().Name;live.Add(new{name,error=code});Console.WriteLine(JsonSerializer.Serialize(new{name,error=code}));Check(false,"live-owner-subscription-accepted-"+(i+1));}
    }
}
string output=args.FirstOrDefault(a=>!a.StartsWith("--"))??"artifacts";Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"feed-tests.json"),JsonSerializer.Serialize(new{host="Windows x64",macRuntimeValidated=false,checks,failures,results,live},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Feeds/banks: {checks} checks, {failures} failures");return failures==0?0:1;
