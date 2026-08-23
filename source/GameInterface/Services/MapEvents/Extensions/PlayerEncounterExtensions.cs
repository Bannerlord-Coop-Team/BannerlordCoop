using GameInterface.Services.MobileParties.Extensions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Extensions;

public static class PlayerEncounterExtensions
{
    /// <summary>
    /// Checks if a client's current PlayerEncounter includes any other players.
    /// </summary>
    public static bool IsSoloEncounter(this PlayerEncounter playerEncounter)
    {
        if (playerEncounter._mapEvent == null) return false;

        foreach (var mapEventSide in playerEncounter._mapEvent._sides)
        {
            if (mapEventSide == null) continue;

            foreach (var mapEventParty in mapEventSide.Parties)
            {
                if (mapEventParty?.Party?.MobileParty == null) continue;

                if (mapEventParty.Party.MobileParty.IsPlayerParty()
                    && mapEventParty.Party.MobileParty != MobileParty.MainParty)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
