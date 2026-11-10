using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    sealed class ProxyPanel19
    {
        internal required FrameworkElement Root;
        internal required Action Refresh;
    }
    readonly List<WeakReference<ProxyPanel19>> localProxyPanels19=[];
    void RefreshLocalProxyPanels19(){foreach(var weak in localProxyPanels19.ToArray())if(weak.TryGetTarget(out var panel))panel.Refresh();else localProxyPanels19.Remove(weak);}
    FrameworkElement BuildLocalProxyPanel19()
    {
        var root=new StackPanel{Margin=new(0,8,0,12)};root.Children.Add(Text(L.Text("LOCAL_PROXY_APPS"),16));root.Children.Add(Text(L.Text("LOCAL_PROXY_APPS_HINT"),12,Muted));
        var protocol=new ComboBox{ItemsSource=new[]{"SOCKS5","HTTP"},SelectedIndex=0,Tag="LOCAL_PROXY_PROTOCOL"};var protocolFrame=OutlinedField("PROTOCOL",protocol);root.Children.Add(protocolFrame);var socksLabel=Text("SOCKS5",16);socksLabel.FlowDirection=FlowDirection.LeftToRight;var fixedProtocol=OutlinedField("PROTOCOL",new ContentControl{Content=socksLabel,VerticalContentAlignment=VerticalAlignment.Center});root.Children.Add(fixedProtocol);
        var fields=new Grid();fields.ColumnDefinitions.Add(new(){Width=new(2,GridUnitType.Star)});fields.ColumnDefinitions.Add(new());
        var host=new TextBox{Text="127.0.0.1",IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight,Tag="LOCAL_PROXY_HOST"};var port=new TextBox{FlowDirection=FlowDirection.LeftToRight,Tag="LOCAL_PROXY_PORT"};fields.Children.Add(OutlinedField("SERVER_HOST",host));var portField=OutlinedField("SERVER_PORT",port);Grid.SetColumn(portField,1);fields.Children.Add(portField);root.Children.Add(fields);
        var statusLine=Text("",12,Muted);statusLine.Tag="LOCAL_PROXY_STATUS";root.Children.Add(statusLine);
        bool draft=false,refreshing=false;string feedback="";int shownProtocol=-1;bool wasLocked=false;
        port.TextChanged+=(_,_)=>{if(!refreshing){draft=true;feedback="";}};
        bool Socks()=>protocol.SelectedIndex==0;
        int Port()=>state.State=="connected"?(Socks()?state.SocksPort:state.HttpPort):(Socks()?data.LocalSocksPort:data.LocalHttpPort);
        var buttons=new WrapPanel();var save=Button("LOCAL_PROXY_SAVE",()=>{
            if(busy||state.State is "connected" or "connecting" or "disconnecting")return;
            if(!int.TryParse(port.Text,NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n is <1 or >65535||n==(Socks()?data.LocalHttpPort:data.LocalSocksPort)){statusLine.Text=L.Text("LOCAL_PROXY_PORT_INVALID");return;}
            var old=(data.LocalHttpPort,data.LocalSocksPort);if(Socks())data.LocalSocksPort=n;else data.LocalHttpPort=n;
            try{Save();draft=false;feedback=L.Text("SETTINGS_SAVED");RefreshLocalProxyPanels19();}catch{(data.LocalHttpPort,data.LocalSocksPort)=old;statusLine.Text=L.Text("PRIVATE_STORE_FAILED");}
        });buttons.Children.Add(save);var copy=Button("COPY",()=>{if(Port()>0)Clipboard.SetText("127.0.0.1:"+Port().ToString(CultureInfo.InvariantCulture));});buttons.Children.Add(copy);root.Children.Add(buttons);
        var application=new ComboBox{ItemsSource=new[]{L.Text("LOCAL_PROXY_TELEGRAM"),L.Text("LOCAL_PROXY_OTHER_APP")},SelectedIndex=0,Tag="LOCAL_PROXY_APPLICATION"};root.Children.Add(OutlinedField("LOCAL_PROXY_APPLICATION",application));
        var telegram=new WrapPanel();var openTelegram=Button("LOCAL_PROXY_OPEN_TELEGRAM",()=>{if(state.State!="connected"||state.SocksPort<=0)return;try{Process.Start(new ProcessStartInfo(LocalProxyLink19(state.SocksPort)){UseShellExecute=true});}catch{statusLine.Text=L.Text("LOCAL_PROXY_TELEGRAM_MISSING");}},true);telegram.Children.Add(openTelegram);var copyLink=Button("LOCAL_PROXY_COPY_TELEGRAM",()=>{if(Port()>0)Clipboard.SetText(LocalProxyLink19(state.State=="connected"?state.SocksPort:data.LocalSocksPort));});telegram.Children.Add(copyLink);root.Children.Add(telegram);
        var other=new StackPanel{Visibility=Visibility.Collapsed};other.Children.Add(Text(L.Text("LOCAL_PROXY_OTHER_HINT"),12,Muted));
        string selectedProgram="";var program=Text("",12,Muted);other.Children.Add(program);other.Children.Add(Button("LOCAL_PROXY_CHOOSE_APP",()=>{var picker=new OpenFileDialog{Filter="Windows applications|*.exe",CheckFileExists=true};if(picker.ShowDialog(this)==true){selectedProgram=picker.FileName;program.Text=Path.GetFileNameWithoutExtension(selectedProgram);}}));
        other.Children.Add(Button("LOCAL_PROXY_OPEN_APP",()=>{if(selectedProgram.Length==0)return;try{Process.Start(new ProcessStartInfo(selectedProgram){UseShellExecute=true});}catch{statusLine.Text=L.Text("LOCAL_PROXY_OPEN_FAILED");}}));root.Children.Add(other);
        root.Children.Add(Text(L.Text("LOCAL_PROXY_METHOD_HINT"),12,Muted));
        void Refresh(){protocolFrame.Visibility=application.SelectedIndex==0?Visibility.Collapsed:Visibility.Visible;fixedProtocol.Visibility=application.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;protocol.IsEnabled=application.SelectedIndex!=0;if(application.SelectedIndex==0&&protocol.SelectedIndex!=0)protocol.SelectedIndex=0;bool connected=state.State=="connected",locked=connected||busy||state.State is "connecting" or "disconnecting";port.IsReadOnly=locked;save.IsEnabled=!locked;if(locked||wasLocked||!draft||shownProtocol!=protocol.SelectedIndex){refreshing=true;port.Text=Port()>0?Port().ToString(CultureInfo.InvariantCulture):"";refreshing=false;draft=false;}shownProtocol=protocol.SelectedIndex;wasLocked=locked;openTelegram.IsEnabled=connected&&state.SocksPort>0;copy.IsEnabled=Port()>0;telegram.Visibility=application.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;other.Visibility=application.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;statusLine.Text=feedback.Length>0&&!locked?feedback:L.Text(connected?"LOCAL_PROXY_LISTENING":"LOCAL_PROXY_OFFLINE");}
        protocol.SelectionChanged+=(_,_)=>Refresh();application.SelectionChanged+=(_,_)=>Refresh();var controller=new ProxyPanel19{Root=root,Refresh=Refresh};root.Tag=controller;localProxyPanels19.Add(new(controller));Refresh();return root;
    }
    internal static string LocalProxyLink19(int port)
    {
        if(port is <1 or >65535)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        return "tg://socks?server=127.0.0.1&port="+port.ToString(CultureInfo.InvariantCulture);
    }
}
