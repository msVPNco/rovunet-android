using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    internal static string DnsTransportLabel20(string value)=>value switch{"tls"=>"DoT","https"=>"DoH","both"=>"UDP + TCP",_=>value.ToUpperInvariant()};
    sealed class DnsTransportConverter20:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>DnsTransportLabel20(value?.ToString()??"");
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
    }
    static void LabelDnsTransport20(ComboBox model)
    {
        var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding{Converter=new DnsTransportConverter20()});model.ItemTemplate=new DataTemplate{VisualTree=text};
    }
    internal static string[] NetworkResolvers20()=>NetworkInterface.GetAllNetworkInterfaces()
        .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType is not(NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
        .OrderByDescending(n=>n.GetIPProperties().GatewayAddresses.Count>0)
        .SelectMany(n=>n.GetIPProperties().DnsAddresses)
        .Where(a=>!IPAddress.IsLoopback(a)&&!a.Equals(IPAddress.Any)&&!a.Equals(IPAddress.IPv6Any))
        .Distinct().Take(16).Select(a=>new IPEndPoint(a,53).ToString()).ToArray();
}
