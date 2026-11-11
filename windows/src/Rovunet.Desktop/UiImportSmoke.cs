using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    async Task ImportAndEndpointSmoke(Dictionary<string,bool> checks,string report)
    {
        const string identifier="00000000-0000-4000-8000-000000000001";
        var originalProfiles=data.Profiles;string selected=data.SelectedId;
        var originalOverrides=data.LocalOverrideSources;data.LocalOverrideSources=[];
        string uri=$"vless://{identifier}@old.example.invalid:443?type=ws&security=tls&host=ws.example.invalid&path=%2Ftest&sni=tls.example.invalid#Fixture";
        var profile=Importer.Single(uri);profile.SourceId="synthetic-sub";data.Profiles=[profile];data.SelectedId=profile.Id;
        void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Button FindButton(DialogWindow d,string key)=>Visuals<Button>(d.PageVisual).Single(b=>b.IsVisible&&AutomationProperties.GetName(b)==L.Text(key));
        TextBox Field(DialogWindow d,string key)=>Visuals<TextBox>(d.PageVisual).Single(b=>Equals(b.Tag,key)&&b.IsVisible);
        void Modal(DialogWindow d,Action action)
        {
            _=Dispatcher.BeginInvoke(()=>{try{action();}catch(Exception e){checks["import_endpoint_exception_"+e.GetType().Name]=false;}finally{if(d.IsVisible)d.Close();}},DispatcherPriority.ApplicationIdle);d.ShowDialog();
        }
        try
        {
            foreach(string lang in new[]{"en","fa"})foreach(string theme in new[]{"dark","light"})
            {
                L.Language=lang;data.Theme=theme;ApplyTheme();Build();
                foreach(string page in new[]{"menu","import","manual","qr","endpoint"})
                {
                    var d=page=="endpoint"?BuildEndpointEditor(profile):page=="menu"?Dialog("MORE"):BuildAddConfiguration(page=="manual"?2:page=="qr"?1:0);
                    if(page=="menu")d.Content=BuildAppMenu(()=>d.Close());d.Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);d.UpdateLayout();
                    SavePage(d.PageVisual,Path.Combine(Path.GetDirectoryName(report)!,page+"-"+lang+"-"+theme+".png"));
                    checks[page+"_"+lang+"_"+theme+"_responsive"]=d.ActualWidth<=ActualWidth;
                    d.Width=680;d.UpdateLayout();
                    checks[page+"_"+lang+"_"+theme+"_touch"]=Visuals<Button>(d.PageVisual).Where(b=>b.IsVisible).All(b=>b.ActualHeight>=44&&b.ActualWidth>=44&&b.ActualWidth<=680);
                    if(page=="manual"){
                        var kind=Visuals<ComboBox>(d.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible);kind.SelectedIndex=4;d.UpdateLayout();
                        checks["manual_"+lang+"_"+theme+"_ssh_fields"]=Field(d,"SERVER_PORT").Text=="22"&&Visuals<PasswordBox>(d.PageVisual).Count(p=>p.IsVisible)==1;
                        kind.SelectedIndex=3;d.UpdateLayout();checks["manual_"+lang+"_"+theme+"_wireguard_fields"]=Field(d,"WIREGUARD_TEXT").IsVisible&&!Visuals<PasswordBox>(d.PageVisual).Any(p=>p.IsVisible);
                    }
                    Click(FindButton(d,"BACK"));checks[page+"_"+lang+"_"+theme+"_back"]=!d.IsVisible;
                }
            }
            L.Language="en";data.Theme="dark";ApplyTheme();Build();
            var editor=BuildEndpointEditor(profile);
            Modal(editor,()=>{
                Field(editor,"SERVER_PORT").Text="0";Click(FindButton(editor,"SAVE"));checks["endpoint_invalid_save_is_atomic"]=ReferenceEquals(data.Profiles[0],profile)&&profile.Raw==uri&&editor.IsVisible;
                Field(editor,"SERVER_PORT").Text="8443";Field(editor,"SERVER_HOST").Text="new.example.invalid";Field(editor,"PROFILE_NAME").Text="Edited fixture";Click(FindButton(editor,"SAVE"));
                checks["endpoint_modal_saves_and_closes"]=editor.DialogResult==true&&data.Profiles[0].Name=="Edited fixture";
                checks["endpoint_subscription_identity_and_wire_preserved"]=profile.Raw==uri&&data.Profiles[0].Id==profile.Id&&data.LocalOverrideSources[profile.Id]==uri&&data.Profiles[0].Raw.Contains("host=ws.example.invalid&path=%2Ftest&sni=tls.example.invalid");
            });
            var cancel=BuildEndpointEditor(data.Profiles[0]);string prior=data.Profiles[0].Raw;Modal(cancel,()=>{Field(cancel,"SERVER_HOST").Text="unsaved.example.invalid";Click(FindButton(cancel,"BACK"));});checks["endpoint_back_discards_draft"]=data.Profiles[0].Raw==prior;
            var draftEditor=BuildEndpointEditor(data.Profiles[0]);Modal(draftEditor,()=>{
                Field(draftEditor,"SERVER_HOST").Text="draft.example.invalid";
                _=Dispatcher.BeginInvoke(()=>{var child=DialogWindow.OpenPages.Single(w=>w!=draftEditor&&Visuals<TextBox>(w.PageVisual).Any(t=>Equals(t.Tag,"RAW_PROFILE")));checks["advanced_editor_receives_unsaved_endpoint_draft"]=Field(child,"RAW_PROFILE").Text.Contains("@draft.example.invalid:8443");Click(FindButton(child,"BACK"));},DispatcherPriority.ApplicationIdle);
                Click(FindButton(draftEditor,"ADVANCED_RAW"));checks["advanced_back_keeps_form_draft_without_saving"]=draftEditor.IsVisible&&Field(draftEditor,"SERVER_HOST").Text=="draft.example.invalid"&&data.Profiles[0].Raw==prior;Click(FindButton(draftEditor,"BACK"));
            });
            var reset=BuildEndpointEditor(data.Profiles[0]);Modal(reset,()=>{
                _=Dispatcher.BeginInvoke(()=>{var confirmation=DialogWindow.OpenPages.Single(w=>w!=reset&&Visuals<Button>(w.PageVisual).Any(b=>AutomationProperties.GetName(b)==L.Text("APPROVE")));Click(FindButton(confirmation,"APPROVE"));},DispatcherPriority.ApplicationIdle);
                Click(FindButton(reset,"RESET_LOCAL_EDIT"));checks["endpoint_reset_restores_subscription_original"]=reset.DialogResult==true&&data.Profiles[0].Raw==uri&&!data.LocalOverrideSources.ContainsKey(profile.Id);
            });
            var import=BuildAddConfiguration();int count=data.Profiles.Count;
            Modal(import,()=>{
                Field(import,"LINK_TEXT").Text="invalid";Click(FindButton(import,"IMPORT"));checks["import_invalid_keeps_existing_profiles"]=data.Profiles.Count==count&&import.IsVisible;
                Field(import,"LINK_TEXT").Text="socks5://127.0.0.1:1080#Imported%20fixture\nsocks5://127.0.0.1:1080#Imported%20fixture";Click(FindButton(import,"IMPORT"));checks["import_preview_does_not_save"]=import.IsVisible&&data.Profiles.Count==count;Click(FindButton(import,"IMPORT"));checks["import_batch_deduplicates_and_closes"]=data.Profiles.Count==count+1&&import.DialogResult==true;
            });
            foreach(int kind in new[]{0,1,2,4,5})
            {
                var manual=BuildAddConfiguration(2);int before=data.Profiles.Count;
                Modal(manual,()=>{
                    Visuals<ComboBox>(manual.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible).SelectedIndex=kind;manual.UpdateLayout();
                    Field(manual,"PROFILE_NAME").Text="Manual fixture "+kind;Field(manual,"SERVER_HOST").Text="manual.example.invalid";Field(manual,"SERVER_PORT").Text=kind==4?"22":"443";
                    if(kind is 0 or 1 or 4 or 5)Field(manual,"ID_OR_USER").Text=kind==0?identifier:"fixture";
                    foreach(var pw in Visuals<PasswordBox>(manual.PageVisual).Where(p=>p.IsVisible))pw.Password="fixture";
                    Click(FindButton(manual,"IMPORT"));checks["manual_protocol_"+kind+"_imports_real_model"]=manual.DialogResult==true&&data.Profiles.Count==before+1&&data.Profiles[^1].CanConnect;
                });
            }
            string wg="[Interface]\nPrivateKey = "+Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('a',32)))+"\nAddress = 10.1.0.2/32\nDNS = 1.1.1.1\n[Peer]\nPublicKey = "+Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('b',32)))+"\nEndpoint = wg.example.invalid:51820\nAllowedIPs = 0.0.0.0/0, ::/0\nPersistentKeepalive = 25";
            var wireguard=Importer.Single(wg);data.Profiles.Add(wireguard);var raw=BuildRawEditor(wireguard);
            Modal(raw,()=>{Field(raw,"RAW_PROFILE").Text=wg.Replace("wg.example.invalid:51820","new.example.invalid:51821");Click(FindButton(raw,"SAVE"));checks["wireguard_portrait_raw_editor_preserves_keys_keepalive"]=raw.DialogResult==true&&data.Profiles.Single(p=>p.Id==wireguard.Id).Raw==wg.Replace("wg.example.invalid:51820","new.example.invalid:51821");});
            var wgImport=BuildAddConfiguration(2);int wgBefore=data.Profiles.Count;
            Modal(wgImport,()=>{Visuals<ComboBox>(wgImport.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible).SelectedIndex=3;wgImport.UpdateLayout();Field(wgImport,"WIREGUARD_TEXT").Text=wg.Replace("wg.example.invalid:51820","import.example.invalid:51822");Click(FindButton(wgImport,"IMPORT"));checks["manual_wireguard_imports"]=wgImport.DialogResult==true&&data.Profiles.Count==wgBefore+1;});
            foreach(int family in Enumerable.Range(0,5)){
                var dns=BuildAddConfiguration(2);int before=data.Profiles.Count;
                Modal(dns,()=>{
                    Visuals<ComboBox>(dns.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible).SelectedIndex=6;dns.UpdateLayout();Visuals<ComboBox>(dns.PageVisual).Single(c=>Equals(c.Tag,"PROTOCOL")&&c.IsVisible&&c.Items.Count==5).SelectedIndex=family;dns.UpdateLayout();
                    Field(dns,"PROFILE_NAME").Text="DNS manual "+family;Field(dns,"TUNNEL_DOMAIN").Text="dns.example.invalid";Field(dns,"DNS_RESOLVERS").Text="127.0.0.1:5353";
                    foreach(var field in Visuals<TextBox>(dns.PageVisual).Where(t=>t.IsVisible)){if(Equals(field.Tag,"SERVER_PUBLIC_KEY"))field.Text=new string('a',64);if(Equals(field.Tag,"ID_OR_USER"))field.Text="fixture";}
                    foreach(var pass in Visuals<PasswordBox>(dns.PageVisual).Where(p=>p.IsVisible))pass.Password="fixture";
                    Click(FindButton(dns,"IMPORT"));checks["manual_dns_family_"+family+"_imports"]=dns.DialogResult==true&&data.Profiles.Count==before+1&&data.Profiles[^1].CanConnect;
                });
            }
            string fixtureFile=Path.Combine(Path.GetDirectoryName(report)!,"synthetic-import.txt");File.WriteAllText(fixtureFile,"socks5://127.0.0.1:1090#File%20fixture");
            var fromFile=BuildAddConfiguration(0,_=>fixtureFile);int fileBefore=data.Profiles.Count;
            Modal(fromFile,()=>{Click(FindButton(fromFile,"CHOOSE_FILE"));checks["file_picker_reads_utf8_into_draft"]=Field(fromFile,"LINK_TEXT").Text.Contains("File%20fixture");Click(FindButton(fromFile,"IMPORT"));Click(FindButton(fromFile,"IMPORT"));checks["file_import_saves_real_model"]=fromFile.DialogResult==true&&data.Profiles.Count==fileBefore+1;});
            string qrText="socks5://127.0.0.1:1091#QR%20fixture";var matrix=new ZXing.QrCode.QRCodeWriter().encode(qrText,ZXing.BarcodeFormat.QR_CODE,320,320);var pixels=new byte[320*320*4];for(int y=0;y<320;y++)for(int x=0;x<320;x++){int i=(y*320+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=matrix[x,y]?(byte)0:(byte)255;pixels[i+3]=255;}var bitmap=BitmapSource.Create(320,320,96,96,PixelFormats.Bgra32,null,pixels,1280);bitmap.Freeze();
            var qrImport=BuildAddConfiguration(1,chooseQrImage:_=>bitmap);int qrBefore=data.Profiles.Count;
            _=Dispatcher.BeginInvoke((Action)(async()=>{
                try{Click(FindButton(qrImport,"QR_IMAGE"));var until=DateTime.UtcNow.AddSeconds(10);while(Visuals<TabControl>(qrImport.PageVisual).Single().SelectedIndex!=0&&DateTime.UtcNow<until)await Task.Delay(25);qrImport.UpdateLayout();checks["qr_bitmap_decodes_to_link_tab"]=Field(qrImport,"LINK_TEXT").Text==qrText;Click(FindButton(qrImport,"IMPORT"));Click(FindButton(qrImport,"IMPORT"));checks["qr_import_saves_real_model"]=qrImport.DialogResult==true&&data.Profiles.Count==qrBefore+1;}
                catch(Exception e){checks["qr_failure_"+e.GetType().Name]=false;}finally{if(qrImport.IsVisible)qrImport.Close();}
            }),DispatcherPriority.ApplicationIdle);qrImport.ShowDialog();
        }
        finally{data.Profiles=originalProfiles;data.SelectedId=selected;data.LocalOverrideSources=originalOverrides;Build();}
    }
}



