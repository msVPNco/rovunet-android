using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    Button homeSearchAction30=null!;
    void BindSearchAction30(TextBox input,Button action,Action apply,string id)
    {
        AutomationProperties.SetAutomationId(action,id);
        void Refresh()
        {
            string mode=input.Visibility!=Visibility.Visible?"open":string.IsNullOrWhiteSpace(input.Text)?"close":"apply";
            string key=mode=="close"?"CLOSE":"SEARCH";
            action.ToolTip=L.Text(key);AutomationProperties.SetName(action,L.Text(key));AutomationProperties.SetItemStatus(action,mode);
            if(action.Content is System.Windows.Shapes.Path icon)icon.Data=Geometry.Parse(mode switch{
                "close"=>"M5,5 L19,19 M19,5 L5,19",
                "apply"=>"M20,4 V14 H4 M10,8 L4,14 L10,20",
                _=>MenuIcon("SEARCH")});
        }
        void Activate()
        {
            if(input.Visibility!=Visibility.Visible){input.Visibility=Visibility.Visible;input.Focus();}
            else if(string.IsNullOrWhiteSpace(input.Text)){input.Clear();input.Visibility=Visibility.Collapsed;apply();action.Focus();}
            else{apply();input.Focus();input.CaretIndex=input.Text.Length;}
            Refresh();
        }
        action.Click+=(_,_)=>Activate();input.TextChanged+=(_,_)=>Refresh();input.IsVisibleChanged+=(_,_)=>Refresh();
        input.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){Activate();e.Handled=true;}else if(e.Key==Key.Escape){input.Clear();input.Visibility=Visibility.Collapsed;apply();action.Focus();Refresh();e.Handled=true;}};
        Refresh();
    }
}
