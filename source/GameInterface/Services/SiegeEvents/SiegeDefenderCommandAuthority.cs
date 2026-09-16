using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents;

public interface ISiegeDefenderCommandAuthority
{
    bool HasPlayerDefender(SiegeEvent siegeEvent, BattleSideEnum side);
}

public class SiegeDefenderCommandAuthority : ISiegeDefenderCommandAuthority
{
    private readonly IPlayerManager playerManager;

    public SiegeDefenderCommandAuthority(IPlayerManager playerManager)
    {
        if (playerManager == null) throw new ArgumentNullException(nameof(playerManager));

        this.playerManager = playerManager;
    }

    public bool HasPlayerDefender(SiegeEvent siegeEvent, BattleSideEnum side)
    {
        if (siegeEvent == null) return false;
        if (side != BattleSideEnum.Defender) return false;

        var settlement = siegeEvent.BesiegedSettlement;
        if (settlement == null) return false;

        if (HasPlayerInvolvedDefender(siegeEvent, side)) return true;

        var town = settlement.Town;
        if (town == null) return false;

        try
        {
            foreach (var defender in town.GetDefenderParties(MapEvent.BattleTypes.Siege))
            {
                if (defender == null) continue;
                if (defender.LeaderHero != null && playerManager.Contains(defender.LeaderHero)) return true;
                if (defender.MobileParty != null && playerManager.Contains(defender.MobileParty)) return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private bool HasPlayerInvolvedDefender(SiegeEvent siegeEvent, BattleSideEnum side)
    {
        ISiegeEventSide siegeSide = null;
        try
        {
            siegeSide = siegeEvent.GetSiegeEventSide(side);
        }
        catch (Exception)
        {
            return false;
        }

        if (siegeSide == null) return false;

        IEnumerable<PartyBase> involved = null;
        try
        {
            involved = siegeSide.GetInvolvedPartiesForEventType();
        }
        catch (Exception)
        {
            return false;
        }

        if (involved == null) return false;

        try
        {
            foreach (var party in involved)
            {
                if (party == null) continue;
                if (party.LeaderHero != null && playerManager.Contains(party.LeaderHero)) return true;
                if (party.MobileParty != null && playerManager.Contains(party.MobileParty)) return true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }
}
