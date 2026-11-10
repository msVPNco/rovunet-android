using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    public async Task<bool> RunProduct20Smoke(string report)
    {
        var checks=new Dictionary<string,bool>();
        async Task Idle(){await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);UpdateLayout();}
        try{
            var now=DateTimeOffset.UtcNow;string source=Importer.Fingerprint("https://subscription.example.invalid/sub/fixture");
            var usage=SubscriptionUsage20.Parse("upload=100; download=200; total=1000; expire="+now.AddDays(5).ToUnixTimeSeconds(),"",now)!;var view=usage.Snapshot(source).Evaluate();var account=AccountUsage.From(view);
            checks["header_usage_and_expiry_are_per_account"]=account.Used==300&&account.Remaining==700&&account.TimeLeft?.TotalDays>4&&account.Current;
            checks["missing_values_stay_unknown"]=SubscriptionUsage20.Parse("total=1000","",now)?.Snapshot(source).Status is{Metering:"partial",ExpiryKnown:false,UsedBytes:null};
            checks["explicit_zeros_mean_unlimited"]=SubscriptionUsage20.Parse("upload=0;download=0;total=0;expire=0","",now)?.Snapshot(source).Status is{Metering:"not_metered",ExpiryKnown:true,ExpiresAt:null};
            checks["duplicate_negative_overflow_and_bad_dates_rejected"]=new[]{"upload=1;upload=2","total=-1","upload=9223372036854775807;download=1","expire=9223372036854775807"}.All(t=>SubscriptionUsage20.Parse(t,"",now)==null);
            string link="socks5://127.0.0.1:9#Product20";string body="#subscription-userinfo: upload=1; download=2; total=10; expire=0\nsupport-url: https://support.example.invalid/\nprofile-web-page-url: https://account.example.invalid/\n"+link;
            checks["body_metadata_not_counted_as_bad_profiles"]=PublicFeed.Preview(body,true).Total==1&&SubscriptionUsage20.ConfigBody(body).Trim()==link;
            checks["base64_metadata_is_read_without_changing_profile"]=SubscriptionUsage20.Parse("",Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body)),now)?.Total==10;
            string mask="""{"tcp":[{"type":"fragment","settings":{"packets":"tlshello","lengths":["0","104","1"],"delays":["0"],"maxSplit":"0"}},{"type":"fragment","settings":{"packets":"1-1","lengths":["114","1"],"delays":["1"],"maxSplit":"11"}}]}""";
            string maskLink="vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=ws&security=tls&cs=TLS_AES_128_GCM_SHA256&fm="+Uri.EscapeDataString(mask);var maskProfile=Importer.Single(maskLink);var config=Importer.Connection(maskProfile).Outbound;
            checks["cipher_and_fragment_mask_preserved_exactly"]=config["streamSettings"]!["tlsSettings"]!["cipherSuites"]!.GetValue<string>()=="TLS_AES_128_GCM_SHA256"&&System.Text.Json.Nodes.JsonNode.DeepEquals(config["streamSettings"]!["finalmask"],System.Text.Json.Nodes.JsonNode.Parse(mask));
            var form=CoreProfileEditing.Read(maskProfile)!;var edited=CoreProfileEditing.Apply(maskProfile,"Renamed",form.Fields);checks["editing_keeps_cipher_and_mask"]=System.Text.Json.Nodes.JsonNode.DeepEquals(Importer.Connection(edited.Profile).Outbound["streamSettings"]!["finalmask"],config["streamSettings"]!["finalmask"]);
            using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"engines","xray.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,ArgumentList={"run","-test","-c","stdin:"}})!){var stdout=process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);var stderr=process.StandardError.BaseStream.CopyToAsync(Stream.Null);await process.StandardInput.WriteAsync(CoreLinks.Runtime(maskProfile,21001,21002));process.StandardInput.Close();await process.WaitForExitAsync();await Task.WhenAll(stdout,stderr);checks["pinned_xray_accepts_native_mask_configuration"]=process.ExitCode==0;}
            bool Reject(string text){try{Importer.Single(text);return false;}catch(SafeError){return true;}}
            checks["unsupported_masks_and_unknown_cipher_are_not_silently_dropped"]=Reject(maskLink.Replace("TLS_AES_128_GCM_SHA256","TLS_UNKNOWN_CIPHER"))&&Reject(maskLink.Replace(Uri.EscapeDataString(mask),Uri.EscapeDataString("{\"tcp\":[{\"type\":\"noise\",\"settings\":{}}]}")));
            checks["empty_shadowsocks_query_is_compatible"]=Importer.Single("ss://"+Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("aes-256-gcm:fixture"))+"@server.example.invalid:443/?#Fixture").CanConnect;
            var newSeed=Importer.Single("vless://00000000-0000-4000-8000-000000000001@server.example.invalid:443?type=kcp&security=none#VLESS");Profile? created=null;var newEditor=BuildCoreProfileEditor(newSeed,p=>created=p);newEditor.Show();await Idle();
            checks["new_vless_editor_requires_server_and_identity"]=Visuals<TextBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:server")).Text.Length==0&&Visuals<PasswordBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:auth")).Password.Length==0;
            Visuals<TextBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:server")).Text="127.0.0.1";Visuals<PasswordBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:auth")).Password="00000000-0000-4000-8000-000000000002";
            Visuals<TextBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:mtu")).Text="1350";Visuals<TextBox>(newEditor.PageVisual).Single(t=>Equals(t.Tag,"core:tti")).Text="20";
            Visuals<Button>(newEditor.PageVisual).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==L.Text("SAVE")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            checks["manual_mkcp_uses_full_editor_and_keeps_timing"]=created!=null&&created.Protocol=="VLESS mKCP"&&Importer.Connection(created).Outbound["streamSettings"]!["kcpSettings"]!["mtu"]!.GetValue<int>()==1350&&!newEditor.IsVisible;if(newEditor.IsVisible)newEditor.Close();
            var profile=Importer.Single(link);profile.SourceId=source;profile.CollectionId="sub:"+source;profile.CollectionName="Fixture";data.Profiles=[profile];data.Subscriptions=[new(){Id=source,Url="https://subscription.example.invalid/sub/fixture",Name="Fixture"}];data.SelectedId=profile.Id;
            var incoming=Importer.Single(link);subscriptionUsage20[source]=usage;activeProfile=profile;SetState(new(){State="connected"});CommitSubscription20(new Uri(data.Subscriptions[0].Url),[incoming],"Fixture");
            checks["update_preserves_ids_selection_and_active_runtime_object"]=data.Profiles.Single().Id==profile.Id&&data.SelectedId==profile.Id&&ReferenceEquals(activeProfile,profile)&&state.State=="connected"&&data.Subscriptions.Single().Usage==usage;
            try{CommitSubscription20(new Uri(data.Subscriptions[0].Url),[],"Fixture");checks["empty_update_keeps_old_profiles"]=false;}catch(SafeError){checks["empty_update_keeps_old_profiles"]=data.Profiles.Count==1;}
            var second=Importer.Single("socks5://127.0.0.1:10#Second");second.CollectionId="manual20";data.Profiles.Add(second);BuildCards();ActivateProfileCard20(second);
            checks["first_click_only_selects_while_other_profile_connected"]=data.SelectedId==second.Id&&ReferenceEquals(activeProfile,profile)&&state.State=="connected"&&client==null;
            checks["selected_profile_is_not_marked_connected"]=rows.Single(r=>r.Profile.Id==profile.Id).Protocol.Text.Contains(L.Text("CONNECTED"))&&!rows.Single(r=>r.Profile.Id==second.Id).Protocol.Text.Contains(L.Text("CONNECTED"));
            data.SelectedId="";checks["no_selection_can_choose_a_connectable_profile"]=RandomConnectableProfile20() is{CanConnect:true};
            statuses[source]=SubscriptionUsage20.Parse("upload=1000000000;download=5000000000;total=30000000000;expire="+now.AddDays(28).ToUnixTimeSeconds(),"",now)!.Snapshot(source);data.ExpandedCollections["sub:"+source]=true;
            activeProfile=null;SetState(new());
            foreach(string language in new[]{"fa","en"})foreach(string theme in new[]{"amoled","light"})foreach(double width in new[]{580d,1180d}){
                L.Language=data.Language=language;data.Theme=theme;Width=width;Height=940;ApplyTheme();Build();await Idle();string id=language+theme+width;
                checks[id+"_compact_header_and_touch_target"]=Math.Abs(heroCard19.ActualHeight-HeroExpanded19)<1&&connect.TranslatePoint(new Point(connect.ActualWidth,connect.ActualHeight),this).Y-connect.TranslatePoint(new Point(),this).Y>=44;
                checks[id+"_compact_width_and_sidebar"]=MinWidth<=580&&contextHost.IsVisible==(width>=ContextThreshold);
                var mark=Visuals<Viewbox>((DependencyObject)Content).Single(v=>Equals(v.Tag,"ROVUNET_BRAND_MARK"));var brandHost=width>=ContextThreshold?(FrameworkElement)contextHost:(FrameworkElement)homeContent;double markLeft=Math.Min(mark.PointToScreen(new Point()).X,mark.PointToScreen(new Point(mark.ActualWidth,mark.ActualHeight)).X);double hostLeft=Math.Min(brandHost.PointToScreen(new Point()).X,brandHost.PointToScreen(new Point(brandHost.ActualWidth,brandHost.ActualHeight)).X);checks[id+"_english_brand_is_physically_left"]=markLeft-hostLeft>=0&&markLeft-hostLeft<50;
                SavePage((FrameworkElement)Content,Path.Combine(Path.GetDirectoryName(report)!,"product20-"+id+".png"));
            }
        }catch(Exception error){checks[error is SafeError safe?safe.Code:error.GetType().Name]=false;}
        bool passed=checks.Values.All(v=>v);File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Product20 subscription metadata, transactional updates, selection and compact UI",checks,passed},new JsonSerializerOptions{WriteIndented=true}));return passed;
    }
    public async Task<bool> RunMetadata20Network(string report)
    {
        var results=new List<object>();int row=0;
        foreach(var sub in data.Subscriptions.DistinctBy(s=>s.Id).Take(16)){
            row++;try{var payload=await SubscriptionClient.Fetch(SubscriptionUrls.Validate(sub.Url));var usage=SubscriptionUsage20.Parse(payload.UserInfo,payload.Body,payload.Date);var batch=PublicFeed.IsKnown(sub.Url)?PublicFeed.Preview(payload.Body,true):Importer.ReadCompatible(SubscriptionUsage20.ConfigBody(payload.Body),true);
                await RefreshStatus(SubscriptionUrls.Validate(sub.Url),sub);CaptureUsage20(sub.Id,payload);statuses.TryGetValue(sub.Id,out var snapshot);var status=snapshot?.Status;
                results.Add(new{row,headerProvided=payload.UserInfo.Length>0,standardMetadataParsed=usage!=null,usedKnown=status?.UsedBytes!=null,quotaKnown=status?.LimitBytes!=null,expiryKnown=status?.ExpiryKnown==true,explicitNoExpiry=status is{ExpiryKnown:true,ExpiresAt:null},trustworthy=snapshot?.Evaluate().Trustworthy==true,imported=batch.Profiles.Count,rejected=batch.Skipped,error=""});
            }catch(Exception error){results.Add(new{row,error=error is SafeError safe?safe.Code:"METADATA_AUDIT_FAILED"});}
        }
        File.WriteAllText(report,JsonSerializer.Serialize(new{suite="Read-only subscription metadata audit",privateValuesIncluded=false,fixtureStoreWritesDisabled=fixtureMode,results},new JsonSerializerOptions{WriteIndented=true}));return results.Count>0;
    }
}
