using Avalonia;
using Avalonia.Media;

namespace Rovunet.Desktop;

// Windows UiShell/PortraitCards tokens; black backdrop follows the owner's third reference.
public sealed class Palette
{
    readonly Dictionary<string,SolidColorBrush> tones=[];
    readonly Dictionary<string,LinearGradientBrush> glass=[];
    public bool Dark {get;private set;}
    public bool Black {get;private set;}
    public Palette(string mode,bool systemDark)=>Update(mode,systemDark);
    public void Update(string mode,bool systemDark){Dark=mode is "dark" or "amoled"||mode=="system"&&systemDark;Black=mode=="amoled";foreach(var pair in tones)pair.Value.Color=Color.Parse(ColorOf(pair.Key));foreach(var pair in glass){var fresh=CreateGlass(pair.Key);for(int i=0;i<2;i++)pair.Value.GradientStops[i].Color=fresh.GradientStops[i].Color;}}
    public IBrush this[string key]=>tones.TryGetValue(key,out var brush)?brush:tones[key]=new SolidColorBrush(Color.Parse(ColorOf(key)));
    public string ColorOf(string key)=>key switch {
        "Bg"=>Black?"#000000":Dark?"#1B263D":"#F3F4FC",
        "Card"=>Black?"#131A22":Dark?"#1B2740":"#FFFFFF",
        "Soft"=>Black?"#182029":Dark?"#33415F":"#F7F6FC",
        "Sidebar"=>Black?"#0B0D14":Dark?"#19243C":"#F0EFF8",
        "Collection"=>Black?"#252935":Dark?"#62517C":"#E7E0F5",
        "Selected"=>Black?"#303444":Dark?"#746191":"#E7DFF6",
        "StatusSurface"=>Black?"#13151D":Dark?"#303B57":"#EAE7F2",
        "Ink"=>Dark?"#F1F3FC":"#19233A",
        "Muted"=>Dark?"#CBD0E4":"#46526A",
        "StaleMetric"=>Dark?"#ADB5C8":"#8891A6",
        "Accent"=>Dark?"#CEBBFF":"#6345AC",
        "OnAccent"=>Dark?ColorOf("Bg"):"#FFFFFF",
        "Outline"=>Dark?"#726B91":"#BAC2D5",
        "Green"=>Dark?"#9AE5BF":"#087849",
        "PowerSurface"=>"#A6EAC4",
        "AddSurface"=>"#DCD8F1",
        "Red"=>Dark?"#FF9BAB":"#AD2645",
        _=>Dark?"#F1F3FC":"#19233A"
    };
    public IBrush Glass(string key)=>glass.TryGetValue(key,out var brush)?brush:glass[key]=CreateGlass(key);
    LinearGradientBrush CreateGlass(string key)
    {
        var color=Color.Parse(ColorOf(key));
        Color Lift(double a)=>Color.FromRgb((byte)(color.R+(255-color.R)*a),(byte)(color.G+(255-color.G)*a),(byte)(color.B+(255-color.B)*a));
        return new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(.7,1,RelativeUnit.Relative),GradientStops=[new GradientStop(Lift(Dark?.065:.05),0),new GradientStop(color,1)]};
    }
    public IBrush Backdrop=>Black?this["Bg"]:new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(.8,1,RelativeUnit.Relative),GradientStops=[new GradientStop(Color.Parse(Dark?"#344264":"#EAE7F7"),0),new GradientStop(Color.Parse(Dark?"#23324A":"#F6F5FC"),.4),new GradientStop(Color.Parse(Dark?"#1B263D":"#EAEFF8"),1)]};
}
