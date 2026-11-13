using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    internal const string OfficialChannel26="https://t.me/RovunetVPN";
    internal const string WindowsUpdates26="https://github.com/msVPNco/rovunet-app/blob/main/windows/README.md";
    internal const string SupportWallet26="TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ";
    internal const string AboutCredits26="AGPL-3.0 | Based on Rovunet/SlipNet 2.5.6 contracts and logic by Anonvector; not affiliated.\nXray-core 26.10.31 | MPL-2.0\nLicences and corresponding sources are included with the installation and source bundle.";
    static readonly Lazy<BitmapSource> SupportQr26=new(()=>{
        var image=BitmapFrame.Create(new Uri("pack://application:,,,/Rovunet;component/Assets/Support/usdt-tron.png"),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        image.Freeze();return image;
    });
    static void SaveSupportQr26(BitmapSource image,string path)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);
    }
    // Callbacks let the fixture exercise actions without touching the owner's
    // clipboard, opening a browser, or presenting a save picker.
    DialogWindow BuildAboutSupport26(Action<string>? copy=null,Action<string>? open=null,Action<BitmapSource>? saveQr=null,Action? updatesAction=null)
    {
        var translations=new List<Action>();
        TextBlock Centered(string value,double size=14,Brush? brush=null){var text=Text(value,size,brush);text.TextAlignment=TextAlignment.Center;return text;}
        TextBlock LocalText(string key,double size=14,Brush? brush=null,string prefix=""){var text=Centered(prefix+L.Text(key),size,brush);translations.Add(()=>text.Text=prefix+L.Text(key));return text;}
        Button LocalAction(string key,Action action,bool primary=false){var button=Button(key,action,primary);translations.Add(()=>{button.Content=L.Text(key);AutomationProperties.SetName(button,L.Text(key));});return button;}
        var page=Dialog("ABOUT",620,740);var panel=new StackPanel{Margin=new(24)};
        panel.Children.Add(Technical(Centered("Rovunet "+Version.Split('+')[0],18)));
        string feedbackKey="";var feedback=Centered("",13,Green);feedback.Tag="about:feedback";
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};actions.Children.Add(LocalAction("COPY_VERSION",()=>Copy("Rovunet "+Version+"\nWindows x64 · system/local proxy · unsigned preview")));
        actions.Children.Add(LocalAction("LICENSES",()=>Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"licenses")){UseShellExecute=true})));panel.Children.Add(actions);
        void Copy(string value){if(copy!=null)copy(value);else Clipboard.SetText(value);feedback.Foreground=Green;feedbackKey="COPIED";feedback.Text=L.Text(feedbackKey);}
        void Open(string url){try{if(open!=null)open(url);else Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch{feedback.Foreground=Red;feedbackKey="ABOUT_LINK_FAILED";feedback.Text=L.Text(feedbackKey);}}
        var community=new StackPanel();community.Children.Add(LocalText("ABOUT_COMMUNITY",18));
        var channelLink=new TextBox{Text=OfficialChannel26,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight,TextAlignment=TextAlignment.Center,Tag="about:community-url",TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        community.Children.Add(channelLink);
        var communityActions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};
        var openChannel=LocalAction("ABOUT_OPEN_CHANNEL",()=>Open(OfficialChannel26),true);openChannel.Tag="about:open-channel";communityActions.Children.Add(openChannel);
        var copyChannel=LocalAction("ABOUT_COPY_CHANNEL",()=>Copy(OfficialChannel26));copyChannel.Tag="about:copy-channel";communityActions.Children.Add(copyChannel);
        community.Children.Add(communityActions);panel.Children.Add(Card(community));

        var support=new StackPanel();var heading=LocalText("ABOUT_SUPPORT",18,Green,"♥  ");support.Children.Add(heading);
        support.Children.Add(LocalText("ABOUT_SUPPORT_BODY",14));
        var network=LocalText("ABOUT_SUPPORT_NETWORK",15);network.FontWeight=FontWeights.SemiBold;network.FlowDirection=FlowDirection.LeftToRight;network.Tag="about:network";support.Children.Add(network);
        var image=new Image{Source=SupportQr26.Value,Width=210,Height=210,Stretch=Stretch.Uniform,Tag="about:wallet-qr",HorizontalAlignment=HorizontalAlignment.Center};
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);AutomationProperties.SetName(image,L.Text("ABOUT_WALLET_QR"));
        // The original image reaches its edges. Keep a real white quiet zone in
        // every theme rather than tinting/recolouring the supplied QR modules.
        support.Children.Add(new Border{Child=image,Padding=new(28),Margin=new(4,10,4,10),Background=Brushes.White,CornerRadius=new(12),HorizontalAlignment=HorizontalAlignment.Center,Tag="about:qr-frame"});
        var address=new TextBox{Text=SupportWallet26,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new("Consolas"),FontSize=14,Tag="about:wallet-address"};
        var walletField=OutlinedField("ABOUT_WALLET_ADDRESS",address);foreach(var label in Visuals<TextBlock>(walletField)){label.TextAlignment=TextAlignment.Center;label.HorizontalAlignment=HorizontalAlignment.Center;translations.Add(()=>{label.Text=L.Text("ABOUT_WALLET_ADDRESS");label.ToolTip=label.Text;});}support.Children.Add(walletField);
        var walletActions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};var copyWallet=LocalAction("ABOUT_COPY_ADDRESS",()=>Copy(SupportWallet26),true);copyWallet.Tag="about:copy-wallet";walletActions.Children.Add(copyWallet);
        var export=LocalAction("ABOUT_SAVE_QR",()=>{
            if(saveQr!=null){saveQr(SupportQr26.Value);return;}
            var picker=new SaveFileDialog{FileName="Rovunet-USDT-TRC20.png",Filter="PNG|*.png"};
            if(picker.ShowDialog(page.NativeOwner)==true)SaveSupportQr26(SupportQr26.Value,picker.FileName);
        });export.Tag="about:save-qr";walletActions.Children.Add(export);
        support.Children.Add(walletActions);panel.Children.Add(Card(support));panel.Children.Add(feedback);
        var updates=LocalAction("ABOUT_UPDATES",()=>{if(updatesAction!=null)updatesAction();else BuildAppUpdatePage28(checkOnOpen:true).Show();});updates.Tag="about:updates";updates.HorizontalAlignment=HorizontalAlignment.Center;updates.Margin=new(4,12,4,0);panel.Children.Add(updates);
        var credits=Centered(AboutCredits26,11,Muted);credits.FlowDirection=FlowDirection.LeftToRight;credits.FontFamily=new("Segoe UI");credits.Margin=new(8,16,8,8);credits.Tag="about:credits";panel.Children.Add(credits);
        page.RelocalizePage=()=>{page.SetPageTitle(L.Text("ABOUT"));foreach(var translate in translations)translate();AutomationProperties.SetName(address,L.Text("ABOUT_WALLET_ADDRESS"));AutomationProperties.SetName(image,L.Text("ABOUT_WALLET_QR"));feedback.Text=feedbackKey.Length==0?"":L.Text(feedbackKey);};
        page.Content=panel;page.EnableVerticalTouchScroll26();return page;
    }
}
