namespace Rovunet.Core;

// A connection-scoped deadline. Wall-clock edits cannot extend or shorten it.
public sealed class SleepTimerSession(TimeProvider? clock = null)
{
    readonly TimeProvider time = clock ?? TimeProvider.System;
    bool connected, armed;
    long started;
    TimeSpan duration;
    public static bool ValidMinutes(int minutes) => minutes >= 0 && minutes <= 120 && minutes % 5 == 0;
    public int RemainingSeconds => !armed ? 0 : Math.Max(0, (int)Math.Ceiling((duration - time.GetElapsedTime(started)).TotalSeconds));
    public bool Active => armed;
    public long Generation { get; private set; }
    public void UpdateConnection(bool value, int minutes)
    {
        if (connected == value) return;
        connected = value; Cancel();
        if (!connected || !ValidMinutes(minutes) || minutes == 0) return;
        duration = TimeSpan.FromMinutes(minutes); started = time.GetTimestamp(); armed = true;
    }
    public void Cancel() { armed = false; Generation++; }
    public bool PollExpired()
    {
        if (!armed || RemainingSeconds > 0) return false;
        armed = false; return true;
    }
}
