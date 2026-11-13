using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
using Rovunet.Platform;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
 internal async Task<bool> RunResearch15Smoke(string report)
 {
  var checks=new Dictionary<string,bool>();var evidence=new List<object>();
  string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
  string store=Path.Combine(folder,"synthetic-store");
  var uri=new Uri("https://generic.example.invalid/feed");string source=Importer.Fingerprint(uri.AbsoluteUri);
  var now=DateTimeOffset.UtcNow;
  var cases=new[]{
   ("volume","upload=10000000;download=20000000;total=1000000000",true,true,false),
   ("time","expire="+now.AddDays(8).ToUnixTimeSeconds(),true,false,true),
   ("mixed","upload=10000000;download=20000000;total=1000000000;expire="+now.AddDays(8).ToUnixTimeSeconds(),true,true,true),
   ("unlimited","upload=0;download=0;total=0;expire=0",true,false,true),
   ("partial","upload=10000000;total=1000000000",true,false,false),
   ("absent","",false,false,false),("malformed","total=oops",false,false,false),
   ("overflow","upload=9223372036854775807;download=1",false,false,false),
   ("expiry-range","expire=9223372036854775807",false,false,false),
   ("duplicate","total=1;total=2",false,false,false)
  };
  try {
   storeSaveFixture28=()=>PrivateStore.SaveIn(store,"profiles.dpapi",data);
   uiEventError15=e=>File.WriteAllText(report+".event-error",e.ToString());
   foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
    L.Language=data.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();Build();string tag=language+"_"+theme;
    await Idle18();var topMore=Visuals<Button>((DependencyObject)Content).Single(b=>Equals(b.Tag,"HEADER_MORE"));
    checks[tag+"_header_more_touch_target"]=topMore.ActualWidth>=44&&topMore.ActualHeight>=44;
    foreach(double scale in new[]{1d,1.25,1.5,2d})checks[tag+"_header_three_filled_dots_"+scale]=VisibleDots28(topMore,scale)==3;
    _=Dispatcher.BeginInvoke(()=>{checks[tag+"_header_more_opens_existing_general_page"]=NavigationDepth==1;if(DialogWindow.OpenPages.Count>0)DialogWindow.OpenPages.Last().Close();},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    topMore.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle18();
    foreach(var (name,header,valid,quota,expiryKnown) in cases){
     subscriptionUsage20.Clear();statuses.Clear();ownAccountSources20.Clear();
     data.Profiles=[];data.Subscriptions=[];data.LocalOverrideNames.Clear();data.LocalOverrideSources.Clear();data.SelectedId="";
     string raw="socks5://127.0.0.1:19123#Research15";
     CaptureUsage20(source,new(raw,now,header));
     CommitSubscription20(uri,[Importer.Single(raw)],"Metadata15");
     var saved=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
     checks[tag+"_"+name+"_profile_and_store_preserved"]=saved.Profiles.Count==1&&saved.Subscriptions.Count==1;
     checks[tag+"_"+name+"_nullable_usage_validity"]=(saved.Subscriptions.Single().Usage!=null)==valid;
     statuses.TryGetValue(source,out var status);var view=status?.Evaluate();var tile=groups.Single().Quota!;
     tile.Update(view);await Idle18();
     checks[tag+"_"+name+"_quota_visibility"]=Visuals<ProgressBar>(tile.Root).Single().Visibility==(quota?Visibility.Visible:Visibility.Collapsed);
     checks[tag+"_"+name+"_missing_not_unlimited"]=name=="unlimited"||tile.Used.Text!=L.Text("UNLIMITED_TRAFFIC");
     checks[tag+"_"+name+"_expiry_known"]=status==null?!expiryKnown:status.Status!.ExpiryKnown==expiryKnown;
     if(name=="partial")checks[tag+"_partial_shows_known_total_without_fabricated_used"]=tile.Used.Text.Contains(string.Format(L.Text("QUOTA_TOTAL_USAGE_UNKNOWN"),AccountUsage.Gigabytes(1000000000).ToString("0.##",System.Globalization.CultureInfo.CurrentCulture)));
     if(name=="time")checks[tag+"_time_only_shows_time_without_volume"]=tile.Time.Text.Length>0&&tile.Time.Text!=L.Text("UNLIMITED_TIME")&&tile.Used.Text!=L.Text("UNLIMITED_TRAFFIC");
     if(name=="volume")checks[tag+"_volume_only_keeps_expiry_unknown"]=tile.Time.Text==L.Text("UNKNOWN");
     evidence.Add(new{language,theme,name,valid,usage=saved.Subscriptions.Single().Usage,text=tile.Used.Text,time=tile.Time.Text,note=tile.Note.Text,profiles=saved.Profiles.Count});
    }
    CaptureUsage20(source,new("socks5://127.0.0.1:19123",now,"upload=1;download=2;total=100"));
    var current=subscriptionUsage20[source];
    current.ReceivedAt=now.AddHours(-1);current.ServerDate=now.AddHours(-1);
    statuses[source]=current.Snapshot(source);var staleTile=groups.Single().Quota!;staleTile.Update(statuses[source].Evaluate());
    checks[tag+"_stale_labelled"]=staleTile.Note.Text==L.Text("QUOTA_CACHED")&&!statuses[source].Evaluate().Trustworthy;
    CaptureUsage20(source,new("socks5://127.0.0.1:19123",now,"total=broken"));
    CommitSubscription20(uri,[Importer.Single("socks5://127.0.0.1:19123#Research15")],"Metadata15");
    var cached=PrivateStore.LoadFrom<UserData>(store,"profiles.dpapi")!;
    checks[tag+"_bad_refresh_persists_cached_label_and_keeps_profiles"]=cached.Profiles.Count==1&&cached.Subscriptions.Single().Usage!.CachedOnly&&!statuses[source].Evaluate().Trustworthy;
    var authoritative=new StatusSnapshot{Status=new(){ServerId=new('a',32),AccountId=new('b',32),PeriodId=new('c',32),State="expired",Metering="complete",LimitBytes=100,UsedBytes=40,GeneratedAt=now,Revision=1},Error="ACCOUNT_STALE"};
    ownAccountSources20.Add(source);statuses[source]=authoritative;
    CaptureUsage20(source,new("socks5://127.0.0.1:19123",now,"upload=0;download=0;total=0;expire=0"));
    checks[tag+"_advisory_cannot_replace_private_status"]=ReferenceEquals(statuses[source],authoritative);
    ResetTelemetry();activeProfile=data.Profiles.Single();SetState(new(){State="connected",HttpPort=18123,SocksPort=18124,Verified=false});
    checks[tag+"_connected_has_clear_status"]=heroDetail.Text==L.Text("CONNECTION_ESTABLISHED");
    lastTraffic=new(1,5);RefreshHero();
    checks[tag+"_traffic_has_clear_status"]=heroDetail.Text==L.Text("CONNECTION_DATA_CONFIRMED");
    liveLatency=123;latencySampled=DateTimeOffset.UtcNow;RefreshHero();
    checks[tag+"_live_https_updates_hero_despite_initial_unverified"]=heroDetail.Text==L.Text("CONNECTION_INTERNET_VERIFIED")&&heroDetail.Foreground==Green;
    checks[tag+"_ambiguous_old_sentence_absent"]=!heroDetail.Text.Contains("هنوز آزمایش")&&!heroDetail.Text.Contains("not been tested");
    await Idle18();SavePage((FrameworkElement)Content,Path.Combine(folder,"status15-"+tag+".png"));
    activeProfile=null;SetState(new());ResetTelemetry();
   }
   string privateBody=JsonSerializer.Serialize(new{version=1,server_id=new string('a',32),account_id=new string('b',32),period_id=new string('c',32),account_status="expired",expires_at=now.AddDays(-1).ToString("O"),limit_bytes=100L,used_bytes=20L,metering_status="complete",sampled_at=now.ToString("O"),generated_at=now.ToString("O"),revision=1});
   var restored=new MainWindow(new(){UseSystemProxy=false,Subscriptions=[new(){Id=source,Url=uri.AbsoluteUri,StatusBody=privateBody,Usage=SubscriptionUsage20.Parse("upload=0;download=0;total=0;expire=0","",now)}]},true);
   checks["restart_keeps_dedicated_status_over_generic_usage"]=restored.statuses[source].Status!.AccountId==new string('b',32)&&restored.statuses[source].Status!.State=="expired";
   checks["restart_keeps_dedicated_status_stale_and_bound"]=restored.ownAccountSources20.Contains(source)&&!restored.statuses[source].Evaluate().Trustworthy;restored.Close();
   var malformed=new MainWindow(new(){UseSystemProxy=false,Subscriptions=[new(){Id=source,Url=uri.AbsoluteUri,StatusBody="invalid",Usage=SubscriptionUsage20.Parse("upload=0;download=0;total=0;expire=0","",now)}]},true);
   malformed.CaptureUsage20(source,new("socks5://127.0.0.1:19123",now,"upload=0;download=0;total=0;expire=0"));
   checks["malformed_private_status_never_downgrades_to_generic_unlimited"]=malformed.ownAccountSources20.Contains(source)&&malformed.statuses[source].Status==null&&!malformed.statuses[source].Evaluate().Trustworthy;malformed.Close();
   var line=new MarqueeLine(new TextBlock{Text=new string('M',100),FontSize=16},26,FlowDirection.LeftToRight,()=>true){Width=190,IsHitTestVisible=false};
   var focusWindow=new Window{Title="Rovunet animation check",Width=300,Height=140,ShowInTaskbar=false,Content=line,WindowStartupLocation=WindowStartupLocation.CenterScreen};
   var otherWindow=new Window{Title="Rovunet focus check",Width=220,Height=120,ShowInTaskbar=false};
   try {
    focusWindow.Show();otherWindow.Owner=focusWindow;focusWindow.Activate();await Idle18();await Task.Delay(80);await Idle18();
    checks["marquee_active_window_animates"]=focusWindow.IsActive&&line.Moving;
    otherWindow.Show();otherWindow.Activate();await Idle18();double frozen=line.Translation;await Task.Delay(120);await Idle18();
    checks["marquee_inactive_window_has_no_animation_clock"]=!focusWindow.IsActive&&!line.Moving&&!line.HasAnimationClock;
    checks["marquee_inactive_text_stays_fixed"]=Math.Abs(line.Translation-frozen)<.1;
    otherWindow.Close();focusWindow.Activate();await Idle18();
    checks["marquee_reactivation_resumes"]=focusWindow.IsActive&&line.Moving;
    focusWindow.WindowState=WindowState.Minimized;await Idle18();
    checks["marquee_minimized_window_has_no_animation_clock"]=!line.Moving&&!line.HasAnimationClock;
   }finally{otherWindow.Close();focusWindow.Close();}
  }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
  finally{storeSaveFixture28=null;uiEventError15=null;}
  bool passed=checks.Count>0&&checks.Values.All(v=>v);
  File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,evidence,synthetic=true,ownerDataTouched=false,realNetworkTested=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
 }
}
