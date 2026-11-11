using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    TextBlock heroTitle = null!, heroDetail = null!, heroPing = null!;

    FrameworkElement BuildHero()
    {
        var grid = new Grid();
        heroRingColumn19=new(){Width=new(140)};grid.ColumnDefinitions.Add(heroRingColumn19);
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

        var ring = new Grid { Width = 176, Height = 176, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ring.Children.Add(new Border { CornerRadius = new(88), BorderThickness = new(6), BorderBrush = Accent, Opacity = .30, IsHitTestVisible = false });
        ring.Children.Add(new Border { CornerRadius = new(74), Margin = new(10), BorderThickness = new(2), BorderBrush = GlassEdge(Accent), Opacity = .45, IsHitTestVisible = false });
        connect = new Button
        {
            Width = 152, Height = 160, Padding = new(0), Cursor = Cursors.Hand, Background = Accent,
            Content = new System.Windows.Shapes.Path { Data = PowerIcon, Stroke = Brushes.White, StrokeThickness = 2.6, Width = 38, Height = 38, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round },
            Template = (ControlTemplate)Application.Current.FindResource("HeroPowerTemplate"),
            ToolTip = L.Text("CONNECT")
        };
        connect.Effect = new DropShadowEffect { Color = darkTheme ? Colors.Black : Color.FromRgb(70, 48, 140), BlurRadius = 24, ShadowDepth = 7, Direction = 270, Opacity = darkTheme ? .55 : .32, RenderingBias = RenderingBias.Performance };
        AutomationProperties.SetName(connect, L.Text("CONNECT"));
        connect.Click += (_, _) => HeroConnect();
        ring.Children.Add(connect);
        heroRingScale19=new Viewbox{Child=ring,Width=124,Height=124,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(heroRingScale19,0);grid.Children.Add(heroRingScale19);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new DockPanel { FlowDirection = L.Direction };
        heroTitle = Text("", 30); heroTitle.FontWeight = FontWeights.Bold; heroTitle.TextWrapping = TextWrapping.Wrap;heroTitle.MaxHeight=80;
        heroPing = Text("", 14, Muted); heroPing.FlowDirection = FlowDirection.LeftToRight; heroPing.VerticalAlignment = VerticalAlignment.Bottom; heroPing.Margin = new(10, 5, 0, 0); titleRow.Children.Add(heroPing);
        DockPanel.SetDock(heroPing,Dock.Bottom);titleRow.Children.Add(heroTitle);right.Children.Add(titleRow);
        heroDetail = Text("", 14, Muted); heroDetail.TextWrapping = TextWrapping.Wrap;heroDetail.MaxHeight=48; heroDetail.Margin = new(0, 8, 0, 0); right.Children.Add(heroDetail);
        Grid.SetColumn(right, 1); grid.Children.Add(right);

        var card = Card(grid);card.Tag="HERO";card.Background=HeroSurface(); card.Height=HeroExpanded19;card.Padding = new(12); card.CornerRadius = new(26); card.Margin = new(24,10,24,8); CardDepth(card);
        return card;
    }

    void HeroConnect()
    {
        if (state.State is "connected" or "connecting") { RequestDisconnect(); return; }
        if (Selected == null) {var candidate=RandomConnectableProfile20();if(candidate==null){OpenAddHub();return;}SelectProfile20(candidate);if(Selected==null)return;}
        _ = Run(Connect);
    }
    bool CanConnectFromTray20()=>data.Profiles.Count==0||(Selected is {} selected?SupportPolicy.Allowed(selected,data.IncludeExperimental):data.Profiles.Any(p=>p.CanConnect&&SupportPolicy.Allowed(p,data.IncludeExperimental)));
    void ToggleTrayConnection()
    {
        if(state.State=="disconnecting")return;
        if(Selected==null)ShowExisting();
        HeroConnect();
    }

    void RefreshHero()
    {
        if (heroTitle == null) return;
        RefreshPowerButton();
        var profile = activeProfile ?? Selected;
        long? ping = state.State=="connected"?liveLatency:null;
        bool connected = state.State == "connected";
        heroTitle.Text = L.Text(state.State.ToUpperInvariant()) + (connected && profile != null ? " · " + profile.Name : "");
        heroPing.Text = ping.HasValue ? ping.Value.ToString(CultureInfo.InvariantCulture) + " ms" : "";
        heroPing.Visibility = ping.HasValue ? Visibility.Visible : Visibility.Collapsed;
        heroDetail.Text = state.State switch { "connected" => L.Text(state.Verified ? "HTTPS_VERIFIED" : "PATH_READY_UNVERIFIED"), "connecting" => L.Text("CONNECTING_HINT"), "error" => L.Text(state.Error.Length > 0 ? state.Error : "ERROR_HINT"), _ => L.Text("HOME_CONNECT_HINT") };
    }

    long? PingOf(Profile? profile)
    {
        if (profile == null) return null;
        if (measurements.TryGetValue(profile.Id, out var live) && live.ElapsedMs > 0) return live.ElapsedMs;
        if (data.Measurements.TryGetValue(profile.Id, out var saved) && saved.ElapsedMs > 0) return saved.ElapsedMs;
        return null;
    }
}
