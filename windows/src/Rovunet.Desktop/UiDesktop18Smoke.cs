using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunDesktop18Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();var bounds=new Dictionary<string,object>();var before=new WinInetProxy().Read();string folder=Path.GetDirectoryName(report)!;
        try{
            var manual=Enumerable.Range(0,3).Select(i=>Importer.Single("socks5://127.0.0.1:"+(10001+i)+"#Manual%20"+(i+1))).ToList();foreach(var p in manual){p.CollectionId="manual18";p.CollectionName="win";}
            var subscription=Enumerable.Range(0,4).Select(i=>Importer.Single("vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&path=%2Fsocket#VLESS%20"+(i+1))).ToList();foreach(var p in subscription){p.SourceId="sub18";p.CollectionId="sub:sub18";p.CollectionName="My subscription";}
            data.Profiles=manual.Concat(subscription).ToList();data.SelectedId=subscription[0].Id;data.Subscriptions=[new(){Id="sub18",Name="My subscription",Url="https://server.example.invalid/sub/fixture.json"}];data.ExpandedCollections["sub:sub18"]=true;
            var now=DateTimeOffset.UtcNow;statuses["sub18"]=new(){ServerDate=now,ReceivedAt=now,Status=new(){State="active",Metering="complete",LimitBytes=30_000_000_000,UsedBytes=6_000_000_000,GeneratedAt=now,SampledAt=now,ExpiresAt=now.AddDays(28).AddHours(1)}};
            foreach(string language in new[]{"en","fa","ru","zh-CN"})foreach(string theme in new[]{"amoled","dark","light"}){
                L.Language=data.Language=language;data.Theme=theme;Width=1200;Height=940;ApplyTheme();Build();SetState(new());await Idle18();string id=language+"_"+theme;
                if(language=="fa")checks[id+"_real_bundled_persian_typeface"]=new Typeface(FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out var glyph)&&glyph.FamilyNames.Values.Contains("Vazirmatn");
                checks[id+"_sidebar_general_above_tools"]=Nav("SETTINGS").TranslatePoint(new Point(),this).Y<Nav("DNS_SCANNER").TranslatePoint(new Point(),this).Y;
                checks[id+"_help_about_pinned_below_tools"]=Nav("HELP").TranslatePoint(new Point(),this).Y>Nav("DNS_SCANNER").TranslatePoint(new Point(),this).Y&&Nav("ABOUT").TranslatePoint(new Point(),this).Y>Nav("HELP").TranslatePoint(new Point(),this).Y;
                checks[id+"_no_duplicate_add_or_more_in_sidebar"]=!Visuals<Button>(contextHost).Any(b=>AutomationProperties.GetAutomationId(b) is "nav:IMPORT" or "nav:SUBSCRIPTIONS" or "nav:PUBLIC_SOURCES" or "nav:MORE");
                checks[id+"_four_touch_flags"]=Visuals<Button>((DependencyObject)Content).Count(b=>b.Tag is string value&&value.StartsWith("language:")&&b.ActualWidth>=44&&b.ActualHeight>=44)==4;
                checks[id+"_bulky_power"]=connect.ActualWidth>=150&&connect.ActualHeight>=150&&connect.Template==Application.Current.FindResource("HeroPowerTemplate");
                checks[id+"_disconnected_no_old_latency"]=heroPing.Visibility==Visibility.Collapsed;
                var group=groups.Single(g=>g.Key=="sub:sub18");checks[id+"_quota_once_for_subscription"]=Visuals<Border>((DependencyObject)Content).Count(b=>Equals(b.Tag,"ACCOUNT_QUOTA")&&b.IsVisible)==1;
                checks[id+"_quota_used_limit_remaining"]=group.Quota!.Used.Text.Contains('6')&&group.Quota.Used.Text.Contains("30")&&group.Quota.Remaining.Text.Contains("24")&&group.Quota.Percent.Text.Contains("20");
                group.Quota.Update(new(statuses["sub18"].Status,now,false,Severity.Unknown));await Idle18();var quotaBar=Visuals<ProgressBar>(group.Quota.Root).Single();checks[id+"_cached_quota_label_does_not_overlap_meter"]=group.Quota.Note.IsVisible&&group.Quota.Note.TranslatePoint(new Point(0,group.Quota.Note.ActualHeight),group.Quota.Root).Y<=quotaBar.TranslatePoint(new Point(),group.Quota.Root).Y&&group.Quota.Used.Text.Contains('6');
                group.Quota.Update(new(new(){State="active",Metering="partial",LimitBytes=30_000_000_000,UsedBytes=6_000_000_000},now,false,Severity.Unknown));await Idle18();checks[id+"_partial_usage_is_not_presented_as_unlimited"]=group.Quota.Used.Text==L.Text("ACCOUNT_INCOMPLETE")&&!quotaBar.IsVisible;
                group.Quota.Update(null);await Idle18();checks[id+"_missing_usage_remains_unknown"]=group.Quota.Used.Text==L.Text("ACCOUNT_UNKNOWN")&&!quotaBar.IsVisible;
                if(language=="fa"&&theme=="dark")SaveApp18(Path.Combine(folder,"audit30-unknown-quota-after-known.png"));
                group.Quota.Update(statuses["sub18"].Evaluate());await Idle18();
                checks[id+"_restored_meter_and_percent"]=quotaBar.IsVisible && quotaBar.Value==20 && group.Quota.Percent.IsVisible;
                RefreshCards();
                await Idle18();double quotaHeight=group.Quota.Root.ActualHeight,childHeight=rows.First().Tile.ActualHeight;group.Quota.Note.Text=new string('x',800);group.Quota.Used.Text=new string('y',800);await Idle18();checks[id+"_content_does_not_grow_card_templates"]=Math.Abs(group.Quota.Root.ActualHeight-quotaHeight)<1&&Math.Abs(rows.First().Tile.ActualHeight-childHeight)<1;RefreshCards();
                SaveApp18(Path.Combine(folder,"home-wide-closed-"+language+"-"+theme+".png"));OpenAddHub();await Task.Delay(220);await Idle18();checks[id+"_approved_add_hub_routes"]=addHub?.IsOpen==true&&Visuals<Button>(addHub.Child).Select(AutomationProperties.GetAutomationId).SequenceEqual(new[]{"add:ADD_LINK_FILE","add:ADD_SUBSCRIPTION","add:ADD_MANUAL","add:PUBLIC_SOURCES","add:CHAIN_TITLE"});
                SaveApp18(Path.Combine(folder,"home-wide-"+language+"-"+theme+".png"));CloseAddHub();
                Width=680;await Idle18();checks[id+"_compact_sidebar_hidden"]=!contextHost.IsVisible;checks[id+"_minimum_height"]=MinHeight>=Math.Min(880,SystemParameters.WorkArea.Height-32);Height=400;await Idle18();checks[id+"_cannot_squash_below_minimum"]=ActualHeight>=MinHeight-1;Height=940;await Idle18();
                SaveApp18(Path.Combine(folder,"home-compact-closed-"+language+"-"+theme+".png"));OpenAddHub();await Task.Delay(220);await Idle18();var popup=(FrameworkElement)addHub!.Child;var popupStart=popup.PointToScreen(new Point());var popupEnd=popup.PointToScreen(new Point(popup.ActualWidth,popup.ActualHeight));var windowStart=PointToScreen(new Point());var windowEnd=PointToScreen(new Point(ActualWidth,ActualHeight));bounds[id]=new{popupStart,popupEnd,windowStart,windowEnd};checks[id+"_popup_stays_inside_window"]=Math.Min(popupStart.X,popupEnd.X)>=Math.Min(windowStart.X,windowEnd.X)-2&&Math.Max(popupStart.X,popupEnd.X)<=Math.Max(windowStart.X,windowEnd.X)+2&&popupStart.Y>=windowStart.Y-2&&popupEnd.Y<=windowEnd.Y+2;checks[id+"_floating_plus_above_status"]=addButton.TranslatePoint(new Point(0,addButton.ActualHeight),this).Y<connectionStrip.TranslatePoint(new Point(),this).Y;
                SaveApp18(Path.Combine(folder,"home-compact-"+language+"-"+theme+".png"));CloseAddHub();
            }
            L.Language=data.Language="en";data.Theme="amoled";Width=1200;ApplyTheme();Build();await Idle18();
            foreach(string key in new[]{"ADD_LINK_FILE","ADD_SUBSCRIPTION","ADD_MANUAL","PUBLIC_SOURCES"}){
                OpenAddHub();await Idle18();var button=Visuals<Button>(addHub!.Child).Single(b=>AutomationProperties.GetAutomationId(b)=="add:"+key);
                _=Dispatcher.BeginInvoke(()=>{checks["add_route_"+key+"_opens_inline"]=NavigationDepth==1&&DialogWindow.OpenPages.Count>0;foreach(var page in DialogWindow.OpenPages.ToArray().Reverse())page.Close();},DispatcherPriority.ApplicationIdle);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();
            }
            checks["general_keeps_all_redistributed_actions"]=Visuals<Button>(GeneralMenu(()=>{})).Select(AutomationProperties.GetName).Order().SequenceEqual(new[]{"SETTINGS","CONNECTION_DETAILS","MANAGE_COLLECTIONS","BACKUP_EXPORT","DIAGNOSTICS","EXIT"}.Select(L.Text).Order());
            var savedProfiles=data.Profiles;data.Profiles=[];data.SelectedId="";
            _=Dispatcher.BeginInvoke(()=>{var page=DialogWindow.OpenPages.Single();var actions=Visuals<Button>(page.PageVisual).Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("manage:")).ToArray();checks["management_without_collection_disables_scoped_actions"]=actions.Length==9&&actions.Where(b=>new[]{"TEST_COLLECTION","STABILITY_TEST","TCP_CHECK","SORT_LATENCY","SORT_RELIABILITY","CLEAR_TESTS"}.Any(k=>AutomationProperties.GetAutomationId(b)=="manage:"+k)).All(b=>!b.IsEnabled);page.Close();},DispatcherPriority.ApplicationIdle);CollectionManagement();await Idle18();data.Profiles=savedProfiles;data.SelectedId=savedProfiles[0].Id;
            _=Dispatcher.BeginInvoke(()=>{var page=DialogWindow.OpenPages.Single();var choose=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"MANAGE_COLLECTION"));choose.SelectedIndex=-1;var sort=Visuals<Button>(page.PageVisual).Single(b=>AutomationProperties.GetAutomationId(b)=="manage:TCP_CHECK");bool disabled=!sort.IsEnabled;choose.SelectedIndex=0;checks["management_selection_controls_scoped_action_state"]=disabled&&sort.IsEnabled;page.Close();},DispatcherPriority.ApplicationIdle);CollectionManagement();await Idle18();
            var oldCount=data.Profiles.Count;var flag=Visuals<Button>((DependencyObject)Content).Single(b=>Equals(b.Tag,"language:fa"));flag.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();checks["flag_changes_language_without_profile_loss"]=L.Language=="fa"&&data.Language=="fa"&&data.Profiles.Count==oldCount;
            checks["source_proxy_unchanged"]=before==new WinInetProxy().Read();
        }catch(Exception error){checks["exception_"+error.GetType().Name]=false;File.WriteAllText(Path.Combine(folder,"fixture-error.txt"),error.ToString());}
        finally{CloseAddHub();foreach(var page in DialogWindow.OpenPages.ToArray().Reverse())page.Close();}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Approved preview18 real WPF UI, add routes, flags, quota, touch bounds and fixed minimum height",synthetic_data=true,external_network_tested=false,bounds,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
        Button Nav(string key)=>Visuals<Button>(contextHost).Single(b=>AutomationProperties.GetAutomationId(b)=="nav:"+key);
    }
    async Task Idle18(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
    void SaveApp18(string path)
    {
        UpdateLayout();var root=(FrameworkElement)Content;var main=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);main.Render(root);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
            dc.DrawImage(main,new Rect(0,0,root.ActualWidth,root.ActualHeight));
            if(addHub?.IsOpen==true&&addHub.Child is FrameworkElement popup){popup.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(popup.ActualWidth),(int)Math.Ceiling(popup.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(popup);var rootA=root.PointToScreen(new Point());var rootB=root.PointToScreen(new Point(root.ActualWidth,root.ActualHeight));var popupA=popup.PointToScreen(new Point());var popupB=popup.PointToScreen(new Point(popup.ActualWidth,popup.ActualHeight));var dpi=VisualTreeHelper.GetDpi(root);var point=new Point((Math.Min(popupA.X,popupB.X)-Math.Min(rootA.X,rootB.X))/dpi.DpiScaleX,(Math.Min(popupA.Y,popupB.Y)-Math.Min(rootA.Y,rootB.Y))/dpi.DpiScaleY);dc.DrawImage(image,new Rect(point,new Size(popup.ActualWidth,popup.ActualHeight)));}
        }var output=new RenderTargetBitmap(main.PixelWidth,main.PixelHeight,96,96,PixelFormats.Pbgra32);output.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(output));using var stream=File.Create(path);encoder.Save(stream);
    }
}
