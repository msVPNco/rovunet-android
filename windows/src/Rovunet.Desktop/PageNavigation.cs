using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    Grid? navigationShell;
    readonly ContentControl homeContent=new();
    readonly ContentControl sidebarContent=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
    readonly ContentControl pageContent=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,MaxWidth=1000};
    readonly List<DialogWindow> pageStack=[];
    readonly Dictionary<DialogWindow,IInputElement?> pageFocus=[];
    readonly Dictionary<DialogWindow,double> pageOffsets=[];
    double homeOffset;
    bool homeLayoutDirty;
    internal int NavigationDepth=>pageStack.Count;
    void SetHomeContent(UIElement home)
    {
        if(navigationShell==null){navigationShell=new Grid();navigationShell.ColumnDefinitions.Add(new());navigationShell.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});navigationShell.SetResourceReference(Panel.BackgroundProperty,"PageBg");navigationShell.Children.Add(sidebarContent);Grid.SetColumn(homeContent,1);Grid.SetColumn(pageContent,1);navigationShell.Children.Add(homeContent);navigationShell.Children.Add(pageContent);Content=navigationShell;
            PreviewKeyDown+=(_,e)=>{if(pageStack.Count>0&&e.Key==Key.Escape){pageStack[^1].Close();e.Handled=true;}};
        }
        // Keep desktop navigation present while the content area hosts a nested editor or scanner.
        var layout=(Grid)home;var body=layout.Children.Cast<UIElement>().First(e=>e!=contextHost);
        layout.Children.Remove(body);layout.Children.Remove(contextHost);homeContent.Content=null;sidebarContent.Content=null;
        navigationShell.FlowDirection=L.Direction;navigationShell.ColumnDefinitions[0].Width=new(compactLayout?0:260);
        homeContent.Content=body;sidebarContent.Content=contextHost;RenderPage();
    }
    async void NavigateTo(Action action)
    {
        if(!await CloseNavigation())return;
        try{action();if(NavigationDepth==0){sidebarRoute20="HOME";RefreshSidebar20();}}catch(Exception error){lastError=error is Rovunet.Core.SafeError safe?safe.Code:"UNEXPECTED_ERROR";SetState(state);}
    }
    void GoHome()=>NavigateTo(()=>{});
    internal void PushPage(DialogWindow page)
    {
        CloseAddHub();
        if(pageStack.Count>0)pageOffsets[pageStack[^1]]=pageStack[^1].ScrollOffset;else homeOffset=mainScroll?.VerticalOffset??0;
        pageFocus[page]=Keyboard.FocusedElement;pageStack.Add(page);RenderPage();UpdateLayout();
    }
    internal void PopPage(DialogWindow page)
    {
        pageStack.Remove(page);pageOffsets.Remove(page);RenderPage();UpdateLayout();
        if(pageStack.Count>0)pageStack[^1].RestoreScroll(pageOffsets.GetValueOrDefault(pageStack[^1]));else mainScroll?.ScrollToVerticalOffset(homeOffset);
        UpdateLayout();
        if(pageFocus.Remove(page,out var focus)&&focus is UIElement{IsVisible:true,IsEnabled:true})Keyboard.Focus(focus);
        if(pageStack.Count==0 && (homeLayoutDirty||compactLayout!=((ActualWidth>0?ActualWidth:Width)<ContextThreshold))){homeLayoutDirty=false;RebuildForResize();}
    }
    void ResizeInnerPage(double width)
    {
        bool compact=width<ContextThreshold;if(compact==compactLayout||navigationShell==null)return;
        compactLayout=compact;homeLayoutDirty=true;navigationShell.ColumnDefinitions[0].Width=new(compact?0:260);
        contextHost.Visibility=compact?Visibility.Collapsed:Visibility.Visible;if(!compact)PopulateContext();
    }
    void RenderPage()
    {
        if(navigationShell==null)return;pageContent.Content=null;
        if(pageStack.Count==0){sidebarRoute20="HOME";RefreshSidebar20();}
        bool open=pageStack.Count>0;homeContent.Visibility=open?Visibility.Collapsed:Visibility.Visible;pageContent.Visibility=open?Visibility.Visible:Visibility.Collapsed;
        if(open)pageContent.Content=pageStack[^1].PageVisual;
    }
    async Task<bool> CloseNavigation()
    {
        foreach(var page in pageStack.AsEnumerable().Reverse().ToArray())page.Close();
        var until=DateTime.UtcNow.AddSeconds(20);while(pageStack.Count>0&&DateTime.UtcNow<until)await Task.Delay(25);return pageStack.Count==0;
    }
}
