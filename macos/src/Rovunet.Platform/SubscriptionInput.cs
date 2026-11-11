using Rovunet.Core;

namespace Rovunet.Platform;

public static class SubscriptionInput
{
    // Android MainViewModel routes a single HTTPS URL to subscriptions before
    // attempting to parse it as a connection profile.
    public static string? Address(string text)
    {
        string value=text.Trim().TrimStart('\uFEFF').Trim();
        if(!value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||value.Contains('\n')||value.Contains('\r'))return null;
        return SubscriptionUrls.Validate(value).AbsoluteUri;
    }
    public static string ProviderName(string title)
    {
        string value=title.Trim();
        if(value.StartsWith("base64:",StringComparison.OrdinalIgnoreCase)){
            try{value=Importer.Decode(value[7..]).Trim();}catch{return "";}
        }
        return value.Length is >0 and <=120&&!value.Any(char.IsControl)&&!value.Contains("://")&&!value.Contains('/')?value:"";
    }
}
