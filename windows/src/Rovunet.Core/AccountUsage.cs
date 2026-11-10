namespace Rovunet.Core;

// Account bytes come from one server snapshot, never from path count or device counters.
public sealed record AccountUsage(bool HasQuota,bool Unlimited,long? Limit,long? Used,long? Remaining,double? UsedPercent,bool Current,TimeSpan? TimeLeft,bool Expired)
{
    public static AccountUsage From(StatusView? view)
    {
        var status=view?.Status;if(status==null)return new(false,false,null,null,null,null,false,null,false);
        bool quota=status.Metering=="complete"&&status.LimitBytes is >0&&status.UsedBytes is >=0;
        TimeSpan? left=status.ExpiresAt.HasValue&&view?.ServerNow is {} now?status.ExpiresAt.Value-now:null;
        return new(quota,status.Metering=="not_metered",quota?status.LimitBytes:null,quota?status.UsedBytes:null,quota?status.RemainingBytes:null,
            quota?Math.Clamp((double)status.UsedBytes!.Value/status.LimitBytes!.Value*100,0,100):null,view?.Trustworthy==true,left,status.State=="expired"||left<=TimeSpan.Zero);
    }
    // Subscription panels conventionally express their quota in decimal GB; name the unit honestly.
    public static double Gigabytes(long bytes)=>bytes/1_000_000_000d;
}
