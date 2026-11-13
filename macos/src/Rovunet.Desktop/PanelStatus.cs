using System.Diagnostics;
using Rovunet.Core;
using Rovunet.Platform;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    readonly string accountStatusSession=Guid.NewGuid().ToString("N");
    async Task<SubscriptionClient.Payload?> ReadPanelStatus(Uri source,CancellationToken cancellation)
    {
        var alias=MacPanelStatus.StatusUri(source);
        if(alias==null)return null;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        try{
            var payload=await subscriptionFetcher(alias,state.HttpPort,deadline.Token);
            _=AccountStatus.Read(payload.Body);
            return payload;
        }catch(SafeError e) when(e.Code=="STATUS_NOT_FOUND"){
            cancellation.ThrowIfCancellationRequested();return null;
        }catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new SafeError("FETCH_TIMEOUT");
        }
    }

    void StorePanelStatus(AppState next,Uri source,SubscriptionClient.Payload? payload)
    {
        var sub=next.Subscriptions.Single(s=>s.Url==source.AbsoluteUri);
        if(payload==null){next.StatusReceipts.Remove(sub.Id);return;}
        var status=AccountStatus.Read(payload.Body);
        if(sub.StatusBody.Length>0){
            AccountStatus? previous=null;
            try{previous=AccountStatus.Read(sub.StatusBody);}catch(SafeError){}
            if(previous!=null&&(status.ServerId!=previous.ServerId||status.AccountId!=previous.AccountId||!status.Accepts(previous))){
                throw new SafeError("MAC_PANEL_STATUS_CONFLICT");
            }
        }
        sub.StatusBody=payload.Body;
        next.StatusReceipts[sub.Id]=new(DateTimeOffset.UtcNow,payload.Date,accountStatusSession,Stopwatch.GetTimestamp());
    }

    string PanelUsageText(Subscription? sub)
    {
        if(sub==null||sub.StatusBody.Length==0)return UsageText(sub?.Usage);
        AccountStatus status;
        try{status=AccountStatus.Read(sub.StatusBody);}catch(SafeError){return L.Text("MAC_UNKNOWN");}
        var received=data.StatusReceipts.GetValueOrDefault(sub.Id);
        var elapsed=received is {Tick:>0}?Stopwatch.GetElapsedTime(received.Tick):TimeSpan.MaxValue;
        var view=MacPanelStatus.Evaluate(status,received,accountStatusSession,DateTimeOffset.UtcNow,elapsed);
        return FormatPanelUsage(status,view);
    }

    string FormatPanelUsage(AccountStatus status,StatusView view)
    {
        string used=status.UsedBytes is {} bytes?Bytes(bytes):L.Text("MAC_UNKNOWN");
        if(status.Metering=="partial")used="≥ "+used;
        if(status.Metering=="unavailable")used=L.Text("MAC_UNKNOWN");
        string limit=status.LimitBytes is {} cap?Bytes(cap):L.Text(status.Metering=="not_metered"?"MAC_UNLIMITED":"MAC_UNKNOWN");
        return L.Text("MAC_ACCOUNT_"+status.State.ToUpperInvariant())+" · "+used+" / "+limit+(view.Trustworthy?"":" · "+L.Text("MAC_STATUS_STALE"));
    }
}
