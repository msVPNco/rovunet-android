using System.Reflection;
using System.Text.Json;
using Avalonia.Media;
using Avalonia;

namespace Rovunet.Desktop;

public static class L
{
    static readonly Dictionary<string,Dictionary<string,string>> all=Load();
    static Dictionary<string,Dictionary<string,string>> Load()
    {
        var result=new Dictionary<string,Dictionary<string,string>>();var assembly=Assembly.GetExecutingAssembly();
        foreach(string code in new[]{"fa","en","ru","zh-CN"}){
            var words=new Dictionary<string,string>();
            foreach(string name in assembly.GetManifestResourceNames().Where(n=>n.EndsWith("."+code+".json",StringComparison.Ordinal)).OrderBy(n=>n.Contains("MacLocales")?1:0)){
                using var stream=assembly.GetManifestResourceStream(name)!;foreach(var pair in JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!)words[pair.Key]=pair.Value;
            }
            result[code]=words;
        }
        return result;
    }
    public static string Language {get;set;}="fa";
    public static string Text(string key)=>all.GetValueOrDefault(Language,all["en"]).GetValueOrDefault(key,all["en"].GetValueOrDefault(key,key));
    public static FlowDirection Direction=>Language=="fa"?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
    public static FontFamily TechnicalFont=>new(OperatingSystem.IsMacOS()?"Menlo":"Consolas, monospace");
    public static FontFamily Font=>OperatingSystem.IsMacOS()?FontFamily.Default:Language=="fa"?new("avares://Rovunet/Assets#Vazirmatn"):new(Language=="zh-CN"?"PingFang SC, Microsoft YaHei UI":"Segoe UI, Helvetica Neue");
}
