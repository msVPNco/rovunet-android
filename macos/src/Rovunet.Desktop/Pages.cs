using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    StackPanel Page()=>new(){Margin=new(18,8,18,20),Spacing=10};
    TextBox Field(Panel body,string key,string value="",bool multi=false,bool secret=false)
    {
        var stack=new StackPanel{Spacing=4};stack.Children.Add(Label(L.Text(key),13,"Muted"));var input=new TextBox{Text=value,MinHeight=multi?110:48,MaxHeight=multi?240:double.PositiveInfinity,AcceptsReturn=multi,TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap,FlowDirection=FlowDirection.LeftToRight,PasswordChar=secret?'●':'\0',Foreground=Tone("Ink"),Background=Tone("Card"),BorderBrush=Tone("Outline"),Padding=new(12,10)};stack.Children.Add(input);body.Children.Add(stack);return input;
    }
    ComboBox Choice(Panel body,string key,string[] values,string selected)
    {
        var row=new StackPanel{Spacing=4};row.Children.Add(Label(L.Text(key),13,"Muted"));var input=new ComboBox{ItemsSource=values,SelectedItem=selected,MinHeight=44,HorizontalAlignment=HorizontalAlignment.Stretch};row.Children.Add(input);body.Children.Add(row);return input;
    }
    async Task Guard(Func<Task> task){try{await task();}catch(Exception e){Display(e);}}
    void ShowSearch()
    {
        var panel=Page();var search=Field(panel,"SEARCH");var found=new StackPanel{Spacing=8};panel.Children.Add(found);void Find(){found.Children.Clear();foreach(var p in data.Profiles.Where(p=>p.Name.Contains(search.Text??"",StringComparison.CurrentCultureIgnoreCase)||p.Protocol.Contains(search.Text??"",StringComparison.OrdinalIgnoreCase)))found.Children.Add(BuildPathCard(p));}search.TextChanged+=(_,_)=>Find();Find();Navigate("SEARCH",panel);search.Focus();
    }
    void ShowAddHub()
    {
        if(addAnchor==null||currentBody!=null)return;if(addFlyout?.IsOpen==true){addFlyout.Hide();return;}
        var panel=new StackPanel{MinWidth=290,Margin=new(6)};foreach(var (key,action) in new (string,Action)[]{("ADD_LINK_FILE",()=>ShowImport()),("ADD_SUBSCRIPTION",()=>ShowSubscription()),("ADD_MANUAL",ShowManual),("PUBLIC_SOURCES",ShowPublic)})panel.Children.Add(NavButton(key,()=>{addFlyout?.Hide();action();}));
        addFlyout=new Flyout{Content=Card(panel,20),Placement=PlacementMode.TopEdgeAlignedRight};addFlyout.ShowAt(addAnchor);
    }
    void ShowGeneral(){var p=Page();foreach(var (key,action) in new (string,Action)[]{("SETTINGS",ShowSettings),("CONNECTION_DETAILS",ShowConnectionDetails),("MANAGE_COLLECTIONS",ShowManagement),("BACKUP_EXPORT",ShowBackup),("DIAGNOSTICS",ShowDiagnostics),("EXIT",()=>_=Exit())})p.Children.Add(Card(NavButton(key,action)));Navigate("SECTION_GENERAL",p);}
    void ShowSettings()
    {
        var panel=Page();
        StackPanel Section(string key){panel.Children.Add(Label(L.Text(key),15,"Accent"));var body=new StackPanel{Spacing=8};panel.Children.Add(Card(body));return body;}
        var connection=Section("SECTION_CONNECTION");connection.Children.Add(ActionButton("LOCAL_PROXY_APPS",ShowAppProxy));var proxy=new CheckBox{Content=L.Text("SYSTEM_PROXY"),IsChecked=data.SystemProxy,MinHeight=44};connection.Children.Add(proxy);connection.Children.Add(Label(L.Text("MAC_SYSTEM_PROXY_HINT"),12,"Muted"));
        var tun=new CheckBox{Content=L.Text("MAC_TUN_ENABLE"),IsChecked=data.SystemTun,MinHeight=44,Name="SystemTunOption"};connection.Children.Add(tun);connection.Children.Add(Label(L.Text("MAC_TUN_HINT"),12,"Muted"));connection.Children.Add(ActionButton("MAC_TUN_INSTALL",()=>_=Guard(InstallTunHelper)));proxy.IsEnabled=!data.SystemTun;
        tun.IsCheckedChanged+=(_,_)=>{try{RequireIdle();Commit(d=>d.SystemTun=tun.IsChecked==true);proxy.IsEnabled=!data.SystemTun;UpdateStatus();}catch(Exception e){tun.IsChecked=data.SystemTun;Display(e);}};
        proxy.IsCheckedChanged+=(_,_)=>{try{RequireIdle();Commit(d=>d.SystemProxy=proxy.IsChecked==true);UpdateStatus();}catch(Exception e){proxy.IsChecked=data.SystemProxy;Display(e);}};
        var http=Field(connection,"LOCAL_HTTP_PORT",data.LocalHttpPort.ToString());var socks=Field(connection,"LOCAL_SOCKS_PORT",data.LocalSocksPort.ToString());
        
        var close=new CheckBox{Content=L.Text("CLOSE_TO_TRAY"),IsChecked=data.CloseToTray};close.IsCheckedChanged+=(_,_)=>{try{Commit(d=>d.CloseToTray=close.IsChecked==true);}catch(Exception e){Display(e);}};connection.Children.Add(close);
        var sleep=Field(connection,"SLEEP_TIMER",data.SleepTimerMinutes.ToString());connection.Children.Add(Label(L.Text("SLEEP_TIMER_HINT"),12,"Muted"));
        var access=Section("KEYCHAIN_PERMISSION_TITLE");access.Children.Add(Label(L.Text("KEYCHAIN_PERMISSION_HINT"),12,"Muted"));access.Children.Add(ActionButton("MAC_OPEN_KEYCHAIN",()=>{if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");Process.Start(new ProcessStartInfo("/usr/bin/open"){UseShellExecute=false,ArgumentList={"-b","com.apple.keychainaccess"}});}));
        var dns=Section("DNS_SETTINGS");dns.Children.Add(ActionButton("FIND_DNS",()=>ShowScanner(Selected)));dns.Children.Add(ActionButton("GLOBAL_RESOLVER_TITLE",()=>ShowResolverPreferences(false)));dns.Children.Add(ActionButton("DNS_POOL_TITLE",()=>ShowResolverPreferences(true)));
        var probe=Section("SECTION_PROBE");var target=Field(probe,"LATENCY_URL",data.ProbeUrl);
        var live=new CheckBox{Content=L.Text("LIVE_LATENCY"),IsChecked=data.LiveLatency};live.IsCheckedChanged+=(_,_)=>{try{Commit(d=>d.LiveLatency=live.IsChecked==true);}catch(Exception e){Display(e);}};probe.Children.Add(live);
        var fallback=new CheckBox{Content=L.Text("PROBE_FALLBACKS"),IsChecked=data.ProbeFallbacks};fallback.IsCheckedChanged+=(_,_)=>{try{Commit(d=>d.ProbeFallbacks=fallback.IsChecked==true);}catch(Exception e){Display(e);}};probe.Children.Add(fallback);
        var appearance=Section("SECTION_APPEARANCE");var themes=new WrapPanel();foreach(var (value,key) in new[]{("system","SYSTEM_THEME"),("light","LIGHT_THEME"),("dark","DARK_THEME"),("amoled","AMOLED_THEME")})themes.Children.Add(ActionButton(key,()=>{Commit(d=>d.Theme=value);Back();ShowSettings();},data.Theme==value));appearance.Children.Add(themes);
        var languages=new WrapPanel();foreach(string code in new[]{"fa","en","ru","zh-CN"}){var b=ActionButton(code,()=>{Commit(d=>d.Language=code);Back();ShowSettings();});b.Content=code switch{"fa"=>"فارسی","en"=>"English","ru"=>"Русский",_=>"简体中文"};languages.Children.Add(b);}appearance.Children.Add(languages);
        var diagnostics=Section("SECTION_DIAGNOSTICS");diagnostics.Children.Add(ActionButton("DIAGNOSTICS",ShowDiagnostics));var recovery=Section("SECTION_RECOVERY");recovery.Children.Add(Label(L.Text("MAC_PROXY_RECOVERY_HINT"),13,"Muted"));recovery.Children.Add(ActionButton("RECOVER",()=>_=Guard(RecoverSystemProxy)));panel.Children.Add(ActionButton("SAVE",()=>{
            if(!int.TryParse(http.Text,out int hp)||hp is <0 or >65535||!int.TryParse(socks.Text,out int sp)||sp is <0 or >65535||hp!=0&&hp==sp)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
            if(!int.TryParse(sleep.Text,out int minutes)||!SleepTimerSession.ValidMinutes(minutes))throw new SafeError("INVALID_IMPORT");
            string uri=SubscriptionUrls.Validate(target.Text??"").AbsoluteUri;
            if(hp!=data.LocalHttpPort||sp!=data.LocalSocksPort)RequireIdle();
            Commit(d=>{d.LocalHttpPort=hp;d.LocalSocksPort=sp;d.SleepTimerMinutes=minutes;d.ProbeUrl=uri;});message=L.Text("SETTINGS_SAVED");UpdateStatus();
        },true));Navigate("SETTINGS",panel);
    }
    void ShowConnectionDetails()
    {
        var panel=Page();panel.Children.Add(Label(L.Text(state.TunActive?"MAC_TUN_ACTIVE":state.State=="connected"&&data.SystemProxy?"MAC_SYSTEM_PROXY_ACTIVE":"MAC_LOCAL_PROXY_ONLY"),14,"Muted"));
        foreach(var (label,value) in new[]{("HTTP",state.HttpPort>0?"127.0.0.1:"+state.HttpPort:"—"),("SOCKS5",state.SocksPort>0?"127.0.0.1:"+state.SocksPort:"—")}){panel.Children.Add(Label(label,12,"Muted",true));panel.Children.Add(Label(value,16,"Ink",true));panel.Children.Add(ActionButton("COPY",()=>_=Guard(async()=>{if(Clipboard!=null&&value!="—")await Clipboard.SetTextAsync(value); })));}
        panel.Children.Add(ActionButton("LOCAL_PROXY_APPS",ShowAppProxy));var active=data.Profiles.FirstOrDefault(p=>p.Id==activeId);panel.Children.Add(Label(L.Text("ACTIVE_PATH")+": "+(active?.Name??"—"),16));panel.Children.Add(Label(L.Text("PROTOCOL")+": "+(active?.Protocol??"—"),14,"Muted",true));panel.Children.Add(Label(L.Text(state.Verified?"HTTPS_VERIFIED":"PATH_READY_UNVERIFIED"),14));panel.Children.Add(ActionButton("VERIFY_HTTPS",()=>_=Guard(Probe),true));Navigate("CONNECTION_DETAILS",panel);
    }
    void ShowImport(string initial="")
    {
        var panel=Page();var raw=Field(panel,"IMPORT",initial,true);var password=Field(panel,"MAC_BUNDLE_PASSWORD","",secret:true);panel.Children.Add(ActionButton("PASTE",()=>_=Guard(async()=>{if(Clipboard!=null)raw.Text=await Clipboard.TryGetTextAsync()??"";})));panel.Children.Add(ActionButton("IMPORT_FILE",()=>_=OpenFile()));panel.Children.Add(ActionButton("IMPORT_QR",()=>_=ReadQr()));
        var preview=new StackPanel{Spacing=6};panel.Children.Add(preview);List<Profile>? batch=null;ImportReview? reviewed=null;var accept=new CheckBox{Content=L.Text("MAC_IMPORT_ACCEPT_PARTIAL"),IsVisible=false,MinHeight=44};panel.Children.Add(accept);
        raw.TextChanged+=(_,_)=>{batch=null;reviewed=null;accept.IsVisible=false;accept.IsChecked=false;preview.Children.Clear();};
        panel.Children.Add(ActionButton("REVIEW_IMPORT",()=>_=Guard(async()=>{
            batch=null;reviewed=null;accept.IsChecked=false;string input=raw.Text??"";if(SubscriptionInput.Address(input) is {} subscription){ShowSubscription(subscription);return;}if(MacBundleCodec.IsBundle(input)){var opened=await Task.Run(()=>MacBundleCodec.Open(input,password.Text??""));if(opened.Rejected>0)throw new SafeError("INVALID_IMPORT");batch=opened.Profiles;reviewed=opened;}else{reviewed=ImportReview.Read(input);batch=reviewed.Profiles;}
            RenderImportReview(preview,reviewed);accept.IsVisible=reviewed.Rejected>0&&batch.Count>0;
        }),true));
        panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();if(batch==null||batch.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");if(reviewed?.Rejected>0&&accept.IsChecked!=true)throw new SafeError("MAC_IMPORT_REVIEW_REQUIRED");var accepted=batch;Commit(d=>{foreach(var p in accepted){if(!d.Profiles.Any(old=>old.Raw==p.Raw))d.Profiles.Add(p);}});GoHome();},true));Navigate("IMPORT",panel);
    }
    async Task OpenFile()
    {
        await Guard(async()=>{var files=await StorageProvider.OpenFilePickerAsync(new(){Title=L.Text("IMPORT"),AllowMultiple=false,FileTypeFilter=[new("Rovunet"){Patterns=["*.txt","*.json","*.conf"]}]});if(files.Count==0)return;await using var stream=await files[0].OpenReadAsync();string text=await ReadText(stream);ShowImport(text);});
    }
    static async Task<string> ReadText(Stream input)
    {
        using var buffer=new MemoryStream();byte[] bytes=new byte[8192];int n;while((n=await input.ReadAsync(bytes))>0){if(buffer.Length+n>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");buffer.Write(bytes,0,n);}return new UTF8Encoding(false,true).GetString(buffer.ToArray());
    }
    async Task ReadQr()
    {
        await Guard(async()=>{
            var files=await StorageProvider.OpenFilePickerAsync(new(){Title=L.Text("IMPORT_QR"),AllowMultiple=false,FileTypeFilter=[FilePickerFileTypes.ImageAll]});if(files.Count==0)return;
            await using var stream=await files[0].OpenReadAsync();using var codec=SkiaSharp.SKCodec.Create(stream);if(codec==null||codec.Info.Width>4096||codec.Info.Height>4096)throw new SafeError("INVALID_IMPORT");
            using var bitmap=SkiaSharp.SKBitmap.Decode(codec,new SkiaSharp.SKImageInfo(codec.Info.Width,codec.Info.Height,SkiaSharp.SKColorType.Bgra8888));if(bitmap==null)throw new SafeError("INVALID_IMPORT");
            var source=new ZXing.RGBLuminanceSource(bitmap.Bytes,bitmap.Width,bitmap.Height,ZXing.RGBLuminanceSource.BitmapFormat.BGRA32);var result=new ZXing.BarcodeReaderGeneric{Options=new(){PossibleFormats=[ZXing.BarcodeFormat.QR_CODE],TryHarder=true}}.Decode(source);if(result==null)throw new SafeError("INVALID_IMPORT");ShowImport(result.Text);
        });
    }
    void ShowSubscription(string url="",string name="")
    {
        var panel=Page();var title=Field(panel,"COLLECTION_NAME",name);var address=Field(panel,"SUBSCRIPTION_URL",url);var preview=new StackPanel{Spacing=6};panel.Children.Add(preview);
        var accept=new CheckBox{Content=L.Text("MAC_IMPORT_ACCEPT_PARTIAL"),IsVisible=false,MinHeight=44};panel.Children.Add(accept);
        (Uri Uri,ImportReview Review,string Name,SubscriptionUsage20? Usage,string Provider)? draft=null;
        int revision=0;CancellationTokenSource? pending=null;
        void Invalidate(){revision++;pending?.Cancel();draft=null;accept.IsChecked=false;accept.IsVisible=false;preview.Children.Clear();}
        address.TextChanged+=(_,_)=>Invalidate();title.TextChanged+=(_,_)=>Invalidate();
        panel.Children.Add(ActionButton("REVIEW_IMPORT",()=>_=Guard(async()=>{
            if(busy)return;Invalidate();busy=true;
            int expectedRevision=revision;string enteredName=title.Text??"";string enteredUrl=address.Text??"";
            using var cancellation=new CancellationTokenSource();pending=cancellation;subscriptionPreviewStop=cancellation;
            try{
                var uri=SubscriptionUrls.Validate(enteredUrl);
                var payload=PublicFeed.IsKnown(uri.AbsoluteUri)?await PublicFeed.Fetch(uri.AbsoluteUri,cancellation.Token,state.HttpPort):await SubscriptionClient.Fetch(uri,state.HttpPort,cancellation:cancellation.Token);
                if(cancellation.IsCancellationRequested||expectedRevision!=revision||route!="ADD_SUBSCRIPTION")return;
                var review=ImportReview.Read(payload.Body);RenderImportReview(preview,review);if(review.Profiles.Count==0)return;
                var usage=SubscriptionUsage20.Parse(payload.UserInfo,payload.Body,payload.Date);
                string provider=SubscriptionInput.ProviderName(payload.Title);if(provider.Length==0)provider=ProviderTitle(payload.Body);
                _=SubscriptionMerge.Apply(data,uri,review.Profiles,enteredName,usage,provider);
                draft=(uri,review,enteredName,usage,provider);preview.Children.Add(Label(UsageText(usage),13,"Muted",true));accept.IsVisible=review.Rejected>0;
            }catch(SafeError error)when(cancellation.IsCancellationRequested&&error.Code=="CANCELLED"){}
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
            finally{if(ReferenceEquals(subscriptionPreviewStop,cancellation))subscriptionPreviewStop=null;pending=null;busy=false;}
        }),true));
        panel.Children.Add(ActionButton("SAVE",()=>{RequireIdle();if(draft is not {} reviewed)throw new SafeError("NO_SUPPORTED_IMPORT");if(reviewed.Review.Rejected>0&&accept.IsChecked!=true)throw new SafeError("MAC_IMPORT_REVIEW_REQUIRED");Install(SubscriptionMerge.Apply(data,reviewed.Uri,reviewed.Review.Profiles,reviewed.Name,reviewed.Usage,reviewed.Provider));GoHome();},true));Navigate("ADD_SUBSCRIPTION",panel);
    }
    static string ProviderTitle(string body)
    {
        foreach(string line in SubscriptionUsage20.DecodeBody(body).Split('\n'))if(SubscriptionUsage20.MetadataLine(line,out var key,out var value)&&key=="profile-title"){
            if(value.StartsWith("base64:",StringComparison.OrdinalIgnoreCase)){try{value=Importer.Decode(value[7..]);}catch{return "";}}
            return SubscriptionInput.ProviderName(value);
        }return "";
    }
    async Task UpdateSubscription(string source)
    {
        await Guard(async()=>{RequireIdle();if(busy)return;busy=true;BuildCards();try{var sub=data.Subscriptions.First(s=>s.Id==source);var uri=SubscriptionUrls.Validate(sub.Url);var payload=PublicFeed.IsKnown(sub.Url)?await PublicFeed.Fetch(sub.Url,proxyPort:state.HttpPort):await SubscriptionClient.Fetch(uri,state.HttpPort);var review=ImportReview.Read(payload.Body);RecordImportReview(review);if(review.Profiles.Count==0)throw new SafeError("NO_SUPPORTED_IMPORT");if(review.Rejected>0&&!await Confirm(ImportReviewSummary(review)+"\n\n"+L.Text("MAC_IMPORT_ACCEPT_PARTIAL")))return;var next=SubscriptionMerge.Apply(data,uri,review.Profiles,sub.Name,SubscriptionUsage20.Parse(payload.UserInfo,payload.Body,payload.Date),ProviderTitle(payload.Body));Install(next);message=L.Text("SUBSCRIPTION_UPDATED");}finally{busy=false;BuildCards();UpdateStatus();}});
    }
    void ShowPublic()
    {
        var p=Page();p.Children.Add(Label(L.Text("PUBLIC_CONSENT"),14,"Muted"));foreach(var preset in PublicFeed.Presets){var b=ActionButton("REVIEW_IMPORT",()=>ShowSubscription(preset.Url,preset.Name));b.Content=preset.Name+" · "+L.Text("REVIEW_IMPORT");p.Children.Add(Card(b));}Navigate("PUBLIC_SOURCES",p);
    }
    void ShowBackup()
    {
        var p=Page();p.Children.Add(ActionButton("EXPORT_ALL",()=>_=ExportProfiles(data.Profiles)));var password=Field(p,"PASSWORD","",secret:true);var confirm=Field(p,"CONFIRM_PASSWORD","",secret:true);p.Children.Add(ActionButton("EXPORT_ENCRYPTED",()=>_=Guard(async()=>{if(password.Text!=confirm.Text||string.IsNullOrEmpty(password.Text))throw new SafeError("BUNDLE_PASSWORD_ERROR");if(!await Confirm(L.Text("EXPORT_SECRET_CONFIRM")))return;string bundle=await Task.Run(()=>MacBundleCodec.Seal(data.Profiles,password.Text));await SaveText(bundle,"Rovunet-encrypted.txt");}),true));Navigate("BACKUP_EXPORT",p);
    }
    static void CheckShare(IEnumerable<Profile> profiles){if(profiles.Any(p=>ProfileEditing.IsNative(p.Raw)&&Importer.Fields(p.Raw)[34]!="1"))throw new SafeError("SHARING_DISABLED");}
    async Task ExportProfiles(IEnumerable<Profile> profiles)
    {
        await Guard(async()=>{var items=profiles.ToArray();CheckShare(items);if(!await Confirm(L.Text("EXPORT_SECRET_CONFIRM")))return;string json=JsonSerializer.Serialize(new{version=1,items=items.Select(p=>new{id=p.ItemId.Length>0?p.ItemId:p.Id,link=p.Raw})});await SaveText(json,"Rovunet-export.json");});
    }
    async Task SaveText(string text,string name)
    {
        var file=await StorageProvider.SaveFilePickerAsync(new(){Title=L.Text("SAVE"),SuggestedFileName=name});if(file==null)return;await using var stream=await file.OpenWriteAsync();if(stream.CanSeek)stream.SetLength(0);await using var writer=new StreamWriter(stream,new UTF8Encoding(false));await writer.WriteAsync(text);
    }
    void ShareQr(List<Profile> profiles)
    {
        _=Guard(async()=>{CheckShare(profiles);if(!await Confirm(L.Text("EXPORT_SECRET_CONFIRM")))return;string text=profiles.Count==1?profiles[0].Raw:JsonSerializer.Serialize(new{version=1,items=profiles.Select(p=>new{id=p.Id,link=p.Raw})});if(Encoding.UTF8.GetByteCount(text)>1800)throw new SafeError("QR_USE_FILE");var pixels=new ZXing.BarcodeWriterPixelData{Format=ZXing.BarcodeFormat.QR_CODE,Options=new ZXing.QrCode.QrCodeEncodingOptions{Width=360,Height=360,Margin=2,CharacterSet="UTF-8"}}.Write(text);var bitmap=new WriteableBitmap(new(pixels.Width,pixels.Height),new(96,96),Avalonia.Platform.PixelFormat.Bgra8888,Avalonia.Platform.AlphaFormat.Opaque);using(var locked=bitmap.Lock())Marshal.Copy(pixels.Pixels,0,locked.Address,pixels.Pixels.Length);var panel=Page();panel.Children.Add(new Image{Source=bitmap,Width=300,Height=300});panel.Children.Add(ActionButton("SAVE_PNG",()=>_=Guard(async()=>{var file=await StorageProvider.SaveFilePickerAsync(new(){SuggestedFileName="Rovunet-QR.png"});if(file==null)return;await using var stream=await file.OpenWriteAsync();if(stream.CanSeek)stream.SetLength(0);bitmap.Save(stream, PngBitmapEncoderOptions.Default);})));Navigate("SHARE_QR",panel);});
    }
    void RequireIdle(){if(busy)throw new SafeError("MAC_OPERATION_BUSY");if(state.State is "connected" or "connecting" or "engine-started")throw new SafeError("MAC_DISCONNECT_FIRST");}
    void ShowPending(string key){var p=Page();p.Children.Add(Label(L.Text("MAC_PENDING"),17));p.Children.Add(Label(L.Text("MAC_NOT_TESTED"),13,"Muted"));Navigate(key,p);}
    void ShowHelp(){var p=Page();p.Children.Add(Label(L.Text("SELECT_PATH"),16));p.Children.Add(Label(L.Text("MAC_LOCAL_PROXY_ONLY"),14));p.Children.Add(Label(L.Text("MAC_NOT_TESTED"),14,"Muted"));Navigate("HELP",p);}
    void ShowAbout(){var p=Page();p.Children.Add(Brand());p.Children.Add(Label(BuildVersion.Text+" · "+L.Text("MACOS_DEV"),16));p.Children.Add(Label(L.Text("MAC_UPDATE_CHANNEL"),14,"Muted"));p.Children.Add(Label(L.Text("MAC_BETA_TEST_SCOPE"),14,"Muted"));Navigate("ABOUT",p);}
    void ShowDiagnostics(){var p=Page();p.Children.Add(Label(L.Text("MAC_DIAGNOSTICS_HINT"),13,"Muted"));string Report()=>SafeDiagnostics.Json(RuntimeInformation.RuntimeIdentifier,state.State,state.Verified,data.SystemProxy&&!data.SystemTun,state.State=="connected"&&data.SystemProxy&&!data.SystemTun,data.SystemTun,state.TunActive);var output=new TextBox{Text=Report(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,FlowDirection=FlowDirection.LeftToRight,FontFamily=new("Menlo, Consolas, monospace"),MinHeight=240,MaxHeight=400};p.Children.Add(output);p.Children.Add(ActionButton("COPY",()=>_=Guard(async()=>{output.Text=Report();if(Clipboard!=null)await Clipboard.SetTextAsync(output.Text);})));p.Children.Add(ActionButton("UPDATE",()=>output.Text=Report()));p.Children.Add(ActionButton("SAVE",()=>_=Guard(()=>SaveText(Report(),"Rovunet-diagnostics.json"))));Navigate("DIAGNOSTICS",p);}
}
