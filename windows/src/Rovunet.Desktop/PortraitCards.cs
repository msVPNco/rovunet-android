using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    readonly Dictionary<string,Brush> glassSurfaces=[];
    Brush HeroSurface()
    {
        if(SystemParameters.HighContrast)return Tone("Card");
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(.8,1)};
        var colors=darkTheme?new[]{"#111B31","#080E1C"}:new[]{"#FDFBFF","#E9E5FA"};
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(colors[0]),0));brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(colors[1]),1));brush.Freeze();return brush;
    }
    Brush GlassSurface(string key)
    {
        if(SystemParameters.HighContrast)return Tone(key);
        if(glassSurfaces.TryGetValue(key,out var brush))return brush;
        Color color=((SolidColorBrush)Tone(key)).Color;
        Color Lift(double amount,byte alpha)=>Color.FromArgb(alpha,(byte)(color.R+(255-color.R)*amount),(byte)(color.G+(255-color.G)*amount),(byte)(color.B+(255-color.B)*amount));
        var glass=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(.7,1)};
        double shine=key is "Sidebar" or "StatusSurface" or "Soft"?0.045:0.18;
        glass.GradientStops.Add(new(Lift(darkTheme?shine:.70,250),0));glass.GradientStops.Add(new(Lift(darkTheme?shine*.5:.35,244),.10));glass.GradientStops.Add(new(Lift(darkTheme?.015:.08,232),.54));glass.GradientStops.Add(new(Lift(0,224),1));glass.Freeze();glassSurfaces[key]=glass;return glass;
    }
    Brush GlassEdge(Brush tint)
    {
        if(SystemParameters.HighContrast||tint is not SolidColorBrush solid)return tint;
        string key="edge:"+solid.Color;if(glassSurfaces.TryGetValue(key,out var existing))return existing;
        var color=solid.Color;var top=Color.FromRgb((byte)(color.R+(255-color.R)*.38),(byte)(color.G+(255-color.G)*.38),(byte)(color.B+(255-color.B)*.38));
        var edge=new LinearGradientBrush(top,color,new Point(0,0),new Point(.2,1));edge.Freeze();glassSurfaces[key]=edge;return edge;
    }
    Brush HomeBackdrop()
    {
        if(SystemParameters.HighContrast)return Tone("Bg");
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(.8,1)};
        foreach(var stop in IsAmoled?new[]{("#303B61",0d),("#1B263D",.34),("#111927",1d)}:darkTheme?new[]{("#344264",0d),("#23324A",.34),("#1B263D",1d)}:new[]{("#EAE7F7",0d),("#F6F5FC",.40),("#EAEFF8",1d)})brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(stop.Item1),stop.Item2));brush.Freeze();return brush;
    }
    void CardDepth(Border card)
    {
        if(SystemParameters.HighContrast)return;
        if(card is ElevatedBorder surface){surface.SetDepth(darkTheme?Color.FromRgb(2,8,18):Color.FromRgb(72,88,128),darkTheme?.48:.25);return;}
        var shadow=new System.Windows.Media.Effects.DropShadowEffect{Color=darkTheme?Color.FromRgb(2,8,18):Color.FromRgb(72,88,128),BlurRadius=20,ShadowDepth=6,Direction=270,Opacity=darkTheme?.48:.25,RenderingBias=System.Windows.Media.Effects.RenderingBias.Quality};shadow.Freeze();card.Effect=shadow;
    }
    static string ProfileHost(Profile profile)
    {
        try { if (profile.Raw.StartsWith("rovunet://") || profile.Raw.StartsWith("slipnet://")) return Importer.Fields(profile.Raw)[3]; return Uri.TryCreate(profile.Raw,UriKind.Absolute,out var uri)?uri.Host:""; } catch { return ""; }
    }
    void BuildCards()
    {
        if(cards==null)return;
        var visible=data.Profiles.Where(p=>SupportPolicy.Allowed(p,data.IncludeExperimental)).ToList();
        string signature=JsonSerializer.Serialize(visible.Select(p=>new{p.Id,p.Name,p.SourceId,p.CollectionId,p.CollectionName,p.Protocol,p.BlockReason,p.CanConnect,instance=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p)}));
        if(signature==cardsSignature){RefreshCards();return;}
        double offset=mainScroll.VerticalOffset;cardsSignature=signature;cards.Children.Clear();rows.Clear();groups.Clear();
        cards.Children.Add(heroScrollSpacer19);
        if(visible.Count==0){var empty=new StackPanel{Margin=new(12,22,12,22)};empty.Children.Add(Text(L.Text(data.Profiles.Count==0?"EMPTY_TITLE":"NO_SUPPORTED_IMPORT"),19));empty.Children.Add(Text(L.Text("ADD_HUB_HINT"),14,Muted));cards.Children.Add(Card(empty));return;}
        foreach(var group in visible.GroupBy(p=>p.SourceId.Length>0?"sub:"+p.SourceId:p.CollectionId))
        {
            var members=group.ToList();var first=members[0];var stack=new StackPanel();var header=new DockPanel{Height=44};var tools=new StackPanel{Orientation=Orientation.Horizontal};var paths=new StackPanel{Margin=new(16,0,16,12)};
            void Toggle(){data.ExpandedCollections[group.Key]=!data.ExpandedCollections.GetValueOrDefault(group.Key,false);Save();RefreshCards();}
            var toggle=IconButton("SHOW_PATHS","M4,8 L12,16 L20,8",Toggle);toggle.Background=Brushes.Transparent;toggle.Width=44;toggle.Margin=new(0);tools.Children.Add(toggle);
            var power=IconButton("CONNECT","M12,2 V11 M6,5 A9,9 0 1 0 18,5",()=>{var chosen=members.FirstOrDefault(x=>x.Id==data.SelectedId)??members.FirstOrDefault(x=>x.CanConnect);if(chosen==null)return;if(state.State is "connected" or "connecting" && members.Any(p=>p.Id==activeProfile?.Id))RequestDisconnect();else SwitchPath(chosen);});power.Background=Brushes.Transparent;power.Width=44;power.Margin=new(0);tools.Children.Add(power);
            var more=MoreButton(CollectionMenu(members));more.Background=Brushes.Transparent;more.Width=44;more.Margin=new(0);tools.Children.Add(more);DockPanel.SetDock(tools,Dock.Right);header.Children.Add(tools);
            var test=IconButton("TEST_COLLECTION","M4,5 H20 V20 H4 Z M8,2 V8 M16,2 V8 M8,13 L11,16 L17,10",()=>CollectionTestDialog(members));test.Width=44;test.Margin=new(0);test.Background=Brushes.Transparent;tools.Children.Insert(1,test);
            var identity=new StackPanel();var name=Text(first.CollectionName.Length>0?first.CollectionName:members.Count==1?first.Name:L.Text("COLLECTION"),17);name.FontWeight=FontWeights.SemiBold;name.TextWrapping=TextWrapping.NoWrap;name.LineHeight=24;name.Margin=new(4,0,4,0);name.ToolTip=name.Text;identity.Children.Add(CardLine(name,24));var meta=Text(L.Text(first.SourceId.Length>0?"COLLECTION_SUBSCRIPTION":"COLLECTION_MANUAL")+" · "+string.Format(L.Text("PATH_COUNT"),members.Count),12,Muted);meta.TextWrapping=TextWrapping.NoWrap;meta.LineHeight=20;meta.Margin=new(4,0,4,0);identity.Children.Add(CardLine(meta,20));
            var collectionLabel=new DockPanel();var collectionIcon=new System.Windows.Shapes.Path{Data=Geometry.Parse(MenuIcon(first.SourceId.Length>0?"MANAGE_COLLECTIONS":"FOLDER")),Stroke=Tone("Ink"),StrokeThickness=1.7,Width=26,Height=26,Stretch=Stretch.Uniform,Margin=new(0,0,12,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(collectionIcon,Dock.Left);collectionLabel.Children.Add(collectionIcon);collectionLabel.Children.Add(identity);
            var choose=Button("SHOW_PATHS",Toggle);choose.Content=collectionLabel;choose.Template=(ControlTemplate)Application.Current.FindResource("FlatContentButtonTemplate");choose.HorizontalContentAlignment=HorizontalAlignment.Stretch;choose.Background=Brushes.Transparent;choose.Padding=new(0);choose.Margin=new(0);header.Children.Add(choose);
            var headerBody=new StackPanel();headerBody.Children.Add(header);var quota=new AccountQuotaTile();var account=quota.Note;var summary=Text("",12,Muted);summary.LineHeight=18;var summaryArea=CardStatus(summary);summaryArea.Height=18;headerBody.Children.Add(summaryArea);
            var stop=Button("STOP_TEST",CancelCollectionTest);stop.Foreground=Red;stop.Background=Tone("Soft");stop.Visibility=Visibility.Collapsed;stop.Margin=new(0);stop.Width=132;stop.Height=44;stop.Padding=new(6,2,6,2);stop.ContentTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}' TextWrapping='Wrap' TextAlignment='Center' FontSize='14' FontWeight='SemiBold'/></DataTemplate>");tools.Children.Add(stop);
            var progress=new ProgressBar{Height=4,Minimum=0,Visibility=Visibility.Collapsed,Margin=new(4,2,4,0)};headerBody.Children.Add(progress);
            headerBody.ClipToBounds=true;var headerCard=new Border{Child=headerBody,Height=64,Background=Brushes.Transparent,CornerRadius=new(18),BorderBrush=Brushes.Transparent,BorderThickness=new(0),Padding=new(14,1,8,1)};stack.Children.Add(headerCard);stack.Children.Add(quota.Root);
            foreach(var item in members)
            {
                var row=new Grid{Height=56};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new(150)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
                var content=new DockPanel{VerticalAlignment=VerticalAlignment.Center};var linkIcon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M10,15 L7,18 A4,4 0 0 1 1,12 L6,7 A4,4 0 0 1 12,7 M14,9 L17,6 A4,4 0 0 1 23,12 L18,17 A4,4 0 0 1 12,17 M8,16 L16,8"),Stroke=Tone("Ink"),StrokeThickness=1.7,Width=24,Height=24,Stretch=Stretch.Uniform,Margin=new(2,0,10,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(linkIcon,Dock.Left);content.Children.Add(linkIcon);var labels=new StackPanel();var title=Text(item.Name,17);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.NoWrap;title.ToolTip=item.Name;labels.Children.Add(CardLine(title,20));
                title.FlowDirection=item.Name.Any(c=>c is >= '\u0590' and <= '\u08FF')?FlowDirection.RightToLeft:FlowDirection.LeftToRight;title.TextAlignment=L.Direction==FlowDirection.RightToLeft?TextAlignment.Right:TextAlignment.Left;
                var host=Text(ProfileHost(item),13,Muted);host.FlowDirection=FlowDirection.LeftToRight;host.TextAlignment=L.Direction==FlowDirection.RightToLeft?TextAlignment.Right:TextAlignment.Left;host.TextWrapping=TextWrapping.NoWrap;host.ToolTip=host.Text;labels.Children.Add(CardLine(host,16));var protocol=Text(item.Protocol,12,Muted);protocol.FlowDirection=FlowDirection.LeftToRight;protocol.TextAlignment=host.TextAlignment;protocol.TextWrapping=TextWrapping.NoWrap;labels.Children.Add(CardLine(protocol,16));content.Children.Add(labels);
                var outcome=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new(4,0,6,0)};var timing=Technical(Text("",11,Muted));timing.Margin=new(2,0,2,2);outcome.Children.Add(timing);var result=Text("",12,Muted);result.LineHeight=18;result.TextWrapping=TextWrapping.Wrap;var resultArea=CardStatus(result);resultArea.Height=36;outcome.Children.Add(resultArea);Grid.SetColumn(outcome,1);row.Children.Add(outcome);title.Margin=host.Margin=protocol.Margin=result.Margin=new(2,0,2,0);
                var select=new RadioButton{Content=content,GroupName="selected",IsChecked=item.Id==data.SelectedId,Margin=new(0),Padding=new(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type RadioButton}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='Transparent' Padding='0'><ContentPresenter/></Border></ControlTemplate>")};AutomationProperties.SetName(select,L.Text("SELECT")+" "+item.Name);
                select.Checked+=(_,_)=>SelectProfile20(item);row.Children.Add(select);
                var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var edit=MenuAction("EDIT",()=>Edit(item));var remove=MenuAction("DELETE",()=>Delete(item));var connectItem=MenuAction("CONNECT",()=>{if(state.State=="connected"&&activeProfile?.Id==item.Id)RequestDisconnect();else SwitchPath(item);});
                var editButton=IconButton("EDIT","M2,18 L16,4 L20,8 L6,22 H2 Z M14,6 L18,10",()=>Edit(item));editButton.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("IsEnabled"){Source=edit});controls.Children.Add(editButton);
                var menu=PathMenu(item,connectItem,edit,remove);controls.Children.Add(MoreButton(menu));
                var deleteButton=IconButton("DELETE","M5,6 H19 M8,6 V3 H16 V6 M7,9 V21 H17 V9",()=>Delete(item));deleteButton.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("IsEnabled"){Source=remove});controls.Children.Add(deleteButton);foreach(Button button in controls.Children){button.Width=44;button.Margin=new(0);button.Padding=new(10);button.Background=Brushes.Transparent;}
                Grid.SetColumn(controls,2);row.Children.Add(controls);var tile=Card(row,new(0,8,0,0));tile.Padding=new(8,2,4,2);tile.CornerRadius=new(16);tile.Height=60;RouteProfileCard20(tile,select,item);row.ClipToBounds=true;CardDepth(tile);LiftOnHover(tile);select.GotKeyboardFocus+=(_,_)=>tile.BorderBrush=Accent;select.LostKeyboardFocus+=(_,_)=>RefreshCards();paths.Children.Add(tile);rows.Add(new(){Profile=item,Tile=tile,Select=select,Connect=connectItem,Result=result,Timing=timing,Protocol=protocol,Edit=edit,Delete=remove});
            }
            stack.Children.Add(paths);var wrapper=new ElevatedBorder{Child=stack,Margin=new(0,0,0,16),Background=GlassSurface("Collection"),BorderBrush=GlassEdge(Tone("Outline")),BorderThickness=new(1),CornerRadius=new(22)};CardDepth(wrapper);cards.Children.Add(wrapper);groups.Add(new(){Key=group.Key,Members=members,Card=wrapper,Header=headerCard,Summary=summary,Paths=paths,Toggle=toggle,Connect=power,Account=account,Quota=quota,Menu=more.ContextMenu,Test=test,Stop=stop,More=more,Progress=progress});
        }
        RefreshCards();Dispatcher.BeginInvoke(()=>mainScroll.ScrollToVerticalOffset(offset),DispatcherPriority.Loaded);
    }
    ScrollViewer CardLine(TextBlock text,double height)
    {
        var scroll=new ScrollViewer{Content=text,Height=height,Focusable=false,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.HorizontalOnly};
        RouteCardWheel(scroll,true);return scroll;
    }
    ScrollViewer CardStatus(TextBlock text){var scroll=new ScrollViewer{Content=text,Height=20,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,PanningMode=PanningMode.VerticalOnly};RouteCardWheel(scroll,false);return scroll;}
    void RouteCardWheel(ScrollViewer scroll,bool horizontal)
    {
        scroll.PreviewMouseWheel+=(_,e)=>{
            double distance=-e.Delta/120d*48;
            if(horizontal&&scroll.ScrollableWidth>0&&System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)){scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset+distance);e.Handled=true;return;}
            if(!horizontal&&scroll.ScrollableHeight>0&&(e.Delta<0?scroll.VerticalOffset<scroll.ScrollableHeight:scroll.VerticalOffset>0))return;
            mainScroll.ScrollToVerticalOffset(mainScroll.VerticalOffset+distance);e.Handled=true;
        };
    }
}

