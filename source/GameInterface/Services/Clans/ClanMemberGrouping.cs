using GameInterface.Services.Players;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans;

public interface IClanMemberGrouping : IGameAbstraction
{
    ClanMemberGroup GetGroup(Hero member, Hero viewer);
}

public class ClanMemberGrouping : IClanMemberGrouping
{
    private readonly IPlayerManager playerManager;

    public ClanMemberGrouping(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    public ClanMemberGroup GetGroup(Hero member, Hero viewer)
    {
        if (member == viewer || playerManager.Contains(member)) return ClanMemberGroup.Players;

        var ancestors = GetAncestors(viewer);
        if (ancestors.Overlaps(GetAncestors(member)) ||
            ancestors.Overlaps(GetAncestors(member.Spouse)) ||
            GetAncestors(viewer.Spouse).Overlaps(GetAncestors(member)))
        {
            return ClanMemberGroup.Family;
        }

        return ClanMemberGroup.OtherFamilies;
    }

    private HashSet<Hero> GetAncestors(Hero hero)
    {
        var ancestors = new HashSet<Hero>();
        var pending = new Stack<Hero>();
        pending.Push(hero);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current == null || !ancestors.Add(current)) continue;
            pending.Push(current.Father);
            pending.Push(current.Mother);
        }

        return ancestors;
    }
}

public enum ClanMemberGroup
{
    Family,
    Players,
    OtherFamilies,
}
