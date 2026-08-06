using Common;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.Dispatch;

/// <summary>
/// Registry-driven dispatch: ONE Harmony patch per vanilla hook point serves EVERY registered quest type.
/// <see cref="QuestTypeRegistry.Get(IssueBase)"/> returning null means "not migrated" - an unregistered type's
/// own issue instance simply never matches, so nothing here (or anywhere else in <c>Generic/</c>) ever touches
/// it; it keeps running whatever bespoke Interfaces/Messages/Handlers/Patches files it already had, completely
/// unaffected. This is what lets migrated and unmigrated quest types coexist without a flag-day cutover.
///
/// What varies per migrated type (which fields to capture, which concrete wire message to publish) stays
/// hand-written: each registered <see cref="QuestTypeDescriptor"/> supplies its own
/// <see cref="QuestTypeDescriptor.OnGenuineCreation"/>/<see cref="QuestTypeDescriptor.OnGenuineQuestSolutionAccept"/>/
/// <see cref="QuestTypeDescriptor.OnGenuineAlternativeAccept"/> delegate that this file's patches invoke once
/// dispatch confirms the type is registered. This file only makes the coexistence gate itself real.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeCreationTriggerPatch
{
    // A replayed creation on a client also reaches this postfix (Harmony runs postfixes regardless of which
    // branch let the prefix through), so gate on ModInformation rather than CallOriginalPolicy - only a genuine
    // server-side creation should proceed.
    // HarmonyPriority(First): a migrated type may also still be eligible for the older, unrelated generic
    // mechanism (GenericAcceptMirrorIssueTypes/IssueAcceptancePatches) via its own independent postfix on this
    // same vanilla method. That mechanism's downstream handling can synchronously round-trip through
    // network.SendAll (this project's test harness delivers it synchronously - see
    // TestNetworkRouter.SendAll), which recurses into another peer's own MessageBroker.Instance context before
    // control returns to this method's remaining postfixes. Running first guarantees this dispatch's own
    // local-message publish happens against the correct (genuinely accepting) peer's context, rather than after
    // such a recursive context switch.
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;

        var issue = issueOwner?.Issue;
        var descriptor = QuestTypeRegistry.Get(issue);
        descriptor?.OnGenuineCreation?.Invoke(issue);
    }
}

/// <summary>[Shape A trigger] Generalizes what every pre-migration bespoke "quest-solution accept capture"
/// Harmony patch already did (see e.g. the deleted <c>VillageNeedsCraftingMaterialsQuestAcceptancePatch</c>) -
/// deliberately never blocked, same reasoning as <c>Patches.IssueQuestAcceptancePatch</c>: this runs inline
/// inside a live one-to-one conversation.</summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeQuestSolutionAcceptTriggerPatch
{
    // See GenericQuestTypeCreationTriggerPatch's own Postfix doc comment for why First is required here too.
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // A mirror replay (server authoritative replay, or another peer applying a received broadcast) runs
        // under AllowedThread - skip re-publishing so it doesn't loop back through the network again.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;

        var descriptor = QuestTypeRegistry.Get(issueOwner?.Issue);
        if (descriptor?.OnGenuineQuestSolutionAccept == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineQuestSolutionAccept(issueOwner, controllerIdProvider?.ControllerId);
    }
}

/// <summary>[Shape B trigger] Generalizes what every pre-migration bespoke "alternative-solution accept capture"
/// Harmony patch already did (see e.g. the deleted <c>VillageNeedsCraftingMaterialsAlternativeAcceptancePatch</c>).</summary>
[HarmonyPatch(typeof(IssueBase))]
internal class GenericQuestTypeAlternativeAcceptTriggerPatch
{
    // See GenericQuestTypeCreationTriggerPatch's own Postfix comment for why First is required here too.
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.OnGenuineAlternativeAccept == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineAlternativeAccept(__instance.IssueOwner, controllerIdProvider?.ControllerId);
    }
}

/// <summary>
/// Applies the vanilla <c>IssueManager.DailyTick</c>/dedicated-host-no-<c>Hero.MainHero</c> ownership hazard to
/// every registered quest type automatically, instead of each type needing its own copy-pasted gate patch. Falls
/// through (returns true - letting the real body run, or any OTHER already-existing gate for this type decide)
/// for any unregistered issue type. Harmless if a registered type also still has its own pre-existing generic
/// gate elsewhere - Harmony ANDs multiple independent prefixes, and both would compute the identical ownership
/// boolean.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!QuestTypeRegistry.IsRegistered(__instance?.GetType())) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner);
    }
}
