using System.Text.Json;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed record SubscriptionPlan(string Basis,AppState Next,List<string> Removed);
    string SubscriptionBasis(){
        var basis=data.Copy();basis.Measurements.Clear();basis.RecentDiagnostics.Clear();
        return Importer.Fingerprint(JsonSerializer.Serialize(basis));
    }

    SubscriptionPlan PrepareSubscription(Uri uri,ImportReview review,string name,SubscriptionUsage20? usage,string provider,SubscriptionClient.Payload? status)
    {
        var next=SubscriptionMerge.Apply(data,uri,review.Profiles,name,usage,provider);
        StorePanelStatus(next,uri,status);
        string source=Importer.Fingerprint(uri.AbsoluteUri);
        var ids=next.Profiles.Where(p=>p.SourceId==source).Select(p=>p.Id).ToHashSet();
        var removed=data.Profiles.Where(p=>p.SourceId==source&&!ids.Contains(p.Id)).Select(p=>p.Name).ToList();
        return new(SubscriptionBasis(),next,removed);
    }

    async Task<bool> ApplySubscription(SubscriptionPlan plan,CancellationToken cancellation)
    {
        void Validate(){
            cancellation.ThrowIfCancellationRequested();
            if(plan.Basis!=SubscriptionBasis())throw new SafeError("MAC_IMPORT_REVIEW_REQUIRED");
        }
        Validate();
        if(plan.Removed.Count>0&&!await Confirm(
            string.Format(L.Text("MAC_REVIEW_REMOVALS"),plan.Removed.Count)+"\n"+string.Join("\n",plan.Removed.Take(12)),cancellation))return false;
        Validate();
        // Background measurements and diagnostic events may advance while a sheet is open.
        var survivors=plan.Next.Profiles.Select(p=>p.Id).ToHashSet();
        plan.Next.Measurements=data.Measurements.Where(pair=>survivors.Contains(pair.Key)).ToDictionary();
        plan.Next.RecentDiagnostics=data.RecentDiagnostics.ToList();
        Install(plan.Next);
        return true;
    }
}
