using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    const double ContextThreshold = 920;
    Border connectionStrip = null!;
    Border contextHost = null!;
    StackPanel connectionControls = null!;
    string contextPage = "menu";
    static readonly Geometry PowerIcon = Geometry.Parse("M12,2 V11 M6,5 A9,9 0 1 0 18,5");

    void ShowSearch() { search.Visibility = Visibility.Visible; search.Focus(); }
    void OpenContext(string page)
    {
        contextPage = page;
        var dialog = Dialog(page == "settings" ? "SETTINGS" : "MORE", 500, 800); dialog.UsePageChrome();
        dialog.Content = page == "settings" ? BuildSettingsPanel(() => dialog.Close()) : BuildAppMenu(() => dialog.Close());
        dialog.ShowDialog();
    }
    void PopulateContext()
    {
        if(compactLayout)return;
        sidebarRows20.Clear();var body=new DockPanel{Background=GlassSurface("Sidebar"),LastChildFill=true};
        var brand=BrandMark();brand.Margin=new(18,16,12,12);var brandFrame=new Border{FlowDirection=FlowDirection.LeftToRight,HorizontalAlignment=HorizontalAlignment.Stretch,Child=brand};DockPanel.SetDock(brandFrame,Dock.Top);body.Children.Add(brandFrame);
        Button Row(string key,Action action){var button=Button(key,()=>NavigateTo(()=>{sidebarRoute20=key;RefreshSidebar20();action();}));button.MinHeight=52;button.FontSize=17;button.Margin=new(8,1,8,1);button.Background=key=="HOME"?GlassSurface("Selected"):Brushes.Transparent;button.Template=(ControlTemplate)Application.Current.FindResource("MenuRowTemplate");button.FocusVisualStyle=(Style)Application.Current.FindResource("MenuRowFocusVisual20");sidebarRows20[key]=button;button.Tag=Geometry.Parse(MenuIcon(key));AutomationProperties.SetAutomationId(button,"nav:"+key);return button;}
        var footer=new StackPanel{Margin=new(4,6,4,10)};footer.Children.Add(new Border{Height=1,Background=Tone("Outline"),Margin=new(14,2,14,10)});footer.Children.Add(Row("HELP",Help));footer.Children.Add(Row("ABOUT",About));
        var versionText=Text("Rovunet "+Version.Split('+')[0],10,Muted);versionText.Margin=new(18,6,4,0);versionText.FlowDirection=FlowDirection.LeftToRight;footer.Children.Add(versionText);DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var list=new StackPanel{Margin=new(4,0,4,0)};
        void Caption(string key){var line=new DockPanel{Margin=new(16,20,16,8)};var label=Text(L.Text(key),14,Muted);label.Margin=new(0,0,10,0);DockPanel.SetDock(label,Dock.Left);line.Children.Add(label);line.Children.Add(new Border{Height=1,VerticalAlignment=VerticalAlignment.Center,Background=Tone("Outline")});list.Children.Add(line);}
        list.Children.Add(Row("HOME",()=>{}));Caption("SECTION_GENERAL");list.Children.Add(Row("SETTINGS",SettingsDialog));list.Children.Add(Row("CONNECTION_DETAILS",ConnectionDetails));list.Children.Add(Row("MANAGE_COLLECTIONS",CollectionManagement));list.Children.Add(Row("BACKUP_EXPORT",BackupExport));list.Children.Add(Row("DIAGNOSTICS",ExportDiagnostics));Caption("NAV_TOOLS");list.Children.Add(Row("DNS_SCANNER",ScanMenu));
        body.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});contextHost.Child=body;RefreshSidebar20();
    }
    void Build()
    {
        CloseAddHub();
        compactLayout = (ActualWidth > 0 ? ActualWidth : Width) < ContextThreshold;
        FontFamily=Typography.Family(L.Language);TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
        FlowDirection=FlowDirection.LeftToRight; Language=XmlLanguage.GetLanguage(L.Language); cardsSignature="";rows.Clear();groups.Clear();
        var root=new Grid { FlowDirection=L.Direction, Background=Tone("Bg") };
        root.ColumnDefinitions.Add(new() {Width=compactLayout?new(0):new(260)});
        root.ColumnDefinitions.Add(new() {Width=new(1,GridUnitType.Star)});
        var home=new Grid{Background=HomeBackdrop()};Grid.SetColumn(home,1);home.RowDefinitions.Add(new(){Height=GridLength.Auto});home.RowDefinitions.Add(new(){Height=GridLength.Auto});home.RowDefinitions.Add(new(){Height=GridLength.Auto});home.RowDefinitions.Add(new());home.RowDefinitions.Add(new(){Height=GridLength.Auto});root.Children.Add(home);
        var header=new DockPanel { FlowDirection=FlowDirection.LeftToRight,Margin=new(14,8,12,6),LastChildFill=true };
        var actions=new StackPanel { FlowDirection=L.Direction,Orientation=Orientation.Horizontal };
        void HeaderAction(string key,string glyph,Action action) {var b=IconButton(key,glyph,action);b.Background=Brushes.Transparent;b.Margin=new(1);b.Width=44;actions.Children.Add(b);}
        actions.Children.Add(LanguageFlags());
        HeaderAction("SEARCH",MenuIcon("SEARCH"),ShowSearch);
        HeaderAction("SETTINGS",MenuIcon("SETTINGS"),SettingsDialog);
        HeaderAction("SECTION_GENERAL","M12,3 L12,5 M12,11 L12,13 M12,19 L12,21",GeneralPage);
        DockPanel.SetDock(actions,Dock.Right);header.Children.Add(actions);header.Children.Add(compactLayout?BrandMark():Text(L.Text("HOME"),24));home.Children.Add(header);
        search=new TextBox { Visibility=Visibility.Collapsed, Margin=new(18,0,18,8),MinHeight=44,VerticalContentAlignment=VerticalAlignment.Center,ToolTip=L.Text("SEARCH") };AutomationProperties.SetName(search,L.Text("SEARCH"));search.TextChanged+=(_,_)=>RefreshCards();Grid.SetRow(search,1);home.Children.Add(search);
        heroScrollSpacer19=new Border{Height=0,IsHitTestVisible=false};cards=new StackPanel { Margin=new(24,10,24,96) };mainScroll=new ScrollViewer {Content=cards,Margin=new(0),PanningMode=PanningMode.VerticalOnly,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};mainScroll.ScrollChanged+=HomeScrollChanged19;Grid.SetRow(mainScroll,3);home.Children.Add(mainScroll);heroCard19=(Border)BuildHero();Grid.SetRow(heroCard19,2);home.Children.Add(heroCard19);WireHeroScroll19();
        addButton=Button("ADD",OpenAddHub,true);addButton.FlowDirection=FlowDirection.LeftToRight;addButton.Width=64;addButton.Height=64;addButton.Padding=new(18);addButton.Margin=new(0,0,26,18);addButton.HorizontalAlignment=HorizontalAlignment.Right;addButton.VerticalAlignment=VerticalAlignment.Bottom;addButton.Template=(ControlTemplate)Application.Current.FindResource("RaisedButtonTemplate");addButton.Content=new System.Windows.Shapes.Path{Data=Geometry.Parse("M12,2 V22 M2,12 H22"),Stroke=Tone("Bg"),StrokeThickness=2.4,Width=28,Height=28,Stretch=Stretch.Uniform};AutomationProperties.SetAutomationId(addButton,"ADD_HUB");addButton.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,BlurRadius=18,ShadowDepth=7,Opacity=.45};var actionOverlay=new Grid{FlowDirection=FlowDirection.LeftToRight};actionOverlay.Children.Add(addButton);Grid.SetRow(actionOverlay,3);Panel.SetZIndex(actionOverlay,5);home.Children.Add(actionOverlay);

        var status=new StackPanel();connectingProgress=new ProgressBar{Height=2,IsIndeterminate=true};status.Children.Add(connectingProgress);
        var statusRow=new DockPanel();shield=BuildShield(38,true);shield.Background=Brushes.Transparent;shield.Padding=new(6);shield.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(shield,Dock.Left);statusRow.Children.Add(shield);
        var statusText=new StackPanel {Margin=new(8,0,4,0)};connectionText=Text("",16);connectionText.FontWeight=FontWeights.SemiBold;globalStatus=connectionText;detail=Text("",11,Muted);detail.MaxHeight=36;statusText.Children.Add(connectionText);statusText.Children.Add(detail);statusRow.Children.Add(statusText);status.Children.Add(statusRow);
        var traffic=new Grid {Margin=new(8,4,8,0)};traffic.ColumnDefinitions.Add(new());traffic.ColumnDefinitions.Add(new());
        var ratesRow=new StackPanel {Orientation=Orientation.Horizontal,FlowDirection=FlowDirection.LeftToRight};trafficUp=Technical(Text("",11,Muted));trafficDown=Technical(Text("",11,Muted));ratesRow.Children.Add(trafficUp);ratesRow.Children.Add(trafficDown);traffic.Children.Add(ratesRow);
        var totalRow=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,FlowDirection=FlowDirection.LeftToRight};trafficTotal=Technical(Text("",11,Muted));totalRow.Children.Add(trafficTotal);Grid.SetColumn(totalRow,1);traffic.Children.Add(totalRow);status.Children.Add(traffic);
        var timer=new WrapPanel();sleepCountdown=Text("",11);cancelSleep=Button("CANCEL_SLEEP_TIMER",CancelSleepTimer);timer.Children.Add(sleepCountdown);timer.Children.Add(cancelSleep);status.Children.Add(timer);
        notice=Text("",11,Green);notice.Visibility=Visibility.Collapsed;status.Children.Add(notice);errorText=Text("",11,Red);errorText.MaxHeight=60;status.Children.Add(errorText);
        connectionStrip=new Border {Background=GlassSurface("StatusSurface"),CornerRadius=new(18,18,0,0),Padding=new(12,10,12,8),Child=status};Grid.SetRow(connectionStrip,4);home.Children.Add(connectionStrip);
        BuildConnectionControls();
        contextHost=new Border {Visibility=compactLayout?Visibility.Collapsed:Visibility.Visible,BorderBrush=Tone("Outline"),BorderThickness=L.Direction==FlowDirection.RightToLeft?new(1,0,0,0):new(0,0,1,0)};Grid.SetColumn(contextHost,0);root.Children.Add(contextHost);SetHomeContent(root);PopulateContext();
        SetState(state);BuildCards();RefreshTrafficText();UpdateTitleTheme();
    }
    void BuildConnectionControls()
    {
        // These controls are now in connection details, rather than a technical dashboard on Home.
        connectionControls=new StackPanel {Margin=new(20)};
        connectionControls.Children.Add(Text(L.Text("CONNECTION_DETAILS"),22));
        proxy=new CheckBox {Content=L.Text("SYSTEM_PROXY"),IsChecked=data.UseSystemProxy};
        RoutedEventHandler choice=(_,_)=>{bool old=data.UseSystemProxy;data.UseSystemProxy=proxy.IsChecked==true;try{Save();}catch{data.UseSystemProxy=old;lastError="PRIVATE_STORE_FAILED";}SetState(state);};proxy.Checked+=choice;proxy.Unchecked+=choice;connectionControls.Children.Add(proxy);
        void Port(string label,out TextBlock value,out Button copy,Func<int> number) {var line=new DockPanel{FlowDirection=FlowDirection.LeftToRight};copy=IconButton("COPY","M8,8 H20 V21 H8 Z M4,16 H2 V2 H15 V4",()=>{if(number()>0){Clipboard.SetText("127.0.0.1:"+number().ToString(CultureInfo.InvariantCulture));Notify(L.Text("COPIED"));}});DockPanel.SetDock(copy,Dock.Right);line.Children.Add(copy);value=Technical(Text("",14));line.Children.Add(value);connectionControls.Children.Add(Text(label,11,Muted));connectionControls.Children.Add(line);}
        Port("HTTP",out httpAddress,out copyHttp,()=>state.HttpPort);Port("SOCKS5",out socksAddress,out copySocks,()=>state.SocksPort);
        TextBlock Meta(string key){connectionControls.Children.Add(Text(L.Text(key),11,Muted));var value=Text("",14);connectionControls.Children.Add(value);return value;}
        pathValue=Meta("ACTIVE_PATH");protocolValue=Meta("PROTOCOL");durationValue=Meta("DURATION");lastTestValue=Meta("LAST_TEST");trafficRecorded=Meta("RECORDED_TRAFFIC");latencyAge=Text("",11,Muted);connectionControls.Children.Add(latencyAge);trafficLine=new(){Stroke=Accent,StrokeThickness=1.6};var graph=new Canvas{Height=30,Width=100,ClipToBounds=true};graph.Children.Add(trafficLine);connectionControls.Children.Add(graph);
        verify=Button("VERIFY_HTTPS",ProbeDialog);connectionControls.Children.Add(verify);
    }
    void RefreshPowerButton()
    {
        if (connect == null) return;
        connect.Content=new System.Windows.Shapes.Path{Data=PowerIcon,Stroke=state.State=="connected"||!darkTheme?Brushes.White:Tone("Bg"),StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=42,Height=42,Stretch=Stretch.Uniform};
        connect.Background=state.State=="connected"?Tone("PowerSurface"):Accent;connect.ToolTip=L.Text(state.State=="connected"?"DISCONNECT":state.State=="connecting"?"CANCEL":"CONNECT");
    }
}
