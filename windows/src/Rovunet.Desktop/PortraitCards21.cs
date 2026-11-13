using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly Dictionary<string, Brush> glassSurfaces = [];
    Brush HeroSurface()
    {
        if (SystemParameters.HighContrast) return Tone("Card");
        var brush = new LinearGradientBrush(((SolidColorBrush)Tone("Hero")).Color, ((SolidColorBrush)Tone("HeroEnd")).Color, new Point(0, 0), new Point(.8, 1));
        brush.Freeze(); return brush;
    }
    Brush GlassSurface(string key)
    {
        if (SystemParameters.HighContrast) return Tone(key);
        if (glassSurfaces.TryGetValue(key, out var cached)) return cached;
        Color color = ((SolidColorBrush)Tone(key)).Color;
        Color Lift(double amount) => Color.FromRgb((byte)(color.R + (255 - color.R) * amount), (byte)(color.G + (255 - color.G) * amount), (byte)(color.B + (255 - color.B) * amount));
        double shine = key is "Sidebar" or "StatusSurface" ? .025 : key == "Collection" ? .10 : .045;
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(.7, 1) };
        brush.GradientStops.Add(new(Lift(darkTheme ? shine : .35), 0));
        brush.GradientStops.Add(new(Lift(darkTheme ? shine * .35 : .12), .20));
        brush.GradientStops.Add(new(color, 1));
        brush.Freeze(); glassSurfaces[key] = brush; return brush;
    }
    Brush GlassEdge(Brush tint)
    {
        if (SystemParameters.HighContrast || tint is not SolidColorBrush solid) return tint;
        string key = "edge:" + solid.Color;
        if (glassSurfaces.TryGetValue(key, out var cached)) return cached;
        var c = solid.Color;
        var top = Color.FromRgb((byte)(c.R + (255 - c.R) * .22), (byte)(c.G + (255 - c.G) * .22), (byte)(c.B + (255 - c.B) * .22));
        var brush = new LinearGradientBrush(top, c, new Point(0, 0), new Point(.2, 1));
        brush.Freeze(); glassSurfaces[key] = brush; return brush;
    }
    Brush HomeBackdrop()
    {
        if (SystemParameters.HighContrast) return Tone("Bg");
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(.7, 1) };
        brush.GradientStops.Add(new(((SolidColorBrush)Tone("BgTop")).Color, 0));
        brush.GradientStops.Add(new(((SolidColorBrush)Tone("Bg")).Color, IsAmoled ? .62 : 1));
        brush.Freeze(); return brush;
    }
    void CardDepth(Border card)
    {
        if (SystemParameters.HighContrast) return;
        var color = darkTheme ? Color.FromRgb(0, 8, 20) : Color.FromRgb(46, 52, 93);
        if (card is ElevatedBorder surface) { surface.SetDepth(color, darkTheme ? .32 : .18); return; }
        var shadow = new System.Windows.Media.Effects.DropShadowEffect { Color = color, BlurRadius = 16, ShadowDepth = 4, Direction = 270, Opacity = darkTheme ? .32 : .18, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
        shadow.Freeze(); card.Effect = shadow;
    }
    static string ProfileHost(Profile profile)
    {
        try
        {
            if(profile.Protocol=="AmneziaWG")return Importer.Connection(profile).Outbound["settings"]!["server"]!.ToString();
            if (profile.Raw.StartsWith("rovunet://") || profile.Raw.StartsWith("slipnet://")) return Importer.Fields(profile.Raw)[3];
            return Uri.TryCreate(profile.Raw, UriKind.Absolute, out var uri) ? uri.Host : "";
        }
        catch { return ""; }
    }

    void BuildCards()
    {
        if (cards == null) return; NormalizeCollections26();
        var visible = data.Profiles.Where(p => SupportPolicy.Allowed(p, data.IncludeExperimental)).ToList();
        string signature = JsonSerializer.Serialize(visible.Select(p => new { p.Id, p.Name, p.SourceId, p.CollectionId, p.CollectionName, p.Protocol, p.BlockReason, p.CanConnect, instance = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p) }));
        if (signature == cardsSignature) { RefreshCards(); return; }
        double offset = mainScroll.VerticalOffset; cardsSignature = signature;
        cards.Children.Clear(); rows.Clear(); groups.Clear(); cards.Children.Add(heroScrollSpacer19);
        if (visible.Count == 0)
        {
            var empty = new StackPanel { Margin = new(12, 22, 12, 22) };
            empty.Children.Add(Text(L.Text(data.Profiles.Count == 0 ? "EMPTY_TITLE" : "NO_SUPPORTED_IMPORT"), 19));
            empty.Children.Add(Text(L.Text("ADD_HUB_HINT"), 14, Muted)); cards.Children.Add(Card(empty)); return;
        }
        foreach (var group in visible.GroupBy(p => p.SourceId.Length > 0 ? "sub:" + p.SourceId : p.CollectionId))
        {
            var members = group.ToList(); var first = members[0];
            var stack = new StackPanel(); var header = new DockPanel { Height = 44 };
            var tools = new StackPanel { Orientation = Orientation.Horizontal };
            var paths = new StackPanel { Margin = new(10, 0, 10, 0) };
            void Toggle() => ToggleCollection26(group.Key);
            var toggle = IconButton("SHOW_PATHS", "M4,8 L12,16 L20,8", Toggle);
            var test = IconButton("TEST_COLLECTION", "M4,5 H20 V20 H4 Z M8,2 V8 M16,2 V8 M8,13 L11,16 L17,10", () => CollectionTestDialog(members));
            var power = IconButton("CONNECT", PowerIcon.ToString(), () =>
            {
                var chosen = members.FirstOrDefault(x => x.Id == data.SelectedId) ?? members.FirstOrDefault(x => x.CanConnect);
                if (chosen == null) return;
                if (state.State is "connected" or "connecting" && members.Any(p => p.Id == activeProfile?.Id)) RequestDisconnect(); else SwitchPath(chosen);
            });
            var more = MoreButton(CollectionMenu(members));
            foreach (var button in new[] { toggle, test, power, more }) { button.Width = 44; button.Margin = new(0); button.Background = Brushes.Transparent; tools.Children.Add(button); }
            DockPanel.SetDock(tools, Dock.Right); header.Children.Add(tools);
            var identity = new StackPanel();
            var name = Text(first.CollectionName.Length > 0 ? first.CollectionName : members.Count == 1 ? first.Name : L.Text("COLLECTION"), 15);
            name.FontWeight = FontWeights.SemiBold; name.ToolTip = name.Text; identity.Children.Add(MovingLine21(name, 22));
            var meta = Text(L.Text(first.SourceId.Length > 0 ? "COLLECTION_SUBSCRIPTION" : "COLLECTION_MANUAL") + " · " + string.Format(L.Text("PATH_COUNT"), members.Count), 12, Muted);
            identity.Children.Add(MovingLine21(meta, 18, L.Direction));
            var collectionLabel = new DockPanel();
            var collectionIcon = new System.Windows.Shapes.Path { Data = Geometry.Parse(MenuIcon(first.SourceId.Length > 0 ? "MANAGE_COLLECTIONS" : "FOLDER")), Stroke = Muted, StrokeThickness = 1.7, Width = 24, Height = 24, Stretch = Stretch.Uniform, Margin = new(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(collectionIcon, Dock.Left); collectionLabel.Children.Add(collectionIcon); collectionLabel.Children.Add(identity);
            var choose = Button("SHOW_PATHS", Toggle); choose.Content = collectionLabel; choose.Template = (ControlTemplate)Application.Current.FindResource("FlatContentButtonTemplate");
            choose.HorizontalContentAlignment = HorizontalAlignment.Stretch; choose.Background = Brushes.Transparent; choose.Padding = new(0); choose.Margin = new(0); header.Children.Add(choose);
            var headerBody = new StackPanel(); headerBody.Children.Add(header);
            var quota = new AccountQuotaTile(() => MarqueeEnabled21); var account = quota.Note;
            var closedStatus=Text("",12,Muted);var closedLine=MovingLine21(closedStatus,18,L.Direction);headerBody.Children.Add(closedLine);
            var details=new StackPanel();headerBody.Children.Add(details);details.Children.Add(quota.Root);
            var summary = Text("", 12, Muted); var summaryLine=MovingLine21(summary,20,L.Direction);summaryLine.SetBinding(VisibilityProperty,new Binding(nameof(Visibility)){Source=summary});details.Children.Add(summaryLine);
            var stop = Button("STOP_TEST", CancelCollectionTest); stop.Foreground = Red; stop.Background = Tone("Soft"); stop.Visibility = Visibility.Collapsed;
            stop.Margin = new(0); stop.Width = 132; stop.Height = 44; stop.Padding = new(6, 2, 6, 2);
            stop.ContentTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}' TextWrapping='NoWrap' TextAlignment='Center' FontSize='13' FontWeight='SemiBold'/></DataTemplate>"); tools.Children.Add(stop);
            var progress = new ProgressBar { Height = 4, Minimum = 0, Visibility = Visibility.Collapsed, Margin = new(0, 1, 0, 0) }; headerBody.Children.Add(progress);
            var headerCard = new ElevatedBorder { Child = headerBody, MinHeight = 72, Background = GlassSurface("Collection"), CornerRadius = new(18), BorderBrush = GlassEdge(Tone("Outline")), BorderThickness = new(1), Padding = new(10, 6, 6, 6) };
            headerCard.CornerRadius=new(16);headerCard.BorderThickness=new(.75);CardDepth(headerCard); stack.Children.Add(headerCard);
            foreach (var item in members)
            {
                var row = new Grid { Height = 54 };
                row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = new(92) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                var labels = new Grid { VerticalAlignment = VerticalAlignment.Center };
                labels.RowDefinitions.Add(new() { Height = new(20) }); labels.RowDefinitions.Add(new() { Height = new(16) }); labels.RowDefinitions.Add(new() { Height = new(18) });
                var titleLine = new Grid(); titleLine.ColumnDefinitions.Add(new());
                var title = Text(item.Name, 15); title.FontWeight = FontWeights.SemiBold; title.ToolTip = item.Name;
                titleLine.Children.Add(MovingLine21(title, 20));
                var timing = Technical(Text("", 12, Muted)); timing.Margin = new(0); timing.TextWrapping = TextWrapping.NoWrap;
                var pingViewport=MovingLine21(timing,20,FlowDirection.LeftToRight);pingViewport.Tag="PROFILE_PING_MARQUEE";
                var ping = new Border { Child = pingViewport, Padding = new(5, 1, 5, 1), Margin = new(5, 0, 4, 0), Background = GlassSurface("Selected"), CornerRadius = new(10), VerticalAlignment = VerticalAlignment.Center };
                ping.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = timing }); Grid.SetColumn(ping, 1); row.Children.Add(ping); labels.Children.Add(titleLine);
                var host = Text(ProfileHost(item), 12, Muted); host.ToolTip = host.Text;
                var hostLine = MovingLine21(host, 16, FlowDirection.LeftToRight); Grid.SetRow(hostLine, 1); labels.Children.Add(hostLine);
                var protocol = Text(item.Protocol, 12, Muted); protocol.FlowDirection = FlowDirection.LeftToRight; protocol.TextWrapping = TextWrapping.NoWrap; protocol.Margin = new(0);
                var result = Text("", 12, Muted); result.TextWrapping = TextWrapping.NoWrap; result.Margin = new(0);
                var message = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = L.Direction };
                message.Children.Add(protocol);
                var separator = Text(" · ", 12, Muted); separator.Margin = new(0); separator.SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = result }); message.Children.Add(separator); message.Children.Add(result);
                var messageLine = new MarqueeLine(message, 18, L.Direction, () => MarqueeEnabled21); Grid.SetRow(messageLine, 2); labels.Children.Add(messageLine);
                var select = new RadioButton { Content = labels, GroupName = "selected", IsChecked = item.Id == data.SelectedId, Margin = new(0), Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type RadioButton}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='Transparent' Padding='0'><ContentPresenter/></Border></ControlTemplate>") };
                AutomationProperties.SetName(select, L.Text("SELECT") + " " + item.Name); select.Checked += (_, _) => SelectProfile20(item); row.Children.Add(select);
                var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var edit = MenuAction("EDIT", () => Edit(item)); var remove = MenuAction("DELETE", () => Delete(item));
                var connectItem = MenuAction("CONNECT", () => { if (state.State == "connected" && activeProfile?.Id == item.Id) RequestDisconnect(); else SwitchPath(item); });
                var editButton = IconButton("EDIT", "M2,18 L16,4 L20,8 L6,22 H2 Z M14,6 L18,10", () => Edit(item)); editButton.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = edit }); controls.Children.Add(editButton);
                var menu = PathMenu(item, connectItem, edit, remove); controls.Children.Add(MoreButton(menu));
                var deleteButton = IconButton("DELETE", "M5,6 H19 M8,6 V3 H16 V6 M7,9 V21 H17 V9", () => Delete(item)); deleteButton.SetBinding(IsEnabledProperty, new Binding("IsEnabled") { Source = remove }); controls.Children.Add(deleteButton);
                foreach (Button button in controls.Children) { button.Width = 44; button.Margin = new(0); button.Padding = new(10); button.Background = Brushes.Transparent; if(button.Content is System.Windows.Shapes.Path shape&&shape.Stroke!=null){shape.StrokeThickness=1.5;shape.Width=shape.Height=20;} }
                Grid.SetColumn(controls, 2); row.Children.Add(controls);
                var tile = Card(row, new(0, 6, 0, 0)); tile.Padding = new(9, 5, 5, 5); tile.CornerRadius = new(13); tile.Height = 66; tile.BorderThickness=new(.75);
                RouteProfileCard20(tile, select, item); row.ClipToBounds = true; CardDepth(tile); LiftOnHover(tile);
                select.GotKeyboardFocus += (_, _) => tile.BorderBrush = Accent; select.LostKeyboardFocus += (_, _) => RefreshCards(); paths.Children.Add(tile);
                rows.Add(new() { Profile = item, Tile = tile, Select = select, Connect = connectItem, Result = result, Timing = timing, Protocol = protocol, Edit = edit, Delete = remove });
            }
            stack.Children.Add(paths);
            var wrapper = new Border { Child = stack, Margin = new(0, 0, 0, 10), Background = Brushes.Transparent, BorderThickness = new(0) };
            cards.Children.Add(wrapper);
            groups.Add(new() { Key = group.Key, Members = members, Card = wrapper, Header = headerCard, Summary = summary, Paths = paths, Toggle = toggle, Connect = power, Account = account, Quota = quota, Menu = more.ContextMenu, Test = test, Stop = stop, More = more, Progress = progress, Details26=details, ClosedStatus26=closedStatus, ClosedLine26=closedLine });
        }
        usageOrderPending17=true;RefreshCards(); Dispatcher.BeginInvoke(() => mainScroll.ScrollToVerticalOffset(offset), DispatcherPriority.Loaded);
    }
    FrameworkElement CardLine(TextBlock text, double height) => MovingLine21(text, height);
    FrameworkElement CardStatus(TextBlock text) => MovingLine21(text, 20, L.Direction);
}
