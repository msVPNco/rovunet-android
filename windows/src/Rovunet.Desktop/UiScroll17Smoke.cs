using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    internal async Task<bool> RunScroll17(string report,bool selectionOnly=false)
    {
        var startedUtc=DateTimeOffset.UtcNow;var checks=new Dictionary<string,bool>();string dir=Path.GetDirectoryName(report)!;Directory.CreateDirectory(dir);
        string store=Path.Combine(dir,"private-fixture");
        Rect Bounds(FrameworkElement e,Visual parent)=>e.TransformToAncestor(parent).TransformBounds(new Rect(e.RenderSize));
        bool Fits(FrameworkElement e)=>!e.IsVisible||new Rect(0,0,heroCard19.ActualWidth,heroCard19.ActualHeight).Contains(Bounds(e,heroCard19));
        try{
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            data.Profiles=Enumerable.Range(0,20).Select(i=>{var p=Importer.Single("socks5://127.0.0.1:"+(19000+i)+"#Path"+i);p.CollectionId="scroll17-"+(i/5);p.CollectionName="Scroll17 "+(i/5);return p;}).ToList();
            data.Subscriptions=[];data.SelectedId=data.Profiles[0].Id;activeProfile=data.Profiles[0];
            if(!selectionOnly)foreach(string lang in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"dark","light","amoled"})foreach(double width in new[]{480d,1040d}){
                L.Language=data.Language=lang;data.Theme=theme;Width=width;Height=900;ApplyTheme();Build();
                foreach(var g in groups)data.ExpandedCollections[g.Key]=true;SetState(new(){State="connected",Verified=true});liveLatency=987;latencySampled=DateTimeOffset.UtcNow;RefreshCards();await Idle18();
                foreach(double offset in new[]{0d,30d,45d,60d}){
                    mainScroll.ScrollToVerticalOffset(offset);await Idle18();
                    // Reproduce the owner's bug: several telemetry/card updates after scroll.
                    for(int i=0;i<3;i++)RefreshCards();await Idle18();string key=lang+"_"+theme+"_"+width+"_"+offset;
                    var title=Visuals<MarqueeLine>(heroCard19).Single(m=>Equals(m.Tag,"HERO_TITLE_MARQUEE"));
                    checks[key+"_title_fully_inside_header"]=Fits(title);
                    checks[key+"_ping_fully_inside_header"]=heroPing.IsVisible&&Fits(heroPing);
                    checks[key+"_visible_name_inside_header"]=Fits(heroNameLine21);
                    checks[key+"_visible_detail_inside_header"]=Fits(heroDetailLine17);
                    checks[key+"_same_state_top_and_bottom"]=heroTitle.Text==connectionText.Text;
                }
                if(lang=="fa"&&theme=="dark"&&width==480)SavePage((FrameworkElement)Content,Path.Combine(dir,"collapsed-after-refresh.png"));
                SetState(new());checks[lang+"_"+theme+"_"+width+"_idle_is_a_status"]=detail.Text==L.Text("CONNECTION_IDLE")&&heroDetail.Text==L.Text("CONNECTION_IDLE");
            }
            var p0=data.Profiles[0];var p1=data.Profiles[1];var p2=data.Profiles[2];var p12=data.Profiles[12];
            data.SelectedId="";data.RecentProfileIds=[];state=new();Build();
            var group=groups.Single(g=>g.Members.Contains(p0));
            var originalPaths=group.Paths.Children.Cast<UIElement>().ToArray();var originalGroups=cards.Children.Cast<UIElement>().ToArray();
            SelectProfile20(p2);await Idle18();
            checks["single_click_selects_without_moving_subcards"]=data.SelectedId==p2.Id&&group.Paths.Children.Cast<UIElement>().SequenceEqual(originalPaths)&&client==null;
            checks["single_click_does_not_update_use_history"]=data.RecentProfileIds.Count==0;
            SelectProfile20(p12);await Idle18();
            checks["single_click_does_not_move_parent_collection"]=cards.Children.Cast<UIElement>().SequenceEqual(originalGroups);
            void Connected(Profile p){SelectProfile20(p);activeProfile=p;RecordConnectedUse17(p);SetState(new(){State="connected"});}
            Connected(p1);Connected(p2);Connected(p0);
            checks["successful_connections_preserve_mru_order"]=data.RecentProfileIds.Take(3).SequenceEqual(new[]{p0.Id,p2.Id,p1.Id});
            checks["connected_subcard_moves_first"]=ReferenceEquals(group.Paths.Children[0],rows.Single(r=>r.Profile.Id==p0.Id).Tile);
            Connected(p12);checks["connected_collection_moves_first"]=ReferenceEquals(cards.Children[1],groups.Single(g=>g.Members.Contains(p12)).Card);
            originalPaths=group.Paths.Children.Cast<UIElement>().ToArray();originalGroups=cards.Children.Cast<UIElement>().ToArray();
            SelectProfile20(p0);for(int i=0;i<5;i++)RefreshCards();
            checks["single_click_with_active_connection_keeps_order_and_session"]=ReferenceEquals(activeProfile,p12)&&group.Paths.Children.Cast<UIElement>().SequenceEqual(originalPaths)&&cards.Children.Cast<UIElement>().SequenceEqual(originalGroups);
            var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
            checks["mru_order_persists_in_private_store"]=saved.RecentProfileIds.SequenceEqual(data.RecentProfileIds)&&saved.SelectedId==p0.Id;
            var history=data.RecentProfileIds.ToArray();string selected=data.SelectedId;
            storeSaveFixture28=()=>throw new IOException("synthetic save failure");SelectProfile20(data.Profiles[4]);
            checks["failed_save_restores_selection_and_order"]=selected==data.SelectedId&&history.SequenceEqual(data.RecentProfileIds);
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            var restarted=new MainWindow(saved,true);try{checks["restored_window_keeps_recent_group_first"]=ReferenceEquals(restarted.cards.Children[1],restarted.groups.Single(g=>g.Members.Any(p=>p.Id==p12.Id)).Card);}finally{restarted.Close();}
        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
        finally{storeSaveFixture28=null;}
        var loaded=new[]{typeof(MainWindow).Assembly,typeof(Profile).Assembly,typeof(UserData).Assembly}.Select(a=>new{path=a.Location,version=a.GetName().Version?.ToString(),sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))}).ToArray();
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,startedUtc,endedUtc=DateTimeOffset.UtcNow,pid=Environment.ProcessId,command=Environment.GetCommandLineArgs(),cwd=Environment.CurrentDirectory,loaded,synthetic=true,realNetworkTested=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
