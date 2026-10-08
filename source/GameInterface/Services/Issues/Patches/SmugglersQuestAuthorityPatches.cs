using Common;
using GameInterface.Policies;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.Smugglers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Quest = SmugglersIssueBehavior.SmugglersIssueQuest;

[HarmonyPatch(typeof(SmugglersIssueBehavior), "OnCheckForIssue")]
internal class SmugglersGenerationScopePatch
{
    [HarmonyPrefix]
    private static bool Prefix(Hero hero, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        if (hero.IsLord && !hero.Clan.IsPlayerClan()
            && ContainerProvider.TryResolve<IPlayerManager>(out var players)
            && ContainerProvider.TryResolve<IObjectManager>(out var objects))
        {
            foreach (var player in players.Players)
            {
                if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var playerHero)
                    || playerHero.GetRelation(hero) < -10f) continue;
                if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) continue;
                __state = new MainHeroSubstitutionScope(playerHero, party);
                return true;
            }
        }
        Campaign.Current.IssueManager.AddPotentialIssueData(hero,
            new PotentialIssueData(typeof(SmugglersIssueBehavior.SmugglersIssue), IssueBase.IssueFrequency.Rare));
        return false;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class SmugglersQuestOwnerEventPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), "OnMapEventEnded");
        yield return AccessTools.Method(typeof(Quest), "OnWarDeclared");
        yield return AccessTools.Method(typeof(Quest), "OnClanChangedKingdom");
        yield return AccessTools.Method(typeof(Quest), "OnSettlementOwnerChanged");
    }

    [HarmonyPrefix]
    private static bool OwnerEventPrefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient || !__instance.IsOngoing) return false;
        return ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            && authority.TryOpenOwnerScope(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void OwnerEventFinalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class SmugglersQuestWorldMutationPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), "OnHourlyTickParty");
        yield return AccessTools.Method(typeof(Quest), "OnFinalize");
    }

    [HarmonyPrefix]
    private static bool WorldMutationPrefix() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}

[HarmonyPatch(typeof(Quest))]
internal class SmugglersQuestAuthorityPatches
{
    [HarmonyPatch("QuestAcceptedConsequences")]
    [HarmonyPrefix]
    private static bool AcceptancePrefix(Quest __instance) => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
        || (ModInformation.IsServer && QuestSolutionStartAuthorityGuard.IsActive && __instance._smugglerParty == null);

    [HarmonyPatch("SucceedQuestWithBribe")]
    [HarmonyPrefix]
    private static bool BribePrefix(Quest __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return IssueFinalizeAuthorityGuard.IsActive;
        if (!ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority) || !authority.IsLocalOwner(__instance)) return false;
        if (Hero.MainHero.Gold < __instance.BribeAmount) return false;

        PlayerEncounter.LeaveEncounter = true;
        SmugglersQuestType.RequestSuccess(__instance, SmugglersQuestType.BribeProof);
        return false;
    }

    [HarmonyPatch("SucceedQuest")]
    [HarmonyPrefix]
    private static bool SuccessPrefix(Quest __instance, TextObject log)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return IssueFinalizeAuthorityGuard.IsActive;
        if (log?.GetID() == __instance.QuestSuccessWithPersuasionLog.GetID())
        {
            SmugglersQuestType.RequestSuccess(__instance, SmugglersQuestType.PersuasionProof);
        }
        return false;
    }

    [HarmonyPatch("FailQuest")]
    [HarmonyPrefix]
    private static bool FailurePrefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
        || (ModInformation.IsServer && IssueFinalizeAuthorityGuard.IsActive);
}

[HarmonyPatch]
internal class SmugglersQuestCompletionPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
    }

    [HarmonyPrefix]
    private static bool CompletionPrefix(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (SmugglersQuestLoadedTracksPatch.IsLoading
            && __originalMethod.Name == nameof(QuestBase.CompleteQuestWithCancel)
            && Campaign.Current.IssueManager.Issues.All(entry => entry.Value.IssueQuest != __instance)) return true;
        if (SmugglersPlayerCharacterChangeScope.IsActive)
        {
            if (ModInformation.IsClient
                || !ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)
                || !SmugglersPlayerCharacterChangeScope.Affects(__instance, owners)) return false;
        }
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (!__instance.IsOngoing) return false;
        if (IssueFinalizeAuthorityGuard.IsActive) return true;
        return ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            && authority.TryOpenOwnerScope(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void CompletionFinalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class SmugglersPlayerCharacterChangePatch
{
    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, out SmugglersPlayerCharacterChangeScope __state)
    {
        __state = new SmugglersPlayerCharacterChangeScope(oldPlayer);
    }

    [HarmonyFinalizer]
    private static void Finalizer(SmugglersPlayerCharacterChangeScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut))]
internal class SmugglersQuestTimeoutPatch
{
    [ThreadStatic]
    internal static Quest CurrentQuest;

    [HarmonyPrefix]
    private static bool TimeoutPrefix(QuestBase __instance, out (Quest Previous, IDisposable Scope) __state)
    {
        __state = (CurrentQuest, null);
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (!quest.IsOngoing || !ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            || !authority.TryOpenOwnerScope(quest.QuestGiver, out var scope)) return false;
        __state = (CurrentQuest, scope);
        CurrentQuest = quest;
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((Quest Previous, IDisposable Scope) __state)
    {
        CurrentQuest = __state.Previous;
        __state.Scope?.Dispose();
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail))]
internal class SmugglersTimeoutFailurePatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, TextObject cancelLog)
    {
        if (__instance != SmugglersQuestTimeoutPatch.CurrentQuest) return true;
        // OnTimedOut still applies its penalties; the outer call owns the one timeout finalization.
        if (cancelLog != null) __instance.AddLog(cancelLog);
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class SmugglersIssueCancellationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, TextObject log, out IDisposable __state)
    {
        __state = null;
        if (__instance is not SmugglersIssueBehavior.SmugglersIssue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (IssueFinalizeAuthorityGuard.IsActive)
        {
            if (!ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var removedOwner)
                || !removedOwner.IsRemovedOwner(__instance)) return true;
            CancelAfterOwnerRemoval(__instance, log);
            return false;
        }
        if (__instance.IsOngoingWithoutQuest)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        if (!ContainerProvider.TryResolve<ISmugglersQuestAuthority>(out var authority)
            || !authority.TryOpenOwnerScope(__instance.IssueOwner, out __state)) return false;
        if (__instance.IssueQuest is not { IsOngoing: true } quest) return true;
        // Quest completion already calls back into IssueManager to remove the issue.
        quest.CompleteQuestWithCancel(log);
        return false;
    }

    private static void CancelAfterOwnerRemoval(IssueBase issue, TextObject log)
    {
        if (issue.IssueQuest is { IsOngoing: true } quest)
        {
            quest.CompleteQuestWithCancel(log);
            return;
        }
        if (issue.IsSolvingWithAlternative)
        {
            issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("{=V5Za6d4h}Your troops have returned from their mission.")));
            Campaign.Current.IssueManager.TryToMakeTroopsReturn(issue);
        }
        // Vanilla cancellation reads MainHero before dispatch; a removed character has no solver.
        CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueCancel, null);
        issue.IssueFinalized();
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
