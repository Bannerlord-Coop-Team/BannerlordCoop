using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Commands;

internal interface IDefenderFixtureBehaviorIdentity
{
    DefenderFixtureBehaviorReferences Capture(MobileParty party, PartyBehaviorUpdateData behavior);
    bool IsCurrent(DefenderFixtureBehaviorReferences captured);
}

internal sealed class DefenderFixtureBehaviorIdentity : IDefenderFixtureBehaviorIdentity
{
    private readonly IObjectManager objects;

    public DefenderFixtureBehaviorIdentity(IObjectManager objects) => this.objects = objects;

    public DefenderFixtureBehaviorReferences Capture(MobileParty party, PartyBehaviorUpdateData behavior) =>
        new DefenderFixtureBehaviorReferences(party, behavior);

    public bool IsCurrent(DefenderFixtureBehaviorReferences captured)
    {
        if (captured == null) return false;
        var data = captured.Behavior;
        if (!HasIdentity(data.TargetPartyId, captured.TargetParty) ||
            !HasIdentity(data.TargetSettlementId, captured.TargetSettlement) ||
            !HasIdentity(data.MoveTargetPartyId, captured.MoveTargetParty)) return false;
        if (data.InteractablePointId == null) return captured.Interactable == null;
        if (!data.IsInteractableAnchor)
            return captured.Interactable is PartyBase expected && HasIdentity(data.InteractablePointId, expected);
        return captured.Interactable is AnchorPoint anchor &&
            HasIdentity(data.InteractablePointId, captured.AnchorOwner) &&
            ReferenceEquals(captured.AnchorOwner?.Anchor, anchor) && ReferenceEquals(anchor.Owner, captured.AnchorOwner);
    }

    private bool HasIdentity<T>(string id, T expected) where T : class =>
        id == null ? expected == null : expected != null &&
            objects.TryGetObject<T>(id, out var actual) && ReferenceEquals(actual, expected);
}

internal sealed class DefenderFixtureBehaviorReferences
{
    public PartyBehaviorUpdateData Behavior { get; }
    public IInteractablePoint Interactable { get; }
    public MobileParty AnchorOwner { get; }
    public MobileParty TargetParty { get; }
    public Settlement TargetSettlement { get; }
    public MobileParty MoveTargetParty { get; }

    public DefenderFixtureBehaviorReferences(MobileParty party, PartyBehaviorUpdateData behavior)
    {
        Behavior = behavior;
        Interactable = behavior.InteractablePointId == null ? null : party.Ai?.AiBehaviorInteractable;
        AnchorOwner = (Interactable as AnchorPoint)?.Owner;
        TargetParty = behavior.TargetPartyId == null ? null : party.TargetParty;
        TargetSettlement = behavior.TargetSettlementId == null ? null : party.TargetSettlement;
        MoveTargetParty = behavior.MoveTargetPartyId == null ? null : party.MoveTargetParty;
    }
}
