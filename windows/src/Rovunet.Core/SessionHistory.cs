namespace Rovunet.Core;
public sealed class SessionHistory
{
    public string Root{get;set;}="";
    public string InstallationReceipt{get;set;}="";
    public List<Guid> Sessions{get;set;}=[];
    public void Accept(string root,string receipt,Guid session)
    {
        if(Root!=root||InstallationReceipt!=receipt||Sessions==null||Sessions.Count>16384||Sessions.Any(s=>s==Guid.Empty)||Sessions.Distinct().Count()!=Sessions.Count)throw new SafeError("BROKER_SESSION_HISTORY_INVALID");
        if(session==Guid.Empty||Sessions.Contains(session))throw new SafeError("BROKER_SESSION_REPLAY_REJECTED");
        // Never silently evict an old nonce and make its request replayable again.
        if(Sessions.Count==16384)throw new SafeError("BROKER_SESSION_HISTORY_FULL");
        Sessions.Add(session);
    }
}
