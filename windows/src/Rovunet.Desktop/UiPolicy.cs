namespace Rovunet.Desktop;

// Pure policy shared with portable regression tests; no Windows APIs here.
public static class UiPolicy
{
    public static double WindowSize(double saved, double available, double preferred)
    {
        double limit = Math.Max(120, available - 32);
        return Math.Clamp(double.IsFinite(saved) && saved >= 320 ? saved : preferred, Math.Min(420, limit), limit);
    }
    public static bool MeasurementFresh(DateTimeOffset sampled, DateTimeOffset now) => sampled <= now && now - sampled < TimeSpan.FromMinutes(15);
    public static TimeSpan TestBudget(bool slip, int seconds) => TimeSpan.FromSeconds(slip ? Math.Max(90, seconds) : Math.Clamp(seconds, 10, 120));
}
