using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunFlowSmoke(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var checks=new Dictionary<string,bool>();var before=new Rovunet.Platform.WinInetProxy().Read();
        var direct=Enumerable.Range(1,7).Select(i=>Importer.Single($"socks5://127.0.0.1:{20000+i}#Route%20{i}")).ToList();
        var dns=Importer.Single(ManualProfiles.Dns("dnstt","DNS route","t.example.invalid","127.0.0.1:1","tcp",new string('a',64),"127.0.0.1","22","fixture","fixture"));
        var all=direct.Append(dns).ToList();foreach(var p in all){p.CollectionId="flow";p.CollectionName="Collection";}
        data.Profiles=all;data.SelectedId=direct[0].Id;data.ExpandedCollections["flow"]=true;data.LatencyUrl="https://example.com/";
        try{
            L.Language="en";data.Theme="light";Width=520;Height=900;ApplyTheme();Build();await Idle();
            IntPtr handle=new WindowInteropHelper(this).Handle;
            var parent=Dialog("SETTINGS",500,800);parent.UsePageChrome();var panel=new StackPanel();var input=new TextBox{Text="Unsaved draft",Margin=new(0,160,0,0)};panel.Children.Add(input);for(int i=0;i<35;i++)panel.Children.Add(Text("Row "+i));parent.Content=panel;parent.Show();await Idle();
            checks["page_uses_main_hwnd"]=NavigationDepth==1&&new WindowInteropHelper(parent).Handle==IntPtr.Zero&&Window.GetWindow(input)==this;
            input.Focus();parent.RestoreScroll(120);await Idle();double offset=parent.ScrollOffset;
            var child=new DialogWindow(parent,L.Text("MORE"),500,700);child.UsePageChrome();child.Content=new TextBox{Text="Nested page"};child.Show();await Idle();
            checks["nested_page_no_native_window"]=NavigationDepth==2&&new WindowInteropHelper(child).Handle==IntPtr.Zero&&new WindowInteropHelper(this).Handle==handle;
            Width=1040;await Idle();checks["resize_keeps_page_and_draft"]=NavigationDepth==2&&input.Text=="Unsaved draft";
            child.Close();await Idle();checks["back_restores_parent_scroll"]=NavigationDepth==1&&Math.Abs(parent.ScrollOffset-offset)<2&&input.Text=="Unsaved draft";
            checks["back_restores_parent_focus"]=Keyboard.FocusedElement==input;
            parent.Close();await Idle();checks["back_restores_home_and_wide_menu"]=NavigationDepth==0&&contextHost.IsVisible&&new WindowInteropHelper(this).Handle==handle&&DialogWindow.OpenPages.Count==0;
            Width=520;Build();await Idle();
            using(var sink=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0))){
                string endpoint=sink.Client.LocalEndPoint!.ToString()!;var scanProfile=Importer.Single(ManualProfiles.Dns("dnstt","Cancel fixture","t.example.invalid",endpoint,"udp",new string('a',64),"127.0.0.1","22","fixture","fixture"));var scanner=BuildResolverFinder(scanProfile);scanner.Show();await Idle();
                Visuals<ComboBox>(scanner.PageVisual).Single(b=>Equals(b.Tag,"SCAN_MODE")).SelectedValue="advanced";Visuals<ComboBox>(scanner.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SOURCE")).SelectedValue="input";
                foreach(var field in Visuals<TextBox>(scanner.PageVisual)){if(Equals(field.Tag,"SCAN_INPUT"))field.Text=endpoint;if(Equals(field.Tag,"SCAN_LIMIT")||Equals(field.Tag,"SCAN_WORKERS"))field.Text="1";if(Equals(field.Tag,"SCAN_TIMEOUT"))field.Text="10000";}
                var start=Visuals<Button>(scanner.PageVisual).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==L.Text("START_TEST"));start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));scanner.Close();
                checks["busy_back_waits_or_finishes_cleanup_synchronously"]=(!start.IsEnabled&&scanner.IsVisible&&NavigationDepth==1)||(!scanner.IsVisible&&NavigationDepth==0);
                var until=DateTime.UtcNow.AddSeconds(10);while(scanner.IsVisible&&DateTime.UtcNow<until)await Task.Delay(25);checks["busy_back_returns_after_cancellation"]=!scanner.IsVisible&&NavigationDepth==0&&DialogWindow.OpenPages.Count==0;
            }
            checks["readable_home_text"]=Visuals<TextBlock>((DependencyObject)Content).Where(t=>t.IsVisible).All(t=>t.FontSize>=11&&t.FontWeight.ToOpenTypeWeight()>=400);
            checks["hero_power_stays_outside_scrolling_list"]=!Ancestor(connect,mainScroll)&&heroCard19.IsVisible;
            Point power=connect.TranslatePoint(new Point(connect.ActualWidth/2,connect.ActualHeight/2),this);var hit=InputHitTest(power) as DependencyObject;
            checks["hero_power_center_is_clickable"]=hit!=null&&Ancestor(hit,connect);
            mainScroll.ScrollToBottom();await Idle();Point end=rows[^1].Tile.TranslatePoint(new Point(0,rows[^1].Tile.ActualHeight),this);
            checks["last_path_scrolls_above_fixed_status"]=end.Y<=connectionStrip.TranslatePoint(new Point(),this).Y+1;
            // Floating action buttons were removed; the list now runs to the status bar.
            mainScroll.ScrollToTop();await Idle();SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"home-transparent-en-light.png"));
            var titleScroller=Visuals<MarqueeLine>(rows[0].Tile).First();Wheel(titleScroller);await Idle();checks["wheel_over_short_name_scrolls_main_list"]=mainScroll.VerticalOffset>0;
            mainScroll.ScrollToTop();await Idle();Wheel(StatusLine(rows[0].Result));await Idle();checks["wheel_over_short_status_scrolls_main_list"]=mainScroll.VerticalOffset>0;mainScroll.ScrollToTop();await Idle();

            using(var trayFixture=new TrayIcon(()=>{},()=>{},()=>{},false)){
                tray=trayFixture;lastError="";SetState(new(){State="connected"});checks["connected_tray_green"]=tray.State=="connected"&&TrayIcon.ColorFor(tray.State)=="#19B879";
                SetState(new(){State="disconnected",Error="CONNECT_FAILED"});checks["failed_tray_red"]=tray.State=="error"&&TrayIcon.ColorFor(tray.State)=="#E74C58";
                lastError="";SetState(new());checks["disconnect_tray_light_lavender"]=tray.State=="disconnected"&&TrayIcon.ColorFor(tray.State)=="#D5C6FF";
                SetState(new(){State="connecting"});checks["connecting_tray_distinct"]=tray.State=="connecting"&&TrayIcon.ColorFor(tray.State)=="#E8AD46";
                SetState(new(){State="disconnected",Error="CANCELLED"});checks["manual_cancel_tray_neutral"]=tray.State=="disconnected";
                foreach(string status in new[]{"connected","error","disconnected"}){using var icon=TrayIcon.Render(status);using var bitmap=icon.ToBitmap();int pixels=0;for(int x=0;x<bitmap.Width;x++)for(int y=0;y<bitmap.Height;y++)if(bitmap.GetPixel(x,y).A>200)pixels++;checks["tray_raster_"+status]=bitmap.Width==32&&bitmap.Height==32&&pixels>150;bitmap.Save(Path.Combine(Path.GetDirectoryName(report)!,"tray-"+status+".png"));}
                tray=null;
            }
            lastError="";SetState(new());networkFixtureMode=true;
            data.Measurements[direct[1].Id]=new(){Verified=true,ElapsedMs=10,SampledAt=DateTimeOffset.UtcNow};
            data.Measurements[direct[0].Id]=new(){Verified=true,ElapsedMs=100,SampledAt=DateTimeOffset.UtcNow};
            data.Measurements[dns.Id]=new(){Verified=true,ElapsedMs=1,SampledAt=DateTimeOffset.UtcNow.AddMinutes(-2)};measurements[dns.Id]=new(){Verified=true,ElapsedMs=1};
            var order=new List<string>();var gate=new TaskCompletionSource<AgentReply>();
            collectionProbeFixture=(p,token)=>{order.Add(p.Id);return order.Count==1?gate.Task:Task.FromResult(new AgentReply{Verified=true,ElapsedMs=20});};
            groups[0].Test.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
            checks["group_click_stays_on_home"]=busy&&NavigationDepth==0&&DialogWindow.OpenPages.Count==0&&groups[0].Progress.IsVisible;
            checks["group_progress_on_same_card"]=groups[0].Summary.Text.Contains("0/")&&groups[0].Test.IsEnabled;
            checks["stop_test_is_explicit_visible_touch_button"]=groups[0].Stop.IsVisible&&groups[0].Stop.IsEnabled&&groups[0].Stop.Content?.ToString()==L.Text("STOP_TEST")&&groups[0].Stop.ActualHeight>=44;
            checks["child_cards_are_narrower_with_approved_fixed_height"]=rows.All(r=>r.Tile.ActualWidth<=groups[0].Header.ActualWidth-20&&r.Tile.ActualHeight==84);
            checks["child_status_has_readable_single_line_viewport"]=rows.All(r=>StatusLine(r.Result).ActualWidth>=120&&StatusLine(r.Result).ActualHeight>=20&&r.Result.TextWrapping==TextWrapping.NoWrap);
            checks["fast_verified_direct_first"]=order.SequenceEqual(new[]{direct[1].Id});
            checks["old_ping_visible_in_gray_while_waiting"]=rows.Single(r=>r.Profile.Id==dns.Id) is var oldRow&&oldRow.Timing.IsVisible&&oldRow.Timing.Text=="1 ms"&&oldRow.Timing.Foreground==Muted;
            SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"group-inline-en-light.png"));
            gate.SetResult(new(){Verified=true,ElapsedMs=10});await collectionTask!;await Idle();
            checks["direct_success_skips_dns"]=order.Count==direct.Count&&!order.Contains(dns.Id)&&collectionRuns["flow"].Pending[dns.Id]=="GROUP_DNS_SKIPPED";
            checks["inline_result_counts_only_tested"]=collectionRuns["flow"].Passed==direct.Count&&collectionRuns["flow"].Completed==direct.Count&&!busy;
            checks["visible_fast_success_first_dns_not_pinned"]=groups[0].Paths.Children[0]==rows.Single(r=>r.Profile.Id==direct[1].Id).Tile&&groups[0].Paths.Children[^1]==rows.Single(r=>r.Profile.Id==dns.Id).Tile&&data.Profiles[0].Id==direct[1].Id;
            checks["new_ping_green_old_ping_gray"]=rows.Single(r=>r.Profile.Id==direct[1].Id).Timing.Foreground==Green&&rows.Single(r=>r.Profile.Id==dns.Id).Timing.Foreground==Muted;
            SortGroup(all,false);await Idle();checks["manual_sort_new_round_beats_lower_old_ping"]=data.Profiles[0].Id==direct[1].Id&&data.Profiles[^1].Id==dns.Id;
            mainScroll.ScrollToBottom();await Idle();SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"new-green-old-gray-en.png"));
            order.Clear();collectionProbeFixture=(p,token)=>{order.Add(p.Id);return Task.FromResult(new AgentReply{Verified=p.Id==dns.Id,Error=p.Id==dns.Id?"":"CONNECT_FAILED",ElapsedMs=30});};
            StartCollectionTest(all,1,false);await collectionTask!;
            checks["all_direct_fail_then_dns"]=order.Count==all.Count&&order[^1]==dns.Id&&collectionRuns["flow"].Passed==1&&collectionRuns["flow"].Completed==all.Count;
            order.Clear();collectionProbeFixture=async(p,token)=>{order.Add(p.Id);await Task.Delay(Timeout.Infinite,token);return new();};
            StartCollectionTest(all,1,false);await Idle();groups[0].Stop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));checks["stop_button_shows_cleanup_in_progress"]=groups[0].Stop.Content?.ToString()==L.Text("CANCELLING")&&!groups[0].Stop.IsEnabled;await collectionTask!.WaitAsync(TimeSpan.FromSeconds(3));
            checks["same_card_cancel_stops_without_dns"]=collectionRuns["flow"].Cancelled&&order.Count==1&&!order.Contains(dns.Id)&&!busy&&NavigationDepth==0;
            string prior=JsonSerializer.Serialize(data.Measurements);collectionProbeFixture=(_,_)=>Task.FromResult(new AgentReply{ElapsedMs=2});
            StartCollectionTest(all,1,true);await collectionTask!;checks["tcp_does_not_write_https_measurements"]=JsonSerializer.Serialize(data.Measurements)==prior;
            collectionProbeFixture=(_,_)=>Task.FromResult(new AgentReply{State="trust",Error="SSH_TRUST_REQUIRED"});StartCollectionTest(all,1,false);await collectionTask!;
            checks["group_trust_never_opens_prompt_or_passes"]=NavigationDepth==0&&DialogWindow.OpenPages.Count==0&&collectionRuns["flow"].Passed==0&&data.SshKeys.Count==0;
            var slip=Importer.Single(ManualProfiles.Dns("slipstream","Slip","t.example.invalid","127.0.0.1:1","udp","","127.0.0.1","22","fixture","fixture"));
            var rejected=await ProbeCollectionProfile(slip,data.LatencyUrl,CancellationToken.None);checks["missing_slip_certificate_stays_inline"]=rejected.Error=="SLIP_CERT_REQUIRED"&&NavigationDepth==0;
            checks["single_main_hwnd_after_all_actions"]=new WindowInteropHelper(this).Handle==handle&&Application.Current.Windows.Cast<Window>().Count(w=>new WindowInteropHelper(w).Handle!=IntPtr.Zero)==1;
            collectionProbeFixture=(p,token)=>Task.FromResult(new AgentReply{Error="PROBE_TIMEOUT"});StartCollectionTest(all,1,false);await collectionTask!;await Idle();
            checks["site_timeout_is_red_and_still_inconclusive"]=rows.All(r=>r.Result.Foreground==Red&&r.Result.Text.Contains(L.Text("TEST_UNCONFIRMED")));
            checks["inconclusive_never_becomes_fake_ping"]=rows.All(r=>r.Timing.Text=="—")&&data.Measurements.Values.All(m=>!m.Verified);
            collectionProbeFixture=(p,token)=>Task.FromResult(new AgentReply{Error="SSH_TRUST_REQUIRED"});StartCollectionTest(all,1,false);await collectionTask!;await Idle();checks["unapproved_ssh_identity_is_pending_not_failed"]=rows.All(r=>r.Result.Foreground==Amber&&r.Result.Text==L.Text("TEST_IDENTITY_PENDING"));
            collectionProbeFixture=(p,token)=>Task.FromResult(new AgentReply{Error="SSH_KEY_CHANGED"});StartCollectionTest(all,1,false);await collectionTask!;await Idle();checks["changed_ssh_identity_still_fails_red"]=rows.All(r=>r.Result.Foreground==Red);
            var attemptsById=new Dictionary<string,int>();collectionProbeFixture=(p,token)=>{int n=attemptsById.GetValueOrDefault(p.Id)+1;attemptsById[p.Id]=n;return Task.FromResult(new AgentReply{Verified=n!=2,Error=n==2?"PROBE_TIMEOUT":"",ElapsedMs=22});};StartCollectionTest(all,3,false);await collectionTask!;await Idle();checks["partial_success_is_red_with_real_pass_count"]=rows.All(r=>r.Result.Foreground==Red&&r.Result.Text.Contains("2/3")&&r.Timing.Text=="—")&&data.Measurements.Values.All(m=>!m.Verified&&m.Passes==2&&m.Attempts==3);
            collectionProbeFixture=(p,token)=>Task.FromResult(new AgentReply{Verified=true,ProbeFallbackUsed=true,ElapsedMs=21});StartCollectionTest(all,1,false);await collectionTask!;await Idle();checks["alternative_https_success_green_and_labeled"]=rows.Where(r=>!ProfileEditing.IsDns(r.Profile)).All(r=>r.Result.Foreground==Green&&r.Result.Text==L.Text("TEST_OK_FALLBACK"))&&collectionRuns["flow"].Pending[dns.Id]=="GROUP_DNS_SKIPPED";
            await Idle();var fixedHeader=groups[0].Header.ActualHeight;var fixedChild=rows[0].Tile.ActualHeight;rows[0].Result.Text=string.Join(' ',Enumerable.Repeat("Full long diagnostic message",25));UpdateLayout();
            checks["long_text_never_resizes_cards"]=groups[0].Header.ActualHeight==fixedHeader&&rows[0].Tile.ActualHeight==fixedChild&&fixedChild==84;
            await Idle();checks["long_status_remains_in_moving_single_line_viewport"]=StatusLine(rows[0].Result).Overflowing&&rows[0].Result.TextWrapping==TextWrapping.NoWrap;
            var sizing=rows[0].Profile;sizing.Name=new string('W',180);sizing.CollectionName=new string('W',100);BuildCards();await Idle();checks["long_names_never_resize_cards"]=groups[0].Header.ActualHeight==fixedHeader&&rows.All(r=>r.Tile.ActualHeight==84);
            Width=420;Build();await Idle();double narrow=rows[0].Tile.ActualWidth;Width=760;Build();await Idle();checks["only_window_width_scales_card_width"]=rows[0].Tile.ActualWidth>narrow&&groups[0].Header.ActualHeight==fixedHeader&&rows.All(r=>r.Tile.ActualHeight==84);Width=520;Build();await Idle();
            checks["proxy_unchanged"]=before==new Rovunet.Platform.WinInetProxy().Read();
            L.Language="fa";data.Theme="dark";ApplyTheme();Build();await Idle();SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"group-inline-fa-dark.png"));
        }catch(Exception e){checks["failure_"+e.GetType().Name]=false;}
        finally{CancelCollectionTest();collectionProbeFixture=null;networkFixtureMode=false;tray=null;lastError="";SetState(new());}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);using var output=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(output,new{suite="WPF one-window navigation, readable fonts, transparent controls, inline group ordering and tray states",external_network_tested=false,synthetic_profiles=true,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        static void Wheel(UIElement element){var args=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent};element.RaiseEvent(args);if(!args.Handled){args.RoutedEvent=Mouse.MouseWheelEvent;element.RaiseEvent(args);}}
        static bool Ancestor(DependencyObject hit,DependencyObject target){for(var p=hit;p!=null;p=VisualTreeHelper.GetParent(p))if(p==target)return true;return false;}
        static MarqueeLine StatusLine(DependencyObject child){for(var parent=VisualTreeHelper.GetParent(child);parent!=null;parent=VisualTreeHelper.GetParent(parent))if(parent is MarqueeLine line)return line;throw new InvalidOperationException("Missing current status viewport");}
    }
}
