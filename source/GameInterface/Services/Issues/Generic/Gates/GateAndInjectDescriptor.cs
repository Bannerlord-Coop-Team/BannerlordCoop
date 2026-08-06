using System;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Gates;

/// <summary>Common shape every gate descriptor exposes to its own bespoke <c>[HarmonyPrefix]</c> shell -
/// <c>Prefix</c> returns whether the real vanilla body should still run, matching Harmony's own
/// <c>bool</c>-return prefix convention exactly.</summary>
public interface IGateDescriptor<TQuest>
{
    bool Prefix(TQuest instance);
}

/// <summary>
/// Ground truth: <c>LordWantsRivalCapturedOwnershipGatePatches.PlayerDeliveredPrisonerQuestSuccessPrefix</c>. Its
/// shape: (1) gate on ownership exactly like <see cref="GateKind.OwnerOnlyMethodGate"/>; (2) if owner AND
/// <paramref name="InjectionCondition"/> holds, proactively run a forward that must be enqueued BEFORE the real
/// vanilla body's own trailing side effect, guaranteeing correct message-arrival order since every message this
/// project sends is <c>DeliveryMethod.ReliableOrdered</c>; (3) return true and let the real vanilla body run
/// unmodified.
///
/// Why this can't reuse <see cref="IQuestBespokeModule{TIssue,TQuest}"/>'s hooks: every one of those hooks fires
/// from a generic postfix on a shared choke point AFTER some already-completed lifecycle transition. None are
/// positioned to run synchronously INSIDE one specific method's own Harmony frame, between "the gate check
/// passed" and "the real body executes" - which is exactly what the message-ordering guarantee here depends on.
/// </summary>
public sealed record GateAndInjectDescriptor<TQuest>(
    Func<TQuest, Hero> QuestGiverSelector,
    Action<TQuest> PreBodyInjection,
    Func<TQuest, bool> InjectionCondition = null
) : IGateDescriptor<TQuest> where TQuest : QuestBase
{
    public bool Prefix(TQuest instance)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(QuestGiverSelector(instance))) return false;
        if (InjectionCondition == null || InjectionCondition(instance)) PreBodyInjection(instance);
        return true;
    }
}
