using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunPortraitPreview(string report)
    {
        var checks=new Dictionary<string,bool>();
        try
        {
            var profiles=new List<Profile>();
            Profile Add(string raw,string group,string title){var p=Importer.Single(raw);p.CollectionId=group;p.CollectionName=title;profiles.Add(p);return p;}
            Add(ManualProfiles.Native("ssh","direct-ssh","gateway.example.invalid",22,"fixture","fixture"),"europe","Europe");
            var selected=Add("vless://00000000-0000-4000-8000-000000000001@gateway.example.invalid:443?type=ws&security=tls&path=%2Ffixture#vless","europe","Europe");
            Add(ManualProfiles.Dns("sayedns","noizdns-socks","t.example.invalid","127.0.0.1","udp",new string('a',64),"127.0.0.1","22","fixture","fixture"),"europe","Europe");
            Add(ManualProfiles.Dns("slipstream","slipstream-socks","s.example.invalid","127.0.0.1","udp","","127.0.0.1","22","fixture","fixture"),"europe","Europe");
            Add("socks5://gateway.example.invalid:1080#Route%201","canada","Canada");Add("socks5://gateway.example.invalid:1081#Route%202","canada","Canada");
            Add(ManualProfiles.Native("ssh","SSH","gateway.example.invalid",22,"fixture","fixture"),"secure","Secure SSH");
            data.Profiles=profiles;data.SelectedId=selected.Id;data.Measurements[selected.Id]=new(){Verified=true,ElapsedMs=42,SampledAt=DateTimeOffset.UtcNow};data.ExpandedCollections["europe"]=true;activeProfile=selected;
            foreach(var example in new[]{("portrait-en-dark","en","dark",520d), ("portrait-fa-dark","fa","dark",520d), ("wide-en-dark","en","dark",1040d), ("portrait-en-light","en","light",520d)})
            {
                L.Language=example.Item2;data.Theme=example.Item3;Width=example.Item4;Height=900;ApplyTheme();Build();SetState(new(){State="connected",HttpPort=21001,SocksPort=21002});
                await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
                var visual=(FrameworkElement)Content;var pixels=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);pixels.Render(visual);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(pixels));
                using(var output=new FileStream(Path.Combine(Path.GetDirectoryName(report)!,example.Item1+".png"),FileMode.CreateNew))png.Save(output);
                checks[example.Item1+"_profile_viewport"]=mainScroll.ViewportHeight>=200;
                checks[example.Item1+"_context_threshold"]=contextHost.IsVisible==(example.Item4>=ContextThreshold);
                checks[example.Item1+"_real_collection_counts"]=groups.Count==3 && groups[0].Members.Count==4 && rows.Count==7;
                checks[example.Item1+"_brand_at_header_corner"]=BrandAtCorner();
                checks[example.Item1+"_power_and_add_clear_status"]=connect.TranslatePoint(new Point(0,connect.ActualHeight),this).Y<=connectionStrip.TranslatePoint(new Point(),this).Y;
            }
            profiles[0].Name=new string('W',140)+" مسیر طولانی";
            profiles[0].CollectionName=new string('W',100)+" مجموعه";
            L.Language="fa";
            foreach(double width in new[]{420d,520d,919d,920d,1200d})
            {
                Width=width;Height=600;Build();UpdateLayout();
                checks["width_"+width+"_context_threshold"]=contextHost.IsVisible==(width>=ContextThreshold);
                checks["width_"+width+"_rtl_brand_at_corner"]=BrandAtCorner();
                checks["width_"+width+"_viewport"]=mainScroll.ViewportHeight>=100;
                checks["width_"+width+"_path_actions_not_clipped"]=Visuals<System.Windows.Controls.Button>(rows[0].Tile).All(b=>{var point=b.TranslatePoint(new Point(),this);return point.X>=0 && point.X+b.ActualWidth<=ActualWidth;});
                checks["width_"+width+"_list_extends_behind_floating_buttons"]=mainScroll.TranslatePoint(new Point(0,mainScroll.ActualHeight),this).Y>connect.TranslatePoint(new Point(0,connect.ActualHeight),this).Y;
            state = new() { State = "connected", HttpPort = 21001, SocksPort = 21002 };
            Width = 1040; Height = 820; Build(); await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); UpdateLayout();
            var rebuilt = (FrameworkElement)Content;
            checks["rebuild_keeps_connected_ring"] = connect.Background is System.Windows.Media.SolidColorBrush ringBrush && ringBrush.Color == ((System.Windows.Media.SolidColorBrush)Tone("PowerSurface")).Color;
            var rebuiltPixels = new RenderTargetBitmap((int)Math.Ceiling(rebuilt.ActualWidth), (int)Math.Ceiling(rebuilt.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            rebuiltPixels.Render(rebuilt);
            var rebuiltPng = new PngBitmapEncoder(); rebuiltPng.Frames.Add(BitmapFrame.Create(rebuiltPixels));
            using (var rebuiltOutput = new FileStream(Path.Combine(Path.GetDirectoryName(report)!, "wide-after-rebuild.png"), FileMode.Create)) rebuiltPng.Save(rebuiltOutput);
            }
        }
        catch(Exception error){checks["failure_"+error.GetType().Name]=false;}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);
        using var stream=new FileStream(report,FileMode.CreateNew);JsonSerializer.Serialize(stream,new{suite="Actual WPF portrait design preview",synthetic_profiles=true,connection_state_simulated=true,external_network_tested=false,checks,passed},new JsonSerializerOptions{WriteIndented=true});return passed;
        bool BrandAtCorner()
        {
            var root=(FrameworkElement)Content;var mark=Visuals<FrameworkElement>(root).Single(e=>Equals(e.Tag,"ROVUNET_BRAND_MARK"));var brand=(System.Windows.Controls.StackPanel)VisualTreeHelper.GetParent(mark);var origin=brand.TranslatePoint(new Point(),root);
            double edge=L.Direction==FlowDirection.RightToLeft?root.ActualWidth-origin.X-brand.ActualWidth:origin.X;
            return edge>=0&&edge<=32&&origin.Y>=0&&origin.Y<=24&&brand.Children.OfType<System.Windows.Controls.TextBlock>().Single().Text=="ovunet";
        }
    }
}
