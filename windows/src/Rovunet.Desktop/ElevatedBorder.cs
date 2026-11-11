using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Rovunet.Desktop;

// Render the shadow on its own surface so text and controls never enter an effect bitmap.
internal sealed class ElevatedBorder : Border
{
    readonly Border shadow=new(){Background=System.Windows.Media.Brushes.Black,IsHitTestVisible=false};
    readonly Border face=new(){IsHitTestVisible=false};
    bool raised;
    internal ElevatedBorder(){AddVisualChild(shadow);AddVisualChild(face);}
    internal void SetDepth(Color color,double opacity)
    {
        raised=true;shadow.Effect=new DropShadowEffect{Color=color,BlurRadius=16,ShadowDepth=4,Direction=270,Opacity=opacity,RenderingBias=RenderingBias.Quality};InvalidateVisual();
    }
    protected override int VisualChildrenCount=>base.VisualChildrenCount+2;
    protected override Visual GetVisualChild(int index)=>index switch{0=>shadow,1=>face,_=>base.GetVisualChild(index-2)};
    protected override Size MeasureOverride(Size constraint){shadow.Measure(constraint);face.Measure(constraint);return base.MeasureOverride(constraint);}
    protected override Size ArrangeOverride(Size finalSize)
    {
        shadow.Arrange(new Rect(finalSize));face.Arrange(new Rect(finalSize));return base.ArrangeOverride(finalSize);
    }
    protected override void OnRender(DrawingContext dc)
    {
        shadow.Visibility=face.Visibility=raised?Visibility.Visible:Visibility.Collapsed;
        if(!raised){base.OnRender(dc);return;}
        // The face is rendered on a non-interactive visual. Keep a hit surface
        // on this border so padding and text-free areas remain real card targets.
        dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));
        shadow.CornerRadius=face.CornerRadius=CornerRadius;face.Background=Background;face.BorderBrush=BorderBrush;face.BorderThickness=BorderThickness;
    }
}
