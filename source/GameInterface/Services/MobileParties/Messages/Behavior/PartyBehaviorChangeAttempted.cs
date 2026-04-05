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
internal record PartyBehaviorChangeAttempted : IEvent
{
    public MobilePartyAi PartyAi { get; }
    public AiBehavior NewAiBehavior { get; }
    public IInteractablePoint InteractablePoint { get; }
    public CampaignVec2 BestTargetPoint { get; }

    public PartyBehaviorChangeAttempted(MobilePartyAi partyAi, AiBehavior newAiBehavior, IInteractablePoint interactablePoint, CampaignVec2 bestTargetPoint)
    {
        PartyAi = partyAi;
        NewAiBehavior = newAiBehavior;
        InteractablePoint = interactablePoint;
        BestTargetPoint = bestTargetPoint;
    }
}