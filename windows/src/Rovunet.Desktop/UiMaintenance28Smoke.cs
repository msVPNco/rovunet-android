using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    public async Task<bool> RunMaintenance28Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();string error="";string folder=Path.GetDirectoryName(report)!;Directory.CreateDirectory(folder);
        string privateRoot=Path.Combine(Path.GetTempPath(),"RovunetMaintenance28-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(privateRoot);
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();}
        bool Reject(Action action,string? code=null){try{action();return false;}catch(SafeError e){return code==null||e.Code==code;}}
        static string Encrypt(string json){using var aes=Aes.Create();aes.Key=Encoding.ASCII.GetBytes("ca815ecfdb1f153a");return Convert.ToBase64String(aes.EncryptCbc(Encoding.UTF8.GetBytes(json),Encoding.ASCII.GetBytes("lvcas56410c97lpb"),PaddingMode.PKCS7));}
        static string Cipher(string a,string b)=>Encrypt(JsonSerializer.Serialize(new{mci=new[]{new{config=a,ads_url="https://ads.example.invalid/never"}},mtn=new[]{new{config=a},new{config=b}}}));
        static Profile P(string raw,string source=""){var p=Importer.Single(raw);p.SourceId=source;p.CollectionId=source.Length>0?"sub:"+source:"fixture";p.CollectionName=source.Length>0?"Fixture subscription":"Fixture";p.ItemId=Importer.Fingerprint(p.Raw);return p;}
        string Snapshot()=>JsonSerializer.Serialize(new{data.Profiles,data.Subscriptions,data.LocalOverrideSources,data.LocalOverrideNames,data.SelectedId,data.SubscriptionUpdated});
        try{
            const string sentinel="Synthetic secret for DPAPI fixture 28";
            PrivateStore.SaveIn(privateRoot,"sample.dpapi",sentinel);string path=Path.Combine(privateRoot,"sample.dpapi");
            checks["dpapi_roundtrip"]=PrivateStore.LoadFrom<string>(privateRoot,"sample.dpapi")==sentinel;
            checks["ciphertext_does_not_contain_plaintext"]=!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(sentinel);
            var acl=new FileInfo(path).GetAccessControl();var sid=WindowsIdentity.GetCurrent().User!;
            checks["owner_only_acl_from_creation"]=acl.AreAccessRulesProtected&&acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().All(r=>r.IdentityReference.Equals(sid));
            var before=File.ReadAllBytes(path);using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))checks["locked_destination_save_rejected"]=Reject(()=>PrivateStore.SaveIn(privateRoot,"sample.dpapi","replacement"),"PRIVATE_STORE_FAILED");
            checks["save_failure_preserves_old_ciphertext"]=before.SequenceEqual(File.ReadAllBytes(path))&&PrivateStore.LoadFrom<string>(privateRoot,"sample.dpapi")==sentinel;
            checks["failed_atomic_write_cleans_own_temp"]=!Directory.EnumerateFiles(privateRoot,"*.tmp").Any();
            string corrupt=Path.Combine(privateRoot,"corrupt.dpapi");var broken=before.ToArray();broken[broken.Length/2]^=0x7f;File.WriteAllBytes(corrupt,broken);var hash=SHA256.HashData(broken);
            checks["corrupt_dpapi_rejected_without_reset"]=Reject(()=>PrivateStore.LoadFrom<UserData>(privateRoot,"corrupt.dpapi"),"PRIVATE_STORE_FAILED")&&hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(corrupt)));
            checks["wrong_json_shape_rejected_without_reset"]=Reject(()=>PrivateStore.LoadFrom<UserData>(privateRoot,"sample.dpapi"),"PRIVATE_STORE_FAILED")&&before.SequenceEqual(File.ReadAllBytes(path));
            PrivateStore.SaveIn<string?>(privateRoot,"null.dpapi",null);checks["encrypted_null_does_not_create_empty_user_store"]=Reject(()=>PrivateStore.LoadFrom<UserData>(privateRoot,"null.dpapi"),"PRIVATE_STORE_FAILED");
            PrivateStore.SaveIn(privateRoot,"legacy.dpapi",new{Profiles=new[]{Importer.Single("socks5://127.0.0.1:9#Legacy")},Subscriptions=new[]{new{Id="legacy",Url="https://legacy.example.invalid/sub",Name="Legacy"}},SelectedId="keep-selection",Theme="dark"});
            var migrated=PrivateStore.LoadFrom<UserData>(privateRoot,"legacy.dpapi")!;checks["legacy_store_defaults_new_field_without_losing_identity"]=migrated.Profiles.Count==1&&migrated.Subscriptions.Single().PublicSource==""&&migrated.Subscriptions.Single().Id=="legacy"&&migrated.SelectedId=="keep-selection"&&migrated.Theme=="dark";
            checks["path_traversal_rejected"]=Reject(()=>PrivateStore.SaveIn(privateRoot,"../escape.dpapi",sentinel),"PRIVATE_STORE_FAILED");
            checks["absolute_filename_rejected"]=Reject(()=>PrivateStore.SaveIn(privateRoot,path,sentinel),"PRIVATE_STORE_FAILED");
            string oversized=Path.Combine(privateRoot,"oversized.dpapi");using(var file=File.Create(oversized))file.SetLength(32*1024*1024+1);
            checks["oversized_store_rejected_before_decrypt"]=Reject(()=>PrivateStore.LoadFrom<string>(privateRoot,"oversized.dpapi"),"PRIVATE_STORE_FAILED");

            const string a="socks5://127.0.0.1:9#Kept",b="socks5://127.0.0.1:10#New",removed="socks5://127.0.0.1:11#Removed";
            string cipher=Cipher(a,b);var decoded=MahsaFeed28.Decode(cipher);
            checks["mahsa_ascii_aes_mci_mtn_deduplicated"]=decoded.SequenceEqual(new[]{a,b});
            checks["mahsa_invalid_base64_rejected"]=Reject(()=>MahsaFeed28.Decode("%%%"),"MAHSA_DECODE_FAILED");
            checks["mahsa_invalid_padding_rejected"]=Reject(()=>MahsaFeed28.Decode(Convert.ToBase64String(new byte[32])),"MAHSA_DECODE_FAILED");
            checks["mahsa_non_json_rejected"]=Reject(()=>MahsaFeed28.Decode(Encrypt("not JSON")),"MAHSA_DECODE_FAILED");
            checks["mahsa_duplicate_critical_json_key_rejected"]=Reject(()=>MahsaFeed28.Decode(Encrypt("{\"mci\":[],\"mci\":[],\"mtn\":[]}")),"MAHSA_DECODE_FAILED");
            checks["mahsa_empty_feed_rejected"]=Reject(()=>MahsaFeed28.Decode(Encrypt("{\"mci\":[],\"mtn\":[]}")),"NO_SUPPORTED_IMPORT");
            checks["public_presets_only_mahsa_and_anonymous"]=PublicFeed.Presets.Select(p=>p.Key).SequenceEqual(new[]{"mahsa","anonymous"});

            string oldUrl="https://raw.githubusercontent.com/hiddify/hiddify-app/refs/heads/main/test.configs/mahsa",source=Importer.Fingerprint(oldUrl);
            var kept=P(a,source);string keptId=kept.Id;var old=P(removed,source);var manual=P("socks5://127.0.0.1:12#Manual");var unrelated=P("socks5://127.0.0.1:13#Other","other");
            data.Profiles=[kept,old,manual,unrelated];data.Subscriptions=[new(){Id=source,Url=oldUrl,Name="Owner Mahsa name"},new(){Id="other",Url="https://other.example.invalid/sub",Name="Other"}];data.SelectedId=keptId;SetState(new());BuildCards();
            var requested=new List<string>();subscriptionFetchFixture28=(uri,stop,port)=>{requested.Add(uri.AbsoluteUri);return Task.FromResult(new SubscriptionClient.Payload(cipher,null,""));};
            await UpdateCollection20(source);
            checks["legacy_mahsa_rewritten_before_first_request"]=requested.SequenceEqual(new[]{PublicFeed.MahsaUrl});
            checks["advertising_metadata_never_requested"]=requested.All(u=>!u.Contains("ads.example.invalid")&&!u.Contains("hiddify"));
            checks["source_id_name_and_selection_preserved"]=data.Subscriptions.Single(s=>s.Id==source) is var sub&&sub.Url==PublicFeed.MahsaUrl&&sub.PublicSource=="mahsa"&&sub.Name=="Owner Mahsa name"&&data.SelectedId==keptId;
            checks["subscription_exact_replacement_removes_old"]=data.Profiles.Where(p=>p.SourceId==source).Select(p=>p.Raw).Order().SequenceEqual(new[]{a,b}.Order())&&!data.Profiles.Any(p=>p.Raw==removed);
            checks["manual_and_other_subscription_preserved"]=data.Profiles.Any(p=>ReferenceEquals(p,manual))&&data.Profiles.Any(p=>ReferenceEquals(p,unrelated))&&data.Subscriptions.Any(s=>s.Id=="other");
            string baseline=Snapshot();
            foreach(string failure in new[]{"network","decode","empty","invalid-config","mixed-invalid-config","mixed-tls","native-validation","cancel","save"}){
                storeSaveFixture28=failure=="save"?()=>throw new SafeError("PRIVATE_STORE_FAILED"):null;
                subscriptionFetchFixture28=(uri,stop,port)=>failure is "network" or "cancel"?Task.FromException<SubscriptionClient.Payload>(new SafeError(failure=="cancel"?"CANCELLED":"FETCH_FAILED")):Task.FromResult(new SubscriptionClient.Payload(failure switch{"decode"=>"invalid base64","empty"=>Encrypt("{\"mci\":[],\"mtn\":[]}"),"invalid-config"=>Cipher("unsupported://bad","unsupported://also-bad"),"mixed-invalid-config"=>Cipher(a,"unsupported://bad"),"mixed-tls"=>Cipher(a,"trojan://fixture@server.example.invalid:443?allowInsecure=1#Unsafe"),"native-validation"=>Cipher("rovunet://broken",a),_=>Cipher(b,"socks5://127.0.0.1:14#Replacement")},null,""));
                await UpdateCollection20(source);checks["failed_"+failure+"_keeps_complete_old_subscription"]=Snapshot()==baseline;checks["failed_"+failure+"_notice_has_correct_error_or_cancel_colour"]=notice.Foreground==(failure=="cancel"?Muted:Red);storeSaveFixture28=null;
                if(failure=="mixed-tls")checks["partial_mahsa_validation_shows_the_rejection_reason"]=notice.Text.Contains(L.Text("TLS_UNVERIFIED"));
            }
            int removedSourceRequests=0;data.Subscriptions.Add(new(){Id="removed-source",Url="https://raw.githubusercontent.com/patterniha/Free-Configs/refs/heads/main/configs.txt",Name="Old stored source"});
            subscriptionFetchFixture28=(uri,stop,port)=>{removedSourceRequests++;return Task.FromResult(new SubscriptionClient.Payload(cipher,null,""));};string removalBaseline=Snapshot();await UpdateCollection20("removed-source");
            checks["removed_preset_cannot_fetch_and_keeps_existing_data"]=removedSourceRequests==0&&Snapshot()==removalBaseline;
            subscriptionFetchFixture28=null;

            foreach(string language in new[]{"fa","en","ru","zh-CN"})foreach(string theme in new[]{"light","dark","amoled"}){
                L.Language=data.Language=language;data.Theme=theme;Width=480;Height=880;ApplyTheme();Build();string id=language+"_"+theme;
                foreach(var preset in PublicFeed.Presets){
                    var page=BuildSubscriptionPage28(initialUrl:preset.Url);page.Show();await Idle();
                    checks[id+"_"+preset.Key+"_input_url_hidden"]=!Visuals<TextBox>(page.PageVisual).Any(t=>t.Text.Contains("raw.githubusercontent.com"));page.Close();
                    var detail=BuildSubscriptionDetails28(new(){Id="managed",Url=preset.Url,Name=preset.DisplayName});detail.Show();await Idle();
                    checks[id+"_"+preset.Key+"_details_have_no_url_or_copy"]=!Visuals<TextBox>(detail.PageVisual).Any(t=>Equals(t.Tag,"SAVED_SUBSCRIPTION_URL"))&&!Visuals<Button>(detail.PageVisual).Any(t=>AutomationProperties.GetName(t)==L.Text("COPY"));detail.Close();
                }
                data.Profiles=[P("socks5://127.0.0.1:9#Fresh success"),P("socks5://127.0.0.1:10#Old success"),P("socks5://127.0.0.1:11#TLS failure"),P("socks5://127.0.0.1:12#Timeout"),P("socks5://127.0.0.1:13#Not tested")];
                data.Subscriptions=[];data.ExpandedCollections["fixture"]=true;data.Measurements.Clear();measurements.Clear();liveMeasurements.Clear();
                for(int i=0;i<4;i++){var p=data.Profiles[i];var m=new PathMeasurement{Verified=i<2,ElapsedMs=i<2?21:0,Error=i==2?"PROBE_TLS_FAILED":i==3?"PROBE_TIMEOUT":"",SampledAt=DateTimeOffset.UtcNow.AddHours(i==1?-2:0),Attempts=1,Passes=i<2?1:0};data.Measurements[p.Id]=m;measurements[p.Id]=new(){Verified=m.Verified,ElapsedMs=m.ElapsedMs,Error=m.Error};if(i!=1)liveMeasurements.Add(p.Id);}
                cardsSignature="";SetState(new());BuildCards();await Idle();
                checks[id+"_fresh_ping_green_old_ping_muted"]=rows[0].Timing.Foreground==Green&&rows[1].Timing.Foreground==Muted;
                checks[id+"_failed_test_text_red_without_fake_certainty"]=rows[2].Result.Foreground==Red&&rows[3].Result.Foreground==Red&&rows[3].Result.Text.Contains(L.Text("TEST_UNCONFIRMED"));
                checks[id+"_disconnected_title_red"]=connectionText.Foreground==Red&&heroTitle.Foreground==Red;
                SetState(new(){State="error",Error="CORE_START_FAILED"});checks[id+"_connection_error_text_red"]=errorText.Foreground==Red&&heroTitle.Foreground==Red&&heroDetail.Foreground==Red;SetState(new());
                var more=Visuals<Button>((FrameworkElement)Content).Where(x=>Equals(x.Tag,"CARD_MORE")).ToArray();
                checks[id+"_all_card_menus_visible_touch_targets"]=more.Length==data.Profiles.Count+1&&more.All(x=>x.IsVisible&&x.ActualWidth>=44&&x.ActualHeight>=44&&x.ContextMenu!=null);
                foreach(double scale in new[]{1d,1.25,1.5,2d})checks[id+"_three_visible_dots_dpi_"+(int)(96*scale)]=more.All(button=>VisibleDots28(button,scale)==3);
                var sample=more.Last();sample.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Idle();checks[id+"_menu_opens_for_its_own_card"]=sample.ContextMenu!.IsOpen&&ReferenceEquals(sample.ContextMenu.PlacementTarget,sample);sample.ContextMenu.IsOpen=false;
                if(language is "fa" or "en"){await Idle();SavePage((FrameworkElement)Content,Path.Combine(folder,"status-menus-"+id+".png"));}
            }
            string? samplePath=Environment.GetEnvironmentVariable("ROVUNET_MAHSA_SAMPLE");
            if(!string.IsNullOrEmpty(samplePath)){
                if(new FileInfo(samplePath).Length>Importer.MaxBytes)throw new SafeError("IMPORT_TOO_LARGE");var links=MahsaFeed28.Decode(File.ReadAllText(samplePath));var preview=PublicFeed.Preview(string.Join('\n',links),true);
                checks["owner_sample_decodes_and_has_usable_profiles"]=links.Length>0&&preview.Profiles.Count>0;
                bool canApply=true;try{PublicFeed.ValidateForApply(PublicFeed.MahsaUrl,preview);}catch(SafeError e)when(e.Code=="MAHSA_VALIDATION_FAILED"){canApply=false;}
                checks["owner_sample_partial_validation_never_replaces_saved_collection"]=canApply==!preview.Skipped.Values.Any(count=>count>0);
                File.WriteAllText(Path.Combine(folder,"owner-sample-redacted.json"),JsonSerializer.Serialize(new{decoded=links.Length,accepted=preview.Profiles.Count,skipped=preview.Skipped,automaticReplacementAllowed=canApply,containsRawLinks=false,connectionTested=false}));
            }
        }catch(Exception ex){checks["exception_"+ex.GetType().Name]=false;error=ex is SafeError safe?safe.Code:ex.GetType().Name;}
        finally{
            storeSaveFixture28=null;subscriptionFetchFixture28=null;SetState(new());
            string full=Path.GetFullPath(privateRoot);if(full.StartsWith(Path.GetFullPath(Path.GetTempPath())+"RovunetMaintenance28-",StringComparison.OrdinalIgnoreCase))try{Directory.Delete(full,true);}catch{}
        }
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{passed,checks,error,syntheticFixture=true,ownerStoreTouched=false,systemNetworkSettingsChanged=false,actualOtherOperatingSystemsTested=false,dpiChecks="native WPF rasterization at96/120/144/192; physical mixed-DPI hardware not tested"},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
    static int VisibleDots28(Button button,double scale)
    {
        int width=(int)Math.Ceiling(button.ActualWidth*scale),height=(int)Math.Ceiling(button.ActualHeight*scale);var image=new RenderTargetBitmap(width,height,96*scale,96*scale,PixelFormats.Pbgra32);var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(button){Stretch=Stretch.Fill},null,new Rect(0,0,button.ActualWidth,button.ActualHeight));image.Render(visual);
        var ink=((SolidColorBrush)Application.Current.FindResource("Ink")).Color;var pixels=new byte[width*height*4];image.CopyPixels(pixels,width*4,0);var seen=new bool[width*height];int components=0;
        bool Match(int p)=>pixels[p*4+3]>220&&Math.Abs(pixels[p*4]-ink.B)<25&&Math.Abs(pixels[p*4+1]-ink.G)<25&&Math.Abs(pixels[p*4+2]-ink.R)<25;
        for(int start=0;start<seen.Length;start++)if(!seen[start]&&Match(start)){
            var queue=new Queue<int>();queue.Enqueue(start);seen[start]=true;int count=0,minX=width,maxX=0,minY=height,maxY=0;
            while(queue.Count>0){int p=queue.Dequeue(),x=p%width,y=p/width;count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);foreach(var (dx,dy) in new[]{(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,-1),(-1,1),(1,1)}){int xx=x+dx,yy=y+dy;if(xx<0||xx>=width||yy<0||yy>=height)continue;int n=yy*width+xx;if(!seen[n]&&Match(n)){seen[n]=true;queue.Enqueue(n);}}}
            if(count>=2&&maxX-minX<10*scale&&maxY-minY<10*scale)components++;
        }
        return components;
    }
}
