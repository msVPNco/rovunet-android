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
        internal AccountQuotaTile(Func<bool>? motion = null)
        {
            bool Enabled()=>motion?.Invoke()??SystemParameters.ClientAreaAnimation;
            TextBlock Label(Brush color)=>new(){FontSize=12,LineHeight=18,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,Foreground=color,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.None};
            var body=new Grid();body.RowDefinitions.Add(new(){Height=new(18)});body.RowDefinitions.Add(new(){Height=new(18)});body.RowDefinitions.Add(new(){Height=new(5)});
            Used=Label(Tone("Ink"));Used.FontWeight=FontWeights.SemiBold;
            body.Children.Add(new MarqueeLine(Used,18,L.Direction,Enabled));
            numbers=new Grid{Height=18};var detail=new StackPanel{Orientation=Orientation.Horizontal,FlowDirection=L.Direction};
            Remaining=Label(Muted);Time=Label(Muted);Note=Label(Muted);Percent=Label(Muted);Percent.FlowDirection=FlowDirection.LeftToRight;
            foreach(var label in new[]{Remaining,Time,Note,Percent}){label.Margin=new(0,0,9,0);detail.Children.Add(label);}
            numbers.Children.Add(new MarqueeLine(detail,18,L.Direction,Enabled));Grid.SetRow(numbers,1);body.Children.Add(numbers);
            meter=new ProgressBar{Minimum=0,Maximum=100,Height=4,Background=Tone("Outline"),Foreground=Accent,VerticalAlignment=VerticalAlignment.Center,Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ProgressBar'><Grid x:Name='PART_Track' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='{TemplateBinding Background}' CornerRadius='3' Opacity='.18'/><Border x:Name='PART_Indicator' HorizontalAlignment='Left' Background='{TemplateBinding Foreground}' CornerRadius='3'/></Grid></ControlTemplate>")};Grid.SetRow(meter,2);body.Children.Add(meter);
            Root=new Border{Child=body,Padding=new(0),Height=44,Margin=new(0,2,0,1),Background=Brushes.Transparent,ToolTip=L.Text("QUOTA_UNIT_HINT"),Tag="ACCOUNT_QUOTA"};
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
            Root.Height=44;Used.Height=18;Used.LineHeight=18;Root.Padding=new(0);Used.FontSize=12;
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
