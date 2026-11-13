using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    CenteredHeader21 header21 = null!;
    double HomeInset21 => compactLayout ? 15 : 19;

    void BuildApprovedShell21()
    {
        CloseAddHub(); compactLayout = (ActualWidth > 0 ? ActualWidth : Width) < ContextThreshold;
        FontFamily = Typography.Family(L.Language); TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        FlowDirection = FlowDirection.LeftToRight; Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Language);
        cardsSignature = ""; rows.Clear(); groups.Clear();
        var root = new Grid { FlowDirection = L.Direction, Background = HomeBackdrop() };
        root.ColumnDefinitions.Add(new() { Width = compactLayout ? new(0) : new(222) }); root.ColumnDefinitions.Add(new());
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());
        var home = new Grid { Background = HomeBackdrop() }; Grid.SetColumn(home, 1); Grid.SetRow(home, 1); root.Children.Add(home);
        home.RowDefinitions.Add(new() { Height = GridLength.Auto }); home.RowDefinitions.Add(new() { Height = GridLength.Auto }); home.RowDefinitions.Add(new()); home.RowDefinitions.Add(new() { Height = GridLength.Auto });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = L.Direction };
        Button Action(string key, string glyph, System.Action action)
        {
            var button = IconButton(key, glyph, action); button.Width = 44; button.Height = 44; button.Margin = new(0); button.Background = Brushes.Transparent; actions.Children.Add(button);return button;
        }
        homeSearchAction30=Action("SEARCH", MenuIcon("SEARCH"), ()=>{}); Action("SETTINGS", MenuIcon("SETTINGS"), SettingsDialog);
        var headerMore=Action("SECTION_GENERAL", "M12,3 L12,5 M12,11 L12,13 M12,19 L12,21", GeneralPage);
        headerMore.Content=FilledMoreIcon28();headerMore.Tag="HEADER_MORE";
        FrameworkElement leading, trailing;
        if (compactLayout) { leading = BrandMark(); trailing = actions; }
        else
        {
            var title = Text(L.Text("HOME"), 21); title.Margin = new(0); title.VerticalAlignment = VerticalAlignment.Center;
            var titleWithSidebar = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight };
            if (L.Direction == FlowDirection.LeftToRight) { titleWithSidebar.Children.Add(new Border { Width = 222 }); titleWithSidebar.Children.Add(title); leading = titleWithSidebar; trailing = actions; }
            else { titleWithSidebar.Children.Add(title); titleWithSidebar.Children.Add(new Border { Width = 222 }); leading = actions; trailing = titleWithSidebar; }
        }
        header21 = new() { Leading = leading, Trailing = trailing, Flags = LanguageFlags() }; header21.Initialize();
        Grid.SetColumnSpan(header21, 2); Panel.SetZIndex(header21, 4); root.Children.Add(header21);

        search = new TextBox { Visibility = Visibility.Collapsed, Margin = new(HomeInset21, 0, HomeInset21, 8), MinHeight = 44, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = L.Text("SEARCH") };
        AutomationProperties.SetName(search, L.Text("SEARCH")); search.TextChanged += (_, _) => RefreshCards(); home.Children.Add(search);
        BindSearchAction30(search,homeSearchAction30,RefreshCards,"HOME_SEARCH_ACTION");
        heroScrollSpacer19 = new Border { Height = 0, IsHitTestVisible = false };
        cards = new StackPanel { Margin = new(HomeInset21, 0, HomeInset21, 72) };
        mainScroll = new ScrollViewer { Content = cards, PanningMode = PanningMode.VerticalOnly, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        mainScroll.ScrollChanged += HomeScrollChanged19; Grid.SetRow(mainScroll, 2); home.Children.Add(mainScroll);
        heroCard19 = (Border)BuildHero(); Grid.SetRow(heroCard19, 1); home.Children.Add(heroCard19); WireHeroScroll19();

        addButton = Button("ADD", OpenAddHub); addButton.FlowDirection = FlowDirection.LeftToRight;
        addButton.Width = 52; addButton.Height = 52; addButton.Padding = new(13); addButton.Margin = new(0, 0, HomeInset21, 13);
        addButton.HorizontalAlignment = HorizontalAlignment.Right; addButton.VerticalAlignment = VerticalAlignment.Bottom;
        addButton.Background = Tone("AddSurface"); addButton.Foreground = Tone("AddInk"); addButton.BorderBrush = GlassEdge(Tone("AddSurface"));
        addButton.Template = (ControlTemplate)Application.Current.FindResource("RaisedButtonTemplate");
        addButton.Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M12,2 V22 M2,12 H22"), Stroke = Tone("AddInk"), StrokeThickness = 1.8, Width = 25, Height = 25, Stretch = Stretch.Uniform };
        AutomationProperties.SetAutomationId(addButton, "ADD_HUB");
        var overlay = new Grid { FlowDirection = FlowDirection.LeftToRight }; overlay.Children.Add(addButton); Grid.SetRow(overlay, 2); Panel.SetZIndex(overlay, 5); home.Children.Add(overlay);

        var status = new StackPanel(); connectingProgress = new ProgressBar { Height = 2, IsIndeterminate = true }; status.Children.Add(connectingProgress);
        var statusRow = new DockPanel(); shield = BuildShield(32, true); shield.Background = Brushes.Transparent; shield.Padding = new(4); shield.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(shield, Dock.Left); statusRow.Children.Add(shield);
        var labels = new StackPanel { Margin = new(7, 0, 0, 0) };
        connectionText = Text("", 14); connectionText.FontWeight = FontWeights.SemiBold; connectionText.Margin = new(0); globalStatus = connectionText;
        detail = Text("", 12, Muted); var statusTitleViewport=MovingLine21(connectionText,22,L.Direction);statusTitleViewport.Tag="FOOTER_TITLE_MARQUEE";labels.Children.Add(statusTitleViewport); labels.Children.Add(MovingLine21(detail, 20, L.Direction)); statusRow.Children.Add(labels); status.Children.Add(statusRow);
        var traffic = new Grid { Margin = new(0, 7, 0, 0), FlowDirection = FlowDirection.LeftToRight }; traffic.ColumnDefinitions.Add(new()); traffic.ColumnDefinitions.Add(new());
        var ratesRow = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight };
        trafficUp = Technical(Text("", 12, Muted)); trafficDown = Technical(Text("", 12, Muted)); trafficUp.Margin = new(0, 0, 8, 0); trafficDown.Margin = new(0);
        ratesRow.Children.Add(trafficUp); ratesRow.Children.Add(trafficDown); traffic.Children.Add(ratesRow);
        var totalRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, FlowDirection = FlowDirection.LeftToRight };
        trafficTotal = Technical(Text("", 12, Muted)); trafficTotal.Margin = new(0); totalRow.Children.Add(trafficTotal); Grid.SetColumn(totalRow, 1); traffic.Children.Add(totalRow); status.Children.Add(traffic);
        var timer = new WrapPanel(); sleepCountdown = Text("", 12); cancelSleep = Button("CANCEL_SLEEP_TIMER", CancelSleepTimer); timer.Children.Add(sleepCountdown); timer.Children.Add(cancelSleep); status.Children.Add(timer);
        timer.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = sleepCountdown });
        notice = Text("", 12, Green); notice.Visibility = Visibility.Collapsed;
        var noticeLine = MovingLine21(notice, 20, L.Direction); noticeLine.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = notice }); status.Children.Add(noticeLine);
        errorText = Text("", 12, Red); errorText.Visibility = Visibility.Collapsed;
        var errorLine = MovingLine21(errorText, 20, L.Direction); errorLine.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = errorText }); status.Children.Add(errorLine);
        connectionStrip = new Border { Background = GlassSurface("StatusSurface"), CornerRadius = new(18, 18, 0, 0), Padding = new(16, 10, 16, 11), Child = status, BorderBrush = GlassEdge(Tone("Outline")), BorderThickness = new(0, 1, 0, 0) };
        Grid.SetRow(connectionStrip, 3); home.Children.Add(connectionStrip);
        BuildConnectionControls();
        contextHost = new Border { Visibility = compactLayout ? Visibility.Collapsed : Visibility.Visible, BorderBrush = Tone("Outline"), BorderThickness = L.Direction == FlowDirection.RightToLeft ? new(1, 0, 0, 0) : new(0, 0, 1, 0) };
        Grid.SetRowSpan(contextHost, 2); root.Children.Add(contextHost);
        SetHomeContent(root); PopulateContext(); SetState(state); BuildCards(); RefreshTrafficText(); UpdateTitleTheme();
    }
}
