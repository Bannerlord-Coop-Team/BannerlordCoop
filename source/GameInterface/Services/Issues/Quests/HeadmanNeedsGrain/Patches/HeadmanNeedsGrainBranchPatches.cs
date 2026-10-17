using Common;
using GameInterface.Services.Issues.Framework.Interface;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
internal class HeadmanNeedsGrainBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static void AcceptPrefix()
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits)) traits.RequestSeed();
    }

    [HarmonyPatch(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply))]
    [HarmonyPostfix]
    private static void RestoreTraitsPostfix(Hero hero)
    {
        if (ModInformation.IsClient && hero == Hero.MainHero && ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits))
            traits.Restore(hero);
    }

    [HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.AddPlayerTraitXPAndLogEntry))]
    [HarmonyPostfix]
    private static void RefreshTraitsPostfix()
    {
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits)) traits.RefreshSeed();
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.Success))]
    [HarmonyPrefix]
    private static bool SuccessPrefix(Quest __instance, out bool __state)
    {
        var allowed = !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, (byte)HeadmanNeedsGrainBranch.DeliverGrain);
        __state = allowed && ModInformation.IsServer;
        return allowed;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.Success))]
    [HarmonyPostfix]
    private static void SuccessPostfix(Quest __instance, bool __state)
    {
        if (!__state || !ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits)) return;
        traits.Apply(DefaultTraits.Mercy, 70, __instance.QuestGiver, ActionNotes.QuestSuccess);
        traits.Apply(DefaultTraits.Generosity, 50, __instance.QuestGiver, ActionNotes.QuestSuccess);
    }

    [HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionEndWithSuccessConsequence))]
    [HarmonyPostfix]
    private static void AlternativeSuccessPostfix(Issue __instance)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits))
            traits.Apply(DefaultTraits.Generosity, 30, __instance.IssueOwner, ActionNotes.QuestSuccess);
    }

    // A dialogue-end callback has no ambient village on the dedicated server.
    [HarmonyPatch(typeof(Quest), nameof(Quest.Success))]
    [HarmonyTranspiler]
    private static System.Collections.Generic.IEnumerable<CodeInstruction> SuccessTranspiler(
        System.Collections.Generic.IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.CurrentSettlement));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getter))
            {
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                yield return CodeInstruction.Call(typeof(HeadmanNeedsGrainBranchPatches), nameof(GiverSettlement));
            }
            else yield return instruction;
        }
    }

    private static Settlement GiverSettlement(Quest quest) => quest.QuestGiver.CurrentSettlement;

    [HarmonyPatch(typeof(Quest), nameof(Quest.TimeoutFail))]
    [HarmonyPrefix]
    private static bool TimeoutFailPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.CriminalRatingFail))]
    [HarmonyPrefix]
    private static bool CrimePrefix(Quest __instance)
    {
        if (ModInformation.IsClient) return false;
        __instance.QuestGiver.AddPower(-10f);
        __instance.RelationshipChangeWithQuestGiver = -5;
        if (ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits))
            traits.Apply(DefaultTraits.Honor, -50, Hero.MainHero, ActionNotes.QuestFailed);

        var settlement = Hero.MainHero.PartyBelongedTo?.MapEvent?.MapEventSettlement ?? __instance.QuestGiver.CurrentSettlement;
        var log = new TextObject("{=9w1RbVEk}You are accused of a crime in {SETTLEMENT}, and {QUEST_GIVER.LINK} no longer wants your help.");
        log.SetTextVariable("SETTLEMENT", settlement.Name);
        log.SetCharacterProperties("QUEST_GIVER", __instance.QuestGiver.CharacterObject);
        __instance.CompleteQuestWithFail(log);
        return false;
    }

    [HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
    [HarmonyPrefix]
    private static bool AlternativeStartPrefix(Issue __instance)
    {
        if (ModInformation.IsClient)
        {
            if (ContainerProvider.TryResolve<IHeadmanNeedsGrainTraitRewards>(out var traits)) traits.RequestSeed();
            return false;
        }
        if (TaleWorlds.CampaignSystem.Hero.MainHero.Gold < __instance.AlternativeSolutionNeededGold)
            throw new System.InvalidOperationException("The grain solution owner cannot fund the companion");
        return true;
    }

    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.TryToMakeTroopsReturn))]
    [HarmonyPostfix]
    private static void ReturnPostfix(IssueBase issue)
    {
        if (ModInformation.IsClient || !(issue is Issue)) return;
        foreach (var troop in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (troop.Character.IsHero && troop.Character.HeroObject.HeroState == Hero.CharacterStates.Active)
            {
                var hero = troop.Character.HeroObject;
                MessageBroker.Instance.Publish(hero, new HeroStateChanged((int)hero.HeroState, hero));
            }
        }
    }
}
