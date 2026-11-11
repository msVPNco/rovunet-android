using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
namespace Rovunet.Desktop;

sealed class TrayIcon : IDisposable
{
    readonly Forms.NotifyIcon icon;
    readonly Forms.ToolStripMenuItem connectionAction=new(),showAction=new(),exitAction=new();
    readonly Dictionary<string,System.Drawing.Icon> images=[];
    internal string State {get;private set;}="";
    internal string ActionKey=>State switch{"connected"=>"DISCONNECT","connecting"=>"CANCEL","disconnecting"=>"DISCONNECTING",_=>"CONNECT"};
    internal bool ActionEnabled=>connectionAction.Enabled;
    internal void InvokeConnectionAction()=>connectionAction.PerformClick();
    internal static string ColorFor(string state)=>state switch{"connected"=>"#19B879","error"=>"#E74C58","connecting" or "disconnecting"=>"#E8AD46",_=>"#D5C6FF"};
    internal static System.Drawing.Icon Render(string state)
    {
        var visual=new DrawingVisual();using(var draw=visual.RenderOpen()){
            draw.PushTransform(new TranslateTransform(2,1));draw.PushTransform(new ScaleTransform(1.2,1.2));
            var shape=(Geometry)Application.Current.FindResource("Shield_disconnected");draw.DrawGeometry(new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorFor(state))),null,shape);
            draw.Pop();draw.Pop();
        }
        var pixels=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);pixels.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(pixels));using var bytes=new MemoryStream();png.Save(bytes);bytes.Position=0;
        using var bitmap=new System.Drawing.Bitmap(bytes);IntPtr handle=bitmap.GetHicon();try{using var borrowed=System.Drawing.Icon.FromHandle(handle);return(System.Drawing.Icon)borrowed.Clone();}finally{DestroyIcon(handle);}
    }
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
    public TrayIcon(Action show,Action toggle,Action exit,bool visible=true)
    {
        icon=new(){Text="Rovunet"};Update("disconnected");icon.Visible=visible;
        var menu=new Forms.ContextMenuStrip();showAction.Click+=(_,_)=>show();connectionAction.Click+=(_,_)=>toggle();exitAction.Click+=(_,_)=>exit();menu.Items.Add(showAction);menu.Items.Add(connectionAction);menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add(exitAction);icon.ContextMenuStrip=menu;icon.DoubleClick+=(_,_)=>show();
        Update("disconnected");
    }
    public void Update(string state,bool canConnect=true,bool operationBusy=false)
    {
        state=state is "connected" or "error" or "connecting" or "disconnecting"?state:"disconnected";
        if(State!=state){if(!images.TryGetValue(state,out var image))images[state]=image=Render(state);icon.Icon=image;State=state;}
        string text="Rovunet · "+L.Text(state.ToUpperInvariant());icon.Text=text.Length>63?text[..63]:text;
        showAction.Text=L.Text("SHOW_WINDOW");exitAction.Text=L.Text("EXIT");connectionAction.Text=L.Text(ActionKey);
        connectionAction.Enabled=state is "connected" or "connecting"||state!="disconnecting"&&!operationBusy&&canConnect;
    }
    public void HiddenNotice()=>icon.ShowBalloonTip(4000,"Rovunet",L.Text("TRAY_HINT"),Forms.ToolTipIcon.Info);
    public void Dispose(){icon.Visible=false;icon.ContextMenuStrip?.Dispose();icon.Dispose();foreach(var image in images.Values)image.Dispose();}
}
