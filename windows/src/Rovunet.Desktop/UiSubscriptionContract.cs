using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    internal async Task<bool> ContractSmoke(string inputs,string report)
    {
        const string url="https://subscription.example.invalid/fixture.json";
        string source=Importer.Fingerprint(url);
        string store=Path.Combine(Path.GetDirectoryName(report)!,"temporary-store-"+Guid.NewGuid().ToString("N"));
        var checks=new Dictionary<string,bool>();
        try {
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            var seed=Importer.ReadCompatible(File.ReadAllText(Path.Combine(inputs,"old20.json")),true);
            CommitSubscription20(new Uri(url),seed.Profiles,"S1");
            var chosen=data.Profiles.First();
            string id=chosen.Id;
            data.SelectedId=id;data.LocalOverrideSources[id]=chosen.Raw;data.LocalOverrideNames[id]=chosen.Name;chosen.Name="Fixture override";Save();
            foreach (var (fixture,expected) in new[]{("unlimited",24),("unlimited.windows",24),("renewed",24),("protected",24),("duplicate",24),("empty",24)}) {
                var frozen=File.ReadAllBytes(Path.Combine(store,"profiles.dpapi"));
                string body=File.ReadAllText(Path.Combine(inputs,fixture+".json"));
                subscriptionFetchFixture28=(uri,stop,port)=>Task.FromResult(new SubscriptionClient.Payload(body,null,""));
                var menu=CollectionMenu(data.Profiles.Where(p=>p.SourceId==source).ToList());
                var update=menu.Items.OfType<MenuItem>().Single(i=>Equals(i.Tag,"UPDATE"));
                update.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));await Idle18();
                var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
                checks[fixture+"_count"]=saved.Profiles.Count==expected;
                checks[fixture+"_identity"]=saved.Subscriptions.Single().Url==url&&saved.Profiles.All(p=>p.SourceId==source)&&saved.SelectedId==id;
                checks[fixture+"_override"]=saved.Profiles.Any(p=>p.Id==id&&p.Name=="Fixture override");
                if(fixture is "protected" or "duplicate" or "empty")checks[fixture+"_preserved"]=frozen.SequenceEqual(File.ReadAllBytes(Path.Combine(store,"profiles.dpapi")));
            }
        }catch(Exception error){checks["exception_"+error.GetType().Name]=false;}
        finally{subscriptionFetchFixture28=null;storeSaveFixture28=null;}
        bool passed=checks.Count>0&&checks.Values.All(value=>value);
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,scope="WPF menu click, synthetic transport, real isolated current-user encrypted store"}));return passed;
    }
}
