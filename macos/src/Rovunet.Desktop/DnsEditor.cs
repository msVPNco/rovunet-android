using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static string DnsTransportLabel(string wire)=>wire switch{"tls"=>"DoT","https"=>"DoH","both"=>"UDP + TCP",_=>wire.ToUpperInvariant()};
    static void LabelDnsTransport(ComboBox choice)=>choice.ItemTemplate=new FuncDataTemplate<string>((value,_)=>new TextBlock{Text=DnsTransportLabel(value??""),FlowDirection=Avalonia.Media.FlowDirection.LeftToRight});
    void ShowDnsForm(Profile original,bool isNew)
    {
        var source=Importer.Fields(original.Raw);var f=TransportPlan.Normalize(source);string family=ResolverScan.Family(f);var page=Page();
        StackPanel Section(string title){var box=new StackPanel{Spacing=8};box.Children.Add(Label(L.Text(title),15,"Accent"));page.Children.Add(Card(box));return box;}
        var main=Section("MAC_DNS_CONNECTION");main.Children.Add(Label(original.Protocol,20));var name=Field(main,"PROFILE_NAME",original.Name);name.FlowDirection=L.Direction;
        var zone=Field(main,"TUNNEL_DOMAIN",isNew?"":f[3]);zone.Name="DnsZone";
        var mode=Choice(main,"TRANSPORT",family=="slipstream"?["udp"]:family=="soar"?["udp","tcp"]:["udp","tcp","tls","https"],f[22]);LabelDnsTransport(mode);mode.Name="DnsTransport";
        if(family=="slipstream")(mode.Parent as Control)!.IsVisible=false;
        var addresses=Field(main,"DNS_RESOLVERS",isNew?"":MacDnsEditing.DisplayResolvers(f[4]),true);addresses.Name="DnsAddresses";
        var doh=Field(main,"DOH_URL",f[21]);doh.Name="DnsDoh";
        var tools=new WrapPanel();main.Children.Add(tools);
        var local=ActionButton("DNS_LOCAL_NETWORK",()=>{string[] values=MacDnsEditing.NetworkResolvers();if(values.Length==0)throw new SafeError("DNS_LOCAL_UNAVAILABLE");addresses.Text=string.Join('\n',values);});tools.Children.Add(local);
        if(family!="slipstream"){var key=Field(main,"SERVER_PUBLIC_KEY",isNew?"":f[11]);key.Name="DnsPublicKey";}
        var edits=new Dictionary<int,Func<string>>{{2,()=>name.Text?.Trim()??""},{3,()=>zone.Text?.Trim().TrimEnd('.')??""}};
        if(family!="slipstream"){var key=main.Children.OfType<StackPanel>().SelectMany(p=>p.Children.OfType<TextBox>()).Single(b=>b.Name=="DnsPublicKey");edits[11]=()=>key.Text?.Trim()??"";}
        var advanced=new StackPanel{Spacing=8};page.Children.Add(new Expander{Header=L.Text("SCAN_OPTIONS"),Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch});
        void Text(int i,string label,bool multiline=false){if(i>=source.Length)return;var b=Field(family=="slipstream"&&i==6?main:advanced,label,f[i],multiline);b.Name="DnsField"+i;edits[i]=()=>multiline?string.Join(',',(b.Text??"").Split(['\r','\n',','],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)):b.Text?.Trim()??"";}
        ComboBox? Select(int i,string label,params string[] options){if(i>=source.Length)return null;var b=Choice(family=="slipstream"&&i==7?main:advanced,label,options,f[i]);b.Name="DnsField"+i;edits[i]=()=>b.SelectedItem?.ToString()??f[i];return b;}
        void Toggle(int i,string label){if(i>=source.Length)return;var b=new CheckBox{Content=L.Text(label),IsChecked=f[i]=="1",MinHeight=40,Name="DnsField"+i};advanced.Children.Add(b);edits[i]=()=>b.IsChecked==true?"1":"0";}
        if(family=="slipstream"){Text(6,"KEEPALIVE_MS");Select(7,"CONGESTION","bbr","dcubic");Toggle(5,"AUTHORITATIVE");}
        ComboBox? spread=null,discoverySource=null,policy=null;
        if(family is "dnstt" or "sayedns"){Toggle(27,"AUTHORITATIVE");Text(39,"DNS_PAYLOAD");spread=Select(60,"RESOLVER_POLICY","fanout","roundrobin");Text(61,"RESOLVER_SPREAD");if(family=="sayedns")Toggle(38,"STEALTH_MODE");}
        if(family=="vaydns"){Toggle(41,"VAY_COMPAT");Select(42,"RECORD_TYPE","txt","cname","a","aaaa","mx","ns","srv","null","caa");foreach(var (i,k) in new[]{(43,"VAY_QNAME"),(44,"VAY_RPS"),(45,"VAY_IDLE"),(46,"VAY_KEEP"),(47,"VAY_TIMEOUT"),(48,"VAY_LABELS"),(49,"VAY_CLIENT_ID")})Text(i,k);}
        if(family=="soar"){Toggle(79,"SOAR_DISCOVERY");policy=Select(81,"SOAR_TCP","auto","on","only","off");discoverySource=Select(82,"SCAN_SOURCE","soar","app","custom");Text(80,"COUNTRY");Text(83,"DNS_RESOLVERS",true);}
        Control? FieldGroup(int n)=>advanced.Children.FirstOrDefault(c=>c is Panel group&&group.Children.Any(child=>child.Name=="DnsField"+n));
        var compat=advanced.Children.OfType<CheckBox>().FirstOrDefault(c=>c.Name=="DnsField41");
        var vayId=(FieldGroup(49) as Panel)?.Children.OfType<TextBox>().FirstOrDefault();
        string manualId=vayId?.Text??"";bool forcedId=false;
        if(vayId!=null)edits[49]=()=>forcedId?manualId:vayId.Text?.Trim()??"";
        void FamilyOptions(){
            if(vayId!=null){
                bool forced=compat?.IsChecked==true;
                if(forced&&!forcedId){manualId=vayId.Text?.Trim()??"";vayId.Text="8";}
                else if(!forced&&forcedId)vayId.Text=manualId;
                forcedId=forced;vayId.IsEnabled=!forced;
                ToolTip.SetTip(vayId,forced?L.Text("MAC_VAY_FIXED_ID"):null);
            }
            if(FieldGroup(47) is {} udpTimeout)udpTimeout.IsVisible=mode.SelectedItem as string=="udp";
            if(FieldGroup(61) is {} rr)rr.IsVisible=spread?.SelectedItem as string=="roundrobin";
            if(family!="soar")return;bool discovery=advanced.Children.OfType<CheckBox>().FirstOrDefault(c=>c.Name=="DnsField79")?.IsChecked==true;
            if(FieldGroup(82) is {} sourceBox)sourceBox.IsVisible=discovery;
            if(FieldGroup(80) is {} country)country.IsVisible=discovery&&discoverySource?.SelectedItem as string=="soar";
            if(FieldGroup(83) is {} custom)custom.IsVisible=discovery&&discoverySource?.SelectedItem as string=="custom";
            if(policy!=null)policy.IsEnabled=mode.SelectedItem as string!="tcp";
        }
        if(compat!=null)compat.IsCheckedChanged+=(_,_)=>FamilyOptions();
        if(spread!=null)spread.SelectionChanged+=(_,_)=>FamilyOptions();if(discoverySource!=null)discoverySource.SelectionChanged+=(_,_)=>FamilyOptions();
        if(advanced.Children.OfType<CheckBox>().FirstOrDefault(c=>c.Name=="DnsField79") is {} discoveryToggle)discoveryToggle.IsCheckedChanged+=(_,_)=>FamilyOptions();FamilyOptions();
        bool hasSsh=f[1].EndsWith("_ssh",StringComparison.Ordinal);var credentials=Section("MAC_DNS_CREDENTIALS");var ssh=new CheckBox{Content=L.Text("MAC_DNS_USE_SSH"),IsChecked=hasSsh,IsEnabled=isNew,MinHeight=40};credentials.Children.Add(ssh);
        var sshFields=new StackPanel{Spacing=8};credentials.Children.Add(sshFields);var sshHost=Field(sshFields,"SERVER_HOST",isNew?"127.0.0.1":f[19]);var sshPort=Field(sshFields,"SERVER_PORT",isNew?"22":f[17]);
        var sshUser=Field(sshFields,"USERNAME",f[15]);var sshPass=Field(sshFields,"PASSWORD",isNew?"":f[16],secret:true);sshPass.IsEnabled=isNew||f[23]=="password";
        var socksFields=new StackPanel{Spacing=8};credentials.Children.Add(socksFields);socksFields.Children.Add(Label(L.Text("MAC_DNS_SOCKS_AUTH"),12,"Muted"));var socksUser=Field(socksFields,"USERNAME",f[12]);var socksPass=Field(socksFields,"PASSWORD",f[13],secret:true);
        if(!isNew&&f[0]!="33"){credentials.IsEnabled=false;credentials.Children.Add(Label(L.Text("MAC_DNS_LEGACY_CREDENTIALS"),12,"Muted"));}
        void Refresh(){bool https=mode.SelectedItem?.ToString()=="https";(addresses.Parent as Control)!.IsVisible=!https;(doh.Parent as Control)!.IsVisible=https;local.IsVisible=!https;sshFields.IsVisible=ssh.IsChecked==true;socksFields.IsVisible=ssh.IsChecked!=true;}
        mode.SelectionChanged+=(_,_)=>{if(policy!=null&&mode.SelectedItem?.ToString()=="tcp")policy.SelectedItem="only";FamilyOptions();Refresh();};ssh.IsCheckedChanged+=(_,_)=>Refresh();Refresh();
        Profile Draft(bool forScan=false){
            string selected=mode.SelectedItem?.ToString()??f[22];var changes=edits.ToDictionary(p=>p.Key,p=>p.Value());
            if(changes[2].Length==0)throw new SafeError("INVALID_IMPORT");
            string resolverText=addresses.Text??"",httpsText=doh.Text??"";
            if(forScan){try{if(selected=="https")_=SubscriptionUrls.Validate(httpsText);else _=MacDnsEditing.NormalizeResolvers(resolverText,selected,f[4]);}catch(SafeError){resolverText=MacDnsEditing.DisplayResolvers(f[4]);httpsText=f[21];}}
            var current=original;
            if(isNew)current=MacProtocols.Single(ManualProfiles.Dns(family+(ssh.IsChecked==true?"_ssh":""),changes[2],changes[3],selected=="https"?httpsText:resolverText,selected,changes.GetValueOrDefault(11,""),sshHost.Text??"",sshPort.Text??"22",ssh.IsChecked==true?sshUser.Text??"":socksUser.Text??"",ssh.IsChecked==true?sshPass.Text??"":socksPass.Text??""));
            if(ssh.IsChecked==true){changes[19]=sshHost.Text??"";changes[17]=sshPort.Text??"22";}
            DnsCredentials? auth=f[0]=="33"?new(sshUser.Text??"",sshPass.Text??"",socksUser.Text??"",socksPass.Text??""):null;
            var draft=MacDnsEditing.Apply(current,changes,resolverText,selected,httpsText,auth);draft.Id=original.Id;return draft;
        }
        tools.Children.Add(ActionButton("FIND_DNS",()=>ShowScannerForEditor(Draft(true),applied=>{
            RequireIdle();
            if(!isNew){var stored=data.Profiles.SingleOrDefault(p=>p.Id==original.Id);if(stored==null||stored.Raw!=original.Raw)throw new SafeError("MAC_SCAN_PROFILE_CHANGED");SaveEdited(original,applied);original=applied;}
            var selected=TransportPlan.Normalize(Importer.Fields(applied.Raw));f=selected;addresses.Text=MacDnsEditing.DisplayResolvers(selected[4]);doh.Text=selected[21];mode.SelectedItem=selected[22];if(policy!=null)policy.SelectedItem=selected[81];
            Back();message=L.Text(isNew?"MAC_SCAN_APPLIED_DRAFT":"MAC_SCAN_APPLIED");UpdateStatus();
        })));
        if(family=="slipstream"){
            tools.Children.Add(ActionButton("MAC_SLIP_REVIEW",()=>_=ReviewSlipIdentity(Draft(true))));
            advanced.Children.Add(ActionButton("MAC_SLIP_IMPORT",()=>_=ChooseCertificate(Draft(true))));
        }
        page.Children.Add(ActionButton("SAVE",()=>{RequireIdle();var next=Draft();if(isNew){next.Id=original.Id;Commit(d=>d.Profiles.Add(next));}else SaveEdited(original,next);GoHome();},true));
        page.Children.Add(ActionButton("ADVANCED_RAW",()=>ShowRawEditor(Draft(),isNew)));Navigate(isNew?"ADD_MANUAL":"EDIT_PROFILE",page);
    }
}
