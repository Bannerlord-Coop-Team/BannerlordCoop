using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Barters;

internal static class SafePassagePartyResolver
{
    // Resolves the parties native PlayerEncounter would include without
    // requiring a client-side encounter on the dedicated server.
    internal static (
        List<MobileParty> PlayerSide,
        List<MobileParty> OpponentSide) Resolve(
        MobileParty playerParty,
        MobileParty encounteredParty)
    {
        var radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
        if (playerParty.SiegeEvent != null || encounteredParty.SiegeEvent != null)
        {
            radius = Campaign.Current.Models.MobilePartyAIModel
                .SettlementDefendingWaitingPositionRadius * 1.25f;
        }
        var playerPosition = playerParty.Position.ToVec2();
        var searchData = MobileParty.StartFindingLocatablesAroundPosition(playerPosition, radius);
        var nearbyParties = new List<MobileParty>();

        for (var party = MobileParty.FindNextLocatable(ref searchData);
             party != null;
             party = MobileParty.FindNextLocatable(ref searchData))
        {
            nearbyParties.Add(party);
        }

        return ResolveFromCandidates(
            playerParty,
            encounteredParty,
            nearbyParties);
    }

    internal static (
        List<MobileParty> PlayerSide,
        List<MobileParty> OpponentSide) ResolveFromCandidates(
        MobileParty playerParty,
        MobileParty encounteredParty,
        IEnumerable<MobileParty> nearbyParties)
    {
        var playerSide = new List<MobileParty>();
        var opponentSide = new List<MobileParty>();
        var playerFaction = playerParty.MapFaction;
        var opponentFaction = encounteredParty.MapFaction;
        foreach (var party in nearbyParties)
        {
            if (!CanJoinEncounter(party, playerParty))
                continue;

            var partyFaction = party.MapFaction;
            if (partyFaction == null || playerFaction == null || opponentFaction == null)
                continue;

            var joinsPlayerSide =
                !partyFaction.IsAtWarWith(playerFaction) &&
                partyFaction.IsAtWarWith(opponentFaction) &&
                opponentSide.All(opponent =>
                    opponent.MapFaction?.IsAtWarWith(partyFaction) == true);
            if (joinsPlayerSide)
            {
                playerSide.Add(party);
            }

            var joinsOpponentSide =
                partyFaction.IsAtWarWith(playerFaction) &&
                !partyFaction.IsAtWarWith(opponentFaction) &&
                playerSide.All(ally =>
                    ally.MapFaction?.IsAtWarWith(partyFaction) == true);
            if (joinsOpponentSide)
            {
                opponentSide.Add(party);
            }
        }

        // Match DefaultEncounterModel: an ignored party suppresses reinforcements
        // for the opposing side.
        if (opponentSide.Any(party => party.ShouldBeIgnored))
            playerSide.Clear();
        if (playerSide.Any(party => party.ShouldBeIgnored))
            opponentSide.Clear();

        if (!playerSide.Contains(playerParty))
            playerSide.Add(playerParty);
        if (!opponentSide.Contains(encounteredParty))
            opponentSide.Add(encounteredParty);

        foreach (var party in playerSide.ToArray())
            AddPartyAndAttachments(playerSide, party);
        foreach (var party in opponentSide.ToArray())
            AddPartyAndAttachments(opponentSide, party);

        return (playerSide, opponentSide);
    }

    private static bool CanJoinEncounter(MobileParty party, MobileParty playerParty)
    {
        if (party == playerParty ||
            party.IsActive != true ||
            party.MapEvent != null ||
            party.SiegeEvent != null ||
            party.CurrentSettlement != null ||
            party.AttachedTo != null ||
            party.IsInRaftState ||
            party.IsCurrentlyAtSea != playerParty.IsCurrentlyAtSea)
        {
            return false;
        }

        return party.IsLordParty ||
               party.IsBandit ||
               party.IsPatrolParty ||
               party.ShouldJoinPlayerBattles;
    }

    private static void AddPartyAndAttachments(ICollection<MobileParty> parties, MobileParty party)
    {
        if (party == null) return;
        if (!parties.Contains(party))
            parties.Add(party);

        if (party.AttachedParties == null) return;
        foreach (var attachedParty in party.AttachedParties)
        {
            if (attachedParty?.IsActive == true && !parties.Contains(attachedParty))
                parties.Add(attachedParty);
        }
    }
}
