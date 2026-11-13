using Avalonia.Controls;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static string ImportReviewSummary(ImportReview review)=>string.Format(L.Text("MAC_IMPORT_SUMMARY"),review.Profiles.Count,review.Rejected)+(review.TlsAdjustedCount>0?"\n"+string.Format(L.Text("MAC_TLS_IMPORT_NORMALIZED"),review.TlsAdjustedCount):"")+string.Concat(review.Issues.Select(i=>"\n"+i.Count+" × "+L.Text(i.Code)+(i.Option.Length>0?" · "+i.Option:"")));
    static void RecordImportReview(ImportReview review){SafeDiagnostics.Record("import-review","REVIEWED");foreach(var issue in review.Issues)SafeDiagnostics.Record("import-review",issue.Code,issue.Option);}
    void RenderImportReview(Panel panel,ImportReview review,bool persistDiagnostics=true)
    {
        panel.Children.Clear();RecordImportReview(review);if(persistDiagnostics)RememberDiagnostics();panel.Children.Add(Label(string.Format(L.Text("MAC_IMPORT_SUMMARY"),review.Profiles.Count,review.Rejected),17));
        if(review.TlsAdjustedCount>0)panel.Children.Add(Label(string.Format(L.Text("MAC_TLS_IMPORT_NORMALIZED"),review.TlsAdjustedCount),13,"Accent"));
        foreach(string note in review.Notices)panel.Children.Add(Label(L.Text(note),12,"Muted"));
        foreach(var issue in review.Issues)panel.Children.Add(Label(issue.Count+" × "+L.Text(issue.Code)+(issue.Option.Length>0?" · "+issue.Option:""),12,"Red"));
        foreach(var p in review.Profiles.Take(20))panel.Children.Add(new OverflowMarquee(Label(p.Name+" · "+p.Protocol,14),24));
    }
}
