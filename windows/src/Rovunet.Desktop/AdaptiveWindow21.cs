using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    HwndSource? boundsSource21;
    bool boundsQueued21;
    [StructLayout(LayoutKind.Sequential)] struct NativeRect21 { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo21 { public int Size;public NativeRect21 Monitor,Work;public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetMonitorInfoW")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool GetMonitorInfo21(IntPtr monitor,ref MonitorInfo21 info);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool GetWindowRect(IntPtr window,out NativeRect21 rect);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    void WireAdaptiveBounds21()
    {
        if(fixtureMode)return;
        SourceInitialized+=(_,_)=>{boundsSource21=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);boundsSource21?.AddHook(WindowMessage21);QueueBounds21();};
        Loaded+=(_,_)=>QueueBounds21();
        Closed+=(_,_)=>{boundsSource21?.RemoveHook(WindowMessage21);boundsSource21=null;};
    }
    IntPtr WindowMessage21(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message is 0x02E0 or 0x007E or 0x001A or 0x0232)QueueBounds21();
        return IntPtr.Zero;
    }
    void QueueBounds21()
    {
        if(boundsQueued21||closing)return;boundsQueued21=true;
        Dispatcher.BeginInvoke(()=>{boundsQueued21=false;FitCurrentMonitor21();},DispatcherPriority.ContextIdle);
    }
    void FitCurrentMonitor21()
    {
        if(closing||WindowState!=WindowState.Normal)return;
        var hwnd=new WindowInteropHelper(this).Handle;if(hwnd==IntPtr.Zero)return;
        var info=new MonitorInfo21{Size=Marshal.SizeOf<MonitorInfo21>()};
        if(!GetMonitorInfo21(MonitorFromWindow(hwnd,2),ref info)||!GetWindowRect(hwnd,out var rect))return;
        double scale=Math.Max(96,GetDpiForWindow(hwnd))/96d;
        var work=new UiPolicy.Frame(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
        var current=new UiPolicy.Frame(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
        MinWidth=Math.Min(480,Math.Max(120,(work.Width-32*scale)/scale));
        MinHeight=Math.Min(880,Math.Max(120,(work.Height-32*scale)/scale));
        var preferred=current with{Width=Math.Max(current.Width,MinWidth*scale),Height=Math.Max(current.Height,MinHeight*scale)};
        var fit=UiPolicy.FitWindow(preferred,work,scale);
        if(Math.Abs(fit.X-current.X)<1&&Math.Abs(fit.Y-current.Y)<1&&Math.Abs(fit.Width-current.Width)<1&&Math.Abs(fit.Height-current.Height)<1)return;
        SetWindowPos(hwnd,IntPtr.Zero,(int)Math.Round(fit.X),(int)Math.Round(fit.Y),(int)Math.Floor(fit.Width),(int)Math.Floor(fit.Height),0x0014);
    }
}
