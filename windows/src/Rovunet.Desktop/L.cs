using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace Rovunet.Desktop;

public static class L
{
    static readonly Dictionary<string, Dictionary<string, string>> all = Load();
    static Dictionary<string, Dictionary<string, string>> Load()
    {
        var a = new Dictionary<string, Dictionary<string, string>>(); var assembly = Assembly.GetExecutingAssembly();
        foreach (var lang in new[] { "fa", "en", "ru", "zh-CN" })
        { using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Locales." + lang + ".json", StringComparison.Ordinal)))!; a[lang] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!; }
        return a;
    }
    public static string Language { get; set; } = "fa";
    public static string Text(string key) => all.GetValueOrDefault(Language, all["en"]).GetValueOrDefault(key, all["en"].GetValueOrDefault(key, all["en"]["UNEXPECTED_ERROR"]));
    public static FlowDirection Direction => Language == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
