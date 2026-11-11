using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Rovunet.Core;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly List<WeakReference<Action>> appProxyRefreshers=[];

    void RefreshAppProxyPages()
    {
        foreach(var reference in appProxyRefreshers.ToArray())
        {
            if(reference.TryGetTarget(out var refresh))refresh();
            else appProxyRefreshers.Remove(reference);
        }
    }

    internal static string TelegramProxyLink(int port)
    {
        if(port is <1 or >65535)throw new SafeError("LOCAL_PROXY_PORT_INVALID");
        return "tg://socks?server=127.0.0.1&port="+port.ToString(CultureInfo.InvariantCulture);
    }

    void ShowAppProxy()
    {
        var page=Page();
        var introduction=new StackPanel{Spacing=8};
        introduction.Children.Add(Label(L.Text("MAC_APP_PROXY_HINT"),14,"Muted"));
        var status=Label("",14);status.Name="AppProxyStatus";introduction.Children.Add(status);
        page.Children.Add(Card(introduction));

        var addresses=new StackPanel{Spacing=8};
        var host=Field(addresses,"SERVER_HOST","127.0.0.1");host.IsReadOnly=true;
        var ports=new Grid{ColumnDefinitions=new("*,*"),ColumnSpacing=10};
        var left=new StackPanel();var right=new StackPanel();ports.Children.Add(left);Grid.SetColumn(right,1);ports.Children.Add(right);addresses.Children.Add(ports);
        int InitialPort(int configured,int active,int fallback)=>configured>0?configured:active>0?active:fallback;
        var socks=Field(left,"SOCKS5",InitialPort(data.LocalSocksPort,state.SocksPort,10808).ToString());socks.Name="AppProxySocksPort";
        var http=Field(right,"HTTP",InitialPort(data.LocalHttpPort,state.HttpPort,10809).ToString());http.Name="AppProxyHttpPort";
        var addressSettings=new Expander{Header=L.Text("MAC_APP_PROXY_PORTS"),Content=addresses,HorizontalAlignment=HorizontalAlignment.Stretch};
        var addressCard=Card(addressSettings);

        var application=new StackPanel{Spacing=8};
        var app=Choice(application,"LOCAL_PROXY_APPLICATION",[L.Text("LOCAL_PROXY_TELEGRAM"),"Firefox",L.Text("LOCAL_PROXY_OTHER_APP")],L.Text("LOCAL_PROXY_TELEGRAM"));app.Name="AppProxyApplication";
        var protocol=Choice(application,"PROTOCOL",["SOCKS5","HTTP"],"SOCKS5");protocol.Name="AppProxyProtocol";
        var endpoint=Label("",16,"Ink",true);endpoint.Name="AppProxyEndpoint";application.Children.Add(endpoint);
        var instructions=Label("",13,"Muted");application.Children.Add(instructions);
        var actions=new WrapPanel();application.Children.Add(actions);
        bool LocalOnly()=>!data.SystemProxy&&!data.SystemTun;
        bool Connected()=>state.State=="connected";
        int Port()=>Connected()?(protocol.SelectedIndex==0?state.SocksPort:state.HttpPort):
            int.TryParse(protocol.SelectedIndex==0?socks.Text:http.Text,out var value)?value:0;
        async Task Copy(string value){if(Clipboard!=null)await Clipboard.SetTextAsync(value);}
        var copy=ActionButton("COPY",()=>_=Guard(()=>Copy("127.0.0.1:"+Port().ToString(CultureInfo.InvariantCulture))));actions.Children.Add(copy);
        var copyTelegram=ActionButton("LOCAL_PROXY_COPY_TELEGRAM",()=>_=Guard(()=>Copy(TelegramProxyLink(Port()))));copyTelegram.Name="AppProxyCopyTelegram";actions.Children.Add(copyTelegram);
        var openTelegram=ActionButton("LOCAL_PROXY_OPEN_TELEGRAM",()=>_=Guard(async()=>{
            if(!Connected()||!LocalOnly())throw new SafeError("MAC_APP_PROXY_SAVE_FIRST");
            if(!OperatingSystem.IsMacOS())throw new SafeError("MACOS_REQUIRED");
            using var process=Process.Start(new ProcessStartInfo("/usr/bin/open"){UseShellExecute=false,ArgumentList={TelegramProxyLink(state.SocksPort)}});
            if(process==null)throw new SafeError("LOCAL_PROXY_TELEGRAM_MISSING");
            await process.WaitForExitAsync();if(process.ExitCode!=0)throw new SafeError("LOCAL_PROXY_TELEGRAM_MISSING");
        }),true);openTelegram.Name="AppProxyOpenTelegram";actions.Children.Add(openTelegram);
        page.Children.Add(Card(application));
        page.Children.Add(addressCard);

        var connectionActions=new WrapPanel();page.Children.Add(connectionActions);
        var connect=ActionButton("CONNECT",()=>_=Guard(async()=>{
            if(!LocalOnly())throw new SafeError("MAC_APP_PROXY_SAVE_FIRST");
            await ConnectProfile(Selected??throw new SafeError("SELECT_PROFILE"));
        }),true);connect.Name="AppProxyConnect";connectionActions.Children.Add(connect);
        var disconnect=ActionButton("DISCONNECT",()=>_=Guard(Disconnect));disconnect.Name="AppProxyDisconnect";connectionActions.Children.Add(disconnect);
        var save=ActionButton("SAVE",()=>{
            RequireIdle();
            if(!int.TryParse(http.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var httpPort)||
                !int.TryParse(socks.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var socksPort)||
                httpPort is <1 or >65535||socksPort is <1 or >65535||httpPort==socksPort)
                throw new SafeError("LOCAL_PROXY_PORT_INVALID");
            Commit(next=>{next.LocalHttpPort=httpPort;next.LocalSocksPort=socksPort;next.SystemProxy=false;next.SystemTun=false;});
            message="";GoHome();
        },true);save.Name="AppProxySave";page.Children.Add(save);

        void Refresh()
        {
            bool locked=busy||state.State is "connected" or "connecting" or "engine-started";
            save.IsEnabled=!locked;socks.IsReadOnly=http.IsReadOnly=locked;
            if(Connected()){socks.Text=state.SocksPort.ToString();http.Text=state.HttpPort.ToString();}
            bool telegram=app.SelectedIndex==0;
            if(telegram&&protocol.SelectedIndex!=0)protocol.SelectedIndex=0;
            protocol.IsEnabled=!telegram;
            ((Control)protocol.Parent!).IsVisible=!telegram;
            endpoint.Text=Port()>0?"127.0.0.1:"+Port().ToString(CultureInfo.InvariantCulture):"—";
            copy.IsEnabled=copyTelegram.IsEnabled=Port() is >0 and <=65535;
            copyTelegram.IsVisible=openTelegram.IsVisible=telegram;
            openTelegram.IsEnabled=Connected()&&LocalOnly()&&state.SocksPort>0;
            connect.IsEnabled=!locked&&LocalOnly()&&Selected!=null;
            disconnect.IsVisible=state.State is "connected" or "connecting" or "engine-started";
            status.Text=L.Text(Connected()&&LocalOnly()?"LOCAL_PROXY_LISTENING":locked?"MAC_APP_PROXY_DISCONNECT_FIRST":LocalOnly()?"LOCAL_PROXY_OFFLINE":"MAC_APP_PROXY_SAVE_FIRST");
            status.Foreground=Tone(Connected()&&LocalOnly()?"Green":"Muted");
            instructions.Text=L.Text(telegram?"MAC_APP_PROXY_TELEGRAM_HINT":app.SelectedIndex==1?"MAC_APP_PROXY_FIREFOX_HINT":"LOCAL_PROXY_OTHER_HINT");
        }
        app.SelectionChanged+=(_,_)=>Refresh();protocol.SelectionChanged+=(_,_)=>Refresh();
        socks.TextChanged+=(_,_)=>Refresh();http.TextChanged+=(_,_)=>Refresh();
        Action refresh=Refresh;page.Tag=refresh;appProxyRefreshers.Add(new(refresh));
        Refresh();Navigate("LOCAL_PROXY_APPS",page);
    }
}
