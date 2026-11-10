using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed class AccountQuotaTile
    {
        internal readonly Border Root;
        internal readonly TextBlock Used,Remaining,Time,Note,Percent;
        readonly ProgressBar meter;
        readonly Grid numbers;
        internal AccountQuotaTile()
        {
            var body=new Grid();body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=GridLength.Auto});
            Used=new TextBlock{FontSize=14,Height=20,LineHeight=20,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,FontWeight=FontWeights.SemiBold,Foreground=Tone("Ink"),TextTrimming=TextTrimming.CharacterEllipsis};body.Children.Add(Used);
            numbers=new Grid{Margin=new(0,1,0,0),Height=18};numbers.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});numbers.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var bar=new Grid{Margin=new(0,2,12,0)};bar.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});bar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            meter=new ProgressBar{Minimum=0,Maximum=100,Height=8,Background=Tone("Outline"),Foreground=Green,VerticalAlignment=VerticalAlignment.Center,Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ProgressBar'><Grid x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='{TemplateBinding Background}' CornerRadius='4' Opacity='.4'/><Border x:Name='PART_Indicator' HorizontalAlignment='Left' Background='{TemplateBinding Foreground}' CornerRadius='4'/></Grid></ControlTemplate>")};bar.Children.Add(meter);
            Percent=new TextBlock{FontSize=13,Foreground=Muted,Margin=new(8,0,0,0),FlowDirection=FlowDirection.LeftToRight};Grid.SetColumn(Percent,1);bar.Children.Add(Percent);numbers.Children.Add(bar);
            var detail=new StackPanel{Orientation=Orientation.Horizontal};Remaining=new TextBlock{FontSize=14,Foreground=Muted,Margin=new(0,0,12,0)};Time=new TextBlock{FontSize=14,Foreground=Muted};detail.Children.Add(Remaining);detail.Children.Add(Time);Grid.SetColumn(detail,1);numbers.Children.Add(detail);Grid.SetRow(numbers,1);body.Children.Add(numbers);
            Note=new TextBlock{FontSize=12,Height=16,LineHeight=16,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,Foreground=Muted,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetRow(Note,2);body.Children.Add(Note);
            Root=new Border{Child=body,Padding=new(12,4,12,4),Height=68,Margin=new(16,0,16,6),CornerRadius=new(14),Background=GlassBrush(),BorderBrush=Tone("Outline"),BorderThickness=new(1),ToolTip=L.Text("QUOTA_UNIT_HINT"),Tag="ACCOUNT_QUOTA"};
            static Brush GlassBrush()=>new SolidColorBrush(Color.FromArgb(25,255,255,255));
        }
        internal void Update(StatusView? view)
        {
            var data=AccountUsage.From(view);string Number(long value)=>AccountUsage.Gigabytes(value).ToString("0.##",CultureInfo.CurrentCulture);
            string TimeText()=>data.Expired?L.Text("ACCOUNT_EXPIRED"):view?.Status is {ExpiryKnown:false}?L.Text("UNKNOWN"):data.TimeLeft is {} left?left.TotalDays>=1?string.Format(L.Text("TIME_DAYS_LEFT"),(int)Math.Floor(left.TotalDays)):left.TotalHours>=1?string.Format(L.Text("TIME_HOURS_LEFT"),(int)Math.Floor(left.TotalHours)):string.Format(L.Text("TIME_MINUTES_LEFT"),Math.Max(0,(int)Math.Ceiling(left.TotalMinutes))):view?.Status?.ExpiresAt==null?L.Text("UNLIMITED_TIME"):L.Text("ACCOUNT_STALE");
            Time.Text=TimeText();Time.Foreground=data.Expired?Red:Muted;
            numbers.Visibility=data.HasQuota||data.Unlimited||view?.Status?.ExpiresAt!=null?Visibility.Visible:Visibility.Collapsed;
            if(data.HasQuota){Used.Text=string.Format(L.Text("QUOTA_USAGE"),Number(data.Used!.Value),Number(data.Limit!.Value));Remaining.Text=string.Format(L.Text("QUOTA_LEFT"),Number(data.Remaining!.Value));meter.Value=data.UsedPercent!.Value;Percent.Text=data.UsedPercent.Value.ToString("0.#",CultureInfo.CurrentCulture)+"%";meter.Visibility=Percent.Visibility=Visibility.Visible;}
            else if(data.Unlimited){Used.Text=L.Text("UNLIMITED_TRAFFIC");Remaining.Text="";meter.Visibility=Percent.Visibility=Visibility.Collapsed;}
            else{Used.Text=L.Text(view?.Status?.Metering is "partial" or "unavailable"?"ACCOUNT_INCOMPLETE":"ACCOUNT_UNKNOWN");Remaining.Text="";}
            Note.Visibility=data.Current?Visibility.Collapsed:Visibility.Visible;Note.Text=view?.Status==null?"":L.Text("QUOTA_CACHED");numbers.Opacity=data.Current?1:.65;
            // Cached values remain visibly labelled; never present incomplete data as unlimited.
            Note.Margin=new(0,2,0,0);Root.Height=view?.Status==null?24:data.Current?46:64;Used.Height=view?.Status==null?18:20;Used.LineHeight=view?.Status==null?18:20;Root.Padding=view?.Status==null?new(8,2,8,2):new(12,3,12,3);Used.FontSize=view?.Status==null?13:14;
            Used.Foreground=data.Current?Tone("Ink"):Muted;meter.Foreground=!data.Current?Muted:view?.Severity switch{Severity.Critical=>Red,Severity.Warning=>Amber,_=>Green};
            if(view?.Status?.State=="limit_reached")Time.Text=L.Text("ACCOUNT_LIMIT");
        }
    }
    void UpdateAccountQuota(GroupView group)
    {
        if(group.Quota==null)return;string id=group.Members[0].SourceId;
        bool publicFeed=PublicFeed.IsKnown(data.Subscriptions.FirstOrDefault(s=>s.Id==id)?.Url??"");
        group.Quota.Root.Visibility=id.Length==0||publicFeed&&!statuses.ContainsKey(id)?Visibility.Collapsed:Visibility.Visible;
        statuses.TryGetValue(id,out var snapshot);group.Quota.Update(snapshot?.Evaluate());
    }
}
