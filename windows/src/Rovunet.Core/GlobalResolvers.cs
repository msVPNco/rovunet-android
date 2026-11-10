namespace Rovunet.Core;

public static class GlobalResolvers
{
    public static string Normalize(string text)
    {
        if (text.Length > 4096) throw new SafeError("DNS_SCAN_LIMIT");
        var endpoints = text.Split(['\r','\n',',',';',' ','\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? SubscriptionUrls.Validate(value).AbsoluteUri : DnsProbe.Endpoint(value).ToString()).Distinct(StringComparer.Ordinal).ToArray();
        if (endpoints.Length > 16) throw new SafeError("DNS_SCAN_LIMIT");
        return string.Join('\n', endpoints);
    }
    public static (Profile Effective, string Notice) ForConnection(Profile original, bool enabled, string text)
    {
        if (!enabled || string.IsNullOrWhiteSpace(text) || !ProfileEditing.IsDns(original)) return (original, "");
        try
        {
            string normalized = Normalize(text);
            if (normalized.Length == 0) return (original, "");
            var fields = Importer.Fields(original.Raw);
            var entries = normalized.Split('\n');
            bool doh = fields[22] == "https";
            var relevant = entries.Where(entry => entry.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == doh).ToArray();
            if (relevant.Length == 0 || doh && relevant.Length != 1) return (original, "GLOBAL_RESOLVER_NOT_APPLIED");
            var changes = new Dictionary<int,string>();
            if (doh) changes[21] = relevant[0];
            else changes[4] = string.Join(',', relevant.Select(endpoint => endpoint + ":0"));
            var effective = ProfileEditing.Replace(original, ProfileEditing.ChangeNative(original, changes));
            if (!effective.CanConnect) return (original, "GLOBAL_RESOLVER_NOT_APPLIED");
            return (effective, "");
        }
        catch (SafeError) { return (original, "GLOBAL_RESOLVER_NOT_APPLIED"); }
    }
}
