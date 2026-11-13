using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;

namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    List<ChainIdentity> ChainIdentityOptions25(Profile p)=>!ChainProfiles.IsChain(p.Raw)?[]:ChainProfiles.Read(p.Raw).Layers.Select(layer=>new ChainIdentity{HostKey=StoredSshKey(layer),SlipCertificate=StoredSlipPin(layer),SshTlsCa=StoredSshTlsCa(layer),SlipInterval=SlipInterval(layer)}).ToList();
    Profile TrustLayer25(Profile p,AgentReply reply)=>ChainProfiles.IsChain(p.Raw)&&reply.ChainLayerIndex>=0?ChainProfiles.Read(p.Raw).Layers[reply.ChainLayerIndex]:p;
    DialogWindow BuildChainEditor25(Profile? original=null)
    {
        var page=Dialog("CHAIN_TITLE");var panel=new StackPanel{Margin=new(20)};
        var name=new TextBox{Text=original?.Name??L.Text("CHAIN_TITLE"),Tag="chain:name"};panel.Children.Add(OutlinedField("PROFILE_NAME",name));panel.Children.Add(Text(L.Text("CHAIN_HINT"),13,Muted));
        var current=original==null?new List<Profile>():ChainProfiles.Read(original.Raw).Layers;
        var choices=data.Profiles.Where(p=>!ChainProfiles.IsChain(p.Raw)&&p.CanConnect).Concat(current).GroupBy(p=>p.Id).Select(g=>g.First()).ToList();
        var selects=new List<ComboBox>();var rows=new StackPanel();panel.Children.Add(rows);
        void AddLayer(Profile? selected=null){if(selects.Count>=4)return;var box=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=selected,Tag="chain:layer:"+selects.Count};selects.Add(box);rows.Children.Add(OutlinedField("CHAIN_LAYER",box));}
        if(current.Count>0)foreach(var layer in current)AddLayer(choices.FirstOrDefault(x=>x.Id==layer.Id));else{AddLayer();AddLayer();}
        var buttons=new WrapPanel();var add=Button("CHAIN_ADD_LAYER",()=>AddLayer());var remove=Button("CHAIN_REMOVE_LAYER",()=>{if(selects.Count>2){selects.RemoveAt(selects.Count-1);rows.Children.RemoveAt(rows.Children.Count-1);}});buttons.Children.Add(add);buttons.Children.Add(remove);panel.Children.Add(buttons);
        var error=Text("",13,Red);panel.Children.Add(error);
        void Apply(){try{var layers=selects.Select(b=>b.SelectedItem as Profile??throw new SafeError("SELECT_PATH")).ToList();string raw=ChainProfiles.Create(name.Text,layers);if(original==null)AddLocalProfile25(Importer.Single(raw));else SaveEdited(original,raw);page.DialogResult=true;}catch(Exception e){error.Text=L.Text(e is SafeError s?s.Code:"INVALID_IMPORT");}}
        page.SetPageAction(Button("SAVE",Apply,true));panel.Children.Add(Button(original==null?"ADD":"SAVE",Apply,true));page.Content=panel;return page;
    }
    void PrepareChainCertificates25(Profile p)
    {
        if(!ChainProfiles.IsChain(p.Raw))return;
        foreach(var layer in ChainProfiles.Read(p.Raw).Layers)if(TransportPlan.UsesSlip(layer.Raw)&&StoredSlipPin(layer).Length==0&&!ChooseSlipCertificate(layer))throw new SafeError("SLIP_CERT_REQUIRED");
    }
}
