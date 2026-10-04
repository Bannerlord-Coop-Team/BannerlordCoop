using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Patches;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

[HarmonyPatch(typeof(TheConquestOfSettlementIssueBehavior), "OnCheckForIssue")]
internal class ConquestIssueOfferPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch(typeof(Quest), MethodType.Constructor, typeof(string), typeof(Hero), typeof(Settlement), typeof(CampaignTime), typeof(int))]
internal class ConquestQuestIdentityPatch
{
    [HarmonyPostfix]
    private static void Postfix(Quest __instance, string questId)
    {
        using (new AllowedThread())
        {
            __instance.StringId = questId;
        }
    }
}

[HarmonyPatch]
internal class ConquestQuestEventPatches
{
    [ThreadStatic]
    internal static bool IsApplying;
    [ThreadStatic]
    internal static Hero TriggeringHero;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "OnWarDeclared", "OnClanChangedKingdom", "OnMapEventStarted", "OnSiegeCompleted", "OnSettlementOwnerChanged", "OnPeaceDeclared" })
            yield return AccessTools.DeclaredMethod(typeof(Quest), method);
    }

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, out (IDisposable Scope, bool WasApplying, Hero PreviousTrigger) __state)
    {
        __state = (null, IsApplying, TriggeringHero);
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        var triggeringHero = ResolvedMainHeroContext.ResolvedMainHero;
        if (ModInformation.IsClient || !__instance.IsOngoing ||
            !ContainerProvider.TryResolve<IConquestQuest>(out var service) ||
            !service.TryOpenOwnerScope(__instance.QuestGiver, out var scope)) return false;
        __state = (scope, IsApplying, TriggeringHero);
        IsApplying = true;
        TriggeringHero = triggeringHero;
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable Scope, bool WasApplying, Hero PreviousTrigger) __state)
    {
        IsApplying = __state.WasApplying;
        TriggeringHero = __state.PreviousTrigger;
        __state.Scope?.Dispose();
    }
}

[HarmonyPatch]
internal class ConquestQuestCompletionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { nameof(QuestBase.CompleteQuestWithSuccess), nameof(QuestBase.CompleteQuestWithFail), nameof(QuestBase.CompleteQuestWithCancel), nameof(QuestBase.CompleteQuestWithTimeOut), nameof(QuestBase.CompleteQuestWithBetrayal) })
            yield return AccessTools.DeclaredMethod(typeof(QuestBase), method);
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out (IDisposable OwnerScope, IssueFinalizeAuthorityGuard Guard) __state)
    {
        __state = default;
        if (__instance is not Quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!__instance.IsOngoing) return false;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed();
        // Cancellation has no owner reward or party action, including after those objects are gone.
        if (__originalMethod.Name == nameof(QuestBase.CompleteQuestWithCancel) &&
            ReferenceEquals(ConquestQuest.CancellingRemovedPlayerQuest, __instance))
        {
            __state = (null, new IssueFinalizeAuthorityGuard());
            return true;
        }
        if (!ContainerProvider.TryResolve<IConquestQuest>(out var service)) return false;
        if (ConquestPlayerChangedPatch.IsChangingPlayer &&
            !service.IsOwnedBy(__instance.QuestGiver, ConquestPlayerChangedPatch.OldPlayer) &&
            !service.IsOwnedBy(__instance.QuestGiver, ConquestPlayerChangedPatch.NewPlayer)) return false;
        if (!service.TryOpenOwnerScope(__instance.QuestGiver, out var scope)) return false;
        __state = (scope, new IssueFinalizeAuthorityGuard());
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable OwnerScope, IssueFinalizeAuthorityGuard Guard) __state)
    {
        __state.Guard?.Dispose();
        __state.OwnerScope?.Dispose();
    }
}

[HarmonyPatch(typeof(DiplomacyHelper), nameof(DiplomacyHelper.IsWarCausedByPlayer))]
internal class ConquestWarInitiatorPatch
{
    [HarmonyPostfix]
    private static void Postfix(DeclareWarAction.DeclareWarDetail declareWarDetail, ref bool __result)
    {
        if (ModInformation.IsClient || !ConquestQuestEventPatches.IsApplying ||
            declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility) return;
        __result = ConquestQuestEventPatches.TriggeringHero != null && ConquestQuestEventPatches.TriggeringHero == Hero.MainHero;
    }
}

[HarmonyPatch(typeof(QuestBase), "FinalizeQuest")]
internal class ConquestQuestJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance)
    {
        if (ModInformation.IsServer && !CallOriginalPolicy.IsOriginalAllowed() && __instance is Quest quest && quest.IsOngoing)
            MessageBroker.Instance.Publish(quest, new ConquestQuestFinalizing(quest));
    }
}

[HarmonyPatch(typeof(Quest), "OnTimedOut")]
internal class ConquestQuestTimeoutPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch(typeof(Quest), "QuestAcceptedConsequences")]
internal class ConquestQuestAcceptancePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance) => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (QuestSolutionStartAuthorityGuard.IsActive && __instance.JournalEntries.Count == 0);
}

[HarmonyPatch]
internal class ConquestIssueFinalizationPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
        yield return AccessTools.DeclaredMethod(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord));
    }

    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IssueFinalizeAuthorityGuard __state)
    {
        __state = null;
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return AllowedThread.IsThisThreadAllowed();
        __state = new IssueFinalizeAuthorityGuard();
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IssueFinalizeAuthorityGuard __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "OnIssueUpdated")]
internal class ConquestIssueRewardsPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue, ref Hero issueSolver)
    {
        // Relation and perk rewards arrive through their authoritative action messages.
        if (issue is Issue && ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate())
            issueSolver = null;
    }
}

[HarmonyPatch(typeof(IssueBase), "get_IssueQuestCanBeDuplicated")]
internal class ConquestQuestDuplicatePatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, ref bool __result)
    {
        if (__instance is Issue && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() &&
            ContainerProvider.TryResolve<IConquestQuest>(out var service))
            __result = !service.HasQuest(Hero.MainHero);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class ConquestPlayerChangedPatch
{
    [ThreadStatic] internal static bool IsChangingPlayer;
    [ThreadStatic] internal static Hero OldPlayer;
    [ThreadStatic] internal static Hero NewPlayer;

    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, Hero newPlayer, out (bool Changing, Hero Old, Hero New) __state)
    {
        __state = (IsChangingPlayer, OldPlayer, NewPlayer);
        IsChangingPlayer = true;
        OldPlayer = oldPlayer;
        NewPlayer = newPlayer;
    }

    [HarmonyFinalizer]
    private static void Finalizer((bool Changing, Hero Old, Hero New) __state)
    {
        IsChangingPlayer = __state.Changing;
        OldPlayer = __state.Old;
        NewPlayer = __state.New;
    }
}

[HarmonyPatch(typeof(TraitLevelingHelper), "AddTraitXp")]
internal class ConquestQuestTraitPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TraitObject trait, int xpAmount)
    {
        if (ModInformation.IsClient || !ConquestQuestEventPatches.IsApplying) return true;
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ApplyOwnerTraitXp(Hero.MainHero, trait, xpAmount);
        return false;
    }
}
