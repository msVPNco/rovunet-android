using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    // Offline UI fixture only. Never loads/saves the user's store or launches an Agent.
    public async Task<bool> RunUiSmoke(string report)
    {
        var checks = new Dictionary<string, bool>();
        void Check(string name, bool passed) => checks[name] = passed;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        try
        {
            Check("twenty_fixture_paths", rows.Count == 20);
            var before = rows[0].Tile; bool focused = rows[0].Select.Focus(); var focus = Keyboard.FocusedElement;
            BuildCards(); Check("refresh_retains_row_and_focus", focused && ReferenceEquals(focus, rows[0].Select) && ReferenceEquals(before, rows[0].Tile) && ReferenceEquals(focus, Keyboard.FocusedElement));
            mainScroll.ScrollToBottom(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); double scroll = mainScroll.VerticalOffset;
            var pinned = connectionStrip; double top = pinned.TranslatePoint(new Point(), this).Y;
            mainScroll.ScrollToTop(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); mainScroll.ScrollToBottom(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            Check("connection_card_stays_pinned", Math.Abs(pinned.TranslatePoint(new Point(), this).Y - top) < 1);
            Check("subscription_paths_have_edit_controls", rows.All(r => r.Edit.IsEnabled));
            RefreshCards(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); Check("refresh_keeps_scroll", Math.Abs(mainScroll.VerticalOffset - scroll) < 1);
            SetState(new() { State = "connecting" }); Check("connecting_has_cancel_and_progress", System.Windows.Automation.AutomationProperties.GetName(connect) == L.Text("CANCEL") && connectingProgress.Visibility == Visibility.Visible);
            busy=true;SetState(new(){State="connecting"});Check("collection_cancel_available_while_busy",groups.Single(g=>g.Members.Any(p=>p.Id==data.SelectedId)).Connect.IsEnabled);busy=false;
            SetState(new() { State = "connected", HttpPort = 21001, SocksPort = 21002 }); Check("ports_separate_and_copy_enabled", httpAddress.Text == "127.0.0.1:21001" && socksAddress.Text == "127.0.0.1:21002" && copyHttp.IsEnabled && copySocks.IsEnabled);
            foreach (string theme in new[] { "light", "dark" })
            {
                data.Theme = theme; ApplyTheme(); Build(); SetState(new() { State = "connected", HttpPort = 21001, SocksPort = 21002 }); UpdateLayout();
                Check("theme_" + theme, Tone("Bg") is SolidColorBrush brush && brush.Color == (Color)ColorConverter.ConvertFromString(theme == "dark" ? "#16263C" : "#F0F1FB"));
                Check("opaque_background_" + theme, Content is System.Windows.Controls.Grid root && root.Background is GradientBrush background && background.Opacity==1 && background.GradientStops.All(stop=>stop.Color.A==255));
                IEnumerable<DependencyObject> Descendants(DependencyObject parent)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = VisualTreeHelper.GetChild(parent, i); yield return child;
                        foreach (var nested in Descendants(child)) yield return nested;
                    }
                }
                var powerPath=connect.Content as System.Windows.Shapes.Path;
                double Lum(Color color){double Part(byte b){double v=b/255d;return v<=0.04045?v/12.92:Math.Pow((v+0.055)/1.055,2.4);}return .2126*Part(color.R)+.7152*Part(color.G)+.0722*Part(color.B);}
                bool contrast=false;if(powerPath?.Stroke is SolidColorBrush foreground && connect.Background is SolidColorBrush surface){double a=Lum(foreground.Color),b=Lum(surface.Color);contrast=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)>=3;}
                Check("power_icon_contrast_"+theme,contrast);
                Check("floating_icons_contrast_"+theme,Descendants((DependencyObject)Content).OfType<System.Windows.Controls.Button>().Where(b=>b.Width==58 && b.IsVisible).All(b=>{if(b.Content is not System.Windows.Shapes.Path icon || icon.Stroke is not SolidColorBrush ink || b.Background is not SolidColorBrush fill)return false;double a=Lum(ink.Color),c=Lum(fill.Color);return (Math.Max(a,c)+.05)/(Math.Min(a,c)+.05)>=3;}));
                Check("touch_home_buttons_" + theme, Descendants((DependencyObject)Content).OfType<System.Windows.Controls.Button>().Where(b => b.IsVisible).All(b => b.ActualHeight >= 44 && b.ActualWidth >= 44));
                void PanelImage(string name, System.Windows.Controls.StackPanel panel)
                {
                    var scroll = new System.Windows.Controls.ScrollViewer { Content = panel, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, PanningMode = System.Windows.Controls.PanningMode.VerticalOnly };
                    var frame = new System.Windows.Controls.Border { Background = Tone("Bg"), Child = scroll, Width = 660, Height = 680, FlowDirection = L.Direction, Style = null };
                    frame.Measure(new Size(660, 680)); frame.Arrange(new Rect(0, 0, 660, 680)); frame.UpdateLayout();
                    Check(name + "_sections_" + theme, panel.Children.OfType<System.Windows.Controls.Border>().Count() == 6);
                    Check(name + "_touch_buttons_" + theme, Descendants(frame).OfType<System.Windows.Controls.Button>().Where(b => b.IsVisible).All(b => b.ActualHeight >= 44 && b.ActualWidth >= 44));
                    if(name=="settings")Check(name+"_scrolls_"+theme,scroll.ExtentHeight>scroll.ViewportHeight);
                    else{scroll.ScrollToBottom();frame.UpdateLayout();var last=Descendants(frame).OfType<System.Windows.Controls.Button>().Last();Check(name+"_all_actions_reachable_"+theme,last.TranslatePoint(new Point(0,last.ActualHeight),scroll).Y<=scroll.ActualHeight+1);}
                    var pixels = new System.Windows.Media.Imaging.RenderTargetBitmap(660, 680, 96, 96, PixelFormats.Pbgra32); pixels.Render(frame);
                    var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(pixels));
                    using var output = new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, name + "-" + theme + ".png"), FileMode.CreateNew); png.Save(output);
                    if (name == "settings")
                    {
                        var pool = Descendants(frame).OfType<System.Windows.Controls.CheckBox>().Single(c => Equals(c.Content, L.Text("DNS_POOL_TITLE")));
                        scroll.ScrollToVerticalOffset(pool.TranslatePoint(new Point(), panel).Y - 100); frame.UpdateLayout();
                        var poolPixels = new System.Windows.Media.Imaging.RenderTargetBitmap(660, 680, 96, 96, PixelFormats.Pbgra32); poolPixels.Render(frame);
                        var poolPng = new System.Windows.Media.Imaging.PngBitmapEncoder(); poolPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(poolPixels));
                        using var poolOutput = new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "settings-dns-pool-" + theme + ".png"), FileMode.CreateNew); poolPng.Save(poolOutput);
                    }
                }
                PanelImage("settings", BuildSettingsPanel(() => { }));
                PanelImage("menu", BuildAppMenu(() => { }));
                var visual = (FrameworkElement)Content; var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1, (int)visual.ActualWidth), Math.Max(1, (int)visual.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var file = new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, "ui-fixture-" + theme + ".png"), FileMode.CreateNew); encoder.Save(file);
            }
            SetState(new()); Check("disconnected_clears_ports", httpAddress.Text == "—" && socksAddress.Text == "—" && !copyHttp.IsEnabled && !copySocks.IsEnabled);
            search.Text = "NO_MATCH_FIXTURE"; Check("search_hides_unmatched", groups.All(g => g.Card.Visibility == Visibility.Collapsed)); search.Clear();
            InteractionSmoke(checks, report);
            NavigationSmoke(checks);
            GlobalResolverSmoke(checks);
            SleepSmoke(checks, report);
            Width=1040; UpdateLayout();
            foreach (var language in new[] { "fa", "en", "ru", "zh-CN" })
            {
                L.Language = language; Build(); UpdateLayout();
                double side = sidebarContent.PointToScreen(new Point(sidebarContent.ActualWidth/2,0)).X; double body = homeContent.PointToScreen(new Point(homeContent.ActualWidth/2,0)).X;
                Check("context_panel_direction_" + language, Math.Abs(sidebarContent.ActualWidth-222)<1 && (language == "fa" ? side > body : side < body));
                Check("named_radio_" + language, rows.All(r => !string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(r.Select))));
            }
        }
        catch { checks["ui_fixture_unexpected_error"] = false; }
        bool passed = checks.Count > 0 && checks.Values.All(x => x);
        try
        {
            using var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, new { version = Version, fixture = "offline UI only; no network or proxy changes", windows = Environment.OSVersion.Version.ToString(), checks, passed }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return false; }
        return passed;
    }
}
