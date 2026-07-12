using GameInterface.Services.MobileParties.Handlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Data;

/// <summary>
/// Identifies how a replicated behavior interactable reference should be resolved.
/// </summary>
public enum BehaviorInteractableKind
{
    PartyBase = 0,
    AnchorPoint = 1,
}

/// <summary>
/// Contains the data used for <see cref="MobilePartyAi"/> behavior synchronisation.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
[ProtoContract(SkipConstructor = true)]
public struct PartyBehaviorUpdateData
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly AiBehavior NewAiBehavior;

    [ProtoMember(3)]
    public readonly string InteractablePointId;

    [ProtoMember(4)]
    public readonly CampaignVec2 BestTargetPoint;

    [ProtoMember(5)]
    public readonly bool HasTarget;

    [ProtoMember(6)]
    public CampaignVec2 PartyPosition { get; set; }

    [ProtoMember(7)]
    public readonly AiBehavior DefaultBehavior;

    [ProtoMember(8)]
    public readonly CampaignVec2 TargetPosition;

    [ProtoMember(9)]
    public readonly MobileParty.NavigationType DesiredAiNavigationType;

    // Server-authored updates leave the origin null.
    [ProtoMember(10)]
    public string OriginControllerId { get; set; }

    [ProtoMember(11)]
    public bool ForcePosition { get; set; }

    [ProtoMember(12)]
    public string TargetPartyId { get; set; }

    [ProtoMember(13)]
    public string TargetSettlementId { get; set; }

    [ProtoMember(14)]
    public CampaignVec2 MoveTargetPoint { get; set; }

    [ProtoMember(15)]
    public bool IsTargetingPort { get; set; }

    [ProtoMember(16)]
    public MoveModeType PartyMoveMode { get; set; }

    [ProtoMember(17)]
    public string MoveTargetPartyId { get; set; }

    [ProtoMember(18)]
    public CampaignVec2 NextTargetPosition { get; set; }

    [ProtoMember(19)]
    public BehaviorInteractableKind InteractableKind { get; set; }

    public PartyBehaviorUpdateData(
        string mobilePartyId,
        AiBehavior newAiBehavior,
        string interactablePointId,
        CampaignVec2 bestTargetPoint,
        bool hasTarget,
        CampaignVec2 partyPosition,
        AiBehavior defaultBehavior,
        CampaignVec2 targetPosition,
        MobileParty.NavigationType desiredAiNavigationType)
    {
        MobilePartyId = mobilePartyId;
        NewAiBehavior = newAiBehavior;
        InteractablePointId = interactablePointId;
        BestTargetPoint = bestTargetPoint;
        HasTarget = hasTarget;
        PartyPosition = partyPosition;
        DefaultBehavior = defaultBehavior;
        TargetPosition = targetPosition;
        DesiredAiNavigationType = desiredAiNavigationType;
        OriginControllerId = null;
        ForcePosition = false;
        TargetPartyId = null;
        TargetSettlementId = null;
        MoveTargetPoint = targetPosition;
        IsTargetingPort = false;
        PartyMoveMode = MoveModeType.Hold;
        MoveTargetPartyId = null;
        NextTargetPosition = targetPosition;
        InteractableKind = BehaviorInteractableKind.PartyBase;
    }
}
