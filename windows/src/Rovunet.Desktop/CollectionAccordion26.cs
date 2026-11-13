using System.Windows;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    void NormalizeCollections26()
    {
        var valid=data.Profiles.Select(CollectionKey).ToHashSet();
        string? open=data.CollectionExpansionRevision<1?data.ExpandedCollections.Where(p=>p.Value&&valid.Contains(p.Key)).Select(p=>p.Key).Order(StringComparer.Ordinal).FirstOrDefault():data.OpenCollectionId;
        if(open!=null&&!valid.Contains(open))open=null;
        data.OpenCollectionId=open;data.CollectionExpansionRevision=1;
        data.ExpandedCollections=data.ExpandedCollections.Keys.ToDictionary(k=>k,k=>open!=null&&k==open);
        if(open!=null)data.ExpandedCollections[open]=true;
    }
    void ToggleCollection26(string key)
    {
        string? before=data.OpenCollectionId;var old=data.ExpandedCollections;int oldRevision=data.CollectionExpansionRevision;
        data.OpenCollectionId=before==key?null:key;
        data.ExpandedCollections=new(old);NormalizeCollections26();
        try{Save();}catch{data.OpenCollectionId=before;data.ExpandedCollections=old;data.CollectionExpansionRevision=oldRevision;throw;}
        RefreshCards();
    }
    void RefreshCollectionLayout26(GroupView group,bool open)
    {
        if(group.Details26==null||group.ClosedStatus26==null||group.ClosedLine26==null)return;
        group.Details26.Visibility=open?Visibility.Visible:Visibility.Collapsed;
        string summary="";
        if(group.Quota?.Root.Visibility==Visibility.Visible)summary=string.Join(" · ",new[]{group.Quota.Used.Text,group.Quota.Time.Text,group.Quota.Note.Visibility==Visibility.Visible?group.Quota.Note.Text:""}.Where(t=>t.Length>0));
        bool known=group.Members[0].SourceId.Length>0&&statuses.GetValueOrDefault(group.Members[0].SourceId)?.Status!=null;
        if(!known&&group.Quota!=null){group.Quota.Root.Visibility=Visibility.Collapsed;summary=group.Members[0].SourceId.Length>0?L.Text("ACCOUNT_UNKNOWN"):"";}
        if(collectionRuns.TryGetValue(group.Key,out var run)&&(run.Running||run.Error.Length>0))summary=group.Summary.Text;
        else if(summary.Length==0&&group.Summary.Visibility==Visibility.Visible)summary=group.Summary.Text;
        group.ClosedStatus26.Text=summary;
        group.ClosedStatus26.Foreground=collectionRuns.TryGetValue(group.Key,out var job)&&job.Error.Length>0?Red:Muted;
        group.ClosedLine26.Visibility=(!open||!known)&&summary.Length>0?Visibility.Visible:Visibility.Collapsed;
    }
}

