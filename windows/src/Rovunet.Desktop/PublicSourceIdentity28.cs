using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    Func<Uri,CancellationToken,int,Task<SubscriptionClient.Payload>>? subscriptionFetchFixture28;
    Action? storeSaveFixture28;
    static PublicFeed.Preset? ManagedSource28(Subscription sub)=>PublicFeed.Presets.FirstOrDefault(p=>p.Key==sub.PublicSource)??PublicFeed.Resolve(sub.Url);
    static string SubscriptionUrl28(Subscription sub)=>ManagedSource28(sub)?.Url??PublicFeed.CanonicalUrl(sub.Url);
    Task<SubscriptionClient.Payload> FetchPublic28(string url,CancellationToken stop=default,int port=0)=>fixtureMode&&subscriptionFetchFixture28!=null?PublicFeed.FetchUsing28(url,subscriptionFetchFixture28,stop,port):PublicFeed.Fetch(url,stop,port);
}
