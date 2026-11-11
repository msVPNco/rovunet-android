namespace Rovunet.Core;

public sealed record PoolProbe(bool Passed, long Milliseconds);
public static class DnsPool
{
    public static string[] Parse(string text)
    {
        if (text.Length > 16384) throw new SafeError("DNS_SCAN_LIMIT");
        var entries = text.Split(['\n',','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(value => !value.StartsWith('#')).Select(value => DnsProbe.Endpoint(value).ToString()).Distinct(StringComparer.Ordinal).ToArray();
        if (entries.Length > 64) throw new SafeError("DNS_SCAN_LIMIT");
        return entries;
    }
    public static async Task<string[]> Select(string[] entries, Func<string,CancellationToken,Task<PoolProbe>> probe, CancellationToken stop)
    {
        var successes = new List<(int Index, string Address, long Time)>();
        await Parallel.ForEachAsync(Enumerable.Range(0, entries.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stop }, async (index, ct) => {
            try {
                var result = await probe(entries[index], ct);
                if (result.Passed) lock(successes) successes.Add((index, entries[index], result.Milliseconds));
            } catch (OperationCanceledException) { ct.ThrowIfCancellationRequested(); }
            catch (SafeError) { }
        });
        return successes.OrderBy(v => v.Time).ThenBy(v => v.Index).Take(8).Select(v => v.Address).ToArray();
    }
}
