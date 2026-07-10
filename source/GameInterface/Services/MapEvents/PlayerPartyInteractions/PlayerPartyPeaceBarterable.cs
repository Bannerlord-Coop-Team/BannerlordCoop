using System;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal class PlayerPartyPeaceBarterable : Barterable
{
    private readonly Hero otherHero;
    private readonly PartyBase otherParty;

    public override int MaxAmount => 1;
    public override TextObject Name => new TextObject("{=coop_player_party_peace_barterable}Peace");
    public override string StringID => "peace_barterable";

    public PlayerPartyPeaceBarterable(
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty) : base(ownerHero, ownerParty)
    {
        this.otherHero = otherHero;
        this.otherParty = otherParty;
    }

    public override void Apply()
    {
    }

    public override int GetUnitValueForFaction(IFaction faction)
    {
        if (faction == otherHero?.MapFaction || faction == otherParty?.MapFaction)
            return 1;

        return -1;
    }

    public override ImageIdentifier GetVisualIdentifier()
        => null;

    public static bool CanOfferPeace(PartyBase ownerParty, PartyBase otherParty)
    {
        if (ownerParty?.LeaderHero == null || otherParty?.LeaderHero == null)
            return false;

        var ownerFaction = GetMapFaction(ownerParty);
        var otherFaction = GetMapFaction(otherParty);
        if (ownerFaction == null || otherFaction == null || ownerFaction == otherFaction)
            return false;

        if (!IsFactionLeader(ownerParty.LeaderHero, ownerFaction))
            return false;

        if (!IsFactionLeader(otherParty.LeaderHero, otherFaction))
            return false;

        return AreHostile(ownerFaction, otherFaction);
    }

    public static bool AreHostile(IFaction faction, IFaction otherFaction)
    {
        if (faction == null || otherFaction == null || faction == otherFaction)
            return false;

        return VillageHostileFactionStanceHelper.HasWarStance(faction, otherFaction) ||
               IsAtWarAgainstFaction(faction, otherFaction) ||
               HasFactionWar(faction, otherFaction) ||
               HasFactionWar(otherFaction, faction);
    }

    public static IFaction GetMapFaction(PartyBase party)
        => party?.MapFaction?.MapFaction ?? party?.MapFaction;

    private static bool IsFactionLeader(Hero hero, IFaction faction)
    {
        if (hero == null || faction == null)
            return false;

        if (faction is Kingdom kingdom)
            return hero.IsKingdomLeader && hero.Clan == kingdom.RulingClan;

        if (faction is Clan clan)
            return clan.Leader == hero;

        return false;
    }

    private static bool IsAtWarAgainstFaction(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return FactionManager.IsAtWarAgainstFaction(faction, otherFaction);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static bool HasFactionWar(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return faction.FactionsAtWarWith?.Contains(otherFaction) == true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }
}