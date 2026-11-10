using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    // Code-native trace of the owner's supplied geometric R and underline.
    // The original artwork and its hash are retained in Assets/Brand and docs.
    FrameworkElement BrandMark()
    {
        var shape=new System.Windows.Shapes.Path{
            Data=Geometry.Parse("F0 M4,0 H329 C416,0 466,57 466,148 C466,238 416,296 334,296 L476,482 H317 L178,296 H129 V482 H2 Z M127,101 H333 V201 H127 Z M0,516 H476 V534 H0 Z"),
            Fill=SystemParameters.HighContrast?SystemColors.WindowTextBrush:new SolidColorBrush(darkTheme?Color.FromRgb(35,178,186):Color.FromRgb(10,132,144)),
            Stretch=Stretch.Uniform
        };
        var mark=new Viewbox{Width=36,Height=40,Child=shape,FlowDirection=FlowDirection.LeftToRight,Margin=new(0,0,2,0),Tag="ROVUNET_BRAND_MARK",IsHitTestVisible=false};
        var wordmark=Text(L.Text("BRAND_WORDMARK"),28);wordmark.Margin=new(0);wordmark.FontWeight=FontWeights.Bold;wordmark.VerticalAlignment=VerticalAlignment.Center;
        var brand=new StackPanel{Orientation=Orientation.Horizontal,FlowDirection=FlowDirection.LeftToRight,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(brand,"Rovunet");brand.Children.Add(mark);brand.Children.Add(wordmark);return brand;
    }
}
