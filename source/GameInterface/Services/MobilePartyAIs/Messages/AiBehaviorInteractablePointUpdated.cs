using Common.Messaging;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs.Messages;

public readonly struct AiBehaviorInteractablePointUpdated : IEvent
{
    public readonly MobilePartyAi PartyAi;
    public readonly IInteractablePoint InteractablePoint;

    public AiBehaviorInteractablePointUpdated(MobilePartyAi partyAi, IInteractablePoint interactablePoint)
    {
        PartyAi = partyAi;
        InteractablePoint = interactablePoint;
    }
}
