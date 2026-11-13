using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunAboutLanguage27Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string error="";string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        try{
            foreach(string theme in new[]{"light","dark","amoled"})foreach(int width in new[]{480,1180}){
                data.Language="en";L.Language="en";data.Theme=theme;Width=width;Height=880;ApplyTheme();Build();
                string copied="";var page=BuildAboutSupport26(value=>copied=value,_=>{},_=>{});page.Show();await Idle();
                var originalRoot=page.PageVisual;string originalCredits=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"about:credits")).Text;
                foreach(string language in new[]{"fa","ru","zh-CN","en"}){
                    Visuals<Button>((FrameworkElement)Content).Single(b=>Equals(b.Tag,"language:"+language)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();
                    string id=theme+"_"+width+"_"+language;
                    var expected=language switch{
                        "fa"=>new[]{"دربارهٔ Rovunet","کانال رسمی","حمایت اختیاری","رووُنت رایگان است و حمایت شما به ادامهٔ توسعه کمک می‌کند.","آدرس کیف پول حمایت","بازکردن کانال تلگرام","کپی لینک کانال","کپی آدرس کیف پول","ذخیرهٔ کد QR","دریافت به‌روزرسانی از گیت‌هاب","کپی اطلاعات نسخه","نمایش مجوزها"},
                        "ru"=>new[]{"О Rovunet","Официальный канал","Добровольная поддержка","Rovunet бесплатен; добровольные пожертвования помогают развитию.","Адрес кошелька для поддержки","Открыть канал Telegram","Копировать ссылку на канал","Копировать адрес кошелька","Сохранить QR-код","Обновления на GitHub","Копировать сведения о версии","Открыть лицензии"},
                        "zh-CN"=>new[]{"关于 Rovunet","官方频道","自愿支持","Rovunet 免费使用，自愿捐助可支持项目持续开发。","支持钱包地址","打开 Telegram 频道","复制频道链接","复制钱包地址","保存二维码","前往 GitHub 获取更新","复制版本信息","打开许可证"},
                        _=>new[]{"About Rovunet","Official channel","Optional support","Rovunet is free; voluntary donations help further development.","Support wallet address","Open Telegram channel","Copy channel link","Copy wallet address","Save QR code","Get updates on GitHub","Copy version information","Open licences"}
                    };
                    var text=Visuals<TextBlock>(page.PageVisual).Select(t=>t.Text).ToArray();var buttons=Visuals<Button>(page.PageVisual).Select(b=>b.Content?.ToString()).ToArray();
                    checks[id+"_flag_changed_selected_language"]=L.Language==language&&data.Language==language;
                    checks[id+"_page_title_and_channel_follow_language"]=text.Contains(expected[0])&&text.Contains(expected[1]);
                    checks[id+"_support_and_wallet_caption_follow_language"]=text.Any(t=>t.Contains(expected[2]))&&text.Contains(expected[3])&&text.Contains(expected[4]);
                    checks[id+"_all_actions_follow_language"]=expected.Skip(5).All(value=>buttons.Contains(value));
                    var credits=Visuals<TextBlock>(page.PageVisual).Single(t=>Equals(t.Tag,"about:credits"));
                    checks[id+"_only_licence_prose_stays_english"]=credits.Text==originalCredits&&credits.Text.Contains("SlipNet")&&credits.Text.All(c=>c<=127)&&credits.FontSize==11;
                    checks[id+"_same_open_page_and_navigation"]=ReferenceEquals(page.PageVisual,originalRoot)&&NavigationDepth==1&&Window.GetWindow(page.PageVisual)==this;
                    checks[id+"_reading_direction_follows_language"]=page.PageVisual.FlowDirection==(language=="fa"?FlowDirection.RightToLeft:FlowDirection.LeftToRight);
                    var wallet=Visuals<TextBox>(page.PageVisual).Single(t=>Equals(t.Tag,"about:wallet-address"));
                    Visuals<Button>(page.PageVisual).Single(b=>Equals(b.Tag,"about:copy-wallet")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    checks[id+"_wallet_and_copy_survive_language_change"]=wallet.Text=="TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ"&&copied==wallet.Text&&wallet.FlowDirection==FlowDirection.LeftToRight;
                    if(language=="fa"){
                        page.RestoreScroll(0);await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"about-fa-"+theme+"-"+width+"-top.png"));
                        credits.BringIntoView();await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"about-fa-"+theme+"-"+width+"-footer.png"));
                    }
                }
                page.Close();
            }
        }catch(Exception ex){checks["exception_"+ex.GetType().Name]=false;error=ex.GetType().Name;}
        bool passed=checks.Values.All(x=>x);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,error,usesActualLanguageFlagClick=true,syntheticFixture=true,actualClipboardTouched=false,externalLinksOpened=false},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
}
