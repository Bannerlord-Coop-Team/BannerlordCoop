using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Updates <see cref="MobilePartyAi"/> behavior on the campaign map.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
[ProtoContract]
public struct UpdatePartyBehavior : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyAiId;

    [ProtoMember(2)]
    public readonly AiBehavior NewAiBehavior;

    [ProtoMember(3)]
    public readonly string InteractablePointId;

    [ProtoMember(4)]
    public readonly CampaignVec2 BestTargetPoint;

    [ProtoMember(5)]
    public readonly bool HasTarget;

    public UpdatePartyBehavior(string mobilePartyAiId, AiBehavior newAiBehavior, string interactablePointId, CampaignVec2 bestTargetPoint, bool hasTarget)
    {
        MobilePartyAiId = mobilePartyAiId;
        NewAiBehavior = newAiBehavior;
        InteractablePointId = interactablePointId;
        BestTargetPoint = bestTargetPoint;
        HasTarget = hasTarget;
    }
}

/// <summary>
/// Notifies that PartyBehavior was updated
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[BatchLogMessage]
public struct PartyBehaviorUpdated : IEvent
{
    public PartyBehaviorUpdateData BehaviorUpdateData { get; }
    public PartyBehaviorUpdated(ref PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}