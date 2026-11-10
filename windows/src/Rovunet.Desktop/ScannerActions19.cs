using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    static string ScannerProbeLabel(string name)=>name switch{"A"=>"RND","LONG-TXT"=>"DPI","NXDOMAIN"=>"NXD",_=>name};
    static bool ScannerKeyValid19(string family,string key)
    {
        try{if(key.Length==64&&Convert.FromHexString(key).Length==32)return true;}catch(FormatException){}
        if(family!="soar")return false;
        try{string b64=key.Trim().Replace('-','+').Replace('_','/');return Convert.FromBase64String(b64.PadRight((b64.Length+3)/4*4,'=')).Length==32;}catch(FormatException){return false;}
    }
    static int CompareScannerEntries19(ScanEntry a,ScanEntry b,string sort)
    {
        int compared=sort switch{
            "dns"=>a.DnsLatency.CompareTo(b.DnsLatency),"latency"=>a.Latency.CompareTo(b.Latency),"score"=>b.Score.CompareTo(a.Score),"prism"=>b.PrismScore.CompareTo(a.PrismScore),"address"=>string.Compare(a.AddressSortKey,b.AddressSortKey,StringComparison.Ordinal),_=>a.ScanIndex.CompareTo(b.ScanIndex)};
        return compared!=0?compared:a.ScanIndex.CompareTo(b.ScanIndex);
    }
    void ScannerOutput19(DialogWindow owner,ObservableCollection<ScanEntry> entries,ListBox visible,HashSet<ScanEntry> selected,string action)
    {
        var page=new DialogWindow(owner,L.Text(action=="copy"?"COPY":action=="pool"?"DNS_POOL_SAVE":"EXPORT_FILE"),500,600);page.UsePageChrome();var body=new StackPanel{Margin=new(18)};
        void Choice(string label,IEnumerable<ScanEntry> values){var rows=values.Where(e=>e.Selectable&&!e.Stale).DistinctBy(e=>e.Address).ToArray();if(rows.Length==0)return;var button=Button(label,()=>{
            try{string[] addresses=rows.Select(e=>e.Address).ToArray();
                if(action=="copy"){Clipboard.SetText(string.Join('\n',addresses));page.Close();Notify(L.Text("COPIED"));}
                else if(action=="export"){var file=new SaveFileDialog{Filter="TXT|*.txt",FileName="Rovunet-DNS.txt"};if(file.ShowDialog(owner.NativeOwner)==true){File.WriteAllLines(file.FileName,addresses);page.Close();}}
                else{page.Close();SaveScannedResolvers(addresses);}
            }catch(Exception error){Info("HELP",L.Text(error is SafeError safe?safe.Code:"EXPORT_FAILED"));}
        });button.Content=L.Text(label)+" · "+rows.Length;body.Children.Add(Card(button));}
        Choice("SCAN_SELECTED_ONLY",selected);Choice("SCAN_ONLY_TUNNEL",visible.Items.Cast<ScanEntry>().Where(e=>e.TunnelPassed));Choice("SCAN_ONLY_WORKING",visible.Items.Cast<ScanEntry>());Choice("SCAN_PRISM_PASSED",entries.Where(e=>e.Result.Attempted>0&&e.Result.Pass));page.Content=body;page.ShowDialog();
    }
}
