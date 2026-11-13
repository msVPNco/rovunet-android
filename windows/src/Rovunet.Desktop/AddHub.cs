using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    Button addButton=null!;Popup? addHub;
    void CloseAddHub(){if(addHub!=null)addHub.IsOpen=false;}
    void OpenAddHub()
    {
        if(addButton==null||busy||closing)return;
        if(addHub?.IsOpen==true){CloseAddHub();return;}
        var list=new StackPanel{Margin=new(6),MinWidth=290};
        void Entry(string key,string glyph,Action action){var button=Button(key,()=>{CloseAddHub();action();});button.Tag="add:"+key;button.Template=(ControlTemplate)Application.Current.FindResource("MenuRowTemplate");button.SetValue(TagProperty,Geometry.Parse(glyph));AutomationProperties.SetAutomationId(button,"add:"+key);button.MinHeight=52;button.Background=Brushes.Transparent;button.Margin=new(0);list.Children.Add(button);}
        Entry("ADD_LINK_FILE","M10,15 L7,18 A4,4 0 0 1 1,12 L6,7 A4,4 0 0 1 12,7 M14,9 L17,6 A4,4 0 0 1 23,12 L18,17 A4,4 0 0 1 12,17 M8,16 L16,8",ImportDialog);
        Entry("ADD_SUBSCRIPTION",MenuIcon("SUBSCRIPTIONS"),SubscriptionDialog);
        Entry("ADD_MANUAL",MenuIcon("EDIT"),NewProfileSelection);
        Entry("PUBLIC_SOURCES",MenuIcon("PUBLIC_SOURCES"),PublicSourcesPage);
        Entry("CHAIN_TITLE","M3,8 A5,5 0 0 1 13,8 V14 A5,5 0 0 1 3,14 Z M11,10 A5,5 0 0 1 21,10 V16 A5,5 0 0 1 11,16 Z",()=>BuildChainEditor25().ShowDialog());
        var surface=new ElevatedBorder{Child=list,Background=GlassSurface("Collection"),BorderBrush=GlassEdge(Accent),BorderThickness=new(1),CornerRadius=new(20),Padding=new(5),FlowDirection=L.Direction};CardDepth(surface);
        // Geometry fixtures must not be dismissed by unrelated desktop focus or pointer changes.
        addHub=new Popup{FlowDirection=FlowDirection.LeftToRight,PlacementTarget=addButton,Placement=PlacementMode.Custom,AllowsTransparency=true,StaysOpen=fixtureMode,Focusable=true,Child=new Border{Padding=new(18),FlowDirection=FlowDirection.LeftToRight,Child=surface},CustomPopupPlacementCallback=(popup,target,offset)=>[new(new Point(target.Width-popup.Width+18,-popup.Height+10),PopupPrimaryAxis.Horizontal)]};
        addHub.Opened+=(_,_)=>{Reveal(surface);((Button)list.Children[0]).Focus();};addHub.Closed+=(_,_)=>{if(IsVisible)addButton.Focus();};surface.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){CloseAddHub();e.Handled=true;}};addHub.IsOpen=true;
    }
    StackPanel GeneralMenu(Action close)
    {
        var panel=new StackPanel{Margin=new(18)};
        void Row(string key,Action action,bool enabled=true){var b=Button(key,()=>{close();action();});b.Template=(ControlTemplate)Application.Current.FindResource("MenuRowTemplate");b.Tag=Geometry.Parse(MenuIcon(key));b.Background=Brushes.Transparent;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.IsEnabled=enabled;panel.Children.Add(Card(b));}
        Row("SETTINGS",SettingsDialog);Row("CONNECTION_DETAILS",ConnectionDetails);Row("MANAGE_COLLECTIONS",CollectionManagement);Row("BACKUP_EXPORT",BackupExport);Row("DIAGNOSTICS",ExportDiagnostics);Row("EXIT",()=>_=ExitApplication());return panel;
    }
    void GeneralPage(){var page=Dialog("SECTION_GENERAL");page.UsePageChrome();page.Content=GeneralMenu(page.Close);page.ShowDialog();}
    void CollectionManagement()
    {
        var page=Dialog("MANAGE_COLLECTIONS");page.UsePageChrome();page.Content=BuildCollectionManagement(page.Close);page.ShowDialog();
    }
    StackPanel BuildCollectionManagement(Action close)
    {
        var panel=new StackPanel{Margin=new(18)};
        var choose=new ComboBox{DisplayMemberPath="Name",SelectedValuePath="Key",Tag="MANAGE_COLLECTION"};
        choose.ItemsSource=data.Profiles.GroupBy(CollectionKey).Select(g=>new{Key=g.Key,Name=g.First().CollectionName.Length>0?g.First().CollectionName:g.First().Name}).ToArray();choose.SelectedValue=Selected is {} selected?CollectionKey(selected):null;panel.Children.Add(OutlinedField("COLLECTION",choose));
        List<Core.Profile> Members()=>data.Profiles.Where(p=>CollectionKey(p)==choose.SelectedValue?.ToString()).ToList();
        var actions=new List<(Button Button,Func<bool> Enabled)>();
        void Item(string key,Action action,Func<bool> enabled){var b=Button(key,()=>{if(!enabled())return;close();action();});AutomationProperties.SetAutomationId(b,"manage:"+key);b.IsEnabled=enabled();actions.Add((b,enabled));panel.Children.Add(Card(b));}
        bool Idle()=>!busy&&state.State is "disconnected" or "error";
        bool Chosen()=>Members().Count>0;
        bool Measured()=>!busy&&Members().Any(p=>data.Measurements.ContainsKey(p.Id));
        Item("TEST_COLLECTION",()=>CollectionTestDialog(Members()),()=>Idle()&&Chosen());Item("STABILITY_TEST",()=>CollectionTestDialog(Members(),3),()=>Idle()&&Chosen());Item("TCP_CHECK",()=>TcpCollection(Members()),()=>!busy&&Chosen());Item("SORT_LATENCY",()=>SortGroup(Members(),false),Measured);Item("SORT_RELIABILITY",()=>SortGroup(Members(),true),Measured);Item("CLEAR_TESTS",()=>ClearTests(Members()),Measured);
        Item("REGROUP",Regroup,()=>Idle()&&data.Profiles.Count>0);Item("UNDO_GROUP",UndoGroups,()=>Idle()&&data.RegroupUndo.Count>0);Item("DUPLICATES",RemoveDuplicates,()=>Idle()&&data.Profiles.Count>0);
        choose.SelectionChanged+=(_,_)=>{foreach(var (button,enabled) in actions)button.IsEnabled=enabled();};return panel;
    }
    void BackupExport()
    {
        var page=Dialog("BACKUP_EXPORT");page.UsePageChrome();var panel=new StackPanel{Margin=new(18)};
        void Item(string key,Action action){var b=Button(key,()=>{page.Close();action();});b.IsEnabled=data.Profiles.Count>0;panel.Children.Add(Card(b));}
        Item("EXPORT_ALL",()=>ExportProfiles(data.Profiles));Item("EXPORT_ENCRYPTED",ExportBundle);page.Content=panel;page.ShowDialog();
    }
}
