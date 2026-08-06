using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.Gates;

/// <summary>
/// <see cref="GateKind.OwnerOnlyMethodGate"/> spec: gates an entire method body to the recorded local-peer
/// owner, exactly like <c>VillageNeedsCraftingMaterialsQuestOwnershipGatePatch</c>/
/// <c>VillageNeedsCraftingMaterialsAlternativeSolutionOwnershipGatePatch</c> do today by hand. A Harmony Prefix
/// calls <see cref="OwnershipGateRunner.IsOwner{TInstance}"/> with one of these instead of re-deriving the
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> call at every gate site.
/// </summary>
public sealed record OwnershipGateSpec<TInstance>(Func<TInstance, Hero> QuestGiverSelector);
