using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace Rovunet.Desktop;

public static class Program
{
    [STAThread] public static void Main(string[] args)=>BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.OpenGl,AvaloniaNativeRenderingMode.Software]})
        .With(new MacOSPlatformOptions {ShowInDock=true,DisableDefaultApplicationMenuItems=true}).LogToTrace();
}
public sealed class App : Application
{
    public override void Initialize(){
        Name="Rovunet";Styles.Add(new FluentTheme());
        if(OperatingSystem.IsMacOS()){
            Styles.Add(new Style(selector=>selector.OfType<TemplatedControl>()){
                Setters={new Setter(TemplatedControl.FontFamilyProperty,FontFamily.Default)}
            });
        }
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;desktop.MainWindow=new MainWindow();}
        base.OnFrameworkInitializationCompleted();
    }
}
