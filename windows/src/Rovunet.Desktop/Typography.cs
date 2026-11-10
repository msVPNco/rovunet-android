using System.Windows;
using System.Windows.Media;
namespace Rovunet.Desktop;
internal static class Typography
{
    internal static double Size(double original)=>original;
    internal static FontWeight Body=>FontWeights.Normal;
    internal static FontFamily Family(string language)=>language=="fa"?new FontFamily(new Uri("pack://application:,,,/Rovunet;component/"),"./Assets/#Vazirmatn"):new FontFamily(language=="zh-CN"?"Microsoft YaHei UI":"Segoe UI");
}
