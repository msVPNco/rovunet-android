using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Visuals<T>(child)) yield return nested;
        }
    }

    void InteractionSmoke(Dictionary<string, bool> checks, string report)
    {
        // Called only by offline fixture: Save() is disabled and no external actions are invoked.
        var originalUrl = data.LatencyUrl; var originalTray = data.CloseToTray;
        var originalExperimental = data.IncludeExperimental; var originalTheme = data.Theme;
        string originalLanguage = L.Language;
        double width = Width, height = Height;
        try
        {
            L.Language = "fa"; SetState(new()); Build(); UpdateLayout();
            var settings = BuildSettingsPanel(() => { });
            var host = new Border { Background = Tone("Bg"), Child = settings, Width = 660 };
            host.Measure(new Size(660, double.PositiveInfinity)); host.Arrange(new Rect(0, 0, 660, host.DesiredSize.Height)); host.UpdateLayout();
            var input = Visuals<TextBox>(host).Single(t => Equals(t.Tag,"LATENCY_URL"));
            var save = Visuals<Button>(host).Single(b => b.Content?.ToString() == L.Text("SAVE"));
            input.Text = "https://example.invalid/fixture"; save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["settings_https_save_updates_model"] = data.LatencyUrl == input.Text;
            var feedback = Visuals<TextBlock>(host).Single(t => t.Text == L.Text("SETTINGS_SAVED"));
            input.Text = "http://example.invalid/rejected";
            checks["editing_hides_stale_save_confirmation"] = feedback.Visibility == Visibility.Collapsed;
            save.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["invalid_url_preserves_model_and_connection"] = data.LatencyUrl == "https://example.invalid/fixture" && state.State == "disconnected" && feedback.Visibility == Visibility.Visible && feedback.Text != L.Text("SETTINGS_SAVED");
            var trayOption = Visuals<CheckBox>(host).Single(b => b.Content?.ToString() == L.Text("CLOSE_TO_TRAY"));
            trayOption.IsChecked = !originalTray;
            checks["tray_setting_updates_model"] = data.CloseToTray != originalTray;
            var experimentalOption = Visuals<CheckBox>(host).Single(b => b.Content?.ToString() == L.Text("INCLUDE_EXPERIMENTAL"));
            experimentalOption.IsChecked = !originalExperimental;
            checks["experimental_setting_updates_model"] = data.IncludeExperimental != originalExperimental;
            bool closed = false;
            var themePanel = BuildSettingsPanel(() => closed = true);
            host.Child = themePanel; host.Measure(new Size(660, double.PositiveInfinity)); host.Arrange(new Rect(0, 0, 660, host.DesiredSize.Height)); host.UpdateLayout();
            Visuals<Button>(host).Single(b => b.Content?.ToString() == L.Text("LIGHT_THEME")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["theme_action_applies_and_closes"] = data.Theme == "light" && !darkTheme && closed;

            Width = 760; Height = 600; Build(); UpdateLayout();
            checks["small_window_has_profile_viewport"] = mainScroll.ViewportHeight >= 100;
            checks["small_window_connect_is_visible"] = connect.TranslatePoint(new Point(), this).X >= 0 && connect.TranslatePoint(new Point(connect.ActualWidth, connect.ActualHeight), this).X <= ActualWidth;
            Screenshot((FrameworkElement)Content, "home-small", 1);

            foreach (var language in new[] { "fa", "en", "ru", "zh-CN" })
            {
                L.Language = language;
                foreach (double scale in new[] { 1.0, 1.5, 2.0 })
                {
                    // Render/layout simulation, not an OS DPI change or physical touch test.
                    var panel = BuildSettingsPanel(() => { });
                    var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, PanningMode = PanningMode.VerticalOnly };
                    var frame = new Border { Background = Tone("Bg"), Child = scroll, Width = 440, Height = 560, FlowDirection = L.Direction };
                    frame.Measure(new Size(440, 560)); frame.Arrange(new Rect(0, 0, 440, 560)); frame.UpdateLayout();
                    var last = Visuals<Button>(frame).Last(); scroll.ScrollToBottom(); frame.UpdateLayout();
                    Point bottom = last.TranslatePoint(new Point(last.ActualWidth, last.ActualHeight), scroll);
                    checks[$"settings_bottom_reachable_{language}_{scale}"] = bottom.Y <= scroll.ActualHeight + 1 && bottom.Y > 0;
                    checks[$"settings_touch_layout_{language}_{scale}"] = Visuals<Button>(frame).Where(b=>b.IsVisible).All(b => b.ActualWidth >= 44 && b.ActualHeight >= 44 && b.ActualWidth <= 440);
                    if (language == "fa") Screenshot(frame, "settings-small-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture), scale);
                }
            }
        }
        finally
        {
            data.LatencyUrl = originalUrl; data.CloseToTray = originalTray; data.IncludeExperimental = originalExperimental;
            data.Theme = originalTheme; L.Language = originalLanguage; Width = width; Height = height;
            ApplyTheme(); Build(); SetState(new());
        }

        void Screenshot(FrameworkElement visual, string name, double scale)
        {
            var pixels = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * scale), (int)Math.Ceiling(visual.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            pixels.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(pixels));
            using var stream = new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!, name + ".png"), FileMode.CreateNew); png.Save(stream);
        }
    }
}
