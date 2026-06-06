using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// The game has attempted to change party behavior.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
internal readonly struct PartyBehaviorChangeAttempted : IEvent
{
    public readonly MobilePartyAi PartyAi;
    public readonly AiBehavior NewAiBehavior;
    public readonly IInteractablePoint InteractablePoint;
    public readonly CampaignVec2 BestTargetPoint;

    public PartyBehaviorChangeAttempted(MobilePartyAi partyAi, AiBehavior newAiBehavior, IInteractablePoint interactablePoint, CampaignVec2 bestTargetPoint)
    {
        PartyAi = partyAi;
        NewAiBehavior = newAiBehavior;
        InteractablePoint = interactablePoint;
        BestTargetPoint = bestTargetPoint;
    }
}