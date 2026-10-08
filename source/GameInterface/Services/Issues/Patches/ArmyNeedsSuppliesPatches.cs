using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Handlers;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Library;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

[HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
internal class ArmyNeedsSuppliesAcceptConsequencePatch
{
    private static bool Prefix(Quest __instance) => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ModInformation.IsServer && QuestSolutionStartAuthorityGuard.IsActive && __instance._grainLog == null);
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Quests), MethodType.Getter)]
internal class ArmyNeedsSuppliesPersonalJournalPatch
{
    internal static void Postfix(ref MBReadOnlyList<QuestBase> __result)
    {
        // Save repair relies on backing-list indexes; only the journal view gets a filtered copy.
        if (!ArmyNeedsSuppliesJournalViewPatch.IsBuilding ||
            !ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service)) return;
        __result = new MBList<QuestBase>(__result.Where(quest => quest is not Quest supplies || service.IsLocalOwner(supplies)));
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal class ArmyNeedsSuppliesJournalViewPatch
{
    [ThreadStatic] internal static bool IsBuilding;

    internal static void Prefix(out bool __state)
    {
        __state = IsBuilding;
        IsBuilding = ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }

    internal static void Finalizer(bool __state) => IsBuilding = __state;
}

[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal class ArmyNeedsSuppliesOldJournalPatch
{
    internal static bool Prefix(JournalLogEntry __instance, ref bool __result)
    {
        if (!ArmyNeedsSuppliesJournalViewPatch.IsBuilding ||
            !ContainerProvider.TryResolve<IArmyNeedsSuppliesJournalOwners>(out var owners) || owners.IsVisible(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class ArmyNeedsSuppliesJournalOwnershipSavePatch
{
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IArmyNeedsSuppliesJournalOwners>(out var owners)) owners.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(Quest))]
internal class ArmyNeedsSuppliesDeliveryPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CollectedGrainConsequence));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CollectedGrainAndLiveStockConsequence));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CollectedGrainAndWineConsequence));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CollectedEverythingConsequence));
    }

    private static bool Prefix(Quest __instance, MethodBase __originalMethod)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return IssueFinalizeAuthorityGuard.IsActive;
        if (!ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) || !service.IsLocalOwner(__instance) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controller)) return false;

        var proof = __originalMethod.Name switch
        {
            nameof(Quest.CollectedGrainConsequence) => ArmyNeedsSuppliesDelivery.Grain,
            nameof(Quest.CollectedGrainAndLiveStockConsequence) => ArmyNeedsSuppliesDelivery.GrainAndLivestock,
            nameof(Quest.CollectedGrainAndWineConsequence) => ArmyNeedsSuppliesDelivery.GrainAndWine,
            _ => ArmyNeedsSuppliesDelivery.Everything,
        };
        var previous = QuestSuccessProofContext.Current;
        QuestSuccessProofContext.Set(proof);
        try
        {
            MessageBroker.Instance.Publish(__instance, new QuestTerminalOutcomeTriggered(
                __instance.QuestGiver, controller.ControllerId, IssueFinalizeReason.QuestSuccess));
        }
        finally { QuestSuccessProofContext.Set(previous); }
        return false;
    }
}

[HarmonyPatch(typeof(Quest))]
internal class ArmyNeedsSuppliesDeliveryConditionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CheckIfPlayerCollectedGrain));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CheckIfPlayerCollectedGrainLiveStock));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CheckIfPlayerCollectedGrainWine));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.CheckIfPlayerCollectedEverything));
    }

    internal static bool Prefix(Quest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) && service.IsLocalOwner(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Quest))]
internal class ArmyNeedsSuppliesWorldEventPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnWarDeclared));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnArmyDispersed));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnClanChangedKingdom));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.HourlyTick));
    }

    private static bool Prefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsServer && __instance.IsOngoing &&
            ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) &&
            service.TryOpenOwnerScope(__instance, out __state);
    }

    private static void Postfix(Quest __instance, MethodBase __originalMethod, IDisposable __state)
    {
        if (__state == null || __originalMethod.Name != nameof(Quest.HourlyTick)) return;
        __instance.CalculateAndUpdateRequestedItemsCountInPlayer(false);
        MessageBroker.Instance.Publish(__instance, new ArmyNeedsSuppliesJournalChanged(__instance));
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Quest), nameof(Quest.CalculateAndUpdateRequestedItemsCountInPlayer))]
internal class ArmyNeedsSuppliesProgressPatch
{
    private static bool Prefix(Quest __instance, ref bool notifyPlayer, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer)
        {
            notifyPlayer = false;
            if (QuestSolutionStartAuthorityGuard.IsActive) return true;
            return ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) &&
                service.TryOpenOwnerScope(__instance, out __state);
        }
        return ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var local) && local.IsLocalOwner(__instance);
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestBase))]
internal class ArmyNeedsSuppliesTerminalPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
    }

    internal static bool Prefix(QuestBase __instance, out Tuple<IDisposable, IssueFinalizeAuthorityGuard> __state)
    {
        __state = null;
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!quest.IsOngoing) return false;
        if (IssueFinalizeAuthorityGuard.IsActive)
        {
            if (ModInformation.IsClient && ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var local) &&
                !local.IsLocalOwner(quest))
            {
                // Observer cleanup must not publish another player's completed personal quest.
                quest.FinalizeQuest();
                quest.AfterFinalize();
                quest.QuestGiver.Issue?.IssueFinalized();
                return false;
            }
            return true;
        }
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service)) return false;
        if (ArmyNeedsSuppliesCharacterChangePatch.OldPlayer != null &&
            !service.IsOwner(quest, ArmyNeedsSuppliesCharacterChangePatch.OldPlayer) &&
            !service.IsOwner(quest, ArmyNeedsSuppliesCharacterChangePatch.NewPlayer)) return false;
        if (!service.TryOpenOwnerScope(quest, out var scope)) return false;
        __state = Tuple.Create(scope, new IssueFinalizeAuthorityGuard());
        return true;
    }

    internal static void Finalizer(Tuple<IDisposable, IssueFinalizeAuthorityGuard> __state)
    {
        if (__state == null) return;
        __state.Item2.Dispose();
        __state.Item1.Dispose();
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.FinalizeQuest))]
internal class ArmyNeedsSuppliesFinalJournalPatch
{
    private static void Prefix(QuestBase __instance)
    {
        if (__instance is Quest quest && ModInformation.IsServer && !CallOriginalPolicy.IsOriginalAllowed())
            MessageBroker.Instance.Publish(quest, new ArmyNeedsSuppliesJournalChanged(quest));
    }
}

[HarmonyPatch(typeof(Quest))]
internal class ArmyNeedsSuppliesCompletionHookPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnCompleteWithSuccess));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnFailed));
    }

    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddDialogs))]
internal class ArmyNeedsSuppliesDialogPatch
{
    private static bool Prefix(QuestBase __instance)
    {
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) &&
            service.IsLocalOwner(quest);
    }
}

[HarmonyPatch(typeof(ArmyNeedsSuppliesIssueBehavior), nameof(ArmyNeedsSuppliesIssueBehavior.OnArmyDispersed))]
internal class ArmyNeedsSuppliesIssueDispersalPatch
{
    private static bool Prefix(Army army)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        // The quest listener must retain the issue long enough to record its cancellation.
        return ModInformation.IsServer && army.ArmyOwner?.Issue?.IssueQuest == null;
    }
}

[HarmonyPatch(typeof(ArmyNeedsSuppliesIssueBehavior), nameof(ArmyNeedsSuppliesIssueBehavior.OnCheckForIssue))]
internal class ArmyNeedsSuppliesCreationPatch
{
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal class ArmyNeedsSuppliesIssueRewardsPatch
{
    private static bool Prefix(IssueBase issue) => issue is not Issue ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), nameof(JournalLogsCampaignBehavior.OnIssueUpdated))]
internal class ArmyNeedsSuppliesIssueJournalPatch
{
    private static bool Prefix(IssueBase issue)
    {
        if (issue is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer) return true;
        return issue.IssueQuest is Quest quest && ContainerProvider.TryResolve<IArmyNeedsSuppliesQuest>(out var service) &&
            service.IsLocalOwner(quest);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class ArmyNeedsSuppliesCharacterChangePatch
{
    [ThreadStatic] internal static Hero OldPlayer;
    [ThreadStatic] internal static Hero NewPlayer;

    private static void Prefix(Hero oldPlayer, Hero newPlayer, out Tuple<Hero, Hero> __state)
    {
        __state = Tuple.Create(OldPlayer, NewPlayer);
        OldPlayer = oldPlayer;
        NewPlayer = newPlayer;
    }

    private static void Finalizer(Tuple<Hero, Hero> __state)
    {
        OldPlayer = __state.Item1;
        NewPlayer = __state.Item2;
    }
}
