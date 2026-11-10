namespace Rovunet.WindowsSmoke;

// Test-only elapsed-time adapter. No corresponding switch exists in the app.
internal sealed class AcceleratedFixtureClock : TimeProvider
{
    readonly long origin = TimeProvider.System.GetTimestamp();
    public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;
    public override long GetTimestamp() => (TimeProvider.System.GetTimestamp() - origin) * 150;
}
