using GameInterface.Services.MobileParties.Extensions;
using System;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
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

    /// <summary>
    /// Reads native <see cref="PlayerEncounter.EncounteredBattle"/>, which dereferences the encountered party and
    /// throws while an encounter is still being set up and after it closes.
    /// </summary>
    /// <returns>False instead of throwing when there is no encounter or no encountered battle.</returns>
    public static bool TryGetEncounteredBattle(out MapEvent battle)
    {
        battle = null;
        if (PlayerEncounter.Current?._encounteredParty == null) return false;

        try
        {
            battle = PlayerEncounter.EncounteredBattle;
        }
        catch (NullReferenceException)
        {
            return false;
        }

        return battle != null;
    }
}
