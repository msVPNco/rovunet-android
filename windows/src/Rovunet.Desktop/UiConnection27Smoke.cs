using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    // Exercise the loaded visual, not just TextBlock.Text: Canvas can leave the
    // previous glyph run on screen even though the state and foreground changed.
    internal async Task<bool> RunConnection27(string report)
    {
        var started = DateTimeOffset.UtcNow;
        var checks = new Dictionary<string, bool>();
        var samples = new List<object>();
        string folder = Path.GetDirectoryName(report)!;
        Directory.CreateDirectory(folder);
        try
        {
            data.MarqueeMotion = "off";
            var profile = Importer.Single("socks5://127.0.0.1:19988#Synthetic%20connection");
            data.Profiles = [profile]; data.SelectedId = profile.Id;
            foreach (string language in new[] { "en", "fa", "ru", "zh-CN" })
            foreach (string theme in new[] { "dark", "light", "amoled" })
            foreach (double width in new[] { 480d, 1120d })
            {
                L.Language = data.Language = language; data.Theme = theme; Width = width;
                activeProfile = null; SetState(new()); ApplyTheme(); Build(); await Idle18();
                string prefix = language + "_" + theme + "_" + width;
                var hero = Visuals<MarqueeLine>(heroCard19).Single(m => Equals(m.Tag, "HERO_TITLE_MARQUEE"));
                var footer = Visuals<MarqueeLine>(connectionStrip).Single(m => Equals(m.Tag, "FOOTER_TITLE_MARQUEE"));
                int step = 0;
                foreach (string next in new[] { "connecting", "connected", "disconnecting", "disconnected", "connected", "error", "connected", "disconnected" })
                {
                    activeProfile = next == "connected" ? profile : null;
                    SetState(new() { State = next, Verified = next == "connected", Error = next == "error" ? "AGENT_EXITED" : "" });
                    await Idle18();
                    string heroGlyphs = Characters(VisualTreeHelper.GetDrawing(heroTitle));
                    string footerGlyphs = Characters(VisualTreeHelper.GetDrawing(connectionText));
                    if (language == "en")
                        checks[prefix + "_" + step + "_" + next + "_actual_rendered_characters"] = heroGlyphs == heroTitle.Text && footerGlyphs == connectionText.Text;
                    if (language == "en" && theme == "dark" && width == 480 && step < 4)
                        SavePage((FrameworkElement)Content, Path.Combine(folder, "immediate-" + step + "-" + next + ".png"));
                    string beforeHero = Pixels(hero), beforeFooter = Pixels(footer);
                    double heroWidth = hero.NaturalWidth, footerWidth = footer.NaturalWidth;
                    // A fresh layout is the oracle; no language/theme/page rebuild.
                    hero.InvalidateMeasure(); footer.InvalidateMeasure(); await Idle18();
                    bool sameHero = beforeHero == Pixels(hero), sameFooter = beforeFooter == Pixels(footer);
                    string id = prefix + "_" + step++ + "_" + next;
                    checks[id + "_current_glyphs_without_rebuild"] = sameHero && sameFooter;
                    checks[id + "_current_width_without_rebuild"] = Math.Abs(heroWidth - hero.NaturalWidth) < .01 && Math.Abs(footerWidth - footer.NaturalWidth) < .01;
                    checks[id + "_state_and_color_agree"] = heroTitle.Text == L.Text(next.ToUpperInvariant()) && connectionText.Text == heroTitle.Text && heroTitle.Foreground == ConnectionInk28(next) && connectionText.Foreground == heroTitle.Foreground;
                    samples.Add(new { id, sameHero, sameFooter, heroGlyphs, footerGlyphs, heroWidth, finalHeroWidth = hero.NaturalWidth, footerWidth, finalFooterWidth = footer.NaturalWidth });
                    if (language == "en" && theme == "dark" && width == 480 && step <= 4)
                        SavePage((FrameworkElement)Content, Path.Combine(folder, step + "-" + next + ".png"));
                }
            }
            var text = new TextBlock { Text = new string('M', 100), FontSize = 16 };
            var line = new MarqueeLine(text, 28, FlowDirection.LeftToRight, () => true) { Width = 190 };
            var panel = new StackPanel(); panel.Children.Add(line);
            var focus = new Window { Title = "Rovunet dynamic-text fixture", Width = 300, Height = 140, Content = panel, ShowInTaskbar = false };
            var other = new Window { Title = "Rovunet focus fixture", Width = 220, Height = 120, ShowInTaskbar = false };
            try
            {
                focus.Show(); focus.Activate(); await Idle18(); await Task.Delay(80); await Idle18();
                checks["overflow_moves_with_active_window"] = focus.IsActive && line.Moving;
                text.Text = "Short"; await Idle18();
                checks["short_update_resets_animation_and_width"] = !line.Overflowing && !line.HasAnimationClock && Math.Abs(line.Translation) < .01;
                text.Text = new string('W', 120); await Idle18();
                checks["long_update_restarts_motion_without_resize"] = line.Overflowing && line.Moving;
                other.Owner = focus; other.Show(); other.Activate(); await Idle18();
                text.Text = "Disconnected"; await Idle18();
                checks["inactive_text_updates_without_animation"] = !focus.IsActive && !line.HasAnimationClock && Characters(VisualTreeHelper.GetDrawing(text)) == text.Text;
                other.Close(); focus.Activate(); await Idle18();
                text.Text = new string('M', 100); await Idle18();
                checks["reactivated_long_text_moves"] = line.Moving;
                panel.Children.Clear(); await Idle18(); text.Text = "Connected"; panel.Children.Add(line); await Idle18();
                double restored = line.NaturalWidth; line.InvalidateMeasure(); await Idle18();
                checks["unload_change_reload_uses_current_text"] = Math.Abs(restored - line.NaturalWidth) < .01 && Characters(VisualTreeHelper.GetDrawing(text)) == text.Text;
                text.Text = new string('M', 100); await Idle18(); focus.WindowState = WindowState.Minimized; await Idle18();
                text.Text = new string('W', 120); await Idle18();
                checks["minimized_update_has_no_animation_clock"] = !line.HasAnimationClock;
            }
            finally { other.Close(); focus.Close(); }
        }
        catch (Exception e) { checks["exception"] = false; File.WriteAllText(report + ".error", e.ToString()); }
        finally { durationTimer.Stop(); }
        bool passed = checks.Count > 0 && checks.Values.All(x => x);
        File.WriteAllText(report, JsonSerializer.Serialize(new { passed, checks, samples, startedUtc = started, endedUtc = DateTimeOffset.UtcNow, pid = Environment.ProcessId, command = Environment.GetCommandLineArgs(), realWpf = true, liveNetwork = false, assembly = typeof(MainWindow).Assembly.Location }, new JsonSerializerOptions { WriteIndented = true }));
        return passed;

        static string Characters(Drawing? drawing) => drawing switch
        {
            GlyphRunDrawing glyph => glyph.GlyphRun.Characters == null ? "" : new string(glyph.GlyphRun.Characters.ToArray()),
            DrawingGroup group => string.Concat(group.Children.Select(Characters)),
            _ => ""
        };

        static string Pixels(FrameworkElement element)
        {
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
    }
}
