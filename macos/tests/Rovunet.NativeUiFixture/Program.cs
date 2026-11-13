using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Rovunet.Core;
using Rovunet.Desktop;
using Rovunet.Platform;

if(!OperatingSystem.IsMacOS()||args.Length!=1)return 2;
NativeCheck.Output=Path.GetFullPath(args[0]);Directory.CreateDirectory(NativeCheck.Output);
return AppBuilder.Configure<NativeCheck>().UsePlatformDetect()
    .With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.OpenGl,AvaloniaNativeRenderingMode.Software]})
    .StartWithClassicDesktopLifetime(args);

sealed class NativeCheck:Application
{
    public static string Output="";
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    public override void Initialize(){
        Styles.Add(new FluentTheme());
        Styles.Add(new Style(s=>s.OfType<TemplatedControl>()){Setters={new Setter(TemplatedControl.FontFamilyProperty,FontFamily.Default)}});
    }
    public override void OnFrameworkInitializationCompleted(){
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life){
            var seed=new AppState{Language="fa",Theme="dark",Expanded=new(){{"ui-fixture",true}},CloseToTray=false,WindowWidth=560,WindowHeight=680};
            for(int i=0;i<24;i++){
                var p=Importer.Single("vless://00000000-0000-4000-8000-000000000001@fixture.invalid:443?security=tls#"+Uri.EscapeDataString("کانفیگ آزمایشی با عنوان طولانی برای اندازه‌گیری حرکت نوشته‌ها در پنجرهٔ مک "+i));
                p.CollectionId="ui-fixture";p.CollectionName="آزمایش رابط";seed.Profiles.Add(p);
            }
            seed.SelectedId=seed.Profiles[0].Id;
            var window=new MainWindow(seed,true){Title="Rovunet · آزمون رابط"};
            life.MainWindow=window;window.Opened+=async(_,_)=>{
                try{await Run(window,seed);life.Shutdown(0);}catch(Exception e){Console.Error.WriteLine(e.GetBaseException().GetType().Name+": "+e.GetBaseException().Message);life.Shutdown(1);}
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
    static async Task Run(MainWindow w,AppState seed){
        var mw=typeof(MainWindow);object? Call(string n,params object?[] args)=>mw.GetMethod(n,Fields)!.Invoke(w,args);
        mw.GetField("activeId",Fields)!.SetValue(w,seed.SelectedId);
        mw.GetField("state",Fields)!.SetValue(w,new AgentReply{State="connected",HttpPort=18080,SocksPort=10808});
        Call("Build");w.Activate();await Task.Delay(900);
        var timer=(DispatcherTimer)typeof(OverflowMarquee).GetField("timer",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var active=typeof(OverflowMarquee).GetField("active",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        int ticks=0,layouts=0,changes=0;timer.Tick+=(_,_)=>ticks++;w.LayoutUpdated+=(_,_)=>layouts++;
        foreach(var banner in w.GetVisualDescendants().OfType<OverflowMarquee>())
            if(banner.Child?.RenderTransform is TranslateTransform move)move.PropertyChanged+=(_,e)=>{if(e.Property==TranslateTransform.XProperty)changes++;};
        using var proc=Process.GetCurrentProcess();var runs=new List<object>();
        async Task Measure(string label){
            proc.Refresh();var cpu=proc.TotalProcessorTime;var start=Stopwatch.GetTimestamp();var utc=DateTimeOffset.UtcNow;var t=ticks;var l=layouts;var c=changes;
            await Task.Delay(6000);proc.Refresh();var elapsed=Stopwatch.GetElapsedTime(start).TotalSeconds;
            runs.Add(new{label,utc,elapsed,focused=w.IsActive,visible=w.IsVisible,minimized=w.WindowState==WindowState.Minimized,clock=timer.IsEnabled,membership=(int)active.GetType().GetProperty("Count")!.GetValue(active)!,ticks=ticks-t,layouts=layouts-l,transformChanges=changes-c,cpuPercentOfOneCore=(proc.TotalProcessorTime-cpu).TotalSeconds*100/elapsed,rssBytes=proc.WorkingSet64,physicalFootprintBytes=Footprint()});
        }
        await Measure("moving-A1");timer.Stop();await Measure("static-B1");timer.Start();await Measure("moving-A2");timer.Stop();await Measure("static-B2");timer.Start();
        w.WindowState=WindowState.Minimized;await Task.Delay(600);await Measure("minimized");w.WindowState=WindowState.Normal;w.Activate();await Task.Delay(700);await Measure("restored");
        var captures=new List<object>();
        async Task Snap(Window target,string name){
            await Task.Delay(450);var handle=target.TryGetPlatformHandle()!;
            string file=Path.Combine(Output,name+".png");
            using var image=new RenderTargetBitmap(new PixelSize((int)target.Bounds.Width,(int)target.Bounds.Height),new Vector(96,96));
            image.Render(target);image.Save(file,PngBitmapEncoderOptions.Default);
            captures.Add(new{name,backend="native macOS window visual rendered in process",handle=handle.HandleDescriptor,width=target.Bounds.Width,height=target.Bounds.Height,focused=target.IsActive,bytes=new FileInfo(file).Length});

        }
        Call("ShowSettings");await Snap(w,"settings-fa-native");
        Call("ShowSearch");await Snap(w,"search-empty-fa-native");w.GetVisualDescendants().OfType<TextBox>().Single(b=>b.Name=="ProfileSearch").Text="کانفیگ";await Snap(w,"search-run-fa-native");
        var confirmation=(Task<bool>)Call("Confirm",string.Format(L.Text("MAC_TLS_EXCEPTION_CONFIRM"),"پروفایل آزمایشی"),CancellationToken.None,new string('a',64))!;
        await Task.Delay(200);var modal=w.OwnedWindows.Single();modal.Width=320;modal.Height=400;await Snap(modal,"tls-320-fa-native");
        modal.Close(false);if(await confirmation)throw new Exception("unexpected confirmation");
        var info=new{fixture="24 synthetic profiles, same window/renderer/font/session; fixture UI has no active agent, no traffic",assembly=typeof(MainWindow).Assembly.Location,sha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(MainWindow).Assembly.Location))),runs,captures};
        File.WriteAllText(Path.Combine(Output,"native-ui.json"),JsonSerializer.Serialize(info,new JsonSerializerOptions{WriteIndented=true}));mw.GetField("exiting",Fields)!.SetValue(w,true);
    }
    static long? Footprint(){
        // rusage_info_v2 starts with uuid followed by 64-bit counters.
        nint buf=Marshal.AllocHGlobal(1024);
        try{return proc_pid_rusage(Environment.ProcessId,2,buf)==0?Marshal.ReadInt64(buf,72):null;}finally{Marshal.FreeHGlobal(buf);}
    }
    [DllImport("/usr/lib/libproc.dylib")] static extern int proc_pid_rusage(int pid,int flavor,nint buffer);
}
