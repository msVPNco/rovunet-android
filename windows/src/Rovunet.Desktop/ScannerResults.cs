using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    ListBox ScanResultsList(ObservableCollection<ScanEntry> entries)
    {
        var list=new ListBox{ItemsSource=entries,SelectionMode=SelectionMode.Multiple,Height=300,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=System.Windows.Media.Brushes.Transparent,BorderThickness=new(0)};
        list.ItemTemplate=(DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid Margin="12,8" MinHeight="88" ToolTip="{Binding Display}">
                <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="52"/></Grid.ColumnDefinitions>
                <CheckBox Grid.Column="1" Width="44" Height="44" Padding="0" Margin="0" VerticalAlignment="Center" IsEnabled="{Binding Selectable}" AutomationProperties.Name="{Binding Address}" IsChecked="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem},Path=IsSelected,Mode=TwoWay}">
                  <CheckBox.Template><ControlTemplate TargetType="CheckBox"><Grid Width="44" Height="44" Background="Transparent" FlowDirection="LeftToRight">
                    <Border Name="Square" Width="26" Height="26" CornerRadius="5" BorderThickness="1.5" BorderBrush="{DynamicResource Muted}"/>
                    <Path Name="Tick" Data="M 0,7 L 5,12 L 15,1" Stroke="{DynamicResource Bg}" StrokeThickness="2.5" Width="16" Height="13" Stretch="Uniform" Visibility="Collapsed"/>
                    <Border Name="Focus" CornerRadius="8" BorderThickness="1" BorderBrush="{DynamicResource Accent}" Visibility="Collapsed"/>
                  </Grid><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Square" Property="Background" Value="{DynamicResource Accent}"/><Setter TargetName="Square" Property="BorderBrush" Value="{DynamicResource Accent}"/><Setter TargetName="Tick" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Focus" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger></ControlTemplate.Triggers></ControlTemplate></CheckBox.Template>
                </CheckBox>
                <StackPanel Margin="4,2,10,2">
                  <TextBlock Text="{Binding Address}" FontFamily="Consolas" FontSize="22" FontWeight="SemiBold" FlowDirection="LeftToRight" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis"/>
                  <WrapPanel FlowDirection="LeftToRight" Margin="0,4,0,2">
                    <Border Background="{DynamicResource Selected}" CornerRadius="5" Padding="8,1" Margin="0,0,8,0"><TextBlock Text="{Binding TransportLabel}" Foreground="{DynamicResource Accent}" FontSize="14" FontWeight="SemiBold"/></Border>
                    <TextBlock Text="{Binding DnsPing}" Foreground="{Binding DnsBrush}" FontFamily="Consolas" FontSize="15" FontWeight="SemiBold" Margin="0,0,10,0"/>
                    <TextBlock Text="{Binding ChecksLabel}" Foreground="{DynamicResource Muted}" FontFamily="Consolas" FontSize="14"/>
                  </WrapPanel>
                  <TextBlock Text="{Binding Description}" Foreground="{Binding ResultBrush}" FontSize="14" FontWeight="Medium" TextWrapping="Wrap" MaxHeight="40" TextTrimming="CharacterEllipsis" Margin="0,2,0,0"/>
                </StackPanel>
              </Grid>
            </DataTemplate>
            """);
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(2)));
        var menu=new ContextMenu();ScanEntry? Row()=>((FrameworkElement?)menu.PlacementTarget)?.DataContext as ScanEntry;
        var copy=new MenuItem{Header=L.Text("COPY_IP")};copy.Click+=(_,_)=>{if(Row() is {} entry)Clipboard.SetText(entry.Address);};menu.Items.Add(copy);
        var detail=new MenuItem{Header=L.Text("SCAN_DETAILS")};detail.Click+=(_,_)=>{if(Row() is not {} entry)return;var page=Dialog("SCAN_DETAILS");page.UsePageChrome();var body=new StackPanel{Margin=new(18)};body.Children.Add(Technical(Text(entry.Address,20)));foreach(var proof in entry.Proofs){body.Children.Add(Text(proof.Transport.ToUpperInvariant(),16,Accent));body.Children.Add(Text(proof.Dns||proof.Pass?L.Text("DNS_REPLIED"):L.Text(proof.Error.Length>0?proof.Error:"DNS_FAILED"),14,proof.Pass?Green:Red));var passed=(proof.Checks??[]).ToHashSet();var completed=(proof.CompletedChecks??[]).ToHashSet();string[] order=["NS","TXT","A","LONG-TXT","NXDOMAIN","EDNS"];foreach(string check in order)body.Children.Add(Text(ScannerProbeLabel(check)+"  "+(passed.Contains(check)?"✓":completed.Contains(check)?"✗":"—"),14,passed.Contains(check)?Green:completed.Contains(check)?Red:Muted));}body.Children.Add(Text(entry.Detail,14,entry.ResultBrush));page.Content=body;page.ShowDialog();};menu.Items.Add(detail);style.Setters.Add(new Setter(ContextMenuProperty,menu));
        Point? touchOrigin=null;list.PreviewTouchDown+=(_,e)=>touchOrigin=e.GetTouchPoint(list).Position;list.PreviewTouchUp+=(_,e)=>{if(touchOrigin is not {} start)return;touchOrigin=null;var end=e.GetTouchPoint(list).Position;double dx=end.X-start.X;if(Math.Abs(end.Y-start.Y)<40&&(L.Direction==FlowDirection.RightToLeft?dx>80:dx< -80)){for(DependencyObject? node=e.OriginalSource as DependencyObject;node!=null;node=System.Windows.Media.VisualTreeHelper.GetParent(node))if(node is ListBoxItem{DataContext:ScanEntry entry}){Clipboard.SetText(entry.Address);e.Handled=true;Notify(L.Text("COPIED"));break;}}};
        style.Setters.Add(new Setter(Control.TemplateProperty,(ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListBoxItem">
              <Border Name="Tile" CornerRadius="18" Margin="0,4,2,4" Padding="3" Background="{DynamicResource Soft}" BorderBrush="{DynamicResource Outline}" BorderThickness="1"><ContentPresenter/></Border>
              <ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Tile" Property="Background" Value="{DynamicResource Selected}"/><Setter TargetName="Tile" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Tile" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate>
            """)));list.ItemContainerStyle=style;
        ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);return list;
    }
    static void SortScanResults(ICollectionView view,string order)
    {
        using(view.DeferRefresh()){
            view.SortDescriptions.Clear();
            if(order is "passed" or "latency" or "dns")view.SortDescriptions.Add(new(nameof(ScanEntry.Rank),ListSortDirection.Ascending));
            if(order=="score")view.SortDescriptions.Add(new(nameof(ScanEntry.Score),ListSortDirection.Descending));
            if(order=="prism")view.SortDescriptions.Add(new(nameof(ScanEntry.PrismScore),ListSortDirection.Descending));
            if(order=="latency")view.SortDescriptions.Add(new(nameof(ScanEntry.Latency),ListSortDirection.Ascending));
            if(order=="dns")view.SortDescriptions.Add(new(nameof(ScanEntry.DnsLatency),ListSortDirection.Ascending));
            if(order=="address")view.SortDescriptions.Add(new(nameof(ScanEntry.AddressSortKey),ListSortDirection.Ascending));
            view.SortDescriptions.Add(new(nameof(ScanEntry.ScanIndex),ListSortDirection.Ascending));
        }
    }
}
