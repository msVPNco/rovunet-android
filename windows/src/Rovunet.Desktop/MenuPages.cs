using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static void MenuHeading(ContextMenu menu,string key)
    {
        if(menu.Items.Count>0)menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem{Header=L.Text(key),IsEnabled=false,FontSize=11,MinHeight=28,Padding=new(12,6,12,2),Focusable=false});
    }
    ContextMenu PathMenu(Profile profile,MenuItem connectItem,MenuItem edit,MenuItem remove)
    {
        var menu=new ContextMenu{FlowDirection=L.Direction};
        MenuHeading(menu,"MENU_CONNECTION");menu.Items.Add(connectItem);
        menu.Items.Add(MenuAction("LAST_TEST",()=>ShowMeasurement(profile)));
        menu.Items.Add(MenuAction("QUICK_HTTPS",()=>CollectionTestDialog([profile])));
        menu.Items.Add(MenuAction("STABILITY_TEST",()=>CollectionTestDialog([profile],3)));
        menu.Items.Add(MenuAction("STOP_TEST",CancelCollectionTest));
        if(ProfileEditing.IsDns(profile))menu.Items.Add(MenuAction("FIND_DNS",()=>ResolverFinder(profile)));
        MenuHeading(menu,"MENU_MANAGE");menu.Items.Add(edit);
        var pin=MenuAction("PIN_TOP",()=>TogglePin(profile));menu.Items.Add(pin);
        if(TransportPlan.UsesSlip(profile.Raw)){menu.Items.Add(MenuAction("SLIP_CERT_CHOOSE",()=>ChooseSlipCertificate(profile)));menu.Items.Add(MenuAction("SLIP_INTERVAL",()=>SlipTimingDialog(profile)));}
        var move=MenuAction("MOVE_COLLECTION",()=>MoveProfiles([profile]));menu.Items.Add(move);
        MenuHeading(menu,"SECTION_TRANSFER");menu.Items.Add(MenuAction("EXPORT_PROFILE",()=>ExportProfiles([profile])));menu.Items.Add(MenuAction("SHARE_QR",()=>ShareQr([profile])));
        menu.Items.Add(new Separator());menu.Items.Add(remove);
        menu.Opened+=(_,_)=>{
            pin.Header=L.Text(data.PinnedProfileIds.Contains(profile.Id)?"UNPIN_TOP":"PIN_TOP");
            foreach(var item in menu.Items.OfType<MenuItem>().Where(i=>i.Tag is string))item.IsEnabled=!busy;
            bool connected=state.State=="connected"&&activeProfile?.Id==profile.Id;
            connectItem.Header=L.Text(connected?"DISCONNECT":"CONNECT");connectItem.IsEnabled=!busy&&(connected||profile.CanConnect);
            remove.IsEnabled=!busy&&state.State!="connected";edit.IsEnabled=!busy;
            move.IsEnabled=!busy&&state.State!="connected"&&profile.SourceId.Length==0;
            foreach(var item in menu.Items.OfType<MenuItem>()){
                if(item.Tag is "QUICK_HTTPS" or "STABILITY_TEST")item.IsEnabled=!busy&&state.State!="connected";
                if(item.Tag is "STOP_TEST")item.IsEnabled=currentCollection is{Running:true};
                if(item.Tag is "LAST_TEST")item.IsEnabled=data.Measurements.ContainsKey(profile.Id);
            }
        };
        return menu;
    }
    void RefreshCollectionMenu(ContextMenu menu,List<Profile> members)
    {
        bool idle=!busy&&state.State is "disconnected" or "error";
        foreach(var item in menu.Items.OfType<MenuItem>().Where(i=>i.Tag is string)){
            item.IsEnabled=!busy;
            if(item.Tag is "RENAME" or "DELETE_COLLECTION" or "TEST_COLLECTION" or "STABILITY_TEST")item.IsEnabled=idle;
            if(item.Tag is "UPDATE")item.IsEnabled=!updatingSubscriptions20.Contains(members[0].SourceId)&&!PublicFeed.IsRemoved(data.Subscriptions.FirstOrDefault(s=>s.Id==members[0].SourceId)?.Url??"");
            if(item.Tag is "MOVE_COLLECTION")item.IsEnabled=idle&&members.All(p=>p.SourceId.Length==0);
            if(item.Tag is "STOP_TEST")item.IsEnabled=currentCollection is{Running:true}&&currentCollection.Key==CollectionKey(members[0]);
            if(item.Tag is "SORT_RESULTS"){
                item.IsEnabled=!busy&&members.Any(p=>data.Measurements.ContainsKey(p.Id));
                foreach(var sort in item.Items.OfType<MenuItem>())sort.IsEnabled=sort.Tag is "SORT_LATENCY"?members.Any(p=>data.Measurements.GetValueOrDefault(p.Id)?.Verified==true):members.Any(p=>data.Measurements.ContainsKey(p.Id));
            }
        }
    }
    void ConnectCollection(List<Profile> members)
    {
        var chosen=members.FirstOrDefault(p=>p.Id==data.SelectedId)??members.FirstOrDefault(p=>p.CanConnect);
        if(chosen==null)return;
        if(state.State is "connected" or "connecting"&&members.Any(p=>p.Id==activeProfile?.Id)){RequestDisconnect();return;}
        SwitchPath(chosen);
    }
    void ToggleCollection(List<Profile> members)
    {
        ToggleCollection26(CollectionKey(members[0]));
    }
    void TogglePin(Profile profile)
    {
        bool pinned=data.PinnedProfileIds.Contains(profile.Id);
        if(pinned)data.PinnedProfileIds.Remove(profile.Id);else data.PinnedProfileIds.Add(profile.Id);
        try{Save();}catch{if(pinned)data.PinnedProfileIds.Add(profile.Id);else data.PinnedProfileIds.Remove(profile.Id);throw;}
        foreach(var group in groups)OrderPinnedPaths(group);Notify(L.Text(pinned?"UNPIN_TOP":"PIN_TOP"));
    }
    void OrderPinnedPaths(GroupView group)
    {
        var tiles=rows.Where(r=>group.Members.Any(p=>p.Id==r.Profile.Id)).ToDictionary(r=>r.Profile.Id,r=>r.Tile);
        var current=group.Paths.Children.Cast<Border>().ToArray();
        var pinned=group.Members.Where(p=>data.PinnedProfileIds.Contains(p.Id)).Select(p=>tiles[p.Id]).ToHashSet();
        var recent=data.RecentProfileIds.Select((id,index)=>(id,index)).ToDictionary(x=>x.id,x=>x.index);
        var byTile=tiles.ToDictionary(x=>x.Value,x=>x.Key);
        var ordered=current.OrderBy(p=>recent.GetValueOrDefault(byTile[p],int.MaxValue)).ThenByDescending(p=>pinned.Contains(p)).ToArray();
        if(current.SequenceEqual(ordered))return;group.Paths.Children.Clear();foreach(var tile in ordered)group.Paths.Children.Add(tile);
    }
    void ChooseCollectionPath(List<Profile> members)
    {
        var page=Dialog("PATH_SELECTION");var panel=new StackPanel{Margin=new(20)};
        panel.Children.Add(Text(L.Text("SELECT_PATH"),14,Muted));
        foreach(var profile in members){var button=SettingsAction("SELECT",()=>{if(busy)return;SelectProfile20(profile);page.DialogResult=true;});button.Content=profile.Name+" · "+profile.Protocol;button.IsEnabled=profile.CanConnect;panel.Children.Add(Card(button));}
        page.Content=panel;page.ShowDialog();
    }
    void ShowMeasurement(Profile profile)
    {
        if(!data.Measurements.TryGetValue(profile.Id,out var result))return;
        Info("LAST_TEST",profile.Name+"\n"+result.SampledAt.ToLocalTime().ToString("g",CultureInfo.CurrentCulture)+"\n"+L.Text(result.Verified?"HTTPS_VERIFIED":result.Error.Length>0?result.Error:"TEST_UNCONFIRMED")+"\n"+result.ElapsedMs.ToString(CultureInfo.InvariantCulture)+" ms · "+result.Passes+"/"+result.Attempts);
    }
    void MoveProfiles(List<Profile> profiles)
    {
        if(busy||state.State=="connected"||profiles.Any(p=>p.SourceId.Length>0))return;
        var page=Dialog("MOVE_COLLECTION");var panel=new StackPanel{Margin=new(20)};
        var targets=data.Profiles.Where(p=>p.SourceId.Length==0&&!profiles.Contains(p)).GroupBy(p=>p.CollectionId).Select(g=>new{Id=g.Key,Name=g.First().CollectionName}).ToList();
        var choose=new ComboBox{ItemsSource=targets,DisplayMemberPath="Name",SelectedIndex=targets.Count>0?0:-1};panel.Children.Add(OutlinedField("COLLECTION",choose));
        var name=new TextBox();panel.Children.Add(OutlinedField("NEW_COLLECTION",name));
        var error=Text("",12,Red);panel.Children.Add(error);
        panel.Children.Add(Button("SAVE",()=>{
            try{
                string newName=name.Text.Trim();if(newName.Length>120)throw new SafeError("CORE_NAME_INVALID");
                var target=targets.FirstOrDefault(t=>ReferenceEquals(choose.SelectedItem,t));
                string id=newName.Length>0?"manual:"+Guid.NewGuid().ToString("N"):target?.Id??throw new SafeError("CORE_NAME_INVALID");
                string title=newName.Length>0?newName:target!.Name;
                var old=profiles.Select(p=>(p,p.CollectionId,p.CollectionName)).ToArray();
                foreach(var p in profiles){p.CollectionId=id;p.CollectionName=title;}
                try{Save();}catch{foreach(var (p,oldId,oldName) in old){p.CollectionId=oldId;p.CollectionName=oldName;}throw;}
                BuildCards();page.DialogResult=true;
            }catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"SAVE_FAILED");}
        },true));page.Content=panel;page.ShowDialog();
    }
    void PublicSourcesPage()
    {
        var page=Dialog("PUBLIC_SOURCES");var panel=new StackPanel{Margin=new(20)};panel.Children.Add(Text(L.Text("PUBLIC_CONSENT"),14,Muted));
        foreach(var preset in PublicFeed.Presets){var body=new StackPanel();body.Children.Add(Text(preset.DisplayName,19));body.Children.Add(Text(L.Text("PUBLIC_PREVIEW_HINT"),12,Muted));body.Children.Add(Button("REVIEW_IMPORT",()=>SubscriptionDialog(null,preset.Url),true));panel.Children.Add(Card(body));}
        page.Content=panel;page.ShowDialog();
    }
    void ShareQr(List<Profile> profiles)
    {
        if(profiles.Count==0)return;
        if(profiles.Any(p=>ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[34]!="1"))throw new SafeError("SHARING_DISABLED");
        if(!Confirm(L.Text("EXPORT_SECRET_CONFIRM")))return;
        string text=profiles.Count==1?profiles[0].Raw:JsonSerializer.Serialize(new{version=1,items=profiles.Select(p=>new{id=p.Id,link=p.Raw})});
        if(Encoding.UTF8.GetByteCount(text)>1800){Info("SHARE_QR",L.Text("QR_USE_FILE"));return;}
        var writer=new ZXing.BarcodeWriterPixelData{Format=ZXing.BarcodeFormat.QR_CODE,Options=new ZXing.QrCode.QrCodeEncodingOptions{Width=360,Height=360,Margin=2,CharacterSet="UTF-8"}};
        var pixels=writer.Write(text);var image=BitmapSource.Create(pixels.Width,pixels.Height,96,96,PixelFormats.Bgra32,null,pixels.Pixels,pixels.Width*4);image.Freeze();
        var page=Dialog("SHARE_QR");var panel=new StackPanel{Margin=new(20)};panel.Children.Add(new Image{Source=image,Width=300,Height=300,HorizontalAlignment=HorizontalAlignment.Center});
        panel.Children.Add(Button("SAVE_PNG",()=>{var file=new SaveFileDialog{FileName="Rovunet-QR.png",Filter="PNG|*.png"};if(file.ShowDialog(this)!=true)return;var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(file.FileName);encoder.Save(stream);}));page.Content=panel;page.ShowDialog();
    }
    void SaveScannedResolvers(string[] addresses)
    {
        var page=Dialog("USE_DNS");var panel=new StackPanel{Margin=new(20)};
        panel.Children.Add(Text(L.Text("SCAN_SAVE_LIMITS"),13,Muted));
        var text=new TextBox{Text=string.Join('\n',addresses),AcceptsReturn=true,MinHeight=100,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FlowDirection=FlowDirection.LeftToRight};panel.Children.Add(OutlinedField("DNS_RESOLVERS",text));
        void SaveGlobal(){string normalized=GlobalResolvers.Normalize(text.Text);var old=(data.GlobalResolverList,data.GlobalResolverEnabled);data.GlobalResolverList=normalized;data.GlobalResolverEnabled=true;try{Save();}catch{(data.GlobalResolverList,data.GlobalResolverEnabled)=old;throw;}page.DialogResult=true;Notify(L.Text("SETTINGS_SAVED"));}
        void SavePool(){string normalized=string.Join('\n',DnsPool.Parse(data.DnsPoolList+"\n"+text.Text));var old=data.DnsPoolList;data.DnsPoolList=normalized;try{Save();}catch{data.DnsPoolList=old;throw;}page.DialogResult=true;Notify(L.Text("SETTINGS_SAVED"));}
        panel.Children.Add(Button("GLOBAL_RESOLVER_SAVE",SaveGlobal,true));panel.Children.Add(Button("DNS_POOL_SAVE",SavePool));page.Content=panel;page.ShowDialog();
    }
}
