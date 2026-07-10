using Common.Logging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Abstracts interacting with the MobileParty class in game
/// </summary>
public interface IMobilePartyInterface : IGameAbstraction
{
    /// <summary>
    /// Current behavior of every registered, server-simulated party — the join-time
    /// snapshot a fresh client needs. MobileParty.DefaultBehavior is NOT a saveable
    /// property, so the transferred save carries no behaviors; the server otherwise
    /// only broadcasts behavior CHANGES (and suppresses same-value re-sends), so
    /// parties whose behavior stays stable after a join — hideout bandits above all
    /// — would remain behaviorless and frozen on that client forever.
    /// </summary>
    List<PartyBehaviorUpdateData> GetBehaviorSnapshot();
}

internal class MobilePartyInterface : IMobilePartyInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyInterface>();

    private readonly IObjectManager objectManager;

    public MobilePartyInterface(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public List<PartyBehaviorUpdateData> GetBehaviorSnapshot()
    {
        var result = new List<PartyBehaviorUpdateData>();
        var campaign = Campaign.Current;
        if (campaign?.MobileParties == null) return result;

        foreach (var party in campaign.MobileParties)
        {
            if (party?.Ai == null || !party.IsActive) continue;

            // Player-controlled parties are owner-simulated; their owners stream
            // their own behavior.
            if (!party.IsControlledByThisInstance()) continue;

            if (!objectManager.TryGetId(party, out var partyId)) continue;

            var interactablePoint = party.Ai._aiBehaviorInteractable;
            string interactablePointId = null;
            if (interactablePoint is PartyBase partyBase &&
                !objectManager.TryGetId(partyBase, out interactablePointId)) continue;

            result.Add(new PartyBehaviorUpdateData(
                partyId,
                party.ShortTermBehavior,
                interactablePointId,
                party.Ai.BehaviorTarget,
                interactablePoint is not null,
                party.Position,
                party.DefaultBehavior,
                party.TargetPosition,
                party.DesiredAiNavigationType));
        }

        return result;
    }
}
