using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// The behavior of a controlled party has been updated.
/// Must be confirmed by the server before the change is applied.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
[ProtoContract]
public readonly struct ControlledPartyBehaviorUpdated : IEvent
{
    [ProtoMember(1)]
    public readonly string MobilePartyAiId;

    [ProtoMember(2)]
    public readonly AiBehavior NewAiBehavior;

    [ProtoMember(3)]
    public readonly string InteractablePointId;

    [ProtoMember(4)]
    public readonly CampaignVec2 BestTargetPoint;

    public ControlledPartyBehaviorUpdated(string mobilePartyAiId, AiBehavior newAiBehavior, string interactablePointId, CampaignVec2 bestTargetPoint)
    {
        MobilePartyAiId = mobilePartyAiId;
        NewAiBehavior = newAiBehavior;
        InteractablePointId = interactablePointId;
        BestTargetPoint = bestTargetPoint;
    }
}