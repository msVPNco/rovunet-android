using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Rovunet.Desktop;

// One clipped line, with a second visual copy for a continuous train. A shared
// clock updates only render transforms; text is remeasured on content/size changes.
public sealed class OverflowMarquee : Border
{
    const double Gap=32,PixelsPerSecond=26;
    static readonly HashSet<OverflowMarquee> active=[];
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    readonly TextBlock label,copy;
    readonly Canvas rail=new(){FlowDirection=FlowDirection.LeftToRight};
    readonly TranslateTransform shift=new();
    readonly double lineHeight;
    double width,position,wait=1.25,lastTick;
    bool queued,reflowing,rtl,attached;
    public bool IsOverflowing {get;private set;}
    public double MotionOffset=>shift.X;
    public string Text=>label.Text??"";
    static OverflowMarquee(){timer.Tick+=(_,_)=>{double now=clock.Elapsed.TotalSeconds;foreach(var item in active.ToArray()){double delta=Math.Clamp(now-item.lastTick,0,.15);item.lastTick=now;if(item.InViewport())item.Advance(delta);}};}
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
        SizeChanged+=(_,_)=>QueueReflow();
        AttachedToVisualTree+=(_,_)=>{attached=true;QueueReflow();};
        DetachedFromVisualTree+=(_,_)=>{attached=false;active.Remove(this);if(active.Count==0)timer.Stop();};
    }
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
            if(attached&&IsOverflowing){active.Add(this);timer.Start();}else{active.Remove(this);if(active.Count==0)timer.Stop();}
        }finally{reflowing=false;}
    }
    bool InViewport()
    {
        if(!IsOverflowing||!IsEffectivelyVisible)return false;
        var top=TopLevel.GetTopLevel(this);if(top==null||!top.IsVisible)return false;
        var origin=this.TranslatePoint(default,top);if(origin==null)return false;var rect=new Rect(origin.Value,Bounds.Size);
        if(!rect.Intersects(new Rect(top.Bounds.Size)))return false;
        foreach(var scroll in this.GetVisualAncestors().OfType<ScrollViewer>()){var start=scroll.TranslatePoint(default,top);if(start!=null&&!rect.Intersects(new Rect(start.Value,scroll.Bounds.Size)))return false;}
        return true;
    }
    void Advance(double elapsed)
    {
        if(!IsOverflowing||IsPointerOver||IsKeyboardFocusWithin)return;
        if(wait>0){double used=Math.Min(wait,elapsed);wait-=used;elapsed-=used;}
        if(elapsed<=0)return;position=(position+elapsed*PixelsPerSecond)%(width+Gap);shift.X=rtl?position:-position;
    }
}
