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
            ["Bg"]="#F2F2FB", ["BgTop"]="#FCFCFF", ["Card"]="#FFFFFF", ["Soft"]="#FFFFFF",
            ["Sidebar"]="#F5F5FD", ["Ink"]="#202C42", ["Muted"]="#526078", ["Accent"]="#6345AC",
            ["Outline"]="#BDC1D4", ["Selected"]="#E4D7ED", ["Green"]="#125439", ["Red"]="#AD2645", ["Amber"]="#885706",
            ["PowerSurface"]="#B5E9CC", ["PowerInk"]="#125439", ["AddSurface"]="#D6CAF8", ["AddInk"]="#302748",
            ["Collection"]="#E8DEEF", ["StatusSurface"]="#FFFFFF", ["Hero"]="#FFFFFF", ["HeroEnd"]="#EDEAF8", ["ScanCyan"]="#146A78"
        } : IsAmoled ? new Dictionary<string, string>
        {
            ["Bg"]="#000000", ["BgTop"]="#10131C", ["Card"]="#171E29", ["Soft"]="#171E29",
            ["Sidebar"]="#080B11", ["Ink"]="#F2F5FC", ["Muted"]="#BDC7D5", ["Accent"]="#D1C5ED",
            ["Outline"]="#596072", ["Selected"]="#50435F", ["Green"]="#A3E7BF", ["Red"]="#FF9BAB", ["Amber"]="#F3CF7D",
            ["PowerSurface"]="#A3E7BF", ["PowerInk"]="#123C2B", ["AddSurface"]="#D1C5ED", ["AddInk"]="#2A293E",
            ["Collection"]="#393143", ["StatusSurface"]="#151922", ["Hero"]="#151922", ["HeroEnd"]="#080B11", ["ScanCyan"]="#83D4DD"
        } : new Dictionary<string, string>
        {
            ["Bg"]="#17273F", ["BgTop"]="#304367", ["Card"]="#2B3E59", ["Soft"]="#2B3E59",
            ["Sidebar"]="#101A2B", ["Ink"]="#F4F6FF", ["Muted"]="#D0D6E7", ["Accent"]="#D6C8F2",
            ["Outline"]="#748299", ["Selected"]="#9C87AE", ["Green"]="#A5E6C0", ["Red"]="#FF9BAB", ["Amber"]="#F3CF7D",
            ["PowerSurface"]="#A5E6C0", ["PowerInk"]="#164737", ["AddSurface"]="#D6C8F2", ["AddInk"]="#302344",
            ["Collection"]="#716082", ["StatusSurface"]="#293952", ["Hero"]="#0E1829", ["HeroEnd"]="#18243B", ["ScanCyan"]="#83D4DD"
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
