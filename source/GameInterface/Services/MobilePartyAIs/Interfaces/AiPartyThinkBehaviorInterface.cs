using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    // Takes the player registry and the object registry used to find each player's party.
    public AiPartyThinkBehaviorInterface(IPlayerManager playerManager, IObjectManager objectManager)
    {
        this.playerManager = playerManager;
        this.objectManager = objectManager;
    }

    // Runs the hold around every player party in the clan, since players can share a clan.
    public void HoldPartiesChasingPlayerParties(Clan clan)
    {
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty)) continue;
            if (playerParty.ActualClan != clan) continue;

            HoldPartiesChasing(playerParty, clan.MapFaction);
        }
    }

    // Vanilla's AiPartyThinkBehavior.OnClanChangedKingdom loop with the main party swapped for a player party.
    private static void HoldPartiesChasing(MobileParty playerParty, IFaction playerFaction)
    {
        float radius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 4f;
        var searchData = MobileParty.StartFindingLocatablesAroundPosition(playerParty.Position.ToVec2(), radius);
        for (var party = MobileParty.FindNextLocatable(ref searchData); party != null; party = MobileParty.FindNextLocatable(ref searchData))
        {
            if (party.MapFaction.IsAtWarWith(playerFaction)) continue;
            if (party.ShortTermTargetParty != playerParty) continue;
            if (party.IsCurrentlyUsedByAQuest) continue;
            if (!party.MapFaction.IsKingdomFaction && !party.IsLordParty) continue;

            party.SetMoveModeHold();
        }
    }
}
