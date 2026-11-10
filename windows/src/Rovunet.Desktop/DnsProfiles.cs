using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    void SaveEdited(Profile original, string raw,string? displayName=null,Action? applyTrust=null)
    {
        var next = ProfileEditing.Replace(original, raw);if(displayName!=null)next.Name=displayName;int index = data.Profiles.FindIndex(x => x.Id == original.Id); if (index < 0) throw new SafeError("INVALID_IMPORT");
        string? oldOverride = data.LocalOverrideSources.GetValueOrDefault(original.Id);string? oldName=data.LocalOverrideNames.GetValueOrDefault(original.Id);var oldMeasurement = data.Measurements.GetValueOrDefault(original.Id); data.Measurements.Remove(original.Id); data.Profiles[index] = next;
        if (original.SourceId.Length > 0 && oldOverride == null){data.LocalOverrideSources[original.Id] = original.Raw;data.LocalOverrideNames[original.Id]=original.Name;}
        var keys=new Dictionary<string,string>(data.SshKeys);var scopes=new Dictionary<string,string>(data.SshKeyScopes);var certificates=new Dictionary<string,string>(data.SlipCertificates);
        var tlsCertificates=new Dictionary<string,string>(data.SshTlsCertificates);
        try { if(SshHostKey.Required(original)&&SshHostKey.Scope(original)!=SshHostKey.Scope(next)){data.SshKeys.Remove(original.Id);data.SshKeyScopes.Remove(original.Id);}applyTrust?.Invoke();Save(); } catch { data.SshKeys=keys;data.SshKeyScopes=scopes;data.SlipCertificates=certificates;data.SshTlsCertificates=tlsCertificates;data.Profiles[index] = original; if (oldOverride == null) data.LocalOverrideSources.Remove(original.Id);if(oldName==null)data.LocalOverrideNames.Remove(original.Id);else data.LocalOverrideNames[original.Id]=oldName;if (oldMeasurement != null) data.Measurements[original.Id] = oldMeasurement; throw; }
        measurements.Remove(original.Id); data.Measurements.Remove(original.Id); BuildCards(); Notify(L.Text(original.SourceId.Length > 0 ? "LOCAL_OVERRIDE_SAVED" : "SAVE"));
    }
    void DnsProfileEditor(Profile p) => BuildDnsProfileEditor(p).ShowDialog();
    DialogWindow BuildDnsProfileEditor(Profile p)
    {
        var d=Dialog("EDIT_PROFILE",500,800);d.UsePageChrome();
        var content=new StackPanel{Margin=new(18,8,18,20)};var textFrames=new Dictionary<TextBox,Border>();var choiceFrames=new Dictionary<ComboBox,Border>();
        string[] wireFields=Importer.Fields(p.Raw);
        string[] f=TransportPlan.Normalize(wireFields);
        string family=ResolverScan.Family(f);
        if(p.SourceId.Length>0)content.Children.Add(Text(L.Text("LOCAL_OVERRIDE_HINT"),12,Muted));
        TextBox Field(Panel parent,string label,string value,bool multi=false)
        {
            var box=new TextBox{Text=value,FlowDirection=FlowDirection.LeftToRight,AcceptsReturn=multi,MinHeight=multi?76:52,MaxHeight=multi?150:double.PositiveInfinity,VerticalScrollBarVisibility=multi?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden};
            var frame=OutlinedField(label,box);parent.Children.Add(frame);textFrames[box]=frame;return box;
        }
        ComboBox Choice(Panel parent,string label,string value,string[] values)
        {
            var select=new ComboBox{ItemsSource=values,SelectedItem=value};var frame=OutlinedField(label,select);choiceFrames[select]=frame;parent.Children.Add(frame);return select;
        }        var name=Field(content,"PROFILE_NAME",p.Name);name.FlowDirection=L.Direction;
        var domain=Field(content,"TUNNEL_DOMAIN",f[3]);
        var transport=Choice(content,"TRANSPORT",f[22],family switch{"slipstream"=>["udp"],"soar"=>["udp","tcp"],_=>["udp","tcp","tls","https"]});
        LabelDnsTransport20(transport);
        if(family=="slipstream")choiceFrames[transport].Visibility=Visibility.Collapsed;
        string resolverWire=f[4];
        var addresses=Field(content,"DNS_RESOLVERS",string.Join('\n',f[4].Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>TransportPlan.SlipResolver(x).Address)),true);
        var doh=Field(content,"DOH_URL",f[21]);var dohLabel=textFrames[doh];
        var addressLabel=textFrames[addresses];
        var local=Button("DNS_LOCAL_NETWORK",()=>{try{var values=NetworkResolvers20();if(values.Length==0){Info("DNS_LOCAL_NETWORK",L.Text("DNS_LOCAL_UNAVAILABLE"));return;}addresses.Text=string.Join('\n',values);}catch{Info("DNS_LOCAL_NETWORK",L.Text("DNS_LOCAL_UNAVAILABLE"));}});local.Tag="DNS_LOCAL_NETWORK";local.ToolTip=L.Text("DNS_LOCAL_HINT");content.Children.Add(local);
        void WireChanged(){bool https=transport.SelectedItem?.ToString()=="https";dohLabel.Visibility=https?Visibility.Visible:Visibility.Collapsed;addressLabel.Visibility=local.Visibility=https?Visibility.Collapsed:Visibility.Visible;}
        transport.SelectionChanged+=(_,_)=>WireChanged();WireChanged();
        var options=new StackPanel();var advanced=new Expander{Header=L.Text("SCAN_OPTIONS"),Margin=new(0,8,0,6),Content=options};
        var edits=new Dictionary<int,Func<string>>();
        void BindText(int i,string label,bool multiline=false)
        {
            if(i>=wireFields.Length)return;
            var field=Field(i==11 || family=="slipstream" && i==6?content:options,label,f[i],multiline);
            if(i==6)field.Tag="SLIP_INTERVAL";
            edits[i]=()=>multiline?string.Join(',',field.Text.Split(['\n','\r',',',' '],StringSplitOptions.RemoveEmptyEntries)):field.Text.Trim();
        }
        ComboBox? BindChoice(int i,string label,params string[] choices)
        {
            if(i>=wireFields.Length)return null;
            if(choices.SequenceEqual(new[]{"0","1"})){var toggle=new CheckBox{Content=L.Text(label),IsChecked=f[i]=="1"};options.Children.Add(SettingsRow(toggle));edits[i]=()=>toggle.IsChecked==true?"1":"0";return null;}
            var combo=Choice(family=="slipstream" && i==7?content:options,label,f[i],choices);edits[i]=()=>combo.SelectedItem?.ToString()??f[i];return combo;
        }
        if(family=="slipstream"){BindText(6,"KEEPALIVE_MS");BindChoice(7,"CONGESTION","bbr","dcubic");}
        else BindText(11,"SERVER_PUBLIC_KEY");
        ComboBox? policy=null;
        if(family=="soar"){
            policy=BindChoice(81,"SOAR_TCP","auto","on","only","off");BindChoice(79,"SOAR_DISCOVERY","0","1");BindChoice(82,"SCAN_SOURCE","soar","app","custom");BindText(80,"COUNTRY");BindText(83,"DNS_RESOLVERS",true);
        }
        if(family is "sayedns" or "dnstt"){
            BindChoice(27,"AUTHORITATIVE","0","1");BindText(39,"DNS_PAYLOAD");BindChoice(60,"RESOLVER_POLICY","fanout","roundrobin");BindText(61,"RESOLVER_SPREAD");
            if(family=="sayedns")BindChoice(38,"STEALTH_MODE","0","1");
        }
        if(family=="vaydns"){BindChoice(41,"VAY_COMPAT","0","1");BindChoice(42,"RECORD_TYPE","txt","cname","a","aaaa","mx","ns","srv","null","caa");BindText(43,"VAY_QNAME");BindText(44,"VAY_RPS");BindText(45,"VAY_IDLE");BindText(46,"VAY_KEEP");BindText(47,"VAY_TIMEOUT");BindText(48,"VAY_LABELS");BindText(49,"VAY_CLIENT_ID");}
        var trust=AddTrustEditor(content,p);content.Children.Add(advanced);
        if(family=="slipstream"){var authoritative=new CheckBox{Content=L.Text("AUTHORITATIVE"),IsChecked=f[5]=="1"};content.Children.Add(SettingsRow(authoritative));edits[5]=()=>authoritative.IsChecked==true?"1":"0";content.Children.Add(SettingsRow(new CheckBox{Content=L.Text("SLIP_GSO"),IsChecked=f[10]=="1",IsEnabled=false},"WINDOWS_UNAVAILABLE"));}
        Profile Draft()
        {
            var changes=edits.Where(x=>x.Value()!=f[x.Key]).ToDictionary(x=>x.Key,x=>x.Value());
            changes[2]=name.Text.Trim();changes[3]=domain.Text.Trim();changes[22]=(string)transport.SelectedItem;
            if(changes[22]=="https")changes[21]=SubscriptionUrls.Validate(doh.Text).AbsoluteUri;
            else if(addresses.Text.Trim().Length>0)changes[4]=NormalizeResolvers(addresses.Text,changes[22],resolverWire);
            else changes[4]="";
            var draft=ProfileEditing.Replace(p,ProfileEditing.ChangeNative(p,changes));
            if(!draft.CanConnect)throw new SafeError(draft.BlockReason);return draft;
        }
        var error=Text("",13,Red);var actions=new WrapPanel();content.Children.Add(actions);content.Children.Add(error);
        var scan=Button("FIND_DNS",()=>{
            try{
                ResolverFinder(Draft(),chosen=>{
                    var next=TransportPlan.Normalize(Importer.Fields(chosen.Raw));
                    transport.SelectedItem=next[22];resolverWire=next[4];addresses.Text=string.Join('\n',next[4].Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>TransportPlan.SlipResolver(x).Address));doh.Text=next[21];if(policy!=null)policy.SelectedItem=next[81];
                });
            }catch(Exception ex){error.Text=L.Text(ex is SafeError se?se.Code:"INVALID_IMPORT");}
        },true);content.Children.Insert(Math.Max(content.Children.IndexOf(addressLabel),content.Children.IndexOf(dohLabel))+1,scan);
        var save=Button("SAVE",()=>{
            try{var draft=Draft();SaveEdited(p,draft.Raw,applyTrust:trust.Prepare(draft));d.DialogResult=true;}
            catch(Exception ex){error.Text=L.Text(ex is SafeError se?se.Code:"INVALID_IMPORT");}
        },true);save.IsDefault=true;d.SetPageAction(save);
        if(data.LocalOverrideSources.ContainsKey(p.Id))content.Children.Add(Button("RESET_LOCAL_EDIT",()=>ResetLocalOverride(p,d)));
        content.Children.Add(Button("ADVANCED_RAW",()=>{try{var advanced=BuildRawEditor(p,Draft().Raw,raw=>{var next=ProfileEditing.Replace(p,raw);SaveEdited(p,raw,applyTrust:trust.Prepare(next));});advanced.Owner=d;if(advanced.ShowDialog()==true)d.DialogResult=true;}catch(Exception ex){error.Text=L.Text(ex is SafeError se?se.Code:"INVALID_IMPORT");}}));d.Content=content;return d;
    }
    void ResetLocalOverride(Profile p,DialogWindow dialog)
    {
        if(!data.LocalOverrideSources.TryGetValue(p.Id,out var original)||!Confirm(L.Text("RESET_LOCAL_EDIT_CONFIRM")))return;
        string? originalName=data.LocalOverrideNames.GetValueOrDefault(p.Id);var next=ProfileEditing.Replace(p,original);if(originalName!=null)next.Name=originalName;int index=data.Profiles.IndexOf(p);data.Profiles[index]=next;data.LocalOverrideSources.Remove(p.Id);data.LocalOverrideNames.Remove(p.Id);
        var prior=data.Measurements.GetValueOrDefault(p.Id);data.Measurements.Remove(p.Id);
        try{Save();}catch{data.Profiles[index]=p;data.LocalOverrideSources[p.Id]=original;if(originalName!=null)data.LocalOverrideNames[p.Id]=originalName;if(prior!=null)data.Measurements[p.Id]=prior;throw;}
        measurements.Remove(p.Id);BuildCards();dialog.DialogResult=true;
    }
    static string NormalizeResolvers(string text,string transport,string? original=null)
    {
        var entries=text.Split(['\n','\r',',',';',' '],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(entries.Length is <1 or >16)throw new SafeError("INVALID_IMPORT");
        var values=new List<string>();
        foreach(string entry in entries){
            try{_=TransportPlan.SlipResolver(entry);values.Add(entry);}
            catch(SafeError){string endpoint=ResolverScan.Endpoint(entry,transport);bool authoritative=(original??"").Split(',',StringSplitOptions.RemoveEmptyEntries).Any(w=>{var prior=TransportPlan.SlipResolver(w);return prior.Address==endpoint&&prior.Authoritative;});values.Add(endpoint+(authoritative?":1":":0"));}
        }
        return string.Join(',',values.Distinct());
    }
}
