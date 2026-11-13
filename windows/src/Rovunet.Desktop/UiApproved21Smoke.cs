using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunApproved21Smoke(string report)
    {
        var checks = new Dictionary<string, bool>(); var layout = new List<object>();
        string folder = Path.GetDirectoryName(report)!;
        async Task Idle() { await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout(); await Task.Delay(60); }
        Rect Bounds(FrameworkElement element)
        {
            var a=element.PointToScreen(new Point());var b=element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));
            return new Rect(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
        }
        bool Overlap(Rect a, Rect b) { a.Intersect(b); return !a.IsEmpty && a.Width > 1 && a.Height > 1; }
        try
        {
            CheckMarqueeAspect21(checks,folder);
            checks["moving_text_enabled_by_default"] = new Rovunet.Platform.UserData().MarqueeMotion == "on";
            foreach(double scale in new[]{1d,1.25,1.5,2})foreach(double origin in new[]{0d,-1920d}){
                var work=new UiPolicy.Frame(origin,0,1366,728);var fit=UiPolicy.FitWindow(new(origin-100,-60,1920,1200),work,scale);
                checks["monitor_fit_"+scale+"_"+origin]=fit.X>=work.X&&fit.Y>=0&&fit.X+fit.Width<=work.X+work.Width&&fit.Y+fit.Height<=work.Height;
            }
            var snapped=new UiPolicy.Frame(0,0,683,728);checks["fitting_or_snapped_window_not_shifted"]=UiPolicy.FitWindow(snapped,new(0,0,1366,728),1)==snapped;
            MinWidth = 300; MinHeight = 400;
            const string firstName = "اروپا — مسیر اصلی با نام بلند برای نمایش کامل نوشته در کادر ثابت و بدون تغییر اندازهٔ کارت";
            var first = Importer.Single("socks5://gateway-primary.example.invalid:1080#" + Uri.EscapeDataString(firstName));
            var second = Importer.Single("socks5://gateway-backup.example.invalid:1081#" + Uri.EscapeDataString("Europe backup connection — a deliberately long descriptive profile name that must move within its fixed field"));
            var third = Importer.Single("socks5://gateway-third.example.invalid:1082#Short");
            string source = Importer.Fingerprint("https://subscription.example.invalid/approved21");
            foreach (var p in new[] { first, second, third }) { p.SourceId = source; p.CollectionId = "sub:" + source; p.CollectionName = "اشتراک شخصی"; }
            var manual = Importer.Single("socks5://manual.example.invalid:1083#Manual"); manual.CollectionId = "manual21"; manual.CollectionName = "مسیرهای دستی";
            data.Profiles = [first, second, third, manual]; data.Subscriptions = [new() { Id = source, Url = "https://subscription.example.invalid/approved21", Name = "اشتراک شخصی" }];
            data.SelectedId = first.Id; activeProfile = first; data.ExpandedCollections["sub:" + source] = true;
            data.MarqueeMotion = "on";
            var now = DateTimeOffset.UtcNow;
            statuses[source] = SubscriptionUsage20.Parse("upload=900000000;download=6000000000;total=30000000000;expire=" + now.AddDays(27).ToUnixTimeSeconds(), "", now)!.Snapshot(source);
            foreach (var p in new[] { first, second, third })
            {
                long ping = p == first ? 186 : p == second ? 241 : 631;
                data.Measurements[p.Id] = new() { Verified = true, ElapsedMs = ping, SampledAt = p == third ? now.AddDays(-1) : now };
                measurements[p.Id] = new() { Verified = true, ElapsedMs = ping };
                if(p!=third)liveMeasurements.Add(p.Id);
            }
            foreach (string language in new[] { "fa", "en" })
            foreach (string theme in new[] { "dark", "light", "amoled" })
            foreach (double width in new[] { 480d, 1040d })
            {
                L.Language = data.Language = language; data.Theme = theme; Width = width; Height = 900;
                ApplyTheme(); Build(); SetState(new() { State = "connected", Verified = true }); await Idle();
                string key = language + "-" + theme + "-" + width;
                homeSearchAction30.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks[key+"_search_open_empty_has_close"]=search.Visibility==Visibility.Visible&&System.Windows.Automation.AutomationProperties.GetItemStatus(homeSearchAction30)=="close";
                search.Text="NO_MATCH_SEARCH30";homeSearchAction30.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                checks[key+"_search_enter_applies_and_stays_open"]=search.Visibility==Visibility.Visible&&System.Windows.Automation.AutomationProperties.GetItemStatus(homeSearchAction30)=="apply"&&groups.All(g=>g.Card.Visibility==Visibility.Collapsed);
                search.Clear();homeSearchAction30.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
                checks[key+"_search_empty_closes_and_restores"]=search.Visibility==Visibility.Collapsed&&System.Windows.Automation.AutomationProperties.GetItemStatus(homeSearchAction30)=="open"&&groups.Any(g=>g.Card.Visibility==Visibility.Visible);
                var flags = Bounds(header21.Flags);var contentBounds=Bounds((FrameworkElement)Content);double center=contentBounds.X+contentBounds.Width/2;
                checks[key + "_flags_center_entire_window"] = Math.Abs(flags.X + flags.Width / 2 - center) < 1;
                checks[key + "_header_slots_do_not_overlap"] = !Overlap(flags, Bounds(header21.Leading)) && !Overlap(flags, Bounds(header21.Trailing));
                checks[key + "_sidebar_matches_layout"] = contextHost.IsVisible == (width >= ContextThreshold);
                checks[key + "_fixed_profile_height"] = rows.Where(r=>r.Tile.IsVisible).All(r => Math.Abs(r.Tile.ActualHeight - 79) < 1);
                checks[key + "_subcards_narrower_than_collection"] = rows.Take(3).All(r => r.Tile.ActualWidth < groups[0].Header.ActualWidth - 10);
                checks[key + "_text_is_single_line"] = Visuals<TextBlock>(rows[0].Tile).All(t => t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None);
                checks[key + "_power_visible_and_touchable"] = connect.ActualWidth >= 44 && heroRingScale19.ActualWidth >= 64 && Bounds(heroRingScale19).Bottom <= Bounds(connectionStrip).Top;
                var mark = Visuals<FrameworkElement>((DependencyObject)Content).Single(v => Equals(v.Tag, "ROVUNET_BRAND_MARK"));
                var brandHost = width >= ContextThreshold ? contextHost : (FrameworkElement)Content;
                checks[key + "_brand_physically_left_in_its_frame"] = Bounds(mark).Left - Bounds(brandHost).Left is >= 0 and < 50;
                checks[key + "_quota_real_sample_values"] = groups[0].Quota!.Used.Text.Contains("30") || groups[0].Quota!.Used.Text.Contains("۳۰");
                checks[key + "_manual_has_no_subscription_quota"] = groups.Single(g => g.Key == "manual21").Quota!.Root.Visibility == Visibility.Collapsed;
                layout.Add(new { key, contentWidth = ((FrameworkElement)Content).ActualWidth, flagCenter = flags.X + flags.Width / 2, targetCenter = center, splitHeader = header21.Split, viewport = mainScroll.ViewportHeight });
                if (language == "fa") SavePage((FrameworkElement)Content, Path.Combine(folder, "approved21-" + key + ".png"));
            }

            foreach (string language in new[] { "fa", "en", "ru", "zh-CN" })
            foreach (double width in new[] { 340d, 480d, 700d, 1040d })
            {
                L.Language = data.Language = language; Width = width; Height = 700; ApplyTheme(); Build(); await Idle();
                var flags = Bounds(header21.Flags);var contentBounds=Bounds((FrameworkElement)Content);string key = "small-" + language + "-" + width;
                checks[key + "_flags_center_without_overlap"] = Math.Abs(flags.X + flags.Width / 2 - contentBounds.X-contentBounds.Width / 2) < 1 && !Overlap(flags, Bounds(header21.Leading)) && !Overlap(flags, Bounds(header21.Trailing));
                checks[key + "_list_and_connect_remain_available"] = mainScroll.ViewportHeight > 100 && Bounds(connect).Bottom < Bounds(connectionStrip).Top;
                checks[key + "_actions_stay_inside_card"] = rows.All(r => Visuals<Button>(r.Tile).All(b => Bounds(b).Left >= Bounds(r.Tile).Left - 1 && Bounds(b).Right <= Bounds(r.Tile).Right + 1));
            }
            L.Language = data.Language = "fa"; data.Theme = "dark"; Width = 480; Height = 700; ApplyTheme(); Build(); await Idle();
            mainScroll.ScrollToBottom(); await Idle();
            checks["scroll_collapses_header_and_retains_power"] = Math.Abs(heroCard19.ActualHeight-HeroCompact19)<1 && Bounds(connect).Bottom<Bounds(connectionStrip).Top;
            double endOffset=mainScroll.VerticalOffset; await Idle();
            checks["collapsed_header_does_not_oscillate_scroll"] = Math.Abs(mainScroll.VerticalOffset-endOffset)<1;
            mainScroll.ScrollToTop(); await Idle();
            checks["scroll_top_restores_expanded_header"] = Math.Abs(heroCard19.ActualHeight-HeroExpanded19)<1;
            Height = 900; ApplyTheme(); Build(); await Idle();
            var rtl = Visuals<MarqueeLine>(rows.First(r => r.Profile == first).Tile).First();
            var ltr = Visuals<MarqueeLine>(rows.First(r => r.Profile == second).Tile).First();
            double beforeRtl = rtl.Translation, beforeLtr = ltr.Translation, fixedHeight = rows[0].Tile.ActualHeight;
            SavePage((FrameworkElement)Content, Path.Combine(folder, "marquee-before.png"));
            await Task.Delay(3100); await Idle();
            checks["rtl_letters_move_inside_fixed_field"] = rtl.Overflowing && rtl.Translation > beforeRtl + 1;
            checks["ltr_letters_move_inside_fixed_field"] = ltr.Overflowing && ltr.Translation < beforeLtr - 1;
            checks["moving_text_does_not_resize_card"] = rows[0].Tile.ActualHeight == fixedHeight;
            SavePage((FrameworkElement)Content, Path.Combine(folder, "marquee-after.png"));
            var shortLine = Visuals<MarqueeLine>(rows.Single(r => r.Profile == third).Tile).First();
            checks["short_name_stays_still"] = !shortLine.Overflowing && !shortLine.Moving;
            data.MarqueeMotion = "off"; MarqueeLine.RefreshAll((DependencyObject)Content); await Idle();
            checks["motion_can_be_disabled"] = Visuals<MarqueeLine>((DependencyObject)Content).All(m => !m.Moving && Math.Abs(m.Translation) < .1);
            data.MarqueeMotion = "system"; MarqueeLine.RefreshAll((DependencyObject)Content); await Idle();
            checks["system_motion_preference_is_respected"] = SystemParameters.ClientAreaAnimation || Visuals<MarqueeLine>((DependencyObject)Content).All(m => !m.Moving);
            var settings = Dialog("SETTINGS"); settings.Content = BuildSettingsPanel(() => settings.Close()); settings.Show(); await Idle();
            var choice = Visuals<ComboBox>(settings.PageVisual).Single(c => Equals(c.Tag, "TEXT_MOTION"));
            checks["motion_setting_present"] = choice.SelectedIndex == 0;
            choice.SelectedIndex=2; await Idle();checks["settings_disable_motion"] = data.MarqueeMotion=="off" && !MarqueeEnabled21;
            choice.SelectedIndex=1; await Idle();checks["settings_enable_motion"] = data.MarqueeMotion=="on" && MarqueeEnabled21;
            settings.Close();
            activeProfile=third;state=new(){State="connected"};liveLatency=321;latencySampled=DateTimeOffset.UtcNow;RefreshCards();var connectedRow=rows.Single(r=>r.Profile==third);var connectedGroup=groups.Single(g=>g.Members.Contains(third));checks["active_card_is_first_with_green_live_latency"]=ReferenceEquals(connectedGroup.Paths.Children[0],connectedRow.Tile)&&connectedRow.Timing.Text=="321 ms"&&Equals(connectedRow.Timing.Foreground,Green);liveLatency=null;RefreshCards();checks["failed_live_latency_does_not_reuse_old_ping"]=connectedRow.Timing.Text=="—";
        }
        catch (Exception error) { checks["exception_" + error.GetType().Name] = false; File.WriteAllText(Path.Combine(folder, "approved21-error.txt"), error.ToString()); }
        bool passed = checks.Count > 0 && checks.Values.All(v => v);
        File.WriteAllText(report, JsonSerializer.Serialize(new { suite = "Approved21 actual WPF layout, overflow movement and preferences", syntheticData = true, connectionStateSimulated = true, externalNetworkTested = false, hardwareRenderingTier = RenderCapability.Tier >> 16, systemClientAreaAnimation = SystemParameters.ClientAreaAnimation, layout, checks, passed }, new JsonSerializerOptions { WriteIndented = true }));
        return passed;
    }

    // Compare real rendered ink, not just the configured brush properties. The
    // repeated copy must retain the original glyph width/height and baseline.
    static void CheckMarqueeAspect21(Dictionary<string,bool> checks,string folder)
    {
        foreach(string language in new[]{"en","fa"})
        {
            var direction=language=="fa"?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
            var text=new TextBlock{Text=language=="fa"?"متن فارسی برای بررسی نسبت طبیعی حروف و جلوگیری از کشیدگی نوشته":"Natural letter proportions — the moving text must keep the original font shape",FontFamily=Typography.Family(language),FontSize=16,Foreground=Brushes.Black,FlowDirection=direction};
            var line=new MarqueeLine(text,28,direction,()=>true){Width=190};
            line.Measure(new Size(190,28));line.Arrange(new Rect(0,0,190,28));line.UpdateLayout();
            (Int32Rect bounds,int pixels) Capture(string phase)
            {
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(190,28,96,96,PixelFormats.Pbgra32);bitmap.Render(line);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(folder,"glyph-"+language+"-"+phase+".png")))png.Save(output);
                byte[] pixels=new byte[190*28*4];bitmap.CopyPixels(pixels,190*4,0);int left=190,top=28,right=-1,bottom=-1,count=0;
                for(int y=0;y<28;y++)for(int x=0;x<190;x++)if(pixels[(y*190+x)*4+3]>48){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);count++;}
                return right<left?(Int32Rect.Empty,0):(new(left,top,right-left+1,bottom-top+1),count);
            }
            var original=Capture("original");
            ((TranslateTransform)((FrameworkElement)line.Children[0]).RenderTransform).X=(direction==FlowDirection.RightToLeft?1:-1)*(line.NaturalWidth+32);
            var repeated=Capture("repeat");
            checks[language+"_loop_copy_preserves_glyph_proportions"]=original.pixels>100&&repeated.pixels>100&&Math.Abs(original.bounds.Width-repeated.bounds.Width)<=1&&Math.Abs(original.bounds.Height-repeated.bounds.Height)<=1&&Math.Abs(original.bounds.Y-repeated.bounds.Y)<=1&&Math.Abs(original.pixels-repeated.pixels)/(double)original.pixels<.08;
            // Recreate the previous brush only on this disposable diagnostic.
            var brush=(VisualBrush)Visuals<System.Windows.Shapes.Rectangle>(line).Single().Fill;
            brush.ViewboxUnits=brush.ViewportUnits=BrushMappingMode.RelativeToBoundingBox;
            brush.Viewbox=brush.Viewport=new Rect(0,0,1,1);brush.Stretch=Stretch.Fill;
            var legacy=Capture("legacy-fill");
            File.WriteAllText(Path.Combine(folder,"glyph-"+language+"-metrics.json"),JsonSerializer.Serialize(new{language,original=new{original.bounds,original.pixels},correctedRepeat=new{repeated.bounds,repeated.pixels},legacyFillDiagnostic=new{legacy.bounds,legacy.pixels}},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}

