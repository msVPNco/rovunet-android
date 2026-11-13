using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    DialogWindow BuildAppUpdatePage28(AppUpdateClient28? service=null,Func<AppUpdate28,string?>? pick=null,bool checkOnOpen=false)
    {
        var page=Dialog("APP_UPDATE_TITLE",580,640);var panel=new StackPanel{Margin=new(24)};service??=new();
        var current=Text("",16);current.Tag="update:current";var route=Text("",14,Muted);var status=Text("",16);status.Tag="update:status";
        var release=Text("",17);release.FlowDirection=FlowDirection.LeftToRight;release.FontWeight=FontWeights.SemiBold;release.Tag="update:release";
        var info=Text("",14,Muted);info.Tag="update:integrity-note";var progress=new ProgressBar{Minimum=0,Maximum=1,Height=6,Margin=new(4,16,4,16),Visibility=Visibility.Collapsed};
        var saved=Text("",13,Muted);saved.FlowDirection=FlowDirection.LeftToRight;saved.Tag="update:saved";var actions=new WrapPanel();
        AppUpdate28? candidate=null;CancellationTokenSource? operation=null;bool running=false,checkedOnce=false;string statusKey="APP_UPDATE_READY",savedPath="";Brush statusInk=Muted;
        var check=Button("APP_UPDATE_CHECK",()=>{},true);check.Tag="update:check";var download=Button("APP_UPDATE_DOWNLOAD",()=>{},true);download.Tag="update:download";
        var cancel=Button("CANCEL",()=>operation?.Cancel());cancel.Tag="update:cancel";
        int Route()=>state.State=="connected"?(state.HttpPort is >0 and <=65535?state.HttpPort:throw new SafeError("APP_UPDATE_ROUTE_CHANGED")):0;
        void Render(){
            page.SetPageTitle(L.Text("APP_UPDATE_TITLE"));current.Inlines.Clear();current.Inlines.Add(new System.Windows.Documents.Run(L.Text("APP_UPDATE_CURRENT")+" · "));current.Inlines.Add(new System.Windows.Documents.Run(Version.Split('+')[0]){FlowDirection=FlowDirection.LeftToRight});route.Text=L.Text(state.State=="connected"?"APP_UPDATE_VIA_TUNNEL":"APP_UPDATE_DIRECT");
            release.TextAlignment=L.Direction==FlowDirection.RightToLeft?TextAlignment.Right:TextAlignment.Left;
            status.Text=L.Text(statusKey);status.Foreground=statusInk;release.Text=candidate==null?"":"Rovunet Windows "+candidate.Version+" · x64";info.Text=L.Text("APP_UPDATE_INTEGRITY_NOTE");saved.Text=savedPath;
            check.Content=L.Text(checkedOnce?"APP_UPDATE_RETRY":"APP_UPDATE_CHECK");download.Content=L.Text("APP_UPDATE_DOWNLOAD");cancel.Content=L.Text("CANCEL");
            check.IsEnabled=!running;download.IsEnabled=!running&&candidate!=null;download.Visibility=candidate==null?Visibility.Collapsed:Visibility.Visible;cancel.Visibility=running?Visibility.Visible:Visibility.Collapsed;
        }
        async Task Run(bool fetching){
            if(running||fetching&&candidate==null)return;string? destination=null;
            if(fetching){if(pick!=null)destination=pick(candidate!);else{var picker=new SaveFileDialog{FileName=candidate!.Name,Filter="Windows installer|*.exe",OverwritePrompt=true};if(picker.ShowDialog(page.NativeOwner)==true)destination=picker.FileName;}if(destination==null)return;}
            var lease=routeGeneration.Capture();operation=CancellationTokenSource.CreateLinkedTokenSource(lease.Cancelled);var stop=operation.Token;int port=0;
            if(!fetching)candidate=null;
            running=true;statusKey=fetching?"APP_UPDATE_DOWNLOADING":"APP_UPDATE_CHECKING";statusInk=Muted;progress.Visibility=Visibility.Visible;progress.IsIndeterminate=!fetching;progress.Value=0;Render();
            try{
                port=Route();
                if(fetching){await service.Download(candidate!,port,destination!,new Progress<double>(value=>{progress.Value=value;}),stop,commit=>routeGeneration.Commit(lease,commit));savedPath=destination!;statusKey="APP_UPDATE_DOWNLOADED";statusInk=Green;}
                else{var received=await service.Check(Version,port,stop);routeGeneration.Commit(lease,()=>candidate=received);checkedOnce=true;statusKey=candidate==null?"APP_UPDATE_CURRENT_OK":"APP_UPDATE_AVAILABLE";statusInk=candidate==null?Green:Accent;}
            }catch(Exception ex){statusKey=!routeGeneration.IsCurrent(lease)?"APP_UPDATE_ROUTE_CHANGED":ex is SafeError safe?safe.Code:ex is OperationCanceledException?"CANCELLED":"APP_UPDATE_NETWORK_FAILED";statusInk=statusKey=="CANCELLED"?Muted:Red;checkedOnce=true;}
            finally{running=false;operation.Dispose();operation=null;progress.Visibility=Visibility.Collapsed;Render();}
        }
        check.Click+=async(_,_)=>await Run(false);download.Click+=async(_,_)=>await Run(true);
        actions.Children.Add(check);actions.Children.Add(download);actions.Children.Add(cancel);panel.Children.Add(current);panel.Children.Add(route);panel.Children.Add(release);panel.Children.Add(status);panel.Children.Add(progress);panel.Children.Add(actions);panel.Children.Add(info);panel.Children.Add(saved);page.Content=panel;
        page.RelocalizePage=Render;page.Closed+=(_,_)=>operation?.Cancel();if(checkOnOpen)page.Loaded+=async(_,_)=>await Run(false);Render();return page;
    }
}
