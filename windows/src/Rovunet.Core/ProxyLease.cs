namespace Rovunet.Core;

public sealed record ProxySettings(int Flags, string Server, string Bypass, string AutoConfigUrl);
public sealed record ProxyJournal(ProxySettings Before, ProxySettings Applied, string OwnerDirectory = "");
public interface IProxyBackend { ProxySettings Read(); void Write(ProxySettings settings); }
public interface ILeaseStore { ProxyJournal? Read(); void Write(ProxyJournal value); void Delete(); }

// Journal is durable before mutation. Callers serialize this per Windows user.
public sealed class ProxyLease(IProxyBackend backend, ILeaseStore store, string ownerDirectory = "")
{
    public void Acquire(int port)
    {
        if (store.Read() != null) throw new SafeError("RECOVERY_REQUIRED");
        var before = backend.Read(); var applied = new ProxySettings(3, $"http=127.0.0.1:{port};https=127.0.0.1:{port}", "<local>", before.AutoConfigUrl);
        store.Write(new(before, applied, ownerDirectory));
        try { backend.Write(applied); if (backend.Read() != applied) throw new SafeError("PROXY_APPLY_FAILED"); }
        catch { Recover(); throw; }
    }
    public void Recover()
    {
        var journal = store.Read(); if (journal == null) return;
        var current = backend.Read();
        if (current == journal.Before) { store.Delete(); return; }
        // Don't overwrite settings changed by Windows, another app, or the user.
        // Keep the journal on conflict so the user can explicitly resolve it.
        if (current != journal.Applied) throw new SafeError("PROXY_RECOVERY_CONFLICT");
        backend.Write(journal.Before);
        if (backend.Read() != journal.Before) throw new SafeError("PROXY_RECOVERY_FAILED");
        store.Delete();
    }
}
