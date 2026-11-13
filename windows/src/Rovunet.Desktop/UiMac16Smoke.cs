using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    internal async Task<bool> RunMac16Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
        Rect In(FrameworkElement e,Visual ancestor)=>e.TransformToAncestor(ancestor).TransformBounds(new Rect(e.RenderSize));
        bool Separate(Rect a,Rect b)=>a.Right<=b.Left+.5||b.Right<=a.Left+.5;
        try {
            var profile=Importer.Single("socks5://127.0.0.1:19123#Mac16");
            profile.Name="Shadowsocks 2022 · Canada Secure · نام طولانی برای بررسی مرز متن";
            profile.CollectionName="Canada · مجموعهٔ آزمایشی دارای نام بلند برای بررسی محدوده";
            data.Profiles=[profile];data.Subscriptions=[];
            foreach(string language in new[]{"fa","en","ru","zh-CN"})
            foreach(string theme in new[]{"dark","light","amoled"})
            foreach(double width in new[]{480d,1040d}){
                L.Language=data.Language=language;data.Theme=theme;Width=width;Height=900;
                ApplyTheme();Build();activeProfile=profile;SetState(new(){State="connected",Verified=true});
                data.ExpandedCollections[groups.Single().Key]=true;RefreshCards();
                heroTitle.Text="متصل · دسترسی به اینترنت با HTTPS تأیید شد · Canada Secure";
                connectionText.Text=heroTitle.Text;detail.Text=profile.Name;
                var row=rows.Single();row.Timing.Visibility=Visibility.Visible;row.Timing.Text="HTTPS 123456 ms";
                row.Result.Visibility=Visibility.Visible;row.Result.Text="پیام آزمایشی طولانی؛ متن باید در خانهٔ خود بماند";
                Activate();await Idle18();string key=language+"_"+theme+"_"+width;
                var ping=Visuals<MarqueeLine>(row.Tile).Single(m=>Equals(m.Tag,"PROFILE_PING_MARQUEE"));
                var pingBounds=In(ping,row.Tile);var selectionBounds=In(row.Select,row.Tile);
                checks[key+"_name_and_ping_have_separate_columns"]=Separate(pingBounds,selectionBounds);
                checks[key+"_four_independent_profile_viewports"]=Visuals<MarqueeLine>(row.Tile).Count()==4;
                checks[key+"_all_profile_viewports_clip"]=Visuals<MarqueeLine>(row.Tile).All(m=>m.ClipToBounds&&m.ActualWidth>0);
                checks[key+"_fixed_79px_profile_height"]=Math.Abs(row.Tile.ActualHeight-79)<1;
                checks[key+"_title_and_footer_have_viewports"]=Visuals<MarqueeLine>((DependencyObject)Content).Any(m=>Equals(m.Tag,"HERO_TITLE_MARQUEE"))&&Visuals<MarqueeLine>((DependencyObject)Content).Any(m=>Equals(m.Tag,"FOOTER_TITLE_MARQUEE"));
                var hb=In(header21.Flags,(Visual)Content);var cb=new Rect(((FrameworkElement)Content).RenderSize);
                checks[key+"_flags_center_entire_frame"]=Math.Abs(hb.X+hb.Width/2-cb.Width/2)<1;
                checks[key+"_no_multiline_or_ellipsis"]=Visuals<TextBlock>(row.Tile).All(t=>t.TextWrapping==TextWrapping.NoWrap&&t.TextTrimming==TextTrimming.None);
                checks[key+"_three_profile_actions_touchable"]=Visuals<Button>(row.Tile).Count(b=>b.ActualWidth>=44&&b.ActualHeight>=44)==3;
                checks[key+"_title_never_reaches_power"]=Separate(In(heroRingScale19,heroCard19),In(Visuals<MarqueeLine>(heroCard19).Single(m=>Equals(m.Tag,"HERO_TITLE_MARQUEE")),heroCard19));
                if(language=="fa")SavePage((FrameworkElement)Content,Path.Combine(folder,"mac16-"+key+".png"));
            }
        }catch(Exception e){checks["exception_"+e.GetType().Name]=false;File.WriteAllText(report+".error",e.ToString());}
        bool passed=checks.Count>0&&checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,synthetic=true,realNetworkTested=false,ownerDataTouched=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
