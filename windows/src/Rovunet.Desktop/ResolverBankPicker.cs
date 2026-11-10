using System.Windows;
using System.Windows.Controls;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    static string BankSummary(ResolverBank bank)=>string.Format(L.Text(bank.Networks?"DNS_BANK_RANGES":"DNS_BANK_ADDRESSES"),ResolverCatalog.Count(bank));
    DialogWindow BuildResolverBankPicker(string? current,Action<string> chosen)
    {
        var d=Dialog("DNS_BANK_TITLE",500,800);var panel=new StackPanel{Margin=new(18,8,18,20)};
        panel.Children.Add(Text(L.Text("DNS_BANK_HINT"),12,Muted));string selected=ResolverCatalog.Banks.Any(b=>b.File==current)?current!:"resolvers.txt";
        foreach(var bank in ResolverCatalog.Banks){
            var body=new StackPanel();var choice=new RadioButton{Content=L.Text(bank.LabelKey),GroupName="resolver-bank",Tag=bank.File,MinHeight=48,IsChecked=bank.File==selected};
            choice.Template=(ControlTemplate)Application.Current.FindResource("BankRadioTemplate");choice.Checked+=(_,_)=>selected=bank.File;body.Children.Add(choice);body.Children.Add(Text(BankSummary(bank),12,Muted));body.Children.Add(Technical(Text(string.Join("\n",ResolverCatalog.Lines(bank.File).Take(3)),11,Muted)));panel.Children.Add(Card(body));
        }
        var use=Button("DNS_BANK_USE",()=>{chosen(selected);d.DialogResult=true;},true);use.IsDefault=true;d.SetPageAction(use);d.Content=panel;return d;
    }
}
