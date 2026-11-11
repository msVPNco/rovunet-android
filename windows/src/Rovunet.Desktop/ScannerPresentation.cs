using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Markup;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed class ScannerPresentation
    {
        internal readonly Grid Root=new(){Margin=new(16,4,16,8)};
        internal readonly TextBlock Total,Scanned,Working,TunnelStatus,Running;
        internal readonly ProgressBar DnsProgress=new(){Height=5,Minimum=0,Maximum=1,Margin=new(2,8,2,8),Tag="SCAN_DNS_PROGRESS"};
        internal readonly ProgressBar TunnelProgress=new(){Height=5,Minimum=0,Maximum=1,Margin=new(2,6,2,8),Tag="SCAN_TUNNEL_PROGRESS"};
        internal readonly StackPanel TunnelPanel=new();
        internal readonly TextBlock BankScope=new(){Margin=new(4,4,4,6),FontSize=13,TextWrapping=TextWrapping.Wrap,Tag="SCAN_BANK_SCOPE"};
        internal readonly TextBlock StageHint=new(){Margin=new(4,2,4,6),FontSize=13,TextWrapping=TextWrapping.Wrap,Tag="SCAN_STAGE_HINT"};
        readonly TextBlock percent;
        readonly UniformGrid runningSlots=new(){Columns=2};
        internal ScannerPresentation(ListBox list,Button stop,Button resume,Button restart,Button stopTunnel,Button testTunnel,Button resumeTunnel,Button retest,ComboBox sort,Button probes,Button workingToggle,ComboBox transport,ComboBox status,TextBox filter,FrameworkElement selectionTools,TextBlock feedback)
        {
            var progressTemplate=(ControlTemplate)XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ProgressBar"><Grid x:Name="PART_Track" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Border Background="{TemplateBinding Background}" CornerRadius="3" Opacity="0.35"/><Border x:Name="PART_Indicator" Background="{TemplateBinding Foreground}" CornerRadius="3" HorizontalAlignment="Left"/></Grid></ControlTemplate>
                """);
            foreach(var indicator in new[]{DnsProgress,TunnelProgress}){indicator.Template=progressTemplate;indicator.Background=Tone("Outline");indicator.Foreground=indicator==DnsProgress?Tone("ScanCyan"):Green;}
            for(int i=0;i<4;i++)Root.RowDefinitions.Add(new(){Height=i==2?new(1,GridUnitType.Star):GridLength.Auto});
            var header=new StackPanel();var summary=new DockPanel();var controls=new StackPanel{Orientation=Orientation.Horizontal};controls.Children.Add(stop);controls.Children.Add(resume);controls.Children.Add(restart);foreach(var b in new[]{stop,resume,restart}){b.MinWidth=88;b.MinHeight=52;b.Margin=new(3);b.FontSize=13;b.Padding=new(8,6,8,6);}DockPanel.SetDock(controls,Dock.Right);summary.Children.Add(controls);
            var values=new UniformGrid{Columns=3};
            TextBlock Stat(string key,Brush color){var value=new TextBlock{Text="0",FontSize=25,FontWeight=FontWeights.SemiBold,Foreground=color,FlowDirection=FlowDirection.LeftToRight,Tag=key};var body=new StackPanel{Margin=new(4,3,4,0)};body.Children.Add(value);body.Children.Add(new TextBlock{Text=L.Text(key),Foreground=Muted,FontSize=13});values.Children.Add(body);return value;}
            Total=Stat("SCAN_TOTAL",Accent);Scanned=Stat("SCAN_SCANNED",Tone("ScanCyan"));Working=Stat("SCAN_WORKING",Green);summary.Children.Add(values);header.Children.Add(summary);BankScope.Foreground=Muted;header.Children.Add(BankScope);
            var dnsBar=new DockPanel();percent=new TextBlock{Text="0%",Foreground=Accent,FontSize=13,Margin=new(10,3,3,0),FlowDirection=FlowDirection.LeftToRight};DockPanel.SetDock(percent,Dock.Right);dnsBar.Children.Add(percent);dnsBar.Children.Add(DnsProgress);header.Children.Add(dnsBar);
            foreach(var b in new[]{testTunnel,stopTunnel,resumeTunnel,retest}){b.HorizontalAlignment=HorizontalAlignment.Stretch;b.MinHeight=50;b.Margin=new(2,4,2,6);if(b==testTunnel||b==stopTunnel)header.Children.Add(b);}var tunnelActions=new UniformGrid{Columns=2};tunnelActions.Children.Add(resumeTunnel);tunnelActions.Children.Add(retest);header.Children.Add(tunnelActions);
            TunnelStatus=new TextBlock{FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=Tone("ScanCyan"),Margin=new(4,8,4,6),Tag="SCAN_TUNNEL_STATUS"};TunnelPanel.Children.Add(TunnelStatus);
            Running=new TextBlock{Visibility=Visibility.Collapsed,Tag="SCAN_RUNNING"};TunnelPanel.Children.Add(Running);TunnelPanel.Children.Add(runningSlots);TunnelPanel.Children.Add(TunnelProgress);header.Children.Add(TunnelPanel);Root.Children.Add(header);
            var tools=new StackPanel();StageHint.Foreground=Muted;tools.Children.Add(StageHint);filter.Visibility=Visibility.Collapsed;filter.MinHeight=44;filter.Margin=new(2,4,2,4);filter.ToolTip=L.Text("SEARCH");tools.Children.Add(filter);feedback.Margin=new(4,2,4,4);feedback.FontSize=12;tools.Children.Add(feedback);Grid.SetRow(tools,1);Root.Children.Add(tools);
            list.Height=double.NaN;list.MinHeight=120;list.Tag="SCAN_RESULTS";list.SetValue(VirtualizingPanel.IsVirtualizingProperty,true);list.SetValue(VirtualizingPanel.VirtualizationModeProperty,VirtualizationMode.Recycling);ScrollViewer.SetCanContentScroll(list,true);Grid.SetRow(list,2);Root.Children.Add(list);
            var empty=new TextBlock{Text=L.Text("SCAN_NO_MATCHES"),Foreground=Muted,FontSize=14,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(24),IsHitTestVisible=false};void Empty()=>empty.Visibility=list.Items.Count==0?Visibility.Visible:Visibility.Collapsed;((System.Collections.Specialized.INotifyCollectionChanged)list.Items).CollectionChanged+=(_,_)=>Empty();Empty();Grid.SetRow(empty,2);Root.Children.Add(empty);
            var footer=new StackPanel{Margin=new(0,6,0,0)};var choices=new UniformGrid{Columns=3};foreach(var choice in new FrameworkElement[]{sort,probes,workingToggle}){choice.Margin=new(3,0,3,4);if(choice is Control control){control.MinHeight=48;control.FontSize=14;}choices.Children.Add(choice);}footer.Children.Add(choices);footer.Children.Add(selectionTools);
            transport.Visibility=status.Visibility=Visibility.Collapsed;footer.Children.Add(transport);footer.Children.Add(status);Grid.SetRow(footer,3);Root.Children.Add(footer);
        }
        internal void Refresh(int total,int scanned,int working,int queued,int completed,int verified,IEnumerable<string> running,bool full)
        {
            Total.Text=total.ToString("N0");Scanned.Text=scanned.ToString("N0");Working.Text=working.ToString("N0");DnsProgress.Value=total>0?(double)scanned/total:0;percent.Text=((int)(DnsProgress.Value*100))+"%";
            TunnelPanel.Visibility=full?Visibility.Visible:Visibility.Collapsed;TunnelStatus.Text=string.Format(L.Text("SCAN_TUNNEL_COUNTS"),completed,queued,verified);var active=running.Take(10).ToArray();Running.Text=string.Join(" · ",active);runningSlots.Children.Clear();foreach(var value in active)runningSlots.Children.Add(new TextBlock{Text=value,FontFamily=new("Consolas"),FontSize=12,Foreground=Muted,FlowDirection=FlowDirection.LeftToRight,Margin=new(4,1,8,2),TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=value});TunnelProgress.Value=queued>0?(double)completed/queued:0;
        }
    }
}
