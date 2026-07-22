using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Extensions;

public static class MapEventExtensions
{
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
}
