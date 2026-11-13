using System.Windows;
using System.Windows.Controls;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    void ShowSubscriptionDetails21(string source)
    {
        var sub=data.Subscriptions.FirstOrDefault(s=>s.Id==source);if(sub==null)return;
        var page=BuildSubscriptionDetails28(sub);page.Show();
    }
    DialogWindow BuildSubscriptionDetails28(Rovunet.Core.Subscription sub)
    {
        var page=Dialog("SUBSCRIPTION_DETAILS",620,380);page.UsePageChrome();
        var panel=new StackPanel{Margin=new(20)};
        var name=Text(sub.Name,20);panel.Children.Add(MovingLine21(name,30,L.Direction));
        if(ManagedSource28(sub) is {} preset){panel.Children.Add(Text(preset.DisplayName,18));panel.Children.Add(Text(L.Text("PUBLIC_SOURCE_MANAGED"),14,Muted));}
        else if(PublicFeed.IsRemoved(sub.Url))panel.Children.Add(Text(L.Text("PUBLIC_SOURCE_REMOVED"),14,Red));
        else{
            var url=new TextBox{Text=sub.Url,IsReadOnly=true,Tag="SAVED_SUBSCRIPTION_URL",FlowDirection=FlowDirection.LeftToRight,TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=54};
            panel.Children.Add(OutlinedField("SUBSCRIPTION_URL",url));panel.Children.Add(Button("COPY",()=>{Clipboard.SetText(sub.Url);Notify(L.Text("COPIED"));},true));
        }
        page.Content=panel;return page;
    }
}
