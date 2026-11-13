using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    TextBlock heroName21 = null!;
    MarqueeLine heroNameLine21 = null!;
    MarqueeLine heroDetailLine17 = null!;
    FrameworkElement BuildHero21()
    {
        double ringSize = compactLayout ? 112 : 124;
        var grid = new Grid(); heroRingColumn19 = new() { Width = new(ringSize + 16) };
        grid.ColumnDefinitions.Add(heroRingColumn19); grid.ColumnDefinitions.Add(new());
        var ring = new Grid { Width = 176, Height = 176 };
        ring.Children.Add(new Border { CornerRadius = new(88), BorderThickness = new(5), BorderBrush = Accent, Opacity = .28, IsHitTestVisible = false });
        ring.Children.Add(new Border { CornerRadius = new(76), Margin = new(9), BorderThickness = new(1.5), BorderBrush = GlassEdge(Accent), Opacity = .40, IsHitTestVisible = false });
        connect = new Button
        {
            Width = 152, Height = 157, Padding = new(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = Tone("AddSurface"), Foreground = Tone("AddInk"),
            Template = (ControlTemplate)Application.Current.FindResource("HeroPowerTemplate"), ToolTip = L.Text("CONNECT")
        };
        AutomationProperties.SetName(connect, L.Text("CONNECT")); connect.Click += (_, _) => HeroConnect(); ring.Children.Add(connect);
        heroRingScale19 = new Viewbox { Child = ring, Width = ringSize, Height = ringSize, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(heroRingScale19);
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(0) };
        heroTitle = Text("", 24); heroTitle.FontWeight = FontWeights.SemiBold; heroTitle.TextWrapping = TextWrapping.NoWrap; heroTitle.Margin = new(0); heroTitle.MaxHeight = 32;
        var titleViewport=MovingLine21(heroTitle,34,L.Direction);titleViewport.Tag="HERO_TITLE_MARQUEE";copy.Children.Add(titleViewport);
        heroName21 = Text("", 14, Muted); heroNameLine21 = MovingLine21(heroName21, 22, L.Direction); heroNameLine21.Margin = new(0, 3, 0, 0); copy.Children.Add(heroNameLine21);
        var meta = new DockPanel { Margin = new(0, 7, 0, 0) };
        heroPing = Technical(Text("", 12, Muted)); heroPing.Margin = new(8, 0, 0, 0); heroPing.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(heroPing, Dock.Right); meta.Children.Add(heroPing);
        heroDetail = Text("", 12, Green); heroDetailLine17 = MovingLine21(heroDetail, 20, L.Direction); meta.Children.Add(heroDetailLine17); copy.Children.Add(meta);
        Grid.SetColumn(copy, 1); grid.Children.Add(copy);
        var card = Card(grid); card.Tag = "HERO"; card.Background = HeroSurface(); card.Height = HeroExpanded19;
        card.Padding = new(10); card.CornerRadius = new(22); card.Margin = new(HomeInset21, 0, HomeInset21, 13); CardDepth(card); return card;
    }
}
