using System.Text.Json;
using Rovunet.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunAboutSupport26Smoke(string report)
    {
        string decoded="",error="";var checks=new Dictionary<string,bool>();string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        string Decode(BitmapSource original){var image=new FormatConvertedBitmap(original,PixelFormats.Bgra32,null,0);int stride=image.PixelWidth*4;var bytes=new byte[stride*image.PixelHeight];image.CopyPixels(bytes,stride,0);return QrCodeReader.Read(bytes,image.PixelWidth,image.PixelHeight);}
        try{
            decoded=Decode(SupportQr26.Value);checks["supplied_qr_matches_exact_owner_address"]=decoded=="TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ"&&decoded==SupportWallet26;
            SaveSupportQr26(SupportQr26.Value,Path.Combine(folder,"Rovunet-USDT-TRC20.png"));
            checks["exported_png_decodes_to_same_address"]=Decode(BitmapFrame.Create(new Uri(Path.Combine(folder,"Rovunet-USDT-TRC20.png"))))==SupportWallet26;
            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();Build();
                string copied="",opened="";bool exported=false,updatesRequested=false;
                var page=BuildAboutSupport26(value=>copied=value,value=>opened=value,image=>exported=ReferenceEquals(image,SupportQr26.Value),()=>updatesRequested=true);page.Show();await Idle();string id=language+"_"+theme;
                var address=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"about:wallet-address"));
                checks[id+"_address_exact_ltr_readonly"]=address.Text==SupportWallet26&&address.FlowDirection==FlowDirection.LeftToRight&&address.IsReadOnly&&!address.Text.Any(char.IsControl);
                checks[id+"_network_visible"]=Visuals<TextBlock>(page.PageVisual).Any(t=>Equals(t.Tag,"about:network")&&t.Text.Contains("TRC20")&&t.Text.Contains("USDT"));
                checks[id+"_optional_support_localized"]=Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text==L.Text("ABOUT_SUPPORT_BODY"))&&L.Text("ABOUT_SUPPORT_BODY")!="ABOUT_SUPPORT_BODY";
                string channelTitle=language switch{"fa"=>"کانال رسمی","en"=>"Official channel","ru"=>"Официальный канал",_=>"官方频道"};
                checks[id+"_channel_not_group"]=Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text==channelTitle);
                checks[id+"_support_text_centered"]=Visuals<TextBlock>(page.PageVisual).Where(t=>t.Text==L.Text("ABOUT_SUPPORT_BODY")||t.Text==L.Text("ABOUT_SUPPORT_NETWORK")||t.Text==channelTitle).All(t=>t.TextAlignment==TextAlignment.Center);
                checks[id+"_address_and_link_centered"]=address.TextAlignment==TextAlignment.Center&&Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"about:community-url")).TextAlignment==TextAlignment.Center;
                var credits=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"about:credits"));
                checks[id+"_small_english_credits_at_bottom"]=credits.Text.Contains("SlipNet")&&credits.Text.Contains("Licences")&&credits.Text.All(c=>c<=127)&&credits.FontSize==11&&credits.FlowDirection==FlowDirection.LeftToRight&&credits.TextAlignment==TextAlignment.Center&&credits.Parent is StackPanel footerPanel&&ReferenceEquals(footerPanel.Children[footerPanel.Children.Count-1],credits);
                checks[id+"_translated_about_body_removed"]=!Visuals<TextBlock>(page.PageVisual).Any(t=>t.Text==L.Text("ABOUT_BODY"));
                void Click(string tag)=>Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,tag)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Click("about:copy-wallet");checks[id+"_copy_is_exact_wallet"]=copied==SupportWallet26;
                Click("about:copy-channel");checks[id+"_copy_is_exact_channel_url"]=copied=="https://t.me/RovunetVPN";
                Click("about:open-channel");checks[id+"_opens_only_owner_channel"]=opened==OfficialChannel26;
                Click("about:updates");checks[id+"_in_app_updates_requested_without_browser"]=updatesRequested&&opened==OfficialChannel26;
                var updateButton=Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"about:updates"));
                checks[id+"_update_link_centered_above_credits"]=updateButton.HorizontalAlignment==HorizontalAlignment.Center&&updateButton.Parent==credits.Parent&&credits.Parent is StackPanel updatePanel&&updatePanel.Children.IndexOf(updateButton)==updatePanel.Children.IndexOf(credits)-1;
                Click("about:save-qr");checks[id+"_exports_current_supplied_qr"]=exported;
                var qr=Visuals<Image>(page.PageVisual).Single(t=>Equals(t.Tag,"about:wallet-qr"));
                checks[id+"_qr_square_white_frame"]=qr.ActualWidth==qr.ActualHeight&&((Border)qr.Parent).Background==Brushes.White;
                checks[id+"_vertical_touch_scroll"]=Visuals<ScrollViewer>(page.PageVisual).Any(s=>s.PanningMode==PanningMode.VerticalOnly);
                checks[id+"_same_app_window"]=NavigationDepth==1&&Window.GetWindow(page.PageVisual)==this;
                if(language is "fa" or "en"){
                    SavePage((FrameworkElement)Content,Path.Combine(folder,"about-overview-"+id+"-compact.png"));
                    Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"about:save-qr")).BringIntoView();await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"about-support-"+id+"-compact.png"));
                    credits.BringIntoView();await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"about-footer-"+id+"-compact.png"));
                    Width=1180;await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"about-support-"+id+"-wide.png"));
                    checks[id+"_resize_preserves_exact_data"]=address.Text==SupportWallet26;
                }
                page.Close();
            }
        }catch(Exception ex){checks["exception_"+ex.GetType().Name]=false;error=ex is SafeError safe?safe.Code:ex.GetType().Name;}
        File.WriteAllText(report,JsonSerializer.Serialize(new{passed=checks.Values.All(x=>x),checks,decodedQrPayload=decoded,error,syntheticFixture=true,actualClipboardTouched=false,externalLinksOpened=false,transactionCreated=false},new JsonSerializerOptions{WriteIndented=true}));
        return checks.Values.All(x=>x);
    }
}
