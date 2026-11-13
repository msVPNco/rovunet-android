using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Rovunet.Core;
using ZXing;
using ZXing.Common;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void AddConfiguration(int tab = 0) => BuildAddConfiguration(tab).ShowDialog();
    DialogWindow BuildAddConfiguration(int tab = 0,Func<Window,string?>? chooseImportFile=null,Func<Window,BitmapSource?>? chooseQrImage=null,string? manualProtocol=null)
    {
        var d = Dialog("IMPORT", 500, 800);d.UsePageChrome();var tabs = new TabControl { Margin = new(18),Tag="IMPORT_TABS" };
        var link = new StackPanel(); link.Children.Add(Text(L.Text("SINGLE_IMPORT_HINT"), 13, Muted));
        var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 190, MaxHeight = 270, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FlowDirection = FlowDirection.LeftToRight, AllowDrop = true };
        link.Children.Add(OutlinedField("LINK_TEXT",input));
        var error = Text("", 13, Red); var summary = Text("", 13, Muted);
        string reviewedText="";CompatibleImport? reviewed=null;
        input.TextChanged+=(_,_)=>{reviewed=null;reviewedText="";summary.Text="";error.Text="";};
        var experimental = new CheckBox { Content = L.Text("INCLUDE_EXPERIMENTAL"), IsChecked = data.IncludeExperimental }; link.Children.Add(SettingsRow(experimental)); link.Children.Add(summary); link.Children.Add(error);
        experimental.Checked+=(_,_)=>reviewed=null;experimental.Unchecked+=(_,_)=>reviewed=null;
        bool Install(CompatibleImport batch)
        {
            try
            {
                summary.Text = ImportSummary(batch);
                if (batch.Profiles.Count == 0) { error.Text = L.Text("NO_SUPPORTED_IMPORT"); return false; }
                var prior = data.Profiles.ToList(); int added = 0, duplicates = 0;
                foreach (var p in batch.Profiles) { if (data.Profiles.Any(x => x.Raw == p.Raw)) { duplicates++; continue; } data.Profiles.Add(p); added++; }
                bool previous = data.IncludeExperimental; data.IncludeExperimental = experimental.IsChecked == true;
                try { Save(); } catch { data.Profiles = prior; data.IncludeExperimental = previous; throw; }
                BuildCards(); Notify(L.Text("IMPORT_ADDED") + " " + added + " · " + L.Text("IMPORT_DUPLICATES") + " " + duplicates + " · " + ImportSummary(batch)); d.DialogResult = true; return true;
            }
            catch (Exception e) { error.Text = L.Text(e is SafeError s ? s.Code : "INVALID_IMPORT"); return false; }
        }
        bool Apply(string text){
            try{return Install(Importer.ReadCompatible(text,experimental.IsChecked==true));}
            catch(Exception e){error.Text=L.Text(e is SafeError se?se.Code:"INVALID_IMPORT");return false;}
        }
        bool importing=false;
        async Task ImportText(){
            if(importing)return;
            string text=input.Text.Trim();
            if(!BundleCodec.IsBundle(text)){
                try{if(reviewed==null||reviewedText!=text){reviewed=Importer.ReadCompatible(text,experimental.IsChecked==true);reviewedText=text;summary.Text=ImportSummary(reviewed)+"\n"+L.Text("IMPORT_REVIEW_CONFIRM");error.Text=reviewed.Profiles.Count==0?L.Text("NO_SUPPORTED_IMPORT"):"";return;}Install(reviewed);}
                catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"INVALID_IMPORT");}return;
            }
            string? password=BundlePassword(d,false);if(password==null)return;
            importing=true;tabs.IsEnabled=false;bool experimentalAllowed=experimental.IsChecked==true;
            try{var batch=await Task.Run(()=>BundleCodec.Open(text,password,experimentalAllowed));if(d.IsVisible)Install(batch);}
            catch(Exception e){error.Text=L.Text(e is SafeError se?se.Code:"BUNDLE_PASSWORD_ERROR");}
            finally{importing=false;tabs.IsEnabled=true;}
        }
        void ReadFile(string file)
        {
            try { if (new FileInfo(file).Length > Importer.MaxBytes) throw new SafeError("IMPORT_TOO_LARGE"); input.Text = File.ReadAllText(file, new UTF8Encoding(false, true)); tabs.SelectedIndex = 0; }
            catch (Exception e) { error.Text = L.Text(e is SafeError s ? s.Code : "INVALID_IMPORT"); }
        }
        var actions = new WrapPanel(); actions.Children.Add(Button("PASTE", () => { if (Clipboard.ContainsText()) input.Text = Clipboard.GetText(); })); actions.Children.Add(Button("CHOOSE_FILE", () => { if(chooseImportFile!=null){if(chooseImportFile(d) is {} picked)ReadFile(picked);return;}var file = new OpenFileDialog { Title = L.Text("CHOOSE_FILE"), Filter = "TXT / JSON / CONF|*.txt;*.json;*.conf|*.*|*.*" }; if (file.ShowDialog(d.NativeOwner) == true) ReadFile(file.FileName); })); actions.Children.Add(Button("IMPORT", () => _=ImportText(), true)); link.Children.Add(actions);
        input.PreviewDragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }; input.PreviewDrop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files) ReadFile(files[0]); e.Handled = true; };
        tabs.Items.Add(new TabItem { Header = L.Text("LINK_TEXT"), Content = link });
        var qr = new StackPanel(); qr.Children.Add(Text(L.Text("QR_IMPORT_HINT"), 13, Muted)); var qrError = Text("", 13, Red); var qrActions = new WrapPanel();
        async Task Decode(BitmapSource bitmap)
        {
            qrActions.IsEnabled = false;
            try
            {
                if (bitmap.PixelWidth < 1 || bitmap.PixelHeight < 1 || (long)bitmap.PixelWidth * bitmap.PixelHeight > 16000000) throw new SafeError("QR_TOO_LARGE");
                var image = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); int stride = checked(image.PixelWidth * 4); byte[] pixels = new byte[checked(stride * image.PixelHeight)]; image.CopyPixels(pixels, stride, 0); int width = image.PixelWidth, height = image.PixelHeight;
                string? result = await Task.Run(() => QrCodeReader.Read(pixels, width, height));
                if (string.IsNullOrEmpty(result)) throw new SafeError("QR_NOT_FOUND"); input.Text = result; tabs.SelectedIndex = 0; error.Text = "";
            }
            catch (Exception e) { qrError.Text = L.Text(e is SafeError s ? s.Code : "QR_NOT_FOUND"); }
            finally { qrActions.IsEnabled = true; }
        }
        var fromFile = Button("QR_IMAGE", () => { }); fromFile.Click += async (_, _) =>
        {
            try { if(chooseQrImage!=null){if(chooseQrImage(d) is {} bitmap)await Decode(bitmap);return;}var f = new OpenFileDialog { Title = L.Text("QR_IMAGE"), Filter = "PNG / JPEG / BMP|*.png;*.jpg;*.jpeg;*.bmp" }; if (f.ShowDialog(d.NativeOwner) != true) return; if (new FileInfo(f.FileName).Length > 12 * 1024 * 1024) throw new SafeError("QR_TOO_LARGE"); using var stream = File.OpenRead(f.FileName); var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnDemand); await Decode(decoder.Frames[0]); }
            catch (Exception e) { qrError.Text = L.Text(e is SafeError s ? s.Code : "QR_NOT_FOUND"); }
        }; qrActions.Children.Add(fromFile);
        var clip = Button("QR_CLIPBOARD", () => { }); clip.Click += async (_, _) => { try { if (!Clipboard.ContainsImage()) throw new SafeError("QR_NOT_FOUND"); await Decode(Clipboard.GetImage() ?? throw new SafeError("QR_NOT_FOUND")); } catch { qrError.Text = L.Text("QR_NOT_FOUND"); } }; qrActions.Children.Add(clip); qr.Children.Add(qrActions); qr.Children.Add(qrError); tabs.Items.Add(new TabItem { Header = L.Text("QR_CODE"), Content = qr });
        var manual = new StackPanel(); var kind = new ComboBox { ItemsSource = new[] { "VLESS", "SOCKS5", "Hysteria2", "WireGuard", "SSH", "NaiveProxy", L.Text("DNS_FAMILIES") }, SelectedIndex = 0,Tag="PROTOCOL" }; var protocolFrame=OutlinedField("PROTOCOL",kind);manual.Children.Add(protocolFrame);
        var extraTypes=new WrapPanel();foreach(string protocol in new[]{"Trojan","VMess","Shadowsocks"}){var create=Button("ADD",()=>NewExtraProfile(protocol));create.Content=protocol;extraTypes.Children.Add(create);}manual.Children.Insert(0,extraTypes);
        var labels = new Dictionary<TextBox, FrameworkElement>();
        TextBox Field(string key, string initial = "") { var box = new TextBox { Text = initial, FlowDirection = FlowDirection.LeftToRight };var field=OutlinedField(key,box);manual.Children.Add(field);labels[box]=field;return box; }
        var name = Field("PROFILE_NAME");name.FlowDirection=L.Direction;var host = Field("SERVER_HOST"); var port = Field("SERVER_PORT", "443"); var identity = Field("ID_OR_USER");var password=new PasswordBox{FlowDirection=FlowDirection.LeftToRight};var passwordLabel=OutlinedField("PASSWORD",password);manual.Children.Add(passwordLabel);
        var transport = new ComboBox { ItemsSource = new[] { "ws", "tcp", "xhttp" }, SelectedIndex = 0 };var transportLabel=OutlinedField("TRANSPORT",transport);manual.Children.Add(transportLabel);
        var security = new ComboBox { ItemsSource = new[] { "tls", "reality", "none" }, SelectedIndex = 0 };var securityLabel=OutlinedField("SECURITY",security);manual.Children.Add(securityLabel);
        var sni = Field("SNI", ""); var wsHost = Field("WS_HOST"); var path = Field("WS_PATH", "/"); var publicKey = Field("REALITY_KEY"); var shortId = Field("REALITY_SHORT_ID");
        var wg = Field("WIREGUARD_TEXT"); wg.AcceptsReturn = true; wg.MinHeight = 120; wg.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        var dnsManual=DnsManualForm(["DNSTT","NoizDNS","VayDNS","Soar","Slipstream"],Apply,error,manualProtocol);manual.Children.Add(dnsManual);
        var manualError = Text("", 13, Red); manual.Children.Add(manualError);
        var standardImport=Button("IMPORT", () =>
        {
            try
            {
                string Esc(string text) => Uri.EscapeDataString(text.Trim()); string server = host.Text.Contains(':') ? "[" + host.Text.Trim('[', ']') + "]" : host.Text.Trim(); _ = kind.SelectedIndex == 3 ? 1 : Importer.Port(port.Text);
                string raw = kind.SelectedIndex switch
                {
                    1 => "socks5://" + (identity.Text.Length > 0 ? Esc(identity.Text) + ":" + Uri.EscapeDataString(password.Password) + "@" : "") + server + ":" + port.Text + "#" + Esc(name.Text),
                    2 => "hysteria2://" + Uri.EscapeDataString(password.Password) + "@" + server + ":" + port.Text + "?sni=" + Esc(sni.Text) + "#" + Esc(name.Text),
                    3 => wg.Text,
                    4 => ManualProfiles.Native("ssh", name.Text, host.Text, int.Parse(port.Text), identity.Text, password.Password),
                    5 => ManualProfiles.Native("naive", name.Text, host.Text, int.Parse(port.Text), identity.Text, password.Password),
                    _ => "vless://" + Esc(identity.Text) + "@" + server + ":" + port.Text + "?type=" + transport.SelectedItem + "&security=" + security.SelectedItem + "&sni=" + Esc(sni.Text) + "&host=" + Esc(wsHost.Text) + "&path=" + Esc(path.Text) + (security.SelectedItem?.ToString() == "reality" ? "&fp=chrome&pbk=" + Esc(publicKey.Text) + "&sid=" + Esc(shortId.Text) : "") + "#" + Esc(name.Text)
                };
                if (!Apply(raw)) manualError.Text = error.Text;
            }
            catch (Exception e) { manualError.Text = L.Text(e is SafeError s ? s.Code : "INVALID_IMPORT"); }
        }, true);manual.Children.Add(standardImport); if(manualProtocol!=null||tab==2)tabs.Items.Add(new TabItem { Header = L.Text("MANUAL_ADD"), Content = manual });
        void Fields()
        {
            bool wire = kind.SelectedIndex == 3, vless = kind.SelectedIndex == 0, socks = kind.SelectedIndex is 1 or 4 or 5, hy = kind.SelectedIndex == 2;
            bool isDns=kind.SelectedIndex==6;dnsManual.Visibility=isDns?Visibility.Visible:Visibility.Collapsed;standardImport.Visibility=manualError.Visibility=isDns?Visibility.Collapsed:Visibility.Visible;password.Visibility=passwordLabel.Visibility=!isDns && (socks || hy)?Visibility.Visible:Visibility.Collapsed;
            foreach (var (box, label) in labels) { bool visible = box == wg ? wire : !wire && (box == name || box == host || box == port || box == identity && (vless || socks) || box == sni && (vless || hy) || vless && (box == wsHost || box == path || box == publicKey || box == shortId)); box.Visibility = label.Visibility = visible && !isDns ? Visibility.Visible : Visibility.Collapsed; }
            transportLabel.Visibility = securityLabel.Visibility = transport.Visibility = security.Visibility = vless ? Visibility.Visible : Visibility.Collapsed;
        }
        string previousDefaultPort="443";
        kind.SelectionChanged += (_, _) => { string nextDefault=kind.SelectedIndex==4?"22":kind.SelectedIndex==1?"1080":"443";if(port.Text==previousDefaultPort)port.Text=nextDefault;previousDefaultPort=nextDefault;Fields();if(tab==2)d.SetPageTitle(L.Text("ADD")+" · "+(kind.SelectedIndex==6?manualProtocol??L.Text("DNS_FAMILIES"):kind.SelectedItem)); }; security.SelectionChanged += (_, _) => { if (security.SelectedItem?.ToString() == "reality") transport.SelectedItem = "tcp"; }; Fields();
        input.PreviewKeyDown+=(_,key)=>{if(key.Key==System.Windows.Input.Key.Enter && System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control){key.Handled=true;_=ImportText();}};
        if(manualProtocol!=null){if(manualProtocol is "REALITY" or "XHTTP" or "VLESS WS"){kind.SelectedItem="VLESS";if(manualProtocol=="REALITY"){transport.SelectedItem="tcp";security.SelectedItem="reality";}else if(manualProtocol=="XHTTP")transport.SelectedItem="xhttp";}else if(kind.Items.Contains(manualProtocol))kind.SelectedItem=manualProtocol;else kind.SelectedIndex=6;}
        tabs.SelectedIndex = tab;
        if(tab==2){
            ((TabItem)tabs.Items[2]).Content=null;manual.Children.Remove(extraTypes);manual.Margin=new(18);
            if(kind.SelectedIndex==6)protocolFrame.Visibility=Visibility.Collapsed;
            d.SetPageTitle(L.Text("ADD")+" · "+(manualProtocol??kind.SelectedItem?.ToString()));d.Content=manual;
        }else d.Content=tabs;
        return d;
    }
    StackPanel DnsManualForm(string[] types, Func<string,bool> apply, TextBlock importError,string? initial=null)
    {
        var panel=new StackPanel();var names=new Dictionary<string,string>{["DNSTT"]="dnstt",["NoizDNS"]="sayedns",["VayDNS"]="vaydns",["Soar"]="soar",["Slipstream"]="slipstream"};
        var fields=new Dictionary<TextBox,FrameworkElement>();
        TextBox Field(string label,string initial="",bool multi=false)
        {
            var box=new TextBox{Text=initial,FlowDirection=FlowDirection.LeftToRight,AcceptsReturn=multi,MinHeight=multi?80:48};var field=OutlinedField(label,box);fields[box]=field;panel.Children.Add(field);return box;
        }
        var family=new ComboBox{ItemsSource=types,SelectedItem=types.Contains(initial)?initial:types[0],MinHeight=48};panel.Children.Add(OutlinedField("PROTOCOL",family));
        var name=Field("PROFILE_NAME");name.FlowDirection=L.Direction;
        var zone=Field("TUNNEL_DOMAIN");var resolvers=Field("DNS_RESOLVERS","",true);var key=Field("SERVER_PUBLIC_KEY");
        var method=new ComboBox{MinHeight=48};panel.Children.Add(OutlinedField("TRANSPORT",method));
        LabelDnsTransport20(method);
        var local=Button("DNS_LOCAL_NETWORK",()=>{try{var values=NetworkResolvers20();if(values.Length==0){Info("DNS_LOCAL_NETWORK",L.Text("DNS_LOCAL_UNAVAILABLE"));return;}resolvers.Text=string.Join('\n',values);}catch{Info("DNS_LOCAL_NETWORK",L.Text("DNS_LOCAL_UNAVAILABLE"));}});local.Tag="DNS_LOCAL_NETWORK";local.ToolTip=L.Text("DNS_LOCAL_HINT");panel.Children.Add(local);
        var ssh=new CheckBox{Content=L.Text("SCAN_USE_SSH"),Margin=new(4)};panel.Children.Add(SettingsRow(ssh));
        var host=Field("SSH_HOST");var port=Field("SERVER_PORT","22");var user=Field("ID_OR_USER");
        var pass=new PasswordBox{MinHeight=48,FlowDirection=FlowDirection.LeftToRight};var passLabel=OutlinedField("PASSWORD",pass);panel.Children.Add(passLabel);
        var info=Text("",12,Red);panel.Children.Add(info);
        void Show(TextBox box,bool on){box.Visibility=fields[box].Visibility=on?Visibility.Visible:Visibility.Collapsed;}
        void Options(bool changed)
        {
            string protocol=family.SelectedItem as string??types[0];
            if(changed){method.ItemsSource=protocol switch{"Slipstream"=>new[]{"udp"},"Soar"=>new[]{"udp","tcp"},_=>new[]{"udp","tcp","tls","https"}};method.SelectedIndex=0;}
            Show(key,protocol!="Slipstream");Show(host,ssh.IsChecked==true);Show(port,ssh.IsChecked==true);
            bool credentials=ssh.IsChecked==true || protocol!="Soar";Show(user,credentials);pass.Visibility=passLabel.Visibility=credentials?Visibility.Visible:Visibility.Collapsed;
        }
        family.SelectionChanged+=(_,_)=>Options(true);ssh.Checked+=(_,_)=>Options(false);ssh.Unchecked+=(_,_)=>Options(false);Options(true);
        panel.Children.Add(Button("IMPORT",()=>{
            try{
                string kind=names[family.SelectedItem as string??types[0]]+(ssh.IsChecked==true?"_ssh":"");
                var raw=ManualProfiles.Dns(kind,name.Text,zone.Text.Trim(),resolvers.Text,(string)method.SelectedItem,key.Text.Trim(),
                    host.Text.Trim(),port.Text,user.Visibility==Visibility.Visible?user.Text:"",pass.Visibility==Visibility.Visible?pass.Password:"");
                if(!apply(raw)) info.Text=importError.Text;
            }catch(Exception e){info.Text=L.Text(e is SafeError se?se.Code:"INVALID_IMPORT");}
        },true));return panel;
    }
    string ImportSummary(CompatibleImport batch)
    {
        string text=L.Text("IMPORT_ACCEPTED")+" "+batch.Profiles.Count+" / "+batch.Total;
        if(batch.Skipped.Count>0)text+="\n"+L.Text("IMPORT_SKIPPED")+" "+batch.Skipped.Values.Sum()+" · "+string.Join(" · ",batch.Skipped.Select(x=>L.Text(x.Key)+": "+x.Value));
        if(batch.Profiles.Count>0)text+="\n"+string.Join(" · ",batch.Profiles.GroupBy(p=>p.Protocol).OrderByDescending(g=>g.Count()).Select(g=>g.Key+": "+g.Count()));
        foreach(string notice in batch.Notices)text+="\n"+L.Text(notice);return text;
    }
}

