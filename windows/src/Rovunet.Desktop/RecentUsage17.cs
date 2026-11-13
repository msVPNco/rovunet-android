using System.Windows;
using System.Windows.Controls;
using Rovunet.Core;
namespace Rovunet.Desktop;
public sealed partial class MainWindow
{
    bool usageOrderPending17;
    void RecordConnectedUse17(Profile profile)
    {
        var previous=data.RecentProfileIds;
        var existing=data.Profiles.Select(p=>p.Id).ToHashSet();
        data.RecentProfileIds=data.RecentProfileIds.Where(id=>id!=profile.Id&&existing.Contains(id)).Prepend(profile.Id).Distinct().ToList();
        try{Save();}catch{data.RecentProfileIds=previous;lastError="PRIVATE_STORE_FAILED";}
        usageOrderPending17=true;
    }
    void ApplyUsageOrder17()
    {
        if(!usageOrderPending17)return;usageOrderPending17=false;
        var rank=data.RecentProfileIds.Select((id,index)=>(id,index)).ToDictionary(x=>x.id,x=>x.index);
        int Rank(Profile p)=>rank.GetValueOrDefault(p.Id,int.MaxValue);
        var byTile=rows.ToDictionary(r=>r.Tile,r=>r.Profile);
        foreach(var group in groups){
            var ordered=group.Paths.Children.Cast<Border>().OrderByDescending(tile=>state.State=="connected"&&activeProfile?.Id==byTile[tile].Id).ThenBy(tile=>Rank(byTile[tile]))
                .ThenByDescending(tile=>data.PinnedProfileIds.Contains(byTile[tile].Id)).ToArray();
            for(int i=0;i<ordered.Length;i++)if(group.Paths.Children.IndexOf(ordered[i])!=i){group.Paths.Children.Remove(ordered[i]);group.Paths.Children.Insert(i,ordered[i]);}
        }
        // A single click never changes history/order; promote after connection.
        var byCard=groups.ToDictionary(g=>g.Card);
        var orderedGroups=cards.Children.Cast<UIElement>().Where(e=>e is Border b&&byCard.ContainsKey(b)).Cast<Border>()
            .OrderByDescending(card=>state.State=="connected"&&byCard[card].Members.Any(p=>p.Id==activeProfile?.Id)).ThenBy(card=>byCard[card].Members.Min(Rank)).ToArray();
        for(int i=0;i<orderedGroups.Length;i++)if(cards.Children.IndexOf(orderedGroups[i])!=i+1){cards.Children.Remove(orderedGroups[i]);cards.Children.Insert(i+1,orderedGroups[i]);}
    }
}
