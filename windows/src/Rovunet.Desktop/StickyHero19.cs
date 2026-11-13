using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    const double HeroExpanded19=148,HeroCompact19=88;
    Border heroCard19=null!,heroScrollSpacer19=null!;
    Viewbox heroRingScale19=null!;
    ColumnDefinition heroRingColumn19=null!;
    void HomeScrollChanged19(object sender,ScrollChangedEventArgs args)
    {
        if(!ReferenceEquals(sender,mainScroll))return;RefreshHeroCollapse19();
    }
    void RefreshHeroCollapse19()
    {
        if(heroCard19==null||heroScrollSpacer19==null||heroRingScale19==null)return;
        double travel=HeroExpanded19-HeroCompact19,forced=Math.Clamp(600-((Content as FrameworkElement)?.ActualHeight??Height),0,travel),collapse=Math.Max(forced,Math.Clamp(mainScroll.VerticalOffset,0,travel)),fraction=collapse/travel;
        heroCard19.Height=HeroExpanded19-collapse;
        // The spacer grows by exactly the height removed from the header. The scroll range
        // stays constant, avoiding offset clamping, jumps or oscillation at the bottom.
        heroScrollSpacer19.Height=collapse-forced;
        double expandedRing=compactLayout?112:124;double ring=expandedRing-(expandedRing-64)*fraction;heroRingScale19.Width=heroRingScale19.Height=ring;heroRingColumn19.Width=new(ring+16);
        heroTitle.FontSize=Typography.Size(24-4*fraction);heroTitle.MaxHeight=double.PositiveInfinity;heroTitle.TextWrapping=TextWrapping.NoWrap;heroTitle.TextTrimming=TextTrimming.None;
        heroNameLine21.Visibility=fraction>=.6||string.IsNullOrEmpty(heroName21.Text)?Visibility.Collapsed:Visibility.Visible;heroNameLine21.Opacity=1-fraction;
        heroDetailLine17.Visibility=fraction>=1?Visibility.Collapsed:Visibility.Visible;
        heroDetail.Visibility=fraction>=1?Visibility.Collapsed:Visibility.Visible;heroDetail.MaxHeight=20;heroDetail.Margin=new(0);heroDetail.Opacity=1-fraction;
        heroCard19.ToolTip=heroTitle.Text+" · "+heroName21.Text+"\n"+heroDetail.Text;
    }
    void WireHeroScroll19()
    {
        heroCard19.PreviewMouseWheel+=(_,e)=>{mainScroll.ScrollToVerticalOffset(mainScroll.VerticalOffset-e.Delta/120d*48);e.Handled=true;};
        Point? origin=null;double initial=0;bool dragging=false;
        heroCard19.PreviewTouchDown+=(_,e)=>{origin=e.GetTouchPoint(heroCard19).Position;initial=mainScroll.VerticalOffset;dragging=false;};
        heroCard19.PreviewTouchMove+=(_,e)=>{if(origin is not {} start)return;double dy=e.GetTouchPoint(heroCard19).Position.Y-start.Y;if(!dragging&&Math.Abs(dy)<12)return;dragging=true;heroCard19.CaptureTouch(e.TouchDevice);mainScroll.ScrollToVerticalOffset(initial-dy);e.Handled=true;};
        heroCard19.PreviewTouchUp+=(_,e)=>{origin=null;if(dragging){heroCard19.ReleaseTouchCapture(e.TouchDevice);e.Handled=true;}dragging=false;};
    }
}
