using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    DialogWindow BuildEndpointEditor(Profile profile)
    {
        var original=EndpointEditing.Read(profile)??throw new SafeError("UNSUPPORTED_FORMAT");
        var d=Dialog("EDIT_PROFILE",500,800);d.UsePageChrome();var panel=new StackPanel{Margin=new(18,8,18,20)};
        panel.Children.Add(Text(profile.Protocol,12,Accent));if(profile.SourceId.Length>0)panel.Children.Add(Text(L.Text("LOCAL_OVERRIDE_HINT"),12,Muted));
        var name=new TextBox{Text=original.Name,FlowDirection=L.Direction};var host=new TextBox{Text=original.Host,FlowDirection=FlowDirection.LeftToRight};var port=new TextBox{Text=original.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),FlowDirection=FlowDirection.LeftToRight};
        panel.Children.Add(OutlinedField("PROFILE_NAME",name));panel.Children.Add(OutlinedField("SERVER_HOST",host));panel.Children.Add(OutlinedField("SERVER_PORT",port));
        TextBox? sni=null;if(SshHostKey.UsesTls(profile)){sni=new TextBox{Text=Importer.Fields(profile.Raw)[51],Tag="SSH_TLS_SNI",FlowDirection=FlowDirection.LeftToRight};panel.Children.Add(OutlinedField("SSH_TLS_SNI",sni));}
        string Draft(){string raw=EndpointEditing.Apply(profile,name.Text,host.Text,port.Text);if(sni==null)return raw;string value=sni.Text.Trim();if(value.Length>0&&Uri.CheckHostName(value)==UriHostNameType.Unknown)throw new SafeError("CORE_SNI_INVALID");return ProfileEditing.ChangeNative(ProfileEditing.Replace(profile,raw),new Dictionary<int,string>{{51,value}});}
        panel.Children.Add(Text(L.Text("ENDPOINT_EDIT_HINT"),12,Muted));var trust=AddTrustEditor(panel,profile);var error=Text("",12,Red);panel.Children.Add(error);
        void Apply(string raw){var next=ProfileEditing.Replace(profile,raw);SaveEdited(profile,raw,applyTrust:trust.Prepare(next));}
        var save=Button("SAVE",()=>{try{Apply(Draft());d.DialogResult=true;}catch(Exception e){error.Text=L.Text(e is SafeError se?se.Code:"INVALID_IMPORT");}},true);save.IsDefault=true;d.SetPageAction(save);
        if(data.LocalOverrideSources.ContainsKey(profile.Id))panel.Children.Add(SettingsAction("RESET_LOCAL_EDIT",()=>ResetLocalOverride(profile,d)));
        panel.Children.Add(SettingsAction("ADVANCED_RAW",()=>{try{var advanced=BuildRawEditor(profile,Draft(),Apply);advanced.Owner=d;if(advanced.ShowDialog()==true)d.DialogResult=true;}catch(Exception e){error.Text=L.Text(e is SafeError se?se.Code:"INVALID_IMPORT");}}));d.Content=panel;return d;
    }
}

