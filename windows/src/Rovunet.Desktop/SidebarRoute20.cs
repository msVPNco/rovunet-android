using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly Dictionary<string,Button> sidebarRows20=[];
    string sidebarRoute20="HOME";
    void RefreshSidebar20(){foreach(var (key,button) in sidebarRows20)button.Background=key==sidebarRoute20?GlassSurface("Selected"):Brushes.Transparent;}
}
