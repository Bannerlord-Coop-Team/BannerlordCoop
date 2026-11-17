using GameInterface.Services.MobileParties.Extensions;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Interfaces;

/// <summary>
/// Runs the player party reactions of <c>AiPartyThinkBehavior</c> for co-op players instead of the main party.
/// </summary>
public interface IAiPartyThinkBehaviorInterface : IGameAbstraction
{
    /// <summary>
    /// Server method. Puts nearby lord parties on hold that were chasing a player party of the clan and are
    /// no longer at war with it, like vanilla does around the main party when the player clan changes kingdom.
    /// </summary>
    void HoldPartiesChasingPlayerParties(Clan clan);
}

/// <inheritdoc cref="IAiPartyThinkBehaviorInterface"/>
public class AiPartyThinkBehaviorInterface : IAiPartyThinkBehaviorInterface
{
    // Runs the hold around every player party in the clan, since players can share a clan.
    public void HoldPartiesChasingPlayerParties(Clan clan)
    {
        foreach (var warPartyComponent in clan.WarPartyComponents)
        {
            var playerParty = warPartyComponent.MobileParty;
            if (!playerParty.IsPlayerParty()) continue;

            foreach (var party in FindNearbyParties(playerParty))
            {
                if (ShouldHoldChaser(party, playerParty)) party.SetMoveModeHold();
            }
        }
    }

    // Walks the parties in the radius vanilla searches around the main party.
    private static IEnumerable<MobileParty> FindNearbyParties(MobileParty playerParty)
    {
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 4f;
        var searchData = MobileParty.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), radius);

        for (var party = MobileParty.FindNextLocatable(ref searchData);
             party != null;
             party = MobileParty.FindNextLocatable(ref searchData))
        {
            yield return party;
        }
    }

    // Vanilla's AiPartyThinkBehavior.OnClanChangedKingdom filter with the main party swapped for a player party.
    // Another player chasing it is left alone, because its client drives its movement.
    internal static bool ShouldHoldChaser(MobileParty party, MobileParty playerParty)
    {
        if (party.IsPlayerParty()) return false;
        if (party.MapFaction.IsAtWarWith(playerParty.MapFaction)) return false;
        if (party.ShortTermTargetParty != playerParty) return false;
        if (party.IsCurrentlyUsedByAQuest) return false;

        return party.MapFaction.IsKingdomFaction || party.IsLordParty;
    }
}
