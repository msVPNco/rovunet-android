using Rovunet.Core;

namespace Rovunet.Platform;

public static class CollectionTestPolicy
{
    public static async Task<int> Run(IReadOnlyList<Profile> profiles,bool phased,
        Func<Profile,CancellationToken,Task<bool>> probe,Func<CancellationToken,Task<bool>> offerDns,
        Action<bool,int> phaseStarted,CancellationToken cancellation)
    {
        async Task<int> Phase(IReadOnlyList<Profile> paths,bool dns)
        {
            cancellation.ThrowIfCancellationRequested();phaseStarted(dns,paths.Count);int passed=0;
            foreach(var path in paths){cancellation.ThrowIfCancellationRequested();if(await probe(path,cancellation))passed++;}
            cancellation.ThrowIfCancellationRequested();return passed;
        }
        if(!phased)return await Phase(profiles,false);
        var fast=profiles.Where(p=>!ProfileEditing.IsDns(p)).ToArray();
        var dns=profiles.Where(ProfileEditing.IsDns).ToArray();
        int successes=await Phase(fast,false);
        if(successes==0&&dns.Length>0){cancellation.ThrowIfCancellationRequested();if(await offerDns(cancellation))return await Phase(dns,true);}
        return successes;
    }
}
