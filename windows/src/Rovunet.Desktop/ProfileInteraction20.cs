using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void SelectProfile20(Profile profile)
    {
        if(busy||data.SelectedId==profile.Id)return;string old=data.SelectedId;data.SelectedId=profile.Id;
        try{Save();}catch{data.SelectedId=old;lastError="PRIVATE_STORE_FAILED";}
        RefreshCards();
    }
    void ActivateProfileCard20(Profile profile)
    {
        if(busy)return;
        if(data.SelectedId!=profile.Id){SelectProfile20(profile);return;}
        if(state.State=="connected"&&activeProfile?.Id==profile.Id)return;
        SwitchPath(profile);
    }
    void RouteProfileCard20(Border tile,RadioButton select,Profile profile)
    {
        // Let padding, timing and status text share the same gesture. Action buttons
        // retain their own route; a pan or scrollbar gesture never selects/connects.
        Point? down=null;double scrollStart=0;
        bool ActionTarget(DependencyObject? node){for(;node!=null&&node!=tile;node=node is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(node):node is FrameworkContentElement content?content.Parent:LogicalTreeHelper.GetParent(node))if(node is ButtonBase button&&button!=select||node is ScrollBar)return true;return false;}
        select.IsHitTestVisible=false;
        select.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){ActivateProfileCard20(profile);e.Handled=true;}};
        tile.MouseLeftButtonDown+=(_,e)=>{if(!ActionTarget(e.OriginalSource as DependencyObject)){down=e.GetPosition(tile);scrollStart=mainScroll.VerticalOffset;}};
        tile.MouseLeftButtonUp+=(_,e)=>{var start=down;down=null;if(start==null||ActionTarget(e.OriginalSource as DependencyObject))return;if((e.GetPosition(tile)-start.Value).Length>8||Math.Abs(mainScroll.VerticalOffset-scrollStart)>4)return;ActivateProfileCard20(profile);select.Focus();e.Handled=true;};
    }
    Profile? RandomConnectableProfile20()
    {
        var candidates=data.Profiles.Where(p=>p.CanConnect&&SupportPolicy.Allowed(p,data.IncludeExperimental)).ToArray();
        return candidates.Length==0?null:candidates[Random.Shared.Next(candidates.Length)];
    }
}
