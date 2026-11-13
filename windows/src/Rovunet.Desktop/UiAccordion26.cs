using System.Diagnostics;
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
    internal async Task<bool> RunAccordion26(string report)
    {
        var start=DateTimeOffset.UtcNow;var checks=new Dictionary<string,bool>();string dir=Path.GetDirectoryName(report)!;Directory.CreateDirectory(dir);string store=Path.Combine(dir,"private-fixture");
        int Count()=>groups.Count(g=>g.Paths.Visibility==Visibility.Visible);
        try{
            data.Profiles=[];data.Subscriptions=[];data.ExpandedCollections=[];data.OpenCollectionId=null;statuses.Clear();
            var now=DateTimeOffset.UtcNow;
            for(int g=0;g<4;g++){
                string source=Importer.Fingerprint("accordion26-"+g);
                for(int n=0;n<(g==0?8:2);n++){var p=Importer.Single("socks5://127.0.0.1:"+(19100+g*10+n)+"#"+Uri.EscapeDataString(n==0?"کانفیگ با نام طولانی برای سنجش حرکت متن و مرزبندی · Profile "+g:"Profile "+g+"-"+n));p.SourceId=source;p.ItemId=g+"-"+n;p.CollectionName=new[]{"Canada · اشتراک حجمی","اشتراک زمانی · Time limited","اطلاعات حساب نامشخص","بدون محدودیت · Unlimited"}[g];data.Profiles.Add(p);}
                data.Subscriptions.Add(new(){Id=source,Name=data.Profiles.Last().CollectionName,Url="https://fixture.invalid/sub/"+g+".json"});
                if(g!=2)statuses[source]=SubscriptionUsage20.Parse("upload=1000000000;download=5000000000;total="+(g==0?"30000000000":"0")+";expire="+(g==3?"0":now.AddDays(28).ToUnixTimeSeconds().ToString()),"",now)!.Snapshot(source);
                data.ExpandedCollections["sub:"+source]=true;
            }
            var chosen=data.Profiles[2];data.SelectedId=chosen.Id;data.PinnedProfileIds.Add(chosen.Id);data.LocalOverrideSources[chosen.Id]=chosen.Raw;data.LocalOverrideNames[chosen.Id]=chosen.Name;
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            string Preserved()=>JsonSerializer.Serialize(new{data.Profiles,data.Subscriptions,data.SelectedId,data.PinnedProfileIds,data.LocalOverrideSources,data.LocalOverrideNames});
            string baseline=Preserved();Build();await Idle18();checks["legacy_multiple_open_normalized_without_profile_loss"]=Count()==1&&data.ExpandedCollections.Count(x=>x.Value)==1&&baseline==Preserved();
            var a=groups[0];var b=groups[1];
            if(data.OpenCollectionId==a.Key)ToggleCollection26(a.Key);a.Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();checks["arrow_opens_A"]=Count()==1&&data.OpenCollectionId==a.Key;
            ToggleCollection(b.Members);await Idle18();checks["menu_B_closes_A"]=Count()==1&&data.OpenCollectionId==b.Key&&a.Paths.Visibility==Visibility.Collapsed;
            b.Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();checks["close_B_allows_all_closed"]=Count()==0&&data.OpenCollectionId=="";
            ToggleCollection(a.Members);string old=data.OpenCollectionId!;var oldDictionary=JsonSerializer.Serialize(data.ExpandedCollections);
            storeSaveFixture28=()=>throw new IOException("synthetic expansion save failure");bool failed=false;try{ToggleCollection(b.Members);}catch(IOException){failed=true;}
            checks["save_failure_rolls_back_entire_expansion_transaction"]=failed&&old==data.OpenCollectionId&&oldDictionary==JsonSerializer.Serialize(data.ExpandedCollections)&&Count()==1;
            storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
            for(int i=0;i<4;i++)RefreshCards();search.Text="Profile";RefreshCards();checks["search_does_not_expand_other_groups"]=Count()<=1;search.Clear();
            string openBefore=data.OpenCollectionId!;bool sawPage=false;Dispatcher.BeginInvoke(()=>{sawPage=pageStack.Count==1;if(pageStack.Count>0)pageStack[^1].Close();},System.Windows.Threading.DispatcherPriority.Loaded);SettingsDialog();await Idle18();checks["page_back_keeps_single_expansion"]=sawPage&&Count()==1&&data.OpenCollectionId==openBefore;
            activeProfile=chosen;RecordConnectedUse17(chosen);SetState(new(){State="connected"});RefreshCards();checks["connection_reorder_does_not_expand_another_group"]=Count()==1&&data.OpenCollectionId==openBefore;activeProfile=null;SetState(new(){State="disconnected"});
            var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;var restarted=new MainWindow(saved,true);try{checks["restart_preserves_one_stable_id"]=restarted.CountOpen26()==1&&saved.OpenCollectionId==a.Key;}finally{restarted.Close();}
            checks["toggle_does_not_change_selection_pin_override_or_membership"]=Preserved()==baseline;
            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"})foreach(double width in new[]{480d,1120d}){
                L.Language=data.Language=language;data.Theme=theme;Width=width;ApplyTheme();Build();await Idle18();if(data.OpenCollectionId!.Length>0)ToggleCollection26(data.OpenCollectionId);await Idle18();
                string tag=language+"_"+theme+"_"+width;checks[tag+"_closed_headers_72_to_88"]=groups.All(g=>g.Header.ActualHeight>=72&&g.Header.ActualHeight<=88)&&Count()==0;
                checks[tag+"_no_hidden_quota_blank_panel"]=groups.All(g=>g.Details26!.Visibility==Visibility.Collapsed);
                checks[tag+"_unknown_does_not_claim_no_expiry_or_unlimited"]=groups.Single(g=>g.Members[0].SourceId==Importer.Fingerprint("accordion26-2")).ClosedStatus26!.Text==L.Text("ACCOUNT_UNKNOWN");
                if(language=="fa")SavePage((FrameworkElement)Content,Path.Combine(dir,"closed_"+theme+"_"+width+".png"));
                groups[0].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();
                checks[tag+"_one_open_and_66_dip_children"]=Count()==1&&rows.All(r=>Math.Abs(r.Tile.Height-66)<1)&&groups[0].Paths.Children.Cast<Border>().All(t=>Math.Abs(t.ActualHeight-66)<1)&&groups[0].Toggle.Width>=44&&groups[0].Toggle.ActualHeight>=44;
                if(language=="fa")SavePage((FrameworkElement)Content,Path.Combine(dir,"open_"+theme+"_"+width+".png"));
            }
            L.Language=data.Language="fa";data.Theme="dark";Width=480;ApplyTheme();Build();await Idle18();if(data.OpenCollectionId!.Length>0)ToggleCollection26(data.OpenCollectionId);mainScroll.ScrollToTop();await Idle18();
            string frames=Path.Combine(dir,"frames");Directory.CreateDirectory(frames);
            for(int frame=0;frame<64;frame++){
                if(frame==12||frame==48)groups[0].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(frame==32)ToggleCollection(groups[1].Members);
                if(frame==60&&data.OpenCollectionId!.Length>0)ToggleCollection26(data.OpenCollectionId);
                await Task.Delay(125);await Idle18();SavePage((FrameworkElement)Content,Path.Combine(frames,"frame"+frame.ToString("000")+".png"));
                if(Count()>1)throw new InvalidOperationException("Accordion expanded more than one collection");
            }
            checks["recorded_real_wpf_sequence_always_zero_or_one"]=true;
        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
        finally{storeSaveFixture28=null;}
        var loaded=new[]{typeof(MainWindow).Assembly,typeof(UserData).Assembly}.Select(a=>new{path=a.Location,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(a.Location)))});
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,startUtc=start,endUtc=DateTimeOffset.UtcNow,pid=Environment.ProcessId,command=Environment.GetCommandLineArgs(),cwd=Environment.CurrentDirectory,dpi=VisualTreeHelper.GetDpi(this).PixelsPerInchX,loaded,synthetic=true,realWpfRendering=true,liveNetwork=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
    int CountOpen26()=>groups.Count(g=>g.Paths.Visibility==Visibility.Visible);
}





