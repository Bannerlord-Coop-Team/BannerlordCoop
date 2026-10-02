using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue;
using Quest = LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

[HarmonyPatch(typeof(LordWantsRivalCapturedIssueBehavior))]
internal class RivalCapturedGenerationPatches
{
    [HarmonyPatch(nameof(LordWantsRivalCapturedIssueBehavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool CheckForIssuePrefix() =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;

    [HarmonyPatch("ConditionsHold")]
    [HarmonyPrefix]
    private static bool ConditionsPrefix(Hero issueGiver, ref Hero targetHero, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var players) || !players.Contains(issueGiver.Clan)) return true;
        targetHero = null;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
internal class RivalCapturedStayAlivePatch
{
    private static void Postfix(Issue __instance, ref bool __result)
    {
        if (__result && ModInformation.IsServer && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() &&
            ContainerProvider.TryResolve<IPlayerManager>(out var players) && players.Contains(__instance.IssueOwner.Clan))
            __result = false;
    }
}

[HarmonyPatch]
internal class RivalCapturedWorldEventPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), "OnPrisonerTaken");
        yield return AccessTools.Method(typeof(Quest), "OnHeroPrisonerReleased");
        yield return AccessTools.Method(typeof(Quest), "OnWarDeclared");
        yield return AccessTools.Method(typeof(Quest), "OnClanChangedKingdom");
        yield return AccessTools.Method(typeof(Quest), "OnHeroKilled");
        yield return AccessTools.Method(typeof(Quest), "OnMapEventStarted");
    }

    private static bool Prefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsServer && __instance.IsOngoing &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests) &&
            quests.TryEnterOwnerScope(__instance, out __state);
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Quest))]
internal class RivalCapturedDialoguePatches
{
    [HarmonyPatch("QuestAcceptedConsequences")]
    [HarmonyPrefix]
    private static bool AcceptPrefix(Quest __instance)
    {
        return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            (QuestSolutionStartAuthorityGuard.IsActive && !__instance.IsOngoing);
    }

    [HarmonyPatch("FirstCounterOfferFinished")]
    [HarmonyPrefix]
    private static bool FirstCounterOfferPrefix(Quest __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.RequestChoice(__instance, RivalCapturedChoice.HearCounterOffer);
        return false;
    }

    [HarmonyPatch("QuestFailCounterOfferAccepted")]
    [HarmonyPrefix]
    private static bool CounterOfferAcceptedPrefix(Quest __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return IssueFinalizeAuthorityGuard.IsActive && __instance.IsOngoing;
        if (ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.RequestChoice(__instance, RivalCapturedChoice.AcceptCounterOffer);
        return false;
    }

    [HarmonyPatch("PlayerDeliveredPrisonerQuestSuccess")]
    [HarmonyPrefix]
    private static bool DeliveredPrefix(Quest __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.RequestChoice(__instance, Hero.OneToOneConversationHero == __instance.QuestGiver
                ? RivalCapturedChoice.DeliverToGiver : RivalCapturedChoice.DeliverToAgent);
        return false;
    }

    [HarmonyPatch("OnSettlementEntered")]
    [HarmonyPrefix]
    private static bool SettlementEnteredPrefix(Quest __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsClient && __instance.IsOngoing &&
            ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(__instance.QuestGiver);
    }

    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;

    [HarmonyPatch("OnPlayerBattleEventEnded")]
    [HarmonyPrefix]
    private static bool PlayerBattleEndedPrefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();

    [HarmonyPatch("CheckCancelConditions")]
    [HarmonyPrefix]
    private static bool CheckCancellationPrefix(Quest __instance, bool causedByPlayer)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer && __instance.IsOngoing &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests) &&
            quests.TryEnterOwnerScope(__instance, out var scope))
        {
            // Send the cancellation log before the generic issue-removal message.
            using (scope) quests.CheckCancellation(__instance, causedByPlayer);
        }
        return false;
    }
}

[HarmonyPatch]
internal class RivalCapturedCompletionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
    }

    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!quest.IsOngoing) return false;
        if (quest.QuestGiver?.Issue?.IssueQuest != quest && __originalMethod.Name == nameof(QuestBase.CompleteQuestWithCancel))
            return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (!ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests)) return false;
        if (RivalCapturedPlayerChangedPatch.OldPlayer != null &&
            !quests.IsOwnedBy(quest, RivalCapturedPlayerChangedPatch.OldPlayer)) return false;
        return quests.TryEnterOwnerScope(quest, out __state);
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class RivalCapturedIssueCancellationPatch
{
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (__instance.IssueQuest is Quest quest)
            return ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests) &&
                quests.TryEnterOwnerScope(quest, out __state);
        __state = new IssueFinalizeAuthorityGuard();
        return true;
    }

    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestBase))]
internal class RivalCapturedJournalPatches
{
    [HarmonyPatch(nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void AddLogPostfix(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.SendProgress(quest);
    }

    [HarmonyPatch("FinalizeQuest")]
    [HarmonyPrefix]
    private static void FinalizePrefix(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.SendProgress(quest);
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "OnIssueUpdated")]
internal class RivalCapturedIssueRewardsPatch
{
    private static bool Prefix(IssueBase issue)
    {
        return issue is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(MapEvent), nameof(MapEvent.OnBattleWon))]
internal class RivalCapturedBattleWonPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(MapEvent __instance)
    {
        if (ModInformation.IsServer && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.OnBattleWon(__instance);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal class RivalCapturedLoadedQuestsPatch
{
    private static void Prefix(QuestManager __instance)
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.RemoveOtherPlayersQuests(__instance);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class RivalCapturedPlayerChangedPatch
{
    [ThreadStatic]
    internal static Hero OldPlayer;

    private static void Prefix(Hero oldPlayer, out Hero __state)
    {
        __state = OldPlayer;
        OldPlayer = oldPlayer;
    }

    private static void Finalizer(Hero __state) => OldPlayer = __state;
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ReplacePlayer))]
internal class RivalCapturedPlayerReplacedPatch
{
    private static void Postfix(Player registeredPlayer, Player replacementPlayer, bool __result)
    {
        if (__result && ModInformation.IsServer && registeredPlayer.HeroId != replacementPlayer.HeroId &&
            ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests))
            quests.CancelReplacedPlayersQuests(registeredPlayer.ControllerId);
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class RivalCapturedEligibilityPatches
{
    [HarmonyPatch("IssueQuestCanBeDuplicated", MethodType.Getter)]
    [HarmonyPostfix]
    private static void DuplicateTypePostfix(IssueBase __instance, ref bool __result)
    {
        if (__instance is Issue && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) __result = true;
    }

    [HarmonyPatch("CheckPreconditions")]
    [HarmonyPostfix]
    private static void PreconditionsPostfix(IssueBase __instance, ref bool __result, ref TextObject explanation)
    {
        if (__instance is not Issue issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
            !ContainerProvider.TryResolve<ILordWantsRivalCapturedQuestService>(out var quests) ||
            !quests.HasOtherPersonalQuest(issue)) return;

        __result = false;
        if (!issue.IssueOwner.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            explanation.SetTextVariable("EXPLANATION", new TextObject("{=HvY7wjHt}I don't think you can help me. I think you may have other, similar commitments that could interfere."));
    }
}
