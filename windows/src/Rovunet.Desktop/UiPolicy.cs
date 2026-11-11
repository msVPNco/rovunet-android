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
    public readonly record struct Frame(double X,double Y,double Width,double Height);
    public static Frame FitWindow(Frame current,Frame work,double scale)
    {
        if(work.Width<=0||work.Height<=0)return current;
        if(current.X>=work.X&&current.Y>=work.Y&&current.X+current.Width<=work.X+work.Width&&current.Y+current.Height<=work.Y+work.Height)return current;
        if(!double.IsFinite(scale)||scale<=0)scale=1;
        double margin=Math.Min(16*scale,Math.Min(work.Width,work.Height)/8);
        double width=Math.Clamp(current.Width,1,Math.Max(1,work.Width-2*margin));
        double height=Math.Clamp(current.Height,1,Math.Max(1,work.Height-2*margin));
        return new(Math.Clamp(current.X,work.X+margin,work.X+work.Width-margin-width),Math.Clamp(current.Y,work.Y+margin,work.Y+work.Height-margin-height),width,height);
    }
}
