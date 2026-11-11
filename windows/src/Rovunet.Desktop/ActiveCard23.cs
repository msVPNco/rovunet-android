using System.Globalization;
using System.Windows;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    void PromoteActive23()
    {
        if(state.State!="connected"||activeProfile==null)return;
        var row=rows.FirstOrDefault(r=>r.Profile.Id==activeProfile.Id);var group=groups.FirstOrDefault(g=>g.Members.Any(p=>p.Id==activeProfile.Id));if(row==null||group==null)return;
        if(cards.Children.IndexOf(group.Card)>1){cards.Children.Remove(group.Card);cards.Children.Insert(1,group.Card);}
        if(group.Paths.Children.IndexOf(row.Tile)>0){group.Paths.Children.Remove(row.Tile);group.Paths.Children.Insert(0,row.Tile);}
        row.Timing.Visibility=Visibility.Visible;
        bool fresh=liveLatency.HasValue&&latencySampled.HasValue&&(DateTimeOffset.UtcNow-latencySampled.Value).TotalSeconds<60;
        row.Timing.Text=fresh?liveLatency!.Value.ToString(CultureInfo.InvariantCulture)+" ms":"—";
        row.Timing.Foreground=fresh?Green:Muted;row.Timing.ToolTip=latencySampled?.ToLocalTime().ToString("g");
    }
}
