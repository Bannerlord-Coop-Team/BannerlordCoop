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

            HoldPartiesChasing(playerParty, FindNearbyParties(playerParty));
        }
    }

    // Collects the parties in the radius vanilla searches around the main party.
    private static List<MobileParty> FindNearbyParties(MobileParty playerParty)
    {
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 4f;
        var searchData = MobileParty.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), radius);
        var nearbyParties = new List<MobileParty>();

        for (var party = MobileParty.FindNextLocatable(ref searchData);
             party != null;
             party = MobileParty.FindNextLocatable(ref searchData))
        {
            nearbyParties.Add(party);
        }

        return nearbyParties;
    }

    // Vanilla's AiPartyThinkBehavior.OnClanChangedKingdom filter with the main party swapped for a player party.
    private static void HoldPartiesChasing(MobileParty playerParty, IEnumerable<MobileParty> nearbyParties)
    {
        foreach (var party in nearbyParties)
        {
            if (party.MapFaction.IsAtWarWith(playerParty.MapFaction)) continue;
            if (party.ShortTermTargetParty != playerParty) continue;
            if (party.IsCurrentlyUsedByAQuest) continue;
            if (!party.MapFaction.IsKingdomFaction && !party.IsLordParty) continue;

            party.SetMoveModeHold();
        }
    }
}
