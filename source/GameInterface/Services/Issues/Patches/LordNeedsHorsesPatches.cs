using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.LordNeedsHorses;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

[HarmonyPatch]
internal class LordNeedsHorsesAcceptanceConversationPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.DoOption), new[] { typeof(int) });
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.DoOptionContinue));
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.ContinueConversation));
    }

    [HarmonyPrefix]
    internal static bool Prefix(ConversationManager __instance) =>
        !LordNeedsHorsesQuestType.PendingAcceptances.TryGetValue(__instance, out _);
}

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal class LordNeedsHorsesConversationEndPatch
{
    [HarmonyPostfix]
    internal static void Postfix(ConversationManager __instance) => LordNeedsHorsesQuestType.PendingAcceptances.Remove(__instance);
}

[HarmonyPatch]
internal class LordNeedsHorsesDifficultyPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(IssueBase), nameof(IssueBase.StartIssueWithQuest));
        yield return AccessTools.DeclaredMethod(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution));
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(AccessTools.Field(typeof(IssueBase), "_issueDifficultyMultiplier")))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(LordNeedsHorsesDifficultyPatch), nameof(SetAcceptedDifficulty));
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("Issue acceptance does not match the installed difficulty assignment");
    }

    internal static void SetAcceptedDifficulty(IssueBase issue, float difficulty)
    {
        // Keep the synchronized offer's troop count, duration and wages through acceptance.
        if (issue is not Issue || issue._issueDifficultyMultiplier == 0f)
            issue._issueDifficultyMultiplier = difficulty;
    }
}

[HarmonyPatch(typeof(Quest), "InitializeQuestOnGameLoad")]
internal class LordNeedsHorsesLoadPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        int replaced = 0;
        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].Calls(AccessTools.DeclaredMethod(typeof(Quest), "GetNumQuestMountsInInventory")))
            {
                code[i].opcode = OpCodes.Call;
                code[i].operand = AccessTools.Method(typeof(LordNeedsHorsesLoadPatch), nameof(SavedMountCount));
                replaced++;
            }
            else if (i + 2 < code.Count &&
                code[i].Calls(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty))) &&
                code[i + 1].Calls(AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.ItemRoster))) &&
                code[i + 2].Calls(AccessTools.PropertyGetter(typeof(ItemRoster), nameof(ItemRoster.VersionNo))))
            {
                // Player registrations arrive after quest loading; the first owned tick refreshes inventory.
                code[i].opcode = OpCodes.Ldc_I4_M1;
                code[i].operand = null;
                code[i + 1].opcode = code[i + 2].opcode = OpCodes.Nop;
                code[i + 1].operand = code[i + 2].operand = null;
                replaced++;
            }
        }
        if (replaced != 2) throw new InvalidOperationException("Horse quest loading does not match the installed inventory reads");
        return code;
    }

    internal static int SavedMountCount(Quest quest) => (quest._questJournalEntry ??
        quest.JournalEntries.FirstOrDefault(log => log.Range == quest._numMountsToBeDelivered && log.Type == LogType.Discreate))
        ?.CurrentProgress ?? 0;
}

[HarmonyPatch(typeof(Quest))]
internal class LordNeedsHorsesQuestPatches
{
    [HarmonyPatch("OnQuestAccepted")]
    [HarmonyPrefix]
    private static bool AcceptOnce(Quest __instance) => __instance._questJournalEntry == null &&
        (QuestSolutionStartAuthorityGuard.IsActive || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate());

    [HarmonyPatch("OnQuestDeclined")]
    [HarmonyPrefix]
    private static bool Decline(Quest __instance)
    {
        if (IssueFinalizeAuthorityGuard.IsActive || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            service.RequestOutcome(__instance, IssueFinalizeReason.QuestFail);
        return false;
    }

    [HarmonyPatch("GetNumQuestMountsInInventory")]
    [HarmonyPrefix]
    private static bool Inventory(Quest __instance, ref int __result)
    {
        if (QuestSolutionStartAuthorityGuard.IsActive || ModInformation.IsServer ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service) && service.IsLocalOwner(__instance.QuestGiver)) return true;
        __result = 0;
        return false;
    }

    [HarmonyPatch("OnSettlementEntered")]
    [HarmonyPrefix]
    private static bool CommanderConversation(Quest __instance) =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service) && service.IsLocalOwner(__instance.QuestGiver));

    [HarmonyPatch("CheckAndHandleQuestSuccessConditions")]
    [HarmonyPrefix]
    private static void BeforeProgress(Quest __instance, out int __state) => __state = __instance._numMountsInInventory;

    [HarmonyPatch("CheckAndHandleQuestSuccessConditions")]
    [HarmonyPostfix]
    private static void AfterProgress(Quest __instance, int __state)
    {
        if (ModInformation.IsServer && __state != __instance._numMountsInInventory && __instance.QuestGiver.Issue is Issue issue)
            MessageBroker.Instance.Publish(issue, new LordNeedsHorsesJournalChanged(issue));
    }
}

[HarmonyPatch(typeof(QuestBase))]
internal class LordNeedsHorsesTerminalPatches
{
    [HarmonyPatch(nameof(QuestBase.CompleteQuestWithSuccess))]
    [HarmonyPrefix]
    private static bool Deliver(QuestBase __instance)
    {
        if (__instance is not Quest quest || IssueFinalizeAuthorityGuard.IsActive ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            service.RequestOutcome(quest, IssueFinalizeReason.QuestSuccess);
        return false;
    }

    [HarmonyPatch("FinalizeQuest")]
    [HarmonyPrefix]
    private static void RetainJournal(QuestBase __instance)
    {
        if (ModInformation.IsServer && __instance is Quest && __instance.QuestGiver.Issue is Issue issue)
            MessageBroker.Instance.Publish(issue, new LordNeedsHorsesJournalChanged(issue));
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class LordNeedsHorsesPlayerChangePatch
{
    [ThreadStatic] internal static Hero PreviousPlayer;

    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, out Hero __state)
    {
        __state = PreviousPlayer;
        PreviousPlayer = oldPlayer;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Hero __state) => PreviousPlayer = __state;
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
internal class LordNeedsHorsesFundingPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => (ModInformation.IsServer && AlternativeSolutionStartAuthorityGuard.IsActive) ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();

    [HarmonyPostfix]
    private static void PayWages(Issue __instance)
    {
        if (ModInformation.IsClient || !AlternativeSolutionStartAuthorityGuard.IsActive) return;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            throw new InvalidOperationException("Horse quest troop selection service is unavailable");
        Hero.MainHero.ChangeHeroGold(-service.AlternativeTroopWages(__instance, __instance.AlternativeSolutionSentTroops));
        CampaignEventDispatcher.Instance.OnHeroGetsBusy(__instance.AlternativeSolutionHero, HeroGetsBusyReasons.SolvesIssue);
    }
}

[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize))]
internal class LordNeedsHorsesTroopSelectionPatch
{
    [HarmonyPrefix]
    private static void Prefix(PartyScreenLogic __instance, ref PartyScreenLogicInitializationData initializationData)
    {
        if (ModInformation.IsClient && Hero.OneToOneConversationHero?.Issue is Issue issue &&
            ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            service.PrepareTroopSelection(issue, __instance, ref initializationData);
    }

    [HarmonyPostfix]
    private static void DetachResetState(PartyScreenLogic __instance)
    {
        if (!LordNeedsHorsesQuestType.TroopSelections.TryGetValue(__instance, out _)) return;
        __instance.CurrentData.RightItemRoster = new ItemRoster(__instance.CurrentData.RightItemRoster);
        __instance.CurrentData.RightParty = null;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "issue_offer_player_accept_alternative_3_consequence")]
internal class LordNeedsHorsesCompanionSelectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (ModInformation.IsServer || Hero.OneToOneConversationHero?.Issue is not Issue issue) return true;
        if (!issue.IsOngoingWithoutQuest) return false;
        if (ConversationSentence.SelectedRepeatObject is Hero hero && hero.PartyBelongedTo == MobileParty.MainParty)
        {
            issue.AlternativeSolutionSentTroops.Clear();
            issue.AlternativeSolutionSentTroops.AddToCounts(hero.CharacterObject, 1);
        }
        return false;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "issue_offer_player_accept_alternative_5_b_consequence")]
internal class LordNeedsHorsesCompanionSelectionCancelPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (ModInformation.IsServer || Hero.OneToOneConversationHero?.Issue is not Issue issue) return true;
        if (issue.IsOngoingWithoutQuest) issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.DoTroopsSatisfyAlternativeSolution))]
internal class LordNeedsHorsesAlternativeTroopValidationPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, TroopRoster troopRoster, ref bool __result, ref TextObject explanation)
    {
        if (!__result || __instance is not Issue issue || !AlternativeSolutionStartAuthorityGuard.IsActive) return;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            throw new InvalidOperationException("Horse quest troop selection service is unavailable");
        __result = service.ValidateAlternativeTroops(issue, troopRoster, out explanation);
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class LordNeedsHorsesAlternativeCompletionPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (__instance is not Issue || AlternativeSolutionCompletionAuthorityGuard.IsActive ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer && __instance.IsSolvingWithAlternative && __instance.AlternativeSolutionReturnTimeForTroops.IsPast)
            AlternativeSolutionCompletionRunner.CompleteOnServer(__instance.IssueOwner, __instance);
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), "get_IssueQuestCanBeDuplicated")]
internal class LordNeedsHorsesDuplicateGatePatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, ref bool __result)
    {
        if (__instance is Issue && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) __result = true;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CheckPreconditions))]
internal class LordNeedsHorsesEligibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, Hero issueGiver, ref bool __result, ref TextObject explanation)
    {
        if (__instance is not Issue issue || !issue.IsOngoingWithoutQuest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service) && !service.HasConflictingQuest(issue)) return;
        // Vanilla ranks the stay-alive cancel and then the at-war refusal above the duplicate one, which outranks the rest.
        if (!__result && (!issue.IssueStayAliveConditions() || issueGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))) return;
        __result = false;
        explanation = new TextObject("{=HvY7wjHt}I don't think you can help me. I think you may have other, similar commitments that could interfere.");
    }
}

[HarmonyPatch(typeof(LordNeedsHorsesIssueBehavior), nameof(LordNeedsHorsesIssueBehavior.ConditionsHold))]
internal class LordNeedsHorsesPlayerClanGiverPatch
{
    // Vanilla excludes only Clan.PlayerClan, which is no co-op player's clan on the server.
    [HarmonyPostfix]
    private static void Postfix(Hero issueGiver, ref bool __result)
    {
        if (__result && issueGiver.Clan.IsPlayerClan()) __result = false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
internal class LordNeedsHorsesPlayerClanStayAlivePatch
{
    [HarmonyPostfix]
    private static void Postfix(Issue __instance, ref bool __result)
    {
        if (__result && __instance.IssueOwner.Clan.IsPlayerClan()) __result = false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionCondition))]
internal class LordNeedsHorsesAlternativeEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Issue __instance, ref bool __result, ref TextObject explanation)
    {
        if (!AlternativeSolutionStartAuthorityGuard.IsActive ||
            __instance.CheckPreconditions(__instance.IssueOwner, out explanation)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
internal class LordNeedsHorsesIssueJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue issue)
            MessageBroker.Instance.Publish(issue, new LordNeedsHorsesJournalChanged(issue));
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class LordNeedsHorsesDailyJournalPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in __instance.Issues)
        {
            if (entry.Value is Issue { IsSolvingWithAlternative: true } issue)
                MessageBroker.Instance.Publish(issue, new LordNeedsHorsesJournalChanged(issue));
        }
    }
}

[HarmonyPatch(typeof(QuestHelper), nameof(QuestHelper.ApplyGenericMinorMajorCoercionConsequences))]
internal class LordNeedsHorsesCoercionTraitPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase quest, out IDisposable __state)
    {
        __state = null;
        if (quest is not Quest || ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
            throw new InvalidOperationException("Horse quest coercion service is unavailable");
        __state = service.SelectCoercionTraitProgress(quest.QuestGiver);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal class LordNeedsHorsesIssueCancelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out (IDisposable Player, IDisposable Finalization) __state)
    {
        __state = default;
        if (__instance is not Issue || IssueFinalizeAuthorityGuard.IsActive ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service)) return false;
        IDisposable playerScope = null;
        if ((__instance.IsSolvingWithAlternative || __instance.IsSolvingWithQuest) &&
            !service.TrySelectPlayer(__instance.IssueOwner, out playerScope)) return false;
        __state = (playerScope, new IssueFinalizeAuthorityGuard());
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable Player, IDisposable Finalization) __state)
    {
        __state.Finalization?.Dispose();
        __state.Player?.Dispose();
    }
}

[HarmonyPatch]
internal class LordNeedsHorsesProgressPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), "HourlyTick");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnPlayerInventoryExchange");
    }

    [HarmonyPrefix]
    private static bool Progress(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service)) return false;
        if (ModInformation.IsClient) return service.IsLocalOwner(__instance.QuestGiver);
        return service.TrySelectPlayer(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void ProgressFinished(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class LordNeedsHorsesWorldEventPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnWarDeclared");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnClanChangedKingdom");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnHeroPrisonerTaken");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnMapEventStarted");
    }

    [HarmonyPrefix]
    private static bool WorldEvent(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service)) return false;
        return service.TrySelectPlayer(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void WorldEventFinished(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal class LordNeedsHorsesConsequencesPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnCompleteWithSuccess");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnFailed");
        yield return AccessTools.DeclaredMethod(typeof(Quest), "OnTimedOut");
    }

    // The server has already sent inventory, gold, renown and relation changes before the terminal mirror.
    [HarmonyPrefix]
    private static bool Consequences() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}

[HarmonyPatch]
internal class LordNeedsHorsesFailurePatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), "CompleteQuestWithFail");
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), "CompleteQuestWithCancel");
        yield return AccessTools.DeclaredMethod(typeof(QuestBase), "CompleteQuestWithTimeOut");
    }

    [HarmonyPrefix]
    private static bool Terminate(QuestBase __instance, out (IDisposable Player, IDisposable Finalization) __state)
    {
        __state = default;
        if (__instance is not Quest quest || IssueFinalizeAuthorityGuard.IsActive ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return false;
        if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service)) return false;
        if (LordNeedsHorsesPlayerChangePatch.PreviousPlayer != null &&
            (!service.TryResolvePlayer(quest.QuestGiver, out var hero, out _) || hero != LordNeedsHorsesPlayerChangePatch.PreviousPlayer))
            return false;
        if (!service.TrySelectPlayer(quest.QuestGiver, out var playerScope)) return false;
        __state = (playerScope, new IssueFinalizeAuthorityGuard());
        return true;
    }

    [HarmonyFinalizer]
    private static void Terminated((IDisposable Player, IDisposable Finalization) __state)
    {
        __state.Finalization?.Dispose();
        __state.Player?.Dispose();
    }
}
