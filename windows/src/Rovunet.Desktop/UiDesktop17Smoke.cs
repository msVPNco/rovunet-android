using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunDesktop17Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string folder=Path.GetDirectoryName(report)!;var before=new WinInetProxy().Read();
        try{
            var profile=Importer.Single(ManualProfiles.Dns("soar","Soar fixture","t.example.invalid","127.0.0.1:5353","udp",Convert.ToBase64String(new byte[32]),"127.0.0.1","22","fixture","fixture"));
            profile.CollectionName="Fixture collection";profile.CollectionId="fixture";data.Profiles=[profile];data.SelectedId=profile.Id;data.ExpandedCollections["fixture"]=true;
            foreach(string language in new[]{"en","fa"})foreach(string theme in new[]{"amoled","light"}){
                L.Language=language;data.Theme=theme;Width=1180;Height=860;ApplyTheme();Build();await Idle17();string prefix=language+"-"+theme;
                var page=BuildResolverFinder(profile);page.Show();await Idle17();
                checks[prefix+"_wide_sidebar"]=contextHost.IsVisible&&Math.Abs(contextHost.ActualWidth-222)<1;
                checks[prefix+"_same_window"]=NavigationDepth==1&&!Application.Current.Windows.Cast<Window>().Any(w=>w is DialogWindow&&w.IsLoaded);
                var input=Visuals<TextBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_INPUT"));input.Text="1.1.1.1:53\n8.8.8.8:53";
                var source=Visuals<ComboBox>(page.PageVisual).Single(b=>Equals(b.Tag,"SCAN_SOURCE"));source.SelectedValue="input";input.Text="1.1.1.1:53\n8.8.8.8:53";
                SavePage((FrameworkElement)Content,Path.Combine(folder,"scanner-wide-"+prefix+".png"));
                Width=680;await Idle17();checks[prefix+"_compact_sidebar_hidden"]=!contextHost.IsVisible&&navigationShell!.ColumnDefinitions[0].Width.Value==0;
                checks[prefix+"_resize_keeps_input"]=input.Text=="1.1.1.1:53\n8.8.8.8:53"&&NavigationDepth==1;
                checks[prefix+"_scanner_options_present"]=Visuals<CheckBox>(page.PageVisual).Any(c=>Equals(c.Tag,"SCAN_SHUFFLE"))&&Visuals<CheckBox>(page.PageVisual).Any(c=>Equals(c.Tag,"SCAN_NEARBY"));
                var model=Visuals<ComboBox>(page.PageVisual).Single(c=>Equals(c.Tag,"SCAN_MODE"));checks[prefix+"_four_scan_modes"]=model.Items.Count==4;
                SavePage((FrameworkElement)Content,Path.Combine(folder,"scanner-compact-"+prefix+".png"));page.Close();await Idle17();
                Width=1180;await Idle17();checks[prefix+"_return_home"]=NavigationDepth==0&&contextHost.IsVisible;
                var settings=Dialog("SETTINGS");settings.Content=BuildSettingsPanel(()=>settings.Close());settings.Show();await Idle17();SavePage((FrameworkElement)Content,Path.Combine(folder,"settings-"+prefix+".png"));settings.Close();
                var importer=BuildAddConfiguration();importer.Show();await Idle17();SavePage((FrameworkElement)Content,Path.Combine(folder,"import-"+prefix+".png"));importer.Close();
                var group=groups.Single();var menu=group.Menu;menu.PlacementTarget=group.More;menu.IsOpen=true;await Idle17();
                checks[prefix+"_collection_menu_complete"]=new[]{"CONNECT","TEST_COLLECTION","STABILITY_TEST","STOP_TEST","PATH_SELECTION","RENAME","SHARE_QR","MOVE_COLLECTION"}.All(key=>menu.Items.OfType<MenuItem>().Any(i=>Equals(i.Tag,key)));
                SavePage(menu,Path.Combine(folder,"collection-menu-"+prefix+".png"));menu.IsOpen=false;
                var row=rows.Single();var pathMenu=Visuals<Button>(row.Tile).Single(b=>b.ContextMenu!=null).ContextMenu!;pathMenu.PlacementTarget=row.Select;pathMenu.IsOpen=true;await Idle17();
                checks[prefix+"_path_menu_complete"]=new[]{"CONNECT","LAST_TEST","QUICK_HTTPS","STABILITY_TEST","STOP_TEST","PIN_TOP","SHARE_QR","EDIT","DELETE"}.All(key=>pathMenu.Items.OfType<MenuItem>().Any(i=>Equals(i.Tag,key)));
                SavePage(pathMenu,Path.Combine(folder,"path-menu-"+prefix+".png"));pathMenu.IsOpen=false;
            }
            await ScannerSelectionSmoke(checks,report);
            await ExtraFormsSmoke(checks,folder);
            await NearbyScannerSmoke(checks);
            await ScannerResultsSmoke(checks,folder);
            int trayActions=0;using(var fixtureTray=new TrayIcon(()=>{},()=>trayActions++,()=>{},visible:false)){
                fixtureTray.Update("disconnected");checks["tray_disconnected_offers_connect"]=fixtureTray.ActionKey=="CONNECT"&&fixtureTray.ActionEnabled;fixtureTray.InvokeConnectionAction();
                fixtureTray.Update("connected");checks["tray_connected_offers_disconnect"]=fixtureTray.ActionKey=="DISCONNECT"&&fixtureTray.ActionEnabled;fixtureTray.InvokeConnectionAction();
                fixtureTray.Update("connecting",operationBusy:true);checks["tray_connecting_offers_cancel"]=fixtureTray.ActionKey=="CANCEL"&&fixtureTray.ActionEnabled;fixtureTray.InvokeConnectionAction();
                fixtureTray.Update("error");checks["tray_error_offers_retry"]=fixtureTray.ActionKey=="CONNECT"&&fixtureTray.ActionEnabled;fixtureTray.InvokeConnectionAction();
                fixtureTray.Update("disconnecting");checks["tray_disconnecting_cannot_restart"]=!fixtureTray.ActionEnabled;fixtureTray.InvokeConnectionAction();
                checks["tray_real_menu_clicks_dispatch_toggle"]=trayActions==4;
                fixtureTray.Update("disconnected",operationBusy:true);checks["tray_busy_test_blocks_connect"]=!fixtureTray.ActionEnabled;
            }
            checks["system_proxy_unchanged"]=before==new WinInetProxy().Read();
            checks["no_remaining_pages"]=NavigationDepth==0;
        }catch(Exception error){checks["exception_"+error.GetType().Name]=false;File.WriteAllText(Path.Combine(folder,"fixture-error.txt"),error.ToString());}
        bool passed=checks.Count>0&&checks.Values.All(x=>x);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Desktop17 navigation, menus, inputs, scanner selection",synthetic_data=true,loopback_dns_worker_tested=true,external_network_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
    async Task Idle17(){UpdateLayout();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
}

