using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    void AddLocalProfile25(Profile p)
    {
        if(data.Profiles.Any(x=>x.Raw==p.Raw))throw new SafeError("DUPLICATE_PROFILE");
        var prior=data.Profiles.ToList();data.Profiles.Add(p);try{Save();}catch{data.Profiles=prior;throw;}
        BuildCards();Notify(L.Text("IMPORT_ADDED")+" 1");
    }
    DialogWindow BuildAlternativeEditor25(string kind,Profile? original=null)
    {
        var page=Dialog(original==null?"ADD":"EDIT_PROFILE");page.SetPageTitle((original==null?L.Text("ADD"):L.Text("EDIT_PROFILE"))+" · "+(kind=="doh"?"DoH":"Tor"));
        var panel=new StackPanel{Margin=new(20)};
        var name=new TextBox{Text=original?.Name??(kind=="doh"?"DoH":"Tor"),Tag="alternative:name",FlowDirection=L.Direction};panel.Children.Add(OutlinedField("PROFILE_NAME",name));
        var input=new TextBox{Tag="alternative:value",FlowDirection=FlowDirection.LeftToRight,AcceptsReturn=kind=="snowflake",MinHeight=kind=="snowflake"?180:48,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,TextWrapping=TextWrapping.Wrap};
        input.Text=original!=null?(kind=="doh"?AlternativeProfiles.DohUrl(original.Raw):string.Join('\n',AlternativeProfiles.Bridges(original.Raw))):kind=="doh"?"https://cloudflare-dns.com/dns-query":"";
        panel.Children.Add(OutlinedField(kind=="doh"?"DOH_URL":"TOR_BRIDGES",input));
        panel.Children.Add(Text(L.Text(kind=="doh"?"DOH_SCOPE_HINT":"TOR_BRIDGES_HINT"),13,Muted));
        var error=Text("",13,Red);panel.Children.Add(error);
        void Apply(){try{string raw=AlternativeProfiles.Create(kind,name.Text,input.Text);if(original==null)AddLocalProfile25(Importer.Single(raw));else SaveEdited(original,raw);page.DialogResult=true;}catch(Exception ex){error.Text=L.Text(ex is SafeError safe?safe.Code:"INVALID_IMPORT");}}
        page.SetPageAction(Button("SAVE",Apply,true));panel.Children.Add(Button(original==null?"ADD":"SAVE",Apply,true));page.Content=panel;return page;
    }
    FrameworkElement ProtocolChoiceFace25(string name)
    {
        string shape=name switch{
            "SSH" or "WireGuard" or "AmneziaWG"=>"M6,10 V7 A6,6 0 0 1 18,7 V10 M4,10 H20 V22 H4 Z M12,14 V18",
            "XHTTP" or "SOCKS5" or "DoH"=>"M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M2,12 H22 M12,2 C5,7 5,17 12,22 C19,17 19,7 12,2",
            "Hysteria2" or "TUIC" or "Slipstream"=>"M2,6 Q7,1 12,6 Q17,11 22,6 M2,12 Q7,7 12,12 Q17,17 22,12 M2,18 Q7,13 12,18 Q17,23 22,18",
            "DNSTT" or "NoizDNS" or "VayDNS" or "Soar"=>"M3,3 H21 V10 H3 Z M3,14 H21 V21 H3 Z M7,6 H9 M7,17 H9",
            _=>"M12,2 L21,6 V12 C21,18 16,21 12,23 C8,21 3,18 3,12 V6 Z"
        };
        var face=new Grid{FlowDirection=FlowDirection.LeftToRight};face.ColumnDefinitions.Add(new(){Width=new(22)});face.ColumnDefinitions.Add(new());
        face.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(shape),Stroke=Tone("Muted"),StrokeThickness=1.5,Width=16,Height=16,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center});
        var title=MovingLine21(Text(name,14),22,FlowDirection.LeftToRight);Grid.SetColumn(title,1);face.Children.Add(title);return face;
    }
}
