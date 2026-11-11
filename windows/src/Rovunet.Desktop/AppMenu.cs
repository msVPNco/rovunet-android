using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    List<Profile> SelectedGroup() => Selected is { } p?data.Profiles.Where(x=>x.SourceId==p.SourceId && (p.SourceId!="" || x.CollectionId==p.CollectionId)).ToList():[];
    void OpenAppMenu()
    {
        GeneralPage();
    }
    StackPanel BuildAppMenu(Action close)
    {
        return GeneralMenu(close);
    }
    static string MenuIcon(string key) => key switch {
        "HOME"=>"M2,11 L12,2 L22,11 M5,10 V22 H10 V15 H14 V22 H19 V10",
        "EDIT"=>"M2,18 L16,4 L20,8 L6,22 H2 Z M14,6 L18,10",
        "FOLDER"=>"M2,5 H10 L13,8 H22 V21 H2 Z",
        "MANAGE_COLLECTIONS"=>"M2,7 L12,1 L22,7 L12,13 Z M2,12 L12,18 L22,12 M2,17 L12,23 L22,17",
        "BACKUP_EXPORT"=>"M12,16 V2 M6,8 L12,2 L18,8 M3,15 V21 H21 V15",
        "PUBLIC_SOURCES"=>"M3,8 H21 V22 H3 Z M1,5 H23 V10 H1 Z M12,5 V22 M12,5 C2,-3 3,9 12,5 C21,-3 22,9 12,5",
        "SEARCH"=>"M10,2 A8,8 0 1 0 10,18 A8,8 0 1 0 10,2 M16,16 L23,23",
        "SETTINGS"=>"M4,6 H20 M4,12 H20 M4,18 H20 M8,3 V9 M16,9 V15 M10,15 V21",
        "LANGUAGE"=>"M3,4 H21 M12,3 C5,8 5,16 12,21 C19,16 19,8 12,3 M3,12 H21",
        "HELP"=>"M9,7 C9,1 18,1 18,7 C18,11 12,10 12,15 M12,18 V20",
        "ABOUT"=>"M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M12,10 V18 M12,6 V7",
        "IMPORT"=>"M12,2 V16 M6,10 L12,16 L18,10 M3,20 H21",
        "EXPORT_ALL" or "EXPORT_ENCRYPTED"=>"M12,16 V2 M6,8 L12,2 L18,8 M3,15 V21 H21 V15",
        "SUBSCRIPTIONS"=>"M3,3 H21 V21 H3 Z M7,8 H17 M7,12 H17 M7,16 H13",
        "CONNECTION_DETAILS" or "TCP_CHECK"=>"M4,8 V12 C4,20 20,20 20,12 V8 M8,2 V8 M16,2 V8 M3,8 H21 M12,18 V23",
        "TEST_COLLECTION" or "DIAGNOSTICS"=>"M3,20 H21 M6,17 V10 M12,17 V3 M18,17 V7",
        "SORT_LATENCY" or "SORT_RELIABILITY"=>"M5,3 V21 M1,17 L5,21 L9,17 M12,5 H22 M12,11 H19 M12,17 H16",
        "CLEAR_TESTS"=>"M4,5 H20 M8,2 H16 M6,5 V22 H18 V5 M10,9 V18 M14,9 V18",
        "REGROUP"=>"M3,3 H10 V10 H3 Z M14,3 H21 V10 H14 Z M3,14 H10 V21 H3 Z M14,14 H21 V21 H14 Z",
        "UNDO_GROUP"=>"M8,3 L2,9 L8,15 M2,9 H15 C24,9 24,21 15,21",
        "DUPLICATES"=>"M8,8 H21 V21 H8 Z M3,16 V3 H16",
        "EXIT"=>"M4,3 H13 V21 H4 Z M10,12 H22 M17,7 L22,12 L17,17",
        _=>"M4,4 H20 V20 H4 Z M8,8 H16 M8,12 H16 M8,16 H13"
    };
    void ConnectionDetails()
    {
        var dialog=Dialog("CONNECTION_DETAILS",500,780);
        dialog.Content=connectionControls;dialog.Closed+=(_,_)=>dialog.Content=new Border();dialog.ShowDialog();
    }
    void SortGroup(List<Profile> group,bool reliability)
    {
        if(group.Count>0)collectionRuns.Remove(CollectionKey(group[0]));
        bool Fresh(Rovunet.Platform.PathMeasurement m)=>UiPolicy.MeasurementFresh(m.SampledAt,DateTimeOffset.UtcNow);
        var measured=group.Where(p=>data.Measurements.TryGetValue(p.Id,out var m)&&m.Verified&&Fresh(m)&&LatestRound(p,m)).ToDictionary(p=>p.Id,p=>data.Measurements[p.Id].ElapsedMs);
        var order=reliability?group.OrderByDescending(p=>data.Measurements.TryGetValue(p.Id,out var m)&&Fresh(m)&&LatestRound(p,m)&&m.Attempts>0?(double)m.Passes/m.Attempts:0).ThenBy(p=>measured.GetValueOrDefault(p.Id,long.MaxValue)).ToArray():CollectionProbePlan.DisplayOrder(group,measured);
        var ids=group.Select(p=>p.Id).ToHashSet();int i=0;var old=data.Profiles;
        data.Profiles=old.Select(p=>ids.Contains(p.Id)?order[i++]:p).ToList();try{Save();}catch{data.Profiles=old;throw;}BuildCards();
    }
    void ClearTests(List<Profile> group)
    {
        if(group.Count>0)collectionRuns.Remove(CollectionKey(group[0]));
        var old=data.Measurements;var ids=group.Select(p=>p.Id).ToHashSet();data.Measurements=old.Where(x=>!ids.Contains(x.Key)).ToDictionary();
        try{Save();}catch{data.Measurements=old;throw;}foreach(string id in ids)measurements.Remove(id);BuildCards();
    }
    void TcpCollection(List<Profile> group) => StartCollectionTest(group,1,true);
    void RemoveDuplicates()
    {
        var redundant=data.Profiles.Where(p=>p.SourceId.Length==0 && p.CanConnect).GroupBy(p=>(p.CollectionId,Key:CollectionActions.DuplicateKey(p)))
            .SelectMany(g=>g.OrderByDescending(p=>p.Id==data.SelectedId || data.SshKeys.ContainsKey(p.Id) || data.LocalOverrideSources.ContainsKey(p.Id)).Skip(1))
            .Where(p=>p.Id!=data.SelectedId && !data.SshKeys.ContainsKey(p.Id) && !data.LocalOverrideSources.ContainsKey(p.Id)).ToHashSet();
        if(redundant.Count==0){Info("DUPLICATES",L.Text("NO_MATCHES"));return;}
        if(!Confirm(L.Text("DELETE_CONFIRM")+"\n"+string.Join("\n",redundant.Select(p=>p.Name))))return;
        var old=data.Profiles;data.Profiles=old.Where(p=>!redundant.Contains(p)).ToList();try{Save();}catch{data.Profiles=old;throw;}BuildCards();
    }
    void Regroup()
    {
        var plan=CollectionActions.SuggestedGroups(data.Profiles).ToList();
        if(plan.Count==0){Info("REGROUP",L.Text("NO_MATCHES"));return;}
        if(!Confirm(L.Text("REGROUP_HINT")+"\n\n"+string.Join("\n\n",plan.Select(g=>string.Join(" · ",g.Select(p=>p.Name))))))return;
        var backup=data.RegroupUndo;data.RegroupUndo=[];
        foreach(var group in plan)
        {
            string target="group:"+Guid.NewGuid().ToString("N");string name=group[0].CollectionName.Length>0?group[0].CollectionName:group[0].Name;
            foreach(var p in group){data.RegroupUndo[p.Id]=new(){Previous=p.CollectionId,Name=p.CollectionName,Group=target};p.CollectionId=target;p.CollectionName=name;}
        }
        try{Save();}catch{foreach(var p in data.Profiles)if(data.RegroupUndo.TryGetValue(p.Id,out var edit)){p.CollectionId=edit.Previous;p.CollectionName=edit.Name;}data.RegroupUndo=backup;throw;}BuildCards();
    }
    void UndoGroups()
    {
        var undo=data.RegroupUndo;var current=new Dictionary<Profile,(string CollectionId,string Name)>();
        foreach(var p in data.Profiles)if(undo.TryGetValue(p.Id,out var edit) && p.CollectionId==edit.Group){current[p]=(p.CollectionId,p.CollectionName);p.CollectionId=edit.Previous;p.CollectionName=edit.Name;}
        data.RegroupUndo=[];try{Save();}catch{data.RegroupUndo=undo;foreach(var (p,old) in current){p.CollectionId=old.CollectionId;p.CollectionName=old.Name;}throw;}BuildCards();
    }
    string? BundlePassword(Window owner,bool exporting)
    {
        var d=new DialogWindow(owner,L.Text("EXPORT_ENCRYPTED"),480,320);var box=new StackPanel{Margin=new(18)};
        d.UsePageChrome();var pw=new PasswordBox{MinHeight=48};box.Children.Add(OutlinedField("PASSWORD",pw));
        var confirm=new PasswordBox{MinHeight=48};
        if(exporting)box.Children.Add(OutlinedField("CONFIRM_PASSWORD",confirm));
        var error=Text("",12,Red);box.Children.Add(error);
        box.Children.Add(Button(exporting?"EXPORT_ENCRYPTED":"IMPORT",()=>{if(pw.Password.Length==0 || exporting&&pw.Password!=confirm.Password){error.Text=L.Text("BUNDLE_PASSWORD_ERROR");return;}d.DialogResult=true;},true));
        d.Content=box;d.Loaded+=(_,_)=>pw.Focus();return d.ShowDialog()==true?pw.Password:null;
    }
    async void ExportBundle()
    {
        try{
            if(!Confirm(L.Text("EXPORT_SECRET_CONFIRM")))return;
            string? password=BundlePassword(this,true);if(password==null)return;
            var save=new SaveFileDialog{FileName="Rovunet-encrypted.txt",Filter="TXT|*.txt"};if(save.ShowDialog(this)!=true)return;
            var profiles=data.Profiles.ToArray();string uri=await Task.Run(()=>BundleCodec.Seal(profiles,password));
            await File.WriteAllTextAsync(save.FileName,uri);
        }catch(Exception e){Info("EXPORT_ENCRYPTED",L.Text(e is SafeError se?se.Code:"SAVE_FAILED"));}
    }
}



