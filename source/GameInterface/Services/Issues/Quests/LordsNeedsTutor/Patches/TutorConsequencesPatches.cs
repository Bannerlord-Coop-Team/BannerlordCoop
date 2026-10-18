using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor.Patches;

[HarmonyPatch(typeof(Quest))]
internal class TutorConsequencesPatches
{
    [HarmonyPatch(nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool AcceptPrefix(Quest __instance)
    {
        if (__instance._startQuestLog != null) return false;
        if (ModInformation.IsServer) return true;
        __instance.StartQuest();
        __instance._youngHeroBeginningSkillPoints = __instance._youngHero.HeroDeveloper.GetTotalSkillPoints();
        __instance._targetSkillGain = __instance._youngHeroBeginningSkillPoints + 30;
        var task = new TextObject("{=8GbGTDtL}{YOUNG_HERO.LINK}'s experience progress");
        Helpers.StringHelpers.SetCharacterProperties("YOUNG_HERO", __instance._youngHero.CharacterObject, task);
        __instance._startQuestLog = __instance.AddDiscreteLog(__instance.PlayerStartsQuestLogText, task, 0, 30);
        __instance._checkForMissionEnd = true;
        foreach (var skill in Skills.All) __instance._oldSkillValues[skill] = __instance._youngHero.GetSkillValue(skill);
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizePrefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (ModInformation.IsClient) return false;
        if (ContainerProvider.TryResolve<ITutorJournal>(out var journal)) journal.Send(__instance);
        if (__instance._startQuestLog == null) return false;
        // Vanilla cancels a dead giver's quest outside any owner scope, but the pupil cleanup checks the owner's clan.
        var issue = IssueManager.GetIssueOfQuest(__instance);
        Hero owner = null;
        if (issue != null && ContainerProvider.TryResolve<IIssueOwnerResolver>(out var resolver))
            resolver.TryResolveOwnerHero(issue, out owner);
        if (owner == null) owner = __instance._youngHero.CompanionOf?.Leader;
        if (owner != null && owner != Hero.MainHero)
            __state = new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo);
        return true;
    }

    [HarmonyPatch(nameof(Quest.OnFinalize))]
    [HarmonyFinalizer]
    private static void FinalizeFinalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPatch(nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            __instance.AddLog(__instance.FailTimedOutQuestLogText);
            __instance.RelationshipChangeWithQuestGiver = -10;
            __instance._showQuestFailedConversation = true;
        }
        else if (Settlement.CurrentSettlement == null && PlayerEncounter.Current == null && MapEvent.PlayerMapEvent == null &&
            GameStateManager.Current.ActiveState is MapState)
        {
            CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty),
                new ConversationCharacterData(__instance._youngHero.CharacterObject, PartyBase.MainParty));
        }
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyPrefix]
    private static void SuccessLogPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) __instance.AddLog(__instance.SuccessQuestLogText);
    }

    [HarmonyPatch(nameof(Quest.QuestCompletedWithSuccessAfterConversation))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RewardTranspiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(
        AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)),
        AccessTools.Method(typeof(TutorConsequencesPatches), nameof(NotifyReward)));

    private static void NotifyReward(TextObject message, int priority, BasicCharacterObject character, Equipment equipment, string sound)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ITutorJournal>(out var journal)) journal.NotifyReward(message);
    }

    [HarmonyPatch(MethodType.Constructor, new[] { typeof(string), typeof(Hero), typeof(Hero) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions) => instructions.MethodReplacer(
        AccessTools.Method(typeof(ChangeGovernorAction), nameof(ChangeGovernorAction.RemoveGovernorOf)),
        AccessTools.Method(typeof(TutorConsequencesPatches), nameof(RemoveGovernor)));

    private static void RemoveGovernor(Hero hero)
    {
        if (ModInformation.IsServer) ChangeGovernorAction.RemoveGovernorOf(hero);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
internal class TutorMirrorStartPatch
{
    private static bool Prefix(QuestBase __instance) => __instance is not Quest || ModInformation.IsClient;
}

[HarmonyPatch]
internal class TutorDialogueBranchPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.FirstConversationEndConsequence));
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.QuestCompletedWithSuccessAfterConversation));
        yield return typeof(Quest).GetMethods(AccessTools.all).Single(method =>
            method.Name.StartsWith("<GetYoungHeroSecondDialogFlow>b__", StringComparison.Ordinal) &&
            !method.IsStatic && method.GetParameters().Length == 0);
    }

    private static bool Prefix(Quest __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name == nameof(Quest.QuestCompletedWithSuccessAfterConversation))
        {
            if (ModInformation.IsServer)
                return __instance.IsOngoing && __instance._youngHero.IsAlive && __instance._firstConversationInitialized &&
                    __instance._youngHero.HeroDeveloper.GetTotalSkillPoints() >= __instance._targetSkillGain;
            return ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) &&
                dispatcher.BeforeQuestBranch(__instance, (byte)LordsNeedsTutorBranch.Success);
        }
        if (ModInformation.IsServer) return true;
        var branch = __originalMethod.Name == nameof(Quest.FirstConversationEndConsequence)
            ? LordsNeedsTutorBranch.Introduction : LordsNeedsTutorBranch.Rejoin;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dialogueDispatcher)) return false;
        if (branch == LordsNeedsTutorBranch.Introduction) __instance._firstConversationInitialized = true;
        return dialogueDispatcher.BeforeQuestBranch(__instance, (byte)branch);
    }
}

[HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTick))]
internal class TutorPresentationTickPatch
{
    private static bool Prefix() => ModInformation.IsClient;
}
