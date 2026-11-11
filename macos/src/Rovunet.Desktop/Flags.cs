using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShapePath=Avalonia.Controls.Shapes.Path;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    Control Flag(string code)
    {
        var canvas=new Canvas{Width=30,Height=20,ClipToBounds=true};
        void Rect(string color,double x,double y,double width,double height){var r=new Border{Width=width,Height=height,Background=Brush.Parse(color)};Canvas.SetLeft(r,x);Canvas.SetTop(r,y);canvas.Children.Add(r);}
        void Shape(string color,string data)=>canvas.Children.Add(new ShapePath{Data=Geometry.Parse(data),Fill=Brush.Parse(color)});
        if(code=="ru"){Rect("#FFFFFF",0,0,30,7);Rect("#0039A6",0,7,30,6);Rect("#D52B1E",0,13,30,7);}
        else if(code=="en"){Rect("#012169",0,0,30,20);Shape("#FFFFFF","M0,0 H4 L30,17 V20 H26 L0,3 Z M30,0 H26 L0,17 V20 H4 L30,3 Z");Rect("#FFFFFF",11,0,8,20);Rect("#FFFFFF",0,6,30,8);Rect("#C8102E",12.5,0,5,20);Rect("#C8102E",0,7.5,30,5);}
        else if(code=="fa")return new Image{Source=IranFlagDrawing.Source,Width=30,Height=20,Stretch=Stretch.Uniform,Name="IranLionSunFlag"};
        else{Rect("#DE2910",0,0,30,20);Shape("#FFDE00","M5,2 L6,4 L8,4 L6.5,5.5 L7,8 L5,6.5 L3,8 L3.5,5.5 L2,4 L4,4 Z");foreach(var (x,y) in new[]{(10d,2d),(12d,4d),(12d,7d),(10d,9d)})Rect("#FFDE00",x,y,1.5,1.5);}
        return canvas;
    }
}
