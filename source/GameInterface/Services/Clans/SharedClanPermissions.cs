using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans;

public interface ISharedClanPermissions
{
    bool CanManageClan(Hero actor, Clan clan);
    bool CanManageParty(Hero actor, MobileParty party);
    bool CanAssignRoles(Hero actor, MobileParty party);
}

public class SharedClanPermissions : ISharedClanPermissions
{
    public static bool CanManageClan(Clan clan)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanManageClan(Hero.MainHero, clan);
    }

    public static bool CanManageParty(MobileParty party)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanManageParty(Hero.MainHero, party);
    }

    public static bool CanAssignRoles(MobileParty party)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanAssignRoles(Hero.MainHero, party);
    }

    public bool CanManageClan(Hero actor, Clan clan)
    {
        return actor != null && clan != null && actor.Clan == clan && clan.Leader == actor;
    }

    public bool CanManageParty(Hero actor, MobileParty party)
    {
        if (party == null || party.IsPlayerParty()) return false;

        var clan = party.IsGarrison ? party.HomeSettlement?.OwnerClan : party.ActualClan;
        return CanManageClan(actor, clan);
    }

    public bool CanAssignRoles(Hero actor, MobileParty party)
    {
        return actor != null && party != null && party.LeaderHero == actor;
    }
}
