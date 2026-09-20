using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
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

        if (siegeEvent.BesiegedSettlement == null) return false;

        return HasPlayerInvolvedDefender(siegeEvent, side);
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
