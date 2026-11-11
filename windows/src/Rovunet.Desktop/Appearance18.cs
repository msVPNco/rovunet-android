using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static bool MotionEnabled=>SystemParameters.ClientAreaAnimation&&!SystemParameters.HighContrast;
    static void Reveal(FrameworkElement element)
    {
        if(!MotionEnabled)return;
        element.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
        var transform=new TranslateTransform();element.RenderTransform=transform;
        transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(7,0,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
    }
    static void LiftOnHover(FrameworkElement element)
    {
        var offset=new TranslateTransform();element.RenderTransform=offset;
        void Lift(double y){if(MotionEnabled)offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(y,TimeSpan.FromMilliseconds(110)));}
        element.MouseEnter+=(_,_)=>Lift(-1.5);element.MouseLeave+=(_,_)=>Lift(0);
    }
    FrameworkElement LanguageFlags()
    {
        var flags=new StackPanel{Orientation=Orientation.Horizontal,FlowDirection=FlowDirection.LeftToRight,Tag="LANGUAGE_FLAGS"};
        foreach(var (code,title) in new[]{("fa","فارسی"),("en","English"),("ru","Русский"),("zh-CN","简体中文")}){
            var button=new Button{Width=44,Height=44,Padding=new(6),Margin=new(0),Tag="language:"+code,ToolTip=title,Content=FlagImage(code),Background=code==L.Language?GlassSurface("Selected"):Brushes.Transparent,BorderBrush=code==L.Language?Accent:Brushes.Transparent,BorderThickness=new(1),Template=(ControlTemplate)Application.Current.FindResource(code==L.Language?"RaisedButtonTemplate":"FlatContentButtonTemplate")};
            AutomationProperties.SetName(button,L.Text("LANGUAGE")+" · "+title);AutomationProperties.SetHelpText(button,code==L.Language?L.Text("SELECTED"):title);
            button.Click+=(_,_)=>ChangeLanguage(code);flags.Children.Add(button);
        }return flags;
    }
    void ChangeLanguage(string code)
    {
        if(data.Language==code)return;string prior=data.Language;data.Language=code;
        try{Save();}catch{data.Language=prior;lastError="PRIVATE_STORE_FAILED";SetState(state);return;}
        CloseAddHub();L.Language=code;ApplyTheme();Build();
    }
    static Image FlagImage(string code)
    {
        var drawing=new DrawingGroup();using(var dc=drawing.Open()){
            Brush Color(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            void Rect(string hex,double x,double y,double w,double h)=>dc.DrawRectangle(Color(hex),null,new(x,y,w,h));
            void Shape(string hex,string path)=>dc.DrawGeometry(Color(hex),null,Geometry.Parse(path));
            if(code=="ru"){Rect("#FFFFFF",0,0,30,20d/3);Rect("#0039A6",0,20d/3,30,20d/3);Rect("#D52B1E",0,40d/3,30,20d/3);}
            else if(code=="en"){
                Rect("#012169",0,0,30,20);Shape("#FFFFFF","M0,0 H4 L30,17 V20 H26 L0,3 Z M30,0 H26 L0,17 V20 H4 L30,3 Z");
                Shape("#C8102E","M0,0 L15,10 H12 L0,2 Z M30,0 L15,10 H18 L30,2 Z M0,20 L15,10 H18 L2,20 Z M30,20 L15,10 H12 L28,20 Z");
                Rect("#FFFFFF",11,0,8,20);Rect("#FFFFFF",0,6,30,8);Rect("#C8102E",12.5,0,5,20);Rect("#C8102E",0,7.5,30,5);
            }else if(code=="fa"){
                Rect("#239F40",0,0,30,20d/3);Rect("#FFFFFF",0,20d/3,30,20d/3);Rect("#DA0000",0,40d/3,30,20d/3);
                var emblem=new FormattedText("☫",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Symbol"),8,Color("#DA0000"),1);dc.DrawText(emblem,new(11,5));
                for(int x=1;x<30;x+=3){dc.DrawLine(new Pen(Brushes.White,.35),new(x,6.2),new(x+1.9,6.2));dc.DrawLine(new Pen(Brushes.White,.35),new(x,13.8),new(x+1.9,13.8));}
            }else{
                Rect("#DE2910",0,0,30,20);
                void Star(double x,double y,double radius,double rotation){var geometry=new StreamGeometry();using(var g=geometry.Open()){for(int i=0;i<10;i++){double a=(rotation+i*36)*Math.PI/180,r=i%2==0?radius:radius*.382;var point=new Point(x+Math.Cos(a)*r,y+Math.Sin(a)*r);if(i==0)g.BeginFigure(point,true,true);else g.LineTo(point,true,false);}}dc.DrawGeometry(Color("#FFDE00"),null,geometry);}
                Star(5,5,3,-90);foreach(var p in new[]{new Point(10,2),new Point(12,4),new Point(12,7),new Point(10,9)})Star(p.X,p.Y,1,Math.Atan2(5-p.Y,5-p.X)*180/Math.PI);
            }
        }drawing.Freeze();return new Image{Source=new DrawingImage(drawing),Width=30,Height=20,Stretch=Stretch.Fill,Clip=new RectangleGeometry(new(0,0,30,20),2,2)};
    }
}
