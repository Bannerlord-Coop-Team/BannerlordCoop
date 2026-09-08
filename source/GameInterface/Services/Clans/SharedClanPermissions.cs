using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans;

public interface ISharedClanPermissions : IGameAbstraction
{
    bool CanManageClan(Hero actor, Clan clan);
    bool CanManageParty(Hero actor, MobileParty party);
    bool CanAssignRoles(Hero actor, MobileParty party);
    bool CanManageHero(Hero actor, Hero hero);
    bool CanRecallHero(Hero actor, Hero hero);
    bool CanRenameHero(Hero actor, Hero hero);
}

public class SharedClanPermissions : ISharedClanPermissions
{
    private readonly IClanMemberGrouping grouping;

    public SharedClanPermissions(IClanMemberGrouping grouping)
    {
        this.grouping = grouping;
    }

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

    public static bool CanRenameHero(Hero hero)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanRenameHero(Hero.MainHero, hero);
    }

    public static bool CanManageHero(Hero hero)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanManageHero(Hero.MainHero, hero);
    }

    public static bool CanRecallHero(Hero hero)
    {
        return ContainerProvider.TryResolve<ISharedClanPermissions>(out var permissions) &&
            permissions.CanRecallHero(Hero.MainHero, hero);
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

    public bool CanManageHero(Hero actor, Hero hero)
    {
        if (actor == null || hero == null || actor.Clan == null || hero.Clan != actor.Clan) return false;
        if (hero == actor) return true;
        if (hero.IsPlayerHero()) return false;

        var party = hero.PartyBelongedTo;
        if (party != null && party == actor.PartyBelongedTo) return true;

        return CanManageClan(actor, hero.Clan) && (party == null || !party.IsPlayerParty());
    }

    public bool CanRecallHero(Hero actor, Hero hero)
    {
        if (actor == null || hero == null || actor.Clan == null || hero.Clan != actor.Clan ||
            hero == actor || hero.IsPlayerHero()) return false;

        return CanManageClan(actor, hero.Clan)
            ? CanManageHero(actor, hero)
            : hero.PartyBelongedTo == null && hero.GovernorOf == null;
    }

    public bool CanRenameHero(Hero actor, Hero hero)
    {
        return CanManageHero(actor, hero) &&
            actor.Clan.AliveLords.Contains(hero) &&
            (hero == actor || grouping.GetGroup(hero, actor) == ClanMemberGroup.Family);
    }
}
