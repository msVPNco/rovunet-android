using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;

namespace Rovunet.Desktop;

// All programmatic dialogs share bounds, scrolling and a real cancel action.
public sealed class DialogWindow : Window
{
    readonly ContentControl body = new();
    readonly DockPanel root = new();
    readonly Button cancel;
    readonly string pageTitle;
    DockPanel? toolbar;
    TextBlock? titleLabel;
    Window? logicalOwner;
    bool presented,disposingShell;
    bool? result;
    DispatcherFrame? frame;
    internal static readonly List<DialogWindow> OpenPages=[];
    internal Action? BackAction;
    internal MainWindow? PageHost=>logicalOwner as MainWindow ?? (logicalOwner as DialogWindow)?.PageHost;
    internal Window? NativeOwner=>PageHost??logicalOwner;
    public new Window? Owner {get=>logicalOwner;set=>logicalOwner=value;}
    public new bool IsVisible=>presented||base.IsVisible;
    public new bool? DialogResult {get=>PageHost!=null?result:base.DialogResult;set{if(PageHost==null){base.DialogResult=value;return;}result=value;if(presented)Close();}}
    public new double ActualWidth=>presented?root.ActualWidth:base.ActualWidth;
    public new double ActualHeight=>presented?root.ActualHeight:base.ActualHeight;
    public new double Width {get=>base.Width;set{base.Width=value;if(presented&&PageHost is {} host)host.Width=value;}}
    public new double Height {get=>base.Height;set{base.Height=value;if(presented&&PageHost is {} host)host.Height=value;}}
    public new void UpdateLayout(){if(presented)PageHost!.UpdateLayout();else base.UpdateLayout();}
    public new void Show()
    {
        if(PageHost==null){base.Owner=logicalOwner;base.Show();return;}
        if(presented)return;base.Content=null;presented=true;OpenPages.Add(this);PageHost.PushPage(this);RaiseEvent(new RoutedEventArgs(LoadedEvent,this));
    }
    public new bool? ShowDialog()
    {
        if(PageHost==null){base.Owner=logicalOwner;return base.ShowDialog();}
        Show();if(!presented)return result;frame=new DispatcherFrame();try{Dispatcher.PushFrame(frame);}finally{frame=null;}return result;
    }
    public new void Close()
    {
        if(!presented){base.Close();return;}
        var args=new CancelEventArgs();base.OnClosing(args);if(args.Cancel)return;
        presented=false;OpenPages.Remove(this);PageHost!.PopPage(this);if(frame!=null)frame.Continue=false;disposingShell=true;base.Close();
    }
    protected override void OnClosing(CancelEventArgs e){if(!disposingShell)base.OnClosing(e);}
    public new object Content { get => body.Content; set => body.Content = value; }
    public DialogWindow(Window? owner, string title, double width, double height)
    {
        pageTitle=title;Title = "Rovunet · " + title; Owner = owner; Style = (Style)Application.Current.FindResource(typeof(Window));FontFamily=Typography.Family(L.Language);
        FlowDirection = FlowDirection.LeftToRight;root.FlowDirection=L.Direction; Language = XmlLanguage.GetLanguage(L.Language);
        root.SetResourceReference(Panel.BackgroundProperty,"PageBg");
        var area = SystemParameters.WorkArea;
        Width = Math.Min(width, area.Width - 24); Height = Math.Min(height + 56, area.Height - 24);
        MinWidth = Math.Min(420, Width); MinHeight = Math.Min(280, Height);
        WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        cancel = new Button { Content = L.Text("CLOSE"), IsCancel = true, HorizontalAlignment = HorizontalAlignment.Left, Margin = new(20, 8, 20, 12) };
        cancel.Click += (_, _) => Close(); DockPanel.SetDock(cancel, Dock.Bottom); root.Children.Add(cancel);
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,Padding=new(8,0,8,12) });
        base.Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    public void UsePageChrome()
    {
        if(toolbar!=null)return;root.Children.Remove(cancel);
        toolbar=new DockPanel{Margin=new(12,6,12,6)};DockPanel.SetDock(toolbar,Dock.Top);
        var back=new Button{IsCancel=true,Content=new System.Windows.Shapes.Path{Data=Geometry.Parse("M16,4 L8,12 L16,20 M8,12 H22"),Stroke=(Brush)FindResource("Ink"),StrokeThickness=1.7,Width=22,Height=22,Stretch=Stretch.Uniform},ToolTip=L.Text("BACK"),Width=48,Height=48,Padding=new(12),Background=Brushes.Transparent};back.Click+=(_,_)=>{if(BackAction!=null)BackAction();else Close();};System.Windows.Automation.AutomationProperties.SetName(back,L.Text("BACK"));DockPanel.SetDock(back,Dock.Left);toolbar.Children.Add(back);
        titleLabel=new TextBlock{Text=pageTitle,FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new(6,0,6,0),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};toolbar.Children.Add(titleLabel);root.Children.Insert(0,toolbar);
    }
    internal void SetPageTitle(string text){UsePageChrome();titleLabel!.Text=text;}
    internal void AddToolbarAction(Button action){UsePageChrome();DockPanel.SetDock(action,Dock.Right);toolbar!.Children.Insert(1,action);}
    internal void UseFixedBody(){var scroll=root.Children.OfType<ScrollViewer>().Single();scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;body.HorizontalContentAlignment=HorizontalAlignment.Stretch;body.VerticalContentAlignment=VerticalAlignment.Stretch;}
    public void SetPageAction(Button action,bool icon=true)
    {
        UsePageChrome();if(action.Parent is Panel parent)parent.Children.Remove(action);
        action.ToolTip=action.Content;if(icon){action.Content=new System.Windows.Shapes.Path{Data=Geometry.Parse("M3,12 L9,18 L22,4"),FlowDirection=FlowDirection.LeftToRight,Stroke=(Brush)FindResource("Ink"),StrokeThickness=1.8,Width=22,Height=22,Stretch=Stretch.Uniform};action.Background=Brushes.Transparent;action.Width=48;action.Padding=new(12);}else{action.MinWidth=110;action.Padding=new(10,4,10,4);}action.Height=48;DockPanel.SetDock(action,Dock.Right);toolbar!.Children.Insert(1,action);
    }
    internal FrameworkElement PageVisual => root;
    internal double ScrollOffset=>root.Children.OfType<ScrollViewer>().Single().VerticalOffset;
    internal void RestoreScroll(double offset)=>root.Children.OfType<ScrollViewer>().Single().ScrollToVerticalOffset(offset);
}
