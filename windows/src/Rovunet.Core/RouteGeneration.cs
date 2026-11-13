namespace Rovunet.Core;

// A->B->A is a new route, even when the final port/profile looks unchanged.
public sealed class RouteGeneration
{
    readonly object gate=new();
    long value;
    CancellationTokenSource lifetime=new();
    public readonly record struct Lease(long Value,CancellationToken Cancelled);
    public Lease Capture(){lock(gate)return new(value,lifetime.Token);}
    public bool IsCurrent(Lease lease){lock(gate)return lease.Value==value&&!lease.Cancelled.IsCancellationRequested;}
    public void Advance()
    {
        CancellationTokenSource previous;
        lock(gate){previous=lifetime;lifetime=new();checked{value++;}}
        previous.Cancel(); // Linked operations observe every transition, without polling.
    }
    public void Commit(Lease lease,Action commit)
    {
        lock(gate){if(lease.Value!=value||lease.Cancelled.IsCancellationRequested)throw new SafeError("APP_UPDATE_ROUTE_CHANGED");commit();}
    }
}
