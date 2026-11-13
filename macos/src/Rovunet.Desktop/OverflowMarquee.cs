using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;

namespace Rovunet.Desktop;

// One clipped line, with a second visual copy for a continuous train. A shared
// clock updates only render transforms; text is remeasured on content/size changes.
public sealed class OverflowMarquee : Border
{
    const double Gap=32,PixelsPerSecond=26;
    static readonly HashSet<OverflowMarquee> active=[];
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(50)};
    readonly TextBlock label,copy;
    readonly Canvas rail=new(){FlowDirection=FlowDirection.LeftToRight};
    readonly TranslateTransform shift=new();
    readonly double lineHeight;
    double width,position,wait=1.25,lastTick;
    bool queued,reflowing,rtl,attached;
    Rect viewport;
    Window? hostWindow;
    public bool IsOverflowing {get;private set;}
    public double MotionOffset=>shift.X;
    public string Text=>label.Text??"";
    static OverflowMarquee(){timer.Tick+=(_,_)=>{double now=clock.Elapsed.TotalSeconds;foreach(var item in active){double delta=Math.Clamp(now-item.lastTick,0,.15);item.lastTick=now;item.Advance(delta);}};}
    public OverflowMarquee(TextBlock text,double height)
    {
        label=text;lineHeight=height;Height=height;ClipToBounds=true;FlowDirection=FlowDirection.LeftToRight;
        label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.None;label.Margin=new(0);label.Height=height;
        copy=new TextBlock{Height=height,TextWrapping=TextWrapping.NoWrap,IsHitTestVisible=false};
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(copy,Avalonia.Automation.AccessibilityView.Raw);
        copy.Bind(TextBlock.TextProperty,new Binding("Text"){Source=label});
        copy.Bind(TextBlock.FontFamilyProperty,new Binding("FontFamily"){Source=label});
        copy.Bind(TextBlock.FontSizeProperty,new Binding("FontSize"){Source=label});
        copy.Bind(TextBlock.FontWeightProperty,new Binding("FontWeight"){Source=label});
        copy.Bind(TextBlock.ForegroundProperty,new Binding("Foreground"){Source=label});
        copy.Bind(TextBlock.FlowDirectionProperty,new Binding("FlowDirection"){Source=label});
        copy.Bind(TextBlock.LineHeightProperty,new Binding("LineHeight"){Source=label});
        rail.Children.Add(label);rail.Children.Add(copy);rail.RenderTransform=shift;Child=rail;
        Avalonia.Automation.AutomationProperties.SetName(this,Text);
        label.PropertyChanged+=(_,e)=>{if(e.Property==TextBlock.TextProperty||e.Property==TextBlock.FontFamilyProperty||e.Property==TextBlock.FontSizeProperty||e.Property==TextBlock.FontWeightProperty||e.Property==TextBlock.FlowDirectionProperty)QueueReflow();};
        EffectiveViewportChanged+=(_,e)=>{viewport=e.EffectiveViewport;UpdateMembership();};
        PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty||e.Property==IsPointerOverProperty||e.Property==IsKeyboardFocusWithinProperty)UpdateMembership();};
        SizeChanged+=(_,_)=>QueueReflow();
        AttachedToVisualTree+=(_,_)=>{attached=true;hostWindow=TopLevel.GetTopLevel(this) as Window;if(hostWindow!=null)hostWindow.PropertyChanged+=HostWindowChanged;QueueReflow();};
        DetachedFromVisualTree+=(_,_)=>{attached=false;if(hostWindow!=null)hostWindow.PropertyChanged-=HostWindowChanged;hostWindow=null;active.Remove(this);UpdateClock();};
    }
    bool HasActiveWindow()=>hostWindow is {IsActive:true,IsVisible:true} window&&window.WindowState!=WindowState.Minimized;
    void HostWindowChanged(object? sender,AvaloniaPropertyChangedEventArgs e)
    {
        if(e.Property!=Window.IsActiveProperty&&e.Property!=Window.WindowStateProperty&&e.Property!=IsVisibleProperty)return;
        // Preserve the current reading position, without catching up for the
        // time spent in another app or in a minimized/hidden window.
        lastTick=clock.Elapsed.TotalSeconds;UpdateMembership();
    }
    static void UpdateClock(){if(active.Count>0){if(!timer.IsEnabled)timer.Start();}else timer.Stop();}
    void QueueReflow(){if(queued||reflowing)return;queued=true;Dispatcher.UIThread.Post(()=>{queued=false;Reflow();},DispatcherPriority.Background);}
    void Reflow()
    {
        if(reflowing)return;reflowing=true;
        try{
            label.Width=double.NaN;label.Measure(new Size(double.PositiveInfinity,lineHeight));width=Math.Ceiling(label.DesiredSize.Width)+1;
            label.Width=copy.Width=width;double available=Math.Max(0,Bounds.Width);rtl=label.FlowDirection==FlowDirection.RightToLeft;
            IsOverflowing=available>0&&width>available+1;double origin=rtl?available-width:0;
            Canvas.SetLeft(label,rtl?origin:0);Canvas.SetLeft(copy,origin+(rtl?-(width+Gap):width+Gap));copy.IsVisible=IsOverflowing;
            position=0;shift.X=0;wait=1.25;lastTick=clock.Elapsed.TotalSeconds;
            ToolTip.SetTip(this,Text);Avalonia.Automation.AutomationProperties.SetName(this,Text);
            UpdateMembership();
        }finally{reflowing=false;}
    }
    bool InViewport()=>attached&&IsOverflowing&&IsEffectivelyVisible&&HasActiveWindow()&&
        viewport.Intersects(new Rect(Bounds.Size));
    void UpdateMembership()
    {
        if(InViewport()&&!IsPointerOver&&!IsKeyboardFocusWithin){
            if(active.Add(this))lastTick=clock.Elapsed.TotalSeconds;
        }else active.Remove(this);
        UpdateClock();
    }
    void Advance(double elapsed)
    {
        if(!IsOverflowing||IsPointerOver||IsKeyboardFocusWithin)return;
        if(wait>0){double used=Math.Min(wait,elapsed);wait-=used;elapsed-=used;}
        if(elapsed<=0)return;position=(position+elapsed*PixelsPerSecond)%(width+Gap);shift.X=rtl?position:-position;
    }
}
