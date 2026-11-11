using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    bool MarqueeEnabled21 => data.MarqueeMotion switch
    {
        "on" => true,
        "off" => false,
        _ => SystemParameters.ClientAreaAnimation
    };

    void ApplyApprovedPalette21()
    {
        var palette = !darkTheme ? new Dictionary<string, string>
        {
            ["Bg"]="#F0F1FB", ["BgTop"]="#FAFAFF", ["Card"]="#FFFFFF", ["Soft"]="#FFFFFF",
            ["Sidebar"]="#F5F5FD", ["Ink"]="#1A2940", ["Muted"]="#56647B", ["Accent"]="#6345AC",
            ["Outline"]="#C5C9DB", ["Selected"]="#E3E4F3", ["Green"]="#125439", ["Red"]="#AD2645", ["Amber"]="#885706",
            ["PowerSurface"]="#B1E8C7", ["PowerInk"]="#125439", ["AddSurface"]="#D6CAF8", ["AddInk"]="#302748",
            ["Collection"]="#E5DFF3", ["StatusSurface"]="#FFFFFF", ["Hero"]="#FFFFFF", ["HeroEnd"]="#EAEAF7", ["ScanCyan"]="#146A78"
        } : IsAmoled ? new Dictionary<string, string>
        {
            ["Bg"]="#000000", ["BgTop"]="#080A0F", ["Card"]="#151D27", ["Soft"]="#151D27",
            ["Sidebar"]="#080B11", ["Ink"]="#F2F5FC", ["Muted"]="#BDC7D5", ["Accent"]="#D1C5ED",
            ["Outline"]="#546273", ["Selected"]="#3D4256", ["Green"]="#A3E7BF", ["Red"]="#FF9BAB", ["Amber"]="#F3CF7D",
            ["PowerSurface"]="#A3E7BF", ["PowerInk"]="#123C2B", ["AddSurface"]="#D1C5ED", ["AddInk"]="#2A293E",
            ["Collection"]="#333348", ["StatusSurface"]="#171A24", ["Hero"]="#171A24", ["HeroEnd"]="#080B11", ["ScanCyan"]="#83D4DD"
        } : new Dictionary<string, string>
        {
            ["Bg"]="#16263C", ["BgTop"]="#293B55", ["Card"]="#2A3D53", ["Soft"]="#2A3D53",
            ["Sidebar"]="#1B2B40", ["Ink"]="#F4F7FF", ["Muted"]="#C7D2E1", ["Accent"]="#D4C8F3",
            ["Outline"]="#64788E", ["Selected"]="#4B5068", ["Green"]="#A1E3BF", ["Red"]="#FF9BAB", ["Amber"]="#F3CF7D",
            ["PowerSurface"]="#A1E3BF", ["PowerInk"]="#143D35", ["AddSurface"]="#D4C8F3", ["AddInk"]="#302C49",
            ["Collection"]="#4A4966", ["StatusSurface"]="#23344B", ["Hero"]="#23344B", ["HeroEnd"]="#17273E", ["ScanCyan"]="#83D4DD"
        };
        foreach (var (key, color) in palette)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); Application.Current.Resources[key] = brush;
        }
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "PowerSurface", "AddSurface" }) Application.Current.Resources[key] = SystemColors.HighlightBrush;
            foreach (var key in new[] { "PowerInk", "AddInk" }) Application.Current.Resources[key] = SystemColors.HighlightTextBrush;
            foreach (var key in new[] { "BgTop", "Hero", "HeroEnd", "Collection", "StatusSurface" }) Application.Current.Resources[key] = SystemColors.WindowBrush;
        }
    }

    MarqueeLine MovingLine21(TextBlock text, double height, FlowDirection? direction = null)
    {
        text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.None;
        text.Margin = new(0); text.LineHeight = height; text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        var reading = direction ?? ReadingDirection21(text.Text);
        text.FlowDirection = reading;
        return new MarqueeLine(text, height, reading, () => MarqueeEnabled21);
    }
    static FlowDirection ReadingDirection21(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsLetter(c)) continue;
            return c is >= '\u0590' and <= '\u08FF' ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
        return L.Direction;
    }
}

// Physical left and right slots never affect the mathematical center of the flags.
// If the slots would overlap, only the flags move to a second, centered row.
internal sealed class CenteredHeader21 : Panel
{
    internal required FrameworkElement Leading { get; init; }
    internal required FrameworkElement Trailing { get; init; }
    internal required FrameworkElement Flags { get; init; }
    internal bool Split { get; private set; }
    const double Edge = 10;
    internal void Initialize() { FlowDirection = FlowDirection.LeftToRight; Children.Add(Leading); Children.Add(Trailing); Children.Add(Flags); }
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children) child.Measure(new Size(double.PositiveInfinity, 48));
        double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
        Split = width < 2 * Math.Max(Leading.DesiredSize.Width, Trailing.DesiredSize.Width) + Flags.DesiredSize.Width + 2 * Edge + 4;
        return new Size(width, Split ? 106 : 65);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = Split ? 5 : (finalSize.Height - 44) / 2;
        Leading.Arrange(new Rect(Edge, y, Leading.DesiredSize.Width, 44));
        Trailing.Arrange(new Rect(finalSize.Width - Edge - Trailing.DesiredSize.Width, y, Trailing.DesiredSize.Width, 44));
        Flags.Arrange(new Rect((finalSize.Width - Flags.DesiredSize.Width) / 2, Split ? 55 : (finalSize.Height - 44) / 2, Flags.DesiredSize.Width, 44));
        return finalSize;
    }
}
