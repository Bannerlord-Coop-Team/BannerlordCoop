using GameInterface.Services.PartyBases.Extensions;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Extensions;

public static class MapEventExtensions
{
    /// <summary>Whether this map event uses the siege-style join flow.</summary>
    public static bool IsSiegeStyleJoin(this MapEvent mapEvent)
        => mapEvent.IsSiegeAssault || mapEvent.IsSallyOut;

    /// <summary>Whether the battle's results were calculated or applied, after which no battle report may change what they read.</summary>
    public static bool HasCalculatedResults(this MapEvent mapEvent)
        => mapEvent._mapEventResultsCalculated || mapEvent._mapEventResultsApplied;

    /// <summary>Whether the battle is a send troops hideout raid.</summary>
    public static bool IsSendTroopsHideout(this MapEvent mapEvent)
        => mapEvent.Component is HideoutEventComponent { IsSendTroops: true };

    /// <summary>Whether a player on either side waits for the server's results instead of applying its own: hideout and naval battles.</summary>
    public static bool WaitsForServerResults(this MapEvent mapEvent)
        => mapEvent.IsHideoutBattle || mapEvent.IsNavalMapEvent;

    /// <summary>
    /// Finds a party's MapEventParty on either side of the event, without going through PartiesOnSide(side) —
    /// safe to call even after the party's own MapEventSide has already been nulled elsewhere.
    /// </summary>
    public static MapEventParty FindMapEventParty(this MapEvent mapEvent, PartyBase party)
        => mapEvent.FindMapEventParty(party, out _);

    /// <summary>
    /// Overload that also returns the side the party was found on, so callers can read side-level figures
    /// (e.g. total contribution) without re-deriving PlayerSide from the now-nulled MapEventSide.
    /// </summary>
    public static MapEventParty FindMapEventParty(this MapEvent mapEvent, PartyBase party, out MapEventSide side)
    {
        foreach (var mapEventSide in mapEvent._sides)
        {
            if (mapEventSide == null) continue;

            foreach (var mapEventParty in mapEventSide.Parties)
            {
                if (mapEventParty?.Party == party)
                {
                    side = mapEventSide;
                    return mapEventParty;
                }
            }
        }

        side = null;
        return null;
    }

    /// <summary>
    /// The party's battle contribution rate from its own side's total, mirroring GetPlayerBattleContributionRate
    /// but without PartiesOnSide(PlayerSide) — safe once a teardown has nulled the party's MapEventSide.
    /// </summary>
    public static float GetPartyContributionRate(this MapEventSide side, MapEventParty mapEventParty)
    {
        if (side == null || mapEventParty == null) return 0f;

        int total = side.CalculateTotalContribution();
        return total == 0 ? 0f : (float)mapEventParty.ContributionToBattle / total;
    }

    public static MBReadOnlyList<MapEventParty> GetPlayerPartiesOnSide(this MapEvent mapEvent, BattleSideEnum battleSideEnum)
    {
        var playerParties = new MBReadOnlyList<MapEventParty>();
        foreach (var mapEventParty in mapEvent.PartiesOnSide(battleSideEnum))
        {
            if (mapEventParty.Party?.IsPlayerParty() == true)
            {
                playerParties.Add(mapEventParty);
            }
        }
        return playerParties;
    }
}
