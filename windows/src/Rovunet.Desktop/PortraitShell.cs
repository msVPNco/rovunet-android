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
        Button Row(string key,Action action){var button=Button(key,()=>NavigateTo(()=>{sidebarRoute20=key;RefreshSidebar20();action();}));button.MinHeight=48;button.FontSize=14;button.Margin=new(6,1,6,1);button.Background=key=="HOME"?GlassSurface("Selected"):Brushes.Transparent;button.Template=(ControlTemplate)Application.Current.FindResource("MenuRowTemplate");button.FocusVisualStyle=(Style)Application.Current.FindResource("MenuRowFocusVisual20");sidebarRows20[key]=button;button.Content=MovingLine21(Text(L.Text(key),14),22,L.Direction);button.ToolTip=L.Text(key);button.Tag=Geometry.Parse(MenuIcon(key));AutomationProperties.SetAutomationId(button,"nav:"+key);return button;}
        var footer=new StackPanel{Margin=new(4,6,4,10)};footer.Children.Add(new Border{Height=1,Background=Tone("Outline"),Margin=new(14,2,14,10)});footer.Children.Add(Row("HELP",Help));footer.Children.Add(Row("ABOUT",About));
        var versionText=Text("Rovunet "+Version.Split('+')[0],11,Muted);versionText.Margin=new(18,6,4,0);versionText.FlowDirection=FlowDirection.LeftToRight;footer.Children.Add(versionText);DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var list=new StackPanel{Margin=new(4,0,4,0)};
        void Caption(string key){var line=new DockPanel{Margin=new(16,20,16,8)};var label=Text(L.Text(key),12,Muted);label.Margin=new(0,0,10,0);DockPanel.SetDock(label,Dock.Left);line.Children.Add(label);line.Children.Add(new Border{Height=1,VerticalAlignment=VerticalAlignment.Center,Background=Tone("Outline")});list.Children.Add(line);}
        list.Children.Add(Row("HOME",()=>{}));Caption("SECTION_GENERAL");list.Children.Add(Row("SETTINGS",SettingsDialog));list.Children.Add(Row("CONNECTION_DETAILS",ConnectionDetails));list.Children.Add(Row("MANAGE_COLLECTIONS",CollectionManagement));list.Children.Add(Row("BACKUP_EXPORT",BackupExport));list.Children.Add(Row("DIAGNOSTICS",ExportDiagnostics));Caption("NAV_TOOLS");list.Children.Add(Row("DNS_SCANNER",ScanMenu));
        body.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});contextHost.Child=body;RefreshSidebar20();
    }
    void Build() => BuildApprovedShell21();
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
        connect.Content=new System.Windows.Shapes.Path{Data=PowerIcon,Stroke=Tone(state.State=="connected"?"PowerInk":"AddInk"),StrokeThickness=2.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Width=34,Height=34,Stretch=Stretch.Uniform};
        connect.Background=state.State=="connected"?Tone("PowerSurface"):Tone("AddSurface");connect.ToolTip=L.Text(state.State=="connected"?"DISCONNECT":state.State=="connecting"?"CANCEL":"CONNECT");
    }
}
